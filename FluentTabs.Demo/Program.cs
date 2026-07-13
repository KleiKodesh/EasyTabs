using System;
using System.Windows.Forms;

namespace FluentTabs.Demo
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // The context keeps the app alive while any tab window is open,
            // including windows created by tearing tabs off
            Application.Run(new FluentTabsApplicationContext(new DemoForm()));
        }
    }
}
