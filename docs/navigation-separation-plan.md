# Separating Shell from Navigation — Core / Navigation / Shell

A reimplementation plan for the three-package split trialled on the `70_trials` branch
(commit `08be33c`, "considerations"). That branch is a **spike to read, not a branch to merge** — this
plan takes its design, resolves the four questions it left open, and sequences the rebuild.

Companion to [`mvvm-generation-plan.md`](./mvvm-generation-plan.md) — see §9, which amends that plan's
packaging decision now that a Core package exists.

## What the trial built

| Package | Contents |
|---|---|
| **`Shiny.Maui.Core`** | `IDialogs`, `IPageLifecycleAware`, `INavigationConfirmation`, `IMainThread`/`MauiMainThread`, `NavigationType`, `IShinyBuilder` |
| **`Shiny.Maui.Navigation`** | `ShinyNavigationBuilder` (+`TabsBuilder`/`FlyoutBuilder`) → `NavigationStructure` → `NavigationHost` → `ShinyNavigator`; `ShinyApplication`, `ShinyNavigationPage`, `TabBadgeManager`, `NavigationDialogs`, `NavigationBuilder` |
| **`Shiny.Maui.Shell`** | unchanged, minus the moved contracts; `ShinyAppBuilder` now implements `IShinyBuilder` |
| Dialog add-ons | retargeted to Core; `UseShinyDialogs<T>()` / `UseUxDiversDialogs<T>()` became generic over `IShinyBuilder` |
| `SampleNav`, `tests/Shiny.Maui.Navigation.Tests`, `skills/shiny-maui-navigation/` | new |

**The architecture is right and should be kept.** Four things in particular:

- **`ShinyNavigationBuilder` → `NavigationStructure` → `NavigationHost`.** Declaration (what the app's
  tree *is*) is separated from materialisation (building MAUI pages) and from navigation (mutating stacks).
  `NavigationStructure` is a pure record, which is why `StructureBuilderTests` can test the whole
  declaration surface with no UI and no platform — something the Shell library's URI routing has never
  allowed.
- **`NavigationHost.CreatePage` resolves the ViewModel, runs `configure`, *then* resolves the page and
  binds it.** The ViewModel is fully initialised before the page exists, so every downstream hook sees
  final state. This is why Navigation needs no equivalent of Shell's pending-ViewModel configurator.
- **`IShinyBuilder`** — one Core abstraction so an add-on package works with either navigation library.
  Proven by the dialog packages becoming `static T UseShinyDialogs<T>(this T builder) where T : IShinyBuilder`.
- **`ActiveNavigationPage`** — "modal stack if a modal is showing, else the current tab's stack" is the
  single question the navigator keeps asking, and answering it in one property is what keeps
  `ShinyNavigator` readable.

## Decisions (confirmed)

| # | Decision | Consequence |
|---|---|---|
| 1 | **Unify `INavigator` in Core.** Core declares the shared contract; each library extends it with `IShellNavigator` / `IPageNavigator`. | Fixes the trial's collision (both libraries declared `Shiny.INavigator`, `Shiny.INavigationBuilder`, `Shiny.NavigationEventArgs` — referencing both was a compile error). Gives the generator one type to emit against. Forces several signature changes (§2.2). |
| 2 | **The generator flavor-detects and emits for both.** Neutral `[PageMap<TPage>]` / `[PageProperty]` in Core; `[ShellMap]`/`[ShellProperty]` kept as aliases. | Navigation gains typed `NavigateToDetail(…)` and `AddGeneratedMaps()`; route constants stay Shell-only because Navigation has no routes. |
| 3 | **Dialog packages renamed** `Shiny.Maui.Shell.ShinyDialogs` → `Shiny.Maui.Dialogs.Shiny`, `Shiny.Maui.Shell.UxDiversDialogs` → `Shiny.Maui.Dialogs.UxDivers`. Old IDs deprecated on nuget.org. | Names match reality now that the extensions are generic over `IShinyBuilder`. |
| 4 | **Same repo, own skill.** `src/Shiny.Maui.Navigation` beside Shell, one solution, one CI pipeline; `skills/shiny-maui-navigation/` as a second skill. | Docs stay in the existing site section (**not** a separate `/mauinav` node) — the trial's `PackageProjectUrl` must change from `shinylib.net/mauinav` to a page under the current section. No repo rename. |

