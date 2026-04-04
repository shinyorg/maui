namespace Shiny.Infrastructure;

public class ProxyNavigator : INavigator, IDisposable
{
    readonly ShinyShellNavigator shellNavigator;
    readonly ICarPlayContext carPlayContext;
    readonly IServiceProvider services;
    readonly CarPlayRegistration registration;
    INavigator? carPlayNavigator;

    public ProxyNavigator(
        ShinyShellNavigator shellNavigator,
        ICarPlayContext carPlayContext,
        IServiceProvider services,
        CarPlayRegistration registration
    )
    {
        this.shellNavigator = shellNavigator;
        this.carPlayContext = carPlayContext;
        this.services = services;
        this.registration = registration;

        shellNavigator.Navigating += OnDelegateNavigating;
        shellNavigator.Navigated += OnDelegateNavigated;
        carPlayContext.ConnectionChanged += OnConnectionChanged;
    }

    public event EventHandler<NavigationEventArgs>? Navigating;
    public event EventHandler<NavigatedEventArgs>? Navigated;

    INavigator Current => this.carPlayContext.IsConnected
        ? this.carPlayNavigator ??= this.ResolveCarPlayNavigator()
        : this.shellNavigator;

    INavigator ResolveCarPlayNavigator()
    {
        var nav = (INavigator)this.services.GetRequiredService(this.registration.NavigatorType);
        nav.Navigating += OnDelegateNavigating;
        nav.Navigated += OnDelegateNavigated;
        return nav;
    }

    void OnConnectionChanged(object? sender, bool connected)
    {
        // events are always forwarded from both delegates, no rewiring needed
    }

    void OnDelegateNavigating(object? sender, NavigationEventArgs e)
        => this.Navigating?.Invoke(this, e);

    void OnDelegateNavigated(object? sender, NavigatedEventArgs e)
        => this.Navigated?.Invoke(this, e);

    public Task NavigateTo(string route, params IEnumerable<(string Key, object Value)> args)
        => this.Current.NavigateTo(route, args);

    public Task NavigateTo<TViewModel>(Action<TViewModel>? configure = null, params IEnumerable<(string Key, object Value)> args)
        => this.Current.NavigateTo(configure, args);

    public Task SetRoot<TViewModel>(Action<TViewModel>? configure = null, params IEnumerable<(string Key, object Value)> args)
        => this.Current.SetRoot(configure, args);

    public Task PopToRoot(params IEnumerable<(string Key, object Value)> args)
        => this.Current.PopToRoot(args);

    public Task GoBack(params IEnumerable<(string Key, object Value)> args)
        => this.Current.GoBack(args);

    public Task GoBack(int backCount = 1, params IEnumerable<(string Key, object Value)> args)
        => this.Current.GoBack(backCount, args);

    public Task SwitchShell(Shell shell)
        => this.Current.SwitchShell(shell);

    public Task SwitchShell<TShell>() where TShell : Shell
        => this.Current.SwitchShell<TShell>();

    public void Dispose()
    {
        this.shellNavigator.Navigating -= OnDelegateNavigating;
        this.shellNavigator.Navigated -= OnDelegateNavigated;

        if (this.carPlayNavigator != null)
        {
            this.carPlayNavigator.Navigating -= OnDelegateNavigating;
            this.carPlayNavigator.Navigated -= OnDelegateNavigated;
        }

        this.carPlayContext.ConnectionChanged -= OnConnectionChanged;
    }
}
