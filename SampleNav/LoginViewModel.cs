namespace SampleNav;

public partial class LoginViewModel(INavigator navigator) : ObservableObject
{
    [RelayCommand]
    Task SignIn() => navigator.RestoreRoot();
}
