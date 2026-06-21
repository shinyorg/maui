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
}
