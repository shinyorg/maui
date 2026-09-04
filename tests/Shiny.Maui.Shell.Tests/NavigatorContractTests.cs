using Shouldly;

namespace Shiny.Maui.Shell.Tests;

/// <summary>
/// Source-level regression tests for the post-GoToAsync direct-BindingContext-read
/// refactor in <c>ShellNavigator.NavigateTo&lt;TViewModel&gt;</c> and
/// <c>NavigationBuilder.Navigate</c>.
///
/// Background
/// ----------
/// Both methods previously subscribed to a static <c>ShinyRouteFactory.PageResolved</c>
/// event to detect when the destination page had been resolved by DI. That event was
/// only raised by <c>ShinyRouteFactory.GetOrCreate</c>, which is only invoked when MAUI
/// Shell instantiates a page through a registered <c>RouteFactory</c>. Pages declared
/// in an AppShell.xaml as <c>&lt;ShellContent ContentTemplate="{DataTemplate ...}"&gt;</c>
/// (paired with <c>[ShellMap&lt;TPage&gt;(registerRoute: false)]</c>) are constructed via
/// the DataTemplate, never go through the factory, and therefore never raised
/// <c>PageResolved</c>.
///
/// The bug
/// -------
/// <c>NavigateTo&lt;TVM&gt;(relativeNavigation: false)</c> targeting a ShellContent route
/// would await a <c>TaskCompletionSource</c> that never completed, leaking the
/// subscribed handler. A later unrelated navigation through a registered route would
/// raise <c>PageResolved</c> with a different page, wake the leaked handler, and throw
/// <c>InvalidOperationException("Page BindingContext is not of type '&lt;original VM&gt;'")</c>.
/// On iOS the exception surfaced asynchronously through
/// <c>NSAsyncSynchronizationContextDispatcher</c> against a stale call site, making the
/// crash look like it originated from a later, unrelated navigation.
///
/// The fix
/// -------
/// <c>NavigateTo&lt;TViewModel&gt;</c> reads <c>Shell.Current.CurrentPage.BindingContext</c>
/// directly after <c>Shell.GoToAsync</c> returns. <c>NavigationBuilder.Navigate</c>
/// walks <c>Shell.Current.Navigation.NavigationStack</c> and applies each segment's
/// configure callback by index. The static <c>PageResolved</c> event is gone.
///
/// Runtime coverage gap
/// --------------------
/// A true end-to-end test of ShellContent-declared route navigation requires a MAUI
/// test host (<c>Shell.Current</c>, <c>IPlatformApplication</c>, handlers). The current
/// test project only references the source generator and runs pure-.NET unit tests, so
/// the lock-in here is at the source-contract level. If a MAUI device-test project is
/// added in the future, port these regressions into a runtime navigation scenario.
/// </summary>
public class NavigatorContractTests
{
    static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Directory.Build.props")))
            dir = Path.GetDirectoryName(dir);

        if (dir == null)
            throw new InvalidOperationException(
                $"Could not locate repo root (no Directory.Build.props found above '{AppContext.BaseDirectory}')."
            );

        return dir;
    }

    static string ReadInfrastructureSource(string fileName)
    {
        var path = Path.Combine(FindRepoRoot(), "src", "Shiny.Maui.Shell", "Infrastructure", fileName);
        File.Exists(path).ShouldBeTrue($"Expected source file at '{path}'.");
        return File.ReadAllText(path);
    }

    [Fact]
    public void ShinyRouteFactory_DoesNotDeclarePageResolvedEvent()
    {
        var source = ReadInfrastructureSource("ShinyRouteFactory.cs");

        source.ShouldNotContain("PageResolved",
            customMessage:
            "ShinyRouteFactory.PageResolved was removed because it never fired for ShellContent-declared " +
            "pages and leaked subscribers across navigations. Reintroducing it reopens the " +
            "'Page BindingContext is not of type X' crash for any caller doing " +
            "NavigateTo<TVM>(relativeNavigation: false) into a XAML-declared ShellContent route."
        );
    }

    [Fact]
    public void ShellNavigator_DoesNotSubscribeToPageResolved()
    {
        var source = ReadInfrastructureSource("ShellNavigator.cs");

        source.ShouldNotContain("ShinyRouteFactory.PageResolved +=",
            customMessage:
            "NavigateTo<TViewModel> must not subscribe to a static page-resolved event. " +
            "That subscription leaked when the target was a ShellContent route (the event never fired) " +
            "and the leaked handler later threw against an unrelated page from a different navigation."
        );
        source.ShouldNotContain("ShinyRouteFactory.PageResolved -=",
            customMessage:
            "The PageResolved unsubscribe was removed alongside the subscribe in the refactor."
        );
    }

    [Fact]
    public void ShellNavigator_ReadsCurrentPageBindingContextAfterGoToAsync()
    {
        var source = ReadInfrastructureSource("ShellNavigator.cs");

        source.ShouldContain("Shell.Current.CurrentPage",
            customMessage:
            "NavigateTo<TViewModel> must read Shell.Current.CurrentPage directly after Shell.GoToAsync " +
            "returns. This is the replacement for the broken static-event flow and is the only path that " +
            "works uniformly for both registered routes and ShellContent-declared routes."
        );
    }

    [Fact]
    public void NavigationBuilder_DoesNotSubscribeToPageResolved()
    {
        var source = ReadInfrastructureSource("NavigationBuilder.cs");

        source.ShouldNotContain("ShinyRouteFactory.PageResolved +=",
            customMessage:
            "NavigationBuilder.Navigate must not subscribe to a static page-resolved event. " +
            "The static event is shared across all in-flight navigations and causes handler crosstalk " +
            "whenever two builders or a builder + NavigateTo<TVM> overlap."
        );
        source.ShouldNotContain("ShinyRouteFactory.PageResolved -=",
            customMessage:
            "The PageResolved unsubscribe was removed alongside the subscribe in the refactor."
        );
    }

    [Fact]
    public void NavigationBuilder_PinsResolvedViewModelsToConfigurator()
    {
        var source = ReadInfrastructureSource("NavigationBuilder.cs");

        source.ShouldContain("configurator.EnqueueResolved",
            customMessage:
            "NavigationBuilder.Navigate must pre-resolve each typed segment's viewmodel, apply its configure " +
            "callback synchronously, and pin the instance on ShellNavigationConfigurator via EnqueueResolved. " +
            "The apply sites (ShinyRouteFactory.GetOrCreate, ShinyShell.OnNavigated, AppOnPageAppearing) consume " +
            "the pinned instances when Shell realises each segment's page. This replaces the v6.1 stack-walk " +
            "approach which raced against Shell's PageAppearing scheduling on Android."
        );
    }

    [Fact]
    public void NavigationBuilder_RollsBackPinnedEntriesOnlyOnFailure()
    {
        var source = ReadInfrastructureSource("NavigationBuilder.cs");

        source.ShouldNotContain("finally",
            customMessage:
            "NavigationBuilder must not unconditionally dispose pinned subscriptions in a finally block — " +
            "on Android the apply sites fire after Navigate returns, so disposing on success would cause " +
            "fallback DI resolves and lose configured viewmodels. Dispose only inside a catch block."
        );
    }
}
