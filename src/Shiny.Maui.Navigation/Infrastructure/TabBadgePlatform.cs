namespace Shiny.Navigation.Infrastructure;

internal static partial class TabBadgePlatform
{
    public static void Set(int tabIndex, int value)
    {
        if (tabIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(tabIndex));
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value));

        PlatformSet(tabIndex, value);
    }


    public static void Clear(int tabIndex)
    {
        if (tabIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(tabIndex));

        PlatformClear(tabIndex);
    }


    static partial void PlatformSet(int tabIndex, int value);
    static partial void PlatformClear(int tabIndex);
}
