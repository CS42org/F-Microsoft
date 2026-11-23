using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace RightClickMenuManager
{
    // Scrollable list of item cards, each with a switch. Fully owner-drawn.
    public class CardList : FlatControl
    {
        public List<MenuEntry> Items = new List<MenuEntry>();
        public Func<MenuEntry, ItemState> EffectiveState = e => e.State;
        public Func<MenuEntry, bool> IsPending = e => false;
        public event Action<MenuEntry> ToggleClicked;
        public event Action<MenuEntry> CardClicked;
        public event Action ClearFiltersClicked;
        public MenuEntry Current;
        public string EmptyTitle = "No items match your filters";

        int scroll;
        int hover = -1;
        bool hoverSwitch;
        bool dragging;
        int dragStartY, dragStartScroll;
        Rectangle clearLink;

        int CardH => Theme.Px(74);
        int Gap => Theme.Px(8);
        int Pad => Theme.Px(16);
        int ContentH => Items.Count == 0 ? 0 : Items.Count * (CardH + Gap) + Pad * 2 - Gap;
        int MaxScroll => Math.Max(0, ContentH - Height);
        bool HasBar => ContentH > Height;

        public CardList() { TabStop = true; }

        public void SetItems(List<MenuEntry> items, bool keepScroll)
        {
            Items = items;
            if (!keepScroll) scroll = 0;
            scroll = Math.Max(0, Math.Min(scroll, MaxScroll));
            hover = -1;
            Invalidate();
        }

        Rectangle CardRect(int i)
        {
            int w = Width - Pad * 2 - (HasBar ? Theme.Px(8) : 0);
            return new Rectangle(Pad, Pad + i * (CardH + Gap) - scroll, w, CardH);
        }

        Rectangle SwitchRect(Rectangle card) =>
            new Rectangle(card.Right - Theme.Px(76), card.Y + (card.Height - Theme.Px(24)) / 2, Theme.Px(46), Theme.Px(24));

        Rectangle ThumbRect()
        {
            if (!HasBar) return Rectangle.Empty;
            int track = Height - Theme.Px(8);
            int th = Math.Max(Theme.Px(40), (int)((long)track * Height / ContentH));
            int ty = Theme.Px(4) + (int)((long)(track - th) * scroll / Math.Max(1, MaxScroll));
            return new Rectangle(Width - Theme.Px(9), ty, Theme.Px(6), th);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hq(g);

            if (Items.Count == 0)
            {
                var c = new Rectangle(0, Height / 2 - Theme.Px(70), Width, Theme.Px(40));
                Theme.Glyph(g, Glyphs.Search, 22f, c, Theme.Faint);
                Theme.Draw(g, EmptyTitle, Theme.Font(11f, FontStyle.Bold),
                    new Rectangle(0, c.Bottom + Theme.Px(10), Width, Theme.Px(24)), Theme.Text, TextFormatFlags.HorizontalCenter);
                string link = "Clear all filters";
                int lw = Theme.Measure(link, Theme.Font(9.5f));
                clearLink = new Rectangle((Width - lw) / 2, c.Bottom + Theme.Px(40), lw, Theme.Px(22));
                Theme.Draw(g, link, Theme.Font(9.5f), clearLink, Theme.AccentText, TextFormatFlags.HorizontalCenter);
                return;
            }

            int first = Math.Max(0, (scroll - Pad) / (CardH + Gap));
            for (int i = first; i < Items.Count; i++)
            {
                var r = CardRect(i);
                if (r.Y > Height) break;
                DrawCard(g, Items[i], r, i == hover);
            }

            if (HasBar)
            {
                var t = ThumbRect();
                Theme.Fill(g, t, t.Width / 2f, dragging ? Theme.SubText : Theme.Faint);
            }
        }

        void DrawCard(Graphics g, MenuEntry e, Rectangle r, bool hov)
        {
            var st = EffectiveState(e);
            bool pending = IsPending(e);
            bool isCurrent = Current != null && Current == e;
            var rf = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1);

            Theme.Fill(g, rf, Theme.Px(12), hov ? Theme.CardHover : Theme.Card);
            Color border = isCurrent ? Theme.Accent : pending ? Color.FromArgb(150, Theme.Accent) : Theme.Border;
            Theme.Stroke(g, rf, Theme.Px(12), border, isCurrent ? 1.6f : 1f);

            // icon
            var ib = new Rectangle(r.X + Theme.Px(16), r.Y + (r.Height - Theme.Px(42)) / 2, Theme.Px(42), Theme.Px(42));
            var icon = IconCache.Get(e.IconSpec);
            bool faded = st == ItemState.Hidden;
            if (icon != null)
            {
                Theme.Fill(g, ib, Theme.Px(10), Theme.Dark ? Theme.Window : Color.FromArgb(0xF3, 0xF4, 0xF6));
                var dst = new Rectangle(ib.X + Theme.Px(7), ib.Y + Theme.Px(7), Theme.Px(28), Theme.Px(28));
                if (faded)
                {
                    using var ia = new ImageAttributes();
                    ia.SetColorMatrix(new ColorMatrix { Matrix33 = 0.4f });
                    g.DrawImage(icon, dst, 0, 0, icon.Width, icon.Height, GraphicsUnit.Pixel, ia);
                }
                else g.DrawImage(icon, dst);
            }
            else
            {
                Color tint = e.Menus.HasFlag(Menus.Modern) && !e.Menus.HasFlag(Menus.Classic) ? Theme.Green
                           : e.Kind == ItemKind.Command ? Theme.Accent : Color.FromArgb(0x93, 0x33, 0xEA);
                Theme.Fill(g, ib, Theme.Px(10), Color.FromArgb(faded ? 18 : 34, tint));
                Theme.Glyph(g, e.Kind == ItemKind.Command ? Glyphs.Command : Glyphs.Puzzle, 14f, ib,
                    faded ? Color.FromArgb(110, tint) : tint);
            }

            int x = ib.Right + Theme.Px(14);
            var sw = SwitchRect(r);
            int textRight = sw.X - Theme.Px(16);
            Theme.Draw(g, e.Name, Theme.Font(10.5f, FontStyle.Bold),
                new Rectangle(x, r.Y + Theme.Px(13), textRight - x, Theme.Px(22)), faded ? Theme.SubText : Theme.Text);

            // tags
            int tx = x, ty = r.Y + Theme.Px(41);
            if (pending) tx = DrawTag(g, "Not applied yet", tx, ty, textRight, Theme.AccentSoft, Theme.AccentText, null);
            tx = DrawTag(g, e.AppliesTo, tx, ty, textRight, Theme.Window, Theme.SubText, WhereGlyph(e));
            tx = DrawTag(g, e.KindText, tx, ty, textRight, Theme.Window, Theme.SubText, null);
            string menuTag = e.Menus == (Menus.Classic | Menus.Modern) ? "Both menus"
                           : e.Menus == Menus.Modern ? "Windows 11 menu" : "Classic menu";
            tx = DrawTag(g, menuTag, tx, ty, textRight,
                e.Menus.HasFlag(Menus.Modern) ? Theme.GreenSoft : Theme.Window,
                e.Menus.HasFlag(Menus.Modern) ? Theme.Green : Theme.SubText, null);
            if (st == ItemState.ShiftOnly) tx = DrawTag(g, "Shift + right click", tx, ty, textRight, Theme.AmberSoft, Theme.Amber, Glyphs.Keyboard);
            if (!e.Detected) tx = DrawTag(g, "Not detected", tx, ty, textRight, Theme.Window, Theme.Faint, null);

            bool on = st != ItemState.Hidden;
            Theme.DrawSwitch(g, sw, on, st == ItemState.ShiftOnly ? Theme.Amber : Theme.Accent, hov && hoverSwitch);
            Theme.Glyph(g, Glyphs.ChevronRight, 8f, new Rectangle(r.Right - Theme.Px(18), r.Y, Theme.Px(12), r.Height),
                hov ? Theme.SubText : Theme.Faint);
        }

        static string WhereGlyph(MenuEntry e)
        {
            if (e.FileGroup != FileGroups.Any) return FileGroups.Glyph(e.FileGroup);
            if (e.Where.HasFlag(Where.Files)) return Glyphs.File;
            if (e.Where.HasFlag(Where.Folders)) return Glyphs.Folder;
            if (e.Where.HasFlag(Where.Background)) return Glyphs.FolderOpen;
            if (e.Where.HasFlag(Where.Desktop)) return Glyphs.Desktop;
            if (e.Where.HasFlag(Where.Drives)) return Glyphs.Drive;
            return null;
        }

        int DrawTag(Graphics g, string text, int x, int y, int right, Color bg, Color fg, string glyph)
        {
            var f = Theme.Font(8.25f);
            int w = Theme.Measure(text, f) + Theme.Px(16) + (glyph != null ? Theme.Px(16) : 0);
            if (x + w > right) return x;
            var r = new Rectangle(x, y, w, Theme.Px(20));
            Theme.Fill(g, r, Theme.Px(6), bg);
            int tx = x + Theme.Px(8);
            if (glyph != null)
            {
                Theme.Glyph(g, glyph, 8f, new Rectangle(tx - 1, y, Theme.Px(14), r.Height), fg);
                tx += Theme.Px(16);
            }
            Theme.Draw(g, text, f, new Rectangle(tx, y, w - (tx - x), r.Height), fg);
            return x + w + Theme.Px(6);
        }

        int HitCard(Point p)
        {
            if (p.X >= Width - Theme.Px(12) && HasBar) return -1;
            int i = (p.Y + scroll - Pad) / (CardH + Gap);
            if (i < 0 || i >= Items.Count) return -1;
            return CardRect(i).Contains(p) ? i : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging)
            {
                int track = Height - Theme.Px(8);
                int th = ThumbRect().Height;
                int dy = e.Y - dragStartY;
                scroll = Math.Max(0, Math.Min(MaxScroll, dragStartScroll + (int)((long)dy * MaxScroll / Math.Max(1, track - th))));
                Invalidate();
                return;
            }
            int h = HitCard(e.Location);
            bool hs = h >= 0 && SwitchRect(CardRect(h)).Contains(e.Location);
            if (h != hover || hs != hoverSwitch) { hover = h; hoverSwitch = hs; Invalidate(); }
            Cursor = h >= 0 || (Items.Count == 0 && clearLink.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (HasBar && e.X >= Width - Theme.Px(12))
            {
                var t = ThumbRect();
                if (!t.Contains(e.Location))
                {
                    scroll = Math.Max(0, Math.Min(MaxScroll, scroll + (e.Y < t.Y ? -Height : Height)));
                    Invalidate();
                    return;
                }
                dragging = true;
                dragStartY = e.Y;
                dragStartScroll = scroll;
                Capture = true;
                Invalidate();
                return;
            }
            if (Items.Count == 0 && clearLink.Contains(e.Location)) { ClearFiltersClicked?.Invoke(); return; }
            int h = HitCard(e.Location);
            if (h < 0) return;
            var item = Items[h];
            if (SwitchRect(CardRect(h)).Contains(e.Location)) ToggleClicked?.Invoke(item);
            else CardClicked?.Invoke(item);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (dragging) { dragging = false; Capture = false; Invalidate(); }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            scroll = Math.Max(0, Math.Min(MaxScroll, scroll - e.Delta * (CardH + Gap) / 120));
            Invalidate();
            OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, e.X, e.Y, 0));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            scroll = Math.Max(0, Math.Min(scroll, MaxScroll));
        }

        protected override bool IsInputKey(Keys k) =>
            k == Keys.Up || k == Keys.Down || k == Keys.Space || k == Keys.Enter || k == Keys.Home || k == Keys.End || base.IsInputKey(k);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Items.Count == 0) return;
            int i = Current == null ? -1 : Items.IndexOf(Current);
            switch (e.KeyCode)
            {
                case Keys.Down: i = Math.Min(Items.Count - 1, i + 1); break;
                case Keys.Up: i = Math.Max(0, i - 1); break;
                case Keys.Home: i = 0; break;
                case Keys.End: i = Items.Count - 1; break;
                case Keys.PageDown: i = Math.Min(Items.Count - 1, i + Math.Max(1, Height / (CardH + Gap))); break;
                case Keys.PageUp: i = Math.Max(0, i - Math.Max(1, Height / (CardH + Gap))); break;
                case Keys.Space:
                    if (Current != null) ToggleClicked?.Invoke(Current);
                    return;
                case Keys.Enter:
                    if (Current != null) CardClicked?.Invoke(Current);
                    return;
                default: return;
            }
            if (i < 0) i = 0;
            Current = Items[i];
            EnsureVisible(i);
            Invalidate();
            CardClicked?.Invoke(Current);
            e.Handled = true;
        }

        void EnsureVisible(int i)
        {
            var r = CardRect(i);
            if (r.Y < 0) scroll += r.Y - Pad;
            else if (r.Bottom > Height) scroll += r.Bottom - Height + Pad;
            scroll = Math.Max(0, Math.Min(MaxScroll, scroll));
        }
    }
}
