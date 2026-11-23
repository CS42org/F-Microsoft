using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RightClickMenuManager
{
    public class FlatControl : Control
    {
        public FlatControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            Theme.Changed += OnThemeChanged;
        }

        protected virtual void OnThemeChanged() => Invalidate();

        protected override void Dispose(bool disposing)
        {
            if (disposing) Theme.Changed -= OnThemeChanged;
            base.Dispose(disposing);
        }

        protected Color ParentBack => Parent?.BackColor ?? Theme.Window;

        // Pass wheel scrolling to the nearest scrollable page.
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            for (Control p = Parent; p != null; p = p.Parent)
            {
                if (p is ScrollableControl sc && sc.AutoScroll && sc.VerticalScroll.Visible)
                {
                    int ny = Math.Max(0, -sc.AutoScrollPosition.Y - e.Delta);
                    sc.AutoScrollPosition = new Point(0, ny);
                    if (e is HandledMouseEventArgs h) h.Handled = true;
                    break;
                }
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var b = new SolidBrush(ParentBack);
            e.Graphics.FillRectangle(b, ClientRectangle);
        }
    }

    // Single-select group drawn as wrapping chips, a segmented control, or tabs.
    public class ChipGroup : FlatControl
    {
        public enum Look { Chips, Segmented }

        public Look Style = Look.Chips;
        public string[] Labels = new string[0];
        public string[] GlyphList;
        public int[] Counts;
        int selected;
        int hover = -1;
        readonly List<Rectangle> rects = new List<Rectangle>();
        public event EventHandler SelectionChanged;

        public ChipGroup() { Cursor = Cursors.Hand; TabStop = true; }

        public int SelectedIndex
        {
            get => selected;
            set { if (value != selected) { selected = value; Invalidate(); SelectionChanged?.Invoke(this, EventArgs.Empty); } }
        }

        int ChipH => Theme.Px(Style == Look.Segmented ? 32 : 30);
        int Gap => Theme.Px(6);

        string LabelAt(int i) => Counts != null && i < Counts.Length && Counts[i] >= 0 ? Labels[i] + "  " + Counts[i] : Labels[i];

        public int PreferredHeight(int width)
        {
            Compute(width);
            if (rects.Count == 0) return ChipH;
            int bottom = 0;
            foreach (var r in rects) bottom = Math.Max(bottom, r.Bottom);
            return bottom + 1;
        }

        void Compute(int width)
        {
            rects.Clear();
            if (Labels.Length == 0) return;
            if (Style == Look.Segmented)
            {
                int w = (width - 2) / Labels.Length;
                for (int i = 0; i < Labels.Length; i++)
                    rects.Add(new Rectangle(1 + i * w, 1, i == Labels.Length - 1 ? width - 2 - i * w : w, ChipH - 2));
                return;
            }
            var f = Theme.Font(9f);
            int x = 0, y = 0;
            for (int i = 0; i < Labels.Length; i++)
            {
                int w = Theme.Measure(LabelAt(i), f) + Theme.Px(24);
                if (GlyphList != null && GlyphList[i] != null) w += Theme.Px(22);
                if (x > 0 && x + w > width) { x = 0; y += ChipH + Gap; }
                rects.Add(new Rectangle(x, y, w, ChipH));
                x += w + Gap;
            }
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Compute(Width); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hq(g);
            Compute(Width);
            var f = Theme.Font(9f);
            var fb = Theme.Font(9f, FontStyle.Bold);

            if (Style == Look.Segmented)
            {
                var outer = new RectangleF(0.5f, 0.5f, Width - 1, ChipH - 1);
                Theme.Fill(g, outer, Theme.Px(8), Theme.Dark ? Theme.Window : Theme.Border);
            }

            for (int i = 0; i < rects.Count; i++)
            {
                var r = rects[i];
                bool sel = i == selected, hov = i == hover;
                if (Style == Look.Segmented)
                {
                    var rr = new RectangleF(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4);
                    if (sel)
                    {
                        Theme.Fill(g, rr, Theme.Px(6), Theme.Card);
                        if (!Theme.Dark) Theme.Stroke(g, rr, Theme.Px(6), Color.FromArgb(18, 0, 0, 0));
                    }
                    else if (hov) Theme.Fill(g, rr, Theme.Px(6), Color.FromArgb(Theme.Dark ? 30 : 60, Theme.Card));
                    Theme.Draw(g, LabelAt(i), sel ? fb : f, r, sel ? Theme.Text : Theme.SubText,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    continue;
                }

                var rf = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1);
                Color bg = sel ? Theme.AccentSoft : hov ? Theme.CardHover : Theme.Card;
                Color fg = sel ? Theme.AccentText : Theme.Text;
                Theme.Fill(g, rf, r.Height / 2f, bg);
                Theme.Stroke(g, rf, r.Height / 2f, sel ? Color.FromArgb(120, Theme.Accent) : Theme.Border);
                int tx = r.X + Theme.Px(12);
                if (GlyphList != null && GlyphList[i] != null)
                {
                    Theme.Glyph(g, GlyphList[i], 9.5f, new Rectangle(tx, r.Y, Theme.Px(18), r.Height), sel ? Theme.Accent : Theme.SubText);
                    tx += Theme.Px(22);
                }
                Theme.Draw(g, LabelAt(i), sel ? fb : f, new Rectangle(tx, r.Y, r.Right - tx - Theme.Px(8), r.Height), fg);
            }
        }

        int HitTest(Point p) { for (int i = 0; i < rects.Count; i++) if (rects[i].Contains(p)) return i; return -1; }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h != hover) { hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h >= 0 && h != selected) { Sounds.Tap(); SelectedIndex = h; }
            Focus();
        }

        protected override bool IsInputKey(Keys k) => k == Keys.Left || k == Keys.Right || base.IsInputKey(k);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Right && selected < Labels.Length - 1) SelectedIndex++;
            if (e.KeyCode == Keys.Left && selected > 0) SelectedIndex--;
        }
    }

    public class FlatButton : FlatControl
    {
        public enum Kind { Primary, Secondary, Ghost, Danger }
        public Kind ButtonKind = Kind.Secondary;
        public string Glyph;
        public float Radius = 8;
        bool hover, down;

        public FlatButton()
        {
            Cursor = Cursors.Hand;
            TabStop = true;
            Height = Theme.Px(34);
        }

        public void FitWidth()
        {
            int w = Theme.Measure(Text ?? "", Theme.Font(9.5f, FontStyle.Bold)) + Theme.Px(32);
            if (!string.IsNullOrEmpty(Glyph)) w += string.IsNullOrEmpty(Text) ? 0 : Theme.Px(24);
            if (string.IsNullOrEmpty(Text)) w = Height;
            Width = w;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hq(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Color bg, fg, border = Color.Empty;
            switch (ButtonKind)
            {
                case Kind.Primary:
                    bg = !Enabled ? Theme.SwitchOff : down ? Theme.AccentHover : hover ? Theme.AccentHover : Theme.Accent;
                    fg = Color.White;
                    break;
                case Kind.Danger:
                    bg = hover ? Theme.RedSoft : Theme.Card;
                    fg = Theme.Red;
                    border = Theme.Border;
                    break;
                case Kind.Ghost:
                    bg = down ? Theme.Border : hover ? Theme.CardHover : Color.Transparent;
                    fg = Enabled ? Theme.Text : Theme.Faint;
                    break;
                default:
                    bg = down ? Theme.Border : hover ? Theme.CardHover : Theme.Card;
                    fg = Enabled ? Theme.Text : Theme.Faint;
                    border = Theme.Border;
                    break;
            }
            if (bg.A > 0) Theme.Fill(g, r, Theme.Px(Radius), bg);
            if (border != Color.Empty) Theme.Stroke(g, r, Theme.Px(Radius), border);

            var font = Theme.Font(9.5f, ButtonKind == Kind.Primary ? FontStyle.Bold : FontStyle.Regular);
            if (string.IsNullOrEmpty(Text))
            {
                Theme.Glyph(g, Glyph, 11f, ClientRectangle, fg);
                return;
            }
            int tw = Theme.Measure(Text, font);
            int gw = string.IsNullOrEmpty(Glyph) ? 0 : Theme.Px(24);
            int x = (Width - tw - gw) / 2;
            if (gw > 0) Theme.Glyph(g, Glyph, 10f, new Rectangle(x, 0, Theme.Px(18), Height), fg);
            Theme.Draw(g, Text, font, new Rectangle(x + gw, 0, tw + 2, Height), fg);
            if (Focused && ShowFocusCues)
                Theme.Stroke(g, new RectangleF(2, 2, Width - 5, Height - 5), Theme.Px(Radius - 2), Theme.Accent);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) OnClick(EventArgs.Empty);
        }
    }

    public class SearchBox : FlatControl
    {
        public readonly TextBox Box = new TextBox();
        public event EventHandler SearchChanged;
        readonly FlatButton clear = new FlatButton { ButtonKind = FlatButton.Kind.Ghost, Glyph = Glyphs.Close };

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);

        public SearchBox()
        {
            Height = Theme.Px(38);
            Box.BorderStyle = BorderStyle.None;
            Box.Font = Theme.Font(10f);
            Box.TextChanged += (s, e) => { clear.Visible = Box.Text.Length > 0; SearchChanged?.Invoke(this, e); };
            Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { Box.Clear(); e.SuppressKeyPress = true; } };
            Box.HandleCreated += (s, e) => { try { SendMessage(Box.Handle, 0x1501, (IntPtr)1, "Search menu items"); } catch { } };
            clear.Visible = false;
            clear.Click += (s, e) => { Box.Clear(); Box.Focus(); };
            Controls.Add(Box);
            Controls.Add(clear);
            Cursor = Cursors.IBeam;
            ApplyColors();
        }

        protected override void OnThemeChanged() { ApplyColors(); base.OnThemeChanged(); }

        void ApplyColors() { Box.BackColor = Theme.Card; Box.ForeColor = Theme.Text; }

        protected override void OnLayout(LayoutEventArgs e)
        {
            int h = Box.PreferredHeight;
            clear.SetBounds(Width - Theme.Px(34), (Height - Theme.Px(28)) / 2, Theme.Px(28), Theme.Px(28));
            Box.SetBounds(Theme.Px(38), (Height - h) / 2, Width - Theme.Px(38) - Theme.Px(40), h);
        }

        protected override void OnClick(EventArgs e) => Box.Focus();

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hq(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Theme.Fill(g, r, Theme.Px(10), Theme.Card);
            Theme.Stroke(g, r, Theme.Px(10), Box.Focused ? Theme.Accent : Theme.Border, Box.Focused ? 1.5f : 1f);
            Theme.Glyph(g, Glyphs.Search, 10.5f, new Rectangle(Theme.Px(10), 0, Theme.Px(22), Height), Theme.SubText);
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            Box.GotFocus += (s, e) => Invalidate();
            Box.LostFocus += (s, e) => Invalidate();
        }
    }

    // Settings row: title, description and a switch.
    public class ToggleRow : FlatControl
    {
        public string Title, Description, Glyph;
        public bool On;
        public event EventHandler Toggled;
        bool hover;

        public ToggleRow() { Height = Theme.Px(64); Cursor = Cursors.Hand; TabStop = true; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hq(g);
            if (hover) Theme.Fill(g, new RectangleF(0, 0, Width, Height), Theme.Px(8), Theme.CardHover);
            int x = Theme.Px(14);
            if (Glyph != null)
            {
                Theme.Fill(g, new RectangleF(x, (Height - Theme.Px(34)) / 2f, Theme.Px(34), Theme.Px(34)), Theme.Px(8), Theme.AccentSoft);
                Theme.Glyph(g, Glyph, 11f, new Rectangle(x, (Height - Theme.Px(34)) / 2, Theme.Px(34), Theme.Px(34)), Theme.Accent);
                x += Theme.Px(48);
            }
            int right = Width - Theme.Px(70);
            Theme.Draw(g, Title, Theme.Font(10f, FontStyle.Bold), new Rectangle(x, Theme.Px(12), right - x, Theme.Px(20)), Theme.Text);
            Theme.Draw(g, Description, Theme.Font(9f), new Rectangle(x, Theme.Px(33), right - x, Theme.Px(18)), Theme.SubText);
            Theme.DrawSwitch(g, new Rectangle(Width - Theme.Px(58), (Height - Theme.Px(22)) / 2, Theme.Px(44), Theme.Px(22)), On, Theme.Accent, hover);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); }

        protected override void OnClick(EventArgs e)
        {
            On = !On;
            if (On) Sounds.ToggleOn(); else Sounds.ToggleOff();
            Invalidate();
            Toggled?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) OnClick(e); }
    }

    // A clickable text link with an optional glyph.
    public class LinkText : FlatControl
    {
        public string Glyph;
        public float FontSize = 9f;
        public bool Bold;
        public Color? Color;
        bool hover;

        public LinkText() { Cursor = Cursors.Hand; Height = Theme.Px(22); }

        public void FitWidth()
        {
            Width = Theme.Measure(Text, Theme.Font(FontSize, Bold ? FontStyle.Bold : FontStyle.Regular)) +
                    (Glyph != null ? Theme.Px(20) : 0) + 4;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var c = Color ?? Theme.AccentText;
            if (hover) c = Theme.Accent;
            int x = 0;
            if (Glyph != null)
            {
                Theme.Glyph(g, Glyph, FontSize, new Rectangle(0, 0, Theme.Px(16), Height), c);
                x = Theme.Px(20);
            }
            var f = Theme.Font(FontSize, Bold ? FontStyle.Bold : FontStyle.Regular);
            if (hover) f = new Font(f, f.Style | FontStyle.Underline);
            Theme.Draw(g, Text, f, new Rectangle(x, 0, Width - x, Height), c);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); }
    }

    // Toast message that fades away.
    public class Toast : FlatControl
    {
        string message;
        bool ok = true;
        readonly Timer timer = new Timer { Interval = 3200 };

        public Toast()
        {
            Visible = false;
            timer.Tick += (s, e) => { timer.Stop(); Visible = false; };
        }

        public void Show(string msg, bool success = true)
        {
            message = msg;
            ok = success;
            var f = Theme.Font(10f, FontStyle.Bold);
            Size = new Size(Theme.Measure(msg, f) + Theme.Px(76), Theme.Px(50));
            Reposition();
            using (var p = Theme.Round(new RectangleF(0, 0, Width, Height), Theme.Px(12)))
                Region = new Region(p);
            Visible = true;
            BringToFront();
            Invalidate();
            timer.Stop();
            timer.Start();
        }

        public void Reposition()
        {
            if (Parent == null) return;
            Location = new Point((Parent.ClientSize.Width - Width) / 2, Parent.ClientSize.Height - Height - Theme.Px(96));
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hq(g);
            var bg = Theme.Dark ? System.Drawing.Color.FromArgb(0xF3, 0xF4, 0xF6) : System.Drawing.Color.FromArgb(0x1F, 0x29, 0x37);
            var fg = Theme.Dark ? System.Drawing.Color.FromArgb(0x1F, 0x29, 0x37) : System.Drawing.Color.White;
            using (var b = new SolidBrush(bg)) g.FillRectangle(b, ClientRectangle);
            var circle = new Rectangle(Theme.Px(14), (Height - Theme.Px(26)) / 2, Theme.Px(26), Theme.Px(26));
            using (var b = new SolidBrush(ok ? Theme.Green : Theme.Red)) g.FillEllipse(b, circle);
            Theme.Glyph(g, ok ? Glyphs.Check : Glyphs.Close, 9f, circle, System.Drawing.Color.White);
            Theme.Draw(g, message, Theme.Font(10f, FontStyle.Bold),
                new Rectangle(Theme.Px(52), 0, Width - Theme.Px(60), Height), fg);
        }

        protected override void OnClick(EventArgs e) { timer.Stop(); Visible = false; }
    }

    // A panel that paints with a theme colour and follows theme changes.
    public class ThemedPanel : Panel
    {
        public Func<Color> Back = () => Theme.Window;
        public bool TopBorder, BottomBorder, RightBorder, LeftBorder;

        public ThemedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Back();
            Theme.Changed += Restyle;
        }

        void Restyle() { BackColor = Back(); Invalidate(true); }

        public void Refresh2() => Restyle();

        protected override void Dispose(bool disposing)
        {
            if (disposing) Theme.Changed -= Restyle;
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(Theme.Border);
            if (TopBorder) e.Graphics.DrawLine(pen, 0, 0, Width, 0);
            if (BottomBorder) e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            if (RightBorder) e.Graphics.DrawLine(pen, Width - 1, 0, Width - 1, Height);
            if (LeftBorder) e.Graphics.DrawLine(pen, 0, 0, 0, Height);
        }
    }

    // Small text label that follows the theme.
    public class ThemedLabel : FlatControl
    {
        public Func<Color> Fore = () => Theme.SubText;
        public float FontSize = 8.5f;
        public bool Bold;
        public bool Wrap;
        public ContentAlignment Align = ContentAlignment.MiddleLeft;

        public ThemedLabel() { Height = Theme.Px(20); }

        public int PreferredHeight(int width) =>
            Wrap ? Theme.MeasureHeight(Text, Theme.Font(FontSize, Bold ? FontStyle.Bold : FontStyle.Regular), width) + 2 : Height;

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var f = Theme.Font(FontSize, Bold ? FontStyle.Bold : FontStyle.Regular);
            if (Wrap) { Theme.DrawWrap(e.Graphics, Text, f, ClientRectangle, Fore()); return; }
            var flags = Align == ContentAlignment.MiddleCenter ? TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                      : Align == ContentAlignment.MiddleRight ? TextFormatFlags.Right | TextFormatFlags.VerticalCenter
                      : TextFormatFlags.Left | TextFormatFlags.VerticalCenter;
            Theme.Draw(e.Graphics, Text, f, ClientRectangle, Fore(), flags);
        }
    }
}
