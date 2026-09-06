namespace Shiny;

public record NavigationEventArgs(
    string? FromUri,
    object? FromViewModel,
    string ToUri,
    NavigationType NavigationType,
    IReadOnlyDictionary<string, object> Parameters
)
{
    /// <summary>Which way through the stack this navigation goes.</summary>
    public NavigationDirection Direction => this.NavigationType.GetDirection();
}

public record NavigatedEventArgs(
    string ToUri,
    object? ToViewModel,
    NavigationType NavigationType,
    IReadOnlyDictionary<string, object> Parameters
)
{
    /// <summary>Which way through the stack this navigation went.</summary>
    public NavigationDirection Direction => this.NavigationType.GetDirection();
}
