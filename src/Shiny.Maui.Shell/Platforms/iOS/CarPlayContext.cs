namespace Shiny.Infrastructure;

public class CarPlayContext : ICarPlayContext
{
    public bool IsConnected { get; private set; }
    public event EventHandler<bool>? ConnectionChanged;

    internal void SetConnected(bool connected)
    {
        this.IsConnected = connected;
        this.ConnectionChanged?.Invoke(this, connected);
    }
}
