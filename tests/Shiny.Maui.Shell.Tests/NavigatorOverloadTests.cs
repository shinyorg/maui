using Shouldly;

namespace Shiny.Maui.Shell.Tests;

/// <summary>
/// Overload resolution lock for <see cref="INavigator"/>. Adding the interceptor controls to
/// methods that end in a <c>params</c> array is exactly where C# starts producing ambiguities at
/// the call site rather than at the declaration - so every call shape the library documents is
/// written out here. The compiler is the assertion; the body never runs.
/// </summary>
public class NavigatorOverloadTests
{
    static async Task EveryDocumentedCallShape(INavigator navigator)
    {
        await navigator.NavigateTo("detail");
        await navigator.NavigateTo("detail", false);
        await navigator.NavigateTo("detail", args: [("Id", 42)]);
        await navigator.NavigateTo("detail", bypassInterceptors: true);
        await navigator.NavigateTo("detail", cancellationToken: CancellationToken.None);
        await navigator.NavigateTo("detail", true, true, CancellationToken.None, ("Id", 42));

        await navigator.NavigateTo<object>();
        await navigator.NavigateTo<object>(x => x.ToString());
        await navigator.NavigateTo<object>(relativeNavigation: false);
        await navigator.NavigateTo<object>(bypassInterceptors: true);
        await navigator.NavigateTo<object>(args: [("Id", 42)]);

        await navigator.GoBack();
        await navigator.GoBack(("Result", "ok"));
        await navigator.GoBack(2);
        await navigator.GoBack(2, ("Result", "ok"));
        await navigator.GoBack(2, true);
        await navigator.GoBack(2, true, CancellationToken.None, ("Result", "ok"));

        await navigator.PopToRoot();
        await navigator.PopToRoot(("Result", "ok"));
        await navigator.PopToRoot(true);
        await navigator.PopToRoot(true, CancellationToken.None, ("Result", "ok"));

        await navigator.CreateBuilder().Add("detail").Navigate();
        await navigator.CreateBuilder().Add("detail").Navigate(bypassInterceptors: true);
        await navigator.CreateBuilder().Add("detail").BypassInterceptors().Navigate();
        await navigator.CreateBuilder().BypassInterceptors().PopBack(2).Add("detail").Navigate();
    }


    [Fact]
    public void EveryCallShapeCompiles()
        => ((Delegate)EveryDocumentedCallShape).ShouldNotBeNull();
}
