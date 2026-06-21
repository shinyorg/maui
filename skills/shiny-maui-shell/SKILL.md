---
name: shiny-maui-shell
description: Generate .NET MAUI Shell pages, ViewModels, navigation, and source-generated routes using Shiny MAUI Shell
auto_invoke: true
triggers:
  - maui shell
  - shell navigation
  - xaml navigation
  - attached navigation
  - tab badge
  - badge
  - shell switch
  - switch shell
  - maui navigation
  - maui page
  - maui viewmodel
  - INavigator
  - IDialogs
  - ShellMap
  - ShellProperty
  - UseShinyShell
  - UseDialogs
  - UseShinyDialogs
  - ShinyDialogs
  - UseUxDiversDialogs
  - ShinyShell
  - ShellServices
  - ShinyAppBuilder
  - IMainThread
  - Shiny.Maui.Shell
  - IPageLifecycleAware
  - INavigationConfirmation
  - INavigationAware
  - NavigateTo
  - GoBack
  - PopToRoot
  - SetRoot
  - SetTabBadge
  - ClearTabBadge
  - SwitchShell
  - CreateBuilder
  - INavigationBuilder
  - NavigationBuilder
  - Navigating
  - Navigated
  - IDialogs
  - Alert
  - Confirm
  - Prompt
  - ActionSheet
  - NavigationEventArgs
  - NavigatedEventArgs
  - Navigate.Route
  - Navigate.RelativeNavigation
  - Navigate.ParameterKey
  - Navigate.ParameterValue
  - Navigate.Parameters
  - NavigationParameters
  - NavigationParameter
  - GetGeneratedRouteInfo
  - GetAiToolApplicableGeneratedRoutes
  - NavigateToRoute
  - GeneratedRouteInfo
  - GeneratedRouteParameter
  - AI navigation
  - AI tool
  - chat navigation
  - AiMauiShellTools
  - AddAiTools
---

# Shiny MAUI Shell Skill

You are an expert in Shiny MAUI Shell, a library that enhances .NET MAUI Shell with ViewModel lifecycle management, navigation services, source generation, tab badges, and XAML-triggered navigation.

## When to Use This Skill

Invoke this skill when the user wants to:
- Create new MAUI pages with ViewModels using Shiny Shell conventions
- Set up or configure Shiny MAUI Shell in their application
- Switch between different Shell instances at runtime (e.g., login shell vs main app shell)
- Implement navigation between pages using `INavigator`
- Set or clear tab badge values on tabs in the active Shell
- Add route-based XAML navigation with `Navigate.*` attached properties
- Build multi-segment navigation chains using `INavigationBuilder` (push multiple pages, pop-and-push)
- Show dialogs (alert, confirm, prompt, action sheet) using `IDialogs`
- Add ViewModel lifecycle hooks (appearing, disappearing, navigation confirmation)
- Use source generation with `[ShellMap]` and `[ShellProperty]` attributes
- Pass parameters between pages during navigation
- Create modal pages or tab navigation
- Migrate from vanilla MAUI Shell or Prism navigation to Shiny MAUI Shell
- Set up AI-driven navigation using `Microsoft.Extensions.AI` with route discovery and `NavigateToRoute`
- Create AI-compatible ViewModels with descriptive `[ShellMap]` and `[ShellProperty]` attributes

## Library Overview

**Documentation**: https://shinylib.net/maui
**GitHub**: https://github.com/shinyorg/mauishell
**NuGet**: `Shiny.Maui.Shell`
**Namespace**: `Shiny`

