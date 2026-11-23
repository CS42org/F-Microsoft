using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace RightClickMenuManager
{
    public static class Links
    {
        public const string Website = "https://cs42.org";
        public const string GitHub = "https://github.com/CS42org";
        public const string ViVeTool = "https://github.com/thebookisclosed/ViVe/releases";

        // Opened through Explorer so the browser does not inherit admin rights.
        public static void Open(string url)
        {
            try { Process.Start("explorer.exe", url); }
            catch { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { } }
        }
    }

    public static class AppIcon
    {
        static Bitmap big;
        public static Bitmap Big
        {
            get
            {
                if (big != null) return big;
                try
                {
                    using var ico = new Icon(Icon.ExtractAssociatedIcon(Application.ExecutablePath), 256, 256);
                    big = ico.ToBitmap();
                }
                catch { }
                return big;
            }
        }

        public static void Draw(Graphics g, Rectangle r)
        {
            var b = Big;
            if (b != null) { g.DrawImage(b, r); return; }
            Theme.Fill(g, r, r.Width * 0.22f, Theme.Accent);
            Theme.Glyph(g, Glyphs.Command, r.Width * 0.3f, r, Color.White);
        }
    }

    // A rounded card that hosts child controls.
    public class CardBox : FlatControl
    {
        public string Title, Subtitle, Glyph;
        public int HeaderHeight => string.IsNullOrEmpty(Title) ? 0 : Theme.Px(string.IsNullOrEmpty(Subtitle) ? 52 : 70);

        public CardBox() { BackColor = Theme.Card; }

        protected override void OnThemeChanged() { BackColor = Theme.Card; base.OnThemeChanged(); }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            var g = e.Graphics;
            Theme.Hq(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Theme.Fill(g, r, Theme.Px(14), Theme.Card);
            Theme.Stroke(g, r, Theme.Px(14), Theme.Border);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (string.IsNullOrEmpty(Title)) return;
            var g = e.Graphics;
            int x = Theme.Px(20);
            if (Glyph != null)
            {
                Theme.Glyph(g, Glyph, 12f, new Rectangle(x, Theme.Px(18), Theme.Px(22), Theme.Px(24)), Theme.Accent);
                x += Theme.Px(32);
            }
            Theme.Draw(g, Title, Theme.Font(11.5f, FontStyle.Bold), new Rectangle(x, Theme.Px(18), Width - x - Theme.Px(20), Theme.Px(24)), Theme.Text);
            if (!string.IsNullOrEmpty(Subtitle))
                Theme.Draw(g, Subtitle, Theme.Font(9f), new Rectangle(x, Theme.Px(42), Width - x - Theme.Px(20), Theme.Px(20)), Theme.SubText);
        }
    }

    public class WelcomeOverlay : FlatControl
    {
        public event EventHandler Done;
        readonly FlatButton start = new FlatButton { Text = "Get started", ButtonKind = FlatButton.Kind.Primary, Height = Theme.Px(44), Radius = 10 };
        readonly LinkText site = new LinkText { Text = "cs42.org", Glyph = Glyphs.Globe };
        readonly LinkText gh = new LinkText { Text = "GitHub", Glyph = Glyphs.Code };
        Rectangle card;

        static readonly (string Glyph, string Title, string Text)[] Benefits =
        {
            (Glyphs.Eye, "Hide the clutter", "Turn menu items on or off with a switch, for files, folders, the desktop and drives."),
            (Glyphs.Command, "Classic or Windows 11 menu", "Bring back the full classic menu, or tidy up the new Windows 11 one."),
            (Glyphs.History, "Safe by design", "A backup is saved before every change, so you can restore anytime."),
        };

        public WelcomeOverlay()
        {
            BackColor = Theme.Card;
            start.Width = Theme.Px(220);
            start.Click += (s, e) => { Sounds.Success(); Done?.Invoke(this, EventArgs.Empty); };
            site.Click += (s, e) => Links.Open(Links.Website);
            gh.Click += (s, e) => Links.Open(Links.GitHub);
            site.FitWidth(); gh.FitWidth();
            Controls.AddRange(new Control[] { start, site, gh });
        }

        protected override void OnThemeChanged() { BackColor = Theme.Card; base.OnThemeChanged(); }

        protected override void OnLayout(LayoutEventArgs e)
        {
            int w = Theme.Px(600), h = Theme.Px(560);
            card = new Rectangle((Width - w) / 2, Math.Max(Theme.Px(10), (Height - h) / 2), w, h);
            start.Location = new Point(card.X + (w - start.Width) / 2, card.Bottom - Theme.Px(136));
            int linksW = site.Width + gh.Width + Theme.Px(18);
            int ly = card.Bottom - Theme.Px(46);
            site.Location = new Point(card.X + (w - linksW) / 2 + Theme.Px(0), ly);
            gh.Location = new Point(site.Right + Theme.Px(18), ly);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(Theme.Window)) g.FillRectangle(b, ClientRectangle);
            Theme.Hq(g);
            using (var b = new SolidBrush(Color.FromArgb(Theme.Dark ? 40 : 28, Theme.Accent)))
            {
                g.FillEllipse(b, -Width / 4, -Height / 3, Width / 1.6f, Height / 1.2f);
                g.FillEllipse(b, Width * 0.6f, Height * 0.45f, Width / 2f, Height / 1.4f);
            }
            Theme.Fill(g, card, Theme.Px(20), Theme.Card);
            Theme.Stroke(g, new RectangleF(card.X + 0.5f, card.Y + 0.5f, card.Width - 1, card.Height - 1), Theme.Px(20), Theme.Border);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hq(g);
            int cx = card.X, w = card.Width;
            AppIcon.Draw(g, new Rectangle(cx + (w - Theme.Px(72)) / 2, card.Y + Theme.Px(34), Theme.Px(72), Theme.Px(72)));
            Theme.Draw(g, "Right Click Menu Manager", Theme.Font(18f, FontStyle.Bold),
                new Rectangle(cx, card.Y + Theme.Px(118), w, Theme.Px(34)), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            Theme.Draw(g, "Choose what shows when you right click", Theme.Font(10.5f),
                new Rectangle(cx, card.Y + Theme.Px(154), w, Theme.Px(24)), Theme.SubText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            int y = card.Y + Theme.Px(204);
            foreach (var b in Benefits)
            {
                var c = new Rectangle(cx + Theme.Px(64), y, Theme.Px(40), Theme.Px(40));
                Theme.Fill(g, c, Theme.Px(10), Theme.AccentSoft);
                Theme.Glyph(g, b.Glyph, 12f, c, Theme.Accent);
                int tx = c.Right + Theme.Px(16);
                Theme.Draw(g, b.Title, Theme.Font(10.5f, FontStyle.Bold), new Rectangle(tx, y, card.Right - tx - Theme.Px(56), Theme.Px(20)), Theme.Text);
                Theme.DrawWrap(g, b.Text, Theme.Font(9f), new Rectangle(tx, y + Theme.Px(21), card.Right - tx - Theme.Px(56), Theme.Px(36)), Theme.SubText);
                y += Theme.Px(66);
            }
            Theme.Draw(g, "Developed by CS42.org", Theme.Font(8.5f),
                new Rectangle(cx, card.Bottom - Theme.Px(70), w, Theme.Px(20)), Theme.Faint, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    public class BackupRow : FlatControl
    {
        public BackupInfo Info;
        public bool Original;
        public readonly FlatButton Restore = new FlatButton { Text = "Restore", Glyph = Glyphs.Restore, Height = Theme.Px(32) };

        public BackupRow()
        {
            Height = Theme.Px(62);
            BackColor = Theme.Card;
            Restore.FitWidth();
            Controls.Add(Restore);
        }

        protected override void OnThemeChanged() { BackColor = Theme.Card; base.OnThemeChanged(); }

        protected override void OnLayout(LayoutEventArgs e) =>
            Restore.Location = new Point(Width - Restore.Width - Theme.Px(16), (Height - Restore.Height) / 2);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, Theme.Px(16), 0, Width - Theme.Px(16), 0);
            int x = Theme.Px(20);
            Theme.Glyph(g, Glyphs.History, 11f, new Rectangle(x, 0, Theme.Px(22), Height), Theme.SubText);
            x += Theme.Px(34);
            string when = Info.Date.ToString("dddd d MMMM yyyy, h:mm tt");
            int right = Restore.Left - Theme.Px(12);
            Theme.Draw(g, when + (Original ? "   (original menu)" : ""), Theme.Font(9.5f, FontStyle.Bold),
                new Rectangle(x, Theme.Px(11), right - x, Theme.Px(20)), Theme.Text);
            string desc = Info.Changes.Length == 0 ? "Snapshot before changes"
                : "Saved before: " + string.Join(", ", Info.Changes);
            Theme.Draw(g, desc, Theme.Font(8.5f), new Rectangle(x, Theme.Px(32), right - x, Theme.Px(18)), Theme.SubText);
        }
    }

    // Settings, menu style and backups.
    public class SettingsPage : ThemedPanel
    {
        public event Action<bool> MenuStyleRequested;
        public event Action<string> RestoreRequested;
        public event Action ResetRequested;

        readonly ThemedPanel column = new ThemedPanel();
        readonly CardBox styleCard = new CardBox { Title = "Menu style", Glyph = Glyphs.Command, Subtitle = "Pick which menu opens when you right click a file or folder" };
        public readonly ChipGroup Style = new ChipGroup { Style = ChipGroup.Look.Segmented, Labels = new[] { "Windows 11 menu", "Classic menu (Windows 10 style)" } };
        readonly ThemedLabel styleNote = new ThemedLabel { Wrap = true, FontSize = 9f };
        readonly ThemedLabel buildNote = new ThemedLabel { Wrap = true, FontSize = 9f, Fore = () => Theme.Amber };
        readonly ThemedLabel viveTitle = new ThemedLabel { Text = "Stronger method with ViVeTool (optional)", Bold = true, FontSize = 9.5f, Fore = () => Theme.Text };
        readonly ThemedLabel viveNote = new ThemedLabel { Wrap = true, FontSize = 9f };
        readonly FlatButton viveLocate = new FlatButton { Text = "Locate ViVeTool.exe", Glyph = Glyphs.Search };
        readonly LinkText viveGet = new LinkText { Text = "Download ViVeTool", Glyph = Glyphs.Link };
        public readonly ToggleRow ViveUse = new ToggleRow { Title = "Also turn off the new menu with ViVeTool", Description = "Used when you switch to the classic menu. Switching back resets it.", Glyph = Glyphs.Settings };

        readonly CardBox prefsCard = new CardBox { Title = "Preferences", Glyph = Glyphs.Settings };
        public readonly ToggleRow SoundsRow = new ToggleRow { Title = "Sounds", Description = "Soft clicks and a chime when changes are applied", Glyph = Glyphs.Volume };
        public readonly ToggleRow DarkRow = new ToggleRow { Title = "Dark mode", Description = "Use a dark theme for this app", Glyph = Glyphs.Moon };
        public readonly ToggleRow ConfirmRow = new ToggleRow { Title = "Confirm before restarting Explorer", Description = "Ask before applying changes", Glyph = Glyphs.Explorer };

        readonly CardBox backupCard = new CardBox { Title = "Backups", Glyph = Glyphs.History, Subtitle = "A snapshot is saved automatically before every change you apply" };
        readonly FlatButton resetBtn = new FlatButton { Text = "Restore original menu", Glyph = Glyphs.Restore, ButtonKind = FlatButton.Kind.Danger };
        readonly FlatButton folderBtn = new FlatButton { Text = "Open backups folder", Glyph = Glyphs.Folder };
        readonly ThemedLabel noBackups = new ThemedLabel { Text = "No backups yet. One is created the first time you apply a change.", FontSize = 9f };
        readonly List<BackupRow> rows = new List<BackupRow>();
        bool syncing;

        public SettingsPage()
        {
            AutoScroll = true;
            Back = () => Theme.Window;
            BackColor = Theme.Window;
            Controls.Add(column);

            Style.SelectionChanged += (s, e) => { if (!syncing) MenuStyleRequested?.Invoke(Style.SelectedIndex == 1); };
            viveLocate.FitWidth();
            viveGet.FitWidth();
            viveGet.Click += (s, e) => Links.Open(Links.ViVeTool);
            viveLocate.Click += (s, e) => LocateViVe();
            ViveUse.Toggled += (s, e) =>
            {
                if (ViveUse.On && string.IsNullOrEmpty(AppSettings.ViVeToolPath)) { ViveUse.On = false; ViveUse.Invalidate(); LocateViVe(); }
            };
            styleCard.Controls.AddRange(new Control[] { Style, styleNote, buildNote, viveTitle, viveNote, viveLocate, viveGet, ViveUse });

            prefsCard.Controls.AddRange(new Control[] { SoundsRow, DarkRow, ConfirmRow });

            resetBtn.FitWidth();
            folderBtn.FitWidth();
            resetBtn.Click += (s, e) => ResetRequested?.Invoke();
            folderBtn.Click += (s, e) =>
            {
                System.IO.Directory.CreateDirectory(MenuRegistry.BackupRoot);
                try { Process.Start("explorer.exe", "\"" + MenuRegistry.BackupRoot + "\""); } catch { }
            };
            backupCard.Controls.AddRange(new Control[] { resetBtn, folderBtn, noBackups });

            column.Controls.AddRange(new Control[] { styleCard, prefsCard, backupCard });
            Resize += (s, e) => Relayout();
        }

        void LocateViVe()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Locate ViVeTool.exe",
                Filter = "ViVeTool|ViVeTool.exe;vivetool.exe|Programs (*.exe)|*.exe",
                FileName = "ViVeTool.exe",
            };
            if (ofd.ShowDialog(FindForm()) == DialogResult.OK)
            {
                AppSettings.ViVeToolPath = ofd.FileName;
                ViveUse.On = true;
                Sounds.ToggleOn();
                Sync();
            }
        }

        public void Sync()
        {
            syncing = true;
            Style.SelectedIndex = MenuRegistry.ClassicMenuEnabled ? 1 : 0;
            syncing = false;
            SoundsRow.On = AppSettings.Sounds;
            DarkRow.On = AppSettings.DarkMode;
            ConfirmRow.On = AppSettings.ConfirmRestart;

            bool newEngine = MenuRegistry.HasNewMenuEngine(out string build);
            styleNote.Text = MenuRegistry.ClassicMenuEnabled
                ? "The classic menu is turned on. It shows every item straight away, without Show more options."
                : "The Windows 11 menu is in use. Choose Classic to open the full menu straight away. You can still use Shift + right click for the classic menu at any time.";
            buildNote.Text = newEngine
                ? "Your Windows build (" + build + ") includes the November 2026 update (KB5132061). Reports say it can ignore the classic menu setting. If the Windows 11 menu still appears after switching, use the ViVeTool option below or hold Shift while right clicking."
                : "";
            string path = AppSettings.ViVeToolPath;
            viveNote.Text = string.IsNullOrEmpty(path)
                ? "ViVeTool is a free tool that can switch off the new menu feature (ID " + MenuRegistry.NewMenuFeatureId + "). Download it, extract the zip, then locate ViVeTool.exe here. Future Windows updates may undo this."
                : "Using: " + path;
            viveLocate.Text = string.IsNullOrEmpty(path) ? "Locate ViVeTool.exe" : "Change location";
            viveLocate.FitWidth();

            foreach (var r in rows) { backupCard.Controls.Remove(r); r.Dispose(); }
            rows.Clear();
            var list = MenuRegistry.ListBackups();
            for (int i = 0; i < list.Count && i < 25; i++)
            {
                var row = new BackupRow { Info = list[i], Original = i == list.Count - 1 };
                string dir = list[i].Dir;
                row.Restore.Click += (s, e) => RestoreRequested?.Invoke(dir);
                rows.Add(row);
                backupCard.Controls.Add(row);
            }
            noBackups.Visible = list.Count == 0;
            resetBtn.Enabled = list.Count > 0;
            Relayout();
            Invalidate(true);
        }

        void Relayout()
        {
            int maxW = Theme.Px(820);
            int sbw = VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0;
            int w = Math.Min(maxW, ClientSize.Width - Theme.Px(48));
            if (w < Theme.Px(300)) w = Theme.Px(300);
            int x = Math.Max(Theme.Px(24), (ClientSize.Width - w) / 2);
            int pad = Theme.Px(20), iw = w - pad * 2;

            // Menu style card
            int y = styleCard.HeaderHeight;
            Style.SetBounds(pad, y, iw, Theme.Px(36));
            y += Theme.Px(48);
            styleNote.SetBounds(pad, y, iw, styleNote.PreferredHeight(iw)); y = styleNote.Bottom + Theme.Px(10);
            buildNote.Visible = buildNote.Text.Length > 0;
            if (buildNote.Visible) { buildNote.SetBounds(pad, y, iw, buildNote.PreferredHeight(iw)); y = buildNote.Bottom + Theme.Px(14); }
            viveTitle.SetBounds(pad, y, iw, Theme.Px(22)); y += Theme.Px(26);
            viveNote.SetBounds(pad, y, iw, viveNote.PreferredHeight(iw)); y = viveNote.Bottom + Theme.Px(10);
            viveLocate.Location = new Point(pad, y);
            viveGet.Location = new Point(viveLocate.Right + Theme.Px(16), y + (viveLocate.Height - viveGet.Height) / 2);
            y += viveLocate.Height + Theme.Px(10);
            ViveUse.SetBounds(Theme.Px(6), y, w - Theme.Px(12), ViveUse.Height); y += ViveUse.Height + Theme.Px(12);
            styleCard.SetBounds(0, 0, w, y);

            // Preferences card
            int py = prefsCard.HeaderHeight;
            foreach (var r in new[] { SoundsRow, DarkRow, ConfirmRow })
            {
                r.SetBounds(Theme.Px(6), py, w - Theme.Px(12), r.Height);
                py += r.Height;
            }
            prefsCard.SetBounds(0, styleCard.Bottom + Theme.Px(18), w, py + Theme.Px(10));

            // Backups card
            int by = backupCard.HeaderHeight;
            resetBtn.Location = new Point(pad, by);
            folderBtn.Location = new Point(resetBtn.Right + Theme.Px(10), by);
            by += resetBtn.Height + Theme.Px(14);
            if (noBackups.Visible) { noBackups.SetBounds(pad, by, iw, Theme.Px(24)); by += Theme.Px(36); }
            foreach (var r in rows) { r.SetBounds(0, by, w, r.Height); by += r.Height; }
            backupCard.SetBounds(0, prefsCard.Bottom + Theme.Px(18), w, by + Theme.Px(8));

            column.SetBounds(x, Theme.Px(24) + AutoScrollPosition.Y, w, backupCard.Bottom + Theme.Px(24));
        }

    }

    public class AboutPage : FlatControl
    {
        readonly LinkText site = new LinkText { Text = "cs42.org", Glyph = Glyphs.Globe, FontSize = 10.5f, Bold = true };
        readonly LinkText gh = new LinkText { Text = "github.com/CS42org", Glyph = Glyphs.Code, FontSize = 10.5f, Bold = true };
        Rectangle card;

        public AboutPage()
        {
            BackColor = Theme.Card;
            site.Height = gh.Height = Theme.Px(26);
            site.FitWidth(); gh.FitWidth();
            site.Click += (s, e) => Links.Open(Links.Website);
            gh.Click += (s, e) => Links.Open(Links.GitHub);
            Controls.AddRange(new Control[] { site, gh });
        }

        protected override void OnThemeChanged() { BackColor = Theme.Card; base.OnThemeChanged(); }

        protected override void OnLayout(LayoutEventArgs e)
        {
            int w = Theme.Px(560), h = Theme.Px(470);
            card = new Rectangle((Width - w) / 2, Math.Max(Theme.Px(20), (Height - h) / 2), w, h);
            int ly = card.Y + Theme.Px(258);
            int total = site.Width + gh.Width + Theme.Px(28);
            site.Location = new Point(card.X + (w - total) / 2, ly);
            gh.Location = new Point(site.Right + Theme.Px(28), ly);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(Theme.Window)) g.FillRectangle(b, ClientRectangle);
            Theme.Hq(g);
            Theme.Fill(g, card, Theme.Px(20), Theme.Card);
            Theme.Stroke(g, new RectangleF(card.X + 0.5f, card.Y + 0.5f, card.Width - 1, card.Height - 1), Theme.Px(20), Theme.Border);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hq(g);
            int cx = card.X, w = card.Width;
            var center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
            AppIcon.Draw(g, new Rectangle(cx + (w - Theme.Px(80)) / 2, card.Y + Theme.Px(34), Theme.Px(80), Theme.Px(80)));
            Theme.Draw(g, "Right Click Menu Manager", Theme.Font(17f, FontStyle.Bold), new Rectangle(cx, card.Y + Theme.Px(126), w, Theme.Px(32)), Theme.Text, center);
            Theme.Draw(g, "Version 2.0", Theme.Font(9f), new Rectangle(cx, card.Y + Theme.Px(160), w, Theme.Px(20)), Theme.SubText, center);
            Theme.Draw(g, "Developed by CS42.org", Theme.Font(11f, FontStyle.Bold), new Rectangle(cx, card.Y + Theme.Px(212), w, Theme.Px(24)), Theme.AccentText, center);
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, cx + Theme.Px(40), card.Y + Theme.Px(306), card.Right - Theme.Px(40), card.Y + Theme.Px(306));
            string text = "Choose what appears in the Windows right click menu, switch between the classic and Windows 11 menus, " +
                          "and restore your menu anytime from automatic backups.";
            Theme.DrawWrap(g, text, Theme.Font(9.5f), new Rectangle(cx + Theme.Px(48), card.Y + Theme.Px(326), w - Theme.Px(96), Theme.Px(60)), Theme.SubText);
            Theme.Draw(g, "Shortcuts:  Ctrl+F search   •   Space toggle   •   Ctrl+Z undo   •   F5 refresh", Theme.Font(8.5f),
                new Rectangle(cx, card.Bottom - Theme.Px(46), w, Theme.Px(20)), Theme.Faint, center);
        }
    }
}
