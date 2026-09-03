using Shiny;

namespace Sample;

[ShellMap<DialogDemoPage>]
public partial class DialogDemoViewModel(IDialogs dialogs, INavigator navigator) : ObservableObject
{
    [ObservableProperty] string lastResult = "(none)";

    [RelayCommand]
    async Task ShowAlert()
    {
        await dialogs.Alert("Alert", "This is a simple alert dialog.");
        this.LastResult = "Alert dismissed";
    }

    [RelayCommand]
    async Task ShowConfirm()
    {
        var result = await dialogs.Confirm("Confirm", "Do you agree?");
        this.LastResult = $"Confirm: {result}";
    }

    [RelayCommand]
    async Task ShowPrompt()
    {
        var result = await dialogs.Prompt("Prompt", "What is your name?", placeholder: "Name");
        this.LastResult = result != null ? $"Prompt: {result}" : "Prompt: (cancelled)";
    }

    [RelayCommand]
    async Task ShowActionSheet()
    {
        var result = await dialogs.ActionSheet("Pick an Option", "Cancel", "Delete", "Edit", "Share", "Copy");
        this.LastResult = $"ActionSheet: {result}";
    }

    // ShowPickColorDialog is source generated from [ShellMap<PickColorPage>] + IDialogAware<string>
    // on PickColorViewModel - no type arguments, and [ShellProperty] values become parameters
    [RelayCommand]
    async Task ShowViewModelDialog()
    {
        var result = await navigator.ShowPickColorDialog(preset: "Violet");
        this.LastResult = result.IsCancelled
            ? "Colour dialog: (cancelled)"
            : $"Colour dialog: {result.Value}";
    }
}
