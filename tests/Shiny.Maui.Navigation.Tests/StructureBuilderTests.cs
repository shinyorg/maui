using System.ComponentModel;
using Shouldly;

namespace Shiny.Maui.Navigation.Tests;

/// <summary>
/// The structure builder is pure logic - no UI, no platform - so unlike the Shell library's
/// URI routing it can be tested directly.
/// </summary>
public class StructureBuilderTests
{
    static ShinyNavigationBuilder CreateBuilder() => new(MauiApp.CreateBuilder());


    [Fact]
    public void SetRoot_DeclaresRootAndRegistersPage()
    {
        var builder = CreateBuilder().SetRoot<TestPage, RootViewModel>();
        var structure = builder.BuildStructure();

        structure.RootViewModelType.ShouldBe(typeof(RootViewModel));
        structure.HasTabs.ShouldBeFalse();
        structure.HasFlyout.ShouldBeFalse();
        builder.GetRegistration(typeof(RootViewModel))!.PageType.ShouldBe(typeof(TestPage));
    }


    [Fact]
    public void AddTabs_PreservesDeclarationOrder()
    {
        var structure = CreateBuilder()
            .AddTabs(t => t
                .Add<TestPage, FirstTabViewModel>("One")
                .Add<OtherPage, SecondTabViewModel>("Two", "two.png")
            )
            .BuildStructure();

        structure.HasTabs.ShouldBeTrue();
        structure.Tabs.Count.ShouldBe(2);
        structure.Tabs[0].ViewModelType.ShouldBe(typeof(FirstTabViewModel));
        structure.Tabs[0].Title.ShouldBe("One");
        structure.Tabs[1].ViewModelType.ShouldBe(typeof(SecondTabViewModel));
        structure.Tabs[1].Icon.ShouldBe("two.png");
    }


    [Fact]
    public void AddFlyout_WithTabs_DeclaresBoth()
    {
        var structure = CreateBuilder()
            .AddFlyout(f => f
                .Menu<TestPage, MenuViewModel>("Main Menu")
                .AddTabs(t => t.Add<OtherPage, FirstTabViewModel>("One"))
            )
            .BuildStructure();

        structure.HasFlyout.ShouldBeTrue();
        structure.FlyoutMenuViewModelType.ShouldBe(typeof(MenuViewModel));
        structure.FlyoutTitle.ShouldBe("Main Menu");
        structure.HasTabs.ShouldBeTrue();
        structure.CloseFlyoutOnNavigate.ShouldBeTrue();
    }


    [Fact]
    public void AddFlyout_WithoutMenu_Throws()
        => Should.Throw<InvalidOperationException>(
            () => CreateBuilder().AddFlyout(_ => { })
        );


    [Fact]
    public void BuildStructure_WithoutRootOrTabs_Throws()
        => Should.Throw<InvalidOperationException>(
            () => CreateBuilder().Add<TestPage, RootViewModel>().BuildStructure()
        );


    [Fact]
    public void GetRegistration_UnregisteredViewModel_ReturnsNull()
        => CreateBuilder()
            .SetRoot<TestPage, RootViewModel>()
            .GetRegistration(typeof(FirstTabViewModel))
            .ShouldBeNull();


    [Fact]
    public void GetViewModelTypeForPage_ResolvesTheReverseDirection()
    {
        var builder = CreateBuilder().SetRoot<TestPage, RootViewModel>();
        builder.GetViewModelTypeForPage(new TestPage()).ShouldBe(typeof(RootViewModel));
        builder.GetViewModelTypeForPage(new OtherPage()).ShouldBeNull();
    }


    [Fact]
    public void CloseOnNavigate_CanBeDisabled()
    {
        var structure = CreateBuilder()
            .AddFlyout(f => f
                .Menu<TestPage, MenuViewModel>()
                .CloseOnNavigate(false)
                .SetRoot<OtherPage, RootViewModel>()
            )
            .BuildStructure();

        structure.CloseFlyoutOnNavigate.ShouldBeFalse();
    }


    public class TestPage : ContentPage;
    public class OtherPage : ContentPage;

    public class RootViewModel : TestViewModel;
    public class MenuViewModel : TestViewModel;
    public class FirstTabViewModel : TestViewModel;
    public class SecondTabViewModel : TestViewModel;

    public abstract class TestViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
