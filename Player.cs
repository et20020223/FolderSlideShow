using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FolderSlideShow
{
    internal sealed class PlayerForm : Form
    {
        private readonly Panel stage = new Panel(), video = new Panel(), bottom = new Panel();
        private readonly PictureBox photo = new PictureBox();
        private readonly Label heading = new Label(), detail = new Label(), message = new Label(), progress = new Label();
        private readonly FlowLayoutPanel actions = new FlowLayoutPanel(), settings = new FlowLayoutPanel();
        private readonly PlayerButton pauseButton, shuffleButton, loopButton, muteButton, fullscreenButton;
        private readonly ComboBox interval = new ComboBox();
        private readonly PlayerSlider volume = new PlayerSlider(), seek = new PlayerSlider();
        private readonly Panel seekRow = new Panel();
        private readonly ToolTip tips = new ToolTip();
        private MediaClickInput mediaClicks;
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly Stopwatch elapsed = new Stopwatch(), chromeClock = Stopwatch.StartNew();
        private long topLastHover = -1000, bottomLastHover = -1000;
        private readonly HashSet<string> badFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly string config = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
        private readonly string trace;
        private Vlc vlc;
        private WebpImage webp;
        private PlayOrder order;
        private CancellationTokenSource scan;
        private string root;
        private bool paused, isVideo, full, shuffled = true, looping = true, muted, dragging, mediaStarted;
        private int seconds = 5, sound = 70, scanGeneration;
        private Rectangle normalBounds;
        private FormWindowState normalState;
        private readonly Color panelColor = Color.FromArgb(17, 22, 30), accent = Color.FromArgb(103, 229, 197);

        public PlayerForm(string folder, bool windowed, string tracePath)
        {
            trace = tracePath;
            ReadSettings();
            Text = "資料夾隨機放映"; BackColor = Color.Black; ForeColor = Color.White;
            SuspendLayout();
            Font = new Font("Microsoft JhengHei UI", 10);
            StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(1200, 740);
            MinimumSize = new Size(1060, 550); KeyPreview = true;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            heading.Height = 46; heading.Padding = new Padding(28, 8, 28, 0);
            heading.Font = new Font(Font.FontFamily, 13, FontStyle.Bold); heading.BackColor = panelColor;
            heading.AutoEllipsis = true; heading.TextAlign = ContentAlignment.MiddleLeft;
            detail.Height = 32; detail.Padding = new Padding(28, 1, 28, 8);
            detail.BackColor = panelColor; detail.ForeColor = Color.FromArgb(145, 161, 178); detail.AutoEllipsis = true;
            bottom.Height = 152; bottom.BackColor = panelColor;
            bottom.Paint += delegate(object sender, PaintEventArgs e) { using (Pen border = new Pen(Color.FromArgb(43, 54, 66))) e.Graphics.DrawLine(border, 0, 0, bottom.Width, 0); };
            stage.BackColor = Color.Black;
            video.Dock = DockStyle.Fill; video.BackColor = Color.Black;
            photo.Dock = DockStyle.Fill; photo.SizeMode = PictureBoxSizeMode.Zoom; photo.BackColor = Color.Black;
            message.Dock = DockStyle.Fill; message.TextAlign = ContentAlignment.MiddleCenter;
            message.Font = new Font(Font.FontFamily, 16); message.BackColor = Color.Black;
            stage.AccessibleName = "放映畫面"; bottom.AccessibleName = "播放控制";
            actions.AccessibleName = "播放按鈕"; settings.AccessibleName = "放映設定";
            seek.AccessibleName = "播放進度"; volume.AccessibleName = "音量"; interval.AccessibleName = "照片間隔";
            stage.Controls.Add(video); stage.Controls.Add(photo); stage.Controls.Add(message);
            Controls.Add(stage); Controls.Add(bottom); Controls.Add(detail); Controls.Add(heading);
            actions.Height = 48; actions.WrapContents = false;
            settings.Height = 36; settings.WrapContents = false;
            seekRow.Height = 30;
            progress.Dock = DockStyle.Right; progress.Width = 150; progress.TextAlign = ContentAlignment.MiddleRight; progress.ForeColor = Color.FromArgb(164, 184, 199);
            seek.Dock = DockStyle.Fill; seek.Minimum = 0; seek.Maximum = 1000;
            seek.BackColor = panelColor; seek.TabStop = false;
            seek.MouseDown += delegate { dragging = true; };
            seek.MouseUp += delegate { if (isVideo && vlc != null && vlc.Length > 0) vlc.Seek(vlc.Length * seek.Value / 1000); dragging = false; Focus(); };
            seekRow.Controls.Add(seek); seekRow.Controls.Add(progress);
            bottom.Controls.Add(seekRow); bottom.Controls.Add(settings); bottom.Controls.Add(actions);
            AddButton(actions, "←  上一個", delegate { Previous(); }, 104);
            pauseButton = AddButton(actions, "暫停", delegate { TogglePause(); }, 118); pauseButton.Primary = true;
            tips.SetToolTip(pauseButton, "空白鍵：暫停／繼續");
            AddButton(actions, "下一個  →", delegate { Next(); }, 104);
            shuffleButton = AddButton(actions, "", delegate { shuffled = !shuffled; if (order != null) order.SetShuffle(shuffled); UpdateButtons(); SaveSettings(); }, 112);
            loopButton = AddButton(actions, "", delegate { looping = !looping; if (order != null) order.Loop = looping; UpdateButtons(); SaveSettings(); }, 112);
            AddButton(actions, "選擇資料夾", delegate { PickFolder(); }, 115);
            fullscreenButton = AddButton(actions, "全螢幕 F11", delegate { ToggleFullscreen(); }, 120);
            AddButton(actions, "結束  Esc", delegate { Close(); }, 100);
            Label caption = new Label { Text = "照片間隔", AutoSize = false, Width = 76, Height = 32, TextAlign = ContentAlignment.MiddleLeft };
            settings.Controls.Add(caption);
            interval.DropDownStyle = ComboBoxStyle.DropDownList; interval.Width = 90;
            interval.FlatStyle = FlatStyle.Flat; interval.BackColor = Color.FromArgb(31, 37, 47); interval.ForeColor = Color.FromArgb(224, 232, 240);
            interval.Margin = new Padding(0, 4, 14, 0);
            foreach (int n in new int[] { 2, 3, 5, 8, 10, 15, 30, 60 }) interval.Items.Add(n + " 秒");
            int selected = Array.IndexOf(new int[] { 2, 3, 5, 8, 10, 15, 30, 60 }, seconds);
            interval.SelectedIndex = selected < 0 ? 2 : selected;
            interval.SelectedIndexChanged += delegate { seconds = new int[] { 2, 3, 5, 8, 10, 15, 30, 60 }[interval.SelectedIndex]; SaveSettings(); Focus(); };
            settings.Controls.Add(interval);
            muteButton = AddButton(settings, "", delegate { muted = !muted; ApplyVolume(); }, 100);
            volume.Minimum = 0; volume.Maximum = 100; volume.Value = sound; volume.Width = 120; volume.Height = 32;
            volume.BackColor = panelColor; volume.TabStop = false;
            volume.ValueChanged += delegate { sound = volume.Value; if (sound > 0) muted = false; ApplyVolume(); };
            settings.Controls.Add(volume);
            Label help = new Label { Text = "單擊畫面換下一個   ·   含子資料夾", AutoSize = false, Width = 350, Height = 32, Padding = new Padding(18, 0, 0, 0), TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(134, 153, 170) };
            settings.Controls.Add(help);
            tips.SetToolTip(fullscreenButton, "F11 / F：全螢幕或視窗");
            tips.SetToolTip(shuffleButton, "S：隨機／依序播放"); tips.SetToolTip(loopButton, "L：開關循環");
            timer.Interval = 100; timer.Tick += Tick; timer.Start();
            FormClosed += delegate
            {
                timer.Stop(); if (scan != null) scan.Cancel(); SaveSettings(); ClearPhoto();
                if (vlc != null) vlc.Dispose(); timer.Dispose(); tips.Dispose(); Log("closed");
            };
            UpdateButtons();
            Shown += delegate
            {
                if (!windowed) ToggleFullscreen();
                if (!String.IsNullOrEmpty(folder)) LoadFolder(folder); else PickFolder();
            };
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            ResumeLayout(true); PerformAutoScale();
            ClientSizeChanged += delegate { LayoutChrome(); };
            stage.SendToBack(); LayoutChrome();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            mediaClicks = new MediaClickInput(Handle, stage, delegate
            {
                if (order != null && !message.Visible && !IsDisposed)
                    BeginInvoke((Action)delegate { if (!IsDisposed) { Log("surface-click-next"); Next(); Focus(); } });
            });
        }
        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (mediaClicks != null) { mediaClicks.Dispose(); mediaClicks = null; }
            base.OnHandleDestroyed(e);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x00FF && mediaClicks != null) mediaClicks.Process(m.LParam);
            base.WndProc(ref m);
        }
        private PlayerButton AddButton(FlowLayoutPanel parent, string text, Action action, int width)
        {
            PlayerButton button = new PlayerButton { Text = text, Width = width, Height = parent == settings ? 32 : 40, FlatStyle = FlatStyle.Flat, BackColor = panelColor, ForeColor = Color.White, TabStop = false, Margin = new Padding(0, 0, 8, 0) };
            button.FlatAppearance.BorderSize = 0;
            button.Click += delegate { ShowControls(); action(); Focus(); };
            parent.Controls.Add(button); return button;
        }
        private void ReadSettings()
        {
            try
            {
                if (!File.Exists(config)) return;
                foreach (string line in File.ReadAllLines(config))
                {
                    string[] pair = line.Split('='); if (pair.Length != 2) continue;
                    int n; bool b;
                    if (pair[0] == "seconds" && Int32.TryParse(pair[1], out n) && new int[] { 2, 3, 5, 8, 10, 15, 30, 60 }.Contains(n)) seconds = n;
                    if (pair[0] == "volume" && Int32.TryParse(pair[1], out n)) sound = Math.Max(0, Math.Min(100, n));
                    if (pair[0] == "shuffle" && Boolean.TryParse(pair[1], out b)) shuffled = b;
                    if (pair[0] == "loop" && Boolean.TryParse(pair[1], out b)) looping = b;
                    if (pair[0] == "mute" && Boolean.TryParse(pair[1], out b)) muted = b;
                }
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        private void SaveSettings()
        {
            try { File.WriteAllLines(config, new string[] { "seconds=" + seconds, "volume=" + sound, "shuffle=" + shuffled, "loop=" + looping, "mute=" + muted }); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        private void Log(string value)
        {
            if (trace == null) return;
            try { File.AppendAllText(trace, DateTime.UtcNow.ToString("O") + " " + value + Environment.NewLine); } catch (IOException) { }
        }
        private void PickFolder()
        {
            bool wasPaused = paused;
            if (!paused) SetPaused(true);
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "選擇要放映的資料夾（會包含所有子資料夾的照片和影片）";
                dialog.ShowNewFolderButton = false;
                if (root != null) dialog.SelectedPath = root;
                if (dialog.ShowDialog(this) == DialogResult.OK) LoadFolder(dialog.SelectedPath);
                else if (order == null) { message.Text = "請點選「選擇資料夾」開始放映\n全螢幕時靠近下緣顯示選單，或按 Ctrl+O"; message.Visible = true; }
                else SetPaused(wasPaused);
            }
        }
        private void LoadFolder(string folder)
        {
            if (scan != null) scan.Cancel();
            scan = new CancellationTokenSource(); CancellationToken token = scan.Token;
            int generation = ++scanGeneration;
            if (vlc != null) vlc.Stop(); ClearPhoto(); isVideo = false; order = null;
            elapsed.Reset(); root = Path.GetFullPath(folder); badFiles.Clear();
            message.Text = "正在尋找照片和影片…\n包含所有子資料夾"; message.Visible = true; message.BringToFront();
            heading.Text = "資料夾隨機放映"; detail.Text = folder; ShowControls();
            Task.Factory.StartNew(delegate
            {
                ScanResult result = null; Exception error = null;
                try { result = Library.Scan(folder, token); } catch (Exception ex) { error = ex; }
                if (token.IsCancellationRequested || IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        if (generation != scanGeneration || IsDisposed) return;
                        if (error != null) { message.Text = "無法讀取這個資料夾\n" + error.Message; return; }
                        if (result.Files.Count == 0) { message.Text = "這個資料夾及子資料夾沒有可播放的照片或影片\n支援 JPG、PNG、WebP、GIF、BMP、TIFF，以及 MP4、MOV、MKV 等影片"; return; }
                        order = new PlayOrder(result.Files, shuffled, new Random()); order.Loop = looping;
                        Log("scan count=" + result.Files.Count + " skipped=" + result.SkippedDirectories);
                        paused = false; order.Next(); RenderCurrent(); UpdateButtons();
                    });
                }
                catch (InvalidOperationException) { }
            });
        }
        private void ClearPhoto()
        {
            Image old = photo.Image; photo.Image = null;
            if (webp != null) { webp.Dispose(); webp = null; }
            else if (old != null) old.Dispose();
            timer.Interval = 100;
        }
        private void RenderCurrent()
        {
            if (order == null || order.Current == null) return;
            for (int attempt = 0; attempt < order.Count; attempt++)
            {
                if (vlc != null) vlc.Stop(); ClearPhoto(); elapsed.Reset(); mediaStarted = false;
                string path = order.Current; isVideo = Library.Videos.Contains(Path.GetExtension(path));
                try
                {
                    if (badFiles.Contains(path)) throw new IOException("這個檔案先前播放失敗。");
                    if (isVideo)
                    {
                        video.Visible = true; photo.Visible = false; video.BringToFront();
                        if (vlc == null) vlc = new Vlc(video.Handle);
                        vlc.Play(path); vlc.Volume(muted ? 0 : sound);
                    }
                    else
                    {
                        Image image;
                        if (String.Equals(Path.GetExtension(path), ".webp", StringComparison.OrdinalIgnoreCase))
                        {
                            webp = new WebpImage(path); image = webp.Frame;
                            if (webp.Animated) timer.Interval = 15;
                            Log("webp animated=" + webp.Animated);
                        }
                        else image = Image.FromFile(path);
                        try
                        {
                            if (Array.IndexOf(image.PropertyIdList, 0x112) >= 0)
                            {
                                int orientation = BitConverter.ToUInt16(image.GetPropertyItem(0x112).Value, 0);
                                RotateFlipType[] transforms = { RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate270FlipNone };
                                if (orientation > 0 && orientation < transforms.Length) image.RotateFlip(transforms[orientation]);
                            }
                        }
                        catch (ArgumentException) { }
                        photo.Image = image; photo.Visible = true; video.Visible = false; photo.BringToFront();
                    }
                    message.Visible = false; elapsed.Start();
                    if (paused) elapsed.Stop();
                    heading.Text = Path.GetFileName(path);
                    detail.Text = (shuffled ? "隨機" : "依序") + "  ·  " + order.Number + " / " + order.Count + "  ·  " + (isVideo ? "影片" : "照片") + "  ·  " + Relative(path) + (badFiles.Count > 0 ? "  ·  已略過 " + badFiles.Count + " 個無法播放的檔案" : "");
                    Text = "資料夾隨機放映 — " + Path.GetFileName(path);
                    seek.Enabled = isVideo; seek.Value = 0;
                    Log("display " + (isVideo ? "video " : "photo ") + path); UpdateButtons(); return;
                }
                catch (Exception ex)
                {
                    badFiles.Add(path); Log("skip " + path + " : " + ex.Message);
                    if (!order.Next()) break;
                }
            }
            SetPaused(true); isVideo = false;
            message.Text = "沒有可繼續播放的檔案\n檔案可能已移除、損壞，或缺少 64 位元 VLC\n請選擇另一個資料夾，或確認 VLC 已安裝。";
            message.Visible = true; message.BringToFront(); ShowControls();
        }
        private string Relative(string path)
        {
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? path.Substring(prefix.Length) : path;
        }
        private void Next()
        {
            if (order == null) return;
            if (order.Next()) RenderCurrent(); else { SetPaused(true); detail.Text = "已播放完畢  ·  開啟循環播放可繼續"; }
        }
        private void Previous() { if (order != null && order.Previous()) RenderCurrent(); }
        private void TogglePause() { if (order != null) SetPaused(!paused); }
        private void SetPaused(bool value)
        {
            paused = value;
            if (paused) elapsed.Stop(); else elapsed.Start();
            if (isVideo && vlc != null) vlc.Pause(paused);
            Log("paused=" + paused); UpdateButtons(); ShowControls();
        }
        private void ApplyVolume()
        {
            if (vlc != null) vlc.Volume(muted ? 0 : sound);
            UpdateButtons(); SaveSettings();
        }
        private void UpdateButtons()
        {
            pauseButton.Text = paused ? "▶  繼續播放" : "Ⅱ  暫停";
            shuffleButton.Text = shuffled ? "隨機播放  ·" : "依序播放"; shuffleButton.Selected = shuffled;
            loopButton.Text = looping ? "循環播放  ·" : "循環已關"; loopButton.Selected = looping;
            muteButton.Text = muted ? "已靜音" : "音量  " + sound; muteButton.Selected = muted;
            fullscreenButton.Text = full ? "視窗 F11" : "全螢幕 F11";
            shuffleButton.Invalidate(); loopButton.Invalidate(); muteButton.Invalidate(); pauseButton.Invalidate();
        }
        private void LayoutChrome()
        {
            heading.SetBounds(0, 0, ClientSize.Width, heading.Height);
            detail.SetBounds(0, heading.Height, ClientSize.Width, detail.Height);
            bottom.SetBounds(0, Math.Max(0, ClientSize.Height - bottom.Height), ClientSize.Width, bottom.Height);
            float scale = AutoScaleDimensions.Width / 96f;
            int margin = (int)(28 * scale), width = Math.Min(ClientSize.Width - margin * 2, (int)(1040 * scale));
            int left = (ClientSize.Width - width) / 2;
            seekRow.SetBounds(left, (int)(8 * scale), width, (int)(30 * scale));
            actions.SetBounds(left, (int)(44 * scale), width, (int)(48 * scale));
            settings.SetBounds(left, (int)(102 * scale), width, (int)(36 * scale));
            int topSpace = full ? 0 : heading.Height + detail.Height;
            int bottomSpace = full ? 0 : bottom.Height;
            // Fullscreen controls overlay the media, so revealing them never resizes the picture or video.
            stage.SetBounds(0, topSpace, ClientSize.Width, Math.Max(0, ClientSize.Height - topSpace - bottomSpace));
        }
        private void SetChromeVisibility(bool topVisible, bool bottomVisible)
        {
            if (heading.Visible == topVisible && detail.Visible == topVisible && bottom.Visible == bottomVisible) return;
            heading.Visible = detail.Visible = topVisible;
            bottom.Visible = bottomVisible;
            if (topVisible) { heading.BringToFront(); detail.BringToFront(); }
            if (bottomVisible) bottom.BringToFront();
            LayoutChrome();
            Log("chrome top=" + topVisible + " bottom=" + bottomVisible + " fullscreen=" + full);
        }
        private void ShowControls()
        {
            if (!full) { SetChromeVisibility(true, true); LayoutChrome(); return; }
            Point mouse = PointToClient(Cursor.Position);
            bool inside = ClientRectangle.Contains(mouse);
            int edge = Math.Max(16, (int)Math.Round(24 * AutoScaleDimensions.Width / 96f));
            long now = chromeClock.ElapsedMilliseconds;
            if (inside && (mouse.Y < edge || (heading.Visible && mouse.Y < detail.Bottom))) topLastHover = now;
            if ((inside && (mouse.Y >= ClientSize.Height - edge || (bottom.Visible && bottom.Bounds.Contains(mouse))))
                || interval.DroppedDown || dragging || volume.Capture || seek.Capture) bottomLastHover = now;
            // A short grace period keeps menus stable while moving onto their controls.
            SetChromeVisibility(now - topLastHover < 300, now - bottomLastHover < 300);
        }
        private void ToggleFullscreen()
        {
            if (!full)
            {
                normalBounds = Bounds; normalState = WindowState;
                WindowState = FormWindowState.Normal; FormBorderStyle = FormBorderStyle.None;
                Bounds = Screen.FromControl(this).Bounds; full = true;
                topLastHover = bottomLastHover = -1000;
                SetChromeVisibility(false, false);
            }
            else
            {
                FormBorderStyle = FormBorderStyle.Sizable; Bounds = normalBounds; WindowState = normalState; full = false;
            }
            LayoutChrome(); ShowControls(); UpdateButtons(); Log("fullscreen=" + full);
        }
        private static string Stamp(long milliseconds)
        {
            TimeSpan t = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
            return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
        }
        private void Tick(object sender, EventArgs e)
        {
            ShowControls();
            if (order == null || message.Visible) return;
            if (webp != null && !paused)
            {
                try { if (webp.AdvanceTo(elapsed.ElapsedMilliseconds)) photo.Invalidate(); }
                catch (Exception ex)
                {
                    badFiles.Add(order.Current); Log("webp-error " + ex.Message);
                    if (badFiles.Count >= order.Count) RenderCurrent(); else Next();
                    return;
                }
            }
            if (isVideo && vlc != null)
            {
                int state = vlc.State;
                if (state == 3 && !mediaStarted)
                {
                    mediaStarted = true; Log("video-playing");
                }
                // Let a newly selected paused video decode its first frame before stopping it.
                if (paused && state == 3 && vlc.Time >= 100) vlc.Pause(true);
                if (!paused && (state == 6 || state == 7 || (!mediaStarted && elapsed.ElapsedMilliseconds > 20000)))
                {
                    if (state != 6) { badFiles.Add(order.Current); Log("video-error state=" + state); }
                    else Log("video-ended");
                    if (badFiles.Count >= order.Count) { RenderCurrent(); return; }
                    Next(); return;
                }
                long length = vlc.Length, time = vlc.Time;
                progress.Text = Stamp(time) + " / " + Stamp(length);
                if (!dragging && length > 0) seek.Value = (int)Math.Max(0, Math.Min(1000, time * 1000 / length));
            }
            else
            {
                progress.Text = (paused ? "已暫停  ·  " : "") + Math.Max(0, seconds - elapsed.Elapsed.TotalSeconds).ToString("0.0") + " 秒";
                if (!dragging) seek.Value = (int)Math.Max(0, Math.Min(1000, elapsed.Elapsed.TotalSeconds * 1000 / seconds));
                if (!paused && elapsed.Elapsed.TotalSeconds >= seconds) Next();
            }
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (interval.Focused && interval.DroppedDown) return base.ProcessCmdKey(ref msg, keyData);
            switch (keyData)
            {
                case Keys.Escape: Close(); return true;
                case Keys.Space: TogglePause(); return true;
                case Keys.Right: Next(); ShowControls(); return true;
                case Keys.Left: Previous(); ShowControls(); return true;
                case Keys.F11: case Keys.F: ToggleFullscreen(); return true;
                case Keys.S: shuffled = !shuffled; if (order != null) order.SetShuffle(shuffled); UpdateButtons(); SaveSettings(); ShowControls(); return true;
                case Keys.L: looping = !looping; if (order != null) order.Loop = looping; UpdateButtons(); SaveSettings(); ShowControls(); return true;
                case Keys.M: muted = !muted; ApplyVolume(); ShowControls(); return true;
                case Keys.Up: volume.Value = Math.Min(100, volume.Value + 5); ShowControls(); return true;
                case Keys.Down: volume.Value = Math.Max(0, volume.Value - 5); ShowControls(); return true;
                case Keys.Control | Keys.O: PickFolder(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
