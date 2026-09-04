namespace Shiny;

/// <summary>
/// Handles inbound deep links (custom schemes and universal/app links) by matching them against
/// the app link templates declared on <see cref="ShellMapAttribute{TPage}"/> and navigating.
/// </summary>
/// <remarks>
/// <see cref="ShinyAppBuilder.UseAppLinks"/> wires the platform delivery points automatically, so
/// most apps never call this directly. It stays public for the case where a platform hook cannot
/// be reached through MAUI's lifecycle events and has to be forwarded by hand.
/// </remarks>
public interface IAppLinks
{
    /// <summary>
    /// Resolves the URI to a route and navigates to it.
    /// </summary>
    /// <param name="uri">The inbound URI.</param>
    /// <returns>
    /// True if a template matched and navigation was issued (or the link was queued for a Shell
    /// that has not started yet). False if nothing matched and no fallback handled it.
    /// </returns>
    Task<bool> Handle(Uri uri);

    /// <summary>
    /// Resolves the URI to a route without navigating. Useful for testing and for callers that
    /// want to inspect a link before acting on it.
    /// </summary>
    bool TryResolve(Uri uri, out AppLinkMatch match);
}


/// <summary>
/// A resolved app link - the route it targets and the values pulled out of the URI.
/// </summary>
/// <param name="Route">The Shell route from <see cref="ShellMapAttribute{TPage}"/>.</param>
/// <param name="Template">The template that matched.</param>
/// <param name="ViewModelType">The ViewModel mapped to the route.</param>
/// <param name="Values">Path tokens and query values, keyed case-insensitively.</param>
/// <param name="Uri">The URI that produced this match.</param>
public record AppLinkMatch(
    string Route,
    string Template,
    Type ViewModelType,
    IReadOnlyDictionary<string, string> Values,
    Uri Uri
);
