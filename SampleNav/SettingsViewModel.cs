namespace SampleNav;

public partial class SettingsViewModel(INavigator navigator) : ObservableObject
{
    [RelayCommand]
    Task Restore() => navigator.RestoreRoot();
}
