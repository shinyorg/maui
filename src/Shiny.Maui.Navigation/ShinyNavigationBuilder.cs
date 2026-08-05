using System.Diagnostics.CodeAnalysis;

namespace Shiny;


/// <summary>
/// Declares the app's page/viewmodel map and its navigation structure (root, tabs, flyout).
/// Everything is ViewModel-typed - there are no routes and no URIs.
/// </summary>
public sealed class ShinyNavigationBuilder(MauiAppBuilder builder) : IShinyBuilder
{
    public MauiAppBuilder MauiBuilder => builder;

    readonly Dictionary<Type, PageRegistration> registrations = new();
    readonly List<TabRegistration> tabs = new();

    Type? rootViewModelType;
    Type? flyoutMenuViewModelType;
    string flyoutTitle = "Menu";
    FlyoutLayoutBehavior flyoutBehavior = FlyoutLayoutBehavior.Popover;
    bool closeFlyoutOnNavigate = true;


    /// <summary>
    /// Maps a Page to its ViewModel. Both are registered as transient services and the
    /// ViewModel is assigned to the page's BindingContext on every navigation.
    /// Registering a page here makes it a valid target for
    /// <c>NavigateTo&lt;TViewModel&gt;</c> and <c>PushModal&lt;TViewModel&gt;</c>.
    /// </summary>
    public ShinyNavigationBuilder Add<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TViewModel
    >()
        where TPage : Page
        where TViewModel : class, INotifyPropertyChanged
    {
        this.registrations[typeof(TViewModel)] = new PageRegistration(typeof(TPage), typeof(TViewModel));
        return this;
    }


    /// <summary>
    /// Maps a Page/ViewModel pair and makes it the app's root page. The root is wrapped in a
    /// <see cref="NavigationPage"/> so it has a back stack. Ignored when <see cref="AddTabs"/>
    /// is also used - tabs supply their own roots.
    /// </summary>
    public ShinyNavigationBuilder SetRoot<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TViewModel
    >()
        where TPage : Page
        where TViewModel : class, INotifyPropertyChanged
    {
        this.Add<TPage, TViewModel>();
        this.rootViewModelType = typeof(TViewModel);
        return this;
    }


    /// <summary>
    /// Declares the app's tabs. Each tab gets its own independent navigation stack.
    /// </summary>
    public ShinyNavigationBuilder AddTabs(Action<TabsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure.Invoke(new TabsBuilder(this));
        return this;
    }


    /// <summary>
    /// Declares a <see cref="FlyoutPage"/> at the root of the app, with the menu page supplied
    /// inside the configure callback. The detail side is whatever you declare via
    /// <see cref="AddTabs"/> or <see cref="SetRoot{TPage,TViewModel}"/>.
    /// </summary>
    public ShinyNavigationBuilder AddFlyout(Action<FlyoutBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure.Invoke(new FlyoutBuilder(this));

        if (this.flyoutMenuViewModelType == null)
            throw new InvalidOperationException("AddFlyout requires a menu page - call Menu<TPage, TViewModel>() inside the configure callback");

        return this;
    }


    /// <summary>
    /// Sets the dialog provider you want to use. Defaults to the native platform
    /// alert/prompt/action sheet when not called.
    /// </summary>
    public ShinyNavigationBuilder UseDialogs<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDialog
    >() where TDialog : class, IDialogs
    {
        builder.Services.AddSingleton<IDialogs, TDialog>();
        return this;
    }


    void IShinyBuilder.UseDialogs<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDialog
    >() => this.UseDialogs<TDialog>();


    /// <summary>Gets the Page/ViewModel registration for a viewmodel type</summary>
    public PageRegistration? GetRegistration(Type viewModelType)
        => this.registrations.GetValueOrDefault(viewModelType);


    /// <summary>Gets the ViewModel type mapped to a given page instance</summary>
    public Type? GetViewModelTypeForPage(Page page)
    {
        var pageType = page.GetType();
        foreach (var pair in this.registrations)
        {
            if (pair.Value.PageType == pageType)
                return pair.Value.ViewModelType;
        }
        return null;
    }


