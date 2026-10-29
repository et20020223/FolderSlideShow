using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FolderSlideShow;

static class CoreTests
{
    static int checks;
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); checks++; Console.WriteLine("PASS " + text); }
    static void Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        ScanResult scan = Library.Scan(root, CancellationToken.None);
        Check(scan.Files.Count == 5, "recursive scan finds 5 supported files, ignores text and junction");
        Check(scan.Files.Any(p => p.Contains("孫資料夾")), "nested Unicode path included");
        Check(scan.Files.Any(p => p.Contains("03_影片")), "video included");
        Check(scan.SkippedDirectories == Int32.Parse(args[1]), "expected skipped directory count");
        bool cancelled = false;
        try { Library.Scan(root, new CancellationToken(true)); } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "scan cancellation");
        bool missing = false;
        try { Library.Scan(Path.Combine(root, "missing"), CancellationToken.None); } catch (DirectoryNotFoundException) { missing = true; }
        Check(missing, "missing root reported");
        List<string> files = Enumerable.Range(0, 40).Select(n => "item" + n.ToString("00")).ToList();
        PlayOrder order = new PlayOrder(files, true, new Random(123));
        HashSet<string> round = new HashSet<string>(); List<string> first = new List<string>();
        for (int i = 0; i < files.Count; i++) { Check(order.Next(), "next " + i); round.Add(order.Current); first.Add(order.Current); }
        Check(round.Count == 40, "no duplicate or omission within shuffle cycle");
        Check(!first.SequenceEqual(files), "shuffle changes original order");
        string last = order.Current; order.Next(); string next = order.Current;
        Check(next != last, "no immediate repetition across cycles");
        Check(order.Previous() && order.Current == last, "previous crosses cycle boundary using actual history");
        Check(order.Next() && order.Current == next, "next after previous follows history");
        PlayOrder toggled = new PlayOrder(files, true, new Random(123));
        round.Clear();
        for (int i = 0; i < 10; i++) { toggled.Next(); round.Add(toggled.Current); }
        toggled.SetShuffle(false);
        for (int i = 0; i < 30; i++) { toggled.Next(); round.Add(toggled.Current); }
        Check(round.Count == 40, "toggling shuffle preserves unplayed remainder without repeats");
        PlayOrder once = new PlayOrder(new string[] { "a", "b" }, false, new Random(1)); once.Loop = false;
        Check(once.Next() && once.Current == "a" && once.Next() && once.Current == "b" && !once.Next(), "nonlooping playback stops at end");
        PlayOrder empty = new PlayOrder(new string[0], true, new Random(1));
        Check(!empty.Next() && !empty.Previous(), "empty playlist safe");
        PlayOrder single = new PlayOrder(new string[] { "a" }, true, new Random(1));
        Check(single.Next() && single.Next() && single.Current == "a", "single item loops");
        for (int i = 0; i < 2200; i++) single.Next();
        Check(single.Previous() && single.Next(), "history bounded without breaking navigation");
        Console.WriteLine("All " + checks + " checks passed.");
    }
}
