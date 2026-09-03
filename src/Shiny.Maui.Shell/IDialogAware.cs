namespace Shiny;

/// <summary>
/// Implemented by a ViewModel that can be presented as a dialog by
/// <see cref="INavigator.ShowDialog{TViewModel, T}"/> and return a value of
/// <typeparamref name="T"/> to the awaiting caller.
/// </summary>
/// <remarks>
/// The ViewModel raises exactly one of the two events to close the dialog. If neither is raised and
/// the user dismisses the dialog by other means (hardware back, an iOS swipe-down, a tap outside a
/// popup), the awaiting call still completes - as a cancellation. Raising an event more than once is
/// harmless; the first one wins.
/// <code>
/// [ShellMap&lt;PickColorPage&gt;("PickColor")]
/// public partial class PickColorViewModel : ObservableObject, IDialogAware&lt;string&gt;
/// {
///     public event EventHandler&lt;string&gt;? Completed;
///     public event EventHandler? Cancelled;
///
///     [RelayCommand] void Pick(string colour) => this.Completed?.Invoke(this, colour);
///     [RelayCommand] void Cancel() => this.Cancelled?.Invoke(this, EventArgs.Empty);
/// }
/// </code>
/// </remarks>
/// <typeparam name="T">The type of value this dialog returns.</typeparam>
public interface IDialogAware<out T>
{
    /// <summary>
    /// Raised by the ViewModel when the user has made a selection. The dialog is torn down and the
    /// value is returned to the awaiting <see cref="INavigator.ShowDialog{TViewModel, T}"/> caller.
    /// </summary>
    event EventHandler<T> Completed;

    /// <summary>
    /// Raised by the ViewModel when the user has explicitly cancelled. The dialog is torn down and
    /// the awaiting caller receives a <see cref="DialogResult{T}"/> with
    /// <see cref="DialogResult{T}.IsCancelled"/> set.
    /// </summary>
    event EventHandler Cancelled;
}
