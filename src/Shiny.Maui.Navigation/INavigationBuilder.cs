namespace Shiny;

/// <summary>
/// Builds a multi-page navigation and executes it as one transaction. Only the final push
/// animates, so a three-deep navigation looks like a single transition to the user.
/// </summary>
public interface INavigationBuilder
{
    /// <summary>
    /// Pops <paramref name="count"/> pages before pushing anything. Must be called before any Add.
    /// </summary>
    INavigationBuilder PopBack(int count = 1);


    /// <summary>Clears the active stack before pushing, making the first Add the new root</summary>
    INavigationBuilder FromRoot();


    /// <summary>Adds a page to push, identified by its ViewModel type</summary>
    INavigationBuilder Add<TViewModel>() where TViewModel : class;


    /// <summary>Adds a page to push, configuring its ViewModel before the page is constructed</summary>
    INavigationBuilder Add<TViewModel>(Action<TViewModel> configure) where TViewModel : class;


    /// <summary>Executes the accumulated pops and pushes</summary>
    Task Navigate();
}
