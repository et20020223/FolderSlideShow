using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;
using ComDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace FolderSlideShow
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ShellPoint { public int X, Y; }

    [ComVisible(true), Guid("00000122-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellDropTarget
    {
        [PreserveSig] int DragEnter([In, MarshalAs(UnmanagedType.Interface)] ComDataObject data, uint keys, ShellPoint point, ref uint effect);
        [PreserveSig] int DragOver(uint keys, ShellPoint point, ref uint effect);
        [PreserveSig] int DragLeave();
        [PreserveSig] int Drop([In, MarshalAs(UnmanagedType.Interface)] ComDataObject data, uint keys, ShellPoint point, ref uint effect);
    }

    // Explorer sends the entire selection in one IDataObject. This runs in our EXE,
    // never as managed code loaded into Explorer, and does not move/copy the files.
    [ComVisible(true), Guid("D923A8E6-8B39-4A2C-99F2-DB7BA7446B75"), ClassInterface(ClassInterfaceType.None)]
    public sealed class FolderDropTarget : IShellDropTarget
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint DragQueryFile(IntPtr drop, uint index, StringBuilder file, uint length);
        [DllImport("ole32.dll")] private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
        private bool accepts;
        private static FORMATETC FileDropFormat()
        {
            return new FORMATETC { cfFormat = 15, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_HGLOBAL };
        }
        public int DragEnter(ComDataObject data, uint keys, ShellPoint point, ref uint effect)
        {
            FORMATETC format = FileDropFormat();
            accepts = data != null && data.QueryGetData(ref format) == 0;
            effect = accepts ? effect & 1u : 0;
            return 0;
        }
        public int DragOver(uint keys, ShellPoint point, ref uint effect) { effect = accepts ? effect & 1u : 0; return 0; }
        public int DragLeave() { accepts = false; return 0; }
        public int Drop(ComDataObject data, uint keys, ShellPoint point, ref uint effect)
        {
            effect = 0;
            try
            {
                string[] folders = ReadFolders(data);
                if (folders.Length == 0) return unchecked((int)0x80070057);
                ShellSelection.Open(folders);
                effect = 1; // COPY effect acknowledges the verb; no filesystem mutation occurs.
                return 0;
            }
            catch (Exception ex) { return Marshal.GetHRForException(ex); }
        }
        internal static string[] ReadFolders(ComDataObject data)
        {
            if (data == null) throw new ArgumentNullException("data");
            FORMATETC format = FileDropFormat();
            STGMEDIUM medium;
            data.GetData(ref format, out medium);
            try
            {
                if (medium.tymed != TYMED.TYMED_HGLOBAL || medium.unionmember == IntPtr.Zero)
                    throw new InvalidDataException("Invalid file selection.");
                uint count = DragQueryFile(medium.unionmember, UInt32.MaxValue, null, 0);
                List<string> folders = new List<string>();
                for (uint i = 0; i < count; i++)
                {
                    uint length = DragQueryFile(medium.unionmember, i, null, 0);
                    if (length == 0 || length > 32767) throw new InvalidDataException("Invalid folder path.");
                    StringBuilder path = new StringBuilder((int)length + 1);
                    if (DragQueryFile(medium.unionmember, i, path, length + 1) != length)
                        throw new InvalidDataException("Incomplete folder path.");
                    // Accept filesystem folders only; never execute supplied data or deserialize objects.
                    if (!Directory.Exists(path.ToString())) throw new DirectoryNotFoundException(path.ToString());
                    folders.Add(path.ToString());
                }
                return Library.NormalizeRoots(folders).ToArray();
            }
            finally { ReleaseStgMedium(ref medium); }
        }
    }

    internal sealed class ShellSelection : ApplicationContext
    {
        private static ShellSelection current;
        private readonly Control dispatcher = new Control();
        private readonly Timer idle = new Timer();
        private readonly RegistrationServices registration = new RegistrationServices();
        private int cookie;
        private bool opened;

        private ShellSelection()
        {
            Application.OleRequired();
            IntPtr handle = dispatcher.Handle;
            // SingleUse gives each invocation its own player and an exact selection,
            // with no timers used to guess which Explorer launches belong together.
            cookie = registration.RegisterTypeForComClients(typeof(FolderDropTarget),
                RegistrationClassContext.LocalServer, RegistrationConnectionType.SingleUse);
            idle.Interval = 60000;
            idle.Tick += delegate { ExitThread(); };
            idle.Start(); // Exit if Explorer cancels activation before delivering a selection.
        }
        internal static void Run()
        {
            using (ShellSelection context = new ShellSelection())
            {
                current = context;
                try { Application.Run(context); }
                finally { current = null; }
            }
        }
        internal static void Open(string[] folders)
        {
            if (current == null || current.opened) throw new InvalidOperationException("Selection already delivered.");
            ShellSelection context = current;
            context.opened = true;
            context.idle.Stop();
            context.dispatcher.BeginInvoke((Action)delegate
            {
                context.MainForm = new PlayerForm(folders, false, null);
                context.MainForm.Show();
                context.MainForm.Activate();
            });
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (cookie != 0) { registration.UnregisterTypeForComClients(cookie); cookie = 0; }
                idle.Dispose(); dispatcher.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
