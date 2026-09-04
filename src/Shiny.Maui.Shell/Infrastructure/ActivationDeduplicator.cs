namespace Shiny.Infrastructure;


/// <summary>
/// Suppresses the same activation arriving twice in quick succession.
/// </summary>
/// <remarks>
/// Platforms can deliver one activation through more than one path - Android's
/// <c>OnCreate</c> re-runs with the original intent if the activity is recreated, and a
/// hand-forwarded call can overlap a hooked one. Without this, a single tap pushes the page twice.
/// The window is deliberately short: a user genuinely re-opening the same link a second later
/// should still be honoured.
/// </remarks>
public class ActivationDeduplicator(TimeSpan? window = null, Func<DateTimeOffset>? clock = null)
{
    readonly TimeSpan window = window ?? TimeSpan.FromSeconds(1);
    readonly Func<DateTimeOffset> clock = clock ?? (() => DateTimeOffset.UtcNow);
    readonly Lock sync = new();

    string? last;
    DateTimeOffset lastAt;


    /// <summary>
    /// Records the activation and reports whether it is a duplicate of the previous one.
    /// </summary>
    public bool IsDuplicate(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (this.sync)
        {
            var now = this.clock();
            var duplicate = this.last == key && (now - this.lastAt) < this.window;

            // Always refresh the timestamp: a rapid stream of the same activation stays
            // suppressed rather than slipping through once the original falls out of the window.
            this.last = key;
            this.lastAt = now;

            return duplicate;
        }
    }
}
