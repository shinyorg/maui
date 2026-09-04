using UXDivers.Popups.Maui;

namespace Shiny;

/// <summary>
/// Appearance and behaviour of <see cref="UxDiversDialogPresenter"/>. Configure it with
/// <see cref="UxDiversDialogsExtensions.UseUxDiversDialogPresenter"/>.
/// </summary>
public class UxDiversDialogPresenterOptions
{
    /// <summary>Opacity of the dimmed backdrop behind the popup (0-1).</summary>
    public double BackdropOpacity { get; set; } = 0.5;

    /// <summary>
    /// Colour of the dimmed backdrop. Leave null to use the UXDivers theme's
    /// <c>PopupBackdropColor</c>.
    /// </summary>
    public Color? BackdropColor { get; set; }

    /// <summary>Tapping the backdrop dismisses the dialog, reporting cancellation to the caller.</summary>
    public bool DismissOnBackdropTap { get; set; } = true;

    /// <summary>Corner radius of the popup card.</summary>
    public double CornerRadius { get; set; } = 16;

    /// <summary>
    /// Background of the popup card. Leave null to use the UXDivers theme's <c>PopupBorderColor</c>,
    /// which is what their own popups are built on.
    /// </summary>
    public Color? CardBackgroundColor { get; set; }

    /// <summary>Widest the popup card will grow, regardless of the screen.</summary>
    public double MaxWidth { get; set; } = 420;

    /// <summary>Inset between the popup card and the edges of the screen.</summary>
    public Thickness Margin { get; set; } = new(24);

    /// <summary>Duration of the appearing/disappearing animation, in milliseconds.</summary>
    public int AnimationDuration { get; set; } = 220;

    /// <summary>Move the popup out of the way of the on-screen keyboard.</summary>
    public bool AvoidKeyboard { get; set; } = true;

    /// <summary>
    /// Runs against the popup just before it is pushed - the escape hatch for anything the options
    /// above don't cover, most usefully a different <c>AppearingAnimation</c>.
    /// </summary>
    public Action<PopupPage>? ConfigurePopup { get; set; }
}
