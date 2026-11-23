using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RightClickMenuManager
{
    public static class Theme
    {
        public static bool Dark;
        public static float Scale = 1f;
        public static event Action Changed;

        public static Color Window => Dark ? C(0x1B1B1F) : C(0xF3F4F6);
        public static Color Sidebar => Dark ? C(0x202024) : C(0xECEEF2);
        public static Color Card => Dark ? C(0x2A2A2F) : C(0xFFFFFF);
        public static Color CardHover => Dark ? C(0x323238) : C(0xF8FAFC);
        public static Color Border => Dark ? C(0x3A3A41) : C(0xE2E5EA);
        public static Color Text => Dark ? C(0xF3F4F6) : C(0x1F2937);
        public static Color SubText => Dark ? C(0xA1A1AA) : C(0x6B7280);
        public static Color Faint => Dark ? C(0x6B6B73) : C(0xA3A9B3);
        public static Color Accent => Dark ? C(0x3B82F6) : C(0x2563EB);
        public static Color AccentHover => Dark ? C(0x60A5FA) : C(0x1D4ED8);
        public static Color AccentSoft => Dark ? C(0x1E2A4A) : C(0xDBEAFE);
        public static Color AccentText => Dark ? C(0x93C5FD) : C(0x1D4ED8);
        public static Color Amber => Dark ? C(0xF59E0B) : C(0xD97706);
        public static Color AmberSoft => Dark ? C(0x3D2A0A) : C(0xFEF3C7);
        public static Color Green => Dark ? C(0x22C55E) : C(0x16A34A);
        public static Color GreenSoft => Dark ? C(0x123321) : C(0xDCFCE7);
        public static Color Red => Dark ? C(0xF87171) : C(0xDC2626);
        public static Color RedSoft => Dark ? C(0x3F1717) : C(0xFEE2E2);
        public static Color SwitchOff => Dark ? C(0x52525B) : C(0xCBD0D8);
        public static Color Overlay => Dark ? Color.FromArgb(160, 0, 0, 0) : Color.FromArgb(110, 15, 23, 42);

        static Color C(int rgb) => Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);

        public static void SetDark(bool dark)
        {
            if (Dark == dark) return;
            Dark = dark;
            Changed?.Invoke();
        }

        // Fonts

        static string textFamily, iconFamily;

        static void ResolveFamilies()
        {
            if (textFamily != null) return;
            string[] installed;
            try
            {
                using var fc = new InstalledFontCollection();
                installed = fc.Families.Select(f => f.Name).ToArray();
            }
            catch { installed = new string[0]; }
            bool Has(string n) => installed.Any(i => string.Equals(i, n, StringComparison.OrdinalIgnoreCase));
            textFamily = Has("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";
            iconFamily = Has("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
        }

        static readonly System.Collections.Generic.Dictionary<string, Font> cache = new System.Collections.Generic.Dictionary<string, Font>();

        public static Font Font(float size, FontStyle style = FontStyle.Regular)
        {
            ResolveFamilies();
            string key = "t" + size + style;
            if (!cache.TryGetValue(key, out var f))
            {
                string fam = textFamily;
                if (style == FontStyle.Bold && fam == "Segoe UI") fam = "Segoe UI Semibold";
                try { f = new Font(fam, size, fam == "Segoe UI Semibold" ? FontStyle.Regular : style); }
                catch { f = new Font(FontFamily.GenericSansSerif, size, style); }
                cache[key] = f;
            }
            return f;
        }

        public static Font Icons(float size)
        {
            ResolveFamilies();
            string key = "i" + size;
            if (!cache.TryGetValue(key, out var f))
            {
                try { f = new Font(iconFamily, size); }
                catch { f = new Font(FontFamily.GenericSansSerif, size); }
                cache[key] = f;
            }
            return f;
        }

        public static int Px(float v) => (int)Math.Round(v * Scale);

        // Drawing helpers

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Fill(Graphics g, RectangleF r, float radius, Color c)
        {
            using var p = Round(r, radius);
            using var b = new SolidBrush(c);
            g.FillPath(b, p);
        }

        public static void Stroke(Graphics g, RectangleF r, float radius, Color c, float width = 1f)
        {
            using var p = Round(r, radius);
            using var pen = new Pen(c, width);
            g.DrawPath(pen, p);
        }

        public static void Draw(Graphics g, string s, Font f, Rectangle r, Color c,
            TextFormatFlags extra = TextFormatFlags.Left | TextFormatFlags.VerticalCenter)
        {
            TextRenderer.DrawText(g, s, f, r, c,
                extra | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }

        public static void DrawWrap(Graphics g, string s, Font f, Rectangle r, Color c)
        {
            TextRenderer.DrawText(g, s, f, r, c,
                TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding);
        }

        public static void Glyph(Graphics g, string glyph, float size, Rectangle r, Color c)
        {
            TextRenderer.DrawText(g, glyph, Icons(size), r, c,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        public static int Measure(string s, Font f) =>
            TextRenderer.MeasureText(s, f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;

        public static int MeasureHeight(string s, Font f, int width) =>
            TextRenderer.MeasureText(s, f, new Size(width, int.MaxValue), TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height;

        public static void Hq(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        public static void DrawSwitch(Graphics g, Rectangle r, bool on, Color onColor, bool hover)
        {
            Hq(g);
            var track = new RectangleF(r.X, r.Y, r.Width, r.Height);
            if (on) Fill(g, track, r.Height / 2f, hover ? ControlPaint.Light(onColor, 0.15f) : onColor);
            else
            {
                Fill(g, track, r.Height / 2f, hover ? ControlPaint.Dark(SwitchOff, 0.03f) : SwitchOff);
            }
            float k = r.Height - Px(6);
            float x = on ? r.Right - Px(3) - k : r.X + Px(3);
            using var b = new SolidBrush(Color.White);
            g.FillEllipse(b, x, r.Y + Px(3), k, k);
        }

        // Dark title bar on Windows 10 2004+ / 11
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void ApplyTitleBar(Form f)
        {
            try
            {
                int v = Dark ? 1 : 0;
                DwmSetWindowAttribute(f.Handle, 20, ref v, 4);
                int corner = 2; // round corners on Windows 11
                DwmSetWindowAttribute(f.Handle, 33, ref corner, 4);
            }
            catch { }
        }
    }

    public static class Glyphs
    {
        public const string Search = "";
        public const string All = "";
        public const string File = "";
        public const string Folder = "";
        public const string FolderOpen = "";
        public const string Desktop = "";
        public const string Drive = "";
        public const string Command = "";
        public const string Puzzle = "";
        public const string Undo = "";
        public const string Refresh = "";
        public const string Settings = "";
        public const string Info = "";
        public const string Check = "";
        public const string Close = "";
        public const string Image = "";
        public const string Video = "";
        public const string Audio = "";
        public const string Document = "";
        public const string Archive = "";
        public const string Other = "";
        public const string Back = "";
        public const string Link = "";
        public const string Copy = "";
        public const string Restore = "";
        public const string History = "";
        public const string Shield = "";
        public const string Warning = "";
        public const string Error = "";
        public const string Sparkle = "";
        public const string Eye = "";
        public const string Keyboard = "";
        public const string Globe = "";
        public const string Code = "";
        public const string Registry = "";
        public const string Clear = "";
        public const string ChevronRight = "";
        public const string Volume = "";
        public const string Moon = "";
        public const string Explorer = "";
    }
}
