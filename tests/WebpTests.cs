using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using FolderSlideShow;

static class WebpTests
{
    static int checks;
    static void Check(bool value, string description) { if (!value) throw new Exception(description); checks++; Console.WriteLine("PASS " + description); }
    static bool Pixel(WebpImage image, Color color) { return image.Frame.GetPixel(20, 15).ToArgb() == color.ToArgb(); }
    static void Main(string[] args)
    {
        string root = args[0], refs = args[1];
        using (WebpImage image = new WebpImage(Path.Combine(root, "01_靜態.WEBP")))
        {
            Check(WebpImage.WebPGetDecoderVersion() >= 0x010600, "native libwebp is at least 1.6.0");
            Check(image.Frame.Width == 40 && image.Frame.Height == 30, "static dimensions preserved");
            Check(!image.Animated && Pixel(image, Color.Red), "lossless color channels are correct");
            Check(!image.AdvanceTo(100000), "static images remain still");
        }
        using (WebpImage image = new WebpImage(Path.Combine(root, "02_透明.webp")))
        {
            Check(image.Frame.GetPixel(0, 0).A == 0, "full transparency preserved");
            Color pixel = image.Frame.GetPixel(20, 15);
            Check(pixel.A == 128 && pixel.R == 40 && pixel.G == 120 && pixel.B == 200, "partial alpha and RGB preserved");
        }
        using (WebpImage image = new WebpImage(Path.Combine(root, "子資料夾", "孫資料夾", "03_動畫.webp")))
        {
            Check(image.Animated && Pixel(image, Color.Red), "animated WebP starts with first frame");
            Check(!image.AdvanceTo(99) && Pixel(image, Color.Red), "first frame duration respected");
            Check(image.AdvanceTo(100) && Pixel(image, Color.Lime), "second frame starts at 100ms");
            Check(!image.AdvanceTo(349) && Pixel(image, Color.Lime), "unequal 250ms frame duration respected");
            Check(image.AdvanceTo(350) && Pixel(image, Color.Blue), "third frame starts at 350ms");
            Check(image.AdvanceTo(500) && Pixel(image, Color.Red), "infinite animation loops at 500ms");
            Check(!image.AdvanceTo(500) && Pixel(image, Color.Red), "unchanged paused clock does not advance frames");
        }
        using (WebpImage image = new WebpImage(Path.Combine(root, "04_有限循環.webp")))
        {
            Check(image.AdvanceTo(350) && Pixel(image, Color.Blue), "finite animation reaches final frame");
            Check(!image.AdvanceTo(10000) && Pixel(image, Color.Blue), "finite loop count is honored");
        }
        using (WebpImage image = new WebpImage(Path.Combine(root, "05_有損.webp")))
        {
            Color p = image.Frame.GetPixel(20, 15);
            Check(Math.Abs(p.R - 220) < 10 && Math.Abs(p.G - 40) < 10 && Math.Abs(p.B - 70) < 10, "lossy WebP decodes with expected colors");
        }
        bool rejected = false;
        try { using (WebpImage bad = new WebpImage(Path.Combine(root, "06_損壞.webp"))) { } } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "corrupt WebP rejected without a native crash");
        using (WebpImage image = new WebpImage(Path.Combine(root, "07_零延遲.webp")))
            Check(image.AdvanceTo(100000), "zero-duration animation remains bounded and responsive");
        using (WebpImage image = new WebpImage(Path.Combine(root, "08_透明動畫.webp")))
        {
            for (int frame = 0; frame < 3; frame++)
            {
                image.AdvanceTo(frame * 100);
                byte[] expected = File.ReadAllBytes(Path.Combine(refs, "alpha-" + frame + ".rgba"));
                bool equal = true;
                for (int y = 0; y < 30; y++) for (int x = 0; x < 40; x++)
                {
                    Color actual = image.Frame.GetPixel(x, y); int i = (y * 40 + x) * 4;
                    if (actual.A != expected[i + 3] || (actual.A != 0 && (actual.R != expected[i] || actual.G != expected[i + 1] || actual.B != expected[i + 2]))) equal = false;
                }
                Check(equal, "alpha animation frame " + frame + " matches independent reference decoder");
            }
        }
        ScanResult scan = Library.Scan(root, CancellationToken.None);
        Check(scan.Files.Count == 8 && scan.Files.Any(p => p.Contains("孫資料夾")), "recursive scanner includes uppercase extension and nested WebP");
        // Disposal must release input files, native decoders, and image canvases on repeated navigation.
        for (int i = 0; i < 100; i++) using (WebpImage image = new WebpImage(Path.Combine(root, "01_靜態.WEBP"))) { }
        using (FileStream file = File.Open(Path.Combine(root, "01_靜態.WEBP"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Check(file.Length > 0, "WebP files are not left locked after playback");
        Console.WriteLine("All " + checks + " WebP checks passed.");
    }
}
