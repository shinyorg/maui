namespace Sample;

/// <summary>
/// An on-screen log of what the dialogs actually did. The ViewModels already log their lifecycle to
/// <c>ILogger</c>, but a presenter's interesting failures - a hook that never fires, a dispose that
/// never happens, an await that hangs - show up on a device, where nobody is watching debug output.
/// </summary>
public class DialogEventLog
{
    const int MaxEntries = 200;

    public ObservableCollection<string> Entries { get; } = [];

    public void Add(string entry) => MainThread.BeginInvokeOnMainThread(() =>
    {
        this.Entries.Insert(0, $"{DateTime.Now:HH:mm:ss.fff}  {entry}");
        while (this.Entries.Count > MaxEntries)
            this.Entries.RemoveAt(this.Entries.Count - 1);
    });

    public void Clear() => MainThread.BeginInvokeOnMainThread(this.Entries.Clear);
}
