using System;
using System.Drawing;
using System.Windows.Forms;

namespace FluentChromeTabs
{
    /// <summary>A single tab hosted in a <see cref="FluentChromeTabsForm" />.</summary>
    public class FluentTab
    {
        private string _title;
        private Image _icon;
        private bool _canClose = true;

        /// <summary>
        /// Creates a content-less tab for loose usage: the tab is pure metadata, and the window's own
        /// content (a WebView, a UserControl, anything you place on the form) reacts to
        /// <see cref="FluentChromeTabsForm.SelectedTabChanged" />.
        /// </summary>
        /// <param name="title">Text shown on the tab.</param>
        public FluentTab(string title)
            : this(title, null)
        {
        }

        /// <summary>Creates a tab with a title and the control shown when the tab is selected.</summary>
        /// <param name="title">Text shown on the tab.</param>
        /// <param name="content">
        /// Control displayed below the tab strip while this tab is selected, or null for a content-less
        /// tab whose selection is handled entirely by your own code.
        /// </param>
        public FluentTab(string title, Control content)
        {
            _title = title ?? string.Empty;
            Content = content;
        }

        /// <summary>The window currently hosting this tab, or null when the tab is detached.</summary>
        public FluentChromeTabsForm Owner { get; internal set; }

        /// <summary>Control displayed while this tab is selected, or null for a content-less tab.</summary>
        public Control Content { get; }

        /// <summary>Text shown on the tab.</summary>
        public string Title
        {
            get { return _title; }
            set
            {
                _title = value ?? string.Empty;
                if (Owner != null)
                {
                    Owner.InvalidateStrip();
                }
            }
        }

        /// <summary>Optional 16x16 icon drawn at the left edge of the tab.</summary>
        public Image Icon
        {
            get { return _icon; }
            set
            {
                _icon = value;
                if (Owner != null)
                {
                    Owner.InvalidateStrip();
                }
            }
        }

        /// <summary>Whether the tab shows a close button and can be closed by the user. Defaults to true.</summary>
        public bool CanClose
        {
            get { return _canClose; }
            set
            {
                _canClose = value;
                if (Owner != null)
                {
                    Owner.InvalidateStrip();
                }
            }
        }

        /// <summary>
        /// Which strip region the tab lives in when the owner's <see cref="FluentChromeTabsForm.SplitStrip" />
        /// is enabled: 0 = primary (inline start), 1 = secondary. Ignored when the strip is not split.
        /// </summary>
        public int Group
        {
            get { return _group; }
            set
            {
                _group = value == 1 ? 1 : 0;
                if (Owner != null)
                {
                    Owner.InvalidateStrip();
                }
            }
        }

        private int _group;

        /// <summary>
        /// Draws the tab with the active-tab surface even when it is not the window's
        /// <see cref="FluentChromeTabsForm.SelectedTab" />. Used by split strips, where each
        /// region shows its own active tab.
        /// </summary>
        public bool Highlighted
        {
            get { return _highlighted; }
            set
            {
                _highlighted = value;
                if (Owner != null)
                {
                    Owner.InvalidateStrip();
                }
            }
        }

        private bool _highlighted;

        /// <summary>Arbitrary user data associated with the tab.</summary>
        public object Tag { get; set; }
    }

    /// <summary>Event arguments carrying the tab an event relates to.</summary>
    public class FluentTabEventArgs : EventArgs
    {
        public FluentTabEventArgs(FluentTab tab)
        {
            Tab = tab;
        }

        public FluentTab Tab { get; }
    }

    /// <summary>Cancelable event arguments raised before a tab closes.</summary>
    public class FluentTabClosingEventArgs : FluentTabEventArgs
    {
        public FluentTabClosingEventArgs(FluentTab tab)
            : base(tab)
        {
        }

        /// <summary>Set to true to keep the tab open.</summary>
        public bool Cancel { get; set; }
    }

    /// <summary>
    /// Raised when the user asks for a new tab (the "+" button or Ctrl+T). Set <see cref="Content" />
    /// (and optionally <see cref="Title" />) to control what the new tab shows; leave <see cref="Content" />
    /// null for a blank tab, or set <see cref="Cancel" /> to true to suppress the new tab entirely.
    /// </summary>
    public class NewTabRequestedEventArgs : EventArgs
    {
        public string Title { get; set; } = "New Tab";

        public Control Content { get; set; }

        public bool Cancel { get; set; }

        /// <summary>Which strip region's "+" was pressed (0 unless <see cref="FluentChromeTabsForm.SplitStrip" /> is on).</summary>
        public int Group { get; set; }
    }
}