Shiny MAUI Shell wraps .NET MAUI Shell to provide:
- Page-to-ViewModel registration and automatic BindingContext assignment
- A testable `INavigator` service for all navigation operations
- A testable `IDialogs` service for alert, confirm, prompt, and action sheet dialogs
- `INavigationBuilder` for multi-segment navigation (push multiple pages in one operation, pop-and-push)
- Native numeric tab badges via `INavigator.SetTabBadge*` / `ClearTabBadge*`
- Attached-property XAML navigation via `Navigate.Route`, `Navigate.RelativeNavigation`, and parameter helpers
- Shell switching — swap the entire Shell at runtime (e.g., login → main app)
- ViewModel lifecycle interfaces (appearing, disappearing, dispose, navigation confirmation)
- Source generators that eliminate boilerplate route registration, produce strongly-typed navigation methods, and generate AI tool metadata
- `ShinyShell` base class for deterministic initial-page BindingContext assignment
- `ShellServices` record that aggregates `INavigator`, `IDialogs`, and `IMainThread` for convenient single-parameter injection
- `IMainThread` abstraction with built-in workarounds for macOS and Linux where `MainThread.InvokeOnMainThreadAsync` can deadlock / fail
- Pluggable `IDialogs` implementation via `UseDialogs<TDialog>()` — swap in your own dialog provider (e.g. ACR UserDialogs, a custom sheet, a test double)

