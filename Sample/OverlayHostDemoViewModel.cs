using Shiny;

namespace Sample;

/// <summary>
/// Hosts a dialog from a <c>ShinyContentPage</c>. <c>ShinyOverlayDialogPresenter</c> takes a
/// different path here than on a plain ContentPage - it puts the overlay into the page's own
/// <c>OverlayHost</c> rather than wrapping the page's content in a Grid - so both need exercising.
/// </summary>
[ShellMap<OverlayHostDemoPage>]
public partial class OverlayHostDemoViewModel(INavigator navigator, DialogEventLog log) : ObservableObject
{
    [ObservableProperty] string lastResult = "(none)";

    [RelayCommand]
    async Task ShowDialog()
    {
        log.Add("ShinyContentPage host: ShowDialog");
        var result = await navigator.ShowPickColorDialog(preset: "Blue");
        this.LastResult = result.IsCancelled ? "(cancelled)" : result.Value!;
        log.Add($"ShinyContentPage host: {this.LastResult}");
    }


    [RelayCommand]
    async Task ShowWhileLoading()
    {
        // The page's built-in loading overlay already occupies the OverlayHost, so this checks the
        // dialog layers above it rather than fighting it.
        this.IsLoading = true;
        var result = await navigator.ShowPickColorDialog(preset: "Green");
        this.IsLoading = false;
        this.LastResult = result.IsCancelled ? "(cancelled, over loading overlay)" : $"{result.Value} (over loading overlay)";
        log.Add($"ShinyContentPage host over loading overlay: {this.LastResult}");
    }


    [ObservableProperty] bool isLoading;
}
