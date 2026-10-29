using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace FolderSlideShow
{
    // Decode only the current animation canvas rather than storing every decoded frame.
    internal sealed class WebpImage : IDisposable
    {
        private const int AbiVersion = 0x0107;
        [StructLayout(LayoutKind.Sequential)] private struct WebpData { public IntPtr Bytes; public UIntPtr Size; }
        [StructLayout(LayoutKind.Sequential)] private struct Options
        {
            public int ColorMode, UseThreads;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)] public uint[] Padding;
        }
        [StructLayout(LayoutKind.Sequential)] private struct Info
        {
            public uint Width, Height, LoopCount, BackgroundColor, FrameCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public uint[] Padding;
        }
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr LoadLibrary(string path);
        [DllImport("kernel32", EntryPoint = "RtlMoveMemory")] private static extern void CopyMemory(IntPtr destination, IntPtr source, UIntPtr length);
        [DllImport("libwebp", CallingConvention = CallingConvention.Cdecl)] private static extern int WebPGetInfo(byte[] data, UIntPtr length, out int width, out int height);
        [DllImport("libwebp", CallingConvention = CallingConvention.Cdecl)] public static extern int WebPGetDecoderVersion();
        [DllImport("libwebpdemux", CallingConvention = CallingConvention.Cdecl)] private static extern int WebPAnimDecoderOptionsInitInternal(ref Options options, int version);
        [DllImport("libwebpdemux", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr WebPAnimDecoderNewInternal(ref WebpData data, ref Options options, int version);
        [DllImport("libwebpdemux", CallingConvention = CallingConvention.Cdecl)] private static extern int WebPAnimDecoderGetInfo(IntPtr decoder, ref Info info);
        [DllImport("libwebpdemux", CallingConvention = CallingConvention.Cdecl)] private static extern int WebPAnimDecoderGetNext(IntPtr decoder, out IntPtr pixels, out int timestamp);
        [DllImport("libwebpdemux", CallingConvention = CallingConvention.Cdecl)] private static extern int WebPAnimDecoderHasMoreFrames(IntPtr decoder);
        [DllImport("libwebpdemux", CallingConvention = CallingConvention.Cdecl)] private static extern void WebPAnimDecoderReset(IntPtr decoder);
        [DllImport("libwebpdemux", CallingConvention = CallingConvention.Cdecl)] private static extern void WebPAnimDecoderDelete(IntPtr decoder);
        private static readonly object loadLock = new object();
        private static bool librariesLoaded;
        private GCHandle input;
        private IntPtr decoder, pixels;
        private Info info;
        private int previousTimestamp;
        private uint completedLoops;
        private long frameEnd;
        private bool ended;
        public Bitmap Frame { get; private set; }
        public bool Animated { get { return info.FrameCount > 1; } }

        private static void LoadLibraries()
        {
            lock (loadLock)
            {
                if (librariesLoaded) return;
                foreach (string name in new string[] { "libsharpyuv.dll", "libwebp.dll", "libwebpdemux.dll" })
                {
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
                    if (LoadLibrary(path) == IntPtr.Zero) throw new InvalidOperationException("無法載入 WebP 解碼元件：" + name);
                }
                librariesLoaded = true;
            }
        }
        public WebpImage(string path)
        {
            try
            {
                LoadLibraries();
                byte[] bytes = File.ReadAllBytes(path);
                int width, height;
                if (WebPGetInfo(bytes, (UIntPtr)(ulong)bytes.LongLength, out width, out height) == 0)
                    throw new InvalidDataException("WebP 標頭無效。");
                if (width <= 0 || height <= 0 || (long)width * height > 64000000)
                    throw new InvalidDataException("WebP 圖片尺寸過大（上限 6,400 萬像素）。");
                input = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                WebpData data = new WebpData { Bytes = input.AddrOfPinnedObject(), Size = (UIntPtr)(ulong)bytes.LongLength };
                Options options = new Options { Padding = new uint[7] };
                if (WebPAnimDecoderOptionsInitInternal(ref options, AbiVersion) == 0) throw new InvalidOperationException("WebP 解碼版本不相容。");
                options.ColorMode = 3; // MODE_BGRA matches System.Drawing.Format32bppArgb byte order.
                options.UseThreads = 1;
                decoder = WebPAnimDecoderNewInternal(ref data, ref options, AbiVersion);
                if (decoder == IntPtr.Zero) throw new InvalidDataException("WebP 檔案不完整或無法解碼。");
                info = new Info { Padding = new uint[4] };
                if (WebPAnimDecoderGetInfo(decoder, ref info) == 0 || info.Width != width || info.Height != height || info.FrameCount == 0)
                    throw new InvalidDataException("無法讀取 WebP 圖片資訊。");
                Frame = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                DecodeNext(); CopyFrame();
            }
            catch { Dispose(); throw; }
        }
        private void DecodeNext()
        {
            int timestamp;
            if (WebPAnimDecoderGetNext(decoder, out pixels, out timestamp) == 0 || pixels == IntPtr.Zero)
                throw new InvalidDataException("WebP 動畫影格解碼失敗。");
            // Zero-duration frames must still make progress through the animation clock.
            frameEnd += Math.Max(10, timestamp - previousTimestamp);
            previousTimestamp = timestamp;
        }
        private void CopyFrame()
        {
            BitmapData target = Frame.LockBits(new Rectangle(0, 0, Frame.Width, Frame.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int rowBytes = checked(Frame.Width * 4);
                if (target.Stride == rowBytes) CopyMemory(target.Scan0, pixels, (UIntPtr)(ulong)(rowBytes * (long)Frame.Height));
                else for (int row = 0; row < Frame.Height; row++)
                    CopyMemory(IntPtr.Add(target.Scan0, row * target.Stride), IntPtr.Add(pixels, row * rowBytes), (UIntPtr)(uint)rowBytes);
            }
            finally { Frame.UnlockBits(target); }
        }
        public bool AdvanceTo(long milliseconds)
        {
            if (decoder == IntPtr.Zero) throw new ObjectDisposedException("WebpImage");
            if (!Animated || ended) return false;
            bool changed = false;
            for (int frames = 0; milliseconds >= frameEnd && frames < 100; frames++)
            {
                if (WebPAnimDecoderHasMoreFrames(decoder) == 0)
                {
                    completedLoops++;
                    if (info.LoopCount > 0 && completedLoops >= info.LoopCount) { ended = true; break; }
                    WebPAnimDecoderReset(decoder); previousTimestamp = 0;
                }
                DecodeNext(); changed = true;
            }
            if (changed) CopyFrame();
            return changed;
        }
        public void Dispose()
        {
            if (decoder != IntPtr.Zero) { WebPAnimDecoderDelete(decoder); decoder = IntPtr.Zero; }
            if (input.IsAllocated) input.Free();
            if (Frame != null) { Frame.Dispose(); Frame = null; }
        }
    }
}
