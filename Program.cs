using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FolderSlideShow
{
    internal static class Program
    {
        [DllImport("user32")] static extern bool SetProcessDPIAware();
        [STAThread]
        static void Main(string[] args)
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            List<string> folders = new List<string>();
            string trace = null; bool windowed = false, shellServer = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--windowed") windowed = true;
                else if (args[i] == "--shell-server" || String.Equals(args[i], "-Embedding", StringComparison.OrdinalIgnoreCase)) shellServer = true;
                else if (args[i] == "--trace" && i + 1 < args.Length) trace = Path.GetFullPath(args[++i]);
                else if (!args[i].StartsWith("--")) folders.Add(args[i]);
            }
            try
            {
                if (shellServer) ShellSelection.Run();
                else Application.Run(new PlayerForm(folders.ToArray(), windowed, trace));
            }
            catch (Exception ex) { MessageBox.Show("播放器發生錯誤：\n" + ex.Message, "資料夾隨機放映", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
