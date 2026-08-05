namespace Shiny;

/// <summary>
/// Raised before a navigation is executed. Unlike the Shell library there are no URIs here -
/// the destination is identified by its ViewModel type.
/// </summary>
public record NavigationEventArgs(
    object? FromViewModel,
    Type? ToViewModelType,
    NavigationType NavigationType
);


/// <summary>
/// Raised once the destination page has appeared and its ViewModel is bound.
/// </summary>
public record NavigatedEventArgs(
    object? ToViewModel,
    NavigationType NavigationType
);
