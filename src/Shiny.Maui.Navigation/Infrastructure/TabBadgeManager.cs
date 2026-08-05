using Microsoft.Extensions.Logging;

namespace Shiny.Navigation.Infrastructure;

/// <summary>
/// Tracks the badge value per tab index and reapplies them after navigation. The reapply
/// exists because every platform drops badges when it recreates a tab's native view.
/// </summary>
public sealed class TabBadgeManager(
    ILogger<TabBadgeManager> logger,
    IMainThread mainThread
)
{
    readonly Dictionary<int, int> badgeValues = new();


    public Task Set(int tabIndex, int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Tab badge value must be 0 or greater");

        return mainThread.InvokeOnMainThreadAsync(() =>
        {
            TabBadgePlatform.Set(tabIndex, value);
            this.badgeValues[tabIndex] = value;
        });
    }


    public Task Clear(int tabIndex) => mainThread.InvokeOnMainThreadAsync(() =>
    {
        TabBadgePlatform.Clear(tabIndex);
        this.badgeValues.Remove(tabIndex);
    });


    public void ReapplyAll()
    {
        if (this.badgeValues.Count == 0)
            return;

        foreach (var badge in this.badgeValues)
        {
            try
            {
                TabBadgePlatform.Set(badge.Key, badge.Value);
            }
            catch (PlatformNotSupportedException ex)
            {
                logger.LogWarning(ex, "Tab badges are not supported on this platform");
                return;
            }
            catch (InvalidOperationException ex)
            {
                logger.LogDebug(ex, "Unable to reapply tab badge for tab '{index}'", badge.Key);
            }
        }
    }
}
