using System;
using System.Drawing;
using System.Windows.Forms;

namespace FluentTabs
{
    /// <summary>
    /// Tab mouse interaction: selection, closing, drag-reorder within the strip, tearing a tab off
    /// into its own window, and dropping a tab onto another <see cref="FluentTabForm" />.
    /// </summary>
    public partial class FluentTabForm
    {
        private int _mouseDownTab = -1;
        private bool _mouseDownOnClose;
        private Point _mouseDownPoint;

        private bool _dragging;
        private int _dragIndex = -1;
        private int _dragOffsetX;
        private int _dragVisualX;

        /// <summary>How far the cursor may leave the strip vertically before the tab tears off, in logical pixels.</summary>
        private const int DetachThreshold = 36;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Y >= StripHeightPx)
            {
                return;
            }

            int tab = HitTab(e.Location);

            if (e.Button == MouseButtons.Left)
            {
                if (tab >= 0)
                {
                    _mouseDownOnClose = TabShowsClose(tab) && TabCloseRect(TabRect(tab)).Contains(e.Location);
                    _mouseDownTab = tab;
                    _mouseDownPoint = e.Location;

                    if (!_mouseDownOnClose)
                    {
                        SelectTabCore(tab);
                    }
                }
                else if (ShowNewTabButton && NewTabButtonRect().Contains(e.Location))
                {
                    RequestNewTab();
                }
            }
            else if (e.Button == MouseButtons.Middle && tab >= 0 && _tabs[tab].CanClose)
            {
                CloseTab(_tabs[tab]);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            SetHotCaptionButton(0);

            if (_dragging)
            {
                ContinueDrag(e.Location);
                return;
            }

            if (_mouseDownTab >= 0 && !_mouseDownOnClose && e.Button == MouseButtons.Left)
            {
                int dx = Math.Abs(e.X - _mouseDownPoint.X);
                int dy = Math.Abs(e.Y - _mouseDownPoint.Y);

                if (dx > SystemInformation.DragSize.Width || dy > SystemInformation.DragSize.Height)
                {
                    BeginDrag(e.Location);
                    return;
                }
            }

            UpdateHoverState(e.Location);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (_dragging)
            {
                EndDrag();
            }
            else if (_mouseDownTab >= 0 && _mouseDownOnClose && e.Button == MouseButtons.Left)
            {
                int tab = HitTab(e.Location);

                if (tab == _mouseDownTab && TabCloseRect(TabRect(tab)).Contains(e.Location))
                {
                    CloseTab(_tabs[tab]);
                }
            }

            _mouseDownTab = -1;
            _mouseDownOnClose = false;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            ClearHoverState();
        }

        private void BeginDrag(Point p)
        {
            // Dragging the only tab of a window drags the whole window, like Chrome
            if (_tabs.Count == 1)
            {
                _mouseDownTab = -1;
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(Handle, NativeMethods.WM_NCLBUTTONDOWN, new IntPtr(NativeMethods.HTCAPTION), IntPtr.Zero);
                return;
            }

            _dragging = true;
            _dragIndex = _mouseDownTab;
            _dragOffsetX = _mouseDownPoint.X - TabRect(_dragIndex).Left;
            _dragVisualX = p.X - _dragOffsetX;

            ClearHoverState();
            InvalidateStrip();
        }

        private void ContinueDrag(Point p)
        {
            // Dropping onto another FluentTabForm's strip transfers the tab there
            if (AllowTabDetach && TryTransferToOtherWindow())
            {
                return;
            }

            // Leaving the strip vertically tears the tab off into its own window
            if (AllowTabDetach && (p.Y < -Dpi(DetachThreshold) || p.Y > StripHeightPx + Dpi(DetachThreshold)))
            {
                DetachDraggedTab();
                return;
            }

            int maxX = TabsLeftPx + (_tabs.Count - 1) * (TabWidthPx + TabGapPx);
            _dragVisualX = Math.Max(TabsLeftPx, Math.Min(p.X - _dragOffsetX, maxX));

            int slot = TabWidthPx + TabGapPx;
            int target = (int) Math.Round((double) (_dragVisualX - TabsLeftPx) / slot);
            target = Math.Max(0, Math.Min(target, _tabs.Count - 1));

            if (target != _dragIndex)
            {
                FluentTab tab = _tabs[_dragIndex];
                _tabs.RemoveAt(_dragIndex);
                _tabs.Insert(target, tab);

                if (_selectedIndex == _dragIndex)
                {
                    _selectedIndex = target;
                }
                else if (_dragIndex < _selectedIndex && target >= _selectedIndex)
                {
                    _selectedIndex--;
                }
                else if (_dragIndex > _selectedIndex && target <= _selectedIndex)
                {
                    _selectedIndex++;
                }

                _dragIndex = target;
            }

            InvalidateStrip();
        }

