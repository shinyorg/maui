namespace Shiny.Infrastructure;


/// <summary>
/// Backs <c>ShinyAppBuilder.AddNavigationInterceptor(Func&lt;...&gt;)</c>.
/// </summary>
public class DelegateNavigationInterceptor(
    Func<string, object?, CancellationToken, Task<NavigationInterceptorResult>> handler,
    int order = 0
) : INavigationInterceptor
{
    public int Order => order;

    public Task<NavigationInterceptorResult> InterceptNavigationAsync(
        string uri,
        object? viewModel,
        CancellationToken cancellationToken
    ) => handler(uri, viewModel, cancellationToken);
}
