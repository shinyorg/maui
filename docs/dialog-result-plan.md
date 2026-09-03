# ViewModel Dialogs — awaiting a typed result from a page

A plan for `navigator.ShowDialog<TViewModel, T>(...)`: present a mapped Page/ViewModel pair as a
dialog and `await` a strongly typed result from it.

Companion to [`navigation-separation-plan.md`](./navigation-separation-plan.md) — the contracts here
are written so they can move to `Shiny.Maui.Core` unchanged when the three-package split lands (§7).

## The starting point

The idea began as a method on `IDialogs`:

```csharp
public interface IDialogResult<out T>
{
    event EventHandler<T> ValueSet;
    event EventHandler Cancelled;
}

// on IDialogs
Task<T> RequestDialogResult<TViewModel, T>(Action<TViewModel>? actionSetter, CancellationToken cancellationToken = default)
    where TViewModel : IDialogResult<T>;
```

The **shape of the ViewModel contract is right and is kept**: two events on the ViewModel, with
`out T` covariance. Covariance here is legal specifically because `EventHandler<in TEventArgs>` is
contravariant, so `T` inside `event EventHandler<T>` lands in a covariant position — verified by
compiling `IDialogResult<string>` → `IDialogResult<object>` and raising through it. A single
`Completed` event carrying an args *class* could not be covariant (variance is interfaces and
delegates only), so two events is the right call, not a compromise.

Four things change.

## Decisions

| # | Decision | Why |
|---|---|---|
| 1 | **The method lives on `INavigator`, not `IDialogs`.** | `IDialogs` has three implementations (`ShellDialogs`, `ShinyDialogs`, `UxDiversDialogs`), two of them over popup frameworks that don't navigate. This method needs `GetPageTypeForViewModel`, DI page/ViewModel resolution, and the lifecycle + disposal hooks — all of which live in `ShinyShellNavigator`. And the separation plan moves `IDialogs` to `Shiny.Maui.Core`, which has no navigator at all. The original comment *"the TViewModel must be in the navigation list"* already says this is navigation. |
| 2 | **Presentation is delegated to `IDialogPresenter`.** | Keeps the plumbing (resolve → configure → subscribe → await → tear down) in one place while leaving *how it appears* swappable. Default is a Shell modal; the popup packages can register their own later without reimplementing any of the result machinery. |
| 3 | **`Task<DialogResult<T>>`, not `Task<T>`.** | `default(T)` can't express cancellation for value types — a `bool` dialog cannot distinguish "user chose No" from "user dismissed". `Prompt` only gets away with returning `null` because it returns `string?`. |
| 4 | **The generator emits a typed, fully inferred wrapper per dialog ViewModel.** | C# never infers type arguments from constraints, only from arguments, and inference is all-or-nothing — `RequestDialogResult<PickColorViewModel>(...)` is `error CS0305: requires 2 type arguments`. The "ideally `T` can be implicitly understood" wish is unreachable by signature; it is reachable by codegen, which is how `NavigateTo` already works. |
| 5 | **Renamed `IDialogResult<T>` → `IDialogAware<T>`.** | It is implemented by a *ViewModel*, so "result" reads as the wrong thing, and it collided with the `DialogResult<T>` return type. `IDialogAware` matches the existing `INavigationAware` / `IPageLifecycleAware` / `INavigationConfirmation` convention. `ValueSet` → `Completed`, `actionSetter` → `configure` (matching `INavigator.NavigateTo`). |

---

## 1. Contracts

```csharp
namespace Shiny;

/// Implemented by a ViewModel that can be presented as a dialog and return a value of T.
public interface IDialogAware<out T>
{
    event EventHandler<T> Completed;
    event EventHandler Cancelled;
}

/// The outcome of a dialog: a value, or cancellation.
public readonly record struct DialogResult<T>(bool IsCancelled, T? Value)
{
    public static DialogResult<T> Cancel() => new(true, default);
    public static DialogResult<T> Complete(T value) => new(false, value);

    public bool TryGetValue([MaybeNullWhen(false)] out T value);
    public T ValueOr(T fallback);
}
```

