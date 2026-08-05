namespace SampleNav;

public partial class DetailViewModel(INavigator navigator) : ObservableObject, IDisposable
{
    [ObservableProperty] public partial int OrderId { get; set; }

    [RelayCommand]
    Task PushMore() => navigator.NavigateTo<DetailViewModel>(vm => vm.OrderId = this.OrderId + 1);

    // Watch the debug output on pop - the library disposes ViewModels when their page
    // leaves the tree.
    public void Dispose() => System.Diagnostics.Debug.WriteLine($"DetailViewModel {this.OrderId} disposed");
}
