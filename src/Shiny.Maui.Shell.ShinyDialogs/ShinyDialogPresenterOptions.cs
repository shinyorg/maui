namespace Shiny;

/// <summary>
/// Appearance and behaviour of <see cref="ShinyOverlayDialogPresenter"/>. Configure it with
/// <see cref="ShinyDialogsExtensions.UseShinyDialogPresenter"/>.
/// </summary>
public class ShinyDialogPresenterOptions
{
    /// <summary>Opacity of the dimmed backdrop behind the dialog (0-1).</summary>
    public double BackdropOpacity { get; set; } = 0.5;

    /// <summary>
    /// Colour of the dimmed backdrop. Leave null to follow the active Shiny theme's scrim.
    /// </summary>
    public Color? BackdropColor { get; set; }

    /// <summary>Tapping the backdrop dismisses the dialog, reporting cancellation to the caller.</summary>
    public bool DismissOnBackdropTap { get; set; } = true;

    /// <summary>Corner radius of the dialog card.</summary>
    public double CornerRadius { get; set; } = 16;

    /// <summary>
    /// Background of the dialog card. Leave null to follow the active Shiny theme's surface colour.
    /// </summary>
    public Color? CardBackgroundColor { get; set; }

    /// <summary>Widest the dialog card will grow, regardless of the screen.</summary>
    public double MaxWidth { get; set; } = 420;

    /// <summary>Inset between the dialog card and the edges of the page.</summary>
    public Thickness Margin { get; set; } = new(24);

    /// <summary>Duration of the fade/scale in and out, in milliseconds.</summary>
    public uint AnimationDuration { get; set; } = 220;

    /// <summary>
    /// Runs against the card just before it is shown - the escape hatch for anything the options
    /// above don't cover (a border, a different shadow, a fixed width).
    /// </summary>
    public Action<Border>? ConfigureCard { get; set; }
}
