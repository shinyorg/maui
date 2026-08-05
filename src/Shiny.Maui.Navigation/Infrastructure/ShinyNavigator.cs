using Microsoft.Extensions.Logging;

namespace Shiny.Navigation.Infrastructure;


public class ShinyNavigator(
    ILogger<ShinyNavigator> logger,
    IMainThread mainThread,
    NavigationHost host,
    TabBadgeManager tabBadgeManager
) : INavigator, IMauiInitializeService, IDisposable
{
    public event EventHandler<NavigationEventArgs>? Navigating;
    public event EventHandler<NavigatedEventArgs>? Navigated;

    Application? application;
    NavigationType? pendingNavigationType;
    TabbedPage? hookedTabs;


    public void Initialize(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService<IApplication>() is not Application app)
            throw new InvalidOperationException("Shiny.Maui.Navigation requires a MAUI Application");

        this.application = app;
        app.DescendantRemoved += this.OnDescendantRemoved;
        app.PageAppearing += this.OnPageAppearing;
        app.PageDisappearing += this.OnPageDisappearing;

        // Build the tree now so it exists by the time MAUI asks the Application for its
        // window. ShinyApplication.CreateWindow hands out host.RootPage - we deliberately
        // don't assign Windows[0].Page here because at Initialize time MAUI has not created
        // a window yet.
        host.BuildRoot();
        this.AttachTabHooks();
    }


    public void Dispose()
    {
        if (this.application == null)
            return;

        this.application.DescendantRemoved -= this.OnDescendantRemoved;
        this.application.PageAppearing -= this.OnPageAppearing;
        this.application.PageDisappearing -= this.OnPageDisappearing;
        this.DetachTabHooks();
    }


    public Page? CurrentPage => host.CurrentPage;
    public object? CurrentViewModel => host.CurrentViewModel;
    public bool HasFlyout => host.Flyout != null;

    public INavigationBuilder CreateBuilder() => new NavigationBuilder(this, host, mainThread);


    public Task NavigateTo<TViewModel>(Action<TViewModel>? configure = null, bool animated = true)
        where TViewModel : class
        => this.DoNavigateTo(typeof(TViewModel), Wrap(configure), animated);


    public Task NavigateTo(Type viewModelType, bool animated = true)
        => this.DoNavigateTo(viewModelType, null, animated);


    Task DoNavigateTo(Type viewModelType, Action<object>? configure, bool animated)
        => mainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (!await this.CanLeaveCurrent().ConfigureAwait(true))
                return;

            var nav = this.RequireActiveNavigation();
            this.BeginNavigation(viewModelType, NavigationType.Push);

            var page = host.CreatePage(viewModelType, configure);
            await nav.PushAsync(page, animated).ConfigureAwait(true);
            this.AfterNavigate();
        });


    public Task NavigateToRoot<TViewModel>(Action<TViewModel>? configure = null, bool animated = true)
        where TViewModel : class
        => this.DoNavigateToRoot(typeof(TViewModel), Wrap(configure), animated);


    public Task NavigateToRoot(Type viewModelType, bool animated = true)
        => this.DoNavigateToRoot(viewModelType, null, animated);


    Task DoNavigateToRoot(Type viewModelType, Action<object>? configure, bool animated)
        => mainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (!await this.CanLeaveCurrent().ConfigureAwait(true))
                return;

            var nav = this.RequireActiveNavigation();
            this.BeginNavigation(viewModelType, NavigationType.SetRoot);

            var page = host.CreatePage(viewModelType, configure);

            // Insert the new page below the current one, then pop to it. Doing it in this
            // order means the user sees a normal back transition instead of the stack
            // visibly emptying underneath them.
            var current = nav.NavigationStack[^1];
            nav.InsertPageBefore(page, current);
            await nav.PopAsync(animated).ConfigureAwait(true);

            // Anything still beneath the new root is stale - drop it without animating.
            foreach (var stale in nav.NavigationStack.Where(x => x != page).ToList())
                nav.RemovePage(stale);

            this.AfterNavigate();
        });


    public Task PushModal<TViewModel>(Action<TViewModel>? configure = null, bool animated = true, bool wrapInNavigationPage = true)
        where TViewModel : class
        => this.DoPushModal(typeof(TViewModel), Wrap(configure), animated, wrapInNavigationPage);


    public Task PushModal(Type viewModelType, bool animated = true, bool wrapInNavigationPage = true)
        => this.DoPushModal(viewModelType, null, animated, wrapInNavigationPage);


    Task DoPushModal(Type viewModelType, Action<object>? configure, bool animated, bool wrapInNavigationPage)
        => mainThread.InvokeOnMainThreadAsync(async () =>
        {
            var nav = host.ModalNavigation
                ?? throw new InvalidOperationException("The navigation host has not been initialized yet");

            this.BeginNavigation(viewModelType, NavigationType.PushModal);

            var page = host.CreatePage(viewModelType, configure);
            var modal = wrapInNavigationPage ? new ShinyNavigationPage(page) : page;

            await nav.PushModalAsync(modal, animated).ConfigureAwait(true);
            this.AfterNavigate();
        });


    public Task PopModal(bool animated = true) => mainThread.InvokeOnMainThreadAsync(async () =>
    {
        if (!await this.CanLeaveCurrent().ConfigureAwait(true))
            return;

        var nav = host.ModalNavigation;
        if (nav == null || nav.ModalStack.Count == 0)
            throw new InvalidOperationException("There is nothing on the modal stack to pop");

        this.BeginNavigation(null, NavigationType.PopModal);
        await nav.PopModalAsync(animated).ConfigureAwait(true);
        this.AfterNavigate();
    });


    public Task GoBack(bool animated = true) => this.GoBack(1, animated);


    public Task GoBack(int backCount, bool animated = true) => mainThread.InvokeOnMainThreadAsync(async () =>
    {
        if (backCount < 1)
            throw new ArgumentException("Back count must be 1 or more", nameof(backCount));

        if (!await this.CanLeaveCurrent().ConfigureAwait(true))
            return;

        var nav = this.RequireActiveNavigation();

        // Snapshot - NavigationStack is live, so indexing it while removing pages shifts
        // out from under the loop.
        var stack = nav.NavigationStack.ToList();
        if (stack.Count <= backCount)
            throw new InvalidOperationException($"Cannot go back {backCount} page(s) - the stack only holds {stack.Count}");

        this.BeginNavigation(null, NavigationType.GoBack);

        // Remove the intermediate pages first so the user sees a single animated pop
        // rather than N of them stacked up.
        for (var i = 1; i < backCount; i++)
            nav.RemovePage(stack[stack.Count - 1 - i]);

        await nav.PopAsync(animated).ConfigureAwait(true);
        this.AfterNavigate();
    });


    public Task PopToRoot(bool animated = true) => mainThread.InvokeOnMainThreadAsync(async () =>
    {
        if (!await this.CanLeaveCurrent().ConfigureAwait(true))
            return;

        var nav = this.RequireActiveNavigation();
        if (nav.NavigationStack.Count <= 1)
            return;

        this.BeginNavigation(null, NavigationType.PopToRoot);
        await nav.PopToRootAsync(animated).ConfigureAwait(true);
        this.AfterNavigate();
    });


    public Task SelectTab<TViewModel>() where TViewModel : class => this.SelectTab(typeof(TViewModel));


    public Task SelectTab(Type viewModelType) => mainThread.InvokeOnMainThreadAsync(() =>
    {
        var tabs = host.Tabs
            ?? throw new InvalidOperationException("This app has no tabs - declare them with AddTabs(...) in UseShinyNavigation");

        var index = host.GetTabIndex(viewModelType);
        if (index < 0)
            throw new InvalidOperationException($"'{viewModelType.FullName}' is not registered as a tab");

        this.BeginNavigation(viewModelType, NavigationType.SelectTab);
        tabs.CurrentPage = tabs.Children[index];
        this.AfterNavigate();
    });


    public Task SetTabBadge<TViewModel>(int value) where TViewModel : class
        => tabBadgeManager.Set(this.RequireTabIndex(typeof(TViewModel)), value);


    public Task ClearTabBadge<TViewModel>() where TViewModel : class
        => tabBadgeManager.Clear(this.RequireTabIndex(typeof(TViewModel)));


    public Task OpenFlyout() => this.SetFlyout(true);
    public Task CloseFlyout() => this.SetFlyout(false);


    public Task SwitchRoot<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : class
    {
        this.BeginNavigation(typeof(TViewModel), NavigationType.SwitchRoot);
        var page = host.CreatePage(typeof(TViewModel), Wrap(configure));
        return this.SwapWindowPage(new ShinyNavigationPage(page));
    }


    public Task RestoreRoot()
    {
        this.BeginNavigation(null, NavigationType.SwitchRoot);
        var root = host.BuildRoot();
        return this.SwapWindowPage(root, host.RootPage);
    }


    // -- internals ------------------------------------------------------------------

    internal NavigationHost Host => host;


    internal void BeginNavigation(Type? toViewModelType, NavigationType navigationType)
    {
        if (host.CurrentViewModel is INavigatingAway away)
            away.OnNavigatingAway();

        this.pendingNavigationType = navigationType;

        try
        {
            this.Navigating?.Invoke(this, new NavigationEventArgs(
                host.CurrentViewModel,
                toViewModelType,
                navigationType
            ));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Navigating event handler");
        }
    }


    /// <summary>
    /// Runs after a navigation completes. Navigated itself is raised from PageAppearing so
    /// that the destination viewmodel is genuinely bound and on screen when subscribers see it.
    /// </summary>
    internal void AfterNavigate()
    {
        if (host.Structure.CloseFlyoutOnNavigate && host.Flyout is { IsPresented: true } flyout)
            flyout.IsPresented = false;

        tabBadgeManager.ReapplyAll();
    }


    internal async Task<bool> CanLeaveCurrent()
    {
        if (host.CurrentViewModel is not INavigationConfirmation confirm)
            return true;

        var canNavigate = await confirm.CanNavigate().ConfigureAwait(true);
        if (!canNavigate)
            logger.LogDebug("[Guard] Navigation cancelled by '{type}'", confirm.GetType().Name);

        return canNavigate;
    }


    internal INavigation RequireActiveNavigation()
        => host.ActiveNavigationPage?.Navigation
            ?? throw new InvalidOperationException(
                "There is no active navigation stack. A modal pushed with wrapInNavigationPage:false cannot push pages - pop it first, or push it wrapped."
            );


    static Action<object>? Wrap<TViewModel>(Action<TViewModel>? configure) where TViewModel : class
        => configure == null ? null : o => configure((TViewModel)o);


    int RequireTabIndex(Type viewModelType)
    {
        var index = host.GetTabIndex(viewModelType);
        if (index < 0)
            throw new InvalidOperationException($"'{viewModelType.FullName}' is not registered as a tab");

        return index;
    }


    Task SetFlyout(bool present) => mainThread.InvokeOnMainThreadAsync(() =>
    {
        var flyout = host.Flyout
            ?? throw new InvalidOperationException("This app has no flyout - declare one with AddFlyout(...) in UseShinyNavigation");

        flyout.IsPresented = present;
    });


    Task SwapWindowPage(Page page, Page? trackAs = null) => mainThread.InvokeOnMainThreadAsync(async () =>
    {
        if (this.application == null || this.application.Windows.Count == 0)
            throw new InvalidOperationException("No active window to set the root page on");

        var window = this.application.Windows[0];

        // Two-phase swap, carried over from the Shell library's SwitchShell: replacing the
        // window page while the outgoing page's native view hierarchy is still live crashes
        // on iOS. Tear the old handler down against a blank page first.
        if (window.Page?.Handler is { } oldHandler)
        {
            logger.LogDebug("Disconnecting old handler '{type}'", oldHandler.GetType().Name);
            oldHandler.DisconnectHandler();
        }
        window.Page = new ContentPage();
        await Task.Delay(50).ConfigureAwait(true);

        window.Page = page;
        host.SetRootPage(trackAs ?? page);

        // Re-hook whatever TabbedPage (if any) the new root exposes, releasing the old one.
        this.AttachTabHooks();
        this.AfterNavigate();
    });


    /// <summary>
    /// Hooks the current <see cref="TabbedPage"/>, unhooking whichever one we were on before.
    /// Tracked by instance rather than by asking the host each time, because a root swap
    /// replaces (or removes) the TabbedPage and the old one must still be released.
    /// </summary>
    void AttachTabHooks()
    {
        if (ReferenceEquals(this.hookedTabs, host.Tabs))
            return;

        this.DetachTabHooks();
        this.hookedTabs = host.Tabs;

        if (this.hookedTabs != null)
            this.hookedTabs.CurrentPageChanged += this.OnTabChanged;
    }


    void DetachTabHooks()
    {
        if (this.hookedTabs != null)
            this.hookedTabs.CurrentPageChanged -= this.OnTabChanged;

        this.hookedTabs = null;
    }


    void OnTabChanged(object? sender, EventArgs args)
        // Covers the user tapping a tab. A programmatic SelectTab has already set this,
        // and setting it twice is harmless.
        => this.pendingNavigationType ??= NavigationType.SelectTab;


    void OnPageAppearing(object? sender, Page page)
    {
        if (page.BindingContext is IPageLifecycleAware lc)
        {
            logger.LogDebug("[OnAppearing] ViewModel '{type}'", lc.GetType().Name);
            lc.OnAppearing();
        }

        // A navigation with no pending type is one the platform initiated - the OS back
        // button, or an interactive back gesture.
        var navType = this.pendingNavigationType ?? NavigationType.GoBack;
        this.pendingNavigationType = null;

        try
        {
            this.Navigated?.Invoke(this, new NavigatedEventArgs(page.BindingContext, navType));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Navigated event handler");
        }
    }


    void OnPageDisappearing(object? sender, Page page)
    {
        if (page.BindingContext is IPageLifecycleAware lc)
        {
            logger.LogDebug("[OnDisappearing] ViewModel '{type}'", lc.GetType().Name);
            lc.OnDisappearing();
        }
    }


    void OnDescendantRemoved(object? sender, ElementEventArgs args)
    {
        if (args.Element is Page { BindingContext: IDisposable disposable })
        {
            logger.LogDebug("[Dispose] ViewModel '{type}'", disposable.GetType().Name);
            disposable.Dispose();
        }
    }
}