        private void EndDrag()
        {
            _dragging = false;
            _dragIndex = -1;
            InvalidateStrip();
        }

        /// <summary>Tears the dragged tab off into a new window and hands the drag over to the native window move.</summary>
        private void DetachDraggedTab()
        {
            FluentTab tab = _tabs[_dragIndex];
            Point screenCursor = Cursor.Position;
            int dragOffsetX = _dragOffsetX;

            EndDrag();
            Capture = false;
            _mouseDownTab = -1;

            DetachTabCore(tab);

            FluentTabForm newWindow = CreateDetachedWindow();
            newWindow.Theme = _theme;
            newWindow.StartPosition = FormStartPosition.Manual;
            newWindow.Size = NativeMethods.IsZoomed(Handle) ? RestoreBounds.Size : Size;
            newWindow.Location = new Point(screenCursor.X - dragOffsetX - newWindow.TabsLeftPx, screenCursor.Y - newWindow.StripHeightPx / 2);
            newWindow.AddTab(tab);
            newWindow.Show();
            newWindow.Activate();

            // Hand off to a native window move so the user keeps dragging the new window seamlessly
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(newWindow.Handle, NativeMethods.WM_NCLBUTTONDOWN, new IntPtr(NativeMethods.HTCAPTION), IntPtr.Zero);
        }

        /// <summary>
        /// Creates the window used when a tab is torn off. Override to customize; the default creates
        /// a new instance of the current type when it has a parameterless constructor.
        /// </summary>
        protected virtual FluentTabForm CreateDetachedWindow()
        {
            try
            {
                FluentTabForm window = (FluentTabForm) Activator.CreateInstance(GetType());

                // A subclass constructor may have pre-populated tabs; the torn-off tab replaces them
                while (window._tabs.Count > 0)
                {
                    FluentTab preexisting = window._tabs[0];
                    window.DetachTabCore(preexisting);
                    preexisting.Content.Dispose();
                }

                return window;
            }
            catch (MissingMethodException)
            {
                return new FluentTabForm { Text = Text };
            }
        }

        /// <summary>Transfers the dragged tab to another FluentTabForm when the cursor is over its strip.</summary>
        private bool TryTransferToOtherWindow()
        {
            Point screenCursor = Cursor.Position;

            NativeMethods.POINT nativePoint = new NativeMethods.POINT { x = screenCursor.X, y = screenCursor.Y };
            IntPtr underCursor = NativeMethods.GetAncestor(NativeMethods.WindowFromPoint(nativePoint), NativeMethods.GA_ROOT);

            if (underCursor == IntPtr.Zero || underCursor == Handle)
            {
                return false;
            }

            foreach (FluentTabForm candidate in OpenTabForms)
            {
                if (candidate == this || !candidate.Visible || candidate.WindowState == FormWindowState.Minimized || candidate.Handle != underCursor)
                {
                    continue;
                }

                Point local = candidate.PointToClient(screenCursor);

                if (local.Y < 0 || local.Y >= candidate.StripHeightPx || local.X < 0 || local.X >= candidate.ClientSize.Width)
                {
                    return false;
                }

                FluentTab tab = _tabs[_dragIndex];
                int offset = _dragOffsetX;

                EndDrag();
                Capture = false;
                _mouseDownTab = -1;

                DetachTabCore(tab);

                int slot = candidate.TabWidthPx + candidate.TabGapPx;
                int index = Math.Max(0, Math.Min((local.X - candidate.TabsLeftPx + slot / 2) / Math.Max(1, slot), candidate._tabs.Count));

                candidate.AttachTab(index, tab);
                candidate.SelectTabCore(candidate._tabs.IndexOf(tab));
                candidate.Activate();

                // Continue the drag inside the target window
                candidate._dragging = true;
                candidate._dragIndex = candidate._tabs.IndexOf(tab);
                candidate._dragOffsetX = Math.Min(offset, candidate.TabWidthPx - Dpi(16));
                candidate._dragVisualX = local.X - candidate._dragOffsetX;
                candidate.Capture = true;
                candidate.InvalidateStrip();

                if (_tabs.Count == 0 && ExitOnLastTabClose)
                {
                    BeginInvoke(new Action(Close));
                }

                return true;
            }

            return false;
        }
    }
}
