using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Microsoft.Win32;

namespace RightClickMenuManager
{
    public enum ItemKind { Command, Extension }
    public enum ItemState { Shown, Hidden, ShiftOnly }

    [Flags]
    public enum Menus { None = 0, Classic = 1, Modern = 2 }

    [Flags]
    public enum Where { None = 0, Files = 1, Folders = 2, Background = 4, Desktop = 8, Drives = 16 }

    public static class FileGroups
    {
        public const string Any = "Any";
        public static readonly string[] All = { "Any", "Images", "Videos", "Audio", "Documents", "Archives", "Other" };

        static readonly Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static FileGroups()
        {
            void Add(string group, params string[] keys) { foreach (var k in keys) map[k] = group; }
            Add("Images", "image", ".jpg", ".jpeg", ".jpe", ".jfif", ".png", ".bmp", ".dib", ".gif", ".tif", ".tiff",
                ".ico", ".heic", ".heif", ".webp", ".avif", ".svg", ".raw", ".cr2", ".nef", ".dng", ".arw", ".jxr", ".wdp");
            Add("Videos", "video", ".mp4", ".m4v", ".avi", ".mkv", ".wmv", ".mov", ".mpg", ".mpeg", ".webm", ".3gp", ".ts", ".m2ts", ".flv");
            Add("Audio", "audio", ".mp3", ".wav", ".wma", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".mid", ".midi", ".aiff", ".aif");
            Add("Documents", "document", "text", ".txt", ".pdf", ".doc", ".docx", ".rtf", ".odt", ".xls", ".xlsx", ".csv",
                ".ppt", ".pptx", ".md", ".log", ".xml", ".json", ".html", ".htm", ".xps", ".oxps");
            Add("Archives", "compressed", ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".cab", ".iso", ".img", ".vhd", ".vhdx");
        }

        public static string Classify(string key) => map.TryGetValue(key, out var g) ? g : "Other";

        public static string Glyph(string group) => group switch
        {
            "Images" => Glyphs.Image,
            "Videos" => Glyphs.Video,
            "Audio" => Glyphs.Audio,
            "Documents" => Glyphs.Document,
            "Archives" => Glyphs.Archive,
            "Other" => Glyphs.Other,
            _ => Glyphs.All,
        };
    }

    public class MenuEntry
    {
        public ItemKind Kind;
        public string Name;
        public string AppliesTo;
        public Where Where;
        public string FileGroup = FileGroups.Any;
        public string KeyPath;      // relative to HKEY_CLASSES_ROOT
        public string Clsid;        // extensions only
        public string Source;
        public string IconSpec;     // "path,index" when known
        public ItemState State;     // what the registry says right now
        public Menus Menus = Menus.Classic;
        public bool Detected = true;

        public string KindText => Kind == ItemKind.Command ? "Command" : "App extension";
        public string Id => (Kind == ItemKind.Command ? "C|" : "E|") + KeyPath;

        public static string StateText(ItemState s) => s switch
        {
            ItemState.Shown => "Shown",
            ItemState.Hidden => "Hidden",
            _ => "Shift only",
        };
    }

    public class BackupInfo
    {
        public string Dir;
        public DateTime Date;
        public string[] Changes = new string[0];
    }

    public static class MenuRegistry
    {
        public const string BlockedPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";

        public static string BackupRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Right Click Menu Manager Backups");

        static readonly (string Key, string Label, Where Where)[] Locations =
        {
            ("*", "All files", Where.Files),
            ("AllFilesystemObjects", "Files and folders", Where.Files | Where.Folders),
            ("Directory", "Folders", Where.Folders),
            ("Folder", "Folders", Where.Folders),
            ("Directory\\Background", "Folder background", Where.Background),
            ("LibraryFolder\\background", "Library background", Where.Background),
            ("DesktopBackground", "Desktop", Where.Desktop),
            ("Drive", "Drives", Where.Drives),
        };

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        static extern int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, int cchOutBuf, IntPtr ppvReserved);

