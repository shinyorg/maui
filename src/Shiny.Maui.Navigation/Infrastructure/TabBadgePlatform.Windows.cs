#if WINDOWS
using MauiControls = Microsoft.Maui.Controls;
using WinControls = Microsoft.UI.Xaml.Controls;
using WinMedia = Microsoft.UI.Xaml.Media;
using WinUI = Microsoft.UI.Xaml;

namespace Shiny.Navigation.Infrastructure;

internal static partial class TabBadgePlatform
{
    static partial void PlatformSet(int tabIndex, int value)
    {
        var item = GetNavigationItem(tabIndex);
        item.InfoBadge = new WinControls.InfoBadge
        {
            Value = value
        };
    }


    static partial void PlatformClear(int tabIndex)
    {
        var item = GetNavigationItem(tabIndex);
        item.InfoBadge = null;
    }


    static WinControls.NavigationViewItem GetNavigationItem(int tabIndex)
    {
        var window = MauiControls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as WinUI.Window;
        var root = window?.Content as WinUI.FrameworkElement
            ?? throw new InvalidOperationException("Could not locate the native Windows window");

        var navView = FindChild<WinControls.NavigationView>(root)
            ?? throw new InvalidOperationException("Could not locate the native Windows navigation view");

        var items = navView.GetDescendants()
            .OfType<WinControls.NavigationViewItem>()
            .ToList();

        if (tabIndex >= items.Count)
            throw new InvalidOperationException($"Tab index '{tabIndex}' does not exist in the Windows tab bar");

        return items[tabIndex];
    }


    static T? FindChild<T>(WinUI.FrameworkElement parent) where T : WinUI.FrameworkElement
    {
        if (parent is T typed)
            return typed;

        var count = WinMedia.VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            if (WinMedia.VisualTreeHelper.GetChild(parent, i) is not WinUI.FrameworkElement child)
                continue;

            var result = FindChild<T>(child);
            if (result != null)
                return result;
        }
        return null;
    }
}


internal static class WinUiVisualTreeExtensions
{
    public static IEnumerable<WinUI.DependencyObject> GetDescendants(this WinUI.DependencyObject parent)
    {
        var count = WinMedia.VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = WinMedia.VisualTreeHelper.GetChild(parent, i);
            yield return child;

            foreach (var descendant in child.GetDescendants())
                yield return descendant;
        }
    }
}
#endif
