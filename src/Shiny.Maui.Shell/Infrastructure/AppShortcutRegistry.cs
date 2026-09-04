namespace Shiny.Infrastructure;


/// <summary>
/// One registered home screen quick action.
/// </summary>
/// <param name="Id">The AppAction id - the route unless overridden.</param>
/// <param name="Route">The Shell route it navigates to.</param>
/// <param name="ViewModelType">The ViewModel mapped to that route.</param>
/// <param name="RegisterRoute">
/// False when the route is a ShellContent declared in AppShell XAML, which decides whether
/// activation resets the stack or pushes onto it.
/// </param>
/// <param name="Configure">
/// Populates the ViewModel on activation. Only the <paramref name="Id"/> is persisted by the
/// platform, so this does not need to survive serialization - the registration is rebuilt on
/// every launch and looked up by id.
/// </param>
public record RegisteredAppShortcut(
    string Id,
    string Route,
    Type ViewModelType,
    bool RegisterRoute,
    string Title,
    string? Subtitle,
    string? Icon,
    int Order,
    Action<object>? Configure
);


/// <summary>
/// Holds the quick actions declared through <see cref="ShellMapAttribute{TPage}.Shortcut"/> or
/// registered by hand with <c>ShinyAppBuilder.AddAppShortcut</c>.
/// </summary>
public class AppShortcutRegistry
{
    /// <summary>
    /// iOS shows at most four quick actions and silently drops the rest; Android guarantees four.
    /// </summary>
    public const int PlatformMaximum = 4;

    readonly List<RegisteredAppShortcut> shortcuts = new();

    /// <summary>Registered shortcuts in display order.</summary>
    public IReadOnlyList<RegisteredAppShortcut> Shortcuts => this.shortcuts;


    public void Add(RegisteredAppShortcut shortcut)
    {
        this.shortcuts.Add(shortcut);
        this.shortcuts.Sort(static (a, b) =>
        {
            var byOrder = a.Order.CompareTo(b.Order);
            return byOrder != 0 ? byOrder : string.CompareOrdinal(a.Id, b.Id);
        });
    }


    public RegisteredAppShortcut? Find(string id)
    {
        foreach (var shortcut in this.shortcuts)
        {
            if (string.Equals(shortcut.Id, id, StringComparison.Ordinal))
                return shortcut;
        }
        return null;
    }
}
