using UXDivers.Popups.Maui;

namespace Shiny;

public static class UxDiversDialogsExtensions
{
    /// <summary>
    /// Registers <see cref="UxDiversDialogs"/> as the <see cref="IDialogs"/> provider, routing Shell
    /// alert/confirm/prompt/action-sheet calls through UXDivers Popups.
    /// </summary>
    public static ShinyAppBuilder UseUxDiversDialogs(this ShinyAppBuilder builder)
    {
        builder.UseDialogs<UxDiversDialogs>();
        EnsurePopups(builder);
        return builder;
    }


    /// <summary>
    /// Registers <see cref="UxDiversDialogPresenter"/> as the <see cref="IDialogPresenter"/>, so
    /// ViewModel dialogs shown with <see cref="INavigator.ShowDialog{TViewModel, T}"/> appear as a
    /// UXDivers popup over a dimmed backdrop instead of a Shell modal page.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure">Optional - backdrop, corner radius, max width, animation.</param>
    public static ShinyAppBuilder UseUxDiversDialogPresenter(this ShinyAppBuilder builder, Action<UxDiversDialogPresenterOptions>? configure = null)
    {
        var options = new UxDiversDialogPresenterOptions();
        configure?.Invoke(options);

        builder.MauiBuilder.Services.AddSingleton(options);
        EnsurePopups(builder);
        builder.UseDialogPresenter<UxDiversDialogPresenter>();
        return builder;
    }


    /// <summary>
    /// Initializes the UXDivers popup infrastructure once, however many of the pieces above an app
    /// opts into - it installs platform handlers (the Android back button among them), so calling it
    /// twice would install them twice.
    /// </summary>
    static void EnsurePopups(ShinyAppBuilder builder)
    {
        if (builder.MauiBuilder.Services.Any(x => x.ServiceType == typeof(PopupsInitialized)))
            return;

        builder.MauiBuilder.Services.AddSingleton<PopupsInitialized>();
        builder.MauiBuilder.UseUXDiversPopups();
    }


    sealed class PopupsInitialized;
}
