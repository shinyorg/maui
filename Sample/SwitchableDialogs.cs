using Shiny;
using Shiny.Infrastructure;

namespace Sample;

/// <summary>
/// Routes every <see cref="IDialogs"/> call to whichever provider the bench has selected.
/// </summary>
/// <remarks>
/// ViewModels take <see cref="IDialogs"/> in their constructor, so the instance they hold has to be
/// stable for the life of the app - the switch has to happen per call, inside a wrapper, rather than
/// by handing out a different implementation.
/// </remarks>
public class SwitchableDialogs(IServiceProvider services, DialogSwitch dialogSwitch) : IDialogs
{
    IDialogs Current => dialogSwitch.Provider switch
    {
        DialogProvider.ShinyControls => services.GetRequiredService<ShinyDialogs>(),
        DialogProvider.UxDivers => services.GetRequiredService<UxDiversDialogs>(),
        _ => services.GetRequiredService<ShellDialogs>()
    };


    public Task Alert(string? title, string message, string acceptText = "OK")
        => this.Current.Alert(title, message, acceptText);


    public Task<bool> Confirm(string? title, string message, string acceptText = "Yes", string cancelText = "No")
        => this.Current.Confirm(title, message, acceptText, cancelText);


    public Task<string?> Prompt(
        string? title,
        string message,
        string acceptText = "OK",
        string cancelText = "Cancel",
        string? placeholder = null,
        string initialValue = "",
        int maxLength = -1,
        Keyboard? keyboard = null
    ) => this.Current.Prompt(title, message, acceptText, cancelText, placeholder, initialValue, maxLength, keyboard);


    public Task<string> ActionSheet(string? title, string? cancel, string? destruction, params string[] buttons)
        => this.Current.ActionSheet(title, cancel, destruction, buttons);
}
