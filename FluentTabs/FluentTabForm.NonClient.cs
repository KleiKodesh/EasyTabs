using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FluentTabs
{
    /// <summary>Non-client handling: reclaiming the caption area, hit testing, and the drawn caption buttons.</summary>
    public partial class FluentTabForm
    {
        private int _hotCaptionButton;
        private int _pressedCaptionButton;

        private static int ResizeBorderHeight()
        {
            return NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSIZEFRAME) + NativeMethods.GetSystemMetrics(NativeMethods.SM_CXPADDEDBORDER);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case NativeMethods.WM_NCCALCSIZE:
                    if (m.WParam != IntPtr.Zero)
                    {
                        // Let the default handler compute the standard frame, then push the top edge
                        // back up so the caption becomes client space. Left/right/bottom resize borders
                        // stay untouched. When maximized the frame hangs off the monitor edge, so the
                        // resize border height goes back on top.
                        NativeMethods.NCCALCSIZE_PARAMS parameters =
                            (NativeMethods.NCCALCSIZE_PARAMS) Marshal.PtrToStructure(m.LParam, typeof(NativeMethods.NCCALCSIZE_PARAMS));
                        int originalTop = parameters.rgrc0.top;

                        base.WndProc(ref m);

                        parameters = (NativeMethods.NCCALCSIZE_PARAMS) Marshal.PtrToStructure(m.LParam, typeof(NativeMethods.NCCALCSIZE_PARAMS));
                        parameters.rgrc0.top = originalTop + (NativeMethods.IsZoomed(Handle) ? ResizeBorderHeight() : 0);
                        Marshal.StructureToPtr(parameters, m.LParam, false);

                        m.Result = IntPtr.Zero;
                        return;
                    }

                    break;

                case NativeMethods.WM_NCHITTEST:
                {
                    base.WndProc(ref m);

                    if ((int) m.Result == NativeMethods.HTCLIENT)
                    {
                        Point p = PointToClient(ScreenPointFromLParam(m.LParam));

                        if (!NativeMethods.IsZoomed(Handle) && p.Y < ResizeBorderHeight())
                        {
                            m.Result = new IntPtr(NativeMethods.HTTOP);
                        }
                        else if (p.Y < StripHeightPx)
                        {
                            if (CaptionButtonRect(NativeMethods.HTCLOSE).Contains(p))
                            {
                                m.Result = new IntPtr(NativeMethods.HTCLOSE);
                            }
                            else if (CaptionButtonRect(NativeMethods.HTMAXBUTTON).Contains(p))
                            {
                                // Returning HTMAXBUTTON is what makes the Windows 11 snap-layouts flyout appear
                                m.Result = new IntPtr(NativeMethods.HTMAXBUTTON);
                            }
                            else if (CaptionButtonRect(NativeMethods.HTMINBUTTON).Contains(p))
                            {
                                m.Result = new IntPtr(NativeMethods.HTMINBUTTON);
                            }
                            else if (HitTab(p) < 0 && !(ShowNewTabButton && NewTabButtonRect().Contains(p)))
                            {
                                // Empty strip drags the window and double-click maximizes, both natively
                                m.Result = new IntPtr(NativeMethods.HTCAPTION);
                            }
                        }
                    }

                    return;
                }

                case NativeMethods.WM_NCMOUSEMOVE:
                {
                    int hit = (int) m.WParam;
                    bool overButton = hit == NativeMethods.HTMINBUTTON || hit == NativeMethods.HTMAXBUTTON || hit == NativeMethods.HTCLOSE;

                    SetHotCaptionButton(overButton ? hit : 0);

                    if (overButton)
                    {
                        // Ask for WM_NCMOUSELEAVE so hover state clears when the cursor leaves the window
                        NativeMethods.TRACKMOUSEEVENT tme = new NativeMethods.TRACKMOUSEEVENT
                        {
                            cbSize = (uint) Marshal.SizeOf(typeof(NativeMethods.TRACKMOUSEEVENT)),
                            dwFlags = NativeMethods.TME_LEAVE | NativeMethods.TME_NONCLIENT,
                            hwndTrack = Handle
                        };
                        NativeMethods.TrackMouseEvent(ref tme);
                    }

                    break;
                }

                case NativeMethods.WM_NCMOUSELEAVE:
                    SetHotCaptionButton(0);

                    if (_pressedCaptionButton != 0)
                    {
                        _pressedCaptionButton = 0;
                        InvalidateStrip();
                    }

                    break;

                case NativeMethods.WM_NCLBUTTONDOWN:
                {
                    int hit = (int) m.WParam;

                    if (hit == NativeMethods.HTMINBUTTON || hit == NativeMethods.HTMAXBUTTON || hit == NativeMethods.HTCLOSE)
                    {
                        // Handled here so DefWindowProc doesn't paint classic-theme buttons over the strip
                        _pressedCaptionButton = hit;
                        InvalidateStrip();
                        m.Result = IntPtr.Zero;
                        return;
                    }

                    break;
                }

                case NativeMethods.WM_NCLBUTTONUP:
                {
                    int hit = (int) m.WParam;

                    if (_pressedCaptionButton != 0)
                    {
                        bool clicked = hit == _pressedCaptionButton;
                        _pressedCaptionButton = 0;
                        InvalidateStrip();

                        if (clicked)
                        {
                            switch (hit)
                            {
                                case NativeMethods.HTCLOSE:
                                    Close();
                                    break;

                                case NativeMethods.HTMINBUTTON:
                                    WindowState = FormWindowState.Minimized;
                                    break;

                                case NativeMethods.HTMAXBUTTON:
                                    WindowState = NativeMethods.IsZoomed(Handle) ? FormWindowState.Normal : FormWindowState.Maximized;
                                    break;
                            }
                        }

                        m.Result = IntPtr.Zero;
                        return;
                    }

                    break;
                }

                case NativeMethods.WM_NCRBUTTONUP:
                    if ((int) m.WParam == NativeMethods.HTCAPTION)
                    {
                        ShowSystemMenu(ScreenPointFromLParam(m.LParam));
                        m.Result = IntPtr.Zero;
                        return;
                    }

                    break;
            }

            base.WndProc(ref m);
        }

        private static Point ScreenPointFromLParam(IntPtr lParam)
        {
            long value = lParam.ToInt64();
            return new Point((short) (value & 0xFFFF), (short) ((value >> 16) & 0xFFFF));
        }

        private void ShowSystemMenu(Point screen)
        {
            IntPtr menu = NativeMethods.GetSystemMenu(Handle, false);
            int command = NativeMethods.TrackPopupMenuEx(menu, NativeMethods.TPM_RETURNCMD, screen.X, screen.Y, Handle, IntPtr.Zero);

            if (command != 0)
            {
                NativeMethods.SendMessage(Handle, NativeMethods.WM_SYSCOMMAND, new IntPtr(command), IntPtr.Zero);
            }
        }

        private void SetHotCaptionButton(int htCode)
        {
            if (_hotCaptionButton != htCode)
            {
                _hotCaptionButton = htCode;
                InvalidateStrip();
            }
        }
    }
}