        public static List<MenuEntry> Scan()
        {
            var result = new List<MenuEntry>();
            var blocked = ReadBlocked();

            var locs = new List<(string Key, string Label, Where Where, string Group)>();
            foreach (var l in Locations) locs.Add((l.Key, l.Label, l.Where, FileGroups.Any));

            using (var sfa = SafeOpen(Registry.ClassesRoot, "SystemFileAssociations"))
            {
                if (sfa != null)
                {
                    foreach (var sub in SafeSubKeys(sfa))
                    {
                        string group = FileGroups.Classify(sub);
                        string label = sub.StartsWith(".") ? group + " (" + sub.ToLowerInvariant() + ")"
                                                           : "All " + sub.ToLowerInvariant() + " files";
                        locs.Add(("SystemFileAssociations\\" + sub, label, Where.Files, group));
                    }
                }
            }

            foreach (var loc in locs)
            {
                ScanCommands(loc.Key, loc.Label, loc.Where, loc.Group, result);
                ScanExtensions(loc.Key, loc.Label, loc.Where, loc.Group, blocked, result);
            }
            AddModernItems(blocked, result);
            return result;
        }

        static void ScanCommands(string loc, string label, Where where, string group, List<MenuEntry> result)
        {
            using var shell = SafeOpen(Registry.ClassesRoot, loc + "\\shell");
            if (shell == null) return;

            foreach (var verb in SafeSubKeys(shell))
            {
                using var vk = SafeOpen(shell, verb);
                if (vk == null) continue;
                if (vk.GetValue("ProgrammaticAccessOnly") != null) continue;

                string name = Resolve(vk.GetValue("MUIVerb") as string);
                if (string.IsNullOrWhiteSpace(name)) name = Resolve(vk.GetValue(null) as string);
                if (string.IsNullOrWhiteSpace(name)) name = Capitalize(verb);
                name = CleanName(name);

                string source = null;
                using (var cmd = SafeOpen(vk, "command"))
                    source = cmd?.GetValue(null) as string;
                if (string.IsNullOrEmpty(source) && vk.GetValue("ExplorerCommandHandler") is string ech)
                    source = "Handler " + ech;
                if (string.IsNullOrEmpty(source) && vk.GetValue("SubCommands") != null)
                    source = "Opens a submenu";

                string icon = vk.GetValue("Icon") as string;
                if (string.IsNullOrWhiteSpace(icon)) icon = ExeFromCommand(source);

                ItemState state;
                if (vk.GetValue("LegacyDisable") != null || vk.GetValue("HideBasedOnVelocityId") != null)
                    state = ItemState.Hidden;
                else if (vk.GetValue("Extended") != null)
                    state = ItemState.ShiftOnly;
                else
                    state = ItemState.Shown;

                result.Add(new MenuEntry
                {
                    Kind = ItemKind.Command,
                    Name = name,
                    AppliesTo = label,
                    Where = where,
                    FileGroup = group,
                    KeyPath = loc + "\\shell\\" + verb,
                    Source = source ?? "",
                    IconSpec = icon,
                    State = state,
                });
            }
        }

