namespace Shiny;

/// <summary>
/// Base class for your <c>App</c>. It hands MAUI the page tree that
/// <c>UseShinyNavigation(...)</c> built, so your app class has no navigation code in it at
/// all - no <c>new Window(new AppShell())</c>, no root page wiring.
/// </summary>
/// <example>
/// <code>
/// public partial class App : ShinyApplication
/// {
///     public App() => this.InitializeComponent();
/// }
/// </code>
/// </example>
public abstract class ShinyApplication : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var services = activationState?.Context?.Services
            ?? IPlatformApplication.Current?.Services
            ?? throw new InvalidOperationException("Unable to resolve MAUI services");

        var host = services.GetRequiredService<NavigationHost>();

        // Initialize normally built the tree already; build on demand for the hot-restart
        // case where the window is recreated after the host was torn down.
        var page = host.RootPage ?? host.BuildRoot();
        return new Window(page);
    }
}
