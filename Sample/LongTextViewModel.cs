using Shiny;

namespace Sample;

/// <summary>
/// A deliberately tall dialog - the case that shows whether a presenter's card sizes, scrolls and
/// clips sensibly instead of running off the screen.
/// </summary>
[ShellMap<LongTextPage>("LongText")]
public partial class LongTextViewModel(DialogEventLog log)
    : ObservableObject, IDialogAware<string>, IPageLifecycleAware, IDisposable
{
    public event EventHandler<string>? Completed;
    public event EventHandler? Cancelled;

    public string[] Lines { get; } = Enumerable
        .Range(1, 40)
        .Select(x => $"Line {x} - enough content to overflow any dialog card.")
        .ToArray();

    [RelayCommand] void Accept() => this.Completed?.Invoke(this, "accepted");
    [RelayCommand] void Cancel() => this.Cancelled?.Invoke(this, EventArgs.Empty);

    public void OnAppearing() => log.Add("LongTextViewModel.OnAppearing");
    public void OnDisappearing() => log.Add("LongTextViewModel.OnDisappearing");
    public void Dispose() => log.Add("LongTextViewModel.Dispose");
}
