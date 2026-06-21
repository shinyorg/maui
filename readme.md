# Shiny MAUI Shell

[![NuGet](https://img.shields.io/nuget/v/Shiny.Maui.Shell?style=for-the-badge)](https://www.nuget.org/packages/Shiny.Maui.Shell)

Make .NET MAUI Shell shinier with ViewModel lifecycle management, navigation services, and source generation to remove boilerplate, reduce errors, and make your app testable.

Inspired by [Prism Library](https://prismlibrary.com) by Dan Siegel and Brian Lagunas.

[Full Documentation](https://shinylib.net/maui)

---

## Features

### 🧭 Navigation — `INavigator`

| Capability | Description |
|:-----------|:------------|
| Route-based | `NavigateTo("Detail", args: [("Id", "123")])` |
| ViewModel-based | `NavigateTo<DetailViewModel>(vm => vm.Id = "123")` |
| Source-generated | `NavigateToDetail("123")` — zero guesswork |
| GoBack | Single page, multi-page `GoBack(3)`, or `PopToRoot()` |
| Root navigation | `NavigateTo<DashboardViewModel>(relativeNavigation: false)` — reset the stack |
| Navigation builder | Fluent multi-segment: `CreateBuilder().AddDetail(42).AddModal().Navigate()` |
| Shell switching | `SwitchShell(new MainShell())` or `SwitchShell<TShell>()` via DI |
| Tab badges | Numeric tab badges via route or ViewModel — `SetTabBadge<InboxViewModel>(3)` |
| XAML navigation | Attached properties on `Button`, `MenuItem`, and `ToolbarItem` |

### 💬 Dialogs — `IDialogs`

| Method | Returns |
|:-------|:--------|
| `Alert(title, message)` | `Task` |
| `Confirm(title, message)` | `Task<bool>` |
| `Prompt(title, message)` | `Task<string?>` |
| `ActionSheet(title, cancel, destructive, ...buttons)` | `Task<string>` |

> Thread-safe — dispatches to UI thread automatically. Inject separately from `INavigator` for clean separation of concerns.
>
> **Alternative providers (same `IDialogs` interface, no ViewModel changes needed):**
> - `Shiny.Maui.Shell.ShinyDialogs` — owned, animated, themeable dialogs powered by [Shiny.Maui.Controls](https://shinylib.net/controls/dialogs/) (never the native alert/prompt; identical across platforms).
> - `Shiny.Maui.Shell.UxDiversDialogs` — styled popup dialogs powered by [UXDivers Popups](https://github.com/uxdivers/uxd-popups).

### 📡 Navigation Events

| Event | Fires | Key Properties |
|:------|:------|:---------------|
| `Navigating` | Before navigation | `FromUri` · `FromViewModel` · `ToUri` · `NavigationType` · `Parameters` |
| `Navigated` | After page resolves | `ToUri` · `ToViewModel` · `NavigationType` · `Parameters` |

`NavigationType`: `Push` · `SetRoot` · `GoBack` · `PopToRoot` · `SwitchShell`

### ♻️ ViewModel Lifecycle

| Interface | Method | Purpose |
|:----------|:-------|:--------|
| `IPageLifecycleAware` | `OnAppearing()` / `OnDisappearing()` | Page visibility hooks |
| `INavigationConfirmation` | `Task<bool> CanNavigate()` | Guard navigation (unsaved changes, etc.) |
| `INavigationAware` | `OnNavigatingFrom(params)` | Mutate parameters before leaving |
| `IQueryAttributable` | `ApplyQueryAttributes(params)` | Receive navigation parameters (only for string-based `NavigateTo` — not needed with `[ShellProperty]`) |
| `IDisposable` | `Dispose()` | Cleanup when page leaves the stack |

### ⚡ Source Generation

| Generated File | What It Does |
|:----------------|:------------|
| `Routes.g.cs` | Static route constants — `Routes.Detail` |
| `NavigationExtensions.g.cs` | Typed methods — `NavigateToDetail(id, page)` with XML docs and `[Description]` attributes |
| `NavigationBuilderNavExtensions.g.cs` | Typed builder methods — `AddDetail(id, page)` |
| `NavigationBuilderExtensions.g.cs` | One-line DI — `AddGeneratedMaps()` |
| `AiExtensions.g.cs` | Route metadata — `GetGeneratedRouteInfo()`, plus `AiMauiShellTools` class with `Prompt`, `Tools`, `GetAiToolApplicableGeneratedRoutes()`, and `NavigateToRoute()` when AI extensions are enabled. Also generates `AddAiTools()` extension on `ShinyAppBuilder` for DI registration |

> Invalid route names produce **SHINY001** compiler errors. Disable individual outputs via MSBuild properties.

### 🔌 Custom Handlers

| Handler | Description |
|:--------|:------------|
| `DisableShellFlyoutSwipeHandler` | Disables the flyout swipe gesture while keeping the hamburger button functional. Opt-in via `DisableShellFlyoutSwipeHandler.Register()` |

### ✅ Zero Ceremony

- One base class change — `AppShell : ShinyShell` — for deterministic BindingContext assignment
- Page–ViewModel mapping with **automatic BindingContext** assignment
- Drop-in `[ShellMap]` attribute replaces manual route registration

---

## Getting Started

### 1. Install

```bash
dotnet add package Shiny.Maui.Shell
```

### 2. Configure MauiProgram.cs

**With source generation (recommended):**
```csharp
builder
    .UseMauiApp<App>()
    .UseShinyShell(x => x.AddGeneratedMaps());
```

**Manual registration:**
```csharp
builder
    .UseMauiApp<App>()
    .UseShinyShell(x => x
        .Add<MainPage, MainViewModel>(registerRoute: false) // pages in AppShell.xaml
        .Add<DetailPage, DetailViewModel>("Detail")
        .Add<SettingsPage, SettingsViewModel>("Settings")
    );
```

### 3. Set up AppShell

Your `AppShell` must inherit from `ShinyShell` instead of `Shell`:

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

> [!NOTE]
> Pages defined in AppShell.xaml should use `registerRoute: false`.

### 4. Navigate

Inject `INavigator` into your ViewModels:

```csharp
public class MyViewModel(INavigator navigator)
{
    // Route-based navigation with args
    await navigator.NavigateTo("Detail", args: [("ItemId", "123")]);

    // ViewModel-based navigation with strongly-typed configuration
    await navigator.NavigateTo<DetailViewModel>(vm => vm.ItemId = "123");

    // Source-generated strongly-typed method (preferred)
    await navigator.NavigateToDetail("123");

    // Root navigation — resets the stack
    await navigator.NavigateTo<DashboardViewModel>(relativeNavigation: false);

    // Go back with result
    await navigator.GoBack(("Result", selectedItem));

    // Go back multiple pages
    await navigator.GoBack(2);

    // Pop to root
    await navigator.PopToRoot();

    // Switch to a different Shell instance
    await navigator.SwitchShell(new MainAppShell());

    // Switch to a Shell resolved from DI
    await navigator.SwitchShell<MainAppShell>();

    // Set or clear a numeric badge on a tab in the active Shell
    await navigator.SetTabBadge("Inbox", 3);
    await navigator.SetTabBadge<InboxViewModel>(7);
    await navigator.ClearTabBadge("Inbox");
    await navigator.ClearTabBadge<InboxViewModel>();

    // Fluent multi-segment navigation builder
    await navigator
        .CreateBuilder()
        .AddDetail(id: 42)
        .AddModal()
        .Navigate();

    // Pop back 2 pages, then push
    await navigator
        .CreateBuilder()
        .PopBack(2)
        .AddHome()
        .Navigate();

    // Navigate from root with builder
    await navigator
        .CreateBuilder(fromRoot: true)
        .AddDashboard()
        .AddDetail(id: 1)
        .Navigate();
}
```

> [!IMPORTANT]
> Root navigation (`relativeNavigation: false` or `CreateBuilder(fromRoot: true)`) uses the `//` URI prefix, which requires the target route to be declared in your `AppShell.xaml`. Routes registered only via `Routing.RegisterRoute` or `[ShellMap]` cannot be navigated to from root. Add the page as a `ShellContent` in your Shell XAML and use `registerRoute: false` in `[ShellMap]`.

> [!NOTE]
> If you're setting arguments on the ViewModel navigation, you should make them observable if they are bound on the Page.

> [!IMPORTANT]
> Tab badges only work for routes that are already present as tabs in the active Shell. The badge APIs are supported on Android, iOS, Mac Catalyst, and Windows. Linux and macOS AppKit throw `PlatformNotSupportedException`.

### 4.1 XAML Navigation

Use `Navigate` attached properties when you want route-based navigation directly from XAML without a ViewModel command:

```xml
<ContentPage
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:shiny="clr-namespace:Shiny;assembly=Shiny.Maui.Shell">

    <Button Text="Open Detail"
            shiny:Navigate.Route="Detail"
            shiny:Navigate.ParameterKey="ItemId"
            shiny:Navigate.ParameterValue="{Binding SelectedId}" />

    <ToolbarItem Text="Home"
                 shiny:Navigate.Route="MainPage"
                 shiny:Navigate.RelativeNavigation="False" />
</ContentPage>
```

For multiple parameters:

```xml
<Button Text="Open Modal"
        shiny:Navigate.Route="modal">
    <shiny:Navigate.Parameters>
        <shiny:NavigationParameters>
            <shiny:NavigationParameter Key="Arg1" Value="{Binding NavArg}" />
            <shiny:NavigationParameter Key="Arg2" Value="5" />
        </shiny:NavigationParameters>
    </shiny:Navigate.Parameters>
</Button>
```

`Navigate` currently supports `Button`, `MenuItem`, and `ToolbarItem`.

### 5. Dialogs

Inject `IDialogs` for user-facing dialogs:

```csharp
public class MyViewModel(IDialogs dialogs)
{
    // Alert
    await dialogs.Alert("Error", "Something went wrong");

    // Confirm
    if (await dialogs.Confirm("Delete?", "Are you sure?"))
    {
        // delete
    }

    // Prompt for text input
    var name = await dialogs.Prompt("Name", "Enter your name", placeholder: "John Doe");
    if (name != null)
    {
        // user entered a value
    }

    // Action sheet
    var choice = await dialogs.ActionSheet("Options", "Cancel", "Delete", "Edit", "Share");
}
```

### 6. Shiny Controls Dialogs (Optional)

Replace the default native dialogs with the owned, animated, themeable dialog service from [Shiny.Maui.Controls](https://shinylib.net/controls/dialogs/). The dialog is always rendered by the library, so it looks identical across platforms:

```bash
dotnet add package Shiny.Maui.Shell.ShinyDialogs
```

Configure in `MauiProgram.cs` — `UseShinyControls()` registers the underlying `IDialogService`, and `UseShinyDialogs()` routes `IDialogs` through it:
```csharp
builder
    .UseMauiApp<App>()
    .UseShinyControls()             // registers the Shiny.Maui.Controls IDialogService
    .UseShinyShell(x => x
        .UseShinyDialogs()          // route IDialogs through the Controls dialog service
        .AddGeneratedMaps()
    )
```

Your ViewModels continue using `IDialogs` as before — only the visual presentation changes.

### 7. UxDivers Dialogs (Optional)

Replace the default platform dialogs with styled popups from [UXDivers Popups](https://github.com/uxdivers/uxd-popups):

```bash
dotnet add package UXDivers.Popups.Maui
```

Add theme dictionaries to `App.xaml`:
```xml
<ResourceDictionary.MergedDictionaries>
    <!-- your existing styles -->
    <uxd:DarkTheme xmlns:uxd="clr-namespace:UXDivers.Popups.Maui.Controls;assembly=UXDivers.Popups.Maui" />
    <uxd:PopupStyles xmlns:uxd="clr-namespace:UXDivers.Popups.Maui.Controls;assembly=UXDivers.Popups.Maui" />
</ResourceDictionary.MergedDictionaries>
```

Configure in `MauiProgram.cs`:
```csharp
builder
    .UseMauiApp<App>()
    .UseUxDiversDialogs()       // Initialize UxDivers popup infrastructure
    .UseShinyShell(x => x
        .UseUxDiversDialogs()   // Register as IDialogs provider
        .AddGeneratedMaps()
    )
```

Your ViewModels continue using `IDialogs` as before — only the visual presentation changes.

---

## Navigation Events

Subscribe to `Navigating` and `Navigated` on `INavigator` for cross-cutting concerns like logging or analytics:

```csharp
public class NavigationLogger(
    ILogger<NavigationLogger> logger,
    INavigator navigator
) : IMauiInitializeService
{
    public void Initialize(IServiceProvider services)
    {
        navigator.Navigating += (_, args) =>
            logger.LogInformation("Navigating from '{From}' to '{To}' ({Type})",
                args.FromUri, args.ToUri, args.NavigationType);

        navigator.Navigated += (_, args) =>
            logger.LogInformation("Navigated to '{To}' - ViewModel: {VM} ({Type})",
                args.ToUri, args.ToViewModel?.GetType().Name, args.NavigationType);
    }
}

// Register in MauiProgram.cs
builder.Services.AddSingleton<IMauiInitializeService, NavigationLogger>();
```

---

## ViewModel Lifecycle

Implement these interfaces on your ViewModels as needed. Works just like [Prism Library](https://prismlibrary.com).

```csharp
[ShellMap<DetailPage>("Detail", description: "Navigate to the detail page")]
public partial class DetailViewModel(INavigator navigator, IDialogs dialogs) : ObservableObject,
    IPageLifecycleAware,
    INavigationConfirmation,
    IDisposable
{
    [ShellProperty("The item identifier")]
    [ObservableProperty]
    string itemId;

    public void OnAppearing() { /* load data */ }
    public void OnDisappearing() { /* pause */ }

    public async Task<bool> CanNavigate()
    {
        if (!hasUnsavedChanges) return true;
        return await dialogs.Confirm("Unsaved Changes", "Discard changes?");
    }

    public void Dispose() { /* cleanup */ }
}
```

---

## Source Generation

Decorate your ViewModels with `[ShellMap]` and `[ShellProperty]` to eliminate boilerplate:

**Input:**
```csharp
[ShellMap<DetailPage>("Detail", description: "Navigate to the detail page")]
public partial class DetailViewModel : ObservableObject
{
    [ShellProperty("The item identifier")]
    public string ItemId { get; set; }

    [ShellProperty("Page number for pagination", required: false)]
    public int Page { get; set; }
}
```

**Generated output:**

```csharp
// Routes.g.cs — constant name matches the route parameter
public static class Routes
{
    public const string Detail = "Detail";
}

// NavigationExtensions.g.cs — typed INavigator methods with XML docs and [Description] attributes
public static class NavigationExtensions
{
    /// <summary>
    /// Navigate to the detail page
    /// </summary>
    /// <param name="itemId">The item identifier</param>
    /// <param name="page">Page number for pagination</param>
    /// <param name="relativeNavigation">If true, it will navigate/stack from where the application currently is otherwise, it will reset the stack to this new route</param>
    [Description("Navigate to the detail page")]
    public static Task NavigateToDetail(this INavigator navigator,
        [Description("The item identifier")] string itemId,
        [Description("Page number for pagination")] int page = default,
        [Description("If true, it will navigate/stack from where the application currently is otherwise, it will reset the stack to this new route")] bool relativeNavigation = true)
    {
        return navigator.NavigateTo<DetailViewModel>(x =>
        {
            x.ItemId = itemId;
            x.Page = page;
        }, relativeNavigation);
    }
}

// NavigationBuilderNavExtensions.g.cs — typed INavigationBuilder methods
public static class NavigationBuilderNavExtensions
{
    public static INavigationBuilder AddDetail(this INavigationBuilder builder,
        string itemId, int page = default)
    {
        return builder.Add<DetailViewModel>(x => { x.ItemId = itemId; x.Page = page; });
    }
}

// NavigationBuilderExtensions.g.cs — uses string literals (not Routes.*)
public static class NavigationBuilderExtensions
{
    public static ShinyAppBuilder AddGeneratedMaps(this ShinyAppBuilder builder)
    {
        builder.Add<DetailPage, DetailViewModel>("Detail");
        return builder;
    }
}

// AiExtensions.g.cs — route metadata (always generated)
public static class AiExtensions
{
    [Description("This provides a list of routes throughout the application")]
    public static GeneratedRouteInfo[] GetGeneratedRouteInfo(this INavigator navigator) =>
    [
        new("Detail", "Navigate to the detail page",
            [new("ItemId", "The item identifier", "string", true),
             new("Page", "Page number for pagination", "int", false)])
    ];
}

// --- AI class and DI extension below generated when AI extensions are enabled ---
// (enabled by default when Microsoft.Extensions.AI is referenced)

// AiMauiShellTools — inject via DI for AI-powered navigation
public class AiMauiShellTools
{
    public AiMauiShellTools(INavigator navigator) { ... }

    // Pre-formatted prompt describing all AI-applicable routes
    public string Prompt { get; }

    // Ready-to-use AITool[] for route discovery and navigation
    public AITool[] Tools { get; }

    // Filtered routes with descriptions and parameters
    public GeneratedRouteInfo[] GetAiToolApplicableGeneratedRoutes() => ...;

    // AI-friendly navigation with automatic type conversion
    public Task<string> NavigateToRoute(string route, Dictionary<string, string>? args = null) { ... }
}

// DI registration extension
public static class AiMauiShellToolsExtensions
{
    public static ShinyAppBuilder AddAiTools(this ShinyAppBuilder builder) { ... }
}
```

Then use it:
```csharp
// MauiProgram.cs - one line to register everything, including AI tools
builder.UseShinyShell(x => x
    .AddGeneratedMaps()
    .AddAiTools()          // registers AiMauiShellTools as singleton
);

// Navigate with generated extension methods - no guesswork
await navigator.NavigateToDetail("123", page: 2);

// Fluent builder with generated extensions
await navigator.CreateBuilder().AddDetail("123", page: 2).Navigate();

// Get route metadata for tooling
var routes = navigator.GetGeneratedRouteInfo();

// AI integration — inject AiMauiShellTools via DI
public class ChatViewModel(AiMauiShellTools aiTools)
{
    var options = new ChatOptions { Tools = [.. aiTools.Tools] };
    // aiTools.Prompt contains the pre-formatted route prompt
}
```

### Route Naming

The `route` parameter in `[ShellMap]` drives the generated constant and method names. It must be a valid C# identifier — invalid names produce a **SHINY001** compiler error.

```csharp
// Route drives the constant and method name
[ShellMap<HomePage>("Dashboard")]
// → Routes.Dashboard = "Dashboard"
// → NavigateToDashboard(...)

// No route — falls back to page type name without "Page" suffix
[ShellMap<HomePage>]
// → Routes.Home = "HomePage"
// → NavigateToHome(...)
```

### Configuring Source Generation

Disable individual generated files via MSBuild properties:

```xml
<PropertyGroup>
    <!-- Disable Routes.g.cs -->
    <ShinyMauiShell_GenerateRouteConstants>false</ShinyMauiShell_GenerateRouteConstants>

    <!-- Disable NavigationExtensions.g.cs, NavigationBuilderNavExtensions.g.cs, and NavigationBuilderExtensions.g.cs (AddGeneratedMaps) -->
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
| `ShinyMauiShell_GenerateAiExtensions` | `true` | `AiMauiShellTools` class, `AddAiTools()`, `GetAiToolApplicableGeneratedRoutes`, `NavigateToRoute`, and `Prompt`. Requires `Microsoft.Extensions.AI` package (**SHINY003** error if missing). Set to `false` to disable |
| `ShinyMauiShell_AiToolsClassName` | `AiMauiShellTools` | Class name for the generated AI tools class |
| `ShinyMauiShell_AiExtensionsClassName` | `AiExtensions` | Class name for the static route info extensions class |
| `ShinyMauiShell_AiNavigateMethodName` | `NavigateToRoute` | Method name for the AI-friendly navigate method |

`NavigationBuilderExtensions.g.cs` (`AddGeneratedMaps()`) is only generated when `[ShellMap]` attributes are detected and `ShinyMauiShell_GenerateNavExtensions` is not set to `false`. A **SHINY002** warning is emitted if maps are detected but nav extensions are disabled.

---

## AI Integration

Shiny MAUI Shell's source generation produces metadata and navigation methods designed for AI tool calling via [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI). An AI chat client can discover your app's routes, understand their purpose, extract parameters from natural language, and navigate to the correct page — all with just two tools.

### How It Works

1. **Describe your routes** — Add `description` to `[ShellMap]` and `[ShellProperty]` to explain what each page does and what its parameters mean:

```csharp
public enum WorkOrderPriority { Low, Medium, High, Urgent }

[ShellMap<WorkOrderPage>(description: "Use when the user reports something broken, malfunctioning, or needing repair")]
public partial class WorkOrderViewModel : ObservableObject
{
    [ShellProperty("Summarize what is broken based on what the user said", required: true)]
    public string Description { get; set; } = string.Empty;

    [ShellProperty("Infer urgency from tone. Must be: Low, Medium, High, or Urgent", required: true)]
    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Medium;
}
```

2. **Install `Microsoft.Extensions.AI`** — AI extensions are enabled by default when this package is referenced:

```bash
dotnet add package Microsoft.Extensions.AI
```

3. **Register AI tools in DI** — Use the generated `AddAiTools()` extension:

```csharp
builder.UseShinyShell(x => x
    .AddGeneratedMaps()
    .AddAiTools()          // registers AiMauiShellTools as singleton
);
```

4. **Inject and use `AiMauiShellTools`** — The generated class provides `Prompt` and `Tools` properties:

```csharp
public class ChatViewModel(AiMauiShellTools aiTools)
{
    // Seed the system prompt with route info
    history.Add(new ChatMessage(ChatRole.System, aiTools.Prompt));

    // Use ready-to-use AITool instances
    var options = new ChatOptions { Tools = [.. aiTools.Tools] };
    var response = await chatClient.GetResponseAsync(history, options);
}
```

The AI calls `GetAiToolApplicableGeneratedRoutes` to discover what pages exist and what they do, then calls `NavigateToRoute` with the appropriate route and parameters extracted from the user's message. `NavigateToRoute` dispatches to `NavigateTo<TViewModel>` with direct property setters — no string-based Shell navigation involved. String values from the AI are automatically converted to the target property type (`int`, `bool`, `double`, enums, `DateTime`, etc.).

### GeneratedRouteParameter

Each parameter in the route info includes:

| Field | Description |
|:------|:------------|
| `ParameterName` | The property name (used as key in `NavigateToRoute` args) |
| `Description` | From `[ShellProperty("...")]` — tells the AI what this field means |
| `TypeName` | CLR type (`string`, `int`, `bool`, etc.) — tells the AI what format to use |
| `IsRequired` | Whether the AI must provide this value |

### Sample App — GitHub Copilot Authentication

The sample application includes a working AI chat demo that authenticates via **GitHub Copilot** using the OAuth device flow. This lets anyone with a Copilot subscription test AI-driven navigation using their own account — no API keys to configure.

The flow:
1. User taps **Login with GitHub** — the app requests a device code and opens `github.com/login/device` in the browser
2. User enters the displayed code to authorize
3. The app exchanges the GitHub token for a Copilot API token and creates an `IChatClient` via `Microsoft.Extensions.AI.OpenAI` (the Copilot API is OpenAI-compatible)
4. The chat injects `AiMauiShellTools` which provides `Prompt` and `Tools` for route discovery and navigation

The relevant sample files:
- `Sample/AI/ChatPage.xaml` — Chat UI using `Shiny.Maui.Controls.ChatView`
- `Sample/AI/ChatViewModel.cs` — AI client setup and tool registration
- `Sample/AI/GitHubCopilotAuthService.cs` — Device flow OAuth + token management
- `Sample/AI/TestWorkOrderViewModel.cs` — AI-navigable work order form
- `Sample/AI/ContactFormViewModel.cs` — AI-navigable contact form

---

## Custom Handlers

Optional handlers that are **not registered by default**. Call `Register()` in your `MauiProgram.cs` to opt in.

### Disable Flyout Swipe

Prevents the Shell flyout from opening via swipe gesture while keeping the hamburger button functional:

```csharp
using Shiny.Handlers;

// In MauiProgram.cs, before builder.Build()
DisableShellFlyoutSwipeHandler.Register();
```

| Platform | Behavior |
|:---------|:---------|
| Android | Locks the `DrawerLayout` to `LockModeLockedClosed` |
| iOS / Mac Catalyst | Disables `UIPanGestureRecognizer` on the Shell view hierarchy |
| Windows | No-op (Windows Shell has no swipe flyout) |
