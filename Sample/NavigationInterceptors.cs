using Microsoft.Extensions.Logging;
using Shiny;

namespace Sample;


/// <summary>
/// Flipped from the Push &amp; Pop page so the interceptors can be seen doing all three things
/// without restarting the app.
/// </summary>
public class NavigationGuardSwitch
{
    /// <summary>Cancels navigation to the detail page - as an unsaved-changes guard would.</summary>
    public bool BlockDetail { get; set; }

    /// <summary>Sends the detail page somewhere else - as an auth guard would.</summary>
    public bool RedirectDetail { get; set; }
}


/// <summary>
/// The cross-cutting case: every navigation in the app, whatever started it, through one method.
/// </summary>
public class LoggingNavigationInterceptor(
    ILogger<LoggingNavigationInterceptor> logger,
    INavigationContextAccessor context
) : INavigationInterceptor
{
    // Runs after the guard below, so the log line reflects what the guard let through.
    public int Order => 100;

    public Task<NavigationInterceptorResult> InterceptNavigationAsync(
        string uri,
        object? viewModel,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "[Interceptor] {From} -> {To} ({Direction}/{Type}) destination VM: {VM}",
            context.Current?.FromUri,
            uri,
            context.Current?.Direction,
            context.Current?.NavigationType,
            viewModel?.GetType().Name ?? "(none)"
        );
        return Task.FromResult(NavigationInterceptorResult.Continue);
    }
}


/// <summary>
/// The guard case. Note what it does with the ViewModel: it is the real instance about to be bound
/// to the page, already carrying the arguments the caller passed, so a guard can decide on the
/// destination's own state rather than on the URI alone.
/// </summary>
public class DetailGuardNavigationInterceptor(
    NavigationGuardSwitch guards,
    IDialogs dialogs
) : INavigationInterceptor
{
    // Guards run before anything that only observes.
    public int Order => -100;

    public async Task<NavigationInterceptorResult> InterceptNavigationAsync(
        string uri,
        object? viewModel,
        CancellationToken cancellationToken
    )
    {
        if (viewModel is not DetailViewModel detail)
            return NavigationInterceptorResult.Continue;

        if (guards.BlockDetail)
        {
            await dialogs.Alert("Blocked", $"An interceptor cancelled navigation to '{uri}'");
            return NavigationInterceptorResult.Cancel();
        }

        if (guards.RedirectDetail)
        {
            // Refactor-safe: the route comes from the ViewModel map, not a string. The detail
            // ViewModel handed to us here is dropped - its page is never built.
            return NavigationInterceptorResult.Redirect<LifecycleDemoViewModel>(relativeNavigation: true);
        }

        // Nothing to veto. Note that `detail` is the instance the page will be bound to, so a
        // guard can also fix up the destination instead of blocking it - though anything the
        // caller passed as a navigation argument is applied by Shell afterwards and wins.
        return NavigationInterceptorResult.Continue;
    }
}
