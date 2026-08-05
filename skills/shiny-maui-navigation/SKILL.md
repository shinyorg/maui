---
name: shiny-maui-navigation
description: Generate .NET MAUI pages, ViewModels, tabs, flyouts, and ViewModel-first navigation using Shiny MAUI Navigation - the Shell-free alternative (no routes, no URIs, no source generation)
auto_invoke: true
triggers:
  - maui navigation without shell
  - no shell navigation
  - shell-free navigation
  - Shiny.Maui.Navigation
  - UseShinyNavigation
  - ShinyNavigationBuilder
  - ShinyApplication
  - ShinyNavigationPage
  - NavigationHost
  - NavigationStructure
  - AddTabs
  - AddFlyout
  - TabsBuilder
  - FlyoutBuilder
  - SetRoot
  - NavigateToRoot
  - PushModal
  - PopModal
  - SelectTab
  - OpenFlyout
  - CloseFlyout
  - SwitchRoot
  - RestoreRoot
  - INavigatingAway
  - Navigate.ViewModel
  - Navigate.Mode
  - Navigate.Action
  - NavigateMode
  - NavigateAction
  - TabbedPage navigation
  - FlyoutPage navigation
  - maui tabs viewmodel
  - maui flyout viewmodel
---

# Shiny MAUI Navigation Skill

Expert guidance for **Shiny.Maui.Navigation** — ViewModel-first navigation, tabs, and flyouts
for .NET MAUI built on plain `NavigationPage` / `TabbedPage` / `FlyoutPage`, with **no Shell**.

## Choosing between the two libraries — read this first

This repo ships two mutually exclusive navigation libraries. Check which package the project
references before generating anything:

| If the project references… | Use skill | Key markers in code |
|:---|:---|:---|
| `Shiny.Maui.Shell` | `shiny-maui-shell` | `UseShinyShell`, `AppShell : ShinyShell`, `[ShellMap]`, `Routes.*`, URI strings |
| `Shiny.Maui.Navigation` | **this skill** | `UseShinyNavigation`, `App : ShinyApplication`, `AddTabs`, `AddFlyout` |

**Never mix them.** When generating for `Shiny.Maui.Navigation`, do NOT emit any of:
`[ShellMap]`, `[ShellProperty]`, `Routes.*`, `nameof(SomePage)` route strings,
`NavigateTo("Detail")`, `IQueryAttributable`, `ApplyQueryAttributes`, `AddGeneratedMaps()`,
`registerRoute:`, `relativeNavigation:`, `//` URI prefixes, `SwitchShell`, or `ShinyShell`.
None of them exist in this library.

## Library Overview

**Documentation**: https://shinylib.net/mauinav
**GitHub**: https://github.com/shinyorg/mauishell
**NuGet**: `Shiny.Maui.Navigation`
**Namespace**: `Shiny` (infrastructure in `Shiny.Navigation.Infrastructure`)

Core idea: **every navigation target is a ViewModel type.** Data reaches the destination
through a typed `configure` callback that runs against the DI-resolved ViewModel *before the
page is constructed* — so there is no parameter dictionary, no string keys, and no
`IQueryAttributable`.

## Setup

### 1. Install
```bash
dotnet add package Shiny.Maui.Navigation
```

### 2. Declare the app structure in MauiProgram.cs

The structure IS the registration. There is no XAML host page.

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
        // pushable / modal targets - registered but not part of the structure
        .Add<DetailPage, DetailViewModel>()
        .Add<SettingsPage, SettingsViewModel>()
    );
```

Structure options — every level is optional:

| Declared | Resulting page tree |
|:---|:---|
| `.SetRoot<TPage, TVm>()` | `ShinyNavigationPage` |
| `.AddTabs(…)` | `TabbedPage` → one `ShinyNavigationPage` per tab |
| `.AddFlyout(…)` + either | `FlyoutPage` wrapping the detail |

`FlyoutBuilder` members: `Menu<TPage, TVm>(title)`, `AddTabs(…)`, `SetRoot<TPage, TVm>()`,
`Behavior(FlyoutLayoutBehavior)`, `CloseOnNavigate(bool)` (defaults true).

`TabsBuilder.Add<TPage, TVm>(title, icon, wrapInNavigationPage)` — `wrapInNavigationPage`
defaults true and is what gives each tab an independent back stack.

### 3. App must inherit ShinyApplication

```xml
<shiny:ShinyApplication xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                        xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                        xmlns:shiny="clr-namespace:Shiny;assembly=Shiny.Maui.Navigation"
                        x:Class="MyApp.App">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Resources/Styles/Colors.xaml" />
                <ResourceDictionary Source="Resources/Styles/Styles.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</shiny:ShinyApplication>
