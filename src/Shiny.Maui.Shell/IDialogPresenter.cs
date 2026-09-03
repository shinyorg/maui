namespace Shiny;

/// <summary>
/// Controls how a dialog ViewModel's page is presented on screen for
/// <see cref="INavigator.ShowDialog{TViewModel, T}"/>. The navigator owns the result plumbing -
/// resolving, configuring, awaiting and tearing down - so a presenter only has to answer
/// "show this page, and tell me when it's gone".
/// </summary>
/// <remarks>
/// The default implementation (<see cref="Shiny.Infrastructure.ShellModalDialogPresenter"/>) pushes
/// the page onto Shell's modal stack. Replace it with
/// <see cref="ShinyAppBuilder.UseDialogPresenter{TPresenter}"/> to render dialogs as popups,
/// bottom sheets, or anything else that can host a <see cref="Page"/>.
/// </remarks>
public interface IDialogPresenter
{
    /// <summary>
    /// Presents <paramref name="page"/> and returns a task that completes once the page is no longer
    /// shown.
    /// </summary>
    /// <remarks>
    /// Implementations are responsible for dispatching to the main thread.
    ///
    /// The returned task must complete in both directions:
    /// <list type="bullet">
    /// <item>when the user dismisses the presentation (hardware back, an iOS modal swipe-down, a tap
    /// outside a popup) - this is how the navigator detects dismissal and reports cancellation;</item>
    /// <item>when <paramref name="dismiss"/> fires, meaning the ViewModel has produced its result and
    /// the presentation should be torn down.</item>
    /// </list>
    ///
    /// A presenter must <b>not</b> throw <see cref="OperationCanceledException"/> when
    /// <paramref name="dismiss"/> fires - token-driven teardown is the normal success path. Genuine
    /// presentation failures should still throw.
    /// </remarks>
    /// <param name="page">The page to present, with its <see cref="BindableObject.BindingContext"/> already set.</param>
    /// <param name="viewModel">The dialog ViewModel bound to <paramref name="page"/>, for presenters that need it (e.g. to read a title).</param>
    /// <param name="dismiss">Signals that the presentation should be torn down.</param>
    Task Present(Page page, object viewModel, CancellationToken dismiss);
}
