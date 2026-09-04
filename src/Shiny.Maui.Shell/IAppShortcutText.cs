namespace Shiny;

/// <summary>
/// Resolves the text a home screen quick action displays. Register an implementation to localize
/// shortcut titles and subtitles - the strings declared on <see cref="ShellMapAttribute{TPage}"/>
/// are attribute literals and cannot be translated on their own.
/// </summary>
/// <remarks>
/// Resolution happens at install time, not compile time, so <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>
/// is already known. Applies to shortcuts from both the source generator and
/// <c>ShinyAppBuilder.AddAppShortcut</c>, since both land in the same registry before install.
///
/// A shortcut installed at startup keeps its text until it is pushed again, so call
/// <see cref="IAppShortcuts.Refresh"/> after a language change.
/// </remarks>
public interface IAppShortcutText
{
    /// <summary>Text for the quick action's title.</summary>
    /// <param name="route">The Shell route the shortcut opens.</param>
    /// <param name="declared">The string declared on the attribute or passed to AddAppShortcut.</param>
    string GetTitle(string route, string declared);

    /// <summary>Text for the quick action's second line. Null in, null out is expected.</summary>
    string? GetSubtitle(string route, string? declared);
}


/// <summary>
/// Controls the installed set of home screen quick actions.
/// </summary>
public interface IAppShortcuts
{
    /// <summary>
    /// Re-resolves every shortcut's text through <see cref="IAppShortcutText"/> and pushes the set
    /// to the platform again. Call this after the app's language changes - installed shortcuts keep
    /// the text they were given until they are pushed again.
    /// </summary>
    Task Refresh();
}
