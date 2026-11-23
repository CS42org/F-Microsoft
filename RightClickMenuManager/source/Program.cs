using System;
using System.Drawing;
using System.Windows.Forms;

namespace RightClickMenuManager
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { using var g = Graphics.FromHwnd(IntPtr.Zero); Theme.Scale = Math.Max(1f, g.DpiX / 96f); } catch { }
            Theme.Dark = AppSettings.DarkMode;
            Sounds.Enabled = AppSettings.Sounds;
            Application.ThreadException += (s, e) =>
                ModernDialog.Info(null, "Something went wrong", e.Exception.Message, ModernDialog.Mood.Error);
            Application.Run(new MainForm());
        }
    }
}
