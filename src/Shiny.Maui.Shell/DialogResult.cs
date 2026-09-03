using System.Diagnostics.CodeAnalysis;

namespace Shiny;

/// <summary>
/// The outcome of a dialog shown with <see cref="INavigator.ShowDialog{TViewModel, T}"/> - either a
/// value the user selected, or cancellation.
/// </summary>
/// <remarks>
/// A distinct result type is used rather than <c>Task&lt;T&gt;</c> because <c>default(T)</c> cannot
/// express cancellation for value types - a <c>bool</c> dialog could not otherwise distinguish
/// "the user chose No" from "the user dismissed the dialog".
/// </remarks>
/// <param name="IsCancelled">True when the ViewModel raised <see cref="IDialogAware{T}.Cancelled"/> or the user dismissed the dialog without making a selection.</param>
/// <param name="Value">The selected value. Undefined when <paramref name="IsCancelled"/> is true.</param>
/// <typeparam name="T">The type of value the dialog returns.</typeparam>
public readonly record struct DialogResult<T>(bool IsCancelled, T? Value)
{
    /// <summary>A cancelled result carrying no value.</summary>
    public static DialogResult<T> Cancel() => new(true, default);

    /// <summary>A completed result carrying <paramref name="value"/>.</summary>
    public static DialogResult<T> Complete(T value) => new(false, value);

    /// <summary>
    /// Gets the selected value when the dialog was not cancelled.
    /// </summary>
    /// <param name="value">The selected value, when this returns true.</param>
    /// <returns>True when the dialog completed with a value; false when it was cancelled.</returns>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = this.Value;
        return !this.IsCancelled;
    }

    /// <summary>
    /// Returns the selected value, or <paramref name="fallback"/> when the dialog was cancelled.
    /// </summary>
    public T ValueOr(T fallback) => this.IsCancelled ? fallback : this.Value!;
}
