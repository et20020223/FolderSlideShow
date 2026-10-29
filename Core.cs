using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace FolderSlideShow
{
    public sealed class ScanResult
    {
        public readonly List<string> Files = new List<string>();
        public int SkippedDirectories;
    }

    public static class Library
    {
        public static readonly HashSet<string> Photos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".jpe", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp" };
        public static readonly HashSet<string> Videos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".m4v", ".mov", ".mkv", ".avi", ".wmv", ".webm", ".mpg", ".mpeg", ".mts", ".m2ts", ".ts", ".3gp", ".flv", ".vob", ".ogv" };

        public static ScanResult Scan(string root, CancellationToken token)
        {
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
            ScanResult result = new ScanResult();
            Stack<string> pending = new Stack<string>();
            pending.Push(Path.GetFullPath(root));
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                string folder = pending.Pop();
                try
                {
                    foreach (string file in Directory.EnumerateFiles(folder))
                    {
                        token.ThrowIfCancellationRequested();
                        string ext = Path.GetExtension(file);
                        if (Photos.Contains(ext) || Videos.Contains(ext)) result.Files.Add(file);
                    }
                    foreach (string child in Directory.EnumerateDirectories(folder))
                    {
                        token.ThrowIfCancellationRequested();
                        try
                        {
                            // Junctions and directory symlinks can lead back to a parent or outside the chosen tree.
                            if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) pending.Push(child);
                            else result.SkippedDirectories++;
                        }
                        catch (UnauthorizedAccessException) { result.SkippedDirectories++; }
                        catch (IOException) { result.SkippedDirectories++; }
                    }
                }
                catch (UnauthorizedAccessException) { result.SkippedDirectories++; }
                catch (IOException) { result.SkippedDirectories++; }
            }
            result.Files.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }
    }

    public sealed class PlayOrder
    {
        private sealed class Entry { public string Path; public int Number; }
        private readonly List<string> all;
        private readonly List<string> pending = new List<string>();
        private readonly List<Entry> history = new List<Entry>();
        private readonly Random random;
        private int position = -1, number;
        public bool Shuffle { get; private set; }
        public bool Loop = true;
        public int Count { get { return all.Count; } }
        public string Current { get { return position < 0 ? null : history[position].Path; } }
        public int Number { get { return position < 0 ? 0 : history[position].Number; } }

        public PlayOrder(IEnumerable<string> files, bool shuffle, Random rng)
        {
            all = new List<string>(files); random = rng; Shuffle = shuffle;
        }
        private void Mix(List<string> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); string tmp = list[i]; list[i] = list[j]; list[j] = tmp; }
        }
        public void SetShuffle(bool enabled)
        {
            Shuffle = enabled;
            if (enabled) Mix(pending); else pending.Sort(StringComparer.OrdinalIgnoreCase);
        }
        public bool Next()
        {
            if (position + 1 < history.Count) { position++; return true; }
            if (all.Count == 0) return false;
            if (pending.Count == 0)
            {
                if (history.Count > 0 && !Loop) return false;
                pending.AddRange(all); number = 0;
                if (Shuffle)
                {
                    Mix(pending);
                    if (pending.Count > 1 && pending[0] == Current)
                    { string tmp = pending[0]; pending[0] = pending[1]; pending[1] = tmp; }
                }
            }
            string path = pending[0]; pending.RemoveAt(0);
            history.Add(new Entry { Path = path, Number = ++number });
            position++;
            if (history.Count > 2000) { history.RemoveRange(0, 500); position -= 500; }
            return true;
        }
        public bool Previous()
        {
            if (position <= 0) return false;
            position--; return true;
        }
    }
}
