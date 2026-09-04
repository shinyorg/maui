using Microsoft.Extensions.Logging;
using Shiny;

namespace Sample;

[ShellMap<ModalDemoPage>("modal", appLinks: ["modal/{title}"])]
public partial class ModalDemoViewModel(
    ILogger<ModalDemoViewModel> logger,
    INavigator navigator,
    DialogEventLog log
) : ObservableObject, IPageLifecycleAware, INavigationAware, IDisposable
{
    [ObservableProperty] string dialogResult = "(none)";

    [ShellProperty(required: true)]
    public string Title { get; set; } = "Modal Page";

    [ShellProperty(required: false)]
    public string OptionalNote { get; set; } = "(none)";

    [RelayCommand]
    Task PushWithinModal() => navigator.NavigateTo<DetailViewModel>(
        x => x.Text = "Inside Modal"
    );

    [RelayCommand]
    Task Close() => navigator.GoBack();

    /// <summary>
    /// A dialog raised while a modal page is up - the overlay presenter has to target the modal, not
    /// the Shell page buried underneath it.
    /// </summary>
    [RelayCommand]
    async Task ShowDialogFromModal()
    {
        var result = await navigator.ShowPickColorDialog(preset: "Blue");
        this.DialogResult = result.IsCancelled ? "(cancelled)" : result.Value!;
        log.Add($"Dialog over a modal page -> {this.DialogResult}");
    }

    public void OnNavigatingFrom(IDictionary<string, object> parameters)
        => logger.LogDebug("ModalDemoViewModel.OnNavigatingFrom");

    public void OnAppearing() => logger.LogDebug("ModalDemoViewModel.OnAppearing");
    public void OnDisappearing() => logger.LogDebug("ModalDemoViewModel.OnDisappearing");
    public void Dispose() => logger.LogDebug("ModalDemoViewModel.Dispose");
}
