using System;
using System.Windows.Forms;

namespace FluentTabs
{
    /// <summary>
    /// Keeps the application running while any <see cref="FluentTabForm" /> window is open, so that
    /// windows created by tearing tabs off survive the original window closing.
    /// <code>
    /// Application.Run(new FluentTabsApplicationContext(new MyTabForm()));
    /// </code>
    /// </summary>
    public class FluentTabsApplicationContext : ApplicationContext
    {
        public FluentTabsApplicationContext(FluentTabForm initialWindow)
        {
            if (initialWindow == null)
            {
                throw new ArgumentNullException("initialWindow");
            }

            FluentTabForm.WindowClosed += OnWindowClosed;
            initialWindow.Show();
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            if (FluentTabForm.OpenWindowCount == 0)
            {
                FluentTabForm.WindowClosed -= OnWindowClosed;
                ExitThread();
            }
        }
    }
}
