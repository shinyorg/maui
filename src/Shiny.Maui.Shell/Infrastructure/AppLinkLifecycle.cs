namespace Shiny.Infrastructure;

/// <summary>
/// Installs the platform delivery points for inbound app links so the consuming app does not have
/// to touch its AppDelegate, MainActivity or Application class.
/// </summary>
public static partial class AppLinkLifecycle
{
    public static partial void Register(MauiAppBuilder builder);


    /// <summary>
    /// Shared entry point for every platform hook. Resolves <see cref="IAppLinks"/> late because
    /// the hooks fire during platform startup, potentially before the MAUI app is fully built.
    /// </summary>
    internal static bool Dispatch(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var services = IPlatformApplication.Current?.Services;
        var appLinks = services?.GetService<IAppLinks>();
        if (appLinks == null)
            return false;

        // Decide the return value synchronously - the platform needs an answer now, while the
        // navigation itself is allowed to complete on its own schedule.
        var handled = appLinks.TryResolve(uri, out _);
        _ = appLinks.Handle(uri);
        return handled;
    }
}
