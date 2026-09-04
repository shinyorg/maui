namespace Shiny.Infrastructure;

/// <summary>
/// Default text resolution - the declared strings, verbatim. Replaced with
/// <c>ShinyAppBuilder.UseShortcutText&lt;T&gt;()</c> when the app needs localized titles.
/// </summary>
public class DeclaredAppShortcutText : IAppShortcutText
{
    public string GetTitle(string route, string declared) => declared;
    public string? GetSubtitle(string route, string? declared) => declared;
}
