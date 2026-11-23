using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace RightClickMenuManager
{
    // Rounded, borderless dialog matching the app style. Returns the index of the button clicked (-1 = cancelled).
    public class ModernDialog : Form
    {
        public enum Mood { Info, Question, Success, Warning, Error }

        readonly string title, message, glyph;
        readonly Color tint;
        readonly List<FlatButton> buttons = new List<FlatButton>();
        public int Result = -1;
        int msgH;

        const int CS_DROPSHADOW = 0x20000;
        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ClassStyle |= CS_DROPSHADOW; return cp; }
        }

        ModernDialog(string title, string message, Mood mood, string[] buttonTexts, int primary)
        {
            this.title = title;
            this.message = message;
            (glyph, tint) = mood switch
            {
                Mood.Success => (Glyphs.Check, Theme.Green),
                Mood.Warning => (Glyphs.Warning, Theme.Amber),
                Mood.Error => (Glyphs.Error, Theme.Red),
                Mood.Question => (Glyphs.Explorer, Theme.Accent),
                _ => (Glyphs.Info, Theme.Accent),
            };

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            KeyPreview = true;
            BackColor = Theme.Card;
            DoubleBuffered = true;
            Font = Theme.Font(9.5f);

            int w = Theme.Px(460);
            msgH = Theme.MeasureHeight(message, Theme.Font(9.5f), w - Theme.Px(96));
            int h = Theme.Px(84) + msgH + Theme.Px(84);
            ClientSize = new Size(w, h);

            int x = w - Theme.Px(24);
            for (int i = buttonTexts.Length - 1; i >= 0; i--)
            {
                int idx = i;
                var b = new FlatButton
                {
                    Text = buttonTexts[i],
                    ButtonKind = i == primary ? FlatButton.Kind.Primary : FlatButton.Kind.Secondary,
                    Height = Theme.Px(36),
                };
                b.FitWidth();
                b.Width = Math.Max(b.Width, Theme.Px(96));
                x -= b.Width;
                b.Location = new Point(x, h - Theme.Px(24) - b.Height);
                x -= Theme.Px(10);
                b.Click += (s, e) => { Result = idx; Close(); };
                buttons.Insert(0, b);
                Controls.Add(b);
            }
            AcceptIndex = primary;
            Shown += (s, e) => { if (primary >= 0 && primary < buttons.Count) buttons[primary].Focus(); };
        }

        int AcceptIndex;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(this);
            using var p = Theme.Round(new RectangleF(0, 0, Width, Height), Theme.Px(14));
            if (Environment.OSVersion.Version.Build < 22000) Region = new Region(p);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Result = -1; Close(); }
            if (e.KeyCode == Keys.Enter && AcceptIndex >= 0) { Result = AcceptIndex; Close(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hq(g);
            using (var pen = new Pen(Theme.Border)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            var circle = new Rectangle(Theme.Px(24), Theme.Px(26), Theme.Px(40), Theme.Px(40));
            using (var b = new SolidBrush(Color.FromArgb(Theme.Dark ? 50 : 30, tint))) g.FillEllipse(b, circle);
            Theme.Glyph(g, glyph, 14f, circle, tint);
            int tx = Theme.Px(80);
            Theme.Draw(g, title, Theme.Font(12.5f, FontStyle.Bold), new Rectangle(tx, Theme.Px(26), Width - tx - Theme.Px(24), Theme.Px(26)), Theme.Text);
            Theme.DrawWrap(g, message, Theme.Font(9.5f), new Rectangle(tx, Theme.Px(60), Width - tx - Theme.Px(24), msgH + 4), Theme.SubText);
        }

        // Dragging the dialog by its body
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            try { ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); } catch { }
        }

        public static int Show(IWin32Window owner, string title, string message, Mood mood, string[] buttons, int primary)
        {
            using var d = new ModernDialog(title, message, mood, buttons, primary);
            d.ShowDialog(owner);
            return d.Result;
        }

        public static void Info(IWin32Window owner, string title, string message, Mood mood = Mood.Info) =>
            Show(owner, title, message, mood, new[] { "OK" }, 0);
    }
}
