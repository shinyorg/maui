namespace Shiny.Infrastructure;


/// <summary>
/// <see cref="AsyncLocal{T}"/>-backed <see cref="INavigationContextAccessor"/>. The value flows
/// into whatever the interceptor awaits, so a dialog shown three awaits deep still sees it.
/// </summary>
public class NavigationContextAccessor : INavigationContextAccessor
{
    readonly AsyncLocal<NavigationContext?> current = new();

    public NavigationContext? Current => this.current.Value;


    /// <summary>
    /// Sets the context for the duration of one interception pass. Dispose restores the previous
    /// value rather than clearing it, so nested navigations (an interceptor that navigates) unwind
    /// correctly.
    /// </summary>
    public IDisposable Push(NavigationContext context)
    {
        var previous = this.current.Value;
        this.current.Value = context;
        return new Scope(this, previous);
    }


    sealed class Scope(NavigationContextAccessor owner, NavigationContext? previous) : IDisposable
    {
        bool disposed;

        public void Dispose()
        {
            if (this.disposed)
                return;

            this.disposed = true;
            owner.current.Value = previous;
        }
    }
}