Packages after the split: `Shiny.Maui.Core`, `Shiny.Maui.Navigation`, `Shiny.Maui.Shell`,
`Shiny.Maui.Dialogs.Shiny`, `Shiny.Maui.Dialogs.UxDivers`.

---

## 1. `Shiny.Maui.Core`

Everything the trial put there, plus the unified navigation contracts and the generator attributes:

```
Shiny.Maui.Core/
├─ IDialogs.cs, IPageLifecycleAware.cs, INavigationConfirmation.cs, INavigatingAway.cs
├─ NavigationType.cs                     // one enum, per-library values documented
├─ IShinyBuilder.cs                      // + AddMap(...) — §3
├─ INavigator.cs, INavigationBuilder.cs, NavigationEventArgs.cs      // §2
├─ PageMapAttribute.cs, PagePropertyAttribute.cs                     // + ShellMap/ShellProperty aliases
├─ Infrastructure/IMainThread.cs, MauiMainThread.cs
└─ Infrastructure/GeneratedRouteInfo.cs  // AI route metadata, shared by both flavors
```

Multi-targets exactly as Shell does today (`net10.0` + android/ios/maccatalyst/windows), references
`Microsoft.Maui.Controls`, `RootNamespace` `Shiny`.

**Binary compatibility:** moving `IDialogs`, `INavigator` etc. out of `Shiny.Maui.Shell` changes their
assembly. Add `[assembly: TypeForwardedTo(typeof(...))]` in Shell for every moved public type so a
consumer assembly compiled against v6 still binds. Source compatibility is unaffected — same namespace.

## 2. The unified navigator

### 2.1 Shape

```csharp
namespace Shiny;

public interface INavigator
{
    event EventHandler<NavigationEventArgs>? Navigating;
    event EventHandler<NavigatedEventArgs>? Navigated;

    Page? CurrentPage { get; }
    object? CurrentViewModel { get; }

    Task NavigateTo(Type viewModelType, Action<object>? configure);
    Task GoBack(int backCount);
    Task PopToRoot();
    Task SetTabBadge(Type viewModelType, int value);
    Task ClearTabBadge(Type viewModelType);
    INavigationBuilder CreateBuilder();
}

public interface INavigationBuilder
{
    INavigationBuilder PopBack(int count = 1);
    INavigationBuilder FromRoot();
    INavigationBuilder Add<TViewModel>() where TViewModel : class;
    INavigationBuilder Add<TViewModel>(Action<TViewModel> configure) where TViewModel : class;
    Task Navigate();
}
```

Core also ships the ergonomic generic wrappers as **extension methods**, so they exist for anyone holding
an `INavigator` without being interface members that derived interfaces must dance around:

```csharp
public static Task NavigateTo<TViewModel>(this INavigator navigator, Action<TViewModel>? configure = null)
    where TViewModel : class;
public static Task GoBack(this INavigator navigator);
public static Task SetTabBadge<TViewModel>(this INavigator navigator, int value);
public static Task ClearTabBadge<TViewModel>(this INavigator navigator);
```

Library-specific surface stays on the derived interfaces:

