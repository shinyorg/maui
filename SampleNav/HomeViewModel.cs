namespace SampleNav;

public partial class HomeViewModel(INavigator navigator, IDialogs dialogs) : ObservableObject, IPageLifecycleAware
{
    [ObservableProperty] public partial string LastResult { get; set; } = "";

    // The configure callback is the whole parameter story - no keys, no IQueryAttributable.
    [RelayCommand]
    Task PushDetail() => navigator.NavigateTo<DetailViewModel>(vm => vm.OrderId = 42);

    [RelayCommand]
    Task PushChain() => navigator
        .CreateBuilder()
        .Add<DetailViewModel>(vm => vm.OrderId = 1)
        .Add<DetailViewModel>(vm => vm.OrderId = 2)
        .Add<GuardedViewModel>()
        .Navigate();

    [RelayCommand]
    Task PushGuarded() => navigator.NavigateTo<GuardedViewModel>();

    [RelayCommand]
    Task BadgeInbox() => navigator.SetTabBadge<InboxViewModel>(3);

    [RelayCommand]
    Task ClearInboxBadge() => navigator.ClearTabBadge<InboxViewModel>();

    [RelayCommand]
    Task GoLogin() => navigator.SwitchRoot<LoginViewModel>();

    [RelayCommand]
    async Task ShowConfirm()
    {
        var result = await dialogs.Confirm("Confirm", "Does this look right?");
        this.LastResult = $"Confirm returned {result}";
    }

    public void OnAppearing() => this.LastResult = "";
    public void OnDisappearing() { }
}
