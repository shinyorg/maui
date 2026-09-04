namespace Shiny.Infrastructure;


/// <summary>
/// Decides the Shell URI an inbound app link navigates to. Pure logic, kept out of
/// <see cref="AppLinkRouter"/> so the push-vs-reset rule can be tested on its own.
/// </summary>
public static class AppLinkRoutes
{
    /// <summary>
    /// Whether a link pushes or resets the stack is inferred from how the route was declared -
    /// it is never configured, because the answer is already in the ShellMap attribute.
    /// </summary>
    /// <param name="match">The resolved link.</param>
    /// <param name="link">The template that matched.</param>
    /// <param name="coldStart">True when the app was launched by this link.</param>
    /// <param name="options">Overrides - see <see cref="AppLinkOptions"/>.</param>
    public static string Build(AppLinkMatch match, RegisteredAppLink link, bool coldStart, AppLinkOptions options)
        => options.ResolveRoute?.Invoke(match)
           ?? Build(match.Route, link.RegisterRoute, coldStart, options.DefaultRoot);


    /// <summary>
    /// The rule itself, over the two facts it actually depends on. App shortcuts share it, so a
    /// quick action lands the same way a link to the same route would.
    /// </summary>
    /// <param name="route">The Shell route.</param>
    /// <param name="registerRoute">False when the route is a ShellContent declared in AppShell XAML.</param>
    /// <param name="coldStart">True when the app was launched by this activation.</param>
    /// <param name="defaultRoot">Absolute route supplying the back stack for a cold-start push.</param>
    public static string Build(string route, bool registerRoute, bool coldStart, string? defaultRoot)
    {
        // A ShellContent declared in AppShell XAML is a Shell item and cannot be pushed - the only
        // correct navigation is an absolute one that selects it.
        if (!registerRoute)
            return "//" + route;

        // A registered detail route pushes. On cold start there is nothing meaningful underneath
        // it, so DefaultRoot (when set) supplies the back stack in the same navigation.
        if (coldStart && !string.IsNullOrWhiteSpace(defaultRoot))
            return defaultRoot!.TrimEnd('/') + "/" + route;

        return route;
    }
}
