using PurrLauncher.Pages;

namespace PurrLauncher
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Register detail page route for Shell navigation
            Routing.RegisterRoute(nameof(PackageDetailPage), typeof(PackageDetailPage));
        }
    }
}