On `INavigator`:

```csharp
Task<DialogResult<T>> ShowDialog<TViewModel, T>(
    Action<TViewModel>? configure = null,
    CancellationToken cancellationToken = default
) where TViewModel : class, IDialogAware<T>;
```

There is no base class. ViewModels already spend their base-class slot on `ObservableObject`, so the
contract has to be implementable as two event declarations:

```csharp
[ShellMap<PickColorPage>("PickColor")]
public partial class PickColorViewModel : ObservableObject, IDialogAware<string>
{
    public event EventHandler<string>? Completed;
    public event EventHandler? Cancelled;

    [ShellProperty(required: false)]
    public string Preset { get; set; } = "";

    [RelayCommand] void Pick(string colour) => this.Completed?.Invoke(this, colour);
    [RelayCommand] void Cancel() => this.Cancelled?.Invoke(this, EventArgs.Empty);
}
```

## 2. `IDialogPresenter`

One method. "Show this page, complete when it is no longer shown, and take it down if the token
fires."

```csharp
namespace Shiny;

public interface IDialogPresenter
{
    Task Present(Page page, object viewModel, CancellationToken dismiss);
}
```

Contract notes, because both halves matter:

- The returned Task completes when the dialog is **gone** — whether the user dismissed it (swipe-down,
  hardware back, tap-outside) or `dismiss` asked for teardown.
- It **must not** throw `OperationCanceledException` when `dismiss` fires. Token-driven teardown is
  the normal success path, not a fault. Genuine presentation failures should still throw.
- It is responsible for dispatching to the main thread.

The default, registered by `UseShinyShell()` via `TryAddSingleton`:

```
ShellModalDialogPresenter
  ├─ subscribes page.ParentChanged (before pushing)
  ├─ Shell.Current.Window.Navigation.PushModalAsync(page, animated: true)
  ├─ awaits whichever comes first: the page being detached, or `dismiss`
  └─ on `dismiss`: PopModalAsync, if this page is the top of ModalStack
```

Two details that are easy to get wrong, both settled by reading MAUI's source:

**Use `Window.Navigation`, not `Shell.Navigation`.** `Shell.Navigation` is a `NavigationProxy` that
reinterprets modal calls — outside an active Shell navigation, its `OnPopModal` becomes
`Shell.GoToAsync("..")`:

```csharp
protected override async Task<Page> OnPopModal(bool animated) {
    if (!_shell.NavigationManager.AccumulateNavigatedEvents) {
        Page page = _shell.CurrentPage;
        await _shell.GoToAsync("..", animated);    // a route navigation, not a modal pop
        return page;
    }
    ...
}
```

That would run the `INavigationConfirmation` guard, raise Shell's navigating events, and pop whatever
Shell believes is current rather than the dialog page. `Window.Navigation` goes straight to
`ModalNavigationManager`.

**Detect dismissal with `Element.ParentChanged`, not `Window.ModalPopped`.** Every teardown path in
`ModalNavigationManager` ends in `RemoveLogicalChild(page)`, but only `PopModalAsync` raises
`ModalPopped` — `SyncPlatformModalStackAsync`, which reconciles a platform-initiated dismissal, pops
and detaches without it. Watching the parent catches strictly more cases. `ParentChanged` also fires
on the push, so the handler completes only when `page.Parent == null`.

Pushing modally (rather than `GoToAsync` to a route with `Shell.PresentationMode="Modal"` set in
XAML) is deliberate: it presents modally regardless of what the page's XAML says, and hands us the
exact page instance so there is no configurator pinning race.

Replaceable via `ShinyAppBuilder.UseDialogPresenter<T>()`.

## 3. Flow in `ShinyShellNavigator.ShowDialog<TViewModel, T>`

