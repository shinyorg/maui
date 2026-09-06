namespace Shiny.Infrastructure;

public class NavigationBuilder(
    ShinyShellNavigator navigator,
    ShinyAppBuilder navBuilder,
    bool fromRoot
) : INavigationBuilder
{
    record Segment(string Route, Type? ViewModelType, Delegate? ConfigureAction);

    readonly List<Segment> segments = new();
    int popCount;
    bool skipInterceptors;


    public INavigationBuilder BypassInterceptors()
    {
        this.skipInterceptors = true;
        return this;
    }

    public INavigationBuilder PopBack(int count)
    {
        if (count < 1)
            throw new ArgumentException("Count must be 1 or more", nameof(count));

        if (fromRoot)
            throw new InvalidOperationException("PopBack is not supported when navigating from root");

        if (this.segments.Count > 0)
            throw new InvalidOperationException("PopBack must be called before any Add calls");

        this.popCount += count;
        return this;
    }

    public INavigationBuilder Add<TViewModel>() where TViewModel : class
    {
        var route = navBuilder.GetRouteForViewModel(typeof(TViewModel))
            ?? throw new InvalidOperationException($"Could not find a route for viewmodel '{typeof(TViewModel)}'");

        this.segments.Add(new Segment(route, typeof(TViewModel), null));
        return this;
    }

    public INavigationBuilder Add<TViewModel>(Action<TViewModel> configure) where TViewModel : class
    {
        var route = navBuilder.GetRouteForViewModel(typeof(TViewModel))
            ?? throw new InvalidOperationException($"Could not find a route for viewmodel '{typeof(TViewModel)}'");

        this.segments.Add(new Segment(route, typeof(TViewModel), configure));
        return this;
    }

    public INavigationBuilder Add(string routeName)
    {
        this.segments.Add(new Segment(routeName, null, null));
        return this;
    }

    public Task<bool> Navigate(bool bypassInterceptors = false, CancellationToken cancellationToken = default)
    {
        if (this.popCount == 0 && this.segments.Count == 0)
            throw new InvalidOperationException("No navigation segments have been added");

        var uri = this.BuildUri();
        var navType = fromRoot ? NavigationType.SetRoot : NavigationType.Push;

        // Pre-resolve each typed segment's viewmodel and apply its configure callback
        // synchronously, then hand them to the navigator to pin. The apply sites
        // (ShinyRouteFactory.GetOrCreate, ShinyShell.OnNavigated, AppOnPageAppearing) consume the
        // pinned instances in FIFO + type order matching the order Shell realises each segment's
        // page. No post-await stack walk is required because the configure callbacks have already
        // run before any page is constructed.
        var pins = new List<ShinyShellNavigator.PinnedViewModel>(this.segments.Count);
        foreach (var seg in this.segments)
        {
            if (seg.ViewModelType == null)
                continue;

            var vm = navigator.Services.GetRequiredService(seg.ViewModelType);
            seg.ConfigureAction?.DynamicInvoke(vm);
            pins.Add(new ShinyShellNavigator.PinnedViewModel(seg.ViewModelType, vm));
        }

        // Interceptors are shown the destination - the last segment - since that is the page the
        // user ends up on. A redirect drops every pin here, because none of those pages get built.
        // A destination added by raw route name has nothing pinned, so the pipeline resolves it.
        var last = this.segments.Count > 0 ? this.segments[^1] : null;
        var destination = last?.ViewModelType != null ? pins[^1] : default;

        return navigator.RunNavigation(new ShinyShellNavigator.NavigationRequest(
            uri,
            navType,
            new Dictionary<string, object>()
        )
        {
            Pins = pins,
            ViewModel = destination.Instance,
            ViewModelType = destination.Type,
            ResolveViewModel = last != null && last.ViewModelType == null,
            // Either way of asking counts - the fluent call and the argument mean the same thing.
            BypassInterceptors = bypassInterceptors || this.skipInterceptors,
            CancellationToken = cancellationToken
        });
    }


    string BuildUri()
    {
        var prefix = fromRoot ? "//" : "";
        var popPart = string.Join("/", Enumerable.Repeat("..", this.popCount));
        var routePart = string.Join("/", this.segments.Select(s => s.Route));

        var separator = this.popCount > 0 && this.segments.Count > 0 ? "/" : "";
        return prefix + popPart + separator + routePart;
    }
}