    /// <summary>The declared navigation structure. Built once, at the end of configuration.</summary>
    public NavigationStructure BuildStructure()
    {
        if (this.tabs.Count == 0 && this.rootViewModelType == null)
            throw new InvalidOperationException("No navigation root declared - call SetRoot<TPage, TViewModel>() or AddTabs(...)");

        return new NavigationStructure(
            this.rootViewModelType,
            this.tabs.ToList(),
            this.flyoutMenuViewModelType,
            this.flyoutTitle,
            this.flyoutBehavior,
            this.closeFlyoutOnNavigate
        );
    }


    internal void RegisterDependencies()
    {
        foreach (var pair in this.registrations)
        {
            builder.Services.AddTransient(pair.Value.PageType);
            builder.Services.AddTransient(pair.Value.ViewModelType);
        }
    }


    internal void AddTab(TabRegistration tab) => this.tabs.Add(tab);

    internal void SetFlyoutMenu(Type viewModelType) => this.flyoutMenuViewModelType = viewModelType;

    internal void SetFlyoutTitle(string title) => this.flyoutTitle = title;

    internal void SetFlyoutBehavior(FlyoutLayoutBehavior behavior) => this.flyoutBehavior = behavior;

    internal void SetCloseFlyoutOnNavigate(bool value) => this.closeFlyoutOnNavigate = value;
}


/// <summary>
/// Declares the app's tabs. Tab order here is tab order on screen.
/// </summary>
public sealed class TabsBuilder(ShinyNavigationBuilder parent)
{
    /// <summary>
    /// Adds a tab hosting the given Page/ViewModel pair.
    /// </summary>
    /// <param name="title">Tab title. Falls back to the page's own Title when null</param>
    /// <param name="icon">Optional icon file name from Resources/Images</param>
    /// <param name="wrapInNavigationPage">
    /// When true (default) the tab gets its own <see cref="NavigationPage"/> back stack
    /// </param>
    public TabsBuilder Add<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TViewModel
    >(string? title = null, string? icon = null, bool wrapInNavigationPage = true)
        where TPage : Page
        where TViewModel : class, INotifyPropertyChanged
    {
        parent.Add<TPage, TViewModel>();
        parent.AddTab(new TabRegistration(typeof(TViewModel), title, icon, wrapInNavigationPage));
        return this;
    }
}


/// <summary>
/// Declares the flyout (drawer) menu page and its behavior.
/// </summary>
public sealed class FlyoutBuilder(ShinyNavigationBuilder parent)
{
    /// <summary>
    /// Sets the page rendered in the flyout drawer.
    /// </summary>
    /// <param name="title">The flyout title - required by MAUI on some platforms. Defaults to "Menu"</param>
    public FlyoutBuilder Menu<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TViewModel
    >(string title = "Menu")
        where TPage : Page
        where TViewModel : class, INotifyPropertyChanged
    {
        parent.Add<TPage, TViewModel>();
        parent.SetFlyoutMenu(typeof(TViewModel));
        parent.SetFlyoutTitle(title);
        return this;
    }


    /// <summary>Sets how the flyout lays out against the detail page. Defaults to Popover.</summary>
    public FlyoutBuilder Behavior(FlyoutLayoutBehavior behavior)
    {
        parent.SetFlyoutBehavior(behavior);
        return this;
    }


    /// <summary>
    /// Whether an open flyout closes automatically when a navigation completes. Defaults to true -
    /// this is what you want for the usual "tap a menu item, go somewhere" flow.
    /// </summary>
    public FlyoutBuilder CloseOnNavigate(bool closeOnNavigate = true)
    {
        parent.SetCloseFlyoutOnNavigate(closeOnNavigate);
        return this;
    }


    /// <summary>Declares the tabs shown on the detail side of the flyout.</summary>
    public FlyoutBuilder AddTabs(Action<TabsBuilder> configure)
    {
        parent.AddTabs(configure);
        return this;
    }


    /// <summary>Declares a single root page on the detail side of the flyout.</summary>
    public FlyoutBuilder SetRoot<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TViewModel
    >()
        where TPage : Page
        where TViewModel : class, INotifyPropertyChanged
    {
        parent.SetRoot<TPage, TViewModel>();
        return this;
    }
}
