namespace Shiny;


/// <summary>
/// Everything about the navigation in flight that does not fit in
/// <see cref="INavigationInterceptor.InterceptNavigationAsync"/>'s two arguments - most usefully
/// the page being left, which is what an unsaved-changes guard needs.
/// </summary>
/// <param name="FromUri">Shell's current location, or null when there is no Shell yet (cold start).</param>
/// <param name="FromViewModel">The ViewModel of the page being left, when it has one.</param>
/// <param name="ToUri">The destination being evaluated - the same URI handed to the interceptor.</param>
/// <param name="NavigationType">How the destination is reached.</param>
/// <param name="Parameters">The navigation arguments, as passed to <see cref="INavigator"/>.</param>
/// <param name="RedirectCount">0 for the original destination, 1+ when this pass follows a redirect.</param>
public record NavigationContext(
    string? FromUri,
    object? FromViewModel,
    string ToUri,
    NavigationType NavigationType,
    IReadOnlyDictionary<string, object> Parameters,
    int RedirectCount
)
{
    /// <summary>
    /// Which way through the stack this navigation goes - the question most guards actually ask
    /// ("is the user leaving this page forwards or backwards?").
    /// </summary>
    public NavigationDirection Direction => this.NavigationType.GetDirection();
}


/// <summary>
/// Gives an <see cref="INavigationInterceptor"/> the context of the navigation it is being asked
/// about. Inject it like <c>IHttpContextAccessor</c>; <see cref="Current"/> is null outside of an
/// interceptor call.
/// </summary>
public interface INavigationContextAccessor
{
    /// <summary>The navigation currently being intercepted, or null when none is.</summary>
    NavigationContext? Current { get; }
}