| `IShellNavigator : INavigator` | `IPageNavigator : INavigator` |
|---|---|
| `NavigateTo(string route, bool relativeNavigation, params args)` | `NavigateTo(Type, bool animated)` |
| `NavigateTo<TVm>(Action<TVm>?, bool relativeNavigation, params args)` | `NavigateTo<TVm>(Action<TVm>?, bool animated)` |
| `GoBack(int backCount, params args)` · `PopToRoot(params args)` | `GoBack(int, bool animated)` · `PopToRoot(bool animated)` |
| `SwitchShell(Shell)` · `SwitchShell<TShell>()` | `PushModal`/`PopModal` · `SelectTab` · `OpenFlyout`/`CloseFlyout` · `SwitchRoot`/`RestoreRoot` |
| `SetTabBadge(string route, int)` · `ClearTabBadge(string route)` | `HasFlyout` |

Both are registered in DI pointing at the same singleton, so `INavigator` and `IShellNavigator`/
`IPageNavigator` can each be injected.

### 2.2 The overload-resolution constraint (read before writing signatures)

A derived interface method that differs from a base one only by **optional or `params` trailing
parameters** makes the no-argument call ambiguous. `nav.GoBack()` against an `IShellNavigator` that
inherits `INavigator.GoBack(int backCount = 1)` while declaring
`GoBack(int backCount = 1, params IEnumerable<(string, object)> args)` does not compile.

**Rule: Core primitives declare no optional and no `params` parameters.** Consequences for the existing
Shell API — all breaking, all in v7:

- `INavigator.CreateBuilder(bool fromRoot = false)` → `CreateBuilder()`, with `fromRoot` becoming
  `CreateBuilder().FromRoot()`. This is also the unification with Navigation's builder, which already
  had `FromRoot()`.
- `GoBack()` / `PopToRoot()` / `SetTabBadge<T>(int)` become Core **extension methods**, not interface
  members, so the parameterful Shell/Nav instance overloads never compete with a base-interface default.
- Shell's `NavigateTo<TVm>(configure, relativeNavigation, args)` stays an instance member on
  `IShellNavigator` — three parameters, no clash with the Core extension. When the variable is typed as
  `IShellNavigator` the instance method wins; typed as `INavigator`, the extension applies.

### 2.3 Event args

One Core record, with the Shell-only members nullable:

```csharp
public record NavigationEventArgs(
    object? FromViewModel,
    Type? ToViewModelType,
    NavigationType NavigationType,
    string? FromUri = null,                                    // Shell only
    string? ToUri = null,                                      // Shell only
    IReadOnlyDictionary<string, object>? Parameters = null     // Shell only
);
```

**Breaking for v6 consumers:** `ToUri` was non-nullable and `Parameters` was always present. The
alternative — a `ShellNavigationEventArgs` subclass — requires re-declaring the events with `new` on
`IShellNavigator`, which means implementers carry two event fields and subscribers of the base see
nothing. Not worth it; nullable members and a documented migration note are the cheaper trade.

`NavigationType` stays one Core enum with per-library values documented (`SwitchShell` is Shell-only;
`PushModal`/`PopModal`/`SelectTab`/`SwitchRoot` are Navigation-only), as the trial had it.

## 3. `IShinyBuilder` grows a registration method

For the generator to emit **one** `AddGeneratedMaps()` for both flavors, Core's builder abstraction must
carry the map registration:

```csharp
public interface IShinyBuilder
{
    MauiAppBuilder MauiBuilder { get; }
    void UseDialogs<TDialog>() where TDialog : class, IDialogs;
    void AddMap(Type pageType, Type viewModelType, string? route, bool registerRoute);
}
```

`ShinyAppBuilder` uses `route`/`registerRoute` as it does today. `ShinyNavigationBuilder` registers the
pair and ignores both (Navigation has no routes) — with a `[PageMap]` `route` value surfaced only as a
diagnostic name. Structure (`AddTabs`/`AddFlyout`/`SetRoot`) is **not** generated: it's declared in
`MauiProgram.cs` and the attributes carry no notion of tabs or flyouts.

## 4. `Shiny.Maui.Navigation`

Rebuild as the trial has it, with the fixes below. Component roles:

