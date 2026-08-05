using Microsoft.Extensions.Logging;

namespace SampleNav;

public class NavigationLogger(
    ILogger<NavigationLogger> logger,
    INavigator navigator
) : IMauiInitializeService
{
    public void Initialize(IServiceProvider services)
    {
        navigator.Navigating += (_, args) => logger.LogInformation(
            "Navigating: {From} -> {To} [{Type}]",
            args.FromViewModel?.GetType().Name,
            args.ToViewModelType?.Name,
            args.NavigationType
        );

        navigator.Navigated += (_, args) => logger.LogInformation(
            "Navigated: {To} [{Type}]",
            args.ToViewModel?.GetType().Name,
            args.NavigationType
        );
    }
}
