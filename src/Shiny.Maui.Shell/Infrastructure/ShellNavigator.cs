using Microsoft.Extensions.Logging;

namespace Shiny.Infrastructure;


public class ShinyShellNavigator(
    ILogger<ShinyShellNavigator> logger,
    IMainThread mainThread,
    ShinyAppBuilder navBuilder,
    ShellTabBadgeManager tabBadgeManager,
    ShellNavigationConfigurator configurator
) : INavigator, IMauiInitializeService, IDisposable
{
    public event EventHandler<NavigationEventArgs>? Navigating;
    public event EventHandler<NavigatedEventArgs>? Navigated;
    IServiceProvider services = null!;
    Application application = null!;

    internal IServiceProvider Services => this.services;

    record PendingNavigation(string ToUri, NavigationType NavigationType, IReadOnlyDictionary<string, object> Parameters);
    PendingNavigation? pendingNavigation;
    bool isProgrammaticNavigation;

    public void Initialize(IServiceProvider serviceProvider)
    {
        var appService = serviceProvider.GetService<IApplication>();
        if (appService is not Application app)
            throw new InvalidOperationException($"Invalid MAUI Application - {application.GetType()}");

        this.services = serviceProvider;
        this.application = app;
        app.DescendantAdded += this.AppOnDescendantAdded;
        app.DescendantRemoved += this.AppOnDescendantRemoved;
        app.PageAppearing += this.AppOnPageAppearing;
        app.PageDisappearing += this.AppOnPageDisappearing;

        // The initial page may have already appeared before event handlers were registered
        var currentPage = Shell.Current?.CurrentPage;
        if (currentPage != null)
            this.AppOnPageAppearing(this, currentPage);
    }
    
    
    public void Dispose()
    {
        if (this.application == null)
            return;
        
        this.application.DescendantAdded -= this.AppOnDescendantAdded;
        this.application.DescendantRemoved -= this.AppOnDescendantRemoved;
        this.application.PageAppearing -= this.AppOnPageAppearing;
        this.application.PageDisappearing -= this.AppOnPageDisappearing;
    }

    
    void RaiseNavigating(Shell shell, string toUri, NavigationType navigationType, IDictionary<string, object> parameters)
    {
        var readOnlyParams = new Dictionary<string, object>(parameters);
        this.pendingNavigation = new PendingNavigation(toUri, navigationType, readOnlyParams);

        try
        {
            this.Navigating?.Invoke(this, new NavigationEventArgs(
                shell.CurrentState?.Location?.ToString(),
                shell.CurrentPage?.BindingContext,
                toUri,
                navigationType,
                readOnlyParams
            ));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Navigating event handler");
        }
    }


    void RaiseNavigated(object? toViewModel)
    {
        var pending = this.pendingNavigation;
        this.pendingNavigation = null;
        if (pending == null)
            return;

        try
        {
            this.Navigated?.Invoke(this, new NavigatedEventArgs(
                pending.ToUri,
                toViewModel,
                pending.NavigationType,
                pending.Parameters
            ));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Navigated event handler");
        }
    }


    public INavigationBuilder CreateBuilder(bool fromRoot = false)
        => new NavigationBuilder(this, navBuilder, configurator, mainThread, logger, fromRoot);


    internal void PrepareForProgrammaticNavigation(string uri, NavigationType navType, Dictionary<string, object> parameters)
    {
        if (Shell.Current.CurrentPage?.BindingContext is INavigationAware navAware)
            navAware.OnNavigatingFrom(parameters);

        this.RaiseNavigating(Shell.Current, uri, navType, parameters);
        this.isProgrammaticNavigation = true;
    }


    public Task NavigateTo(string route, bool relativeNavigation = true, params IEnumerable<(string Key, object Value)> args) =>
        mainThread.InvokeOnMainThreadAsync(() =>
        {
            var shell = Shell.Current;
            var parameters = args.ToDictionary(x => x.Key, x => x.Value);

            if (shell.CurrentPage?.BindingContext is INavigationAware navAware)
                navAware.OnNavigatingFrom(parameters);

            var uri = relativeNavigation ? route : $"//{route}";
            var navType = relativeNavigation ? NavigationType.Push : NavigationType.SetRoot;
            this.RaiseNavigating(shell, uri, navType, parameters);
            this.isProgrammaticNavigation = true;

            if (OperatingSystem.IsLinux())
            {
                // Shell.GoToAsync is unreliable on Platform.Maui.Linux.Gtk4 — resolve
                // the page from the registered route map and push directly.
                var pageType = navBuilder.GetPageTypeForRoute(route);
                if (pageType != null && services.GetService(pageType) is Page page)
                    return shell.Navigation.PushAsync(page, true);
            }

            return shell.GoToAsync(uri, true, parameters);
        });


    public async Task NavigateTo<TViewModel>(
        Action<TViewModel>? configure = null,
        bool relativeNavigation = true,
        params IEnumerable<(string Key, object Value)> args
    )
    {
        var route = navBuilder.GetRouteForViewModel(typeof(TViewModel));
        if (route == null)
            throw new InvalidOperationException($"Could not find a route for viewmodel '{typeof(TViewModel)}'");

        if (!relativeNavigation)
            route = $"//{route}";

        var parameters = args.ToDictionary(x => x.Key, x => x.Value);
        if (Shell.Current.CurrentPage?.BindingContext is INavigationAware navAware)
            navAware.OnNavigatingFrom(parameters);

        var navType = relativeNavigation ? NavigationType.Push : NavigationType.SetRoot;
        this.RaiseNavigating(Shell.Current, route, navType, parameters);
        this.isProgrammaticNavigation = true;

        // Resolve and configure the viewmodel synchronously. Pin the instance
        // on the configurator so whichever apply site fires
        // (ShinyRouteFactory.GetOrCreate for registered routes,
        // ShinyShell.OnNavigated for ShellContent routes, or AppOnPageAppearing
        // as a fallback) consumes our instance instead of resolving a fresh one
        // from DI. The configure callback runs before pinning so every downstream
        // hook — including IPageLifecycleAware.OnAppearing on whatever schedule
        // Shell decides to fire it — observes a fully initialised viewmodel.
        var vm = (TViewModel)services.GetRequiredService(typeof(TViewModel)!);
        configure?.Invoke(vm);
        var subscription = configurator.EnqueueResolved(typeof(TViewModel), vm!);

        try
        {
            // Fire the navigation. GoToAsync throws synchronously for an unknown
            // route, which surfaces as the awaited Task's exception — the only
            // failure mode the navigator needs to report. After the awaiter
            // resolves we deliberately don't probe Shell.Current.CurrentPage:
            // on Android the awaiter can resolve before Shell's CurrentItem
            // chain updates and before Shell.OnNavigated / PageAppearing fire,
            // so a post-await BindingContext check races against Shell's own
            // scheduling. The pinned viewmodel + apply-site model handles
            // that timing naturally — once Shell raises PageAppearing for the
            // target page (immediately, or on the next dispatcher tick), the
            // apply site consumes the pinned instance and binds it before
            // OnAppearing runs.
            await mainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(route, true, parameters));
        }
        catch
        {
            // Only roll back the pinned entry when navigation actually failed.
            // On success we leave it pinned because the apply site has not yet
            // fired (typically runs on the next dispatcher tick) and disposing
            // would cause it to fall back to a fresh DI resolve, throwing away
            // our configured instance.
            subscription.Dispose();
            throw;
        }
    }

    
    public async Task<DialogResult<T>> ShowDialog<TViewModel, T>(
        Action<TViewModel>? configure = null,
        CancellationToken cancellationToken = default
    ) where TViewModel : class, IDialogAware<T>
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pageType = navBuilder.GetPageTypeForViewModel(typeof(TViewModel))
            ?? throw new InvalidOperationException(
                $"Could not find a page mapped to viewmodel '{typeof(TViewModel)}'. Dialog viewmodels must be mapped with ShinyAppBuilder.Add<TPage, TViewModel>() or [ShellMap<TPage>] + AddGeneratedMaps()"
            );

        var vm = (TViewModel)services.GetRequiredService(typeof(TViewModel));
        var completion = new TaskCompletionSource<DialogResult<T>>(TaskCreationOptions.RunContinuationsAsynchronously);

        // TrySetResult throughout: the first of the two events wins, so a viewmodel that raises
        // Completed twice, or raises Cancelled after Completed, cannot corrupt the result.
        void OnCompleted(object? sender, T value) => completion.TrySetResult(DialogResult<T>.Complete(value));
        void OnCancelled(object? sender, EventArgs args) => completion.TrySetResult(DialogResult<T>.Cancel());

        // Subscribe before configure so a viewmodel that completes during configuration - or
        // synchronously from OnAppearing before the presenter's push has been awaited - is captured.
        vm.Completed += OnCompleted;
        vm.Cancelled += OnCancelled;
        try
        {
            configure?.Invoke(vm);

            // Resolve and bind the page on the main thread - InitializeComponent is not safe to run
            // off it. Normal navigation gets this for free because Shell constructs the page itself
            // inside GoToAsync; here we own the instance so we own the dispatch.
            var page = await mainThread
                .InvokeOnMainThreadAsync(() =>
                {
                    var resolved = (Page)services.GetRequiredService(pageType);
                    resolved.BindingContext = vm;
                    return Task.FromResult(resolved);
                })
                .ConfigureAwait(false);

            var presenter = services.GetRequiredService<IDialogPresenter>();
            using var dismiss = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            logger.LogDebug("[Dialog] Presenting '{type}'", typeof(TViewModel));
            var presentation = presenter.Present(page, vm, dismiss.Token);
            var finished = await Task.WhenAny(completion.Task, presentation).ConfigureAwait(false);

            if (finished == presentation)
            {
                // The presentation ended without the viewmodel producing a result - the user
                // dismissed it (back button, swipe-down, tap-outside). Awaiting it first surfaces a
                // genuine presenter fault instead of silently reporting cancellation.
                await presentation.ConfigureAwait(false);
                completion.TrySetResult(DialogResult<T>.Cancel());
            }
            else
            {
                // The viewmodel produced a result - tell the presenter to tear the dialog down and
                // wait until it is actually gone before returning to the caller.
                await dismiss.CancelAsync().ConfigureAwait(false);
                await presentation.ConfigureAwait(false);
            }

            // Caller cancellation is an OperationCanceledException, not a cancelled DialogResult -
            // the two mean different things and callers handle them differently.
            cancellationToken.ThrowIfCancellationRequested();

            var result = await completion.Task.ConfigureAwait(false);
            logger.LogDebug("[Dialog] '{type}' closed (cancelled: {cancelled})", typeof(TViewModel), result.IsCancelled);
            return result;
        }
        finally
        {
            vm.Completed -= OnCompleted;
            vm.Cancelled -= OnCancelled;
        }
    }


    public Task PopToRoot(params IEnumerable<(string Key, object Value)> args)
    {
        // we already have 1 page covered and we don't want to pop the last page
        var count = Shell.Current.Navigation.NavigationStack.Count - 1;
        if (count < 1)
            count = 1;

        return this.DoGoBack(count, NavigationType.PopToRoot, args);
    }


    public Task GoBack(params IEnumerable<(string Key, object Value)> args) => this.DoGoBack(1, NavigationType.GoBack, args);


    public Task GoBack(int backCount = 1, params IEnumerable<(string Key, object Value)> args) => this.DoGoBack(backCount, NavigationType.GoBack, args);


    public async Task SwitchShell(Shell shell)
    {
        ArgumentNullException.ThrowIfNull(shell);

        if (application is not Application app)
            throw new InvalidOperationException($"Invalid MAUI Application - {application.GetType()}");

        var currentShell = Shell.Current;
        var parameters = new Dictionary<string, object>();

        if (currentShell?.CurrentPage?.BindingContext is INavigationAware navAware)
            navAware.OnNavigatingFrom(parameters);

        if (currentShell != null)
        {
            this.RaiseNavigating(
                currentShell,
                shell.GetType().Name,
                NavigationType.SwitchShell,
                parameters
            );
        }

        if (app.Windows.Count == 0)
            throw new InvalidOperationException("No active window to switch Shell on");

        // Two-phase swap: first replace the current Shell with a temporary blank page.
        // This forces the platform to tear down the old Shell handlers and puts the
        // native window (UIWindow on iOS) into a clean state — avoiding the crash in
        // ShellFlyoutRenderer.ViewDidLoad that occurs when a new Shell handler is
        // created while the old Shell's native view hierarchy is still active.
        await mainThread.InvokeOnMainThreadAsync(() =>
        {
            var window = app.Windows[0];
            if (window.Page?.Handler is IElementHandler oldHandler)
            {
                logger.LogDebug("Disconnecting old handler '{type}'", oldHandler.GetType().Name);
                oldHandler.DisconnectHandler();
            }
            window.Page = new ContentPage();
        });

        // Yield to let the platform fully process the interim page and clean up native state
        await Task.Delay(50).ConfigureAwait(false);

        // Now set the actual Shell in a clean window state
        await mainThread.InvokeOnMainThreadAsync(() =>
        {
            var window = app.Windows[0];
            window.Page = shell;
            logger.LogDebug("Switched Shell to '{type}'", shell.GetType().Name);
        });
    }


    public Task SwitchShell<TShell>() where TShell : Shell
    {
        var shell = services.GetRequiredService<TShell>();
        return this.SwitchShell(shell);
    }


    public Task SetTabBadge(string route, int value) => tabBadgeManager.Set(route, value);


    public Task SetTabBadge<TViewModel>(int value)
    {
        var route = navBuilder.GetRouteForViewModel(typeof(TViewModel));
        if (route == null)
            throw new InvalidOperationException($"Could not find a route for viewmodel '{typeof(TViewModel)}'");

        return tabBadgeManager.Set(route, value);
    }


    public Task ClearTabBadge(string route) => tabBadgeManager.Clear(route);


    public Task ClearTabBadge<TViewModel>()
    {
        var route = navBuilder.GetRouteForViewModel(typeof(TViewModel));
        if (route == null)
            throw new InvalidOperationException($"Could not find a route for viewmodel '{typeof(TViewModel)}'");

        return tabBadgeManager.Clear(route);
    }


    Task DoGoBack(int backCount, NavigationType navType, IEnumerable<(string Key, object Value)> args) => mainThread.InvokeOnMainThreadAsync(() =>
    {
        if (backCount < 1)
            throw new ArgumentException("Back count must be 1 or more");

        var uri = String.Empty;
        for (var i = 0; i < backCount; i++)
        {
            if (i > 0)
                uri += "/";

            uri += "..";
        }

        var shell = Shell.Current;
        var parameters = args.ToDictionary(x => x.Key, x => x.Value);
        if (shell.CurrentPage?.BindingContext is INavigationAware navAware)
            navAware.OnNavigatingFrom(parameters);

        this.RaiseNavigating(shell, uri, navType, parameters);
        this.isProgrammaticNavigation = true;
        return shell.GoToAsync(uri, true, parameters);
    });
    
    
    void AppOnDescendantAdded(object? sender, ElementEventArgs args)
    {
        if (args.Element is Shell shell)
        {
            shell.Navigating += async (_, shellArgs) =>
            {
                if (this.isProgrammaticNavigation)
                {
                    this.isProgrammaticNavigation = false;
                    return;
                }
                
                var vm = shell.CurrentPage?.BindingContext;

                if (vm is INavigationConfirmation confirm)
                {
                    var deferral = shellArgs.GetDeferral();
                    var canNav = await confirm.CanNavigate();
                    if (!canNav)
                        shellArgs.Cancel();

                    deferral.Complete();
                }
            };
        }
    }
    
    
    void AppOnDescendantRemoved(object? sender, ElementEventArgs args)
    {
        if (args.Element is Page { BindingContext: IDisposable disposable })
        {
            logger.LogDebug("[Dispose] ViewModel '{type}'", disposable.GetType());
            disposable.Dispose();
        }
    }

    
    void AppOnPageAppearing(object? sender, Page page)
    {
        // BindingContext may be inherited from Shell rather than explicitly set —
        // check whether it's already the correct ViewModel type
        var viewModelType = navBuilder.GetViewModelTypeForPage(page);
        if (viewModelType != null && (page.BindingContext == null || !viewModelType.IsInstanceOfType(page.BindingContext)))
        {
            // Prefer the pinned instance from a pending NavigateTo<TVm> /
            // INavigationBuilder.Navigate call. Falls back to DI for the
            // initial-page case where no programmatic navigation issued one.
            var vm = configurator.TryConsume(viewModelType) ?? services.GetService(viewModelType);
            page.BindingContext = vm;
            logger.LogDebug("[Binding] ViewModel {type} set on page", viewModelType);
        }

        if (page.BindingContext is IPageLifecycleAware lc)
        {
            logger.LogDebug("[OnAppearing] ViewModel '{type}' ", lc.GetType());
            lc.OnAppearing();
        }

        tabBadgeManager.ReapplyAll();
        this.RaiseNavigated(page.BindingContext);
    }


    void AppOnPageDisappearing(object? sender, Page page)
    {
        if (page.BindingContext is IPageLifecycleAware lc)
        {
            logger.LogDebug("[OnDisappearing] ViewModel '{type}' ", lc.GetType());
            lc.OnDisappearing();
        }
    }
}
