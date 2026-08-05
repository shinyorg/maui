#if IOS || MACCATALYST
using System.Globalization;
using UIKit;

namespace Shiny.Navigation.Infrastructure;

internal static partial class TabBadgePlatform
{
    static partial void PlatformSet(int tabIndex, int value)
        => GetTabBarItem(tabIndex).BadgeValue = value.ToString(CultureInfo.InvariantCulture);


    static partial void PlatformClear(int tabIndex)
        => GetTabBarItem(tabIndex).BadgeValue = null;


    static UITabBarItem GetTabBarItem(int tabIndex)
    {
        var window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as UIWindow;
        var controller = FindTabBarController(window?.RootViewController)
            ?? throw new InvalidOperationException("Could not locate the native iOS tab bar controller");

        var items = controller.TabBar.Items;
        if (items == null || tabIndex >= items.Length)
            throw new InvalidOperationException($"Tab index '{tabIndex}' does not exist in the iOS tab bar");

        return items[tabIndex];
    }


    static UITabBarController? FindTabBarController(UIViewController? controller)
    {
        if (controller == null)
            return null;

        if (controller is UITabBarController tabBarController)
            return tabBarController;

        if (controller.PresentedViewController != null)
        {
            var presented = FindTabBarController(controller.PresentedViewController);
            if (presented != null)
                return presented;
        }

        if (controller is UINavigationController navigationController)
        {
            var navigationResult = FindTabBarController(navigationController.VisibleViewController ?? navigationController.TopViewController);
            if (navigationResult != null)
                return navigationResult;
        }

        foreach (var child in controller.ChildViewControllers)
        {
            var childResult = FindTabBarController(child);
            if (childResult != null)
                return childResult;
        }
        return null;
    }
}
#endif
