namespace Shiny.Navigation.Infrastructure;

public class NavigationBuilder(
    ShinyNavigator navigator,
    NavigationHost host,
    IMainThread mainThread
) : INavigationBuilder
{
    record Segment(Type ViewModelType, Action<object>? Configure);

    readonly List<Segment> segments = new();
    int popCount;
    bool fromRoot;


    public INavigationBuilder PopBack(int count = 1)
    {
        if (count < 1)
            throw new ArgumentException("Count must be 1 or more", nameof(count));

        if (this.segments.Count > 0)
            throw new InvalidOperationException("PopBack must be called before any Add calls");

        if (this.fromRoot)
            throw new InvalidOperationException("PopBack cannot be combined with FromRoot");

        this.popCount += count;
        return this;
    }


    public INavigationBuilder FromRoot()
    {
        if (this.popCount > 0)
            throw new InvalidOperationException("FromRoot cannot be combined with PopBack");

        this.fromRoot = true;
        return this;
    }


    public INavigationBuilder Add<TViewModel>() where TViewModel : class
        => this.AddSegment(typeof(TViewModel), null);


    public INavigationBuilder Add<TViewModel>(Action<TViewModel> configure) where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(configure);
        return this.AddSegment(typeof(TViewModel), o => configure((TViewModel)o));
    }


    public Task Navigate() => mainThread.InvokeOnMainThreadAsync(async () =>
    {
        if (this.segments.Count == 0)
            throw new InvalidOperationException("No navigation segments have been added");

        if (!await navigator.CanLeaveCurrent().ConfigureAwait(true))
            return;

        var nav = navigator.RequireActiveNavigation();
        navigator.BeginNavigation(
            this.segments[0].ViewModelType,
            this.fromRoot ? NavigationType.SetRoot : NavigationType.Push
        );

        // Build every page up front. A missing registration then fails before the stack has
        // been touched, rather than half way through mutating it.
        var pages = this.segments
            .Select(x => host.CreatePage(x.ViewModelType, x.Configure))
            .ToList();

        // Snapshot what was on the stack before we add anything - these are the candidates
        // for popping/clearing once the new pages are in place.
        var existing = nav.NavigationStack.ToList();

        // Push the LAST page first so the user sees exactly one forward animation, then
        // slide the intermediate pages in underneath it. Pushing them in order would play
        // one animation per page.
        await nav.PushAsync(pages[^1], true).ConfigureAwait(true);
        for (var i = 0; i < pages.Count - 1; i++)
            nav.InsertPageBefore(pages[i], pages[^1]);

        if (this.fromRoot)
        {
            foreach (var page in existing)
                nav.RemovePage(page);
        }
        else
        {
            // Drop the requested number of pages from the top of the pre-existing stack.
            for (var i = 0; i < this.popCount && existing.Count - 1 - i >= 0; i++)
                nav.RemovePage(existing[existing.Count - 1 - i]);
        }

        navigator.AfterNavigate();
    });


    INavigationBuilder AddSegment(Type viewModelType, Action<object>? configure)
    {
        this.segments.Add(new Segment(viewModelType, configure));
        return this;
    }
}
