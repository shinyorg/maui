namespace Shiny.Infrastructure;

public class NullCarPlayContext : ICarPlayContext
{
    public bool IsConnected => false;
    public event EventHandler<bool> ConnectionChanged
    {
        add { }
        remove { }
    }
}
