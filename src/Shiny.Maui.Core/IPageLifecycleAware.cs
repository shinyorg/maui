namespace Shiny;

public interface IPageLifecycleAware
{
    /// <summary>
    /// Fires as the page is (re)appearing
    /// </summary>
    void OnAppearing();

    /// <summary>
    /// Fires as the page is being removed or backgrounded on the navigation stack
    /// </summary>
    void OnDisappearing();
}