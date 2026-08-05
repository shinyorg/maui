# Shiny MAUI Core

[![NuGet](https://img.shields.io/nuget/v/Shiny.Maui.Core?style=for-the-badge)](https://www.nuget.org/packages/Shiny.Maui.Core)

Shared contracts for [`Shiny.Maui.Shell`](https://www.nuget.org/packages/Shiny.Maui.Shell) and
[`Shiny.Maui.Navigation`](https://www.nuget.org/packages/Shiny.Maui.Navigation). You do not
install this directly — it comes in with whichever navigation library you choose.

| Type | Purpose |
|:---|:---|
| `IDialogs` | Alert · Confirm · Prompt · ActionSheet |
| `IPageLifecycleAware` | `OnAppearing()` / `OnDisappearing()` |
| `INavigationConfirmation` | `Task<bool> CanNavigate()` — veto navigation |
| `IMainThread` / `MauiMainThread` | UI-thread dispatch |
| `NavigationType` | The kind of navigation reported by nav events |
| `IShinyBuilder` | What add-on packages (dialog providers) target so one extension method works with either navigation library |
