using PurrLauncher.Pages;

namespace PurrLauncher
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Guard against duplicate registration when the shell is recreated
            // after a theme switch.
            try
            {
                Routing.RegisterRoute(nameof(PackageDetailPage), typeof(PackageDetailPage));
            }
            catch (ArgumentException)
            {
                // Already registered — safe to ignore.
            }
        }
    }
}
