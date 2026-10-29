using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FolderSlideShow
{
    internal sealed class PlayerButton : Button
    {
        public bool Selected { get; set; }
        public bool Primary { get; set; }
        private bool hover, pressed;
        public PlayerButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Primary ? Color.FromArgb(103, 229, 197) : Selected ? Color.FromArgb(30, 65, 62) : Color.FromArgb(31, 37, 47);
            if (hover) fill = ControlPaint.Light(fill, .12f);
            if (pressed) fill = ControlPaint.Dark(fill, .08f);
            float radius = Math.Max(8, Height * .24f), d = radius * 2;
            RectangleF r = new RectangleF(1, 1, Width - 3, Height - 3);
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); path.CloseFigure();
                using (Brush brush = new SolidBrush(fill)) e.Graphics.FillPath(brush, path);
                using (Pen pen = new Pen(Selected ? Color.FromArgb(56, 113, 101) : Color.FromArgb(48, 56, 68)))
                    if (!Primary) e.Graphics.DrawPath(pen, path);
            }
            Color ink = Primary ? Color.FromArgb(12, 39, 34) : Selected ? Color.FromArgb(134, 238, 214) : Color.FromArgb(221, 227, 235);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    internal sealed class PlayerSlider : Control
    {
        private int value, maximum = 100;
        private bool hover;
        public int Minimum { get; set; }
        public int Maximum { get { return maximum; } set { maximum = Math.Max(Minimum + 1, value); Invalidate(); } }
        public int Value
        {
            get { return value; }
            set { int next = Math.Max(Minimum, Math.Min(Maximum, value)); if (this.value == next) return; this.value = next; Invalidate(); if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); }
        }
        public event EventHandler ValueChanged;
        public PlayerSlider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            TabStop = false; Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.Slider;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        private void MoveTo(int x) { int pad = Math.Max(8, Height / 4); Value = Minimum + (int)Math.Round((double)(x - pad) / Math.Max(1, Width - pad * 2) * (Maximum - Minimum)); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Capture = true; MoveTo(e.X); } base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (Capture) MoveTo(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { if (e.Button == MouseButtons.Left && Capture) { MoveTo(e.X); Capture = false; } base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float pad = Math.Max(8, Height / 4), y = Height / 2f, end = Math.Max(pad, Width - pad);
            float x = pad + (end - pad) * (Value - Minimum) / (Maximum - Minimum);
            float line = Math.Max(3, Height / 10f);
            using (Pen track = new Pen(Color.FromArgb(50, 59, 72), line))
            using (Pen active = new Pen(Color.FromArgb(103, 229, 197), line))
            {
                track.StartCap = track.EndCap = active.StartCap = active.EndCap = LineCap.Round;
                e.Graphics.DrawLine(track, pad, y, end, y);
                if (x > pad) e.Graphics.DrawLine(active, pad, y, x, y);
            }
            if (Enabled && (hover || Capture))
            {
                float radius = Math.Max(5, Height / 6f);
                using (Brush brush = new SolidBrush(Color.FromArgb(225, 255, 247))) e.Graphics.FillEllipse(brush, x - radius, y - radius, radius * 2, radius * 2);
            }
        }
    }

    // Foreground-only raw mouse input reaches the form even when VLC owns the
    // video child window. No global hook, input suppression or background capture.
    internal sealed class MediaClickInput : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] private struct Device { public ushort Page, Usage; public uint Flags; public IntPtr Target; }
        [StructLayout(LayoutKind.Sequential)] private struct Header { public uint Type, Size; public IntPtr Device, Parameter; }
        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterRawInputDevices(Device[] devices, uint count, uint size);
        [DllImport("user32.dll")] private static extern uint GetRawInputData(IntPtr raw, uint command, IntPtr data, ref uint size, uint headerSize);
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")] private static extern bool IsChild(IntPtr parent, IntPtr child);
        private readonly Control surface;
        private readonly Action clicked;
        private readonly IntPtr buffer;
        private readonly int headerSize = Marshal.SizeOf(typeof(Header));
        private bool armed, registered;
        private Point down;
        public MediaClickInput(IntPtr window, Control surface, Action clicked)
        {
            this.surface = surface; this.clicked = clicked;
            buffer = Marshal.AllocHGlobal(headerSize + 24);
            registered = RegisterRawInputDevices(new Device[] { new Device { Page = 1, Usage = 2, Target = window } }, 1, (uint)Marshal.SizeOf(typeof(Device)));
        }
        private bool OverSurface(Point p)
        {
            if (!surface.Visible || !surface.IsHandleCreated) return false;
            IntPtr hit = WindowFromPoint(p);
            return hit == surface.Handle || IsChild(surface.Handle, hit);
        }
        public void Process(IntPtr raw)
        {
            uint size = (uint)(headerSize + 24);
            if (GetRawInputData(raw, 0x10000003, buffer, ref size, (uint)headerSize) != size || Marshal.ReadInt32(buffer) != 0) return;
            int flags = (ushort)Marshal.ReadInt16(buffer, headerSize + 4);
            if ((flags & 1) != 0) { down = Cursor.Position; armed = OverSurface(down); }
            if ((flags & 2) != 0)
            {
                Point up = Cursor.Position;
                bool fire = armed && OverSurface(up) && Math.Abs(up.X - down.X) <= SystemInformation.DragSize.Width && Math.Abs(up.Y - down.Y) <= SystemInformation.DragSize.Height;
                armed = false;
                if (fire) clicked();
            }
        }
        public void Dispose()
        {
            if (registered) RegisterRawInputDevices(new Device[] { new Device { Page = 1, Usage = 2, Flags = 1 } }, 1, (uint)Marshal.SizeOf(typeof(Device)));
            Marshal.FreeHGlobal(buffer);
        }
    }
}