```
1. page type      ← navBuilder.GetPageTypeForViewModel(typeof(TViewModel))   [throws if unmapped]
2. viewmodel      ← DI
3. subscribe Completed/Cancelled → TaskCompletionSource<DialogResult<T>>     [before configure]
4. configure?.Invoke(vm)
5. page           ← DI on the main thread;  page.BindingContext = vm
6. presentation   = presenter.Present(page, vm, linkedCts.Token)
7. await Task.WhenAny(completion, presentation)
     ├─ completion won  → cancel linkedCts (tear the dialog down), await presentation
     └─ presentation won → the user dismissed it → result is DialogResult<T>.Cancel()
8. cancellationToken.ThrowIfCancellationRequested()   → caller cancellation surfaces as OCE
9. finally: unsubscribe both events
```

Subscribing at step 3 (before `configure`) means a ViewModel that completes during configuration —
or synchronously during `OnAppearing` — is still captured.

The two cancellation concepts stay distinct on purpose:

| Source | Result |
|---|---|
| ViewModel raised `Cancelled` | `DialogResult<T>` with `IsCancelled == true` |
| User dismissed the dialog | `DialogResult<T>` with `IsCancelled == true` |
| Caller's `CancellationToken` fired | `OperationCanceledException` |

**Every dismissal path completes the awaiting Task.** This is the hole in the original two-event
sketch: hardware back, an iOS swipe-down, or a tap-outside fires neither event, so the `await` hangs
forever. Here, "the presentation ended without the ViewModel completing" *is* cancellation, and the
TCS `TrySetResult` guard makes double-raising a `Completed` event harmless.

## 4. Source generator

New output `DialogExtensions.g.cs`, emitted only for `[ShellMap<TPage>]` ViewModels whose symbol
implements `Shiny.IDialogAware<T>`. The file is skipped entirely when there are none.

`ShellMapInfo` gains `DialogResultTypeFullName` (null when the ViewModel is not dialog-aware),
resolved by walking `AllInterfaces` on the ViewModel symbol.

`[ShellProperty]` parameters flow through exactly as they do for `NavigateTo{Name}` — same required-
before-optional ordering, same `[Description]` attributes, same enum-to-string conversion:

```csharp
public static Task<DialogResult<string>> ShowPickColorDialog(
    this INavigator navigator,
    string preset = null,
    CancellationToken cancellationToken = default
) => navigator.ShowDialog<PickColorViewModel, string>(x => { x.Preset = preset; }, cancellationToken);
```

Call site: `var result = await navigator.ShowPickColorDialog(preset: "red");` — zero type arguments.

Gated by the existing `ShinyMauiShell_GenerateNavExtensions` property, alongside the other nav
extensions. Dialog methods are deliberately **not** added to the AI tool surface — an AI agent
should be driving navigation, not blocking on a modal awaiting human input.

## 5. Lifecycle

Traced through MAUI 10.0.51's `ModalNavigationManager`, `Page`, `Element` and `Window` rather than
assumed. The full chain, for a page pushed with `Window.Navigation.PushModalAsync`:

**Push** — `_window.AddLogicalChild(modal)` parents the page to the `Window` (whose own parent is the
`Application`). Then:

```csharp
if (_window.Page is Shell shell) {
    if (!shell.CurrentItem.CurrentItem.IsPushingModalStack) {
        previousPage?.SendDisappearing();   // the page under the dialog
        CurrentPage?.SendAppearing();       // the dialog
    }
}
```

`IsPushingModalStack` / `IsPoppingModalStack` are set **only** inside `ShellSection`'s own
Shell-driven modal navigation (i.e. `GoToAsync` to a `Shell.PresentationMode="Modal"` route). A direct
`PushModalAsync` leaves them false, so both hooks fire. `Page.SendAppearing` then calls
`FindApplication(this)?.OnPageAppearing(this)`, which walks `page → Window → Application`, so
`Application.PageAppearing` is raised and `AppOnPageAppearing` runs.

**Pop** — `modal.SendDisappearing()` (parent still set, so `Application.PageDisappearing` is raised),
then `CurrentPage?.SendAppearing()` on the page underneath, then
`modal.Parent?.RemoveLogicalChild(modal)` → `Element.OnChildRemoved` → `OnDescendantRemoved` bubbling
up to `Application.DescendantRemoved`.

