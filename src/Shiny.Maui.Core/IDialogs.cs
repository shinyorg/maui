namespace Shiny;

public interface IDialogs
{
    /// <summary>
    /// Displays an alert dialog with a title, message, and an accept button.
    /// </summary>
    /// <remarks>This method is typically used to display informational messages or warnings to the user. The
    /// dialog will not close until the user interacts with the accept button.</remarks>
    /// <param name="title">The title of the alert dialog. Can be <see langword="null"/> to omit the title.</param>
    /// <param name="message">The message to display in the alert dialog. This parameter is required.</param>
    /// <param name="acceptText">The text for the accept button. Defaults to "OK" if not specified.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation of displaying the alert.</returns>
    Task Alert(string? title, string message, string acceptText = "OK");


    /// <summary>
    /// Displays a confirmation dialog with the specified title, message, and button text,  and returns the user's
    /// response.
    /// </summary>
    /// <param name="title">The title of the confirmation dialog. Can be <see langword="null"/> to omit the title.</param>
    /// <param name="message">The message displayed in the confirmation dialog. This parameter is required.</param>
    /// <param name="acceptText">The text for the confirmation button. Defaults to "Yes" if not specified.</param>
    /// <param name="cancelText">The text for the cancellation button. Defaults to "No" if not specified.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the user
    /// selects the confirmation button; otherwise, <see langword="false"/>.</returns>
    Task<bool> Confirm(string? title, string message, string acceptText = "Yes", string cancelText = "No");


    /// <summary>
    /// Displays a prompt dialog that allows the user to enter text input.
    /// </summary>
    /// <param name="title">The title of the prompt dialog. Can be <see langword="null"/> to omit the title.</param>
    /// <param name="message">The message displayed in the prompt dialog. This parameter is required.</param>
    /// <param name="acceptText">The text for the accept button. Defaults to "OK" if not specified.</param>
    /// <param name="cancelText">The text for the cancel button. Defaults to "Cancel" if not specified.</param>
    /// <param name="placeholder">The placeholder text displayed in the input field when it is empty. Can be <see langword="null"/>.</param>
    /// <param name="initialValue">The initial value pre-filled in the input field. Defaults to an empty string.</param>
    /// <param name="maxLength">The maximum number of characters allowed in the input field. Defaults to -1 (no limit).</param>
    /// <param name="keyboard">The type of keyboard to display for the input field. Defaults to <see langword="null"/> (default keyboard).</param>
    /// <returns>A task that represents the asynchronous operation. The task result is the text entered by the user,
    /// or <see langword="null"/> if the user cancelled the dialog.</returns>
    Task<string?> Prompt(
        string? title,
        string message,
        string acceptText = "OK",
        string cancelText = "Cancel",
        string? placeholder = null,
        string initialValue = "",
        int maxLength = -1,
        Keyboard? keyboard = null
    );


    /// <summary>
    /// Displays an action sheet that allows the user to choose from a set of options.
    /// </summary>
    /// <param name="title">The title of the action sheet. Can be <see langword="null"/> to omit the title.</param>
    /// <param name="cancel">The text for the cancel button. Can be <see langword="null"/> to omit.</param>
    /// <param name="destruction">The text for a destructive action button (displayed in red on some platforms). Can be <see langword="null"/> to omit.</param>
    /// <param name="buttons">The array of action button labels to display.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is the text of the button
    /// selected by the user.</returns>
    Task<string> ActionSheet(string? title, string? cancel, string? destruction, params string[] buttons);
}
