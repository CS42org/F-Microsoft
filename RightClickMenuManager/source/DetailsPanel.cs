using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace RightClickMenuManager
{
    public class DetailsPanel : FlatControl
    {
        public MenuEntry Entry;
        public Func<MenuEntry, ItemState> EffectiveState = e => e.State;
        public Func<MenuEntry, List<(string Name, ItemState State, bool Current)>> PreviewItems;
        public event Action<MenuEntry, ItemState> StateChosen;
        public event EventHandler CloseClicked;

        readonly FlatButton close = new FlatButton { ButtonKind = FlatButton.Kind.Ghost, Glyph = Glyphs.Close };
        readonly ChipGroup state = new ChipGroup { Style = ChipGroup.Look.Segmented };
        readonly LinkText copy = new LinkText { Text = "Copy registry path", Glyph = Glyphs.Copy, FontSize = 8.5f };
        readonly LinkText regedit = new LinkText { Text = "Open in Registry Editor", Glyph = Glyphs.Registry, FontSize = 8.5f };
        bool updating;

        int yState, yInfo, yLinks, yPreview;
        readonly List<(string Label, string Value, int Y, int H)> rows = new List<(string, string, int, int)>();

        protected override void OnThemeChanged() { BackColor = Theme.Card; base.OnThemeChanged(); }

        public DetailsPanel()
        {
            BackColor = Theme.Card;
            close.Size = new Size(Theme.Px(32), Theme.Px(32));
            close.Click += (s, e) => CloseClicked?.Invoke(this, EventArgs.Empty);
            state.SelectionChanged += (s, e) =>
            {
                if (updating || Entry == null) return;
                StateChosen?.Invoke(Entry, IndexToState(state.SelectedIndex));
            };
            copy.Click += (s, e) =>
            {
                if (Entry == null) return;
                try { Clipboard.SetText(Entry.KeyPath.StartsWith("Windows 11 menu") ? Entry.Clsid : @"HKEY_CLASSES_ROOT\" + Entry.KeyPath); Sounds.Tap(); } catch { }
            };
            regedit.Click += (s, e) =>
            {
                if (Entry == null) return;
                try { MenuRegistry.OpenInRegedit(Entry); } catch { }
            };
            Controls.AddRange(new Control[] { close, state, copy, regedit });
            copy.FitWidth();
            regedit.FitWidth();
        }

        bool IsCommand => Entry != null && Entry.Kind == ItemKind.Command;

        ItemState IndexToState(int i) => IsCommand
            ? (i == 0 ? ItemState.Shown : i == 1 ? ItemState.ShiftOnly : ItemState.Hidden)
            : (i == 0 ? ItemState.Shown : ItemState.Hidden);

        int StateToIndex(ItemState s) => IsCommand
            ? (s == ItemState.Shown ? 0 : s == ItemState.ShiftOnly ? 1 : 2)
            : (s == ItemState.Hidden ? 1 : 0);

        public void ShowEntry(MenuEntry e)
        {
            Entry = e;
            updating = true;
            state.Labels = IsCommand ? new[] { "Shown", "Shift only", "Hidden" } : new[] { "Shown", "Hidden" };
            state.SelectedIndex = StateToIndex(EffectiveState(e));
            updating = false;
            regedit.Visible = !e.KeyPath.StartsWith("Windows 11 menu");
            copy.Text = regedit.Visible ? "Copy registry path" : "Copy CLSID";
            copy.FitWidth();
            Arrange();
            state.Invalidate();
            Invalidate();
        }

        public void RefreshState()
        {
            if (Entry == null) return;
            updating = true;
            state.SelectedIndex = StateToIndex(EffectiveState(Entry));
            updating = false;
            Invalidate();
        }

        void Arrange()
        {
            if (Entry == null) return;
            int pad = Theme.Px(22), w = Width - pad * 2;
            close.Location = new Point(Width - Theme.Px(46), Theme.Px(14));
            int nameH = Theme.MeasureHeight(Entry.Name, Theme.Font(13f, FontStyle.Bold), w - Theme.Px(64));
            int y = Theme.Px(64) + Math.Max(Theme.Px(52), nameH + Theme.Px(24)) + Theme.Px(18);

            yState = y;
            state.SetBounds(pad, y + Theme.Px(26), w, Theme.Px(34));
            y += Theme.Px(26) + Theme.Px(34) + Theme.Px(10);
            y += Theme.Px(22); // hint line
            y += Theme.Px(14);

            yInfo = y;
            rows.Clear();
            string menus = Entry.Menus == (Menus.Classic | Menus.Modern) ? "Classic menu and Windows 11 menu"
                         : Entry.Menus == Menus.Modern ? "Windows 11 menu" : "Classic menu (Show more options)";
            AddRow("Appears on", Entry.AppliesTo, ref y, w);
            AddRow("Menu", menus, ref y, w);
            AddRow(Entry.Kind == ItemKind.Command ? "Runs" : "Provided by", string.IsNullOrEmpty(Entry.Source) ? "Windows" : Entry.Source, ref y, w);
            AddRow(regedit.Visible ? "Registry" : "CLSID",
                regedit.Visible ? @"HKEY_CLASSES_ROOT\" + Entry.KeyPath : Entry.Clsid, ref y, w);

            yLinks = y;
            copy.Location = new Point(pad, y);
            regedit.Location = new Point(pad + copy.Width + Theme.Px(14), y);
            y += Theme.Px(36);
            yPreview = y;
        }

        void AddRow(string label, string value, ref int y, int w)
        {
            int h = Math.Min(Theme.Px(52), Theme.MeasureHeight(value, Theme.Font(9f), w));
            rows.Add((label, value, y, h));
            y += Theme.Px(18) + h + Theme.Px(10);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Arrange(); }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var b = new SolidBrush(Theme.Card);
            e.Graphics.FillRectangle(b, ClientRectangle);
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, 0, 0, Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Entry == null) return;
            var g = e.Graphics;
            Theme.Hq(g);
            int pad = Theme.Px(22), w = Width - pad * 2;

            Theme.Draw(g, "ITEM DETAILS", Theme.Font(8f, FontStyle.Bold), new Rectangle(pad, Theme.Px(20), w, Theme.Px(20)), Theme.SubText);

            // icon + name
            var ib = new Rectangle(pad, Theme.Px(64), Theme.Px(52), Theme.Px(52));
            var icon = IconCache.Get(Entry.IconSpec);
            Theme.Fill(g, ib, Theme.Px(12), Theme.Dark ? Theme.Window : Color.FromArgb(0xF3, 0xF4, 0xF6));
            if (icon != null) g.DrawImage(icon, new Rectangle(ib.X + Theme.Px(10), ib.Y + Theme.Px(10), Theme.Px(32), Theme.Px(32)));
            else Theme.Glyph(g, Entry.Kind == ItemKind.Command ? Glyphs.Command : Glyphs.Puzzle, 16f, ib, Theme.Accent);
            int nx = ib.Right + Theme.Px(14);
            int nameH = Theme.MeasureHeight(Entry.Name, Theme.Font(13f, FontStyle.Bold), Width - nx - pad);
            Theme.DrawWrap(g, Entry.Name, Theme.Font(13f, FontStyle.Bold), new Rectangle(nx, ib.Y, Width - nx - pad, nameH), Theme.Text);
            Theme.Draw(g, Entry.KindText, Theme.Font(9f), new Rectangle(nx, ib.Y + nameH + Theme.Px(4), Width - nx - pad, Theme.Px(18)), Theme.SubText);

            // state section
            Theme.Draw(g, "Show this item", Theme.Font(9.5f, FontStyle.Bold), new Rectangle(pad, yState, w, Theme.Px(20)), Theme.Text);
            string hint = EffectiveState(Entry) switch
            {
                ItemState.ShiftOnly => "Appears only when you hold Shift while right clicking.",
                ItemState.Hidden => Entry.Kind == ItemKind.Extension ? "Hidden from every menu this app adds it to." : "Won't appear in the menu.",
                _ => "Appears every time you right click.",
            };
            Theme.Draw(g, hint, Theme.Font(8.5f), new Rectangle(pad, yState + Theme.Px(66), w, Theme.Px(20)), Theme.SubText);

            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, pad, yInfo - Theme.Px(8), Width - pad, yInfo - Theme.Px(8));

            foreach (var row in rows)
            {
                Theme.Draw(g, row.Label.ToUpperInvariant(), Theme.Font(7.5f, FontStyle.Bold), new Rectangle(pad, row.Y, w, Theme.Px(16)), Theme.Faint);
                Theme.DrawWrap(g, row.Value, Theme.Font(9f), new Rectangle(pad, row.Y + Theme.Px(18), w, row.H), Theme.Text);
            }

            DrawPreview(g, pad, w);
        }

        void DrawPreview(Graphics g, int pad, int w)
        {
            if (PreviewItems == null) return;
            var items = PreviewItems(Entry);
            int y = yPreview;
            bool modernOnly = Entry.Menus == Menus.Modern;
            Theme.Draw(g, modernOnly ? "Preview (Windows 11 menu)" : "Preview (classic menu)", Theme.Font(9.5f, FontStyle.Bold),
                new Rectangle(pad, y, w, Theme.Px(20)), Theme.Text);
            y += Theme.Px(28);

            int rowH = Theme.Px(30);
            int avail = Height - y - Theme.Px(16);
            int maxRows = Math.Max(1, (avail - Theme.Px(12)) / rowH);
            if (items.Count > maxRows)
            {
                int ci = items.FindIndex(i => i.Current);
                int start = Math.Max(0, Math.Min(items.Count - maxRows, ci - maxRows / 2));
                items = items.GetRange(start, maxRows);
            }
            if (items.Count == 0) return;

            int menuW = Math.Min(w, Theme.Px(280));
            var box = new Rectangle(pad, y, menuW, items.Count * rowH + Theme.Px(12));
            // shadow
            for (int i = 3; i >= 1; i--)
                Theme.Fill(g, new RectangleF(box.X - i + 2, box.Y - i + 4, box.Width + i * 2 - 4, box.Height + i * 2 - 4), Theme.Px(10),
                    Color.FromArgb(Theme.Dark ? 30 : 10, 0, 0, 0));
            Theme.Fill(g, box, Theme.Px(8), Theme.Dark ? Color.FromArgb(0x2C, 0x2C, 0x2C) : Color.White);
            Theme.Stroke(g, new RectangleF(box.X + 0.5f, box.Y + 0.5f, box.Width - 1, box.Height - 1), Theme.Px(8), Theme.Border);

            int ry = box.Y + Theme.Px(6);
            foreach (var it in items)
            {
                var r = new Rectangle(box.X + Theme.Px(5), ry, box.Width - Theme.Px(10), rowH);
                bool hidden = it.State == ItemState.Hidden;
                if (it.Current)
                    Theme.Fill(g, r, Theme.Px(5), hidden ? Theme.Window : Theme.AccentSoft);
                var f = Theme.Font(9f, it.Current ? FontStyle.Bold : FontStyle.Regular);
                if (hidden) f = new Font(f, f.Style | FontStyle.Strikeout);
                Color c = hidden ? Theme.Faint : it.Current ? Theme.AccentText : Theme.Text;
                Theme.Draw(g, it.Name, f, new Rectangle(r.X + Theme.Px(30), r.Y, r.Width - Theme.Px(90), r.Height), c);
                if (it.State == ItemState.ShiftOnly)
                    Theme.Draw(g, "Shift", Theme.Font(8f), new Rectangle(r.Right - Theme.Px(60), r.Y, Theme.Px(52), r.Height), Theme.Amber, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                else if (hidden)
                    Theme.Draw(g, "hidden", Theme.Font(8f), new Rectangle(r.Right - Theme.Px(60), r.Y, Theme.Px(52), r.Height), Theme.Faint, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                ry += rowH;
            }
        }
    }
}
