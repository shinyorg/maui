namespace Shiny;

public enum NavigationType
{
    Push,
    SetRoot,
    GoBack,
    PopToRoot,

    /// <summary>Shiny.Maui.Shell only - the entire Shell was replaced</summary>
    SwitchShell,

    /// <summary>Shiny.Maui.Navigation only - a page was pushed onto the modal stack</summary>
    PushModal,

    /// <summary>Shiny.Maui.Navigation only - a page was popped off the modal stack</summary>
    PopModal,

    /// <summary>Shiny.Maui.Navigation only - the active tab was changed</summary>
    SelectTab,

    /// <summary>Shiny.Maui.Navigation only - the entire navigation root was rebuilt</summary>
    SwitchRoot
}
