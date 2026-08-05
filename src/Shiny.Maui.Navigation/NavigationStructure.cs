namespace Shiny;

/// <summary>
/// A Page &lt;=&gt; ViewModel pair registered with <see cref="ShinyNavigationBuilder"/>.
/// </summary>
/// <param name="PageType">The page type - resolved from DI on every navigation</param>
/// <param name="ViewModelType">The viewmodel type - resolved from DI and assigned to the page's BindingContext</param>
public record PageRegistration(Type PageType, Type ViewModelType);


/// <summary>
/// A single tab in the app's <see cref="TabbedPage"/>.
/// </summary>
/// <param name="ViewModelType">The viewmodel hosted as the tab's root page</param>
/// <param name="Title">The tab title. Falls back to the page's own Title when null</param>
/// <param name="Icon">Optional icon file name from your Resources/Images folder</param>
/// <param name="WrapInNavigationPage">
/// When true (default) the tab root is wrapped in a <see cref="NavigationPage"/> so the tab
/// gets its own independent back stack. Set false for a tab that can never push.
/// </param>
public record TabRegistration(
    Type ViewModelType,
    string? Title = null,
    string? Icon = null,
    bool WrapInNavigationPage = true
);


/// <summary>
/// The declared shape of the app's navigation tree. Built by <see cref="ShinyNavigationBuilder"/>
/// and materialised into MAUI pages by the navigation host at startup.
/// </summary>
public sealed record NavigationStructure(
    Type? RootViewModelType,
    IReadOnlyList<TabRegistration> Tabs,
    Type? FlyoutMenuViewModelType,
    string FlyoutTitle,
    FlyoutLayoutBehavior FlyoutBehavior,
    bool CloseFlyoutOnNavigate
)
{
    /// <summary>True when the app declares a <see cref="FlyoutPage"/> at its root</summary>
    public bool HasFlyout => this.FlyoutMenuViewModelType != null;

    /// <summary>True when the app declares a <see cref="TabbedPage"/></summary>
    public bool HasTabs => this.Tabs.Count > 0;
}
