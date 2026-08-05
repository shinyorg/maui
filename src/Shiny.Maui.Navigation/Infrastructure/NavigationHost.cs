using Microsoft.Extensions.Logging;

namespace Shiny.Navigation.Infrastructure;

/// <summary>
/// Owns the MAUI page tree - it builds the FlyoutPage/TabbedPage/NavigationPage structure
/// declared on <see cref="ShinyNavigationBuilder"/> and answers the one question the
/// navigator keeps asking: "which navigation stack is active right now?"
/// </summary>
public sealed class NavigationHost(
    ILogger<NavigationHost> logger,
    ShinyNavigationBuilder builder,
    IServiceProvider services
)
{
    /// <summary>The structure declared at startup</summary>
    public NavigationStructure Structure { get; } = builder.BuildStructure();

    /// <summary>The page assigned to the window</summary>
    public Page? RootPage { get; private set; }

    /// <summary>The root FlyoutPage, when one was declared</summary>
    public FlyoutPage? Flyout { get; private set; }

    /// <summary>The TabbedPage, when tabs were declared</summary>
    public TabbedPage? Tabs { get; private set; }


    /// <summary>
    /// Resolves the ViewModel, runs <paramref name="configure"/> against it, resolves the Page,
    /// and binds them. Ordering matters: the viewmodel is fully initialised before the page
    /// exists, so every downstream hook sees final state. This is the whole reason the
    /// Shell library's pending-viewmodel configurator is unnecessary here.
    /// </summary>
    public Page CreatePage(Type viewModelType, Action<object>? configure = null)
    {
        var reg = builder.GetRegistration(viewModelType)
            ?? throw new InvalidOperationException(
                $"ViewModel '{viewModelType.FullName}' is not registered. Add it inside UseShinyNavigation with .Add<TPage, {viewModelType.Name}>()"
            );

        var vm = services.GetRequiredService(reg.ViewModelType);
        configure?.Invoke(vm);

        var page = (Page)services.GetRequiredService(reg.PageType);
        page.BindingContext = vm;

        logger.LogDebug("[Create] Page '{page}' bound to ViewModel '{vm}'", reg.PageType.Name, reg.ViewModelType.Name);
        return page;
    }


    /// <summary>
    /// Materialises the declared structure into a page tree and returns the page to assign
    /// to the window.
    /// </summary>
    public Page BuildRoot()
    {
        Page detail;

        if (this.Structure.HasTabs)
        {
            var tabbed = new TabbedPage();
            foreach (var tab in this.Structure.Tabs)
            {
                var page = this.CreatePage(tab.ViewModelType);

                if (!String.IsNullOrWhiteSpace(tab.Title))
                    page.Title = tab.Title;

                if (!String.IsNullOrWhiteSpace(tab.Icon))
                    page.IconImageSource = tab.Icon;

                // The NavigationPage wrapper is what gives each tab an independent back
                // stack. It has to carry the title/icon itself because the tab bar reads
                // them off the direct child of the TabbedPage, not off the inner page.
                Page child = tab.WrapInNavigationPage
                    ? new ShinyNavigationPage(page)
                    {
                        Title = page.Title,
                        IconImageSource = page.IconImageSource
                    }
                    : page;

                tabbed.Children.Add(child);
            }
            this.Tabs = tabbed;
            detail = tabbed;
        }
        else
        {
            var page = this.CreatePage(this.Structure.RootViewModelType!);
            detail = new ShinyNavigationPage(page);
        }

        if (this.Structure.HasFlyout)
        {
            var menu = this.CreatePage(this.Structure.FlyoutMenuViewModelType!);

            // MAUI throws if the flyout page has no title
            if (String.IsNullOrWhiteSpace(menu.Title))
                menu.Title = this.Structure.FlyoutTitle;

            var flyout = new FlyoutPage
            {
                Flyout = menu,
                Detail = detail,
                FlyoutLayoutBehavior = this.Structure.FlyoutBehavior
            };
            this.Flyout = flyout;
            this.RootPage = flyout;
        }
        else
        {
            this.Flyout = null;
            this.RootPage = detail;
        }

        return this.RootPage;
    }


    /// <summary>
    /// Assigns an arbitrary page as the window root, discarding the built structure.
    /// Used by <c>SwitchRoot</c>.
    /// </summary>
    public void SetRootPage(Page page)
    {
        this.RootPage = page;
        this.Flyout = page as FlyoutPage;
        this.Tabs = (this.Flyout?.Detail ?? page) as TabbedPage;
    }


    /// <summary>
    /// The navigation stack a push should land on: the modal stack when a modal is showing,
    /// otherwise the current tab's stack (or the single root stack).
    /// </summary>
    public NavigationPage? ActiveNavigationPage
    {
        get
        {
            var modals = this.ModalStack;
            if (modals is { Count: > 0 })
                return modals[^1] as NavigationPage;

            return this.DetailNavigationPage;
        }
    }


    /// <summary>The navigation stack behind any modals - the current tab's stack, or the single root stack</summary>
    public NavigationPage? DetailNavigationPage
    {
        get
        {
            var detail = this.Flyout?.Detail ?? this.RootPage;
            if (detail is TabbedPage tabbed)
                return tabbed.CurrentPage as NavigationPage;

            return detail as NavigationPage;
        }
    }


    /// <summary>The window-level modal stack</summary>
    public IReadOnlyList<Page>? ModalStack => this.RootPage?.Navigation?.ModalStack;


    /// <summary>The <see cref="INavigation"/> to use for modal push/pop - always window level</summary>
    public INavigation? ModalNavigation => this.RootPage?.Navigation;


    /// <summary>The page the user is actually looking at</summary>
    public Page? CurrentPage
    {
        get
        {
            var modals = this.ModalStack;
            if (modals is { Count: > 0 })
            {
                var top = modals[^1];
                return top is NavigationPage modalNav ? modalNav.CurrentPage : top;
            }

            var nav = this.DetailNavigationPage;
            if (nav != null)
                return nav.CurrentPage;

            var detail = this.Flyout?.Detail ?? this.RootPage;
            return detail is TabbedPage tabbed ? tabbed.CurrentPage : detail;
        }
    }


    /// <summary>The BindingContext of <see cref="CurrentPage"/></summary>
    public object? CurrentViewModel => this.CurrentPage?.BindingContext;


    /// <summary>
    /// The index of the tab hosting the given viewmodel type, or -1 when it isn't a tab.
    /// Tab index is how the native badge APIs address a tab.
    /// </summary>
    public int GetTabIndex(Type viewModelType)
    {
        for (var i = 0; i < this.Structure.Tabs.Count; i++)
        {
            if (this.Structure.Tabs[i].ViewModelType == viewModelType)
                return i;
        }
        return -1;
    }
}
