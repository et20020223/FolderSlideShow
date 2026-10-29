using System;
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
            string folder = null, trace = null; bool windowed = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--windowed") windowed = true;
                else if (args[i] == "--trace" && i + 1 < args.Length) trace = Path.GetFullPath(args[++i]);
                else if (!args[i].StartsWith("--")) folder = args[i];
            }
            try { Application.Run(new PlayerForm(folder, windowed, trace)); }
            catch (Exception ex) { MessageBox.Show("播放器發生錯誤：\n" + ex.Message, "資料夾隨機放映", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
