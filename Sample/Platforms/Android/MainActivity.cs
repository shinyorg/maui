using Android.App;
using Android.Content.PM;
using Android.Content;
using Android.OS;

namespace Sample;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
// App links. The library installs the runtime hooks through UseAppLinks(), but the intent filters
// have to live here: the merged manifest names this activity with a CRC64 hash of its namespace,
// so no build-generated manifest overlay can target it.
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "shinyshell" }
)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "https",
    DataHosts = new[] { "shinylib.net", "www.shinylib.net" },
    AutoVerify = true
)]
public class MainActivity : MauiAppCompatActivity
{
}