| Hook | Fires? | Where |
|---|---|---|
| `IPageLifecycleAware.OnAppearing` (dialog) | ✅ on push | `ModalNavigationManager.PushModalAsync` → `SendAppearing` |
| `IPageLifecycleAware.OnDisappearing` (dialog) | ✅ on pop | `PopModalAsync` → `SendDisappearing` |
| `IPageLifecycleAware.OnDisappearing` (page underneath) | ✅ on push | `previousPage?.SendDisappearing()` |
| `IPageLifecycleAware.OnAppearing` (page underneath) | ✅ on pop | `CurrentPage?.SendAppearing()` |
| `IDisposable.Dispose` (dialog ViewModel) | ✅ on pop | `RemoveLogicalChild` → `AppOnDescendantRemoved` |
| `INavigationAware.OnNavigatingFrom` | ❌ | by design — §6 |
| `INavigationConfirmation.CanNavigate` | ❌ | by design — §6 |
| `INavigator.Navigating` / `Navigated` | ❌ | by design — §6 |

`AppOnPageAppearing` sees `BindingContext` is already the right ViewModel type, so it skips the
rebind and goes straight to the hook. `RaiseNavigated` is a no-op because `pendingNavigation` is null.

**Disposal ordering.** `RemoveLogicalChild` runs *before* `Window.OnModalPopped`, so the ViewModel is
disposed slightly before `ShowDialog` returns. The result value was captured in the
`TaskCompletionSource` when the ViewModel raised its event, so the returned `DialogResult<T>` is
unaffected — but a caller must not keep using the ViewModel instance after the await. `ShowDialog`
deliberately does not dispose it itself; that would double-dispose.

## 6. Out of scope

- **`Navigating` / `Navigated` events are not raised.** A dialog is not a stack mutation and doesn't
  belong in a navigation log.
- **`INavigationAware` / `INavigationConfirmation` are not consulted.** Showing a dialog does not
  navigate away from the current page; a "are you sure you want to leave" guard firing on a dialog
  would be wrong.
- **`IQueryAttributable` args.** `configure` covers it, and there is no URI to carry a query string.
- **Popup presenters** for `Shiny.Maui.Controls` and `UXDivers.Popups`. The `IDialogPresenter` seam
  is the deliverable; the two implementations are a follow-up, and both need a way to host an
  arbitrary `Page` inside a popup host that needs designing against each library.
- **Nesting.** One dialog at a time is assumed. Nothing prevents stacked modals, but the presenter
  makes no attempt to coordinate them.

## 7. Alignment with the separation plan

`IDialogAware<T>`, `DialogResult<T>` and `IDialogPresenter` are all Shell-free — no `Shell`, no
routes, only `Page`. They move to `Shiny.Maui.Core` in phase 0 of the separation plan alongside
`IDialogs`. `ShowDialog` belongs on the **unified Core `INavigator`** (decision #1 of that plan), not
on `IShellNavigator`: `Shiny.Maui.Navigation` can implement it against its own modal stack with a
different `IDialogPresenter`, and the generator's flavor detection then emits `Show{Name}Dialog` for
both flavors from the same `ShellMapInfo` field.

## 8. Work

| Step | Change |
|---|---|
| 1 | `IDialogAware.cs`, `DialogResult.cs`, `IDialogPresenter.cs`; delete `IDialogResult.cs`; revert the `IDialogs` TODO |
| 2 | `ShinyAppBuilder.GetPageTypeForViewModel`, `UseDialogPresenter<T>` |
| 3 | `Infrastructure/ShellModalDialogPresenter.cs` |
| 4 | `INavigator.ShowDialog<TViewModel, T>` + `ShinyShellNavigator` implementation |
| 5 | `MauiAppBuilderExtensions` — `TryAddSingleton<IDialogPresenter, ShellModalDialogPresenter>` |
| 6 | Generator: `DialogResultTypeFullName` + `GenerateDialogExtensions` |
| 7 | Generator tests |
| 8 | Sample: `PickColorPage`/`PickColorViewModel` + a button on `DialogDemoPage` |
| 9 | `readme.md`, `skills/shiny-maui-shell` (SKILL.md + reference), docs site `dialogs.mdx` + release note |
