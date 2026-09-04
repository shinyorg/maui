namespace Sample;

/// <summary>Which <c>IDialogs</c> provider the bench is currently exercising.</summary>
public enum DialogProvider
{
    Shell,
    ShinyControls,
    UxDivers
}

/// <summary>Which <c>IDialogPresenter</c> the bench is currently exercising.</summary>
public enum DialogPresenterKind
{
    ShellModal,
    ShinyOverlay,
    UxDiversPopup
}

/// <summary>
/// The bench's current selection. Both <c>IDialogs</c> and <c>IDialogPresenter</c> are chosen once,
/// at DI registration, so exercising all six in one app means routing through this rather than
/// re-registering - see <see cref="SwitchableDialogs"/> and <see cref="SwitchableDialogPresenter"/>.
/// </summary>
/// <remarks>
/// Persisted, so a restart keeps the selection - which matters when the thing being tested is what
/// happens on a cold start, or on a platform where redeploying is the only way back to the page.
/// </remarks>
public partial class DialogSwitch : ObservableObject
{
    const string ProviderKey = "bench.dialogs.provider";
    const string PresenterKey = "bench.dialogs.presenter";

    public DialogSwitch()
    {
        this.provider = Read(ProviderKey, DialogProvider.UxDivers);
        this.presenter = Read(PresenterKey, DialogPresenterKind.UxDiversPopup);
    }

    [ObservableProperty] DialogProvider provider;
    [ObservableProperty] DialogPresenterKind presenter;

    partial void OnProviderChanged(DialogProvider value) => Preferences.Set(ProviderKey, value.ToString());
    partial void OnPresenterChanged(DialogPresenterKind value) => Preferences.Set(PresenterKey, value.ToString());

    static T Read<T>(string key, T fallback) where T : struct, Enum
        => Enum.TryParse<T>(Preferences.Get(key, null), out var value) ? value : fallback;
}
