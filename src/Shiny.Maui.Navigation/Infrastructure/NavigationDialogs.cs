namespace Shiny.Navigation.Infrastructure;

/// <summary>
/// The default <see cref="IDialogs"/> provider - the native platform alert, prompt, and
/// action sheet, presented from whichever page is currently on screen.
/// </summary>
public class NavigationDialogs(IMainThread mainThread, NavigationHost host) : IDialogs
{
    public Task Alert(string? title, string message, string acceptText = "OK")
        => mainThread.InvokeOnMainThreadAsync(() =>
            this.RequirePage().DisplayAlertAsync(title, message, acceptText)
        );


    public Task<bool> Confirm(string? title, string message, string acceptText = "Yes", string cancelText = "No")
        => mainThread.InvokeOnMainThreadAsync(() =>
            this.RequirePage().DisplayAlertAsync(title, message, acceptText, cancelText)
        );


    public Task<string?> Prompt(
        string? title,
        string message,
        string acceptText = "OK",
        string cancelText = "Cancel",
        string? placeholder = null,
        string initialValue = "",
        int maxLength = -1,
        Keyboard? keyboard = null
    ) => mainThread.InvokeOnMainThreadAsync(() =>
        this.RequirePage().DisplayPromptAsync(
            title ?? String.Empty,
            message,
            acceptText,
            cancelText,
            placeholder,
            maxLength,
            keyboard,
            initialValue
        )
    );


    public Task<string> ActionSheet(string? title, string? cancel, string? destruction, params string[] buttons)
        => mainThread.InvokeOnMainThreadAsync(() =>
            this.RequirePage().DisplayActionSheetAsync(title, cancel, destruction, buttons)
        );


    Page RequirePage()
        => host.CurrentPage
            ?? throw new InvalidOperationException("There is no page on screen to present a dialog from");
}
