using System;
using System.Drawing;
using System.Windows.Forms;

namespace FluentChromeTabs
{
    /// <summary>
    /// Tab mouse interaction: selection, closing, drag-reorder within the strip, tearing a tab off
    /// into its own window, and dropping a tab onto another <see cref="FluentChromeTabsForm" />.
    /// </summary>
    public partial class FluentChromeTabsForm
    {
        private int _mouseDownTab = -1;
        private bool _mouseDownOnClose;
        private Point _mouseDownPoint;

        private bool _dragging;
        private int _dragIndex = -1;
        private int _dragOffsetX;
        private int _dragVisualX;
        private int _dragStartGroup;

        private bool _dividerDragging;

        /// <summary>How far the cursor may leave the strip vertically before the tab tears off, in logical pixels.</summary>
        private const int DetachThreshold = 36;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (!StripVisible || e.Y >= StripHeightPx)
            {
                return;
            }

            // The split divider is grabbed before anything else — it sits between regions
            if (e.Button == MouseButtons.Left && SplitStrip && DividerHitRect.Contains(e.Location))
            {
                _dividerDragging = true;
                Capture = true;
                InvalidateStrip();
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
                else
                {
                    for (int group = 0; group < GroupCount; group++)
                    {
                        if (ShowNewTabButton && NewTabButtonRect(group).Contains(e.Location))
                        {
                            RequestNewTab(group);
                            break;
                        }

                        if (ShowTabListButton && TabListButtonRect(group).Contains(e.Location))
                        {
                            ShowTabListMenu(group);
                            break;
                        }
                    }
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

            if (_dividerDragging)
            {
                // Live resize, mirrored to the host so the app's split view follows in tandem.
                // Dragging leaves pixel-pinned mode; the host re-pins from its next layout.
                _dividerOverrideLeft = -1;
                SplitRatio = (double) e.X / Math.Max(1, ClientSize.Width);
                SplitRatioChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

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

            if (_dividerDragging)
            {
                _dividerDragging = false;
                Capture = false;
                InvalidateStrip();
            }
            else if (_dragging)
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
            _dragStartGroup = GroupOf(_tabs[_dragIndex]);
            _dragOffsetX = _mouseDownPoint.X - TabRect(_dragIndex).Left;
            _dragVisualX = p.X - _dragOffsetX;

            ClearHoverState();
            InvalidateStrip();
        }

        private void ContinueDrag(Point p)
        {
            // Dropping onto another FluentChromeTabsForm's strip transfers the tab there
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

            // Split strip: crossing the divider moves the dragged tab into the other region.
            // Reassign its group live so it previews in place; the move is reported to the
            // host on drop (EndDrag). Everything here is in logical client coordinates, so it
            // works identically in a mirrored (RTL) window.
            if (_splitStrip)
            {
                int cursorGroup = p.X >= DividerXPx ? 1 : 0;

                if (cursorGroup != GroupOf(_tabs[_dragIndex]))
                {
                    _tabs[_dragIndex].Group = cursorGroup;

                    // The target region can size tabs differently — keep the grab point sane
                    _dragOffsetX = Math.Max(0, Math.Min(_dragOffsetX, TabWidthPx(cursorGroup) - Dpi(16)));
                }
            }

            // Reorder is confined to the dragged tab's own split region
            int group = GroupOf(_tabs[_dragIndex]);
            int count = GroupTabCount(group);
            int tabsLeft = GroupTabsLeftPx(group);
            int slot = TabWidthPx(group) + TabGapPx;
            int maxX = tabsLeft + (count - 1) * slot;

            _dragVisualX = Math.Max(tabsLeft, Math.Min(p.X - _dragOffsetX, maxX));

            int target = (int) Math.Round((double) (_dragVisualX - tabsLeft) / slot);
            target = Math.Max(0, Math.Min(target, count - 1));

            if (target != GroupPosition(_dragIndex))
            {
                FluentTab dragged = _tabs[_dragIndex];
                FluentTab selected = SelectedTab;
                int targetGlobal = GlobalIndexOfGroupPosition(group, target);

                _tabs.RemoveAt(_dragIndex);
                _tabs.Insert(Math.Min(targetGlobal, _tabs.Count), dragged);

                _selectedIndex = selected == null ? -1 : _tabs.IndexOf(selected);
                _dragIndex = _tabs.IndexOf(dragged);
            }

            InvalidateStrip();
        }

        private void EndDrag()
        {
            // Report a cross-region move to the host once, on drop. The tab's group was
            // already reassigned live during the drag; fire only when it truly changed.
            if (_dragIndex >= 0 && _dragIndex < _tabs.Count && _splitStrip)
            {
                FluentTab dropped = _tabs[_dragIndex];
                int group = GroupOf(dropped);

                if (group != _dragStartGroup)
                {
                    TabDraggedToGroup?.Invoke(this, new FluentTabGroupEventArgs(dropped, group));
                }
            }

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

            FluentChromeTabsForm newWindow = CreateDetachedWindow();
            newWindow.Theme = _theme;
            newWindow.CustomThemeColor = _customThemeColor;
            newWindow.StartPosition = FormStartPosition.Manual;
            newWindow.Size = NativeMethods.IsZoomed(Handle) ? RestoreBounds.Size : Size;
            newWindow.Location = new Point(screenCursor.X - dragOffsetX - newWindow.GroupTabsLeftPx(0), screenCursor.Y - newWindow.StripHeightPx / 2);
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
        protected virtual FluentChromeTabsForm CreateDetachedWindow()
        {
            try
            {
                FluentChromeTabsForm window = (FluentChromeTabsForm) Activator.CreateInstance(GetType());

                // A subclass constructor may have pre-populated tabs; the torn-off tab replaces them
                while (window._tabs.Count > 0)
                {
                    FluentTab preexisting = window._tabs[0];
                    window.DetachTabCore(preexisting);
                    preexisting.Content?.Dispose();
                }

                return window;
            }
            catch (MissingMethodException)
            {
                return new FluentChromeTabsForm { Text = Text };
            }
        }

        /// <summary>Transfers the dragged tab to another FluentChromeTabsForm when the cursor is over its strip.</summary>
        private bool TryTransferToOtherWindow()
        {
            Point screenCursor = Cursor.Position;

            NativeMethods.POINT nativePoint = new NativeMethods.POINT { x = screenCursor.X, y = screenCursor.Y };
            IntPtr underCursor = NativeMethods.GetAncestor(NativeMethods.WindowFromPoint(nativePoint), NativeMethods.GA_ROOT);

            if (underCursor == IntPtr.Zero || underCursor == Handle)
            {
                return false;
            }

            foreach (FluentChromeTabsForm candidate in OpenTabForms)
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

                int slot = candidate.TabWidthPx(0) + candidate.TabGapPx;
                int index = Math.Max(0, Math.Min((local.X - candidate.GroupTabsLeftPx(0) + slot / 2) / Math.Max(1, slot), candidate._tabs.Count));

                tab.Group = 0;
                candidate.AttachTab(index, tab);
                candidate.SelectTabCore(candidate._tabs.IndexOf(tab));
                candidate.Activate();

                // Continue the drag inside the target window
                candidate._dragging = true;
                candidate._dragIndex = candidate._tabs.IndexOf(tab);
                candidate._dragOffsetX = Math.Min(offset, candidate.TabWidthPx(0) - Dpi(16));
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
