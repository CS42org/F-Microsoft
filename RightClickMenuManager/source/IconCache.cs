using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace RightClickMenuManager
{
    public static class IconCache
    {
        static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern uint ExtractIconEx(string file, int index, IntPtr[] large, IntPtr[] small, uint count);

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr h);

        public static Bitmap Get(string spec)
        {
            if (string.IsNullOrWhiteSpace(spec)) return null;
            if (cache.TryGetValue(spec, out var bmp)) return bmp;
            bmp = Load(spec);
            cache[spec] = bmp;
            return bmp;
        }

        static Bitmap Load(string spec)
        {
            try
            {
                string path = spec.Trim().Trim('"');
                int index = 0;
                int comma = path.LastIndexOf(',');
                if (comma > 1 && int.TryParse(path.Substring(comma + 1).Trim(), out int idx))
                {
                    index = idx;
                    path = path.Substring(0, comma).Trim().Trim('"');
                }
                path = Environment.ExpandEnvironmentVariables(path);
                if (!Path.IsPathRooted(path))
                {
                    string sys = Path.Combine(Environment.SystemDirectory, path);
                    string win = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), path);
                    path = File.Exists(sys) ? sys : File.Exists(win) ? win : path;
                }
                if (!File.Exists(path)) return null;

                var large = new IntPtr[1];
                var small = new IntPtr[1];
                if (ExtractIconEx(path, index, large, small, 1) > 0 && large[0] != IntPtr.Zero)
                {
                    try
                    {
                        using var ico = Icon.FromHandle(large[0]);
                        return ico.ToBitmap();
                    }
                    finally
                    {
                        DestroyIcon(large[0]);
                        if (small[0] != IntPtr.Zero) DestroyIcon(small[0]);
                    }
                }
                if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    using var ico = Icon.ExtractAssociatedIcon(path);
                    return ico?.ToBitmap();
                }
            }
            catch { }
            return null;
        }
    }
}
