#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS
namespace Shiny.Navigation.Infrastructure;

internal static partial class TabBadgePlatform
{
    static partial void PlatformSet(int tabIndex, int value)
        => throw new PlatformNotSupportedException("Tab badges are only supported on Android, iOS, Mac Catalyst, and Windows");

    static partial void PlatformClear(int tabIndex)
        => throw new PlatformNotSupportedException("Tab badges are only supported on Android, iOS, Mac Catalyst, and Windows");
}
#endif
