# Shiny MAUI Navigation

[![NuGet](https://img.shields.io/nuget/v/Shiny.Maui.Navigation?style=for-the-badge)](https://www.nuget.org/packages/Shiny.Maui.Navigation)

ViewModel-first navigation, tabs, and flyouts for .NET MAUI — **without Shell**.

Everything is identified by its ViewModel type. There are no routes, no URI strings, and no
source generator. Data reaches the destination through a typed callback that runs before the
page is even constructed.

```csharp
await navigator.NavigateTo<DetailViewModel>(vm => vm.OrderId = 42);
```

[Full Documentation](https://shinylib.net/mauinav)

> Looking for the Shell version? [Shiny.Maui.Shell](https://www.nuget.org/packages/Shiny.Maui.Shell)
> gives you the same ideas layered on .NET MAUI Shell, with URI routes and source-generated
> navigation methods. Pick one — they are alternatives, not companions.

---

## Features

| Capability | API |
|:-----------|:----|
| Push / back | `NavigateTo<TVm>(vm => …)` · `GoBack()` · `GoBack(3)` · `PopToRoot()` |
| Reset a stack | `NavigateToRoot<TVm>()` |
| Modals | `PushModal<TVm>()` · `PopModal()` |
| Tabs | `AddTabs(…)` · `SelectTab<TVm>()` · independent back stack per tab |
| Tab badges | `SetTabBadge<TVm>(3)` · `ClearTabBadge<TVm>()` |
| Flyout | `AddFlyout(…)` · `OpenFlyout()` · `CloseFlyout()` |
| Multi-page push | `CreateBuilder().Add<A>().Add<B>().Navigate()` — one animation |
| Root swapping | `SwitchRoot<LoginViewModel>()` · `RestoreRoot()` |
| Nav events | `Navigating` · `Navigated` |
| Dialogs | `IDialogs` — `Alert` · `Confirm` · `Prompt` · `ActionSheet` |
| XAML navigation | `Navigate.ViewModel` · `Navigate.Mode` · `Navigate.Action` attached properties |

### ViewModel lifecycle

| Interface | Method | Purpose |
|:----------|:-------|:--------|
| `IPageLifecycleAware` | `OnAppearing()` / `OnDisappearing()` | Page visibility hooks |
| `INavigationConfirmation` | `Task<bool> CanNavigate()` | Veto leaving (unsaved changes) |
| `INavigatingAway` | `OnNavigatingAway()` | Fires just before you leave |
| `IDisposable` | `Dispose()` | Called when the page leaves the tree |

---

## Getting Started

### 1. Install

```bash
dotnet add package Shiny.Maui.Navigation
```

### 2. Declare the app in `MauiProgram.cs`

The structure *is* the registration. There is no `AppShell.xaml` equivalent.

```csharp
builder
    .UseMauiApp<App>()
    .UseShinyNavigation(x => x
        .AddFlyout(f => f
            .Menu<MenuPage, MenuViewModel>("My App")
            .AddTabs(t => t
                .Add<HomePage,  HomeViewModel>("Home",  "home.png")
                .Add<InboxPage, InboxViewModel>("Inbox", "inbox.png")
            )
        )
        // pushable / modal targets — registered, but not part of the structure
        .Add<DetailPage, DetailViewModel>()
        .Add<SettingsPage, SettingsViewModel>()
    );
```

Every level is optional:

| You declare | You get |
|:---|:---|
| `SetRoot<…>()` only | `NavigationPage` |
| `AddTabs(…)` | `TabbedPage`, one `NavigationPage` per tab |
| `AddFlyout(…)` + either of the above | `FlyoutPage` wrapping the detail |

### 3. Inherit `ShinyApplication`

Your `App` needs no navigation code — the library hands MAUI the page tree it built.

```xml
<shiny:ShinyApplication xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                        xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                        xmlns:shiny="clr-namespace:Shiny;assembly=Shiny.Maui.Navigation"
                        x:Class="MyApp.App">
</shiny:ShinyApplication>
```

```csharp
public partial class App : ShinyApplication
{
    public App() => this.InitializeComponent();
}
```

### 4. Navigate

```csharp
public partial class HomeViewModel(INavigator navigator) : ObservableObject
{
    [RelayCommand]
    Task Open() => navigator.NavigateTo<DetailViewModel>(vm => vm.OrderId = 42);
}
```

The `configure` callback runs against the DI-resolved ViewModel **before** the page is
constructed, so `OnAppearing` and every binding sees fully initialised state. That ordering is
why no parameter dictionary or `IQueryAttributable` is needed.

---

## Where a push lands

`NavigateTo` always targets the **active** stack:

1. a modal is showing → the modal's stack
2. otherwise, tabs → the current tab's stack
3. otherwise → the single root stack

That means a tab keeps its own history: switch away and back, and the user is exactly where
they left off.

---

## XAML navigation

```xml
<Button Text="Details" shiny:Navigate.ViewModel="{x:Type vm:DetailViewModel}" />
<Button Text="Compose" shiny:Navigate.ViewModel="{x:Type vm:ComposeViewModel}" shiny:Navigate.Mode="Modal" />
<Button Text="Inbox"   shiny:Navigate.ViewModel="{x:Type vm:InboxViewModel}"   shiny:Navigate.Mode="Tab" />
<Button Text="Back"    shiny:Navigate.Action="GoBack" />
<ToolbarItem Text="Menu" shiny:Navigate.Action="ToggleFlyout" />
```

`Navigate.Mode`: `Push` (default) · `Root` · `Modal` · `Tab`
`Navigate.Action`: `GoBack` · `PopToRoot` · `PopModal` · `OpenFlyout` · `CloseFlyout` · `ToggleFlyout`

Works on `Button`, `MenuItem`, `ToolbarItem`, and any `View` (tap). Teach it about other
controls with `Navigate.RegisterInvoker<T>(attach, detach)`.

---

## Guarding navigation

```csharp
public class EditViewModel(IDialogs dialogs) : INavigationConfirmation
{
    public bool IsDirty { get; set; }

    public Task<bool> CanNavigate()
        => this.IsDirty
            ? dialogs.Confirm("Unsaved changes", "Leave without saving?", "Leave", "Stay")
            : Task.FromResult(true);
}
```

The guard runs on every navigation the library initiates, and on the Android hardware/gesture
back button.

> [!NOTE]
> The **iOS navigation bar back arrow** cannot be intercepted by MAUI. On a page that must not
> be left without confirmation, hide it with `NavigationPage.SetHasBackButton(page, false)` and
> give the user an explicit action that calls `GoBack()`.

---

## Dialog providers

`IDialogs` defaults to the native platform alert/prompt/action sheet. Swap it for an owned,
themeable implementation without touching a single ViewModel:

```csharp
.UseShinyNavigation(x => x
    .UseShinyDialogs()      // Shiny.Maui.Shell.ShinyDialogs package
    …
)
```

- `Shiny.Maui.Shell.ShinyDialogs` — animated, themeable dialogs from
  [Shiny.Maui.Controls](https://shinylib.net/controls/dialogs/), identical on every platform
- `Shiny.Maui.Shell.UxDiversDialogs` — styled popups from
  [UXDivers Popups](https://github.com/uxdivers/uxd-popups)

Both work with this package and with `Shiny.Maui.Shell`.

---

## Shell or Navigation?

| | `Shiny.Maui.Shell` | `Shiny.Maui.Navigation` |
|:---|:---|:---|
| Built on | .NET MAUI Shell | `NavigationPage` / `TabbedPage` / `FlyoutPage` |
| Targets | Route strings + URIs, or ViewModel types | ViewModel types only |
| Parameters | `configure` callback **and** string-keyed args | `configure` callback |
| Source generator | Yes — `Routes.*`, `NavigateToDetail(…)` | No |
| Deep links | URI-native | Call `NavigateTo<T>` yourself |
| Structure declared in | `AppShell.xaml` | `MauiProgram.cs` |

Take Shell if you want URI deep links and generated navigation methods. Take Navigation if you
want plain MAUI pages, no route strings, and a structure you can unit test.
