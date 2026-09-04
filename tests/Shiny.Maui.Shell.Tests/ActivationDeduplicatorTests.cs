using Shiny.Infrastructure;
using Shouldly;

namespace Shiny.Maui.Shell.Tests;

/// <summary>
/// The one testable piece of the activation path - the platform hooks themselves cannot be
/// exercised from a plain test host.
/// </summary>
public class ActivationDeduplicatorTests
{
    static (ActivationDeduplicator Dedupe, Action<TimeSpan> Advance) Build(TimeSpan? window = null)
    {
        var now = DateTimeOffset.UnixEpoch;
        var dedupe = new ActivationDeduplicator(window ?? TimeSpan.FromSeconds(1), () => now);
        return (dedupe, span => now = now.Add(span));
    }


    [Fact]
    public void FirstActivation_IsNeverDuplicate()
    {
        var (dedupe, _) = Build();

        dedupe.IsDuplicate("myapp://detail/1").ShouldBeFalse();
    }


    [Fact]
    public void SameActivationTwice_IsSuppressed()
    {
        var (dedupe, _) = Build();
        dedupe.IsDuplicate("myapp://detail/1");

        dedupe.IsDuplicate("myapp://detail/1").ShouldBeTrue();
    }


    [Fact]
    public void DifferentActivation_IsNotSuppressed()
    {
        var (dedupe, _) = Build();
        dedupe.IsDuplicate("myapp://detail/1");

        dedupe.IsDuplicate("myapp://detail/2").ShouldBeFalse();
    }


    [Fact]
    public void SameActivationAfterTheWindow_IsAllowedThrough()
    {
        // Re-opening the same link a moment later is a real user action, not a double delivery
        var (dedupe, advance) = Build();
        dedupe.IsDuplicate("myapp://detail/1");

        advance(TimeSpan.FromSeconds(2));

        dedupe.IsDuplicate("myapp://detail/1").ShouldBeFalse();
    }


    [Fact]
    public void RapidRepeats_StaySuppressed()
    {
        // The timestamp refreshes on every call, so a stream of duplicates cannot slip through
        // once the original falls out of the window.
        var (dedupe, advance) = Build(TimeSpan.FromSeconds(1));
        dedupe.IsDuplicate("myapp://detail/1");

        for (var i = 0; i < 5; i++)
        {
            advance(TimeSpan.FromMilliseconds(900));
            dedupe.IsDuplicate("myapp://detail/1").ShouldBeTrue();
        }
    }


    [Fact]
    public void AlternatingActivations_AreBothAllowed()
    {
        var (dedupe, _) = Build();

        dedupe.IsDuplicate("a").ShouldBeFalse();
        dedupe.IsDuplicate("b").ShouldBeFalse();
        dedupe.IsDuplicate("a").ShouldBeFalse();
    }
}
