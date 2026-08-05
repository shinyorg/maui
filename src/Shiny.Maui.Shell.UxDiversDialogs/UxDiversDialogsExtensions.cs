using UXDivers.Popups.Maui;

namespace Shiny;

public static class UxDiversDialogsExtensions
{
    /// <summary>
    /// Registers <see cref="UxDiversDialogs"/> as the <see cref="IDialogs"/> provider.
    /// Works with both <c>Shiny.Maui.Shell</c> and <c>Shiny.Maui.Navigation</c>.
    /// </summary>
    public static T UseUxDiversDialogs<T>(this T builder) where T : IShinyBuilder
    {
        builder.UseDialogs<UxDiversDialogs>();
        builder.MauiBuilder.UseUXDiversPopups();
        return builder;
    }
}
