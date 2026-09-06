using Shouldly;

namespace Shiny.Maui.Shell.Tests;

public class NavigationDirectionTests
{
    [Theory]
    [InlineData(NavigationType.Push, NavigationDirection.Forward)]
    [InlineData(NavigationType.GoBack, NavigationDirection.Back)]
    [InlineData(NavigationType.PopToRoot, NavigationDirection.Back)]
    [InlineData(NavigationType.SetRoot, NavigationDirection.Root)]
    [InlineData(NavigationType.SwitchShell, NavigationDirection.Root)]
    public void GetDirection(NavigationType type, NavigationDirection expected)
        => type.GetDirection().ShouldBe(expected);


    [Fact]
    public void EventArgs_ExposeTheDirection()
    {
        var args = new NavigationEventArgs("from", null, "..", NavigationType.GoBack, new Dictionary<string, object>());
        args.Direction.ShouldBe(NavigationDirection.Back);

        var navigated = new NavigatedEventArgs("//home", null, NavigationType.SetRoot, new Dictionary<string, object>());
        navigated.Direction.ShouldBe(NavigationDirection.Root);
    }
}
