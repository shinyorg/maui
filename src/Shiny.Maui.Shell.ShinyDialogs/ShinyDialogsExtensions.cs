namespace Shiny;

public static class ShinyDialogsExtensions
{
    /// <summary>
    /// Registers <see cref="ShinyDialogs"/> as the <see cref="IDialogs"/> provider, routing
    /// Shell alert/confirm/prompt/action-sheet calls through the Shiny.Maui.Controls dialog service.
    /// </summary>
    /// <remarks>
    /// The underlying <c>IDialogService</c> is registered by <c>UseShinyControls()</c>, so ensure
    /// your <c>MauiAppBuilder</c> calls <c>UseShinyControls()</c> as well.
    /// </remarks>
    public static ShinyAppBuilder UseShinyDialogs(this ShinyAppBuilder builder)
    {
        builder.UseDialogs<ShinyDialogs>();
        return builder;
    }


    /// <summary>
    /// Registers <see cref="ShinyOverlayDialogPresenter"/> as the <see cref="IDialogPresenter"/>, so
    /// ViewModel dialogs shown with <see cref="INavigator.ShowDialog{TViewModel, T}"/> appear as a
    /// themed card over a dimmed backdrop instead of a Shell modal page.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure">Optional - backdrop, corner radius, max width, animation duration.</param>
    public static ShinyAppBuilder UseShinyDialogPresenter(this ShinyAppBuilder builder, Action<ShinyDialogPresenterOptions>? configure = null)
    {
        var options = new ShinyDialogPresenterOptions();
        configure?.Invoke(options);

        builder.MauiBuilder.Services.AddSingleton(options);
        builder.UseDialogPresenter<ShinyOverlayDialogPresenter>();
        return builder;
    }
}
