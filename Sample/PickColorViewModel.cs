using Shiny;

namespace Sample;

[ShellMap<PickColorPage>("PickColor")]
public partial class PickColorViewModel(INavigator navigator, DialogEventLog log)
    : ObservableObject, IDialogAware<string>, IPageLifecycleAware, IDisposable
{
    public event EventHandler<string>? Completed;
    public event EventHandler? Cancelled;

    [ShellProperty("The colour to pre-select when the dialog opens", required: false)]
    public string Preset { get; set; } = "Red";

    [ObservableProperty] string[] colors = ["Red", "Green", "Blue", "Violet"];

    [ObservableProperty] string nestedResult = "(none)";

    [RelayCommand]
    void Pick(string color) => this.Completed?.Invoke(this, color);

    [RelayCommand]
    void Cancel() => this.Cancelled?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// A dialog raised from inside a dialog - stacked modals for the default presenter, stacked
    /// overlays or popups for the other two.
    /// </summary>
    [RelayCommand]
    async Task ShowNested()
    {
        log.Add("PickColorViewModel: opening a nested dialog");
        var result = await navigator.ShowLongTextDialog();
        this.NestedResult = result.IsCancelled ? "(cancelled)" : result.Value!;
        log.Add($"PickColorViewModel: nested dialog -> {this.NestedResult}");
    }

    public void OnAppearing() => log.Add($"PickColorViewModel.OnAppearing - preset {this.Preset}");
    public void OnDisappearing() => log.Add("PickColorViewModel.OnDisappearing");
    public void Dispose() => log.Add("PickColorViewModel.Dispose");
}
