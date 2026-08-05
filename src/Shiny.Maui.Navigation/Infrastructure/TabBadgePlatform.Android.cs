#if ANDROID
using Android.Views;
using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Tabs;
using AndroidApp = Microsoft.Maui.ApplicationModel.Platform;

namespace Shiny.Navigation.Infrastructure;

/// <summary>
/// Unlike Shell - which always renders a BottomNavigationView - a MAUI TabbedPage renders a
/// TabLayout at the top unless the app opts into bottom placement via
/// <c>AndroidSpecific.TabbedPage.SetToolbarPlacement(ToolbarPlacement.Bottom)</c>.
/// Both are handled here.
/// </summary>
internal static partial class TabBadgePlatform
{
    static partial void PlatformSet(int tabIndex, int value)
    {
        var root = GetRootView();

        if (FindChild<BottomNavigationView>(root) is { } bottomNav)
        {
            var menuItem = GetMenuItem(bottomNav, tabIndex);
            bottomNav.GetOrCreateBadge(menuItem.ItemId).Number = value;
            return;
        }

        GetTab(root, tabIndex).OrCreateBadge.Number = value;
    }


    static partial void PlatformClear(int tabIndex)
    {
        var root = GetRootView();

        if (FindChild<BottomNavigationView>(root) is { } bottomNav)
        {
            var menuItem = GetMenuItem(bottomNav, tabIndex);
            bottomNav.RemoveBadge(menuItem.ItemId);
            return;
        }

        GetTab(root, tabIndex).RemoveBadge();
    }


    static ViewGroup GetRootView()
        => AndroidApp.CurrentActivity?.Window?.DecorView as ViewGroup
            ?? throw new InvalidOperationException("Could not locate the native Android view hierarchy");


    static TabLayout.Tab GetTab(ViewGroup root, int tabIndex)
    {
        var tabLayout = FindChild<TabLayout>(root)
            ?? throw new InvalidOperationException("Could not locate the native Android tab bar");

        return tabLayout.GetTabAt(tabIndex)
            ?? throw new InvalidOperationException($"Tab index '{tabIndex}' does not exist in the Android tab bar");
    }


    static IMenuItem GetMenuItem(BottomNavigationView bottomNav, int tabIndex)
    {
        if (tabIndex >= bottomNav.Menu!.Size())
            throw new InvalidOperationException($"Tab index '{tabIndex}' does not exist in the Android tab bar");

        return bottomNav.Menu.GetItem(tabIndex)
            ?? throw new InvalidOperationException($"Could not locate Android tab menu item at index '{tabIndex}'");
    }


    static T? FindChild<T>(ViewGroup root) where T : Android.Views.View
    {
        if (root is T typed)
            return typed;

        for (var i = 0; i < root.ChildCount; i++)
        {
            var child = root.GetChildAt(i);
            if (child is T typedChild)
                return typedChild;

            if (child is ViewGroup group && FindChild<T>(group) is { } result)
                return result;
        }
        return null;
    }
}
#endif
