namespace SampleNav;

public partial class GuardedViewModel(IDialogs dialogs) : ObservableObject, INavigationConfirmation, INavigatingAway
{
    [ObservableProperty] public partial bool HasUnsavedChanges { get; set; } = true;

    public Task<bool> CanNavigate()
        => this.HasUnsavedChanges
            ? dialogs.Confirm("Unsaved changes", "Leave without saving?", "Leave", "Stay")
            : Task.FromResult(true);

    public void OnNavigatingAway() => System.Diagnostics.Debug.WriteLine("GuardedViewModel is being left");
}
