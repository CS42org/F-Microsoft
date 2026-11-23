using Microsoft.Win32;

namespace RightClickMenuManager
{
    public static class AppSettings
    {
        const string Path = @"Software\CS42.org\Right Click Menu Manager";

        public static bool Sounds { get => Get("Sounds", true); set => Set("Sounds", value); }
        public static bool DarkMode { get => Get("DarkMode", false); set => Set("DarkMode", value); }
        public static bool ConfirmRestart { get => Get("ConfirmRestart", true); set => Set("ConfirmRestart", value); }
        public static bool Welcomed { get => Get("Welcomed", false); set => Set("Welcomed", value); }

        public static string ViVeToolPath
        {
            get
            {
                try { using var k = Registry.CurrentUser.OpenSubKey(Path); return k?.GetValue("ViVeToolPath") as string; }
                catch { return null; }
            }
            set
            {
                try { using var k = Registry.CurrentUser.CreateSubKey(Path); k.SetValue("ViVeToolPath", value ?? ""); }
                catch { }
            }
        }

        static bool Get(string name, bool def)
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(Path);
                var v = k?.GetValue(name);
                return v is int i ? i != 0 : def;
            }
            catch { return def; }
        }

        static void Set(string name, bool value)
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(Path);
                k.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
            }
            catch { }
        }
    }
}