- **`ShinyNavigationBuilder`** + `TabsBuilder` / `FlyoutBuilder` — declaration only. `BuildStructure()`
  validates ("no navigation root declared", "AddFlyout requires a menu page") and returns the record.
- **`NavigationStructure`** / `PageRegistration` / `TabRegistration` — pure records, unit-testable.
- **`NavigationHost`** — `BuildRoot()` materialises FlyoutPage/TabbedPage/NavigationPage;
  `CreatePage(vmType, configure)` does resolve → configure → bind; `ActiveNavigationPage`,
  `DetailNavigationPage`, `ModalStack`, `CurrentPage`, `GetTabIndex`.
- **`ShinyNavigator : IPageNavigator, IMauiInitializeService, IDisposable`** — stack mutation, lifecycle
  hooks off `Application.PageAppearing`/`PageDisappearing`/`DescendantRemoved`, tab-change hooks, event raising.
- **`ShinyApplication`** — `CreateWindow` hands out `host.RootPage`, so the app class has no navigation code.
- **`ShinyNavigationPage`** — routes the Android hardware/gesture back button through `INavigationConfirmation`.
- **`NavigationDialogs`**, **`TabBadgeManager`** + platform partials (Android/iOS/Windows/Unsupported).

### 4.1 Issues in the trial code to fix on the rebuild

1. **`SwapWindowPage` sleeps.** `await Task.Delay(50)` between blanking and assigning `window.Page` is
   sleep-as-synchronisation, inherited from Shell's `SwitchShell`. Replace with an explicit dispatcher
   yield after `DisconnectHandler()`, or — if the delay is genuinely load-bearing on iOS — isolate it in
   one documented helper shared with Shell rather than duplicating the magic number in two libraries.
2. **The `pendingNavigationType` heuristic.** `OnPageAppearing` treats "no pending type" as
   `NavigationType.GoBack`, and `OnTabChanged` does `??= SelectTab`. Any missed `BeginNavigation` silently
   reports the wrong type. Make it explicit (a small state object set by every navigation entry point) and
   cover platform-initiated back with a test.
3. **Double `BuildRoot()`.** `Initialize` builds the tree and `ShinyApplication.CreateWindow` builds again
   when `RootPage` is null (hot restart). Two calls means two sets of ViewModels resolved and the first set
   never disposed. Make `BuildRoot()` idempotent, or have `CreateWindow` be the only caller.
4. **Guard coverage differs from Shell, per platform.** `INavigationConfirmation` fires on: programmatic
   navigation (both libraries), Android hardware back (Navigation only, via `ShinyNavigationPage`), Shell's
   `Navigating` deferral (Shell only). The iOS nav-bar back arrow is uninterceptable in MAUI in both. This
   needs a per-platform matrix in the docs, not a footnote — it's the single biggest behavioral difference
   between the two libraries.
5. **`RequireActiveNavigation()` throws for a modal pushed with `wrapInNavigationPage: false`.** Correct,
   but the message should name the calling API, and it deserves a test.
6. **`TabBadgeManager.ReapplyAll()` on every navigation** — fine, but confirm it's a no-op when no badges
   are set rather than a per-navigation platform call.

## 5. The generator

Rename `Shiny.Maui.Shell.SourceGenerators` → **`Shiny.Maui.SourceGenerators`** and pack the analyzer +
`Package.targets` from **`Shiny.Maui.Core`**, so both libraries carry it. MSBuild properties keep the
`ShinyMauiShell_*` names as a fallback and gain `ShinyMaui_*` equivalents; read new-then-old.

**Flavor detection** uses the trick already in `ShinyShellGenerator.cs:80` for the AI package:

```csharp
var flavor = compilation.GetTypeByMetadataName("Shiny.ShinyAppBuilder") != null ? Shell
           : compilation.GetTypeByMetadataName("Shiny.ShinyNavigationBuilder") != null ? Navigation
           : None;   // no output
```

Both referenced → diagnostic error. The namespace collision is gone, but two `INavigator` singletons in
one container is not a supported configuration: the packages remain alternatives.

