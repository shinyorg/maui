using Shiny;

namespace Sample;

[ShellMap<NavigationDemoPage>]
public partial class NavigationDemoViewModel(
    INavigator navigator,
    NavigationGuardSwitch guards
) : ObservableObject, IQueryAttributable, INavigationAware
{
    [ObservableProperty] string arg = "Hello";

    /// <summary>Drives <see cref="DetailGuardNavigationInterceptor"/> - cancels the push.</summary>
    public bool BlockDetail
    {
        get => guards.BlockDetail;
        set => this.SetProperty(guards.BlockDetail, value, guards, (g, v) => g.BlockDetail = v);
    }

    /// <summary>Drives <see cref="DetailGuardNavigationInterceptor"/> - redirects the push.</summary>
    public bool RedirectDetail
    {
        get => guards.RedirectDetail;
        set => this.SetProperty(guards.RedirectDetail, value, guards, (g, v) => g.RedirectDetail = v);
    }

    [NotifyPropertyChangedFor(nameof(HasBackResult))]
    [ObservableProperty] string? backResult;
    public bool HasBackResult => !string.IsNullOrWhiteSpace(BackResult);

    [RelayCommand]
    Task PushByRoute() => navigator.NavigateTo(
        nameof(DetailPage),
        args: [("Text", this.Arg)]
    );

    [RelayCommand]
    Task PushByViewModel() => navigator.NavigateTo<DetailViewModel>(
        args: [("Text", this.Arg)]
    );

    [RelayCommand]
    Task PushByViewModelConfigure() => navigator.NavigateTo<DetailViewModel>(
        x => x.Text = $"{this.Arg} (configured)"
    );

    [RelayCommand]
    Task GoBack() => navigator.GoBack();

    /// <summary>
    /// The escape hatch: this push runs even with the cancel switch on, because a guard's own
    /// navigation must not be guarded again.
    /// </summary>
    [RelayCommand]
    Task PushBypassingInterceptors() => navigator.NavigateTo<DetailViewModel>(
        x => x.Text = $"{this.Arg} (bypassed the guards)",
        bypassInterceptors: true
    );

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("BackArg", out var value))
            this.BackResult = value?.ToString();
    }

    public void OnNavigatingFrom(IDictionary<string, object> parameters) { }
}
