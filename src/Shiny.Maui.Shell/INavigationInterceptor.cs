namespace Shiny;


/// <summary>
/// What an interceptor wants to happen to the navigation it was handed. The default instance
/// (every property left alone) lets the navigation proceed untouched.
/// </summary>
public class NavigationInterceptorResult
{
    /// <summary>
    /// Stops the navigation dead - the user stays where they are and nothing else in the chain
    /// runs. Takes precedence over <see cref="RedirectUri"/> if both are set.
    /// </summary>
    public bool CancelNavigation { get; set; }

    /// <summary>
    /// Sends the navigation somewhere else instead. The original destination is abandoned - its
    /// ViewModel is dropped, never bound to a page - and the whole interceptor chain runs again
    /// against the new URI, so a redirect is guarded exactly like the navigation that caused it.
    /// </summary>
    /// <remarks>
    /// A <c>//</c> prefix resets the Shell stack, a single leading <c>/</c> is treated the same
    /// way (<c>"/login"</c> and <c>"//login"</c> are equivalent), and anything else pushes.
    /// A redirect back to the URI already being navigated to is ignored rather than looping.
    /// </remarks>
    public string? RedirectUri { get; set; }

    /// <summary>
    /// Refactor-safe alternative to <see cref="RedirectUri"/> - the route mapped to this ViewModel
    /// type is resolved at redirect time. Ignored when <see cref="RedirectUri"/> is also set.
    /// </summary>
    public Type? RedirectViewModelType { get; set; }

    /// <summary>
    /// Whether a <see cref="RedirectViewModelType"/> redirect pushes (true) or resets the Shell
    /// stack (false, the default - which is what a guard sending the user to a login page wants).
    /// Has no effect on <see cref="RedirectUri"/>, which says so itself with its prefix.
    /// </summary>
    public bool RedirectRelative { get; set; }


    /// <summary>Lets the navigation continue.</summary>
    public static NavigationInterceptorResult Continue => new();

    /// <summary>Cancels the navigation.</summary>
    public static NavigationInterceptorResult Cancel() => new() { CancelNavigation = true };

    /// <summary>Redirects to the given URI - see <see cref="RedirectUri"/> for the prefix rules.</summary>
    public static NavigationInterceptorResult Redirect(string uri) => new() { RedirectUri = uri };

    /// <summary>
    /// Redirects to the route mapped to <typeparamref name="TViewModel"/>.
    /// </summary>
    /// <param name="relativeNavigation">True pushes the route, false (default) resets the stack to it.</param>
    public static NavigationInterceptorResult Redirect<TViewModel>(bool relativeNavigation = false) => new()
    {
        RedirectViewModelType = typeof(TViewModel),
        RedirectRelative = relativeNavigation
    };
}


/// <summary>
/// Runs before a navigation is handed to Shell, and can let it through, cancel it, or send it
/// somewhere else. Register as many as you like - they run in <see cref="Order"/> then
/// registration order, and the first one to cancel or redirect wins.
/// </summary>
/// <remarks>
/// Interceptors see every navigation the app makes: <see cref="INavigator"/> calls (route, typed,
/// builder, back), inbound app links and home screen shortcuts, and Shell-driven navigation the
/// user starts by tapping a tab or the back button. <c>ShowDialog</c> and <c>SwitchShell</c> are
/// not navigation and are not intercepted.
///
/// <para>
/// The <c>viewModel</c> argument is the <b>destination</b> ViewModel, resolved from DI and fully
/// populated (app link values applied, <c>configure</c> callback run) before the interceptor is
/// called, so it can be inspected and mutated - the instance handed to you is the one that gets
/// bound to the page. It is null when the destination route has no ViewModel mapping, and for
/// user-driven Shell navigation, where Shiny does not construct the destination. The page the user
/// is leaving is available from <see cref="INavigationContextAccessor"/>.
/// </para>
///
/// <para>
/// An exception thrown from an interceptor propagates to the caller and the navigation does not
/// happen - a guard that fails is never treated as a guard that passed.
/// </para>
/// </remarks>
public interface INavigationInterceptor
{
    /// <summary>
    /// Decides what happens to a navigation to <paramref name="uri"/>.
    /// </summary>
    /// <param name="uri">The destination Shell URI, exactly as it will be given to Shell.</param>
    /// <param name="viewModel">The destination ViewModel - see the remarks on <see cref="INavigationInterceptor"/>.</param>
    /// <param name="cancellationToken">
    /// The token passed to the navigation call. Cancelling it abandons the navigation with an
    /// <see cref="OperationCanceledException"/> - use it for the network call an auth guard makes,
    /// not for the decision itself, which is what <see cref="NavigationInterceptorResult.Cancel"/>
    /// is for.
    /// </param>
    Task<NavigationInterceptorResult> InterceptNavigationAsync(
        string uri,
        object? viewModel,
        CancellationToken cancellationToken
    );


    /// <summary>
    /// Lowest runs first; equal values keep registration order. Defaults to zero, so an
    /// interceptor that does not care about ordering does not have to say so - override it when a
    /// guard must run before or after another (an auth check before an audit log, say).
    /// </summary>
    int Order => 0;
}
