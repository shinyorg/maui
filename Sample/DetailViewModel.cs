using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Shiny;

namespace Sample;

[ShellMap<DetailPage>(appLinks: ["detail/{text}"])]
[global::System.ComponentModel.Description("This is a test generation")]
public partial class DetailViewModel(
    ILogger<DetailViewModel> logger,
    INavigator navigator,
    IDialogs dialogs
) : ObservableObject, IQueryAttributable, IPageLifecycleAware, INavigationConfirmation, INavigationAware, IDisposable
{
    [ShellProperty("This is required Text to render", required: true)]
    public string Text { get; set; } = string.Empty;

    [ShellProperty("Optional line to highlight - demonstrates typed app link conversion", required: false)]
    public int Highlight { get; set; }

    [ObservableProperty] string source = "unknown";
    [ObservableProperty] bool requireConfirmation;

    [RelayCommand]
    Task GoBack() => navigator.GoBack();

    [RelayCommand]
    Task GoBackWithArg() => navigator.GoBack(("BackArg", "Returned from Detail"));

    [RelayCommand]
    Task PopToRoot() => navigator.PopToRoot();

    [RelayCommand]
    Task PushAnother() => navigator.NavigateTo<DetailViewModel>(
        x => x.Text = "Pushed from Detail"
    );

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Text", out var value))
            this.Text = value?.ToString() ?? string.Empty;

        this.Source = query.ContainsKey("Text") ? "query parameter" : "ViewModel configure";
        this.OnPropertyChanged(nameof(Text));
    }

    public async Task<bool> CanNavigate()
    {
        if (!RequireConfirmation)
            return true;

        return await dialogs.Confirm("Leave Page?", "Are you sure you want to navigate away?");
    }

    public void OnNavigatingFrom(IDictionary<string, object> parameters)
    {
        if (!parameters.ContainsKey("BackArg"))
            parameters["BackArg"] = "Set by OnNavigatingFrom";

        logger.LogDebug("DetailViewModel.OnNavigatingFrom");
    }

    public void OnAppearing() => logger.LogDebug("DetailViewModel.OnAppearing");
    public void OnDisappearing() => logger.LogDebug("DetailViewModel.OnDisappearing");
    public void Dispose() => logger.LogDebug("DetailViewModel.Dispose");
}
