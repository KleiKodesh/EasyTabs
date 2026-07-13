using System;
using System.Windows.Forms;

namespace FluentChromeTabs
{
    /// <summary>
    /// Keeps the application running while any <see cref="FluentChromeTabsForm" /> window is open, so that
    /// windows created by tearing tabs off survive the original window closing.
    /// <code>
    /// Application.Run(new FluentChromeTabsApplicationContext(new MyTabForm()));
    /// </code>
    /// </summary>
    public class FluentChromeTabsApplicationContext : ApplicationContext
    {
        public FluentChromeTabsApplicationContext(FluentChromeTabsForm initialWindow)
        {
            if (initialWindow == null)
            {
                throw new ArgumentNullException("initialWindow");
            }

            FluentChromeTabsForm.WindowClosed += OnWindowClosed;
            initialWindow.Show();
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            if (FluentChromeTabsForm.OpenWindowCount == 0)
            {
                FluentChromeTabsForm.WindowClosed -= OnWindowClosed;
                ExitThread();
            }
        }
    }
}
