using System.Diagnostics.CodeAnalysis;

namespace Shiny;

/// <summary>
/// The common surface shared by <c>Shiny.Maui.Shell</c>'s <c>ShinyAppBuilder</c> and
/// <c>Shiny.Maui.Navigation</c>'s <c>ShinyNavigationBuilder</c>. Add-on packages (dialog
/// providers, for example) target this so a single extension method works with either
/// navigation library.
/// </summary>
public interface IShinyBuilder
{
    /// <summary>
    /// The underlying MAUI app builder, for registering platform services and other MAUI features
    /// </summary>
    MauiAppBuilder MauiBuilder { get; }

    /// <summary>
    /// Sets the dialog provider you want to use
    /// </summary>
    /// <typeparam name="TDialog">The <see cref="IDialogs"/> implementation to register</typeparam>
    void UseDialogs<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDialog
    >() where TDialog : class, IDialogs;
}
