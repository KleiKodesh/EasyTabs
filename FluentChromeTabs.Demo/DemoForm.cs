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
            AddTab("Colors", BuildColorsContent());
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
                if (tab.Content != null)
                {
                    Restyle(tab.Content);
                }
            }
        }

        private void Restyle(Control control)
        {
            control.BackColor = ContentBackColor;
            control.ForeColor = ContentForeColor;

            foreach (Control child in control.Controls)
            {
                if (Equals(child.Tag, "swatch"))
                {
                    // Color swatches keep their own background
                }
                else if (child is TextBox)
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

        private Control BuildColorsContent()
        {
            Panel panel = new Panel { BackColor = ContentBackColor };

            Label heading = new Label
            {
                Text = "Appearance",
                Font = new Font("Segoe UI Semibold", 18f),
                ForeColor = ContentForeColor,
                AutoSize = true,
                Location = new Point(40, 40)
            };

            Label themeLabel = new Label
            {
                Text = "Theme mode:",
                Font = new Font("Segoe UI", 11f),
                ForeColor = ContentForeColor,
                AutoSize = true,
                Location = new Point(42, 92)
            };

            Button light = MakeButton("Light", new Point(42, 120));
            light.Click += (sender, e) =>
            {
                CustomThemeColor = null;
                Theme = FluentChromeTabsTheme.Light;
            };

            Button dark = MakeButton("Dark", new Point(160, 120));
            dark.Click += (sender, e) =>
            {
                CustomThemeColor = null;
                Theme = FluentChromeTabsTheme.Dark;
            };

            Button auto = MakeButton("Auto", new Point(278, 120));
            auto.Click += (sender, e) =>
            {
                CustomThemeColor = null;
                Theme = FluentChromeTabsTheme.Auto;
            };

            Label customLabel = new Label
            {
                Text = "Custom chrome color (CustomThemeColor) — click a swatch:",
                Font = new Font("Segoe UI", 11f),
                ForeColor = ContentForeColor,
                AutoSize = true,
                Location = new Point(42, 192)
            };

            object[][] swatches =
            {
                new object[] { "Midnight", Color.FromArgb(24, 34, 58) },
                new object[] { "Forest", Color.FromArgb(28, 48, 34) },
                new object[] { "Wine", Color.FromArgb(64, 26, 38) },
                new object[] { "Plum", Color.FromArgb(46, 30, 62) },
                new object[] { "Teal", Color.FromArgb(14, 58, 58) },
                new object[] { "Sand", Color.FromArgb(238, 227, 206) },
                new object[] { "Sky", Color.FromArgb(214, 230, 244) },
                new object[] { "Rose", Color.FromArgb(246, 219, 226) }
            };

            for (int i = 0; i < swatches.Length; i++)
            {
                string name = (string) swatches[i][0];
                Color color = (Color) swatches[i][1];

                Button swatch = new Button
                {
                    Text = name,
                    Tag = "swatch",
                    Font = new Font("Segoe UI", 9f),
                    Size = new Size(104, 40),
                    Location = new Point(42 + (i % 4) * 116, 232 + (i / 4) * 52),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = color,
                    ForeColor = Palette_IsDark(color) ? Color.White : Color.Black
                };
                swatch.FlatAppearance.BorderColor = Color.Gray;
                swatch.Click += (sender, e) => CustomThemeColor = color;
                panel.Controls.Add(swatch);
            }

            Button pick = MakeButton("Pick a color…", new Point(42, 352));
            pick.Size = new Size(200, 36);
            pick.Click += (sender, e) =>
            {
                using (ColorDialog dialog = new ColorDialog { FullOpen = true, Color = CustomThemeColor ?? ContentBackColor })
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        CustomThemeColor = dialog.Color;
                    }
                }
            };

            panel.Controls.Add(heading);
            panel.Controls.Add(themeLabel);
            panel.Controls.Add(light);
            panel.Controls.Add(dark);
            panel.Controls.Add(auto);
            panel.Controls.Add(customLabel);
            panel.Controls.Add(pick);

            return panel;
        }

        private static bool Palette_IsDark(Color color)
        {
            return 0.299 * color.R + 0.587 * color.G + 0.114 * color.B < 128;
        }

        private Button MakeButton(string text, Point location)
        {
            return new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 10f),
                Size = new Size(110, 36),
                Location = location,
                FlatStyle = FlatStyle.Flat
            };
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
