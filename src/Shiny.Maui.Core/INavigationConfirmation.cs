namespace Shiny;

public interface INavigationConfirmation
{
    /// <summary>
    /// Determines whether navigation to the target destination is allowed.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if navigation is
    /// allowed; otherwise, <see langword="false"/>.</returns>
    Task<bool> CanNavigate();
}