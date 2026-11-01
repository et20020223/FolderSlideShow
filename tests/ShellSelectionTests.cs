using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ComDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

static class ShellSelectionTests
{
    static int checks;
    static MethodInfo read;
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
    static string[] Read(object data) { return (string[])read.Invoke(null, new object[] { data }); }
    static bool Rejects(object data)
    {
        try { Read(data); return false; }
        catch (TargetInvocationException) { return true; }
    }
    [STAThread]
    static void Main(string[] args)
    {
        Assembly app = Assembly.LoadFrom(args[0]);
        Type dropType = app.GetType("FolderSlideShow.FolderDropTarget", true);
        read = dropType.GetMethod("ReadFolders", BindingFlags.Static | BindingFlags.NonPublic);
        string temp = Path.Combine(Path.GetTempPath(), "FolderSlideShow-shell-" + Guid.NewGuid().ToString("N"));
        string a = Path.Combine(temp, "相簿 A & 符號 (一)");
        string b = Path.Combine(temp, "相簿 B 空格");
        Directory.CreateDirectory(a); Directory.CreateDirectory(b);
        try
        {
            ComDataObject selection = (ComDataObject)new DataObject(DataFormats.FileDrop, new string[] { a, b });
            Check(Read(selection).SequenceEqual(new string[] { a, b }), "native CF_HDROP preserves the complete Unicode multi-selection");
            Check(Read(new DataObject(DataFormats.FileDrop, new string[] { a, a + "\\.", b })).Length == 2, "duplicate shell paths normalized");
            Check(Read(new DataObject(DataFormats.FileDrop, new string[] { a })).Single() == a, "single folder remains supported");
            Check(Rejects(null), "null data rejected");
            Check(Rejects(new DataObject(DataFormats.Text, "untrusted text")), "non-file data rejected without deserialization");
            Check(Rejects(new DataObject(DataFormats.FileDrop, new string[] { a, Path.Combine(temp, "missing") })), "missing folder does not silently reduce selection");
            string file = Path.Combine(temp, "not-a-folder.txt"); File.WriteAllText(file, "fixture");
            try { Check(Rejects(new DataObject(DataFormats.FileDrop, new string[] { file })), "non-directory input rejected"); }
            finally { File.Delete(file); }
            object target = Activator.CreateInstance(dropType);
            object point = Activator.CreateInstance(app.GetType("FolderSlideShow.ShellPoint", true));
            object[] enter = new object[] { selection, 0u, point, 3u };
            Check((int)dropType.GetMethod("DragEnter").Invoke(target, enter) == 0 && (uint)enter[3] == 1u, "shell offer accepts COPY only, never MOVE");
            dropType.GetMethod("DragLeave").Invoke(target, null);
            object[] over = new object[] { 0u, point, 3u };
            dropType.GetMethod("DragOver").Invoke(target, over);
            Check((uint)over[2] == 0u, "cancelled shell offer cleared");
            Console.WriteLine("All " + checks + " shell checks passed.");
        }
        finally { Directory.Delete(a); Directory.Delete(b); Directory.Delete(temp); }
    }
}
