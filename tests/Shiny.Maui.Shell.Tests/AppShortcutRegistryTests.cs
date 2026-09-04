using Shiny.Infrastructure;
using Shouldly;

namespace Shiny.Maui.Shell.Tests;

/// <summary>
/// Behavioural tests for shortcut registration. Activation itself goes through MAUI's AppActions
/// and cannot be unit tested here - see the plan's §9.6 note on MAUI statics in a plain test host.
/// </summary>
public class AppShortcutRegistryTests
{
    class HomeViewModel { public int Value { get; set; } }
    class SearchViewModel { }

    static RegisteredAppShortcut Shortcut(
        string id,
        int order = 0,
        bool registerRoute = true,
        Action<object>? configure = null
    ) => new(id, id, typeof(HomeViewModel), registerRoute, $"Title {id}", null, null, order, configure);


    [Fact]
    public void Find_ReturnsTheRegistration()
    {
        var registry = new AppShortcutRegistry();
        registry.Add(Shortcut("Home"));

        registry.Find("Home")!.Title.ShouldBe("Title Home");
    }


    [Fact]
    public void Find_IsCaseSensitive()
    {
        // The id round-trips through the platform verbatim, so matching must not be lenient
        var registry = new AppShortcutRegistry();
        registry.Add(Shortcut("Home"));

        registry.Find("home").ShouldBeNull();
    }


    [Fact]
    public void Find_UnknownId_ReturnsNull()
    {
        new AppShortcutRegistry().Find("nope").ShouldBeNull();
    }


    [Fact]
    public void Shortcuts_AreOrderedByOrderNotInsertion()
    {
        var registry = new AppShortcutRegistry();
        registry.Add(Shortcut("Third", order: 30));
        registry.Add(Shortcut("First", order: 10));
        registry.Add(Shortcut("Second", order: 20));

        registry.Shortcuts.Select(x => x.Id).ShouldBe(["First", "Second", "Third"]);
    }


    [Fact]
    public void Shortcuts_WithEqualOrder_FallBackToIdForDeterminism()
    {
        var registry = new AppShortcutRegistry();
        registry.Add(Shortcut("b"));
        registry.Add(Shortcut("a"));

        registry.Shortcuts.Select(x => x.Id).ShouldBe(["a", "b"]);
    }


    [Fact]
    public void Configure_PopulatesTheViewModel()
    {
        // The lambda is why a hand-registered shortcut can target a parameterised route: only the
        // id is persisted by the platform, the registration is rebuilt every launch.
        var registry = new AppShortcutRegistry();
        registry.Add(Shortcut("Product", configure: vm => ((HomeViewModel)vm).Value = 42));

        var target = new HomeViewModel();
        registry.Find("Product")!.Configure!.Invoke(target);

        target.Value.ShouldBe(42);
    }


    [Fact]
    public void PlatformMaximum_MatchesWhatBothPlatformsGuarantee()
    {
        AppShortcutRegistry.PlatformMaximum.ShouldBe(4);
    }
}
