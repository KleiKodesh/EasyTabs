using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FluentChromeTabs
{
    /// <summary>Painting of the tab strip: tabs, new-tab button, and caption buttons.</summary>
    public partial class FluentChromeTabsForm
    {
        private int _hoverTab = -1;
        private bool _hoverTabClose;
        private bool _hoverNewTab;
        private string _toolTipShownFor;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (SolidBrush strip = new SolidBrush(_palette.Strip))
            {
                g.FillRectangle(strip, 0, 0, ClientSize.Width, StripHeightPx);
            }

            for (int i = 0; i < _tabs.Count; i++)
            {
                if (!_dragging || i != _dragIndex)
                {
                    DrawTab(g, i, TabRect(i));
                }
            }

            // The dragged tab floats above its neighbors at the cursor position
            if (_dragging && _dragIndex >= 0)
            {
                Rectangle rect = TabRect(_dragIndex);
                rect.X = _dragVisualX;
                DrawTab(g, _dragIndex, rect);
            }

            if (ShowNewTabButton)
            {
                DrawNewTabButton(g);
            }

            DrawCaptionButton(g, NativeMethods.HTMINBUTTON);
            DrawCaptionButton(g, NativeMethods.HTMAXBUTTON);
            DrawCaptionButton(g, NativeMethods.HTCLOSE);
        }

        private static GraphicsPath RoundedTopPath(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, radius * 2, radius * 2, 180, 90);
            path.AddArc(r.Right - radius * 2, r.Top, radius * 2, radius * 2, 270, 90);
            path.AddLine(r.Right, r.Bottom, r.Left, r.Bottom);
            path.CloseFigure();
            return path;
        }

        private Color StripTextColor(bool activeTab)
        {
            Color color = activeTab ? _palette.TextActive : _palette.TextInactive;
            return _windowActive ? color : Palette.Blend(color, _palette.Strip, 0.4);
        }

        private void DrawTab(Graphics g, int index, Rectangle rect)
        {
            bool active = index == _selectedIndex;
            bool hover = index == _hoverTab;
            FluentTab tab = _tabs[index];

            if (active || hover)
            {
                using (GraphicsPath path = RoundedTopPath(rect, TabRadiusPx))
                using (SolidBrush brush = new SolidBrush(active ? _palette.TabActive : _palette.TabHover))
                {
                    g.FillPath(brush, path);
                }
            }
            else if (index >= 0 && index < _tabs.Count - 1 && index + 1 != _selectedIndex && index + 1 != _hoverTab)
            {
                using (Pen pen = new Pen(_palette.Separator))
                {
                    int x = rect.Right + TabGapPx / 2;
                    g.DrawLine(pen, x, rect.Top + Dpi(8), x, rect.Bottom - Dpi(8));
                }
            }

            int textLeft = rect.Left + Dpi(10);

            if (tab.Icon != null)
            {
                int iconSize = Dpi(16);
                g.DrawImage(tab.Icon, new Rectangle(textLeft, rect.Top + (rect.Height - iconSize) / 2, iconSize, iconSize));
                textLeft += iconSize + Dpi(6);
            }

            bool showClose = TabShowsClose(index);
            int textRight = showClose ? TabCloseRect(rect).Left - Dpi(4) : rect.Right - Dpi(8);

            Rectangle textRect = new Rectangle(textLeft, rect.Top, Math.Max(0, textRight - textLeft), rect.Height);
            TextRenderer.DrawText(
                g, tab.Title, Font, textRect, StripTextColor(active),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (showClose)
            {
                Rectangle close = TabCloseRect(rect);

                if (_hoverTabClose && index == _hoverTab && !_dragging)
                {
                    Color surface = active ? _palette.TabActive : hover ? _palette.TabHover : _palette.Strip;

                    using (SolidBrush brush = new SolidBrush(Palette.Blend(surface, _palette.TextActive, 0.12)))
                    using (GraphicsPath path = RoundedRectPath(close, Dpi(4)))
                    {
                        g.FillPath(brush, path);
                    }
                }

                using (Pen pen = new Pen(StripTextColor(active), Dpi(1) * 1.1f))
                {
                    int inset = Dpi(5);
                    g.DrawLine(pen, close.Left + inset, close.Top + inset, close.Right - inset, close.Bottom - inset);
                    g.DrawLine(pen, close.Left + inset, close.Bottom - inset, close.Right - inset, close.Top + inset);
                }
            }
        }

        private static GraphicsPath RoundedRectPath(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, radius * 2, radius * 2, 180, 90);
            path.AddArc(r.Right - radius * 2, r.Top, radius * 2, radius * 2, 270, 90);
            path.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(r.Left, r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void DrawNewTabButton(Graphics g)
        {
            Rectangle rect = NewTabButtonRect();

            if (_hoverNewTab && !_dragging)
            {
                using (SolidBrush brush = new SolidBrush(Palette.Blend(_palette.Strip, _palette.TextActive, 0.1)))
                using (GraphicsPath path = RoundedRectPath(rect, Dpi(4)))
                {
                    g.FillPath(brush, path);
                }
            }

            using (Pen pen = new Pen(StripTextColor(false), Dpi(1) * 1.1f))
            {
                int cx = rect.Left + rect.Width / 2;
                int cy = rect.Top + rect.Height / 2;
                int arm = Dpi(5);
                g.DrawLine(pen, cx - arm, cy, cx + arm, cy);
                g.DrawLine(pen, cx, cy - arm, cx, cy + arm);
            }
        }

        private void DrawCaptionButton(Graphics g, int htCode)
        {
            Rectangle rect = CaptionButtonRect(htCode);
            bool hot = _hotCaptionButton == htCode;
            bool pressed = _pressedCaptionButton == htCode;
            bool isClose = htCode == NativeMethods.HTCLOSE;

            Color background = _palette.Strip;

            if (hot || pressed)
            {
                background = isClose
                    ? (pressed ? Palette.Blend(_palette.CloseButtonHover, _palette.Strip, 0.25) : _palette.CloseButtonHover)
                    : (pressed ? _palette.ButtonPressed : _palette.ButtonHover);

                using (SolidBrush brush = new SolidBrush(background))
                {
                    g.FillRectangle(brush, rect);
                }
            }

            Color glyphColor = isClose && (hot || pressed) ? Color.White : StripTextColor(true);
            int x = rect.Left + rect.Width / 2;
            int y = rect.Top + rect.Height / 2;
            int arm = Dpi(5);

            SmoothingMode previous = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;

            using (Pen pen = new Pen(glyphColor))
            {
                switch (htCode)
                {
                    case NativeMethods.HTMINBUTTON:
                        g.DrawLine(pen, x - arm, y, x + arm, y);
                        break;

                    case NativeMethods.HTMAXBUTTON:
                        if (NativeMethods.IsZoomed(Handle))
                        {
                            int box = Dpi(8);
                            int offset = Dpi(2);

                            // Back square first, then the front square erases its overlap
                            g.DrawRectangle(pen, x - box / 2 + offset - 1, y - box / 2 - offset + 1, box, box);

                            using (SolidBrush eraser = new SolidBrush(background))
                            {
                                g.FillRectangle(eraser, x - box / 2 - 1, y - box / 2 - 1, box + 2, box + 2);
                            }

                            g.DrawRectangle(pen, x - box / 2 - 1, y - box / 2 - 1, box + 1, box + 1);
                        }
                        else
                        {
                            int box = Dpi(10);
                            g.DrawRectangle(pen, x - box / 2, y - box / 2, box, box);
                        }

                        break;

                    case NativeMethods.HTCLOSE:
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.DrawLine(pen, x - arm, y - arm, x + arm, y + arm);
                        g.DrawLine(pen, x - arm, y + arm, x + arm, y - arm);
                        break;
                }
            }

            g.SmoothingMode = previous;
        }

        private void UpdateHoverState(Point p)
        {
            int tab = HitTab(p);
            bool overClose = tab >= 0 && TabShowsClose(tab) && TabCloseRect(TabRect(tab)).Contains(p);
            bool overNewTab = ShowNewTabButton && p.Y < StripHeightPx && NewTabButtonRect().Contains(p);

            if (tab != _hoverTab || overClose != _hoverTabClose || overNewTab != _hoverNewTab)
            {
                _hoverTab = tab;
                _hoverTabClose = overClose;
                _hoverNewTab = overNewTab;
                InvalidateStrip();
                UpdateToolTip();
            }
        }

        private void ClearHoverState()
        {
            if (_hoverTab != -1 || _hoverNewTab || _hoverTabClose)
            {
                _hoverTab = -1;
                _hoverTabClose = false;
                _hoverNewTab = false;
                InvalidateStrip();
                UpdateToolTip();
            }
        }

        /// <summary>Shows the full title as a tooltip when it is truncated on the tab.</summary>
        private void UpdateToolTip()
        {
            string tip = null;

            if (_hoverTab >= 0 && !_dragging)
            {
                FluentTab tab = _tabs[_hoverTab];
                Rectangle rect = TabRect(_hoverTab);
                int available = rect.Width - Dpi(10) - (tab.Icon != null ? Dpi(22) : 0) - (TabShowsClose(_hoverTab) ? Dpi(28) : Dpi(8));

                if (TextRenderer.MeasureText(tab.Title, Font).Width > available)
                {
                    tip = tab.Title;
                }
            }

            if (tip != _toolTipShownFor)
            {
                _toolTipShownFor = tip;

                if (tip != null)
                {
                    _toolTip.Show(tip, this, TabRect(_hoverTab).Left, StripHeightPx + Dpi(4), 3000);
                }
                else
                {
                    _toolTip.Hide(this);
                }
            }
        }
    }
}