```

```csharp
public partial class App : ShinyApplication
{
    public App() => this.InitializeComponent();
}
```

Do NOT override `CreateWindow` and do NOT write `new Window(new AppShell())` — the base class
hands MAUI the page tree the builder produced.

## INavigator API

```csharp
// push onto the active stack; configure runs before the page exists
Task NavigateTo<TViewModel>(Action<TViewModel>? configure = null, bool animated = true);
Task NavigateTo(Type viewModelType, bool animated = true);

// replace the active stack, making this the new root
Task NavigateToRoot<TViewModel>(Action<TViewModel>? configure = null, bool animated = true);
Task NavigateToRoot(Type viewModelType, bool animated = true);

Task PushModal<TViewModel>(Action<TViewModel>? configure = null, bool animated = true, bool wrapInNavigationPage = true);
Task PushModal(Type viewModelType, bool animated = true, bool wrapInNavigationPage = true);
Task PopModal(bool animated = true);

Task GoBack(bool animated = true);
Task GoBack(int backCount, bool animated = true);
Task PopToRoot(bool animated = true);

Task SelectTab<TViewModel>();
Task SelectTab(Type viewModelType);
Task SetTabBadge<TViewModel>(int value);
Task ClearTabBadge<TViewModel>();

bool HasFlyout { get; }
Task OpenFlyout();
Task CloseFlyout();

// login-screen swap, and its counterpart
Task SwitchRoot<TViewModel>(Action<TViewModel>? configure = null);
Task RestoreRoot();

INavigationBuilder CreateBuilder();

Page? CurrentPage { get; }
object? CurrentViewModel { get; }

event EventHandler<NavigationEventArgs>? Navigating;   // FromViewModel, ToViewModelType, NavigationType
event EventHandler<NavigatedEventArgs>? Navigated;     // ToViewModel, NavigationType
```

### Passing data — always the configure callback

```csharp
// CORRECT
await navigator.NavigateTo<DetailViewModel>(vm =>
{
    vm.OrderId = 42;
    vm.Mode = EditMode.ReadOnly;
});

// WRONG - this library has no string parameters
await navigator.NavigateTo("Detail", args: [("OrderId", 42)]);   // does not compile
```

### Where a push lands

`NavigateTo` targets the **active** stack, resolved in this order:
1. a modal is showing → the modal's stack
2. tabs exist → the current tab's stack
3. otherwise → the single root stack

Pushing from a modal created with `wrapInNavigationPage: false` throws — that modal has no stack.

### Multi-page push — one animation

```csharp
await navigator
    .CreateBuilder()
    .Add<CategoryViewModel>(vm => vm.Id = 3)
    .Add<ProductViewModel>(vm => vm.Sku = "ABC")
    .Navigate();

// pop 2 then push 1, still one animation
await navigator.CreateBuilder().PopBack(2).Add<SummaryViewModel>().Navigate();

// clear the stack, then push
await navigator.CreateBuilder().FromRoot().Add<DashboardViewModel>().Navigate();
```

`PopBack` and `FromRoot` are mutually exclusive and must precede any `Add`.

## ViewModel lifecycle interfaces

| Interface | Member | Fires |
|:---|:---|:---|
| `IPageLifecycleAware` | `OnAppearing()` / `OnDisappearing()` | page shown / hidden |
| `INavigationConfirmation` | `Task<bool> CanNavigate()` | before leaving — return false to veto |
| `INavigatingAway` | `OnNavigatingAway()` | just before leaving |
| `IDisposable` | `Dispose()` | page removed from the tree |

`INavigatingAway` is this library's replacement for Shell's
`INavigationAware.OnNavigatingFrom(IDictionary<string, object>)` — it takes **no parameters**,
because there are no string-keyed parameters here.

```csharp
public partial class EditViewModel(IDialogs dialogs) : ObservableObject, INavigationConfirmation
{
    [ObservableProperty] public partial bool IsDirty { get; set; }

