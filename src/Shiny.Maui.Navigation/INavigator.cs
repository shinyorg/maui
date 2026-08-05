namespace Shiny;

/// <summary>
/// ViewModel-first navigation over plain MAUI pages - no Shell, no routes, no URIs.
/// Every target is identified by its ViewModel type, which must be registered with
/// <see cref="ShinyNavigationBuilder"/>.
/// </summary>
public interface INavigator
{
    event EventHandler<NavigationEventArgs>? Navigating;
    event EventHandler<NavigatedEventArgs>? Navigated;

    /// <summary>The page currently visible to the user - the top of the modal stack, or the top of the active tab's stack</summary>
    Page? CurrentPage { get; }

    /// <summary>The BindingContext of <see cref="CurrentPage"/></summary>
    object? CurrentViewModel { get; }


    /// <summary>
    /// Creates a fluent builder for pushing several pages in one transaction. Only the last
    /// page animates, so the user sees a single transition rather than a stutter of pushes.
    /// </summary>
    INavigationBuilder CreateBuilder();


    /// <summary>
    /// Pushes the page mapped to <typeparamref name="TViewModel"/> onto the active navigation
    /// stack (the current tab's stack, or the modal stack when a modal is showing).
    /// </summary>
    /// <param name="configure">
    /// Runs against the DI-resolved viewmodel before the page is constructed, so every
    /// downstream hook - BindingContext assignment, OnAppearing - sees a fully initialised
    /// viewmodel. This replaces the string parameters/IQueryAttributable model entirely.
    /// </param>
    /// <param name="animated">Whether to animate the transition</param>
    Task NavigateTo<TViewModel>(Action<TViewModel>? configure = null, bool animated = true) where TViewModel : class;


    /// <summary>
    /// Non-generic <see cref="NavigateTo{TViewModel}"/>, for callers holding the ViewModel type
    /// at runtime - XAML navigation and dynamic/AI-driven navigation.
    /// </summary>
    Task NavigateTo(Type viewModelType, bool animated = true);


    /// <summary>
    /// Replaces the active navigation stack entirely, making the page mapped to
    /// <typeparamref name="TViewModel"/> its new root. This is the flyout-menu move - the
    /// user picks a destination and the back stack starts over from there.
    /// </summary>
    Task NavigateToRoot<TViewModel>(Action<TViewModel>? configure = null, bool animated = true) where TViewModel : class;


    /// <summary>Non-generic <see cref="NavigateToRoot{TViewModel}"/></summary>
    Task NavigateToRoot(Type viewModelType, bool animated = true);


    /// <summary>
    /// Pushes the page mapped to <typeparamref name="TViewModel"/> onto the modal stack.
    /// </summary>
    /// <param name="wrapInNavigationPage">
    /// When true (default) the modal is wrapped in a <see cref="NavigationPage"/>, so it gets a
    /// title bar and can push further pages of its own.
    /// </param>
    Task PushModal<TViewModel>(Action<TViewModel>? configure = null, bool animated = true, bool wrapInNavigationPage = true) where TViewModel : class;


    /// <summary>Non-generic <see cref="PushModal{TViewModel}"/></summary>
    Task PushModal(Type viewModelType, bool animated = true, bool wrapInNavigationPage = true);


    /// <summary>Pops the top page off the modal stack</summary>
    Task PopModal(bool animated = true);


    /// <summary>Navigates back one page</summary>
    Task GoBack(bool animated = true);


    /// <summary>Navigates back <paramref name="backCount"/> pages</summary>
    Task GoBack(int backCount, bool animated = true);


    /// <summary>Returns to the root of the active navigation stack, however deep you are</summary>
    Task PopToRoot(bool animated = true);


    /// <summary>Switches to the tab hosting <typeparamref name="TViewModel"/>. That tab keeps its own back stack.</summary>
    Task SelectTab<TViewModel>() where TViewModel : class;


    /// <summary>Non-generic <see cref="SelectTab{TViewModel}"/></summary>
    Task SelectTab(Type viewModelType);


    /// <summary>Sets a numeric badge on the tab hosting <typeparamref name="TViewModel"/></summary>
    Task SetTabBadge<TViewModel>(int value) where TViewModel : class;


    /// <summary>Clears the badge from the tab hosting <typeparamref name="TViewModel"/></summary>
    Task ClearTabBadge<TViewModel>() where TViewModel : class;


    /// <summary>Whether the app declares a flyout at all</summary>
    bool HasFlyout { get; }


    /// <summary>Opens the flyout drawer</summary>
    Task OpenFlyout();


    /// <summary>Closes the flyout drawer</summary>
    Task CloseFlyout();


    /// <summary>
    /// Replaces the entire window page with a fresh single-page root - the login-screen move.
    /// Call <see cref="RestoreRoot"/> to rebuild the structure you declared at startup.
    /// </summary>
    Task SwitchRoot<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : class;


    /// <summary>
    /// Rebuilds the window page from the structure declared with <see cref="ShinyNavigationBuilder"/>,
    /// discarding all existing navigation state. The counterpart to <see cref="SwitchRoot{TViewModel}"/>.
    /// </summary>
    Task RestoreRoot();
}
