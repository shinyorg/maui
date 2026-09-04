using Shiny;
using Shiny.Infrastructure;
using Shouldly;

namespace Shiny.Maui.Shell.Tests;

/// <summary>
/// The push-vs-reset rule. This is inferred from how the route was declared rather than
/// configured, so these tests pin the inference itself.
/// </summary>
public class AppLinkRouteTests
{
    class ProductViewModel { }

    static (AppLinkMatch Match, RegisteredAppLink Link) Setup(bool registerRoute, string route = "Product")
    {
        var link = new RegisteredAppLink(
            "product/{id}",
            route,
            typeof(ProductViewModel),
            registerRoute,
            static (_, _) => true
        );
        var match = new AppLinkMatch(
            route,
            link.Template,
            typeof(ProductViewModel),
            new Dictionary<string, string>(),
            new Uri("myapp://product/1")
        );
        return (match, link);
    }


    [Fact]
    public void ShellContentRoute_ResetsTheStack()
    {
        // registerRoute: false means the page is a ShellContent in AppShell XAML - it cannot be pushed
        var (match, link) = Setup(registerRoute: false);

        AppLinkRoutes.Build(match, link, coldStart: false, new AppLinkOptions())
            .ShouldBe("//Product");
    }


    [Fact]
    public void RegisteredRoute_Pushes()
    {
        var (match, link) = Setup(registerRoute: true);

        AppLinkRoutes.Build(match, link, coldStart: false, new AppLinkOptions())
            .ShouldBe("Product");
    }


    [Fact]
    public void ColdStart_WithDefaultRoot_SuppliesTheBackStack()
    {
        var (match, link) = Setup(registerRoute: true);
        var options = new AppLinkOptions { DefaultRoot = "//main/home" };

        AppLinkRoutes.Build(match, link, coldStart: true, options)
            .ShouldBe("//main/home/Product");
    }


    [Fact]
    public void ColdStart_TrailingSlashOnDefaultRoot_DoesNotDouble()
    {
        var (match, link) = Setup(registerRoute: true);
        var options = new AppLinkOptions { DefaultRoot = "//main/home/" };

        AppLinkRoutes.Build(match, link, coldStart: true, options)
            .ShouldBe("//main/home/Product");
    }


    [Fact]
    public void WarmStart_IgnoresDefaultRoot()
    {
        // Already somewhere sensible - pushing relative keeps the user's back stack
        var (match, link) = Setup(registerRoute: true);
        var options = new AppLinkOptions { DefaultRoot = "//main/home" };

        AppLinkRoutes.Build(match, link, coldStart: false, options)
            .ShouldBe("Product");
    }


    [Fact]
    public void ColdStart_ShellContentRoute_IgnoresDefaultRoot()
    {
        var (match, link) = Setup(registerRoute: false);
        var options = new AppLinkOptions { DefaultRoot = "//main/home" };

        AppLinkRoutes.Build(match, link, coldStart: true, options)
            .ShouldBe("//Product");
    }


    [Fact]
    public void ResolveRoute_OverridesEverything()
    {
        var (match, link) = Setup(registerRoute: false);
        var options = new AppLinkOptions
        {
            DefaultRoot = "//main/home",
            ResolveRoute = _ => "//somewhere/else"
        };

        AppLinkRoutes.Build(match, link, coldStart: true, options)
            .ShouldBe("//somewhere/else");
    }
}

/// <summary>
/// App shortcuts share the push-vs-reset rule with app links, so a quick action lands the same way
/// a link to the same route would.
/// </summary>
public class AppShortcutRouteTests
{
    [Fact]
    public void ShellContentRoute_ResetsTheStack()
        => AppLinkRoutes.Build("Home", registerRoute: false, coldStart: true, defaultRoot: null)
            .ShouldBe("//Home");

    [Fact]
    public void RegisteredRoute_Pushes()
        => AppLinkRoutes.Build("Detail", registerRoute: true, coldStart: false, defaultRoot: null)
            .ShouldBe("Detail");

    [Fact]
    public void ColdStart_WithDefaultRoot_SuppliesTheBackStack()
        => AppLinkRoutes.Build("Detail", registerRoute: true, coldStart: true, defaultRoot: "//main/home")
            .ShouldBe("//main/home/Detail");

    [Fact]
    public void WarmStart_IgnoresDefaultRoot()
        => AppLinkRoutes.Build("Detail", registerRoute: true, coldStart: false, defaultRoot: "//main/home")
            .ShouldBe("Detail");
}
