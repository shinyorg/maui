namespace Shiny;

public static class ShinyDialogsExtensions
{
    /// <summary>
    /// Registers <see cref="ShinyDialogs"/> as the <see cref="IDialogs"/> provider, routing
    /// alert/confirm/prompt/action-sheet calls through the Shiny.Maui.Controls dialog service.
    /// Works with both <c>Shiny.Maui.Shell</c> and <c>Shiny.Maui.Navigation</c>.
    /// </summary>
    /// <remarks>
    /// The underlying <c>IDialogService</c> is registered by <c>UseShinyControls()</c>, so ensure
    /// your <c>MauiAppBuilder</c> calls <c>UseShinyControls()</c> as well.
    /// </remarks>
    public static T UseShinyDialogs<T>(this T builder) where T : IShinyBuilder
    {
        builder.UseDialogs<ShinyDialogs>();
        return builder;
    }
}
