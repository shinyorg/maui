namespace Shiny;

/// <summary>
/// Tunes how inbound app links are turned into navigation. Every property is optional - the
/// defaults follow the route's own <c>registerRoute</c> declaration.
/// </summary>
public class AppLinkOptions
{
    /// <summary>
    /// Absolute route (eg. <c>"//main/home"</c>) a relative app link is pushed onto when the app
    /// is cold-started by the link. Defaults to null, meaning the link pushes onto whatever Shell
    /// resolved as its first item.
    /// </summary>
    public string? DefaultRoot { get; set; }

    /// <summary>
    /// Last word on the destination URI, overriding the <c>registerRoute</c> inference and
    /// <see cref="DefaultRoot"/>. Return an absolute (<c>//</c>-prefixed) or relative route.
    /// </summary>
    public Func<AppLinkMatch, string>? ResolveRoute { get; set; }

    /// <summary>
    /// Called when no template matched the inbound URI. Return true to report the link as
    /// handled. Defaults to null, which logs a warning and leaves the user where they are.
    /// </summary>
    public Func<Uri, Task<bool>>? OnUnhandled { get; set; }
}
