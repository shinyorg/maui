using Shiny;
using Shiny.Infrastructure;

namespace Sample;

/// <summary>
/// Routes <see cref="INavigator.ShowDialog{TViewModel, T}"/> to whichever
/// <see cref="IDialogPresenter"/> the bench has selected.
/// </summary>
/// <remarks>
/// Worth noting that this needs nothing from the library beyond the interface itself: if a bench
/// like this could not be written without library changes, the presenter abstraction would be wrong.
/// </remarks>
public class SwitchableDialogPresenter(IServiceProvider services, DialogSwitch dialogSwitch) : IDialogPresenter
{
    public Task Present(Page page, object viewModel, CancellationToken dismiss)
    {
        var presenter = dialogSwitch.Presenter switch
        {
            DialogPresenterKind.ShinyOverlay => services.GetRequiredService<ShinyOverlayDialogPresenter>(),
            DialogPresenterKind.UxDiversPopup => services.GetRequiredService<UxDiversDialogPresenter>(),
            _ => (IDialogPresenter)services.GetRequiredService<ShellModalDialogPresenter>()
        };
        return presenter.Present(page, viewModel, dismiss);
    }
}
