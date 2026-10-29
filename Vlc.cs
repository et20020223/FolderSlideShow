using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace FolderSlideShow
{
    internal sealed class Vlc : IDisposable
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr LoadLibrary(string path);
        [DllImport("kernel32", CharSet = CharSet.Unicode)] static extern bool SetDllDirectory(string path);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr libvlc_new(int count, IntPtr args);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] static extern void libvlc_release(IntPtr instance);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr libvlc_media_player_new(IntPtr instance);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] static extern void libvlc_media_player_release(IntPtr player);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr libvlc_media_new_path(IntPtr instance, byte[] path);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] static extern void libvlc_media_release(IntPtr media);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] static extern void libvlc_media_player_set_media(IntPtr player, IntPtr media);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] static extern void libvlc_media_player_set_hwnd(IntPtr player, IntPtr hwnd);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] static extern void libvlc_video_set_key_input(IntPtr player, uint enabled);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] static extern void libvlc_video_set_mouse_input(IntPtr player, uint enabled);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] static extern int libvlc_media_player_play(IntPtr player);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] static extern void libvlc_media_player_stop(IntPtr player);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] static extern void libvlc_media_player_set_pause(IntPtr player, int paused);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] static extern int libvlc_media_player_get_state(IntPtr player);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] static extern long libvlc_media_player_get_time(IntPtr player);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] static extern long libvlc_media_player_get_length(IntPtr player);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] static extern void libvlc_media_player_set_time(IntPtr player, long time);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] static extern int libvlc_audio_set_volume(IntPtr player, int volume);
        private IntPtr instance, player;

        public static string FindHome()
        {
            string portable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VLC");
            if (File.Exists(Path.Combine(portable, "libvlc.dll"))) return portable;
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\VideoLAN\VLC"))
            {
                string home = key == null ? null : key.GetValue("InstallDir") as string;
                if (home != null && File.Exists(Path.Combine(home, "libvlc.dll"))) return home;
            }
            string standard = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VideoLAN", "VLC");
            return File.Exists(Path.Combine(standard, "libvlc.dll")) ? standard : null;
        }
        public Vlc(IntPtr hwnd)
        {
            string home = FindHome();
            if (home == null) throw new InvalidOperationException("影片播放需要 64 位元 VLC。請先安裝 VLC，再重新啟動播放器。照片仍可正常播放。");
            SetDllDirectory(home);
            try
            {
                if (LoadLibrary(Path.Combine(home, "libvlccore.dll")) == IntPtr.Zero || LoadLibrary(Path.Combine(home, "libvlc.dll")) == IntPtr.Zero)
                    throw new InvalidOperationException("無法載入 VLC，請確認安裝的是 64 位元版本。");
                Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", Path.Combine(home, "plugins"));
                string[] options = { "--no-video-title-show", "--no-osd", "--no-snapshot-preview", "--quiet", "--no-stats", "--no-media-library", "--avcodec-hw=none" };
                IntPtr[] pointers = new IntPtr[options.Length];
                IntPtr argv = Marshal.AllocHGlobal(IntPtr.Size * pointers.Length);
                try
                {
                    for (int i = 0; i < pointers.Length; i++) { pointers[i] = Marshal.StringToHGlobalAnsi(options[i]); Marshal.WriteIntPtr(argv, i * IntPtr.Size, pointers[i]); }
                    instance = libvlc_new(pointers.Length, argv);
                }
                finally { foreach (IntPtr p in pointers) if (p != IntPtr.Zero) Marshal.FreeHGlobal(p); Marshal.FreeHGlobal(argv); }
                if (instance == IntPtr.Zero) throw new InvalidOperationException("VLC 播放核心啟動失敗。");
                player = libvlc_media_player_new(instance);
                if (player == IntPtr.Zero) throw new InvalidOperationException("無法建立影片播放器。");
                libvlc_media_player_set_hwnd(player, hwnd);
                libvlc_video_set_key_input(player, 0); libvlc_video_set_mouse_input(player, 0);
            }
            catch { Dispose(); throw; }
            finally { SetDllDirectory(null); }
        }
        public void Play(string path)
        {
            Stop();
            IntPtr media = libvlc_media_new_path(instance, Encoding.UTF8.GetBytes(path + "\0"));
            if (media == IntPtr.Zero) throw new InvalidOperationException("無法開啟影片。");
            try { libvlc_media_player_set_media(player, media); }
            finally { libvlc_media_release(media); }
            if (libvlc_media_player_play(player) != 0) throw new InvalidOperationException("影片無法播放。");
        }
        public int State { get { return libvlc_media_player_get_state(player); } }
        public long Time { get { return libvlc_media_player_get_time(player); } }
        public long Length { get { return libvlc_media_player_get_length(player); } }
        public void Seek(long time) { libvlc_media_player_set_time(player, Math.Max(0, time)); }
        public void Pause(bool paused) { libvlc_media_player_set_pause(player, paused ? 1 : 0); }
        public void Volume(int volume) { libvlc_audio_set_volume(player, volume); }
        public void Stop() { if (player != IntPtr.Zero) libvlc_media_player_stop(player); }
        public void Dispose()
        {
            if (player != IntPtr.Zero) { libvlc_media_player_stop(player); libvlc_media_player_release(player); player = IntPtr.Zero; }
            if (instance != IntPtr.Zero) { libvlc_release(instance); instance = IntPtr.Zero; }
        }
    }
}