| Generated file | Shell | Navigation |
|---|:---:|:---:|
| `Routes.g.cs` (route constants) | yes | **no** — no routes exist |
| `NavigationExtensions.g.cs` (`NavigateToDetail(id, …)`) | `bool relativeNavigation = true` | `bool animated = true` |
| `NavigationBuilderNavExtensions.g.cs` (`AddDetail(id)`) | yes | yes |
| `NavigationBuilderExtensions.g.cs` (`AddGeneratedMaps()`) | one emitter, `where T : IShinyBuilder`, via `AddMap(...)` | same |
| `AiExtensions.g.cs` | keyed by route | keyed by map name |

Attributes: `[PageMap<TPage>(name, registerRoute, description)]` and `[PageProperty(description, required)]`
in Core are the v7 vocabulary; `[ShellMap]`/`[ShellProperty]` remain as aliases the generator also matches,
so existing Shell apps compile untouched. Docs and skill teach `PageMap`.

**Deferred:** typed `SelectTabX()` / `PushModalX()` helpers for Navigation. Whether a ViewModel is a tab is
known only at `MauiProgram` time, not from an attribute — generating them would need tab/modal metadata on
`[PageMap]`, which is a separate decision.

## 6. Build order

| Phase | Work | Gate |
|---|---|---|
| **0** | Create `Shiny.Maui.Core`; move `IDialogs`, lifecycle, confirmation, `IMainThread`, `NavigationType`, `IShinyBuilder`; Shell references it; type-forwards added. | `Sample` builds and runs with **no source changes**. |
| **1** | Unified `INavigator`/`INavigationBuilder`/`NavigationEventArgs` in Core (§2); `IShellNavigator`; `CreateBuilder()`/`FromRoot()` change; extension wrappers. | `Sample` builds after the documented migration edits; existing `NavigatorContractTests` updated and green. |
| **2** | `IShinyBuilder.AddMap`; dialog packages retargeted **and renamed** to `Shiny.Maui.Dialogs.*`. | Both dialog providers register against `ShinyAppBuilder` through the generic extension. |
| **3** | `Shiny.Maui.Navigation`: builder → structure → host → navigator, `ShinyApplication`, `ShinyNavigationPage`, dialogs, tab badges + platform partials, with the §4.1 fixes. | `SampleNav` runs: tabs, flyout, push, modal, guard, root swap. |
| **4** | Generator: project rename, pack from Core, `[PageMap]`/`[PageProperty]`, flavor detection, both output flavors. | Both samples build; snapshot tests cover both flavors. |
| **5** | Tests: `StructureBuilderTests` port + host/navigator tests + generator flavor tests. | `dotnet test` green across all test projects. |
| **6** | Docs, skill, readme, release notes, CI/solution wiring. | §8. |

Phase 0 is separable and non-breaking — it can land on its own. Everything from phase 1 is v7-breaking.

## 7. Testing

- **Pure-logic tests (the big win).** `StructureBuilderTests` proves the declaration surface is testable
  without UI: root/tabs/flyout declaration, ordering, the two `BuildStructure()` validation throws,
  `GetRegistration`, `GetTabIndex`. Extend to `NavigationBuilder` segment accumulation
  (`PopBack` before `Add`, `PopBack`+`FromRoot` rejection).
- **`NavigationHost`** — `ActiveNavigationPage`/`CurrentPage` selection across the four shapes (single root,
  tabs, flyout+tabs, modal on top) using constructed page trees, no platform.
- **Generator** — snapshot tests per flavor: Shell-only compilation, Navigation-only compilation, both
  (diagnostic), neither (no output). Requires Core/Shell/Nav metadata references in the test harness.
- **Contract parity** — extend `NavigatorContractTests` to assert both `IShellNavigator` and
  `IPageNavigator` implement every Core member, and that no derived method differs from a base method only
  by optional/`params` tail (§2.2) — that's a compile-time trap worth a test that fails loudly.
