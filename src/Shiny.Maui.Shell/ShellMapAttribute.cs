namespace Shiny;

/// <summary>
/// This attribute is used to map a page to a route for Shell navigation. It can be applied to any class, but is typically used on ViewModel classes to associate them with their corresponding pages. The route can be specified explicitly or will default to the name of the page type. If the page is already registered in the AppShell xaml, set registerRoute to false to prevent conflicts.
/// </summary>
/// <param name="route">An optional route name (must be named like a C# class) or the page type name is used which can cause conflicted names</param>
/// <param name="registerRoute">Set this to false if you have the page specified in your AppShell xaml to prevent issues</param>
/// <param name="description">The source generator uses this to create AI compatible methods</param>
/// <param name="appLinks">
/// Optional inbound URL templates that navigate to this route - eg. <c>"product/{id}"</c>. A
/// <c>{token}</c> path segment binds to the <see cref="ShellPropertyAttribute"/> property of the
/// same name (case-insensitive); query string values bind by property name too. Any configured
/// scheme or domain serves any template, so adding a domain later needs no change here.
/// Whether the link pushes or resets the stack is inferred from <paramref name="registerRoute"/>.
/// </param>
/// <typeparam name="TPage"></typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ShellMapAttribute<TPage>(
    string? route = null,
    bool registerRoute = true,
    string? description = null,
    string[]? appLinks = null
) : Attribute
{
    public string Route => route ?? typeof(TPage).Name;
    public bool RegisterRoute => registerRoute;
    public string[] AppLinks => appLinks ?? [];

    /// <summary>
    /// Title of a home screen quick action (iOS UIApplicationShortcutItem, Android app shortcut)
    /// that navigates to this route. Setting this is what declares the shortcut - the other
    /// Shortcut* properties are optional refinements and mean nothing on their own.
    /// </summary>
    /// <remarks>
    /// A route with a required <see cref="ShellPropertyAttribute"/> cannot declare a shortcut this
    /// way, because an attribute cannot supply a runtime value - register it with
    /// <c>ShinyAppBuilder.AddAppShortcut&lt;TViewModel&gt;(configure: ...)</c> instead.
    /// </remarks>
    public string? Shortcut { get; set; }

    /// <summary>Secondary line shown under the title. iOS only; most Android launchers ignore it.</summary>
    public string? ShortcutSubtitle { get; set; }

    /// <summary>Platform icon name - a system icon on iOS, a drawable resource on Android.</summary>
    public string? ShortcutIcon { get; set; }

    /// <summary>Display order. Explicit because source order across files is not stable.</summary>
    public int ShortcutOrder { get; set; }
}
