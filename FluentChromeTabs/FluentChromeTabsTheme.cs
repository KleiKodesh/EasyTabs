using System.Drawing;

namespace FluentChromeTabs
{
    /// <summary>Color theme for a <see cref="FluentChromeTabsForm" />.</summary>
    public enum FluentChromeTabsTheme
    {
        /// <summary>Follow the Windows apps light/dark setting, and react when it changes.</summary>
        Auto,

        /// <summary>Always light.</summary>
        Light,

        /// <summary>Always dark.</summary>
        Dark
    }

    /// <summary>Resolved color set for painting the tab strip. Modeled on the Windows 11 title bar palette.</summary>
    internal sealed class Palette
    {
        public Color Strip;
        public Color TabActive;
        public Color TabHover;
        public Color TextActive;
        public Color TextInactive;
        public Color Separator;
        public Color ButtonHover;
        public Color ButtonPressed;
        public Color CloseButtonHover;

        public static readonly Palette Light = new Palette
        {
            Strip = Color.FromArgb(243, 243, 243),
            TabActive = Color.White,
            TabHover = Color.FromArgb(234, 234, 234),
            TextActive = Color.FromArgb(26, 26, 26),
            TextInactive = Color.FromArgb(96, 96, 96),
            Separator = Color.FromArgb(208, 208, 208),
            ButtonHover = Color.FromArgb(230, 230, 230),
            ButtonPressed = Color.FromArgb(222, 222, 222),
            CloseButtonHover = Color.FromArgb(196, 43, 28)
        };

        public static readonly Palette Dark = new Palette
        {
            Strip = Color.FromArgb(32, 32, 32),
            TabActive = Color.FromArgb(45, 45, 45),
            TabHover = Color.FromArgb(41, 41, 41),
            TextActive = Color.FromArgb(245, 245, 245),
            TextInactive = Color.FromArgb(158, 158, 158),
            Separator = Color.FromArgb(63, 63, 63),
            ButtonHover = Color.FromArgb(45, 45, 45),
            ButtonPressed = Color.FromArgb(56, 56, 56),
            CloseButtonHover = Color.FromArgb(196, 43, 28)
        };

        /// <summary>Blends <paramref name="from" /> toward <paramref name="to" /> by <paramref name="amount" /> (0..1).</summary>
        public static Color Blend(Color from, Color to, double amount)
        {
            return Color.FromArgb(
                (int) (from.R + (to.R - from.R) * amount),
                (int) (from.G + (to.G - from.G) * amount),
                (int) (from.B + (to.B - from.B) * amount));
        }
    }
}