Inspired by [Prism Library](https://prismlibrary.com) by Dan Siegel and Brian Lagunas.

## Setup

### 1. Install NuGet Package
```bash
dotnet add package Shiny.Maui.Shell
```

### 2. Configure in MauiProgram.cs

**Manual registration:**
```csharp
public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    builder
        .UseMauiApp<App>()
        .UseShinyShell(x => x
            .Add<MainPage, MainViewModel>(registerRoute: false)
            .Add<DetailPage, DetailViewModel>("Detail")
            .Add<SettingsPage, SettingsViewModel>("Settings")
        )
        .ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
        });

    return builder.Build();
}
```

**With source generation (preferred):**
```csharp
builder
    .UseMauiApp<App>()
    .UseShinyShell(x => x
        .AddGeneratedMaps()
        .AddAiTools()       // registers AiMauiShellTools as singleton for AI integration
    )
```

**With a custom dialog provider:**
```csharp
builder
    .UseMauiApp<App>()
    .UseShinyShell(x => x
        .AddGeneratedMaps()
        .UseDialogs<MyCustomDialogs>()   // register a custom IDialogs implementation
    );
```

`UseDialogs<TDialog>()` replaces the default `ShellDialogs` provider. The default registration uses `TryAddSingleton`, so a `UseDialogs<>` call always wins.

**Built-in alternative providers** (same `IDialogs` interface — no ViewModel changes):
```csharp
// Owned, animated, themeable dialogs via Shiny.Maui.Controls (package: Shiny.Maui.Shell.ShinyDialogs)
builder
    .UseMauiApp<App>()
    .UseShinyControls()                  // registers the Controls IDialogService
    .UseShinyShell(x => x.AddGeneratedMaps().UseShinyDialogs());

// Styled UXDivers popups (package: Shiny.Maui.Shell.UxDiversDialogs)
builder
    .UseMauiApp<App>()
    .UseUxDiversDialogs()                 // initialize UXDivers popup infrastructure
    .UseShinyShell(x => x.AddGeneratedMaps().UseUxDiversDialogs());
```

### 3. AppShell must inherit from `ShinyShell`

Your `AppShell` (or any Shell subclass) must inherit from `Shiny.ShinyShell` instead of `Shell`. This ensures the initial page's BindingContext is set deterministically via Shell's own `OnNavigated` lifecycle.

**AppShell.xaml:**
```xml
<shiny:ShinyShell
    x:Class="MyApp.AppShell"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:shiny="clr-namespace:Shiny;assembly=Shiny.Maui.Shell"
    xmlns:local="clr-namespace:MyApp"
    Title="MyApp">

    <ShellContent
        Title="Home"
        ContentTemplate="{DataTemplate local:MainPage}"
        Route="MainPage" />

</shiny:ShinyShell>
```

**AppShell.xaml.cs:**
```csharp
using Shiny;

namespace MyApp;

public partial class AppShell : ShinyShell
{
    public AppShell()
    {
        InitializeComponent();
    }
}
```

### Important Notes
- Pages defined in AppShell.xaml should use `registerRoute: false` since Shell already registers them
- Pages navigated to programmatically need route registration (the default behavior)
- All Pages and ViewModels are registered as Transient in DI automatically

## Code Generation Instructions

When generating code for Shiny MAUI Shell projects, follow these conventions:

### 1. ViewModels

All ViewModels must implement `INotifyPropertyChanged`. Use `CommunityToolkit.Mvvm` `ObservableObject` as the base:

```csharp
[ShellMap<MyPage>("MyRoute")]
public partial class MyViewModel : ObservableObject
{
}
```

- Use `[ShellMap<TPage>("Route")]` on every ViewModel class
- The `route` parameter must be a valid C# identifier — it is used as the generated constant name and method name
- Invalid route names (hyphens, spaces, leading digits) produce a **SHINY001** compiler error
- When no route is specified, the page type name without the `Page` suffix is used as the generated name
- Set `registerRoute: false` only for pages already declared in AppShell.xaml
- ViewModel classes using source generation should be `partial`
- Use primary constructors to inject `INavigator` and other dependencies

### 2. Navigation Properties

Use `[ShellProperty]` on ViewModel properties that should be passed as navigation parameters:

```csharp
[ShellMap<DetailPage>("Detail")]
public partial class DetailViewModel : ObservableObject
{
    [ShellProperty]
    public string ItemId { get; set; }

    [ShellProperty(required: false)]
    public int PageIndex { get; set; }
}
```

- Properties marked `[ShellProperty]` are required by default
- Use `[ShellProperty(required: false)]` for optional parameters
- `[ShellProperty]` properties are set directly by the source-generated navigation methods — no `IQueryAttributable` needed
- Source generator creates strongly-typed extension methods on `INavigator`

### 3. Lifecycle Interfaces

Implement these interfaces on ViewModels as needed:

| Interface | Purpose |
|-----------|---------|
| `IPageLifecycleAware` | `OnAppearing()` / `OnDisappearing()` hooks |
| `INavigationConfirmation` | `Task<bool> CanNavigate()` - confirm before leaving |
| `INavigationAware` | `OnNavigatingFrom(IDictionary<string, object>)` - mutate args before leaving |
| `IQueryAttributable` | `ApplyQueryAttributes(IDictionary<string, object>)` - receive navigation args (only needed for string-based `NavigateTo(route, args)` — not needed when using `[ShellProperty]`) |
| `IDisposable` | Cleanup when page is removed from navigation stack |

### 4. Navigation Events

`INavigator` exposes two events for observing navigation:

- `Navigating` — fires **before** navigation with the source ViewModel instance
- `Navigated` — fires **after** navigation with the destination ViewModel instance

```csharp
navigator.Navigating += (sender, args) =>
{
    // args.FromUri, args.FromViewModel, args.ToUri, args.NavigationType, args.Parameters
};

navigator.Navigated += (sender, args) =>
{
    // args.ToUri, args.ToViewModel, args.NavigationType, args.Parameters
};
```

Hook these events in an `IMauiInitializeService` for cross-cutting concerns like logging or analytics.

### 5. Navigation

Always use `INavigator` for navigation, never `Shell.Current.GoToAsync` directly:

```csharp
// Route-based navigation with args
await navigator.NavigateTo("Detail", args: [("ItemId", "123"), ("PageIndex", 0)]);

// ViewModel-based navigation with strongly-typed configuration
await navigator.NavigateTo<DetailViewModel>(vm => vm.ItemId = "123");

// Source-generated strongly-typed method (preferred)
await navigator.NavigateToDetail("123", pageIndex: 0);

// Absolute navigation (navigates to root route "//Detail")
await navigator.NavigateTo("Detail", relativeNavigation: false);
await navigator.NavigateTo<DetailViewModel>(relativeNavigation: false);

// Go back with result parameters
await navigator.GoBack(("Result", selectedItem));

// Go back multiple pages
await navigator.GoBack(backCount: 2);

// Pop to root
await navigator.PopToRoot();

// Switch to a different Shell instance
await navigator.SwitchShell(new MainAppShell());

// Switch to a Shell resolved from DI
await navigator.SwitchShell<MainAppShell>();
```

### 5a. Navigation Builder

Use `INavigationBuilder` for multi-segment navigation (pushing multiple pages in a single operation):

```csharp
// Push a chain of pages: One > Another > Two
await navigator
    .CreateBuilder()
    .Add<OneViewModel>(x => x.Text = "First")
    .Add<AnotherViewModel>(x => x.Arg = "Middle")
    .Add<TwoViewModel>(x => x.Text = "Last")
    .Navigate();

// Pop back 2 pages, then push a new page
await navigator
    .CreateBuilder()
    .PopBack(2)
    .Add<OneViewModel>(x => x.Text = "Replaced")
    .Navigate();

// Add by route name (no configure callback)
await navigator
    .CreateBuilder()
    .Add("Detail")
    .Navigate();
```

**Important Shell constraints for the Navigation Builder:**
- All pages used in a builder chain **must** be globally registered via `Routing.RegisterRoute` (i.e., `registerRoute: true`, which is the default). Pages declared as `ShellContent` in XAML cannot be used in multi-segment relative URIs.
- `PopBack()` must be called before any `Add()` calls.
- `fromRoot: true` on `CreateBuilder` only works when the target route is a shell-declared route (a `ShellContent` in XAML), not a globally registered route.

### 5b. Tab Badges

Use the badge APIs when a route already exists as a tab in the active Shell:

```csharp
// Route-based badge updates
await navigator.SetTabBadge("Inbox", 3);
await navigator.ClearTabBadge("Inbox");

// ViewModel-based badge updates
await navigator.SetTabBadge<InboxViewModel>(7);
await navigator.ClearTabBadge<InboxViewModel>();
```

- Badge APIs target existing tabs in the active Shell, not arbitrary pushed pages
- Supported platforms: Android, iOS, Mac Catalyst, Windows
- Unsupported platforms currently throw `PlatformNotSupportedException` (neutral target, Linux, macOS AppKit)

### 5c. XAML Navigation

Use `Navigate.*` attached properties for simple route-based navigation directly from XAML:

```xml
<ContentPage
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:shiny="clr-namespace:Shiny;assembly=Shiny.Maui.Shell">
    <ContentPage.ToolbarItems>
        <ToolbarItem Text="Home"
                     shiny:Navigate.Route="MainPage"
                     shiny:Navigate.RelativeNavigation="False" />
    </ContentPage.ToolbarItems>

    <Button Text="Open Detail"
            shiny:Navigate.Route="Detail"
            shiny:Navigate.ParameterKey="ItemId"
            shiny:Navigate.ParameterValue="{Binding SelectedId}" />
</ContentPage>
```

For multiple parameters:

```xml
<Button Text="Open Modal"
        shiny:Navigate.Route="Modal">
    <shiny:Navigate.Parameters>
        <shiny:NavigationParameters>
            <shiny:NavigationParameter Key="Arg1" Value="{Binding NavArg}" />
            <shiny:NavigationParameter Key="Arg2" Value="5" />
        </shiny:NavigationParameters>
    </shiny:Navigate.Parameters>
</Button>
```

- Supported controls: `Button`, `MenuItem`, `ToolbarItem`
- `Navigate.Route` accepts the route string passed to `INavigator.NavigateTo(...)`
- Keep XAML navigation generic; strongly-typed helpers belong in generated C# extensions, not XAML attached properties

### 5d. AI Tool Navigation

Shiny MAUI Shell generates AI-compatible route metadata and navigation methods for use with `Microsoft.Extensions.AI`. An AI chat client can discover routes, understand their purpose, and navigate with parameters extracted from natural language.

**Describe routes for AI** — Add `description` to `[ShellMap]` and `[ShellProperty]`:

```csharp
public enum WorkOrderPriority { Low, Medium, High, Urgent }

[ShellMap<WorkOrderPage>(description: "Use when the user reports something broken or needing repair")]
public partial class WorkOrderViewModel : ObservableObject
{
    [ShellProperty("Summarize what is broken based on what the user said", required: true)]
    public string Description { get; set; } = string.Empty;

    [ShellProperty("Infer urgency from tone. Must be: Low, Medium, High, or Urgent", required: true)]
    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Medium;
}
```

**Generated AI class (`AiMauiShellTools`):**

The source generator produces an `AiMauiShellTools` class (name configurable via `ShinyMauiShell_AiToolsClassName`) that takes `INavigator` via constructor injection and provides:

- `Prompt` — pre-formatted string describing all AI-applicable routes for seeding AI system messages
- `Tools` — ready-to-use `AITool[]` instances for route discovery and navigation
- `GetAiToolApplicableGeneratedRoutes()` — returns only routes that have a description AND at least one parameter
- `NavigateToRoute(route, args)` — AI-friendly navigation using switch dispatch to `NavigateTo<TViewModel>` with direct property setters and automatic type conversion (`int`, `bool`, `double`, enums, `DateTime`, etc.)

Additionally, `GetGeneratedRouteInfo()` remains as a static extension on `INavigator` returning all routes with parameter metadata.

A generated `AddAiTools()` extension on `ShinyAppBuilder` registers the class as a singleton.

**Wire up AI tools (enabled by default when `Microsoft.Extensions.AI` is referenced):**

```csharp
// MauiProgram.cs
builder.UseShinyShell(x => x
    .AddGeneratedMaps()
    .AddAiTools()
);

// In your ViewModel — inject AiMauiShellTools
public class ChatViewModel(AiMauiShellTools aiTools)
{
    var options = new ChatOptions { Tools = [.. aiTools.Tools] };
}
```

**Key conventions for AI-friendly ViewModels:**
- Route descriptions should describe **user intent signals**, not just the page name — e.g. "Use when the user reports something broken" not "Work order page"
- Property descriptions should tell the AI to **infer values** from natural language — e.g. "Infer urgency from tone" not "The priority level"
- Use `GetAiToolApplicableGeneratedRoutes` (not `GetGeneratedRouteInfo`) to keep the AI focused on actionable routes
- Properties can be `string`, `int`, `bool`, `double`, enums, `DateTime`, `Guid`, etc. — the generated `NavigateToRoute` handles type conversion automatically
- Enums are especially AI-friendly — the model outputs the member name as a string and the generator parses it case-insensitively

### 6. Dialogs

Always use `IDialogs` for user-facing dialogs. Inject it via the primary constructor:

```csharp
public class MyViewModel(INavigator navigator, IDialogs dialogs)
{
    // Alert - informational message
    await dialogs.Alert("Title", "Something happened");

    // Confirm - yes/no question, returns bool
    bool confirmed = await dialogs.Confirm("Delete?", "Are you sure?");

    // Prompt - text input, returns string? (null if cancelled)
    var name = await dialogs.Prompt("Name", "Enter your name", placeholder: "John Doe");

    // Prompt with numeric keyboard
    var age = await dialogs.Prompt("Age", "Enter your age", keyboard: Keyboard.Numeric);

    // Action sheet - choose from options
    var choice = await dialogs.ActionSheet("Options", "Cancel", "Delete", "Edit", "Share");
}
```

### 7. ShellServices Aggregate & IMainThread

`ShellServices` is a convenience record that bundles the three shell services together. Inject it when a ViewModel or service needs most of them and you want a single parameter:

```csharp
public record ShellServices(
    INavigator Navigator,
    IDialogs Dialogs,
    IMainThread MainThread
);

public class MyViewModel(ShellServices shell)
{
    async Task DoWork()
    {
        shell.MainThread.BeginInvokeOnMainThread(() => /* UI update */);
        await shell.Dialogs.Alert("Done", "Work complete");
        await shell.Navigator.GoBack();
    }
}
```

`IMainThread` is the thread-marshalling abstraction used internally by `ShellNavigator` and `ShellDialogs`. Prefer it over `Microsoft.Maui.ApplicationModel.MainThread` inside Shiny Shell code because the default implementation (`MauiMainThread`) transparently works around platforms where MAUI's `MainThread.InvokeOnMainThreadAsync` is broken — currently macOS and Linux, where calls are executed inline instead of being dispatched.

```csharp
public interface IMainThread
{
    Task InvokeOnMainThreadAsync(Action action);
    Task InvokeOnMainThreadAsync(Func<Task> func);
    Task<T> InvokeOnMainThreadAsync<T>(Func<Task<T>> func);
    void BeginInvokeOnMainThread(Action action);
}
```

Both `ShellServices` and `IMainThread` are registered as singletons by `UseShinyShell()` — no extra setup required.

### 8. Modal Pages

Set `Shell.PresentationMode="Modal"` on the page XAML:
```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             Shell.PresentationMode="Modal"
             x:Class="MyApp.ModalPage">
```

Navigate to it like any other page. Close with `GoBack()`.

### 9. File Organization

Place files following standard MAUI conventions:
- Pages: `Views/{Name}Page.xaml` + `Views/{Name}Page.xaml.cs`
- ViewModels: `ViewModels/{Name}ViewModel.cs`
- Or co-locate: `Features/{Feature}/{Name}Page.xaml` + `{Name}ViewModel.cs`

## Source Generation Output

The source generator produces up to three files from `[ShellMap]` and `[ShellProperty]` attributes. Each can be individually disabled via MSBuild properties.

### Routes.g.cs
The constant name is derived from the `route` parameter (or page type name without `Page` suffix when no route is specified):
```csharp
public static class Routes
{
    public const string Detail = "Detail";
    public const string Settings = "Settings";
}
```

### NavigationExtensions.g.cs
Method names are also derived from the route parameter:
```csharp
public static class NavigationExtensions
{
    public static Task NavigateToDetail(this INavigator navigator, string itemId, int pageIndex = default)
    {
        return navigator.NavigateTo<DetailViewModel>(x =>
        {
            x.ItemId = itemId;
            x.PageIndex = pageIndex;
        });
    }
}
```

### NavigationBuilderExtensions.g.cs
Uses inline string literals (not `Routes.*` constants), so it works regardless of whether route constants are enabled:
```csharp
public static class NavigationBuilderExtensions
{
    public static ShinyAppBuilder AddGeneratedMaps(this ShinyAppBuilder builder)
    {
        builder.Add<DetailPage, DetailViewModel>("Detail");
        builder.Add<SettingsPage, SettingsViewModel>("Settings");
        return builder;
    }
}
```

### Configuring Source Generation

Disable individual generated files via MSBuild properties in `.csproj`:

```xml
<PropertyGroup>
    <!-- Disable Routes.g.cs -->
    <ShinyMauiShell_GenerateRouteConstants>false</ShinyMauiShell_GenerateRouteConstants>

    <!-- Disable NavigationExtensions.g.cs -->
    <ShinyMauiShell_GenerateNavExtensions>false</ShinyMauiShell_GenerateNavExtensions>

    <!-- Disable AI extensions (enabled by default, requires Microsoft.Extensions.AI) -->
    <ShinyMauiShell_GenerateAiExtensions>false</ShinyMauiShell_GenerateAiExtensions>

    <!-- Customize the generated AI tools class name (default: AiMauiShellTools) -->
    <ShinyMauiShell_AiToolsClassName>MyAppAiTools</ShinyMauiShell_AiToolsClassName>

    <!-- Customize the generated static extensions class name (default: AiExtensions) -->
    <ShinyMauiShell_AiExtensionsClassName>MyAppRouteExtensions</ShinyMauiShell_AiExtensionsClassName>

    <!-- Customize the AI navigate method name (default: NavigateToRoute) -->
    <ShinyMauiShell_AiNavigateMethodName>GoToPage</ShinyMauiShell_AiNavigateMethodName>
</PropertyGroup>
```

| Property | Default | Controls |
|---|---|---|
| `ShinyMauiShell_GenerateRouteConstants` | `true` | `Routes.g.cs` |
| `ShinyMauiShell_GenerateNavExtensions` | `true` | All navigation extensions and `AddGeneratedMaps` |
| `ShinyMauiShell_GenerateAiExtensions` | `true` | `AiMauiShellTools` class, `AddAiTools()`, `GetAiToolApplicableGeneratedRoutes`, `NavigateToRoute`, and `Prompt`. Requires `Microsoft.Extensions.AI` (**SHINY003** error if missing). Set to `false` to disable |
| `ShinyMauiShell_AiToolsClassName` | `AiMauiShellTools` | Class name for the generated AI tools class |
| `ShinyMauiShell_AiExtensionsClassName` | `AiExtensions` | Class name for the static route info extensions class |
| `ShinyMauiShell_AiNavigateMethodName` | `NavigateToRoute` | Method name for the AI-friendly navigate method |

## Complete ViewModel Example

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiny;

namespace MyApp.ViewModels;

[ShellMap<DetailPage>("Detail")]
public partial class DetailViewModel(INavigator navigator, IDialogs dialogs) : ObservableObject,
    IPageLifecycleAware,
    INavigationConfirmation,
    INavigationAware,
    IDisposable
{
    [ShellProperty]
    [ObservableProperty]
    string itemId;

    [ObservableProperty]
    string title;

    bool hasUnsavedChanges;

    // Page appeared
    public void OnAppearing()
    {
        // Load data, start listening, etc.
    }

    // Page disappearing
    public void OnDisappearing()
    {
        // Pause operations
    }

    // Confirm before leaving
    public async Task<bool> CanNavigate()
    {
        if (!hasUnsavedChanges)
            return true;

        return await dialogs.Confirm(
            "Unsaved Changes",
            "You have unsaved changes. Discard them?"
        );
    }

    // Mutate parameters before leaving
    public void OnNavigatingFrom(IDictionary<string, object> parameters)
    {
        parameters["LastViewedItem"] = ItemId;
    }

    [RelayCommand]
    async Task Save()
    {
        // Save logic
        hasUnsavedChanges = false;
        await navigator.GoBack(("Saved", true));
    }

    [RelayCommand]
    Task GoBack() => navigator.GoBack();

    public void Dispose()
    {
        // Cleanup subscriptions, timers, etc.
    }
}
```

## Best Practices

1. **Use source generation** - Always prefer `[ShellMap]` + `[ShellProperty]` + `AddGeneratedMaps()` over manual registration
2. **Inject INavigator** - Never use `Shell.Current.GoToAsync` directly; use `INavigator` for testability
3. **Inject IDialogs** - Never use `Shell.Current.DisplayAlert` directly; use `IDialogs` for testability
4. **Use primary constructors** - Inject dependencies via primary constructor parameters
5. **Use `[ShellProperty]`** - Properties are set directly by generated navigation methods — no `IQueryAttributable` needed
6. **Use ObservableObject** - From CommunityToolkit.Mvvm as the ViewModel base class
7. **Implement IDisposable** - Clean up event handlers and subscriptions to prevent memory leaks
8. **Use CanNavigate for guards** - Protect unsaved changes with `INavigationConfirmation`
9. **Mark ViewModel partial** - Required when using `[ShellMap]` source generation and CommunityToolkit attributes
10. **Pass results via GoBack args** - Return data to the previous page through navigation parameters
11. **Use tab badges only on shell tabs** - Badge APIs resolve existing tab routes in the active Shell
12. **Use `Navigate.*` for lightweight XAML wiring** - Prefer ViewModel commands when navigation needs branching logic or validation

## Reference Files

For detailed templates and examples, see:
- `reference/templates.md` - Page and ViewModel code generation templates
- `reference/api-reference.md` - Full API surface, interfaces, and attributes

## Common Packages

```bash
dotnet add package Shiny.Maui.Shell           # Core library with source generators
dotnet add package CommunityToolkit.Mvvm      # ObservableObject, RelayCommand, etc.
```
