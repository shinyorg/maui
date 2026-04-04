namespace Shiny;


//https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/shell/navigation?view=net-maui-9.0
public static class MauiAppBuilderExtensions
{
    public static MauiAppBuilder UseShinyShell(this MauiAppBuilder builder, Action<ShinyAppBuilder> navBuilderAction)
    {
        var navBuilder = new ShinyAppBuilder(builder);
        navBuilderAction.Invoke(navBuilder);
        navBuilder.RegisterDependencies();

        if (!builder.Services.Any(x => x.ImplementationType == typeof(ShinyShellNavigator)))
        {
            builder.Services.AddSingleton<ShellServices>();
            builder.Services.AddSingleton<ShinyShellNavigator>();
            builder.Services.AddSingleton<IMauiInitializeService>(
                sp => sp.GetRequiredService<ShinyShellNavigator>()
            );

            if (navBuilder.CarPlayRegistration != null)
            {
                builder.Services.AddSingleton<ShellDialogs>();
#if IOS
                builder.Services.AddSingleton<ICarPlayContext, CarPlayContext>();
#else
                builder.Services.AddSingleton<ICarPlayContext, NullCarPlayContext>();
#endif
                builder.Services.AddSingleton<INavigator, ProxyNavigator>();
                builder.Services.AddSingleton<IDialogs, ProxyDialogs>();
            }
            else
            {
                builder.Services.AddSingleton<INavigator>(
                    sp => sp.GetRequiredService<ShinyShellNavigator>()
                );
                builder.Services.TryAddSingleton<IDialogs, ShellDialogs>();
            }

            builder.Services.AddSingleton(navBuilder);
        }

        return builder;
    }
}
