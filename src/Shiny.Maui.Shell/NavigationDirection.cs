namespace Shiny;


/// <summary>
/// Which way through the navigation stack a navigation goes - the coarse question
/// ("am I going back?") that <see cref="NavigationType"/> answers precisely.
/// </summary>
public enum NavigationDirection
{
    /// <summary>Deeper into the stack - a push.</summary>
    Forward,

    /// <summary>Up the stack - a back, or a pop to root.</summary>
    Back,

    /// <summary>Neither: the stack is replaced, by an absolute route or a Shell swap.</summary>
    Root
}


public static class NavigationTypeExtensions
{
    /// <summary>
    /// The direction a <see cref="NavigationType"/> travels.
    /// </summary>
    public static NavigationDirection GetDirection(this NavigationType navigationType) => navigationType switch
    {
        NavigationType.Push => NavigationDirection.Forward,
        NavigationType.GoBack or NavigationType.PopToRoot => NavigationDirection.Back,
        _ => NavigationDirection.Root
    };
}
