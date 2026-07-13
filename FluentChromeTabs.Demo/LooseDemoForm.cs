using System;
using System.Drawing;
using System.Windows.Forms;

namespace FluentChromeTabs.Demo
{
    /// <summary>
    /// Loose mode: the tabs are pure metadata (no per-tab Content). One static control fills the
    /// window — in a real app a WebView2 or a stateful UserControl — and reacts to
    /// <see cref="FluentChromeTabsForm.SelectedTabChanged" />.
    /// </summary>
    public class LooseDemoForm : FluentChromeTabsForm
    {
        private readonly Label _stage;

        public LooseDemoForm()
        {
            Text = "Loose mode — tab strip only";
            ClientSize = new Size(780, 440);

            // The one static content control, added directly to the form; it sits below the strip
            // because the form's Padding reserves the strip area
            _stage = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12f),
                BackColor = ContentBackColor,
                ForeColor = ContentForeColor
            };
            Controls.Add(_stage);

            SelectedTabChanged += OnTabChanged;
            ThemeChanged += (sender, e) =>
            {
                _stage.BackColor = ContentBackColor;
                _stage.ForeColor = ContentForeColor;
            };

            // Content-less tabs: just titles. Closing, reordering, tear-off all still work.
            AddTab("Alpha");
            AddTab("Beta");
            AddTab("Gamma");
            SelectedIndex = 0;
        }

        private void OnTabChanged(object sender, FluentTabEventArgs e)
        {
            int visits = (e.Tab.Tag as int? ?? 0) + 1;
            e.Tab.Tag = visits;

            _stage.Text =
                "One static content area shared by every tab.\n\n" +
                "Selected tab: “" + e.Tab.Title + "”   (selected " + visits + "× — state lives in Tab.Tag)\n\n" +
                "In a real app this is where you would navigate a WebView2\n" +
                "or switch the state of a single UserControl.";
        }
    }
}
