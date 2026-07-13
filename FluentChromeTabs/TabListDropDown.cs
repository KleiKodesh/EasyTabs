using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FluentChromeTabs
{
    /// <summary>One row in the tab-list dropdown.</summary>
    public class TabListItem
    {
        public TabListItem(string text, bool active, Action activate)
        {
            Text = text ?? string.Empty;
            Active = active;
            Activate = activate;
        }

        /// <summary>Display text.</summary>
        public string Text { get; }

        /// <summary>Whether this row represents the currently selected tab (gets the accent indicator).</summary>
        public bool Active { get; }

        /// <summary>Invoked when the row is clicked, right after the dropdown closes.</summary>
        public Action Activate { get; }
    }

    /// <summary>A headed group of rows in the tab-list dropdown.</summary>
    public class TabListSection
    {
        public TabListSection(string header)
        {
            Header = header ?? string.Empty;
        }

        public string Header { get; }

        public List<TabListItem> Items { get; } = new List<TabListItem>();
    }

    /// <summary>
    /// Raised right before the tab-list dropdown opens. The open-tabs section is pre-populated;
    /// handlers may append additional sections (e.g. recently closed documents).
    /// </summary>
    public class TabListOpeningEventArgs : EventArgs
    {
        public TabListOpeningEventArgs(List<TabListSection> sections)
        {
            Sections = sections;
        }

        public List<TabListSection> Sections { get; }
    }

    /// <summary>
    /// Fluent-styled popup listing tabs in headed sections. Fully owner-drawn from the strip
    /// palette: hover/focus rows, an accent indicator pill on the active tab, section headers,
    /// truncation tooltips, and manual RTL layout (the popup itself is never mirrored —
    /// alignment is computed explicitly). Item rows match the strip's tab height so mouse and
    /// touch targets feel identical to the tabs themselves; taps work through Windows'
    /// touch-to-mouse promotion (activation on mouse-up, no hover dependency).
    ///
    /// Keyboard: Up/Down/Home/End move the focus row, Enter activates, Esc closes.
    /// Implemented as an owned borderless form so click/tap-outside reliably dismisses it
    /// (via Deactivate), which a ToolStripDropDown hosting a focusable control does not do.
    /// </summary>
    internal sealed class TabListDropDown : Form
    {
        private const int RowKindHeader = 0;
        private const int RowKindItem = 1;
        private const int RowKindSeparator = 2;

        private const int CS_DROPSHADOW = 0x00020000;

        private struct Row
        {
            public int Kind;
            public string Text;
            public TabListItem Item;
        }

        private readonly FluentChromeTabsForm _ownerForm;
        private readonly List<Row> _rows = new List<Row>();
        private readonly Palette _palette;
        private readonly bool _rtl;
        private readonly float _scale;
        private readonly ToolTip _toolTip = new ToolTip();
        private Font _headerFont;
        private int _hoverRow = -1;
        private int _focusRow = -1;
        private int _scrollY;
        private string _toolTipShownFor;

        public TabListDropDown(FluentChromeTabsForm owner, List<TabListSection> sections)
        {
            _ownerForm = owner;
            _palette = owner.CurrentPalette;
            _rtl = owner.RightToLeft == RightToLeft.Yes;
            _scale = owner.EffectiveDpi / 96f;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw,
                true);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Font = owner.Font;
            _headerFont = new Font(owner.Font.FontFamily, owner.Font.Size * 0.85f);
            BackColor = _palette.TabActive;

            bool first = true;

            foreach (TabListSection section in sections)
            {
                if (section.Items.Count == 0)
                {
                    continue;
                }

                if (!first)
                {
                    _rows.Add(new Row { Kind = RowKindSeparator });
                }

                first = false;

                if (!string.IsNullOrEmpty(section.Header))
                {
                    _rows.Add(new Row { Kind = RowKindHeader, Text = section.Header });
                }

                foreach (TabListItem item in section.Items)
                {
                    _rows.Add(new Row { Kind = RowKindItem, Text = item.Text, Item = item });

                    if (item.Active && _focusRow < 0)
                    {
                        _focusRow = _rows.Count - 1;
                    }
                }
            }

            ClientSize = MeasureContent();

            if (_focusRow >= 0)
            {
                EnsureRowVisible(_focusRow);
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // Windows 11 rounded popup corners; harmless no-op on Windows 10
            int preference = NativeMethods.DWMWCP_ROUNDSMALL;
            NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }

        // Click/tap-outside dismissal: interacting with anything else (including the owner
        // window) deactivates the popup, which closes it.
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Close();
        }

        // ── Keyboard navigation ─────────────────────────────────────────────────────

        protected override bool ProcessDialogKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Escape:
                    Close();
                    return true;

                case Keys.Down:
                    MoveFocus(1);
                    return true;

                case Keys.Up:
                    MoveFocus(-1);
                    return true;

                case Keys.Home:
                    FocusEdge(first: true);
                    return true;

                case Keys.End:
                    FocusEdge(first: false);
                    return true;

                case Keys.Enter:
                case Keys.Space:
                    if (_focusRow >= 0 && _rows[_focusRow].Kind == RowKindItem)
                    {
                        ActivateRow(_focusRow);
                    }

                    return true;
            }

            return base.ProcessDialogKey(keyData);
        }

        /// <summary>Moves the focus row to the next/previous item row, wrapping around.</summary>
        private void MoveFocus(int direction)
        {
            if (_rows.Count == 0)
            {
                return;
            }

            int start = _focusRow >= 0 ? _focusRow : (direction > 0 ? -1 : _rows.Count);

            for (int step = 1; step <= _rows.Count; step++)
            {
                int i = ((start + direction * step) % _rows.Count + _rows.Count) % _rows.Count;

                if (_rows[i].Kind == RowKindItem)
                {
                    SetFocusRow(i);
                    return;
                }
            }
        }

        private void FocusEdge(bool first)
        {
            for (int step = 0; step < _rows.Count; step++)
            {
                int i = first ? step : _rows.Count - 1 - step;

                if (_rows[i].Kind == RowKindItem)
                {
                    SetFocusRow(i);
                    return;
                }
            }
        }

        private void SetFocusRow(int index)
        {
            _focusRow = index;
            EnsureRowVisible(index);
            Invalidate();
        }

        // ── Layout ──────────────────────────────────────────────────────────────────

        private int S(int logical)
        {
            return (int) Math.Round(logical * _scale);
        }

        private int PadX
        {
            get { return S(6); }
        }

        private int PadY
        {
            get { return S(6); }
        }

        private int RowHeight(int kind)
        {
            // Item rows match the strip's tab height so the popup's touch targets
            // feel identical to the tabs themselves
            return kind == RowKindHeader ? S(24) : kind == RowKindSeparator ? S(9) : S(_ownerForm.TabHeight);
        }

        private int ContentHeight
        {
            get
            {
                int height = PadY * 2;

                foreach (Row row in _rows)
                {
                    height += RowHeight(row.Kind);
                }

                return height;
            }
        }

        private Size MeasureContent()
        {
            int width = S(230);

            foreach (Row row in _rows)
            {
                if (row.Text != null)
                {
                    Font font = row.Kind == RowKindHeader ? _headerFont : Font;
                    width = Math.Max(width, TextRenderer.MeasureText(row.Text, font).Width + S(56));
                }
            }

            width = Math.Min(width, S(340));
            int height = Math.Min(ContentHeight, Screen.FromControl(_ownerForm).WorkingArea.Height * 3 / 4);

            return new Size(width, height);
        }

        private Rectangle RowRect(int index)
        {
            int y = PadY - _scrollY;

            for (int i = 0; i < index; i++)
            {
                y += RowHeight(_rows[i].Kind);
            }

            return new Rectangle(PadX, y, ClientSize.Width - PadX * 2, RowHeight(_rows[index].Kind));
        }

        private int HitRow(Point p)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Kind == RowKindItem && RowRect(i).Contains(p))
                {
                    return i;
                }
            }

            return -1;
        }

        // ── Scrolling ───────────────────────────────────────────────────────────────

        private int MaxScroll
        {
            get { return Math.Max(0, ContentHeight - ClientSize.Height); }
        }

        private void ScrollTo(int y)
        {
            int clamped = Math.Max(0, Math.Min(y, MaxScroll));

            if (clamped != _scrollY)
            {
                _scrollY = clamped;
                Invalidate();
            }
        }

        private void EnsureRowVisible(int index)
        {
            Rectangle rect = RowRect(index);

            if (rect.Top < PadY)
            {
                ScrollTo(_scrollY + rect.Top - PadY);
            }
            else if (rect.Bottom > ClientSize.Height - PadY)
            {
                ScrollTo(_scrollY + rect.Bottom - (ClientSize.Height - PadY));
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ScrollTo(_scrollY - Math.Sign(e.Delta) * RowHeight(RowKindItem) * 2);
            OnMouseMove(e);
        }

        // ── Painting ────────────────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (SolidBrush bg = new SolidBrush(_palette.TabActive))
            {
                g.FillRectangle(bg, ClientRectangle);
            }

            TextFormatFlags flags =
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                (_rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left);

            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                Rectangle rect = RowRect(i);

                if (rect.Bottom < 0 || rect.Top > ClientSize.Height)
                {
                    continue;
                }

                if (row.Kind == RowKindSeparator)
                {
                    using (Pen pen = new Pen(_palette.Separator))
                    {
                        int y = rect.Top + rect.Height / 2;
                        g.DrawLine(pen, rect.Left + S(4), y, rect.Right - S(4), y);
                    }

                    continue;
                }

                if (row.Kind == RowKindHeader)
                {
                    Rectangle headerRect = InsetInline(rect, S(10), S(10));
                    TextRenderer.DrawText(g, row.Text, _headerFont, headerRect, _palette.TextInactive, flags);
                    continue;
                }

                bool hover = i == _hoverRow;
                bool focused = i == _focusRow;
                bool active = row.Item != null && row.Item.Active;

                if (hover || focused || active)
                {
                    double amount = hover || focused ? 0.08 : 0.05;

                    using (SolidBrush brush = new SolidBrush(Palette.Blend(_palette.TabActive, _palette.TextActive, amount)))
                    using (GraphicsPath path = RoundedRect(rect, S(4)))
                    {
                        g.FillPath(brush, path);
                    }
                }

                if (active)
                {
                    // Fluent selection indicator: a small rounded accent pill on the inline-start edge
                    Color accent = _ownerForm.AccentColor ?? _palette.TextActive;
                    int pillH = S(16);
                    int pillW = S(3);
                    int pillX = _rtl ? rect.Right - S(5) - pillW : rect.Left + S(5);
                    Rectangle pill = new Rectangle(pillX, rect.Top + (rect.Height - pillH) / 2, pillW, pillH);

                    using (SolidBrush brush = new SolidBrush(accent))
                    using (GraphicsPath path = RoundedRect(pill, pillW / 2 + 1))
                    {
                        g.FillPath(brush, path);
                    }
                }

                TextRenderer.DrawText(g, row.Text, Font, TextRectFor(rect), _palette.TextActive, flags);
            }

            // Hairline border so the popup reads as a surface over same-colored content
            using (Pen pen = new Pen(_palette.TabBorder))
            {
                g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            }
        }

        private Rectangle TextRectFor(Rectangle rowRect)
        {
            return InsetInline(rowRect, S(16), S(12));
        }

        /// <summary>Insets a rectangle by start/end amounts along the reading direction.</summary>
        private Rectangle InsetInline(Rectangle rect, int start, int end)
        {
            return _rtl
                ? Rectangle.FromLTRB(rect.Left + end, rect.Top, rect.Right - start, rect.Bottom)
                : Rectangle.FromLTRB(rect.Left + start, rect.Top, rect.Right - end, rect.Bottom);
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, radius * 2, radius * 2, 180, 90);
            path.AddArc(r.Right - radius * 2, r.Top, radius * 2, radius * 2, 270, 90);
            path.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(r.Left, r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ── Mouse / touch ───────────────────────────────────────────────────────────

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int row = HitRow(e.Location);

            if (row != _hoverRow)
            {
                _hoverRow = row;
                Cursor = row >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
                UpdateToolTip(row);
            }
        }

        /// <summary>Shows the full title as a tooltip when it is truncated in its row.</summary>
        private void UpdateToolTip(int row)
        {
            string tip = null;

            if (row >= 0)
            {
                Rectangle rect = RowRect(row);
                string text = _rows[row].Text;

                if (TextRenderer.MeasureText(text, Font).Width > TextRectFor(rect).Width)
                {
                    tip = text;
                }
            }

            if (tip != _toolTipShownFor)
            {
                _toolTipShownFor = tip;

                if (tip != null)
                {
                    Rectangle rect = RowRect(row);
                    _toolTip.Show(tip, this, _rtl ? PadX : PadX + S(12), rect.Bottom + S(2), 3000);
                }
                else
                {
                    _toolTip.Hide(this);
                }
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            if (_hoverRow != -1)
            {
                _hoverRow = -1;
                Invalidate();
                UpdateToolTip(-1);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            int row = HitRow(e.Location);

            if (row >= 0)
            {
                ActivateRow(row);
            }
        }

        private void ActivateRow(int row)
        {
            TabListItem item = _rows[row].Item;
            Close();
            item.Activate?.Invoke();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip.Dispose();
                _headerFont?.Dispose();
                _headerFont = null;
            }

            base.Dispose(disposing);
        }
    }
}
