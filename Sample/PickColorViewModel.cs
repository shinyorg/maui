using Microsoft.Extensions.Logging;
using Shiny;

namespace Sample;

[ShellMap<PickColorPage>("PickColor")]
public partial class PickColorViewModel(ILogger<PickColorViewModel> logger)
    : ObservableObject, IDialogAware<string>, IPageLifecycleAware, IDisposable
{
    public event EventHandler<string>? Completed;
    public event EventHandler? Cancelled;

    [ShellProperty("The colour to pre-select when the dialog opens", required: false)]
    public string Preset { get; set; } = "Red";

    [ObservableProperty] string[] colors = ["Red", "Green", "Blue", "Violet"];

    [RelayCommand]
    void Pick(string color) => this.Completed?.Invoke(this, color);

    [RelayCommand]
    void Cancel() => this.Cancelled?.Invoke(this, EventArgs.Empty);

    public void OnAppearing() => logger.LogDebug("PickColorViewModel.OnAppearing - preset {preset}", this.Preset);
    public void OnDisappearing() => logger.LogDebug("PickColorViewModel.OnDisappearing");
    public void Dispose() => logger.LogDebug("PickColorViewModel.Dispose");
}
