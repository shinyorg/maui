namespace Shiny;

public static class MauiAppBuilderExtensions
{
    extension(MauiAppBuilder builder)
    {
        /// <summary>
        /// Registers ViewModel-first navigation over plain MAUI pages - no Shell required.
        /// Declare your page/viewmodel map and the app's structure (root, tabs, flyout) in the
        /// callback; the library builds the page tree and assigns it to the window at startup,
        /// so your App class does not need to create a page at all.
        /// </summary>
        public MauiAppBuilder UseShinyNavigation(Action<ShinyNavigationBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            var navBuilder = new ShinyNavigationBuilder(builder);
            configure.Invoke(navBuilder);
            navBuilder.RegisterDependencies();

            if (builder.Services.Any(x => x.ImplementationType == typeof(ShinyNavigator)))
                return builder;

            builder.Services.AddSingleton(navBuilder);
            builder.Services.TryAddSingleton<IMainThread, MauiMainThread>();
            builder.Services.AddSingleton<NavigationHost>();
            builder.Services.AddSingleton<TabBadgeManager>();
            builder.Services.TryAddSingleton<IDialogs, NavigationDialogs>();

            builder.Services.AddSingleton<ShinyNavigator>();
            builder.Services.AddSingleton<INavigator>(sp => sp.GetRequiredService<ShinyNavigator>());
            builder.Services.AddSingleton<IMauiInitializeService>(sp => sp.GetRequiredService<ShinyNavigator>());

            return builder;
        }
    }
}
