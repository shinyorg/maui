using UXDivers.Popups;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups.Services;
using Shiny.Infrastructure;

namespace Shiny;

public class UxDiversDialogs(IMainThread mainThread) : IDialogs
{
    public Task Alert(string? title, string message, string acceptText = "OK")
        => mainThread.InvokeOnMainThreadAsync(async () =>
        {
            var tcs = new TaskCompletionSource();
            var popup = new SimpleActionPopup
            {
                Title = title ?? string.Empty,
                Text = message,
                ActionButtonText = acceptText,
                ActionButtonCommand = new Command(async () =>
                {
                    await IPopupService.Current.PopAsync();
                    tcs.TrySetResult();
                }),
                ShowSecondaryActionButton = false
            };
            void OnPopupClosed(object? sender, PopupEventArgs e)
            {
                popup.PopupClosed -= OnPopupClosed;
                tcs.TrySetResult();
            }
            popup.PopupClosed += OnPopupClosed;
            await IPopupService.Current.PushAsync(popup);
            await tcs.Task;
        });


    public Task<bool> Confirm(string? title, string message, string acceptText = "Yes", string cancelText = "No")
        => mainThread.InvokeOnMainThreadAsync(async () =>
        {
            var tcs = new TaskCompletionSource<bool>();
            var popup = new SimpleActionPopup
            {
                Title = title ?? string.Empty,
                Text = message,
                ActionButtonText = acceptText,
                ActionButtonCommand = new Command(async () =>
                {
                    await IPopupService.Current.PopAsync();
                    tcs.TrySetResult(true);
                }),
                SecondaryActionButtonText = cancelText,
                SecondaryActionButtonCommand = new Command(async () =>
                {
                    await IPopupService.Current.PopAsync();
                    tcs.TrySetResult(false);
                })
            };
            void OnPopupClosed(object? sender, PopupEventArgs e)
            {
                popup.PopupClosed -= OnPopupClosed;
                tcs.TrySetResult(false);
            }
            popup.PopupClosed += OnPopupClosed;
            await IPopupService.Current.PushAsync(popup);
            return await tcs.Task;
        });


    public Task<string?> Prompt(
        string? title,
        string message,
        string acceptText = "OK",
        string cancelText = "Cancel",
        string? placeholder = null,
        string initialValue = "",
        int maxLength = -1,
        Keyboard? keyboard = null
    ) => mainThread.InvokeOnMainThreadAsync(async () =>
    {
        var field = new FormField
        {
            Placeholder = placeholder,
            Value = initialValue
        };
        var tcs = new TaskCompletionSource<string?>();
        var popup = new FormPopup
        {
            Title = title ?? string.Empty,
            Text = message,
            Items = new[] { field },
            ActionButtonText = acceptText,
            ActionButtonCommand = new Command(async () =>
            {
                var result = field.Value;
                await IPopupService.Current.PopAsync();
                tcs.TrySetResult(result);
            }),
            SecondaryActionLinkText = cancelText,
            SecondaryActionLinkCommand = new Command(async () =>
            {
                await IPopupService.Current.PopAsync();
                tcs.TrySetResult(null);
            })
        };
        void OnPopupClosed(object? sender, PopupEventArgs e)
        {
            popup.PopupClosed -= OnPopupClosed;
            tcs.TrySetResult(null);
        }
        popup.PopupClosed += OnPopupClosed;
        await IPopupService.Current.PushAsync(popup);
        return await tcs.Task;
    });


    public Task<string> ActionSheet(string? title, string? cancel, string? destruction, params string[] buttons)
        => mainThread.InvokeOnMainThreadAsync(async () =>
        {
            var tcs = new TaskCompletionSource<string>();
            var items = new List<OptionSheetItem>();

            foreach (var button in buttons)
            {
                items.Add(new OptionSheetItem
                {
                    Text = button,
                    Command = new Command(async () =>
                    {
                        await IPopupService.Current.PopAsync();
                        tcs.TrySetResult(button);
                    })
                });
            }

            if (destruction != null)
            {
                items.Add(new OptionSheetItem
                {
                    Text = destruction,
                    IconColor = Colors.Red,
                    Command = new Command(async () =>
                    {
                        await IPopupService.Current.PopAsync();
                        tcs.TrySetResult(destruction);
                    })
                });
            }

            var popup = new OptionSheetPopup
            {
                Title = title ?? string.Empty,
                Items = items
            };

            if (cancel != null)
            {
                popup.CloseButtonCommand = new Command(async () =>
                {
                    await IPopupService.Current.PopAsync();
                    tcs.TrySetResult(cancel);
                });
            }

            void OnPopupClosed(object? sender, PopupEventArgs e)
            {
                popup.PopupClosed -= OnPopupClosed;
                tcs.TrySetResult(cancel ?? string.Empty);
            }
            popup.PopupClosed += OnPopupClosed;
            await IPopupService.Current.PushAsync(popup);
            return await tcs.Task;
        });
}