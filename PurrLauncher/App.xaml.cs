using PurrLauncher.Services;

namespace PurrLauncher
{
    public partial class App : Application
    {
        private readonly IServiceProvider _services;

        public App(IServiceProvider services)
        {
            _services = services;
            InitializeComponent();

            // Apply the saved theme AFTER InitializeComponent so the brand-colour
            // dictionary is appended after Colors.xaml.  Later-wins resolution
            // means every newly created control picks up the correct values.
            var themeService = services.GetRequiredService<ThemeService>();
            themeService.InitializeTheme(this, services);
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(_services.GetRequiredService<AppShell>());
        }
    }
}