    public Task<bool> CanNavigate()
        => this.IsDirty
            ? dialogs.Confirm("Unsaved changes", "Leave without saving?", "Leave", "Stay")
            : Task.FromResult(true);
}
```

The guard runs on every navigation the library initiates and on the Android
hardware/gesture back button (via `ShinyNavigationPage.OnBackButtonPressed`). It does **not**
run on the iOS navigation bar back arrow — MAUI cannot intercept it. When that matters, tell
the user to hide it with `NavigationPage.SetHasBackButton(page, false)` and provide an explicit
action that calls `GoBack()`.

## XAML navigation

```xml
xmlns:shiny="clr-namespace:Shiny;assembly=Shiny.Maui.Navigation"

<Button Text="Details" shiny:Navigate.ViewModel="{x:Type vm:DetailViewModel}" />
<Button Text="Compose" shiny:Navigate.ViewModel="{x:Type vm:ComposeViewModel}"
                       shiny:Navigate.Mode="Modal" />
<Button Text="Inbox"   shiny:Navigate.ViewModel="{x:Type vm:InboxViewModel}"
                       shiny:Navigate.Mode="Tab" />
<Button Text="Back"    shiny:Navigate.Action="GoBack" />
<ToolbarItem Text="Menu" shiny:Navigate.Action="ToggleFlyout" />
```

- `Navigate.ViewModel` — the target ViewModel **type** (`{x:Type …}`, never a string)
- `Navigate.Mode` — `Push` (default) · `Root` · `Modal` · `Tab`
- `Navigate.Action` — `GoBack` · `PopToRoot` · `PopModal` · `OpenFlyout` · `CloseFlyout` · `ToggleFlyout`

Supported on `Button`, `MenuItem`, `ToolbarItem`, and any `View` (tap gesture). Extend with
`Navigate.RegisterInvoker<T>(attach, detach)`.

There are no `Navigate.Route`, `Navigate.Parameters`, `Navigate.ParameterKey`, or
`Navigate.ParameterValue` properties in this library.

## Dialogs

`IDialogs` (`Alert`, `Confirm`, `Prompt`, `ActionSheet`) defaults to `NavigationDialogs`, which
presents native platform dialogs from the current page. Swap the provider without touching a
ViewModel:

```csharp
.UseShinyNavigation(x => x
    .UseShinyDialogs()        // Shiny.Maui.Shell.ShinyDialogs package
    // or .UseUxDiversDialogs()
    // or .UseDialogs<MyDialogs>()
    …
)
```

Provider packages target `IShinyBuilder` from `Shiny.Maui.Core`, so the same extension methods
work with `Shiny.Maui.Shell` too.

## Tab badges

```csharp
await navigator.SetTabBadge<InboxViewModel>(3);
await navigator.ClearTabBadge<InboxViewModel>();
```

Badges are addressed by tab index derived from the declared structure, and are reapplied after
every navigation because platforms drop them when a tab's native view is recreated.

On Android, a `TabbedPage` renders a top `TabLayout` by default. For a bottom bar (and
BottomNavigationView badges), the app opts in with
`Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.TabbedPage.SetToolbarPlacement(page, ToolbarPlacement.Bottom)`.
Both are supported.

## Root swapping (login flow)

```csharp
await navigator.SwitchRoot<LoginViewModel>();   // whole window becomes a login stack
// …after sign-in
await navigator.RestoreRoot();                  // rebuild the declared flyout/tabs structure
```

`SwitchRoot` uses a two-phase window swap (blank page → real page) to avoid an iOS crash when
replacing the window page while the outgoing handler is still live. Do not reimplement this by
assigning `Window.Page` directly.

## Page/ViewModel conventions

Pages are plain `ContentPage`s. The library resolves both page and ViewModel from DI as
transients and assigns `BindingContext` itself — never set `BindingContext` in the page
constructor.

```csharp
public partial class DetailPage : ContentPage
{
    public DetailPage() => this.InitializeComponent();
}

public partial class DetailViewModel(INavigator navigator) : ObservableObject, IPageLifecycleAware
{
    [ObservableProperty] public partial int OrderId { get; set; }

    [RelayCommand]
    Task Back() => navigator.GoBack();

    public void OnAppearing() { }
    public void OnDisappearing() { }
}
```

ViewModels must implement `INotifyPropertyChanged` (the `Add<TPage, TViewModel>` constraint) —
`ObservableObject` from CommunityToolkit.Mvvm satisfies this.

## Testability

`ShinyNavigationBuilder` is pure logic: `BuildStructure()`, `GetRegistration(Type)`, and
`GetViewModelTypeForPage(Page)` can all be asserted in a plain unit test with no UI or
platform. See `tests/Shiny.Maui.Navigation.Tests`.
