using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FluentTabs
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
    /// var window = new FluentTabForm { Text = "My App" };
    /// window.AddTab("Home", new HomeControl());
    /// Application.Run(window);
    /// </code>
    /// </summary>
    public partial class FluentTabForm : Form
    {
        private static readonly List<FluentTabForm> OpenTabForms = new List<FluentTabForm>();

        private readonly List<FluentTab> _tabs = new List<FluentTab>();
        private readonly ToolTip _toolTip = new ToolTip();

        private int _selectedIndex = -1;
        private FluentTabsTheme _theme = FluentTabsTheme.Auto;
        private Palette _palette;
        private bool _isDark;
        private bool _windowActive = true;
        private int _newTabCounter;

        public FluentTabForm()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
                true);

            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(1000, 640);
            Text = "FluentTabs";

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

        /// <summary>The color theme. <see cref="FluentTabsTheme.Auto" /> follows the Windows setting live.</summary>
        public FluentTabsTheme Theme
        {
            get { return _theme; }
            set
            {
                _theme = value;
                ApplyTheme();
            }
        }

        /// <summary>True when the effective theme (after resolving <see cref="FluentTabsTheme.Auto" />) is dark.</summary>
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

        /// <summary>Raised whenever the effective theme changes (including Windows switching light/dark in Auto mode).</summary>
        public event EventHandler ThemeChanged;

        /// <summary>Adds a tab to the end of the strip and selects it.</summary>
        public FluentTab AddTab(string title, Control content)
        {
            return InsertTab(_tabs.Count, new FluentTab(title, content));
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
            tab.Content.Dispose();
            TabClosed?.Invoke(this, new FluentTabEventArgs(tab));

            if (_tabs.Count == 0 && ExitOnLastTabClose)
            {
                Close();
            }

            return true;
        }

        /// <summary>
        /// Requests a new tab exactly as the "+" button does: raises <see cref="NewTabRequested" /> and adds
        /// the resulting tab (a blank one when no handler set content).
        /// </summary>
        public void RequestNewTab()
        {
            NewTabRequestedEventArgs args = new NewTabRequestedEventArgs();

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
            Control content = args.Content ?? new Panel { BackColor = ContentBackColor };
            AddTab(args.Title, content);
        }

        #endregion

        #region Tab bookkeeping

        private void AttachTab(int index, FluentTab tab)
        {
            index = Math.Max(0, Math.Min(index, _tabs.Count));

            tab.Owner = this;
            tab.Content.Dock = DockStyle.Fill;
            tab.Content.Visible = false;
            Controls.Add(tab.Content);

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
            Controls.Remove(tab.Content);
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

            if (_selectedIndex >= 0 && _selectedIndex < _tabs.Count)
            {
                _tabs[_selectedIndex].Content.Visible = false;
            }

            _selectedIndex = index;

            if (index >= 0 && index < _tabs.Count)
            {
                FluentTab tab = _tabs[index];
                tab.Content.Visible = true;
                tab.Content.Select();
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

        #region Theme

        private void ApplyTheme()
        {
            bool dark = _theme == FluentTabsTheme.Dark || (_theme == FluentTabsTheme.Auto && IsSystemDark());

            _isDark = dark;
            _palette = dark ? Palette.Dark : Palette.Light;
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
            if (e.Category == UserPreferenceCategory.General && _theme == FluentTabsTheme.Auto)
            {
                ApplyTheme();
            }
        }

        #endregion

        #region Metrics

        private int Dpi(int logical)
        {
            return (int) Math.Round(logical * DeviceDpi / 96.0);
        }

        private int StripHeightPx
        {
            get { return Dpi(40); }
        }

        private int TabHeightPx
        {
            get { return Dpi(32); }
        }

        private int TabGapPx
        {
            get { return Dpi(4); }
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

        private int TabsLeftPx
        {
            get { return Dpi(8); }
        }

        private int NewTabButtonSizePx
        {
            get { return Dpi(28); }
        }

        private int TabsRightLimitPx
        {
            get
            {
                int reserved = 3 * CaptionButtonWidthPx + Dpi(8);

                if (ShowNewTabButton)
                {
                    reserved += NewTabButtonSizePx + Dpi(8);
                }

                return ClientSize.Width - reserved;
            }
        }

        private int TabWidthPx
        {
            get
            {
                if (_tabs.Count == 0)
                {
                    return Dpi(220);
                }

                int available = Math.Max(0, TabsRightLimitPx - TabsLeftPx);
                int width = (available - (_tabs.Count - 1) * TabGapPx) / _tabs.Count;

                return Math.Max(Dpi(48), Math.Min(Dpi(220), width));
            }
        }

        private Rectangle TabRect(int index)
        {
            return new Rectangle(TabsLeftPx + index * (TabWidthPx + TabGapPx), StripHeightPx - TabHeightPx, TabWidthPx, TabHeightPx);
        }

        private Rectangle TabCloseRect(Rectangle tabRect)
        {
            int size = Dpi(16);
            return new Rectangle(tabRect.Right - size - Dpi(8), tabRect.Top + (tabRect.Height - size) / 2, size, size);
        }

        private bool TabShowsClose(int index)
        {
            return _tabs[index].CanClose && (index == _selectedIndex || TabWidthPx >= Dpi(72));
        }

        private Rectangle NewTabButtonRect()
        {
            int last = _tabs.Count > 0 ? TabRect(_tabs.Count - 1).Right : TabsLeftPx;
            int size = NewTabButtonSizePx;
            return new Rectangle(last + Dpi(8), StripHeightPx - TabHeightPx + (TabHeightPx - size) / 2, size, size);
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
            Padding = new Padding(0, StripHeightPx, 0, 0);
        }

        #endregion

        #region Lifecycle

        /// <summary>Number of <see cref="FluentTabForm" /> windows currently open in the process.</summary>
        public static int OpenWindowCount
        {
            get { return OpenTabForms.Count; }
        }

        /// <summary>Raised after any <see cref="FluentTabForm" /> in the process closes. Used by <see cref="FluentTabsApplicationContext" />.</summary>
        public static event EventHandler WindowClosed;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

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
                _toolTip.Dispose();
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
