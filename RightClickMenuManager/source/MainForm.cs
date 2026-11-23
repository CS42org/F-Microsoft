using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RightClickMenuManager
{
    public class MainForm : Form
    {
        enum Page { Menu, Settings, About }

        // Layout parts
        readonly ThemedPanel header = new ThemedPanel { BottomBorder = true };
        readonly ThemedPanel sidebar = new ThemedPanel { RightBorder = true };
        readonly ThemedPanel content = new ThemedPanel();
        readonly ThemedPanel toolbar = new ThemedPanel();
        readonly ThemedPanel pendingBar = new ThemedPanel { TopBorder = true };
        readonly CardList cards = new CardList();
        readonly DetailsPanel details = new DetailsPanel();
        readonly SettingsPage settingsPage = new SettingsPage();
        readonly AboutPage aboutPage = new AboutPage();
        readonly WelcomeOverlay welcome = new WelcomeOverlay();
        readonly Toast toast = new Toast();

        // Header
        readonly HeaderTitle title = new HeaderTitle();
        readonly ChipGroup menuStyle = new ChipGroup { Style = ChipGroup.Look.Segmented, Labels = new[] { "Windows 11 menu", "Classic menu" } };
        readonly FlatButton btnBack = new FlatButton { Text = "Back to menu items", Glyph = Glyphs.Back, ButtonKind = FlatButton.Kind.Ghost };
        readonly FlatButton btnRefresh = new FlatButton { Glyph = Glyphs.Refresh, ButtonKind = FlatButton.Kind.Ghost };
        readonly FlatButton btnSettings = new FlatButton { Glyph = Glyphs.Settings, ButtonKind = FlatButton.Kind.Ghost };
        readonly FlatButton btnAbout = new FlatButton { Glyph = Glyphs.Info, ButtonKind = FlatButton.Kind.Ghost };
        readonly ToolTip tips = new ToolTip();

        // Sidebar filters
        readonly ThemedLabel lblWhere = Header("WHERE YOU RIGHT CLICK");
        readonly ChipGroup fWhere = new ChipGroup
        {
            Labels = new[] { "Everywhere", "Files", "Folders", "Folder background", "Desktop", "Drives" },
            GlyphList = new[] { Glyphs.All, Glyphs.File, Glyphs.Folder, Glyphs.FolderOpen, Glyphs.Desktop, Glyphs.Drive },
        };
        readonly ThemedLabel lblKind = Header("KIND");
        readonly ChipGroup fKind = new ChipGroup { Style = ChipGroup.Look.Segmented, Labels = new[] { "All", "Commands", "App ext." } };
        readonly ThemedLabel lblMenu = Header("WHICH MENU");
        readonly ChipGroup fMenu = new ChipGroup { Style = ChipGroup.Look.Segmented, Labels = new[] { "All", "Classic", "Windows 11" } };
        readonly ThemedLabel lblType = Header("FILE TYPE");
        readonly ChipGroup fType = new ChipGroup
        {
            Labels = new[] { "Any type", "Images", "Videos", "Audio", "Documents", "Archives", "Other" },
            GlyphList = FileGroups.All.Select(FileGroups.Glyph).ToArray(),
        };
        readonly LinkText resetFilters = new LinkText { Text = "Reset filters", Glyph = Glyphs.Clear, FontSize = 8.5f };
        readonly ThemedLabel credit = new ThemedLabel { Text = "Developed by CS42.org", FontSize = 8.5f, Bold = true, Fore = () => Theme.SubText };
        readonly LinkText creditSite = new LinkText { Text = "cs42.org", Glyph = Glyphs.Globe, FontSize = 8.5f };
        readonly LinkText creditGit = new LinkText { Text = "GitHub", Glyph = Glyphs.Code, FontSize = 8.5f };

        // Toolbar
        readonly SearchBox search = new SearchBox();
        readonly ChipGroup fStatus = new ChipGroup { Labels = new[] { "All", "Shown", "Hidden", "Shift only" } };
        readonly ThemedLabel countLabel = new ThemedLabel { Align = ContentAlignment.MiddleRight, FontSize = 9f };

        // Pending bar
        readonly ThemedLabel pendingLabel = new ThemedLabel { FontSize = 10f, Bold = true, Fore = () => Theme.Text };
        readonly ThemedLabel pendingHint = new ThemedLabel { FontSize = 8.5f, Text = "Changes take effect after Explorer restarts" };
        readonly FlatButton btnUndo = new FlatButton { Text = "Undo", Glyph = Glyphs.Undo };
        readonly FlatButton btnDiscard = new FlatButton { Text = "Discard all", ButtonKind = FlatButton.Kind.Ghost };
        readonly FlatButton btnApply = new FlatButton { Text = "Apply and restart Explorer", Glyph = Glyphs.Check, ButtonKind = FlatButton.Kind.Primary, Height = Theme.Px(38) };

        // State
        List<MenuEntry> entries = new List<MenuEntry>();
        readonly Dictionary<string, ItemState> pending = new Dictionary<string, ItemState>();
        readonly Stack<List<(string Id, ItemState? Prev)>> undo = new Stack<List<(string, ItemState?)>>();
        Page page = Page.Menu;
        bool loading;

        public MainForm()
        {
            Text = "Right Click Menu Manager";
            Font = Theme.Font(9.5f);
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(Theme.Px(1240), Theme.Px(800));
            MinimumSize = new Size(Theme.Px(980), Theme.Px(620));
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = Theme.Window;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            header.Back = () => Theme.Window;
            sidebar.Back = () => Theme.Sidebar;
            content.Back = () => Theme.Window;
            toolbar.Back = () => Theme.Window;
            pendingBar.Back = () => Theme.Card;
            foreach (var p in new[] { header, sidebar, content, toolbar, pendingBar }) p.Refresh2();

            BuildHeader();
            BuildSidebar();
            BuildContent();

            details.Visible = false;
            details.EffectiveState = Effective;
            details.PreviewItems = Preview;
            details.CloseClicked += (s, e) => CloseDetails();
            details.StateChosen += (e, st) => { SetPending(e, st); Sounds.Tap(); };

            settingsPage.Visible = false;
            aboutPage.Visible = false;
            settingsPage.MenuStyleRequested += classic => ChangeMenuStyle(classic);
            settingsPage.RestoreRequested += dir => RestoreBackup(dir, false);
            settingsPage.ResetRequested += () =>
            {
                var all = MenuRegistry.ListBackups();
                if (all.Count > 0) RestoreBackup(all[all.Count - 1].Dir, true);
            };
            settingsPage.SoundsRow.Toggled += (s, e) => { AppSettings.Sounds = Sounds.Enabled = settingsPage.SoundsRow.On; };
            settingsPage.DarkRow.Toggled += (s, e) => { AppSettings.DarkMode = settingsPage.DarkRow.On; Theme.SetDark(settingsPage.DarkRow.On); };
            settingsPage.ConfirmRow.Toggled += (s, e) => { AppSettings.ConfirmRestart = settingsPage.ConfirmRow.On; };

            welcome.Visible = !AppSettings.Welcomed;
            welcome.Done += (s, e) => { AppSettings.Welcomed = true; welcome.Visible = false; search.Box.Focus(); };

            Controls.AddRange(new Control[] { header, sidebar, content, details, settingsPage, aboutPage });
            Controls.Add(toast);
            Controls.Add(welcome);
            welcome.BringToFront();

            Theme.Changed += OnThemeChanged;
            Resize += (s, e) => Relayout();
            Shown += (s, e) => { Relayout(); Reload(false); };
        }

        static ThemedLabel Header(string text) =>
            new ThemedLabel { Text = text, FontSize = 7.75f, Bold = true, Fore = () => Theme.SubText };

        // ---------- building ----------

        void BuildHeader()
        {
            menuStyle.SelectedIndex = MenuRegistry.ClassicMenuEnabled ? 1 : 0;
            menuStyle.SelectionChanged += (s, e) =>
            {
                if (syncingStyle) return;
                ChangeMenuStyle(menuStyle.SelectedIndex == 1);
            };
            foreach (var b in new[] { btnRefresh, btnSettings, btnAbout }) b.Size = new Size(Theme.Px(38), Theme.Px(38));
            tips.SetToolTip(btnRefresh, "Refresh (F5)");
            tips.SetToolTip(btnSettings, "Settings and backups");
            tips.SetToolTip(btnAbout, "About");
            tips.SetToolTip(menuStyle, "Which menu opens when you right click");
            btnRefresh.Click += (s, e) => Reload(true);
            btnSettings.Click += (s, e) => ShowPage(page == Page.Settings ? Page.Menu : Page.Settings);
            btnAbout.Click += (s, e) => ShowPage(page == Page.About ? Page.Menu : Page.About);
            btnBack.FitWidth();
            btnBack.Visible = false;
            btnBack.Click += (s, e) => ShowPage(Page.Menu);
            title.Click += (s, e) => ShowPage(Page.Menu);
            header.Controls.AddRange(new Control[] { title, btnBack, menuStyle, btnRefresh, btnSettings, btnAbout });
        }

        void BuildSidebar()
        {
            foreach (var g in new[] { fWhere, fKind, fMenu, fType })
                g.SelectionChanged += (s, e) => ApplyFilters(false);
            resetFilters.FitWidth();
            resetFilters.Click += (s, e) => ResetFilters();
            creditSite.FitWidth();
            creditGit.FitWidth();
            creditSite.Click += (s, e) => Links.Open(Links.Website);
            creditGit.Click += (s, e) => Links.Open(Links.GitHub);
            sidebar.Controls.AddRange(new Control[] { lblWhere, fWhere, lblKind, fKind, lblMenu, fMenu, lblType, fType, resetFilters, credit, creditSite, creditGit });
            sidebar.Resize += (s, e) => LayoutSidebar();
        }

        void BuildContent()
        {
            search.SearchChanged += (s, e) => ApplyFilters(false);
            fStatus.SelectionChanged += (s, e) => ApplyFilters(false);
            toolbar.Controls.AddRange(new Control[] { search, fStatus, countLabel });

            cards.EffectiveState = Effective;
            cards.IsPending = e => pending.ContainsKey(e.Id);
            cards.ToggleClicked += Toggle;
            cards.CardClicked += OpenDetails;
            cards.ClearFiltersClicked += ResetFilters;

            btnUndo.FitWidth();
            btnDiscard.FitWidth();
            btnApply.FitWidth();
            btnUndo.Click += (s, e) => Undo();
            btnDiscard.Click += (s, e) => Discard();
            btnApply.Click += (s, e) => Apply();
            pendingBar.Controls.AddRange(new Control[] { btnUndo, btnDiscard, btnApply, pendingLabel, pendingHint });
            pendingBar.Visible = false;
            pendingBar.Paint += (s, e) =>
            {
                Theme.Hq(e.Graphics);
                using var b = new SolidBrush(Theme.Accent);
                e.Graphics.FillEllipse(b, Theme.Px(24), pendingBar.Height / 2 - Theme.Px(14), Theme.Px(10), Theme.Px(10));
            };

            content.Controls.AddRange(new Control[] { toolbar, cards, pendingBar });
            content.Resize += (s, e) => LayoutContent();
        }

        // ---------- layout ----------

        void Relayout()
        {
            if (ClientSize.Width == 0) return;
            int hh = Theme.Px(64);
            header.SetBounds(0, 0, ClientSize.Width, hh);
            LayoutHeader();
            int bodyH = ClientSize.Height - hh;
            int sw = Theme.Px(286);
            int dw = details.Visible && page == Page.Menu ? Theme.Px(400) : 0;

            sidebar.Visible = page == Page.Menu;
            content.Visible = page == Page.Menu;
            settingsPage.Visible = page == Page.Settings;
            aboutPage.Visible = page == Page.About;

            if (page == Page.Menu)
            {
                sidebar.SetBounds(0, hh, sw, bodyH);
                content.SetBounds(sw, hh, ClientSize.Width - sw - dw, bodyH);
                if (dw > 0) details.SetBounds(ClientSize.Width - dw, hh, dw, bodyH);
            }
            settingsPage.SetBounds(0, hh, ClientSize.Width, bodyH);
            aboutPage.SetBounds(0, hh, ClientSize.Width, bodyH);
            welcome.SetBounds(0, 0, ClientSize.Width, ClientSize.Height);
            toast.Reposition();
        }

        void LayoutHeader()
        {
            int h = header.Height;
            title.SetBounds(Theme.Px(18), 0, Theme.Px(330), h - 1);
            int x = header.Width - Theme.Px(14);
            foreach (var b in new[] { btnAbout, btnSettings, btnRefresh })
            {
                x -= b.Width;
                b.Location = new Point(x, (h - b.Height) / 2);
                x -= Theme.Px(4);
            }
            int msw = Theme.Px(270);
            x -= msw + Theme.Px(14);
            menuStyle.SetBounds(x, (h - Theme.Px(32)) / 2, msw, Theme.Px(32));
            btnBack.Location = new Point(Theme.Px(360), (h - btnBack.Height) / 2);
            btnBack.Visible = page != Page.Menu;
            btnRefresh.Visible = page == Page.Menu;
        }

        void LayoutSidebar()
        {
            int pad = Theme.Px(20), w = sidebar.Width - pad * 2;
            int y = Theme.Px(22);
            void Group(ThemedLabel l, ChipGroup g)
            {
                l.SetBounds(pad, y, w, Theme.Px(18));
                y += Theme.Px(26);
                int gh = g.PreferredHeight(w);
                g.SetBounds(pad, y, w, gh);
                y += gh + Theme.Px(22);
            }
            Group(lblWhere, fWhere);
            Group(lblKind, fKind);
            Group(lblMenu, fMenu);
            Group(lblType, fType);
            resetFilters.Location = new Point(pad, y - Theme.Px(6));

            int by = sidebar.Height - Theme.Px(70);
            credit.SetBounds(pad, by, w, Theme.Px(20));
            creditSite.Location = new Point(pad, by + Theme.Px(24));
            creditGit.Location = new Point(creditSite.Right + Theme.Px(16), by + Theme.Px(24));
        }

        void LayoutContent()
        {
            int pad = Theme.Px(16);
            int w = content.Width;
            int th = Theme.Px(104);
            toolbar.SetBounds(0, 0, w, th);
            search.SetBounds(pad, Theme.Px(16), w - pad * 2, Theme.Px(40));
            int sy = Theme.Px(66);
            int fw = fStatus.PreferredHeight(w) > 0 ? w - pad * 2 - Theme.Px(160) : 0;
            fStatus.SetBounds(pad, sy, fw, fStatus.PreferredHeight(fw));
            countLabel.SetBounds(w - pad - Theme.Px(160), sy, Theme.Px(160), Theme.Px(30));

            int pb = pendingBar.Visible ? Theme.Px(68) : 0;
            cards.SetBounds(0, th, w, content.Height - th - pb);
            if (pendingBar.Visible)
            {
                pendingBar.SetBounds(0, content.Height - pb, w, pb);
                int x = w - pad - btnApply.Width;
                btnApply.Location = new Point(x, (pb - btnApply.Height) / 2);
                x -= Theme.Px(10) + btnUndo.Width;
                btnUndo.Location = new Point(x, (pb - btnUndo.Height) / 2);
                x -= Theme.Px(6) + btnDiscard.Width;
                btnDiscard.Location = new Point(x, (pb - btnDiscard.Height) / 2);
                int lw = x - Theme.Px(44) - Theme.Px(10);
                btnDiscard.Visible = lw > Theme.Px(150);
                if (!btnDiscard.Visible) lw = btnUndo.Left - Theme.Px(54);
                pendingLabel.SetBounds(Theme.Px(44), Theme.Px(12), Math.Max(0, lw), Theme.Px(22));
                pendingHint.SetBounds(Theme.Px(44), Theme.Px(34), Math.Max(0, lw), Theme.Px(20));
            }
        }

        void ShowPage(Page p)
        {
            page = p;
            if (p == Page.Settings) settingsPage.Sync();
            Sounds.Tap();
            Relayout();
            LayoutHeader();
            header.Invalidate(true);
        }

        void OnThemeChanged()
        {
            BackColor = Theme.Window;
            Theme.ApplyTitleBar(this);
            // nudge title bar repaint on Windows 10/11
            var s = Size; Size = new Size(s.Width + 1, s.Height); Size = s;
            Invalidate(true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(this);
        }

        // ---------- data ----------

        void Reload(bool userRequested)
        {
            if (loading) return;
            loading = true;
            cards.EmptyTitle = "Reading your right click menu…";
            cards.SetItems(new List<MenuEntry>(), false);
            Cursor = Cursors.AppStarting;
            Task.Run(() => MenuRegistry.Scan()).ContinueWith(t =>
            {
                BeginInvoke((Action)(() =>
                {
                    loading = false;
                    Cursor = Cursors.Default;
                    cards.EmptyTitle = "No items match your filters";
                    if (t.IsFaulted)
                    {
                        Sounds.Error();
                        ModernDialog.Info(this, "Couldn't read the menu", t.Exception?.GetBaseException().Message ?? "Unknown error", ModernDialog.Mood.Error);
                        return;
                    }
                    entries = t.Result;
                    // drop pending changes that no longer apply
                    foreach (var id in pending.Keys.ToList())
                        if (!entries.Any(x => x.Id == id)) pending.Remove(id);
                    SyncMenuStyle();
                    ApplyFilters(false);
                    UpdatePending();
                    if (details.Visible && details.Entry != null)
                    {
                        var again = entries.FirstOrDefault(x => x.Id == details.Entry.Id);
                        if (again != null) OpenDetails(again); else CloseDetails();
                    }
                    if (userRequested) toast.Show("Menu refreshed");
                }));
            });
        }

        ItemState Effective(MenuEntry e) => pending.TryGetValue(e.Id, out var s) ? s : e.State;

        IEnumerable<MenuEntry> BaseFiltered()
        {
            IEnumerable<MenuEntry> v = entries;
            Where[] whereMap = { Where.None, Where.Files, Where.Folders, Where.Background, Where.Desktop, Where.Drives };
            if (fWhere.SelectedIndex > 0) { var w = whereMap[fWhere.SelectedIndex]; v = v.Where(e => (e.Where & w) != 0); }
            if (fKind.SelectedIndex == 1) v = v.Where(e => e.Kind == ItemKind.Command);
            if (fKind.SelectedIndex == 2) v = v.Where(e => e.Kind == ItemKind.Extension);
            if (fMenu.SelectedIndex == 1) v = v.Where(e => e.Menus.HasFlag(Menus.Classic));
            if (fMenu.SelectedIndex == 2) v = v.Where(e => e.Menus.HasFlag(Menus.Modern));
            if (fType.SelectedIndex > 0) { var g = FileGroups.All[fType.SelectedIndex]; v = v.Where(e => e.FileGroup == g); }
            string q = search.Box.Text.Trim();
            if (q.Length > 0)
                v = v.Where(e => Has(e.Name, q) || Has(e.AppliesTo, q) || Has(e.Source, q) || Has(e.KindText, q));
            return v;
        }

        static bool Has(string s, string q) => s != null && s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

        void ApplyFilters(bool keepScroll)
        {
            var baseList = BaseFiltered().ToList();
            fStatus.Counts = new[]
            {
                baseList.Count,
                baseList.Count(e => Effective(e) == ItemState.Shown),
                baseList.Count(e => Effective(e) == ItemState.Hidden),
                baseList.Count(e => Effective(e) == ItemState.ShiftOnly),
            };
            IEnumerable<MenuEntry> v = baseList;
            if (fStatus.SelectedIndex == 1) v = v.Where(e => Effective(e) == ItemState.Shown);
            if (fStatus.SelectedIndex == 2) v = v.Where(e => Effective(e) == ItemState.Hidden);
            if (fStatus.SelectedIndex == 3) v = v.Where(e => Effective(e) == ItemState.ShiftOnly);
            var list = v.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(e => e.AppliesTo, StringComparer.CurrentCultureIgnoreCase).ToList();
            cards.SetItems(list, keepScroll);
            countLabel.Text = loading ? "" : list.Count == 1 ? "1 item" : list.Count + " items";
            fStatus.Invalidate();
            LayoutContent();
        }

        void ResetFilters()
        {
            foreach (var g in new[] { fWhere, fKind, fMenu, fType, fStatus }) g.SelectedIndex = 0;
            search.Box.Clear();
            ApplyFilters(false);
        }

        // ---------- pending changes ----------

        IEnumerable<MenuEntry> Siblings(MenuEntry e) =>
            e.Kind == ItemKind.Extension
                ? entries.Where(o => o.Kind == ItemKind.Extension && string.Equals(o.Clsid, e.Clsid, StringComparison.OrdinalIgnoreCase))
                : new[] { e };

        void SetPending(MenuEntry e, ItemState st, bool recordUndo = true)
        {
            var step = new List<(string, ItemState?)>();
            foreach (var x in Siblings(e))
            {
                ItemState? prev = pending.TryGetValue(x.Id, out var p) ? p : (ItemState?)null;
                if (Effective(x) == st) continue;
                step.Add((x.Id, prev));
                if (x.State == st) pending.Remove(x.Id); else pending[x.Id] = st;
            }
            if (step.Count == 0) return;
            if (recordUndo) undo.Push(step);
            AfterPendingChange();
        }

        void Toggle(MenuEntry e)
        {
            var cur = Effective(e);
            ItemState next = cur == ItemState.Hidden
                ? (e.State != ItemState.Hidden ? e.State : ItemState.Shown)
                : ItemState.Hidden;
            SetPending(e, next);
            if (next == ItemState.Hidden) Sounds.ToggleOff(); else Sounds.ToggleOn();
            cards.Current = e;
            if (details.Visible) OpenDetails(e);
        }

        void Undo()
        {
            if (undo.Count == 0) return;
            foreach (var (id, prev) in undo.Pop())
            {
                if (prev.HasValue) pending[id] = prev.Value; else pending.Remove(id);
            }
            Sounds.Tap();
            AfterPendingChange();
        }

        void Discard()
        {
            pending.Clear();
            undo.Clear();
            Sounds.ToggleOff();
            AfterPendingChange();
        }

        void AfterPendingChange()
        {
            ApplyFilters(true);
            UpdatePending();
            details.RefreshState();
        }

        void UpdatePending()
        {
            int n = pending.Count;
            bool show = n > 0;
            pendingLabel.Text = n == 1 ? "1 change not applied yet" : n + " changes not applied yet";
            btnUndo.Enabled = undo.Count > 0;
            if (pendingBar.Visible != show)
            {
                pendingBar.Visible = show;
                LayoutContent();
            }
            pendingBar.Invalidate(true);
            Text = show ? "Right Click Menu Manager  •  " + n + " pending" : "Right Click Menu Manager";
        }

        // ---------- details ----------

        void OpenDetails(MenuEntry e)
        {
            cards.Current = e;
            bool wasVisible = details.Visible;
            details.Visible = true;
            if (!wasVisible) Relayout();
            details.ShowEntry(e);
            cards.Invalidate();
        }

        void CloseDetails()
        {
            details.Visible = false;
            cards.Current = null;
            Relayout();
            cards.Invalidate();
        }

        List<(string Name, ItemState State, bool Current)> Preview(MenuEntry cur)
        {
            bool modernOnly = cur.Menus == Menus.Modern;
            var pool = entries.Where(e =>
                    (modernOnly ? e.Menus.HasFlag(Menus.Modern) : e.Menus.HasFlag(Menus.Classic)) &&
                    (e.Where & cur.Where) != 0 &&
                    (e.FileGroup == cur.FileGroup || e.FileGroup == FileGroups.Any))
                .Where(e => e == cur || Effective(e) != ItemState.Hidden)
                .GroupBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(g => g.Contains(cur) ? cur : g.First())
                .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(e => (e.Name, Effective(e), e == cur))
                .ToList();
            return pool;
        }

        // ---------- apply ----------

        void Apply()
        {
            if (pending.Count == 0) return;
            int n = pending.Count;
            if (AppSettings.ConfirmRestart)
            {
                int r = ModernDialog.Show(this, n == 1 ? "Apply 1 change?" : "Apply " + n + " changes?",
                    "Explorer will restart for a moment to update the menu. Your taskbar will blink and open folder windows will close.\n\n" +
                    "A backup is saved first, so you can restore it anytime. You can turn off this question in Settings.",
                    ModernDialog.Mood.Question, new[] { "Cancel", "Apply" }, 1);
                if (r != 1) return;
            }

            Cursor = Cursors.WaitCursor;
            var byId = entries.ToDictionary(x => x.Id);
            var changeLines = pending.Where(p => byId.ContainsKey(p.Key))
                .Select(p => MenuEntry.StateText(p.Value) + ": " + byId[p.Key].Name + " (" + byId[p.Key].AppliesTo + ")")
                .ToList();
            try { MenuRegistry.SaveBackup(entries, changeLines); }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                Sounds.Error();
                if (ModernDialog.Show(this, "Couldn't save a backup", ex.Message + "\n\nApply the changes anyway?",
                        ModernDialog.Mood.Warning, new[] { "Cancel", "Apply anyway" }, 0) != 1) return;
                Cursor = Cursors.WaitCursor;
            }

            var protectedItems = new List<string>();
            var failed = new List<string>();
            var doneClsids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in pending.ToList())
            {
                if (!byId.TryGetValue(p.Key, out var e)) continue;
                if (e.Kind == ItemKind.Extension && !doneClsids.Add(e.Clsid)) continue;
                try { MenuRegistry.SetState(e, p.Value); }
                catch (Exception ex) when (MenuRegistry.IsProtectedError(ex)) { protectedItems.Add(e.Name + " (" + e.AppliesTo + ")"); }
                catch (Exception ex) { failed.Add(e.Name + ": " + ex.Message); }
            }
            pending.Clear();
            undo.Clear();

            try { MenuRegistry.RestartExplorer(); }
            catch (Exception ex) { failed.Add("Restarting Explorer: " + ex.Message + ". Restart your PC instead."); }
            Cursor = Cursors.Default;
            Activate();

            Reload(false);
            if (protectedItems.Count == 0 && failed.Count == 0)
            {
                Sounds.Success();
                toast.Show("Menu updated. Backup saved.");
            }
            else
            {
                Sounds.Error();
                var msg = "";
                if (protectedItems.Count > 0)
                    msg += "Windows protects these items, so they can't be changed:\n• " + string.Join("\n• ", protectedItems.Take(10)) + "\n\n";
                if (failed.Count > 0)
                    msg += "These didn't work:\n• " + string.Join("\n• ", failed.Take(10));
                ModernDialog.Info(this, "Some changes weren't applied", msg.Trim(), ModernDialog.Mood.Warning);
            }
        }

        // ---------- menu style ----------

        bool syncingStyle;

        void SyncMenuStyle()
        {
            syncingStyle = true;
            menuStyle.SelectedIndex = MenuRegistry.ClassicMenuEnabled ? 1 : 0;
            syncingStyle = false;
            if (settingsPage.Visible) settingsPage.Sync();
        }

        void ChangeMenuStyle(bool classic)
        {
            if (classic == MenuRegistry.ClassicMenuEnabled) { SyncMenuStyle(); return; }
            bool newEngine = MenuRegistry.HasNewMenuEngine(out string build);
            string vive = AppSettings.ViVeToolPath;
            bool useVive = settingsPage.ViveUse.On && !string.IsNullOrEmpty(vive) && System.IO.File.Exists(vive);

            string msg = classic
                ? "The full classic menu will open every time you right click, without Show more options. Explorer will restart for a moment."
                : "The Windows 11 menu will come back. The classic menu stays available with Shift + right click. Explorer will restart for a moment.";
            if (classic && newEngine && !useVive)
                msg += "\n\nYour Windows build (" + build + ") includes the November 2026 update, which can ignore this setting. " +
                       "If the Windows 11 menu still appears, open Settings and use the ViVeTool option.";

            int r = ModernDialog.Show(this, classic ? "Switch to the classic menu?" : "Switch back to the Windows 11 menu?", msg,
                ModernDialog.Mood.Question, new[] { "Cancel", "Switch" }, 1);
            if (r != 1) { SyncMenuStyle(); return; }

            Cursor = Cursors.WaitCursor;
            string viveError = null;
            try
            {
                MenuRegistry.SetClassicMenu(classic);
                if (useVive)
                {
                    int code = MenuRegistry.RunViVeTool(vive, classic, out string output);
                    if (code != 0) viveError = output.Trim();
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                Sounds.Error();
                ModernDialog.Info(this, "Couldn't change the menu style", ex.Message, ModernDialog.Mood.Error);
                SyncMenuStyle();
                return;
            }
            string restartError = null;
            try { MenuRegistry.RestartExplorer(); } catch (Exception ex) { restartError = ex.Message; }
            Cursor = Cursors.Default;
            Activate();
            SyncMenuStyle();
            Sounds.Success();
            if (restartError != null)
                ModernDialog.Info(this, "Restart needed", "The menu style was changed, but Explorer couldn't be restarted (" + restartError + "). Restart your PC to see the change.", ModernDialog.Mood.Warning);
            else if (viveError != null)
                ModernDialog.Info(this, "ViVeTool reported a problem", viveError + "\n\nThe registry setting was still changed. A restart of your PC may be needed.", ModernDialog.Mood.Warning);
            else if (useVive)
                toast.Show(classic ? "Classic menu on. Restart your PC to finish." : "Windows 11 menu back. Restart your PC to finish.");
            else
                toast.Show(classic ? "Classic menu turned on" : "Windows 11 menu is back");
        }

        // ---------- backups ----------

        void RestoreBackup(string dir, bool original)
        {
            Dictionary<string, ItemState> snap;
            try { snap = MenuRegistry.LoadSnapshot(dir); }
            catch (Exception ex)
            {
                Sounds.Error();
                ModernDialog.Info(this, "Couldn't read the backup", ex.Message, ModernDialog.Mood.Error);
                return;
            }
            pending.Clear();
            undo.Clear();
            int changes = 0;
            foreach (var e in entries)
            {
                if (!snap.TryGetValue(e.Id, out var st) || st == e.State) continue;
                pending[e.Id] = st;
                changes++;
            }
            ShowPage(Page.Menu);
            ResetFilters();
            UpdatePending();
            if (changes == 0) toast.Show(original ? "Your menu already matches the original" : "Your menu already matches this backup");
            else
            {
                fStatus.SelectedIndex = 0;
                toast.Show(changes + " changes loaded. Review them, then click Apply.");
            }
        }

        // ---------- keyboard and closing ----------

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.F: ShowPage(Page.Menu); search.Box.Focus(); search.Box.SelectAll(); return true;
                case Keys.Control | Keys.Z when !search.Box.Focused: Undo(); return true;
                case Keys.F5: Reload(true); return true;
                case Keys.Escape when details.Visible && !search.Box.Focused: CloseDetails(); return true;
                case Keys.Control | Keys.S when pending.Count > 0: Apply(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (pending.Count > 0 && e.CloseReason == CloseReason.UserClosing)
            {
                int r = ModernDialog.Show(this, "Apply your changes first?",
                    pending.Count + (pending.Count == 1 ? " change hasn't" : " changes haven't") + " been applied yet.",
                    ModernDialog.Mood.Warning, new[] { "Cancel", "Close without applying", "Apply" }, 2);
                if (r == 2) { Apply(); }
                else if (r != 1) { e.Cancel = true; return; }
            }
            base.OnFormClosing(e);
        }
    }

    // App icon, name and credit at the top left.
    public class HeaderTitle : FlatControl
    {
        public HeaderTitle() { Cursor = Cursors.Hand; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Hq(g);
            int s = Theme.Px(34);
            AppIcon.Draw(g, new Rectangle(0, (Height - s) / 2, s, s));
            int x = s + Theme.Px(12);
            Theme.Draw(g, "Right Click Menu Manager", Theme.Font(12.5f, FontStyle.Bold), new Rectangle(x, Height / 2 - Theme.Px(21), Width - x, Theme.Px(24)), Theme.Text);
            Theme.Draw(g, "by CS42.org", Theme.Font(8.5f), new Rectangle(x, Height / 2 + Theme.Px(3), Width - x, Theme.Px(18)), Theme.SubText);
        }
    }
}
