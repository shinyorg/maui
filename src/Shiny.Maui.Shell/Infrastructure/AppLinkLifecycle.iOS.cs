#if IOS || MACCATALYST
using Foundation;
using Microsoft.Maui.LifecycleEvents;

namespace Shiny.Infrastructure;

public static partial class AppLinkLifecycle
{
    public static partial void Register(MauiAppBuilder builder)
        => builder.ConfigureLifecycleEvents(events => events.AddiOS(ios =>
        {
            // Both the AppDelegate and the UISceneDelegate variants are hooked. MauiUISceneDelegate
            // raises ONLY the Scene-prefixed events - it does not forward to the AppDelegate ones -
            // so an app that declares UIApplicationSceneManifest gets nothing from the pair below.
            // iOS calls one delegate or the other, never both, so hooking both cannot double-deliver.

            // Custom schemes (myapp://...). MAUI does not forward these to the app itself.
            ios.OpenUrl((_, url, _) => Dispatch(url?.AbsoluteString));
            ios.SceneOpenUrl((_, urlContexts) =>
            {
                var handled = false;
                foreach (var context in urlContexts)
                    handled |= Dispatch(context.Url?.AbsoluteString);

                return handled;
            });

            // Universal links (https://...) arrive as a browsing-web user activity.
            ios.ContinueUserActivity((_, userActivity, _) => DispatchUserActivity(userActivity));
            ios.SceneContinueUserActivity((_, userActivity) => DispatchUserActivity(userActivity));
        }));


    static bool DispatchUserActivity(NSUserActivity? userActivity)
    {
        if (userActivity?.ActivityType != NSUserActivityType.BrowsingWeb)
            return false;

        return Dispatch(userActivity.WebPageUrl?.AbsoluteString);
    }
}
#endif