        static void ScanExtensions(string loc, string label, Where where, string group, HashSet<string> blocked, List<MenuEntry> result)
        {
            using var handlers = SafeOpen(Registry.ClassesRoot, loc + "\\shellex\\ContextMenuHandlers");
            if (handlers == null) return;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sub in SafeSubKeys(handlers))
            {
                using var hk = SafeOpen(handlers, sub);
                string def = hk?.GetValue(null) as string;

                string clsid = null;
                bool dashed = false;
                if (!string.IsNullOrEmpty(def))
                {
                    string t = def.Trim();
                    if (t.StartsWith("-") && IsGuid(t.Substring(1))) { clsid = t.Substring(1); dashed = true; }
                    else if (IsGuid(t)) clsid = t;
                }
                if (clsid == null && IsGuid(sub)) clsid = sub;
                if (clsid == null) continue;
                clsid = NormalizeGuid(clsid);
                if (!seen.Add(clsid)) continue;

                string clsidName = null, dll = null;
                using (var ck = SafeOpen(Registry.ClassesRoot, "CLSID\\" + clsid))
                {
                    clsidName = Resolve(ck?.GetValue(null) as string);
                    using var ip = SafeOpen(ck, "InprocServer32");
                    dll = ip?.GetValue(null) as string;
                }

                string name;
                if (!IsGuid(sub)) name = sub;
                else if (!string.IsNullOrWhiteSpace(clsidName)) name = clsidName;
                else name = "Unknown extension";

                result.Add(new MenuEntry
                {
                    Kind = ItemKind.Extension,
                    Name = CleanName(name),
                    AppliesTo = label,
                    Where = where,
                    FileGroup = group,
                    KeyPath = loc + "\\shellex\\ContextMenuHandlers\\" + sub,
                    Clsid = clsid,
                    Source = string.IsNullOrEmpty(dll)
                        ? (string.IsNullOrWhiteSpace(clsidName) ? clsid : clsidName + "  " + clsid)
                        : dll,
                    IconSpec = dll,
                    State = (dashed || blocked.Contains(clsid)) ? ItemState.Hidden : ItemState.Shown,
                });
            }
        }

        // Items from the new Windows 11 menu. They are hidden the same way as app extensions (by CLSID).
        static readonly (string Clsid, string Name, Where Where, string Group, string AppliesTo)[] ModernItems =
        {
            ("{9F156763-7844-4DC4-B2B1-901F640F5155}", "Open in Terminal", Where.Folders | Where.Background, FileGroups.Any, "Folders and folder background"),
            ("{02DB545A-3E20-46DE-83A5-1329B1E88B6B}", "Open in Terminal Preview", Where.Folders | Where.Background, FileGroups.Any, "Folders and folder background"),
            ("{CB3B0003-8088-4EDE-8769-8B354AB2FF8C}", "Ask Copilot", Where.Files, FileGroups.Any, "Files"),
            ("{CA6CC9F1-867A-481E-951E-A28C5E4F01EA}", "Edit in Notepad", Where.Files, "Documents", "Text files"),
            ("{E2BF9676-5F8F-435C-97EB-11607A5BEDF7}", "Share", Where.Files | Where.Folders, FileGroups.Any, "Files and folders"),
            ("{2430F218-B743-4FD6-97BF-5C76541B4AE9}", "Edit with Paint", Where.Files, "Images", "Images"),
            ("{BFE0E2A4-C70C-4AD7-AC3D-10D1ECEBB5B4}", "Edit with Photos", Where.Files, "Images", "Images"),
            ("{8AB635F8-9A67-4698-AB99-784AD929F332}", "Edit with Clipchamp", Where.Files, "Videos", "Videos and images"),
        };

        static void AddModernItems(HashSet<string> blocked, List<MenuEntry> result)
        {
            foreach (var m in ModernItems)
            {
                string clsid = NormalizeGuid(m.Clsid);
                bool detected = SafeOpen(Registry.ClassesRoot, "CLSID\\" + clsid) != null ||
                                SafeOpen(Registry.ClassesRoot, "PackagedCom\\ClassIndex\\" + clsid) != null;
                // If a classic entry already uses this CLSID, mark it as appearing in both menus instead of adding a duplicate.
                var existing = result.Where(r => r.Kind == ItemKind.Extension && string.Equals(r.Clsid, clsid, StringComparison.OrdinalIgnoreCase)).ToList();
                if (existing.Count > 0)
                {
                    foreach (var x in existing) x.Menus |= Menus.Modern;
                    continue;
                }
                // Skip items whose app isn't on this PC, unless they were hidden before (so they can be shown again).
                if (!detected && !blocked.Contains(clsid)) continue;
                result.Add(new MenuEntry
                {
                    Kind = ItemKind.Extension,
                    Name = m.Name,
                    AppliesTo = m.AppliesTo,
                    Where = m.Where,
                    FileGroup = m.Group,
                    KeyPath = "Windows 11 menu\\" + clsid,
                    Clsid = clsid,
                    Source = detected ? "Built into the Windows 11 menu   " + clsid : "Not found on this PC   " + clsid,
                    State = blocked.Contains(clsid) ? ItemState.Hidden : ItemState.Shown,
                    Menus = Menus.Modern,
                    Detected = detected,
                });
            }
        }

        // Menu style: the classic (Windows 10 style) menu as the default instead of the Windows 11 menu.

        const string ClassicKey1 = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
        const string ClassicKey2 = @"Software\Classes\CLSID\{51668040-5e31-4023-bbe3-149163bd80f1}";
        public const string NewMenuFeatureId = "60813048";

        public static bool ClassicMenuEnabled
        {
            get
            {
                try
                {
                    using var k = Registry.CurrentUser.OpenSubKey(ClassicKey1 + "\\InprocServer32");
                    return k != null;
                }
                catch { return false; }
            }
        }

        public static void SetClassicMenu(bool classic)
        {
            if (classic)
            {
                foreach (var key in new[] { ClassicKey1, ClassicKey2 })
                    using (var k = Registry.CurrentUser.CreateSubKey(key + "\\InprocServer32"))
                        k.SetValue(null, "", RegistryValueKind.String);
            }
            else
            {
                foreach (var key in new[] { ClassicKey1, ClassicKey2 })
                    try { Registry.CurrentUser.DeleteSubKeyTree(key, false); } catch { }
            }
        }

        // True on builds that include the November 2026 update (KB5132061), which is reported to ignore the registry method.
        public static bool HasNewMenuEngine(out string build)
        {
            build = "";
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                int cur = int.Parse((k.GetValue("CurrentBuild") as string) ?? "0", CultureInfo.InvariantCulture);
                int ubr = k.GetValue("UBR") is int u ? u : 0;
                build = cur + "." + ubr;
                if (cur > 26300) return true;
                return (cur == 26100 || cur == 26200 || cur == 26300) && ubr >= 9974;
            }
            catch { return false; }
        }

        // Runs ViVeTool (supplied by the user) to turn the new menu feature off or back to default.
        public static int RunViVeTool(string exe, bool disableNewMenu, out string output)
        {
            var psi = new ProcessStartInfo(exe, (disableNewMenu ? "/disable" : "/reset") + " /id:" + NewMenuFeatureId)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(exe),
            };
            using var p = Process.Start(psi);
            output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit(30000);
            return p.ExitCode;
        }

        public static void SetState(MenuEntry e, ItemState state)
        {
            if (e.Kind == ItemKind.Command) SetCommandState(e, state);
            else SetExtensionState(e, state == ItemState.Hidden);
        }

        static void SetCommandState(MenuEntry e, ItemState state)
        {
            // Writes through HKCR land in HKCU\Software\Classes when the key lives there, otherwise HKLM.
            using var k = Registry.ClassesRoot.OpenSubKey(e.KeyPath, true)
                          ?? throw new InvalidOperationException("This item no longer exists. Click Refresh.");
            switch (state)
            {
                case ItemState.Shown:
                    k.DeleteValue("LegacyDisable", false);
                    k.DeleteValue("Extended", false);
                    k.DeleteValue("HideBasedOnVelocityId", false);
                    break;
                case ItemState.Hidden:
                    k.SetValue("LegacyDisable", "", RegistryValueKind.String);
                    break;
                case ItemState.ShiftOnly:
                    k.DeleteValue("LegacyDisable", false);
                    k.DeleteValue("HideBasedOnVelocityId", false);
                    k.SetValue("Extended", "", RegistryValueKind.String);
                    break;
            }
            e.State = state;
        }

        static void SetExtensionState(MenuEntry e, bool hide)
        {
            if (hide)
            {
                using var b = Registry.LocalMachine.CreateSubKey(BlockedPath, true);
                b.SetValue(e.Clsid, e.Name ?? "", RegistryValueKind.String);
                e.State = ItemState.Hidden;
                return;
            }

            using (var b = Registry.LocalMachine.OpenSubKey(BlockedPath, true))
                b?.DeleteValue(e.Clsid, false);
            using (var b = Registry.CurrentUser.OpenSubKey(BlockedPath, true))
                b?.DeleteValue(e.Clsid, false);

            if (e.KeyPath.StartsWith("Windows 11 menu")) { e.State = ItemState.Shown; return; }
            using (var hk = Registry.ClassesRoot.OpenSubKey(e.KeyPath, true))
            {
                if (hk?.GetValue(null) is string def && def.Trim().StartsWith("-"))
                    hk.SetValue(null, def.Trim().Substring(1), RegistryValueKind.String);
            }
            e.State = ItemState.Shown;
        }

        public static bool IsProtectedError(Exception ex) => ex is UnauthorizedAccessException || ex is SecurityException;

        // Backups: a snapshot of every item's state, saved before each apply.

        public static string SaveBackup(IEnumerable<MenuEntry> all, IEnumerable<string> changes)
        {
            string dir = Path.Combine(BackupRoot, DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(dir);
            var sb = new StringBuilder();
            sb.AppendLine("# Right Click Menu Manager snapshot (developed by CS42.org)");
            foreach (var e in all)
                sb.Append(e.Kind).Append('\t').Append(e.KeyPath).Append('\t').Append(e.Clsid ?? "").Append('\t').Append(e.State).AppendLine();
            File.WriteAllText(Path.Combine(dir, "snapshot.tsv"), sb.ToString(), Encoding.UTF8);
            File.WriteAllLines(Path.Combine(dir, "changes.txt"), changes, Encoding.UTF8);
            return dir;
        }

        public static void AppendResult(string dir, IEnumerable<string> lines)
        {
            try { File.AppendAllLines(Path.Combine(dir, "changes.txt"), lines, Encoding.UTF8); } catch { }
        }

        public static List<BackupInfo> ListBackups()
        {
            var list = new List<BackupInfo>();
            if (!Directory.Exists(BackupRoot)) return list;
            foreach (var d in Directory.GetDirectories(BackupRoot))
            {
                if (!File.Exists(Path.Combine(d, "snapshot.tsv"))) continue;
                if (!DateTime.TryParseExact(Path.GetFileName(d), "yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var date))
                    date = Directory.GetCreationTime(d);
                string[] changes = new string[0];
                try { changes = File.ReadAllLines(Path.Combine(d, "changes.txt")).Where(l => l.Trim().Length > 0).ToArray(); } catch { }
                list.Add(new BackupInfo { Dir = d, Date = date, Changes = changes });
            }
            return list.OrderByDescending(b => b.Date).ToList();
        }

        // Returns id -> state saved in the snapshot.
        public static Dictionary<string, ItemState> LoadSnapshot(string dir)
        {
            var map = new Dictionary<string, ItemState>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadAllLines(Path.Combine(dir, "snapshot.tsv")))
            {
                if (line.StartsWith("#")) continue;
                var p = line.Split('\t');
                if (p.Length < 4) continue;
                if (!Enum.TryParse(p[0], out ItemKind kind) || !Enum.TryParse(p[3], out ItemState st)) continue;
                map[(kind == ItemKind.Command ? "C|" : "E|") + p[1]] = st;
            }
            return map;
        }

        public static void RestartExplorer()
        {
            foreach (var p in Process.GetProcessesByName("explorer"))
            {
                try { p.Kill(); p.WaitForExit(5000); } catch { }
            }
            System.Threading.Thread.Sleep(1500);
            if (Process.GetProcessesByName("explorer").Length == 0)
                Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"));
        }

        public static void OpenInRegedit(MenuEntry e)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit"))
                k.SetValue("LastKey", @"Computer\HKEY_CLASSES_ROOT\" + e.KeyPath);
            foreach (var p in Process.GetProcessesByName("regedit"))
            {
                try { p.Kill(); p.WaitForExit(3000); } catch { }
            }
            Process.Start("regedit.exe", "-m");
        }

        // helpers

        static HashSet<string> ReadBlocked()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                using var b = SafeOpen(root, BlockedPath);
                if (b == null) continue;
                foreach (var n in b.GetValueNames())
                    if (IsGuid(n)) set.Add(NormalizeGuid(n));
            }
            return set;
        }

        static RegistryKey SafeOpen(RegistryKey parent, string path)
        {
            if (parent == null) return null;
            try { return parent.OpenSubKey(path, false); } catch { return null; }
        }

        static string[] SafeSubKeys(RegistryKey k)
        {
            try { return k.GetSubKeyNames(); } catch { return new string[0]; }
        }

        static bool IsGuid(string s) => !string.IsNullOrEmpty(s) && s.Trim().StartsWith("{") && Guid.TryParse(s.Trim(), out _);

        static string NormalizeGuid(string s) => Guid.Parse(s.Trim()).ToString("B").ToUpperInvariant();

        static string ExeFromCommand(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd) || cmd.StartsWith("Handler") || cmd.StartsWith("Opens")) return null;
            cmd = cmd.Trim();
            if (cmd.StartsWith("\""))
            {
                int end = cmd.IndexOf('"', 1);
                return end > 1 ? cmd.Substring(1, end - 1) : null;
            }
            int exe = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            return exe > 0 ? cmd.Substring(0, exe + 4) : cmd.Split(' ')[0];
        }

        static string Resolve(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (!s.StartsWith("@")) return s;
            var sb = new StringBuilder(1024);
            try
            {
                if (SHLoadIndirectString(Environment.ExpandEnvironmentVariables(s), sb, sb.Capacity, IntPtr.Zero) == 0)
                    return sb.ToString();
            }
            catch { }
            return null;
        }

        static string CleanName(string s) => s.Replace("&&", "\u0001").Replace("&", "").Replace("\u0001", "&").Trim();

        static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
