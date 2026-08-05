using Microsoft.Extensions.Logging;

namespace SampleNav;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp
            .CreateBuilder()
            .UseMauiApp<App>()
            .UseShinyNavigation(x => x
                .AddFlyout(f => f
                    .Menu<MenuPage, MenuViewModel>("Shiny Nav")
                    .AddTabs(t => t
                        .Add<HomePage, HomeViewModel>("Home")
                        .Add<InboxPage, InboxViewModel>("Inbox")
                        .Add<SettingsPage, SettingsViewModel>("Settings")
                    )
                )
                // pushable / modal targets - not part of the structure
                .Add<DetailPage, DetailViewModel>()
                .Add<ModalPage, ModalViewModel>()
                .Add<GuardedPage, GuardedViewModel>()
                .Add<LoginPage, LoginViewModel>()
            )
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<IMauiInitializeService, NavigationLogger>();

#if DEBUG
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
