using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FluentChromeTabs
{
    /// <summary>
    /// A Fluent-styled tooltip that matches the tab strip theme instead of the dated yellow
    /// WinForms/OS default. It is a borderless, non-activating top-level window positioned at
    /// explicit screen coordinates — so it never steals focus, never appears in Alt-Tab, and
    /// (because callers pass screen points computed via RectangleToScreen) is placed correctly
    /// in both LTR and RTL/mirrored windows.
    /// </summary>
    internal sealed class ThemedToolTip : Form
    {
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int CS_DROPSHADOW = 0x00020000;

        private readonly float _scale;
        private Palette _palette;
        private bool _rtl;
        private string _text = string.Empty;

        public ThemedToolTip(Font font, float scale)
        {
            _scale = scale;
            Font = font;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Enabled = false; // purely visual — never takes input

            SetStyle(
                ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint,
                true);
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int pref = NativeMethods.DWMWCP_ROUNDSMALL;
            NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
        }

        private int S(int logical)
        {
            return (int) Math.Round(logical * _scale);
        }

        /// <summary>
        /// Shows <paramref name="text" /> anchored so its top edge is at <paramref name="anchorTopScreen" />
        /// and it is inline-aligned to <paramref name="anchorStartScreenX" /> (the item's start edge in
        /// screen pixels; the reading direction is honored). Clamped to the working area.
        /// </summary>
        public void ShowText(string text, Palette palette, bool rtl, int anchorStartScreenX, int anchorTopScreen)
        {
            _palette = palette;
            _rtl = rtl;
            _text = text ?? string.Empty;

            Size textSize = TextRenderer.MeasureText(_text, Font);
            int w = textSize.Width + S(16);
            int h = textSize.Height + S(8);
            ClientSize = new Size(w, h);

            // In RTL the start edge is the right edge, so the box extends left from the anchor
            int left = _rtl ? anchorStartScreenX - w : anchorStartScreenX;

            Rectangle work = Screen.FromPoint(new Point(anchorStartScreenX, anchorTopScreen)).WorkingArea;
            left = Math.Max(work.Left, Math.Min(left, work.Right - w));
            int top = Math.Min(anchorTopScreen, work.Bottom - h);

            Location = new Point(left, top);

            if (!Visible)
            {
                Show();
            }
            else
            {
                Invalidate();
            }
        }

        public void HideTip()
        {
            if (Visible)
            {
                Hide();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (SolidBrush bg = new SolidBrush(_palette.TabActive))
            {
                g.FillRectangle(bg, ClientRectangle);
            }

            using (Pen pen = new Pen(_palette.TabBorder))
            {
                g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            }

            TextFormatFlags flags =
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                (_rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left);

            Rectangle textRect = new Rectangle(S(8), 0, ClientSize.Width - S(16), ClientSize.Height);
            TextRenderer.DrawText(g, _text, Font, textRect, _palette.TextActive, flags);
        }
    }
}
