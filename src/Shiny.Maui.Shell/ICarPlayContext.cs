namespace Shiny;

public interface ICarPlayContext
{
    bool IsConnected { get; }
    event EventHandler<bool> ConnectionChanged;
}
