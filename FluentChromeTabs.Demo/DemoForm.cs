using System;
using System.Drawing;
using System.Windows.Forms;

namespace FluentChromeTabs.Demo
{
    public class DemoForm : FluentChromeTabsForm
    {
        public DemoForm()
        {
            Text = "WinForms Fluent Chrome Tabs — Demo";

            AddTab("Welcome", BuildWelcomeContent());
            AddTab("Editor", BuildEditorContent());
            AddTab("Pinned", BuildPinnedContent()).CanClose = false;
            SelectedIndex = 0;

            NewTabRequested += OnNewTabRequested;
            ThemeChanged += (sender, e) => RestyleAllTabs();
        }

        private void OnNewTabRequested(object sender, NewTabRequestedEventArgs e)
        {
            e.Content = BuildPlaceholderContent(e.Title);
        }

        private void RestyleAllTabs()
        {
            foreach (FluentTab tab in Tabs)
            {
                Restyle(tab.Content);
            }
        }

        private void Restyle(Control control)
        {
            control.BackColor = ContentBackColor;
            control.ForeColor = ContentForeColor;

            foreach (Control child in control.Controls)
            {
                if (child is TextBox)
                {
                    child.BackColor = ContentBackColor;
                    child.ForeColor = ContentForeColor;
                }
                else
                {
                    Restyle(child);
                }
            }
        }

        private Control BuildWelcomeContent()
        {
            Panel panel = new Panel { BackColor = ContentBackColor };

            Label title = new Label
            {
                Text = "Fluent Chrome Tabs",
                Font = new Font("Segoe UI Semibold", 22f),
                ForeColor = ContentForeColor,
                AutoSize = true,
                Location = new Point(40, 48)
            };

            Label body = new Label
            {
                Text =
                    "Native tabs in the title bar for WinForms — no overlay windows, no hacks.\n\n" +
                    "Things to try:\n" +
                    "    •  Drag tabs to reorder them\n" +
                    "    •  Drag a tab up or down out of the strip to tear it off into its own window\n" +
                    "    •  Drag a tab onto another window's tab strip to merge it back\n" +
                    "    •  Drag the empty strip to move the window; double-click it to maximize\n" +
                    "    •  Hover the maximize button for Windows 11 snap layouts\n" +
                    "    •  Right-click the strip for the system menu\n" +
                    "    •  Middle-click a tab to close it\n" +
                    "    •  Ctrl+T new tab, Ctrl+W close, Ctrl+Tab cycle, Ctrl+1..9 select\n" +
                    "    •  The pinned tab has no close button (CanClose = false)",
                Font = new Font("Segoe UI", 11f),
                ForeColor = ContentForeColor,
                AutoSize = true,
                Location = new Point(42, 104)
            };

            Button themeToggle = new Button
            {
                Text = "Toggle light / dark",
                Font = new Font("Segoe UI", 10f),
                Size = new Size(180, 36),
                Location = new Point(42, 380),
                FlatStyle = FlatStyle.Flat
            };
            themeToggle.Click += (sender, e) => Theme = IsDarkTheme ? FluentChromeTabsTheme.Light : FluentChromeTabsTheme.Dark;

            Button autoTheme = new Button
            {
                Text = "Follow Windows (Auto)",
                Font = new Font("Segoe UI", 10f),
                Size = new Size(180, 36),
                Location = new Point(236, 380),
                FlatStyle = FlatStyle.Flat
            };
            autoTheme.Click += (sender, e) => Theme = FluentChromeTabsTheme.Auto;

            Button looseMode = new Button
            {
                Text = "Open loose-mode window",
                Font = new Font("Segoe UI", 10f),
                Size = new Size(200, 36),
                Location = new Point(430, 380),
                FlatStyle = FlatStyle.Flat
            };
            looseMode.Click += (sender, e) => new LooseDemoForm { Theme = Theme }.Show();

            panel.Controls.Add(title);
            panel.Controls.Add(body);
            panel.Controls.Add(themeToggle);
            panel.Controls.Add(autoTheme);
            panel.Controls.Add(looseMode);

            return panel;
        }

        private Control BuildEditorContent()
        {
            Panel panel = new Panel { BackColor = ContentBackColor, Padding = new Padding(16) };

            TextBox editor = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 11f),
                BackColor = ContentBackColor,
                ForeColor = ContentForeColor,
                Text = "// A real, focusable control hosted in a tab.\r\n// Type here, then drag this tab out into its own window —\r\n// the content moves with it, state intact.\r\n"
            };

            panel.Controls.Add(editor);
            return panel;
        }

        private Control BuildPinnedContent()
        {
            return BuildPlaceholderContent("This tab is pinned: CanClose = false, so it has no close button\nand Ctrl+W won't close it.");
        }

        private Control BuildPlaceholderContent(string text)
        {
            return new Label
            {
                Text = text,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14f),
                BackColor = ContentBackColor,
                ForeColor = ContentForeColor
            };
        }
    }
}
