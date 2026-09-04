#if ANDROID
using Android.Content;
using Microsoft.Maui.LifecycleEvents;

namespace Shiny.Infrastructure;

public static partial class AppLinkLifecycle
{
    public static partial void Register(MauiAppBuilder builder)
        => builder.ConfigureLifecycleEvents(events => events.AddAndroid(android =>
        {
            // Cold start - the launch intent carries the link.
            android.OnCreate((activity, _) => DispatchIntent(activity.Intent));

            // Warm start - requires the activity to be SingleTop (the MAUI template default).
            android.OnNewIntent((_, intent) => DispatchIntent(intent));
        }));


    static void DispatchIntent(Intent? intent)
    {
        if (intent?.Action != Intent.ActionView)
            return;

        Dispatch(intent.DataString);
    }
}
#endif
