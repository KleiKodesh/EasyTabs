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

        /// <summary>Device-scale factor for glyph geometry (1.0 at 96 DPI).</summary>
        private float GlyphScale
        {
            get { return DeviceDpi / 96f; }
        }

        /// <summary>
        /// Thin round-capped pen matching the stroke weight of Segoe Fluent Icons. Glyphs are drawn as
        /// vector paths rather than font glyphs so the Fluent look works on any Windows version.
        /// </summary>
        private Pen GlyphPen(Color color)
        {
            Pen pen = new Pen(color, Math.Max(1f, 1.1f * GlyphScale));
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            return pen;
        }

        /// <summary>
        /// Draws an image so it stays readable in a mirrored (WS_EX_LAYOUTRTL) window: GDI flips all
        /// output in RTL layout, so the image is pre-flipped locally to cancel that out.
        /// </summary>
        private void DrawUnmirroredImage(Graphics g, Image image, Rectangle rect)
        {
            if (IsMirrored)
            {
                GraphicsState state = g.Save();
                g.TranslateTransform(rect.Left * 2 + rect.Width, 0);
                g.ScaleTransform(-1f, 1f);
                g.DrawImage(image, rect);
                g.Restore(state);
            }
            else
            {
                g.DrawImage(image, rect);
            }
        }

        private static GraphicsPath RoundedRectPathF(float x, float y, float w, float h, float r)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(x, y, r * 2, r * 2, 180, 90);
            path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (SolidBrush strip = new SolidBrush(_palette.Strip))
            {
                g.FillRectangle(strip, 0, 0, ClientSize.Width, StripHeightPx);
            }

            // Below the strip, paint the active-tab surface so content-less (loose mode) tabs and
            // any uncovered client area blend with the selected tab instead of showing strip color
            using (SolidBrush content = new SolidBrush(_palette.TabActive))
            {
                g.FillRectangle(content, 0, StripHeightPx, ClientSize.Width, Math.Max(0, ClientSize.Height - StripHeightPx));
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
                // Edge style: the tab is a floating rounded card; the active one gets a subtle outline
                using (GraphicsPath path = RoundedRectPath(rect, TabRadiusPx))
                {
                    using (SolidBrush brush = new SolidBrush(active ? _palette.TabActive : _palette.TabHover))
                    {
                        g.FillPath(brush, path);
                    }

                    // A soft outline on the active tab in light themes only; dark tabs are borderless
                    if (active && !_isDark)
                    {
                        using (Pen pen = new Pen(_palette.TabBorder))
                        {
                            g.DrawPath(pen, path);
                        }
                    }
                }
            }

            int textLeft = rect.Left + Dpi(10);

            if (tab.Icon != null)
            {
                int iconSize = Dpi(16);
                Rectangle iconRect = new Rectangle(textLeft, rect.Top + (rect.Height - iconSize) / 2, iconSize, iconSize);
                DrawUnmirroredImage(g, tab.Icon, iconRect);
                textLeft += iconSize + Dpi(6);
            }

            bool showClose = TabShowsClose(index);
            int textRight = showClose ? TabCloseRect(rect).Left - Dpi(4) : rect.Right - Dpi(8);

            TextFormatFlags textFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

            if (RightToLeft == RightToLeft.Yes)
            {
                // Correct bidi shaping and punctuation order for Hebrew/Arabic titles; in a mirrored
                // (RightToLeftLayout) window GDI flips the output, so "Left" renders at the visual right
                textFlags |= TextFormatFlags.RightToLeft;
            }

            Rectangle textRect = new Rectangle(textLeft, rect.Top, Math.Max(0, textRight - textLeft), rect.Height);
            TextRenderer.DrawText(g, tab.Title, Font, textRect, StripTextColor(active), textFlags);

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

                using (Pen pen = GlyphPen(StripTextColor(active)))
                {
                    float ccx = close.Left + close.Width / 2f;
                    float ccy = close.Top + close.Height / 2f;
                    float arm = 3.5f * GlyphScale;
                    g.DrawLine(pen, ccx - arm, ccy - arm, ccx + arm, ccy + arm);
                    g.DrawLine(pen, ccx - arm, ccy + arm, ccx + arm, ccy - arm);
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

            using (Pen pen = GlyphPen(StripTextColor(false)))
            {
                float cx = rect.Left + rect.Width / 2f;
                float cy = rect.Top + rect.Height / 2f;
                float arm = 5f * GlyphScale;
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

            // Fluent-style glyphs (Segoe Fluent Icons look) drawn as vector paths: thin anti-aliased
            // strokes with rounded corners, so they render on any Windows version without icon fonts
            Color glyphColor = isClose && (hot || pressed) ? Color.White : StripTextColor(true);
            float s = GlyphScale;
            float cx = rect.Left + rect.Width / 2f;
            float cy = rect.Top + rect.Height / 2f;

            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (Pen pen = GlyphPen(glyphColor))
            {
                switch (htCode)
                {
                    case NativeMethods.HTMINBUTTON:
                        g.DrawLine(pen, cx - 5f * s, cy, cx + 5f * s, cy);
                        break;

                    case NativeMethods.HTMAXBUTTON:
                        if (NativeMethods.IsZoomed(Handle))
                        {
                            // ChromeRestore: front rounded square plus the visible top-right edge of the back sheet
                            float gx = cx - 5f * s;
                            float gy = cy - 5f * s;
                            float front = 8f * s;
                            float corner = 1.6f * s;

                            using (GraphicsPath back = new GraphicsPath())
                            {
                                back.AddLine(gx + 3.5f * s, gy, gx + 10f * s - corner, gy);
                                back.AddArc(gx + 10f * s - 2f * corner, gy, 2f * corner, 2f * corner, 270, 90);
                                back.AddLine(gx + 10f * s, gy + corner, gx + 10f * s, gy + 6.5f * s);
                                g.DrawPath(pen, back);
                            }

                            using (GraphicsPath frontPath = RoundedRectPathF(gx, gy + 2f * s, front, front, corner))
                            {
                                g.DrawPath(pen, frontPath);
                            }
                        }
                        else
                        {
                            // ChromeMaximize: rounded-corner square
                            using (GraphicsPath box = RoundedRectPathF(cx - 4.5f * s, cy - 4.5f * s, 9f * s, 9f * s, 1.8f * s))
                            {
                                g.DrawPath(pen, box);
                            }
                        }

                        break;

                    case NativeMethods.HTCLOSE:
                        g.DrawLine(pen, cx - 4.5f * s, cy - 4.5f * s, cx + 4.5f * s, cy + 4.5f * s);
                        g.DrawLine(pen, cx - 4.5f * s, cy + 4.5f * s, cx + 4.5f * s, cy - 4.5f * s);
                        break;
                }
            }
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