- **Samples** — `Sample` (Shell) and `SampleNav` (Navigation) both in `MauiShell.slnx` and `Build.slnf`,
  both built in CI.

## 8. Docs & required updates (per CLAUDE.md)

1. **`readme.md`** — the trial's "Which package?" comparison table (keep it; it's good), a `Shiny.Maui.Core`
   line, the new dialog package names, and the `INavigator`/`IShellNavigator` migration note.
2. **Per-project readmes** — `src/Shiny.Maui.Core/readme.md` and `src/Shiny.Maui.Navigation/readme.md` pack
   into their own packages via the `Directory.build.targets` conditional the trial added (keep that).
3. **Skill** — `skills/shiny-maui-navigation/` as a second skill (the trial drafted 357 lines); the existing
   `shiny-maui-shell` skill gains a "you may want the Navigation package instead" pointer and drops
   `ShellMap`-only vocabulary in favour of `PageMap` with the alias noted.
4. **Docs site** — Navigation pages live **under the existing MAUI Shell section**, not a separate
   `/mauinav` node (decision #4): a navigation overview + structure/tabs/flyout pages, the per-platform
   guard matrix (§4.1.4), and the package-choice table. `PackageProjectUrl` in
   `Shiny.Maui.Navigation.csproj` must point there rather than `shinylib.net/mauinav`.
   Release notes: `<RN type="feature">` for the split and `<RN breaking>` entries for §2.2/§2.3, with a
   `### Migration from v6` block.

## 9. Amendment to the MVVM plan

[`mvvm-generation-plan.md`](./mvvm-generation-plan.md) decision #1 puts the MVVM runtime, dirty detection,
validation and `IErrorHandler` **inside `Shiny.Maui.Shell`** — a decision taken when Shell was the only
library. With Core in the picture that choice strands every Navigation user without `ObservableObject`,
`[RelayCommand]` or `[DirtyDetection]`, and the dirty-detection generator depends on `IDialogs` and
`INavigationConfirmation`, both of which now live in Core.

**Recommendation: move all of it to `Shiny.Maui.Core`.** This does not violate the "no new package"
intent behind decision #1 — Core exists for reasons of its own — and it keeps one MVVM implementation for
both navigation libraries. The generator changes are minimal since the analyzer now packs from Core too.

Sequencing between the two plans: **phase 0–1 here should land before the MVVM plan's phase 0**, so the
shared `ViewModelModel` work is written once, against Core, rather than moved afterwards.

## 10. Open questions

1. **Dialog package names.** Decision #3 fixes the prefix, but `Shiny.Maui.Dialogs.Shiny` stutters.
   `Shiny.Maui.Dialogs.Controls` (it is powered by `Shiny.Maui.Controls`) reads better and pairs naturally
   with `.UxDivers`. *Recommendation: `.Controls`, unless the `Shiny` suffix is deliberate branding.*
2. **Does `Shiny.Maui.Core` get a public `ShinyServices` record?** Shell has `ShellServices(INavigator,
   IDialogs, IMainThread)`; Navigation has no equivalent. Either promote it to Core or drop it.
3. **`INavigatingAway` vs `INavigationAware`.** Navigation's parameterless `INavigatingAway` belongs in
   Core; Shell's `INavigationAware.OnNavigatingFrom(IDictionary)` is parameter-dictionary-specific and
   stays in Shell. Should Shell also raise `INavigatingAway`, so a shared ViewModel works under both?
   *Recommendation: yes — Core hook first, Shell's dictionary hook second.*
4. **Is the 50 ms delay in the root swap real?** Worth an actual iOS test before copying it into a second
   library (§4.1.1).
5. **Does Navigation need a deep-link story at all?** The trial says no by design (no URIs). An app with
   push notifications still has to land the user somewhere — the answer may simply be "call
   `NavigateTo<T>` from your handler", but it should be a documented answer.
