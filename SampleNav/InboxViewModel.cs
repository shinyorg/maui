namespace SampleNav;

public partial class InboxViewModel(INavigator navigator) : ObservableObject, IPageLifecycleAware
{
    [ObservableProperty] public partial string Status { get; set; } = "";

    int appearCount;

    [RelayCommand]
    Task PushDetail() => navigator.NavigateTo<DetailViewModel>(vm => vm.OrderId = 99);

    public void OnAppearing() => this.Status = $"Appeared {++this.appearCount} time(s)";
    public void OnDisappearing() { }
}
