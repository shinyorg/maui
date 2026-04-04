namespace Shiny.Infrastructure;

public class ProxyDialogs : IDialogs
{
    readonly ShellDialogs shellDialogs;
    readonly ICarPlayContext carPlayContext;
    readonly IServiceProvider services;
    readonly CarPlayRegistration registration;
    IDialogs? carPlayDialogs;

    public ProxyDialogs(
        ShellDialogs shellDialogs,
        ICarPlayContext carPlayContext,
        IServiceProvider services,
        CarPlayRegistration registration
    )
    {
        this.shellDialogs = shellDialogs;
        this.carPlayContext = carPlayContext;
        this.services = services;
        this.registration = registration;
    }

    IDialogs Current => this.carPlayContext.IsConnected
        ? this.carPlayDialogs ??= (IDialogs)this.services.GetRequiredService(this.registration.DialogsType)
        : this.shellDialogs;

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
