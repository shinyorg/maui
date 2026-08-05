namespace Shiny;

/// <summary>
/// The <see cref="NavigationPage"/> used for every stack the library builds. Its only job is
/// to route the Android hardware/gesture back button through
/// <see cref="INavigationConfirmation"/> so a ViewModel can veto leaving.
/// </summary>
/// <remarks>
/// The iOS navigation bar's back arrow cannot be intercepted by MAUI. If a page must not be
/// left without confirmation, hide the back button
/// (<c>NavigationPage.SetHasBackButton(page, false)</c>) and give the user an explicit
/// toolbar action that calls <c>INavigator.GoBack()</c> - the guard runs there.
/// </remarks>
public class ShinyNavigationPage : NavigationPage
{
    public ShinyNavigationPage(Page root) : base(root) { }
    public ShinyNavigationPage() { }


    protected override bool OnBackButtonPressed()
    {
        // On the root page there is nothing to pop - let the platform do its thing
        // (exit the app on Android) rather than swallowing the press and trapping the user.
        if (this.Navigation.NavigationStack.Count <= 1)
            return base.OnBackButtonPressed();

        if (this.CurrentPage?.BindingContext is not INavigationConfirmation confirm)
            return base.OnBackButtonPressed();

        // Nothing about MAUI's back button is async, so swallow the press, ask the
        // viewmodel, and perform the pop ourselves if it says yes.
        this.Dispatcher.Dispatch(async () =>
        {
            var canNavigate = await confirm.CanNavigate().ConfigureAwait(true);
            if (canNavigate)
                await this.Navigation.PopAsync().ConfigureAwait(true);
        });
        return true;
    }
}
