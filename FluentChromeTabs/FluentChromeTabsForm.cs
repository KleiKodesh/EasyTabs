using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FluentChromeTabs
{
    /// <summary>
    /// A WinForms window that hosts Fluent (Windows 11) style tabs directly in its title bar.
    /// <para>
    /// The standard window frame is kept, so DWM shadows, rounded corners, snap layouts, and
    /// minimize/maximize animations all behave natively; the caption area is reclaimed as client
    /// space and the tab strip is painted there. There is no separate overlay window.
    /// </para>
    /// <para>Minimal usage:</para>
    /// <code>
    /// var window = new FluentChromeTabsForm { Text = "My App" };
    /// window.AddTab("Home", new HomeControl());
    /// Application.Run(window);
    /// </code>
    /// </summary>
    public partial class FluentChromeTabsForm : Form
    {
        private static readonly List<FluentChromeTabsForm> OpenTabForms = new List<FluentChromeTabsForm>();

        private readonly List<FluentTab> _tabs = new List<FluentTab>();
        private ThemedToolTip _toolTip;

        private int _selectedIndex = -1;
        private FluentChromeTabsTheme _theme = FluentChromeTabsTheme.Auto;
        private Palette _palette;
        private bool _isDark;
        private bool _windowActive = true;
        private int _newTabCounter;

        public FluentChromeTabsForm()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
                true);

            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(1000, 640);
            Text = "FluentChromeTabs";

            ApplyTheme();
            UpdateMetrics();
        }

        #region Public API

        /// <summary>The tabs currently hosted by this window, in display order.</summary>
        public IReadOnlyList<FluentTab> Tabs
        {
            get { return _tabs; }
        }

        /// <summary>Index of the selected tab, or -1 when the window has no tabs.</summary>
        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (value >= 0 && value < _tabs.Count)
                {
                    SelectTabCore(value);
                }
            }
        }

        /// <summary>The selected tab, or null when the window has no tabs.</summary>
        public FluentTab SelectedTab
        {
            get { return _selectedIndex >= 0 && _selectedIndex < _tabs.Count ? _tabs[_selectedIndex] : null; }
            set { SelectedIndex = value == null ? -1 : _tabs.IndexOf(value); }
        }

        /// <summary>Whether the window closes itself when its last tab is closed. Defaults to true.</summary>
        public bool ExitOnLastTabClose { get; set; } = true;

        /// <summary>Whether the "+" new-tab button is shown. Defaults to true.</summary>
        public bool ShowNewTabButton
        {
            get { return _showNewTabButton; }
            set
            {
                _showNewTabButton = value;
                InvalidateStrip();
            }
        }

        private bool _showNewTabButton = true;

        /// <summary>Whether tabs may be torn off into their own window or dropped onto other windows. Defaults to true.</summary>
        public bool AllowTabDetach { get; set; } = true;

        /// <summary>
        /// Splits the strip down the middle into two parallel tab regions. Tabs are laid out in
        /// the region given by <see cref="FluentTab.Group" /> (0 = inline start, 1 = inline end),
        /// and each region gets its own tab-list toggle and "+" button (gestures report the group).
        /// Each region marks its own active tab via <see cref="FluentTab.Highlighted" />.
        /// Defaults to false.
        /// </summary>
        public bool SplitStrip
        {
            get { return _splitStrip; }
            set
            {
                _splitStrip = value;
                InvalidateStrip();
            }
        }

        private bool _splitStrip;

        /// <summary>
        /// Where the split divider sits, as region 0's share of the client width (0.15–0.85).
        /// Setting it programmatically does not raise <see cref="SplitRatioChanged" /> — that
        /// event reports only user drags of the divider. Defaults to 0.5.
        /// </summary>
        public double SplitRatio
        {
            get { return _splitRatio; }
            set
            {
                double clamped = Math.Max(0.15, Math.Min(0.85, value));

                if (Math.Abs(clamped - _splitRatio) > 0.0001)
                {
                    _splitRatio = clamped;
                    InvalidateStrip();
                }
            }
        }

        private double _splitRatio = 0.5;

        /// <summary>Raised while the user drags the split divider (read <see cref="SplitRatio" />).</summary>
        public event EventHandler SplitRatioChanged;

        private int _dividerOverrideLeft = -1;
        private int _dividerOverrideWidth;

        /// <summary>
        /// Pins the split divider to exact client pixels, letting a host align it perfectly
        /// with a content divider below the strip (no fraction rounding). Pass a negative
        /// left to return to <see cref="SplitRatio" />-based placement. A user drag of the
        /// divider clears the pin until it is set again.
        /// </summary>
        public void SetSplitDividerPixels(int leftPx, int widthPx)
        {
            _dividerOverrideLeft = leftPx;
            _dividerOverrideWidth = Math.Max(1, widthPx);
            InvalidateStrip();
        }

        private int GroupCount
        {
            get { return _splitStrip ? 2 : 1; }
        }

        private int GroupOf(FluentTab tab)
        {
            return _splitStrip && tab.Group == 1 ? 1 : 0;
        }

        /// <summary>
        /// Whether the tab strip is shown. Hide it for fullscreen/kiosk modes: the reserved
        /// padding collapses so content fills the whole window, and strip painting, mouse
        /// handling, and caption-button hit testing are suspended until it is shown again.
        /// Defaults to true.
        /// </summary>
        public bool StripVisible
        {
            get { return _stripVisible; }
            set
            {
                if (_stripVisible == value)
                {
                    return;
                }

                _stripVisible = value;
                ClearHoverState();
                UpdateMetrics();
                Invalidate();
            }
        }

        private bool _stripVisible = true;

        /// <summary>Height of the tab strip in logical pixels (scaled for DPI). Defaults to 40.</summary>
        public int StripHeight
        {
            get { return _stripHeight; }
            set
            {
                _stripHeight = Math.Max(28, value);
                UpdateMetrics();
                Invalidate();
            }
        }

        private int _stripHeight = 40;

        /// <summary>Height of the tab cards in logical pixels (scaled for DPI). Defaults to 32.</summary>
        public int TabHeight
        {
            get { return _tabHeight; }
            set
            {
                _tabHeight = Math.Max(20, value);
                InvalidateStrip();
            }
        }

        private int _tabHeight = 32;

        /// <summary>
        /// Whether the window's <see cref="Form.Icon" /> is drawn in the strip, in a slot just before
        /// the start of the tabs (the visual right in a mirrored/RTL window). Defaults to false.
        /// </summary>
        public bool ShowWindowIcon
        {
            get { return _showWindowIcon; }
            set
            {
                _showWindowIcon = value;
                InvalidateStrip();
            }
        }

        private bool _showWindowIcon;

        /// <summary>
        /// Whether a tab-list dropdown button is shown just before the start of the tabs (after the
        /// window icon when both are enabled). Clicking it opens a Fluent-styled dropdown listing
        /// every open tab (the selected one marked with an accent indicator); picking an entry
        /// selects that tab. Hosts can append extra sections via <see cref="TabListOpening" />.
        /// Defaults to false.
        /// </summary>
        public bool ShowTabListButton
        {
            get { return _showTabListButton; }
            set
            {
                _showTabListButton = value;
                InvalidateStrip();
            }
        }

        private bool _showTabListButton;

        /// <summary>Header text of the open-tabs section in the tab-list dropdown.</summary>
        public string TabListOpenTabsHeader { get; set; } = "Open tabs";

        /// <summary>
        /// Optional accent color used for the active-tab indicator in the tab-list dropdown
        /// and the split divider's hover highlight. Falls back to the palette text color when null.
        /// </summary>
        public Color? AccentColor { get; set; }

        /// <summary>
        /// Optional color for the split divider's resting state, letting it match a host
        /// content divider exactly. Falls back to the palette separator when null.
        /// </summary>
        public Color? SplitDividerColor { get; set; }

        /// <summary>
        /// Tooltip text for the caption buttons. The buttons are custom-drawn and hit-tested
        /// as the standard caption controls, so Windows would otherwise show its own
        /// OS-language tooltips; setting these shows localized ones instead. English by default.
        /// </summary>
        public string MinimizeToolTip { get; set; } = "Minimize";
        public string MaximizeToolTip { get; set; } = "Maximize";
        public string RestoreToolTip { get; set; } = "Restore";
        public string CloseToolTip { get; set; } = "Close";

        private string CaptionButtonToolTip(int htCode)
        {
            if (htCode == NativeMethods.HTCLOSE) return CloseToolTip;
            if (htCode == NativeMethods.HTMINBUTTON) return MinimizeToolTip;
            if (htCode == NativeMethods.HTMAXBUTTON) return NativeMethods.IsZoomed(Handle) ? RestoreToolTip : MaximizeToolTip;
            return null;
        }

        /// <summary>
        /// Raised right before the tab-list dropdown opens, with the open-tabs section already
        /// populated. Handlers may append additional sections (e.g. recently closed documents).
        /// </summary>
        public event EventHandler<TabListOpeningEventArgs> TabListOpening;

        /// <summary>The active palette, for controls that must match the strip theme.</summary>
        internal Palette CurrentPalette
        {
            get { return _palette; }
        }

        /// <summary>The color theme. <see cref="FluentChromeTabsTheme.Auto" /> follows the Windows setting live.</summary>
        public FluentChromeTabsTheme Theme
        {
            get { return _theme; }
            set
            {
                _theme = value;
                ApplyTheme();
            }
        }

        /// <summary>
        /// Optional custom chrome color. When set, the tab strip takes this exact color and a full
        /// palette (tab surfaces, text, hover states) is derived from its brightness, overriding
        /// <see cref="Theme" />. Set to null to return to the Light/Dark/Auto themes.
        /// </summary>
        public Color? CustomThemeColor
        {
            get { return _customThemeColor; }
            set
            {
                _customThemeColor = value;
                ApplyTheme();
            }
        }

        private Color? _customThemeColor;

        /// <summary>True when the effective theme (after resolving <see cref="FluentChromeTabsTheme.Auto" /> or a custom color) is dark.</summary>
        public bool IsDarkTheme
        {
            get { return _isDark; }
        }

        /// <summary>Suggested background color for tab content, matching the active tab surface.</summary>
        public Color ContentBackColor
        {
            get { return _palette.TabActive; }
        }

        /// <summary>Suggested text color for tab content.</summary>
        public Color ContentForeColor
        {
            get { return _palette.TextActive; }
        }

        /// <summary>Raised after a tab is added to this window.</summary>
        public event EventHandler<FluentTabEventArgs> TabAdded;

        /// <summary>Raised before a user-initiated tab close; set Cancel to keep the tab.</summary>
        public event EventHandler<FluentTabClosingEventArgs> TabClosing;

        /// <summary>Raised after a tab is closed and its content disposed.</summary>
        public event EventHandler<FluentTabEventArgs> TabClosed;

        /// <summary>Raised when the selected tab changes.</summary>
        public event EventHandler<FluentTabEventArgs> SelectedTabChanged;

        /// <summary>Raised when the user requests a new tab (the "+" button or Ctrl+T).</summary>
        public event EventHandler<NewTabRequestedEventArgs> NewTabRequested;

        /// <summary>
        /// Raised when the user drags a tab across the divider into the other split-strip region
        /// (only fires when <see cref="SplitStrip" /> is on and the tab's region actually changed).
        /// </summary>
        public event EventHandler<FluentTabGroupEventArgs> TabDraggedToGroup;

        /// <summary>Raised whenever the effective theme changes (including Windows switching light/dark in Auto mode).</summary>
        public event EventHandler ThemeChanged;

        /// <summary>Adds a tab to the end of the strip and selects it.</summary>
        public FluentTab AddTab(string title, Control content)
        {
            return InsertTab(_tabs.Count, new FluentTab(title, content));
        }

        /// <summary>
        /// Adds a content-less tab (loose mode): the tab is pure metadata and your own static content —
        /// a WebView, a UserControl, anything you add to the form yourself — reacts to
        /// <see cref="SelectedTabChanged" />.
        /// </summary>
        public FluentTab AddTab(string title)
        {
            return InsertTab(_tabs.Count, new FluentTab(title));
        }

        /// <summary>Adds a tab to the end of the strip and selects it.</summary>
        public FluentTab AddTab(FluentTab tab)
        {
            return InsertTab(_tabs.Count, tab);
        }

        /// <summary>Inserts a tab at <paramref name="index" /> and selects it.</summary>
        public FluentTab InsertTab(int index, FluentTab tab)
        {
            if (tab == null)
            {
                throw new ArgumentNullException("tab");
            }

            if (tab.Owner != null)
            {
                throw new InvalidOperationException("Tab already belongs to a window. Close it there first.");
            }

            AttachTab(index, tab);
            TabAdded?.Invoke(this, new FluentTabEventArgs(tab));
            SelectTabCore(_tabs.IndexOf(tab));

            return tab;
        }

        /// <summary>Closes <paramref name="tab" />, raising <see cref="TabClosing" /> first. Returns false if canceled.</summary>
        public bool CloseTab(FluentTab tab)
        {
            if (tab == null || !_tabs.Contains(tab))
            {
                return false;
            }

            FluentTabClosingEventArgs closing = new FluentTabClosingEventArgs(tab);
            TabClosing?.Invoke(this, closing);

            if (closing.Cancel)
            {
                return false;
            }

            DetachTabCore(tab);
            tab.Content?.Dispose();
            TabClosed?.Invoke(this, new FluentTabEventArgs(tab));

            if (_tabs.Count == 0 && ExitOnLastTabClose)
            {
                Close();
            }

            return true;
        }

        /// <summary>
        /// Requests a new tab exactly as the "+" button does: raises <see cref="NewTabRequested" /> and adds
        /// the resulting tab (a blank one when no handler set content). <paramref name="group" /> reports
        /// which split region's "+" was pressed (always 0 when the strip is not split).
        /// </summary>
        public void RequestNewTab(int group = 0)
        {
            NewTabRequestedEventArgs args = new NewTabRequestedEventArgs { Group = group };

            if (args.Title == "New Tab" && _newTabCounter > 0)
            {
                args.Title = "New Tab " + (_newTabCounter + 1);
            }

            NewTabRequested?.Invoke(this, args);

            if (args.Cancel)
            {
                return;
            }

            _newTabCounter++;
            FluentTab added = AddTab(args.Title, args.Content);
            added.Group = group;
        }

        #endregion

        #region Tab bookkeeping

        private void AttachTab(int index, FluentTab tab)
        {
            index = Math.Max(0, Math.Min(index, _tabs.Count));

            tab.Owner = this;

            if (tab.Content != null)
            {
                tab.Content.Dock = DockStyle.Fill;
                tab.Content.Visible = false;
                Controls.Add(tab.Content);
            }

            if (index <= _selectedIndex)
            {
                _selectedIndex++;
            }

            _tabs.Insert(index, tab);
            InvalidateStrip();
        }

        /// <summary>Removes a tab from this window without disposing its content (used for closing and for drag transfers).</summary>
        private void DetachTabCore(FluentTab tab)
        {
            int index = _tabs.IndexOf(tab);

            _tabs.RemoveAt(index);

            if (tab.Content != null)
            {
                Controls.Remove(tab.Content);
            }

            tab.Owner = null;

            if (_tabs.Count == 0)
            {
                _selectedIndex = -1;
            }
            else
            {
                int newSelection = _selectedIndex;

                if (index < newSelection)
                {
                    newSelection--;
                }
                else if (index == newSelection)
                {
                    newSelection = Math.Min(index, _tabs.Count - 1);
                }

                _selectedIndex = -1;
                SelectTabCore(newSelection);
            }

            InvalidateStrip();
        }

        private void SelectTabCore(int index)
        {
            if (index == _selectedIndex)
            {
                return;
            }

            if (_selectedIndex >= 0 && _selectedIndex < _tabs.Count && _tabs[_selectedIndex].Content != null)
            {
                _tabs[_selectedIndex].Content.Visible = false;
            }

            _selectedIndex = index;

            if (index >= 0 && index < _tabs.Count)
            {
                FluentTab tab = _tabs[index];

                if (tab.Content != null)
                {
                    tab.Content.Visible = true;
                    tab.Content.Select();
                }

                SelectedTabChanged?.Invoke(this, new FluentTabEventArgs(tab));
            }

            InvalidateStrip();
        }

        internal void InvalidateStrip()
        {
            if (IsHandleCreated)
            {
                Invalidate(new Rectangle(0, 0, ClientSize.Width, StripHeightPx));
            }
        }

        #endregion

        #region Window icon

        private Icon _windowIconSource;
        private Bitmap _windowIconBitmap;
        private int _windowIconBitmapSize;

        /// <summary>The window icon rendered at the requested size, cached until the icon or DPI changes.</summary>
        private Bitmap GetWindowIconBitmap(int size)
        {
            if (!ReferenceEquals(_windowIconSource, Icon) || _windowIconBitmapSize != size)
            {
                _windowIconBitmap?.Dispose();
                _windowIconBitmap = null;
                _windowIconSource = Icon;
                _windowIconBitmapSize = size;

                if (Icon != null)
                {
                    // new Icon(icon, w, h) picks the closest embedded frame for crisp small sizes
                    using (Icon sized = new Icon(Icon, size, size))
                    {
                        _windowIconBitmap = sized.ToBitmap();
                    }
                }
            }

            return _windowIconBitmap;
        }

        #endregion

        #region Tab list dropdown

        private TabListDropDown _tabListDropDown;
        private int _tabListClosedTick;
        private int _tabListOpenGroup = -1;

        /// <summary>True while the tab-list popup is on screen (drives the toggle's open state).</summary>
        internal bool IsTabListOpen
        {
            get { return _tabListDropDown != null && !_tabListDropDown.IsDisposed && _tabListDropDown.Visible; }
        }

        /// <summary>The split region whose tab-list popup is open, or -1 when none is.</summary>
        internal int TabListOpenGroup
        {
            get { return IsTabListOpen ? _tabListOpenGroup : -1; }
        }

        /// <summary>
        /// Opens the Fluent-styled popup listing the region's open tabs plus any host-provided
        /// sections; picking an entry activates it. Acts as a toggle: invoking it while
        /// the popup is open (or immediately after the opening click dismissed it via
        /// deactivation) closes instead of reopening.
        /// </summary>
        private void ShowTabListMenu(int group)
        {
            if (IsTabListOpen)
            {
                _tabListDropDown.Close();
                return;
            }

            // Reclicking the toggle: the mousedown first deactivates the popup (closing it),
            // then reaches us — an immediate reopen would make the toggle feel stuck open.
            if (Environment.TickCount - _tabListClosedTick < 300)
            {
                return;
            }

            List<TabListSection> sections = new List<TabListSection>();
            TabListSection open = new TabListSection(TabListOpenTabsHeader);

            foreach (FluentTab tab in _tabs)
            {
                if (_splitStrip && GroupOf(tab) != group)
                {
                    continue;
                }

                FluentTab captured = tab;
                open.Items.Add(new TabListItem(
                    string.IsNullOrEmpty(tab.Title) ? "…" : tab.Title,
                    tab == SelectedTab || tab.Highlighted,
                    () => SelectedTab = captured));
            }

            if (open.Items.Count > 0)
            {
                sections.Add(open);
            }

            TabListOpening?.Invoke(this, new TabListOpeningEventArgs(sections, group));

            sections.RemoveAll(s => s.Items.Count == 0);

            if (sections.Count == 0)
            {
                return;
            }

            TabListDropDown drop = new TabListDropDown(this, sections);
            _tabListDropDown = drop;
            _tabListOpenGroup = group;

            drop.FormClosed += (s, e) =>
            {
                _tabListClosedTick = Environment.TickCount;
                _tabListDropDown = null;
                _tabListOpenGroup = -1;
                InvalidateStrip();
                BeginInvoke(new Action(drop.Dispose));
            };

            // Anchor below the toggle. In a mirrored (RTL) window the anchor lands on the
            // button's visual-right edge, so the popup right-aligns to it; clamp on-screen.
            Rectangle rect = TabListButtonRect(group);
            Point anchor = PointToScreen(new Point(rect.Left, StripHeightPx - Dpi(2)));
            Rectangle workArea = Screen.FromControl(this).WorkingArea;

            int x = IsMirrored ? anchor.X - drop.Width : anchor.X;
            x = Math.Max(workArea.Left, Math.Min(x, workArea.Right - drop.Width));
            int y = Math.Min(anchor.Y, workArea.Bottom - drop.Height);

            drop.Location = new Point(x, y);
            drop.Show(this);
            InvalidateStrip();
        }

        #endregion

        #region Theme

        private void ApplyTheme()
        {
            if (_customThemeColor.HasValue)
            {
                _isDark = Palette.IsDarkColor(_customThemeColor.Value);
                _palette = Palette.FromColor(_customThemeColor.Value);
            }
            else
            {
                _isDark = _theme == FluentChromeTabsTheme.Dark || (_theme == FluentChromeTabsTheme.Auto && IsSystemDark());
                _palette = _isDark ? Palette.Dark : Palette.Light;
            }

            BackColor = _palette.Strip;

            if (IsHandleCreated)
            {
                ApplyDwmDarkMode();
                RepaintNonClient();
            }

            ThemeChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        private static bool IsSystemDark()
        {
            try
            {
                object value = Registry.GetValue(
                    @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
                return value is int && (int) value == 0;
            }
            catch
            {
                return false;
            }
        }

        private void ApplyDwmDarkMode()
        {
            int dark = _isDark ? 1 : 0;

            if (NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int)) != 0)
            {
                // Windows 10 before 20H1 used attribute 19 for the same flag
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref dark, sizeof(int));
            }
        }

        /// <summary>
        /// Forces the non-client frame to repaint after a DWM attribute change on an already-visible
        /// window: toggling WM_NCACTIVATE is the only thing that reliably does this on Windows 10.
        /// </summary>
        private void RepaintNonClient()
        {
            IntPtr active = new IntPtr(_windowActive ? 1 : 0);
            IntPtr inactive = new IntPtr(_windowActive ? 0 : 1);

            NativeMethods.SendMessage(Handle, NativeMethods.WM_NCACTIVATE, inactive, IntPtr.Zero);
            NativeMethods.SendMessage(Handle, NativeMethods.WM_NCACTIVATE, active, IntPtr.Zero);
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General && _theme == FluentChromeTabsTheme.Auto)
            {
                ApplyTheme();
            }
        }

        #endregion

        #region Metrics

        // Control.DeviceDpi can stay stuck at 96 in .NET Framework WinForms apps that are
        // DPI-aware at the process level but run in the legacy WinForms DPI mode (no
        // DpiAwareness app.config entry). Ask the window itself so strip metrics match
        // the monitor's real scale; cached until the handle or DPI changes.
        private int _effectiveDpi;

        internal int EffectiveDpi
        {
            get
            {
                if (_effectiveDpi > 0)
                {
                    return _effectiveDpi;
                }

                if (IsHandleCreated)
                {
                    try
                    {
                        _effectiveDpi = (int) NativeMethods.GetDpiForWindow(Handle);
                    }
                    catch (EntryPointNotFoundException)
                    {
                        // Pre-1607 Windows 10 — fall back to whatever WinForms reports
                        _effectiveDpi = DeviceDpi;
                    }

                    if (_effectiveDpi <= 0)
                    {
                        _effectiveDpi = DeviceDpi;
                    }

                    return _effectiveDpi;
                }

                return DeviceDpi;
            }
        }

        private int Dpi(int logical)
        {
            return (int) Math.Round(logical * EffectiveDpi / 96.0);
        }

        private int StripHeightPx
        {
            get { return Dpi(_stripHeight); }
        }

        private int TabHeightPx
        {
            get { return Dpi(Math.Min(_tabHeight, _stripHeight - 8)); }
        }

        /// <summary>Gap between the bottom of the floating tab cards and the strip edge (Edge style).</summary>
        private int TabBottomMarginPx
        {
            get { return Dpi(4); }
        }

        private int TabGapPx
        {
            get { return Dpi(2); }
        }

        private int TabRadiusPx
        {
            get { return Dpi(6); }
        }

        private int CaptionButtonWidthPx
        {
            get { return Dpi(46); }
        }

        private int CaptionButtonHeightPx
        {
            get { return Dpi(32); }
        }

        private bool HasWindowIcon
        {
            get { return _showWindowIcon && Icon != null; }
        }

        private int WindowIconSizePx
        {
            get { return Dpi(16); }
        }

        /// <summary>Slot for the window icon, just before the start of the tabs.</summary>
        private Rectangle WindowIconRect()
        {
            int size = WindowIconSizePx;
            int tabTop = StripHeightPx - TabHeightPx - TabBottomMarginPx;
            return new Rectangle(Dpi(10), tabTop + (TabHeightPx - size) / 2, size, size);
        }

        private int TabListButtonSizePx
        {
            get { return Dpi(24); }
        }

        private int NewTabButtonSizePx
        {
            get { return Dpi(28); }
        }

        /// <summary>X coordinate of the split divider (pixel pin when set, else <see cref="SplitRatio" />).</summary>
        private int DividerXPx
        {
            get
            {
                return _dividerOverrideLeft >= 0
                    ? _dividerOverrideLeft + _dividerOverrideWidth / 2
                    : (int) Math.Round(ClientSize.Width * _splitRatio);
            }
        }

        /// <summary>Divider grab area — wider than the painted line, like the Vue divider's hit zone.</summary>
        private Rectangle DividerHitRect
        {
            get { return new Rectangle(DividerXPx - Dpi(10), 0, Dpi(20), StripHeightPx); }
        }

        /// <summary>Inline-start edge of a strip region (icon slot only precedes region 0).</summary>
        private int RegionLeftPx(int group)
        {
            return group == 1
                ? DividerXPx + Dpi(8)
                : Dpi(8) + (HasWindowIcon ? WindowIconSizePx + Dpi(10) : 0);
        }

        /// <summary>Inline-end edge of a strip region (caption buttons bound the last region).</summary>
        private int RegionRightPx(int group)
        {
            return _splitStrip && group == 0
                ? DividerXPx - Dpi(8)
                : ClientSize.Width - (3 * CaptionButtonWidthPx + Dpi(8));
        }

        /// <summary>Slot for a region's tab-list dropdown button, before its tabs.</summary>
        private Rectangle TabListButtonRect(int group)
        {
            int size = TabListButtonSizePx;
            int tabTop = StripHeightPx - TabHeightPx - TabBottomMarginPx;
            return new Rectangle(RegionLeftPx(group), tabTop + (TabHeightPx - size) / 2, size, size);
        }

        private int GroupTabsLeftPx(int group)
        {
            return RegionLeftPx(group) + (_showTabListButton ? TabListButtonSizePx + Dpi(6) : 0);
        }

        private int GroupTabsRightPx(int group)
        {
            return RegionRightPx(group) - (ShowNewTabButton ? NewTabButtonSizePx + Dpi(8) : 0);
        }

        private int GroupTabCount(int group)
        {
            int count = 0;

            for (int i = 0; i < _tabs.Count; i++)
            {
                if (GroupOf(_tabs[i]) == group)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Position of the tab at <paramref name="index" /> within its own region.</summary>
        private int GroupPosition(int index)
        {
            int group = GroupOf(_tabs[index]);
            int position = 0;

            for (int i = 0; i < index; i++)
            {
                if (GroupOf(_tabs[i]) == group)
                {
                    position++;
                }
            }

            return position;
        }

        /// <summary>Global index of the region member at position <paramref name="position" />.</summary>
        private int GlobalIndexOfGroupPosition(int group, int position)
        {
            int seen = 0;

            for (int i = 0; i < _tabs.Count; i++)
            {
                if (GroupOf(_tabs[i]) == group)
                {
                    if (seen == position)
                    {
                        return i;
                    }

                    seen++;
                }
            }

            return _tabs.Count - 1;
        }

        private int TabWidthPx(int group)
        {
            int count = GroupTabCount(group);

            if (count == 0)
            {
                return Dpi(220);
            }

            int available = Math.Max(0, GroupTabsRightPx(group) - GroupTabsLeftPx(group));
            int width = (available - (count - 1) * TabGapPx) / count;

            return Math.Max(Dpi(48), Math.Min(Dpi(220), width));
        }

        private Rectangle TabRect(int index)
        {
            int group = GroupOf(_tabs[index]);
            int width = TabWidthPx(group);

            return new Rectangle(
                GroupTabsLeftPx(group) + GroupPosition(index) * (width + TabGapPx),
                StripHeightPx - TabHeightPx - TabBottomMarginPx,
                width,
                TabHeightPx);
        }

        private Rectangle TabCloseRect(Rectangle tabRect)
        {
            int size = Dpi(16);
            return new Rectangle(tabRect.Right - size - Dpi(8), tabRect.Top + (tabRect.Height - size) / 2, size, size);
        }

        private bool TabShowsClose(int index)
        {
            return _tabs[index].CanClose
                && (index == _selectedIndex || _tabs[index].Highlighted || TabWidthPx(GroupOf(_tabs[index])) >= Dpi(72));
        }

        private Rectangle NewTabButtonRect(int group)
        {
            int count = GroupTabCount(group);
            int last = count > 0
                ? GroupTabsLeftPx(group) + count * (TabWidthPx(group) + TabGapPx) - TabGapPx
                : GroupTabsLeftPx(group);
            int size = NewTabButtonSizePx;
            int tabTop = StripHeightPx - TabHeightPx - TabBottomMarginPx;
            return new Rectangle(last + Dpi(8), tabTop + (TabHeightPx - size) / 2, size, size);
        }

        /// <summary>True when the point hits any region's "+" or tab-list toggle.</summary>
        private bool HitsStripButton(Point p)
        {
            for (int group = 0; group < GroupCount; group++)
            {
                if (ShowNewTabButton && NewTabButtonRect(group).Contains(p))
                {
                    return true;
                }

                if (ShowTabListButton && TabListButtonRect(group).Contains(p))
                {
                    return true;
                }
            }

            return false;
        }

        private Rectangle CaptionButtonRect(int htCode)
        {
            int slotFromRight = htCode == NativeMethods.HTCLOSE ? 1 : htCode == NativeMethods.HTMAXBUTTON ? 2 : 3;
            return new Rectangle(ClientSize.Width - slotFromRight * CaptionButtonWidthPx, 0, CaptionButtonWidthPx, CaptionButtonHeightPx);
        }

        private int HitTab(Point p)
        {
            if (p.Y >= StripHeightPx)
            {
                return -1;
            }

            for (int i = 0; i < _tabs.Count; i++)
            {
                if (TabRect(i).Contains(p))
                {
                    return i;
                }
            }

            return -1;
        }

        private void UpdateMetrics()
        {
            Padding = _stripVisible ? new Padding(0, StripHeightPx, 0, 0) : Padding.Empty;
        }

        #endregion

        #region Lifecycle

        /// <summary>Number of <see cref="FluentChromeTabsForm" /> windows currently open in the process.</summary>
        public static int OpenWindowCount
        {
            get { return OpenTabForms.Count; }
        }

        /// <summary>Raised after any <see cref="FluentChromeTabsForm" /> in the process closes. Used by <see cref="FluentChromeTabsApplicationContext" />.</summary>
        public static event EventHandler WindowClosed;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // Re-resolve the monitor DPI for the new handle before any metrics are computed
            _effectiveDpi = 0;

            ApplyDwmDarkMode();

            // A 1px top extension keeps DWM's subtle frame line above the strip
            NativeMethods.MARGINS margins = new NativeMethods.MARGINS { top = 1 };
            NativeMethods.DwmExtendFrameIntoClientArea(Handle, ref margins);

            // Re-run WM_NCCALCSIZE now that the caption is being reclaimed
            NativeMethods.SetWindowPos(
                Handle, IntPtr.Zero, 0, 0, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);

            UpdateMetrics();

            if (!OpenTabForms.Contains(this))
            {
                OpenTabForms.Add(this);
            }

            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

            base.OnHandleDestroyed(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            OpenTabForms.Remove(this);
            base.OnFormClosed(e);

            EventHandler handler = WindowClosed;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);

            _effectiveDpi = 0;
            UpdateMetrics();
            Invalidate();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            _windowActive = true;
            InvalidateStrip();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            _windowActive = false;
            InvalidateStrip();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip?.Dispose();
                _windowIconBitmap?.Dispose();
            }

            base.Dispose(disposing);
        }

        #endregion

        #region Keyboard

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.T))
            {
                RequestNewTab();
                return true;
            }

            if (keyData == (Keys.Control | Keys.W) || keyData == (Keys.Control | Keys.F4))
            {
                if (SelectedTab != null && SelectedTab.CanClose)
                {
                    CloseTab(SelectedTab);
                }

                return true;
            }

            if (keyData == (Keys.Control | Keys.Tab))
            {
                CycleTab(1);
                return true;
            }

            if (keyData == (Keys.Control | Keys.Shift | Keys.Tab))
            {
                CycleTab(-1);
                return true;
            }

            if ((keyData & (Keys.Control | Keys.Alt | Keys.Shift)) == Keys.Control)
            {
                Keys key = keyData & Keys.KeyCode;

                if (key >= Keys.D1 && key <= Keys.D9)
                {
                    int number = key - Keys.D1;
                    SelectedIndex = key == Keys.D9 ? _tabs.Count - 1 : Math.Min(number, _tabs.Count - 1);
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void CycleTab(int direction)
        {
            if (_tabs.Count > 1)
            {
                SelectedIndex = (_selectedIndex + direction + _tabs.Count) % _tabs.Count;
            }
        }

        #endregion
    }
}
