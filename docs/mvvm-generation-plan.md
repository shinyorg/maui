# ViewModel Source Generation — MVVM, Dirty Detection, Validation, Error Manager

One plan covering the whole ViewModel story for v7:

1. **MVVM generation** — bring `[ObservableProperty]` / `[RelayCommand]` in-house as a drop-in
   replacement for CommunityToolkit.Mvvm.
2. **Dirty detection + validation** — `[DirtyDetection]`, auto-implemented `INavigationConfirmation`,
   DataAnnotations validation with a bindable error surface.
3. **Global error manager** — an injectable handler chain that generated commands route failures through.

> **Supersedes** `docs/dirty-detection-plan.md` on the `70_trials` branch. That plan is merged in here
> (§2.3, §5, §6) with three of its premises revised — owning the property generator removes the workaround
> it was built around. Changes from the original are called out in **[was: …]** notes so the earlier
> reasoning survives. Delete the standalone file when this lands.

---

## Context

Every ViewModel in this repo — and in every app using Shiny MAUI Shell — depends on
`CommunityToolkit.Mvvm` for the boilerplate that makes a ViewModel a ViewModel:

```csharp
[ShellMap<DialogDemoPage>]
public partial class DialogDemoViewModel(IDialogs dialogs) : ObservableObject   // CTMvvm
{
    [ObservableProperty] string lastResult = "(none)";                          // CTMvvm

    [RelayCommand]                                                              // CTMvvm
    async Task ShowAlert() { ... }
}
```

That leaves the library with a hard third-party dependency on its most-used code path and — more
importantly — **a generator we don't control sitting between us and the ViewModel**. Four concrete costs:

1. **Dirty detection has to work around it.** The original plan's "Property discovery strategy" section
   exists solely because `[ObservableProperty]` fields produce properties from *another* generator that ours
   cannot see. It therefore snapshots the *field*, not the property, and can never hook the setter.
2. **`[ShellProperty]` and `[ObservableProperty]` don't compose.** `GetShellProperties`
   (`ShinyShellGenerator.cs:272`) walks only `PropertyDeclarationSyntax`, so a route parameter must be a
   hand-written property — it can never be an observable one.
3. **Async command exceptions are unowned.** `[RelayCommand] async Task Save()` invoked through
   `ICommand.Execute` is async-void at the binding boundary. A throw crashes the app and there is no
   framework seam to intercept it.
4. **Unsaved-changes guards are entirely manual.** `INavigationConfirmation.CanNavigate()` exists
   (`ShellNavigator.cs:335-360`), but wiring it up means hand-tracking a `hasUnsavedChanges` flag,
   implementing the interface, and building the dialog by hand (readme lifecycle example, lines 393-399).

## Decisions (confirmed)

### Scope & packaging

| # | Decision | Consequence |
|---|---|---|
| 1 | **Ships inside `Shiny.Maui.Shell`** (runtime types) and the existing `Shiny.Maui.Shell.SourceGenerators` assembly (generators). | No new package, no new packaging glue — the analyzer DLL is already packed to `analyzers/dotnet/cs` from the main csproj. A Roslyn analyzer assembly can host multiple `[Generator]` types. |
| 2 | **Drop-in replacement — same type and attribute names** as CTMvvm, in Shiny namespaces. | Migration = swap two global usings. The two packages **cannot** be referenced together (ambiguous names) — enforced by a diagnostic, not left to a link error. |
| 3 | **MVVM parity scope is properties + commands only.** | No `IMessenger`/`ObservableRecipient` (Shiny.Mediator owns pub/sub), no `ObservableGroupedCollection`, no `Ioc.Default`, no `ObservableValidator`. Validation is the dirty-detection model (§5), not a second one. |
| 4 | **The error manager covers generated command failures** in phase 1. | The contract carries an `ErrorSource` enum so navigation/lifecycle/dialog/validation reach can be wired later without a breaking change. |

### Dirty detection & validation (carried over from the original plan)

| # | Decision | Consequence |
|---|---|---|
| 5 | **2-way dialog — Save / Discard** (via `IDialogs.Confirm`). | Navigation proceeds on either choice **unless** Save is chosen and validation fails. |
| 6 | **`[SaveDirty]` required, `[CancelDirty]` optional.** | Enforced at compile time (SHINY201). |
| 7 | **Manual `MarkClean()` captures the baseline.** | Deterministic and async-load safe. Dirty state is queryable at three granularities: `IsDirty`, `IsPropertyDirty(...)`, `GetDirtyProperties()`. |
| 8 | **Overridable `virtual` members.** `CanNavigate()` is `public virtual` (implicit interface implementation); dialog chrome comes from `protected virtual GetDirtyText()` returning a `DirtyText` record. | Consumers override for localization; `null` members fall back to defaults. |
| 9 | **Validation blocks save silently; errors surface via bindings.** | Invalid → `CanNavigate()` returns `false`, nothing saved, **no dialog**. `Validate()` refreshes bindable `Errors`/`IsValid`/`HasErrors` so the page shows them inline. Discard always navigates. |
| 10 | **Validation messages belong to the annotations, not to `DirtyText`.** | `[StringLength(8, MinimumLength = 3, ErrorMessage = "This code requires {2}+ characters")]` → `GetValidationResult` fills the placeholders. Localize at the attribute or in `OnValidate` — never in `GetDirtyText`. |

**AOT is a hard requirement throughout.** No reflection anywhere: property comparison is emitted per-member
using `EqualityComparer<T>.Default`, validation is emitted as explicit per-attribute `new` expressions, and
route/command dispatch is switch-based — mirroring the existing generator's fully-qualified, reflection-free
output style.

---

## 1. Namespaces and the migration surface

CTMvvm splits its surface across two namespaces and app code reaches them through global usings
(`Sample/GlobalUsings.cs`). Mirroring that split makes migration mechanical:

| CommunityToolkit.Mvvm | Shiny |
| --- | --- |
| `CommunityToolkit.Mvvm.ComponentModel` | `Shiny.ComponentModel` |
| `CommunityToolkit.Mvvm.Input` | `Shiny.Input` |

```diff
- global using CommunityToolkit.Mvvm.ComponentModel;
- global using CommunityToolkit.Mvvm.Input;
+ global using Shiny.ComponentModel;
+ global using Shiny.Input;
```

Everything else in a consuming ViewModel — `: ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`,
`SetProperty(...)`, `OnPropertyChanged(nameof(X))`, the `SaveCommand` naming convention, the `OnXChanged`
partial hooks — compiles unchanged. That is the acceptance bar for "drop-in": **the Sample app builds after
deleting the `CommunityToolkit.Mvvm` `PackageReference` and editing those two lines, with no other source edit.**

Dirty-detection and error types (`[DirtyDetection]`, `IErrorHandler`, …) live in the root `Shiny` namespace
alongside `INavigator` / `IDialogs` / `ShellMapAttribute`, because they are Shell-framework concepts, not
CTMvvm replacements.

> MVVM types are deliberately *not* in root `Shiny`: that would push `ObservableObject`/`RelayCommand` onto
> every Shell consumer whether they want them or not, and would collide loudly for anyone mid-migration who
> still has CTMvvm referenced in the same file.

## 2. Runtime types

All AOT/trim clean: no reflection, no `MakeGenericMethod`, no `Type.GetType`.

### 2.1 `Shiny.ComponentModel` — `src/Shiny.Maui.Shell/ComponentModel/`

- **`ObservableObject`** — implements `INotifyPropertyChanged` **and** `INotifyPropertyChanging`. Members
  required for drop-in:
  - `event PropertyChangedEventHandler? PropertyChanged` / `PropertyChangingEventHandler? PropertyChanging`
  - `protected void OnPropertyChanged(string? propertyName)` + `(PropertyChangedEventArgs e)` overload
  - `protected void OnPropertyChanging(string? propertyName)` + args overload
  - `protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)`
  - `protected bool SetProperty<T>(ref T field, T newValue, IEqualityComparer<T> comparer, [CallerMemberName] string? = null)`
  - `protected bool SetProperty<TModel, T>(T oldValue, T newValue, TModel model, Action<TModel, T> callback, [CallerMemberName] string? = null)`
  - Cached `PropertyChangedEventArgs`/`PropertyChangingEventArgs` per emitted name, so a property set inside a
    list doesn't allocate two event-args objects per set.
  - *Not* ported: `SetPropertyAndNotifyOnCompletion` / `TaskNotifier` (see SHINY108).
- **`ObservableObjectAttribute`** (`[ObservableObject]` on a class) — for ViewModels that must inherit a
  different base; the generator injects the INPC/INPChanging implementation into the partial class.
  `[INotifyPropertyChanged]` is the CTMvvm alias for the notification-only subset — supported as an alias.
- **`ObservablePropertyAttribute`** — `AttributeTargets.Field | AttributeTargets.Property`.
- **`NotifyPropertyChangedForAttribute(params string[] propertyNames)`**
- **`NotifyCanExecuteChangedForAttribute(params string[] commandNames)`**

### 2.2 `Shiny.Input` — `src/Shiny.Maui.Shell/Input/`

- **`IRelayCommand : ICommand`** (`void NotifyCanExecuteChanged()`), **`IRelayCommand<in T>`**
- **`IAsyncRelayCommand : IRelayCommand`** — `Task? ExecutionTask`, `bool IsRunning`, `bool CanBeCanceled`,
  `bool IsCancellationRequested`, `void Cancel()`, `Task ExecuteAsync(object?)`
- **`IAsyncRelayCommand<in T>`**
- **`RelayCommand` / `RelayCommand<T>`** — sync, `CanExecute` delegate, `NotifyCanExecuteChanged()`
- **`AsyncRelayCommand` / `AsyncRelayCommand<T>`** — overloads for `Func<Task>`, `Func<CancellationToken, Task>`,
  `Func<T, Task>`, `Func<T, CancellationToken, Task>`; concurrency guard (`IsRunning` blocks re-entry unless
  `AllowConcurrentExecutions`); `Cancel()` backed by a `CancellationTokenSource`; raises `PropertyChanged` for
  `IsRunning`/`ExecutionTask`/`CanBeCanceled`.
- **`AsyncRelayCommandOptions`** — `None`, `AllowConcurrentExecutions`, `FlowExceptionsToTaskScheduler` (flags,
  CTMvvm-compatible names and semantics).
- **`RelayCommandAttribute`** — `string? CanExecute`, `bool AllowConcurrentExecutions` (default `false`),
  `bool IncludeCancelCommand`, `bool FlowExceptionsToTaskScheduler` (default `false`).

**Deliberate divergence from CTMvvm:** `AsyncRelayCommand` routes a thrown exception to the error manager
(§6) instead of leaving the task faulted and unobserved. `FlowExceptionsToTaskScheduler = true` restores the
CTMvvm behavior verbatim.

### 2.3 `Shiny` (root) — dirty detection, validation, errors

- **`DirtyDetectionAttribute`** — `[AttributeUsage(AttributeTargets.Class)]`, properties `string? Title`,
  `string? Message` (dialog chrome; English defaults when null).
- **`SaveDirtyAttribute`** — `[AttributeUsage(AttributeTargets.Method)]`, ctor `(string? buttonText = null)`.
- **`CancelDirtyAttribute`** — same shape.
- **`DirtyIgnoreAttribute`** — `[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]`, excludes
  UI-state members (e.g. `SelectedTab`) from dirty tracking.
- **`DirtyText`** — `sealed record DirtyText(string? Title = null, string? Message = null, string? SaveText = null,
  string? CancelText = null)`. Returned by the overridable `GetDirtyText()`; `null` members default at dialog time.
- **`ValidationError`** — `sealed record ValidationError(string? MemberName, string Message)`.
- **`IErrorHandler` / `ErrorContext` / `ErrorSource`** — §6.

Validation uses **`System.ComponentModel.DataAnnotations` attributes directly** (`[Required]`, `[Range]`,
`[StringLength]`, `[EmailAddress]`, …) — no bespoke attribute set, and no new dependency (the package is part
of the framework).

## 3. Shared generator model (the linchpin)

Three generators now write partial members into the *same* class. They must agree on one model, built once
per type — this is what makes the merge worth doing rather than running two independent plans:

```
ViewModelModel
├─ namespace, class name, type parameters, partial?, base chain (has ObservableObject?)
├─ resolved members: IDialogs member name, IErrorHandler member name (if any)
├─ PropertyModel[]        // one per trackable member, whatever its syntactic form
│    ├─ SourceKind: DeclaredProperty | ObservableField | ObservablePartialProperty
│    ├─ PropertyName ("LastResult"), BackingName ("lastResult"), fully-qualified type
│    ├─ ForwardedAttributes ([property:] / [field:] payloads — ShellProperty, DataAnnotations, …)
│    ├─ NotifyPropertyChangedFor[], NotifyCanExecuteChangedFor[]
│    ├─ DirtyTracked (false when [DirtyIgnore]), ValidationAttributes[]
│    └─ IsEnum + enum values (feeds the existing AI/route metadata path)
└─ CommandModel[]         // name, signature shape, CanExecute target, options
```

`ShinyShellGenerator` consumes the same `PropertyModel[]` for `[ShellProperty]` discovery (§7), which is what
finally lets a route parameter also be an observable property.

## 4. Generator — `ObservablePropertyGenerator.cs`

Same pipeline shape as `ShinyShellGenerator.Initialize`: `CreateSyntaxProvider` over member declarations,
semantic-model match on the attribute type, `Collect()` per containing type, one
`<Class>.Observable.g.cs` per class.

Both input forms are supported — the field form because drop-in demands it, the partial-property form because
new code should be able to use it (C# 13+/.NET 9+; repo is net10 with `LangVersion latest`):

```csharp
[ObservableProperty] string lastResult = "(none)";                  // -> public string LastResult { get; set; }
[ObservableProperty] public partial string LastResult { get; set; }
```

Name derivation follows CTMvvm exactly (`lastResult` → `LastResult`, `_lastResult` → `LastResult`,
`m_lastResult` → `LastResult`); anything else is SHINY102.

```csharp
public string LastResult
{
    get => this.lastResult;
    set
    {
        if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(this.lastResult, value))
        {
            var __old = this.lastResult;
            this.OnLastResultChanging(value);
            this.OnLastResultChanging(__old, value);
            this.OnPropertyChanging(__ChangingArgs.LastResult);
            this.lastResult = value;
            this.OnPropertyChanged(__ChangedArgs.LastResult);
            this.OnLastResultChanged(value);
            this.OnLastResultChanged(__old, value);
            this.OnPropertyChanged(__ChangedArgs.FullName);   // [NotifyPropertyChangedFor]
            this.SaveCommand.NotifyCanExecuteChanged();       // [NotifyCanExecuteChangedFor]
            this.__OnTrackedMemberChanged("LastResult");      // emitted only on [DirtyDetection] classes — §5.2
        }
    }
}

partial void OnLastResultChanging(string value);
partial void OnLastResultChanging(string oldValue, string newValue);
partial void OnLastResultChanged(string value);
partial void OnLastResultChanged(string oldValue, string newValue);
```

**Attribute forwarding** (`[property:]`, `[field:]`) is required — it is how a consumer puts `[ShellProperty]`
or a DataAnnotations attribute on a generated property, and it is the hinge for both §5 and §7:

```csharp
[ObservableProperty]
[property: ShellProperty("The work order id", required: true)]
[property: Required]
string workOrderId = "";
```

## 5. Generator — `DirtyDetectionGenerator.cs`

Emits `<Class>.Dirty.g.cs` for classes carrying `[DirtyDetection]`. **[was: a standalone plan that had to
enumerate `[ObservableProperty]` *fields* separately because the generated properties were invisible.]** It now
reads `PropertyModel[]` from §3, so tracked members are named by their **public property name** — which is what
`GetDirtyProperties()` should hand a binding anyway.

`IDialogs` is resolved by scanning the class for an accessible member of type `Shiny.IDialogs` —
primary-constructor parameter (from `ClassDeclarationSyntax.ParameterList`), then field, then property.
Primary-constructor parameters are in scope across all partial files, so this is legal. SHINY202 if none found.

### 5.1 Generated shape

```csharp
#nullable enable
namespace <ns>;

partial class <Class> : global::Shiny.INavigationConfirmation
{
    private <Type> __snapshot_<Name>;          // one per tracked member
    private bool __hasSnapshot;
    private bool __wasDirty;                   // for transition-only IsDirty notification

    public void MarkClean()
    {
        this.__snapshot_<Name> = this.<Name>;  // repeated per member
        this.__hasSnapshot = true;
        this.__RefreshDirty();
    }

    public bool IsDirty
    {
        get
        {
            if (!this.__hasSnapshot) return false;
            return !global::System.Collections.Generic.EqualityComparer<<Type>>.Default
                       .Equals(this.__snapshot_<Name>, this.<Name>) || ...;   // OR-chained per member
        }
    }

    public bool IsPropertyDirty(string propertyName)   // switch dispatch — no reflection
    {
        if (!this.__hasSnapshot) return false;
        switch (propertyName)
        {
            case "<Name>":
                return !global::System.Collections.Generic.EqualityComparer<<Type>>.Default
                            .Equals(this.__snapshot_<Name>, this.<Name>);
            default: return false;
        }
    }

    // Strongly-typed overload — reads the member name off the expression tree (no .Compile()).
    public bool IsPropertyDirty(
        global::System.Linq.Expressions.Expression<global::System.Func<<Class>, object?>> property)
    {
        var __body = property.Body;
        if (__body is global::System.Linq.Expressions.UnaryExpression __u) __body = __u.Operand;  // unwrap boxing Convert
        var __name = (__body as global::System.Linq.Expressions.MemberExpression)?.Member.Name;
        return __name is not null && this.IsPropertyDirty(__name);
    }

    public string[] GetDirtyProperties() { /* one check per tracked member, returns property names */ }

    // ---- validation ----
    private global::System.Collections.Generic.IReadOnlyList<global::Shiny.ValidationError> __errors
        = global::System.Array.Empty<global::Shiny.ValidationError>();

    public global::System.Collections.Generic.IReadOnlyList<global::Shiny.ValidationError> Errors => this.__errors;
    public bool IsValid => this.__errors.Count == 0;
    public bool HasErrors => this.__errors.Count > 0;

    public global::System.Collections.Generic.IEnumerable<global::Shiny.ValidationError> GetErrors(string? memberName)
    {
        foreach (var __e in this.__errors)
            if (__e.MemberName == memberName) yield return __e;
    }

    public global::System.Collections.Generic.IReadOnlyList<global::Shiny.ValidationError> Validate()
    {
        var __list = new global::System.Collections.Generic.List<global::Shiny.ValidationError>();

        // per validated member, per DataAnnotations attribute — rendered from AttributeData TypedConstants
        {
            var __ctx = new global::System.ComponentModel.DataAnnotations.ValidationContext(this)
                { MemberName = "<Member>" };
            var __a = new global::System.ComponentModel.DataAnnotations.RequiredAttribute();
            var __r = __a.GetValidationResult(this.<Member>, __ctx);
            if (__r != global::System.ComponentModel.DataAnnotations.ValidationResult.Success)
                __list.Add(new global::Shiny.ValidationError("<Member>", __r!.ErrorMessage ?? "Invalid"));
        }

        this.OnValidate(__list);                       // cross-field / custom checks
        this.__errors = __list;
        this.OnPropertyChanged(nameof(this.Errors));   // always available — base is Shiny's ObservableObject
        this.OnPropertyChanged(nameof(this.IsValid));
        this.OnPropertyChanged(nameof(this.HasErrors));
        return this.__errors;
    }

    protected virtual void OnValidate(
        global::System.Collections.Generic.IList<global::Shiny.ValidationError> errors) { }

    // Dialog chrome only — never validation messages (decision #10).
    protected virtual global::Shiny.DirtyText GetDirtyText()
        => new global::Shiny.DirtyText(
            Title:      "<[DirtyDetection] Title or 'Unsaved Changes'>",
            Message:    "<[DirtyDetection] Message or 'You have unsaved changes. Save them?'>",
            SaveText:   "<[SaveDirty] text or 'Save'>",
            CancelText: "<[CancelDirty] text or 'Discard'>");

    public virtual async global::System.Threading.Tasks.Task<bool> CanNavigate()
    {
        if (!this.IsDirty) return true;

        var __text = this.GetDirtyText();
        var __save = await this.<dialogsMember>.Confirm(
            __text.Title      ?? "Unsaved Changes",
            __text.Message    ?? "You have unsaved changes. Save them?",
            __text.SaveText   ?? "Save",
            __text.CancelText ?? "Discard");

        if (__save)
        {
            this.Validate();          // refreshes + notifies Errors/IsValid so the page can show them
            if (this.HasErrors)
                return false;         // block save + navigation, stay on page, no dialog

            await <SaveMethod>();     // awaited only for Task/ValueTask; routed through the error manager (§6.1)
            this.MarkClean();
        }
        else
        {
            <CancelMethod>();         // only emitted when [CancelDirty] is present
        }
        return true;
    }
}
```

Notes:
- `EqualityComparer<T>.Default` for every member — AOT-safe and uniform across value types, strings,
  nullables and reference types.
- `CanNavigate()` is `public virtual` (implicit interface implementation) so the whole flow is overridable;
  explicit interface implementations can't be virtual.
- **Localization = override `GetDirtyText()`.** The base bakes in the attribute literals; an override may
  return any subset, with `??` fallbacks applied at the dialog. Compiler-checked, unlike a naming convention.
- Validation members are enumerated **independently of dirty tracking** — a `[DirtyIgnore]` member can still be
  validated, and a validated member need not be dirty-tracked. `Validate()`/`Errors`/`IsValid`/`OnValidate` are
  always emitted (even with zero attributes) so manual-only validation works.

### 5.2 Setter-driven dirty state **[new — enabled by the merge]**

Because the setter is now ours, `ObservablePropertyGenerator` emits `__OnTrackedMemberChanged(name)` on
`[DirtyDetection]` classes (§4). It recomputes and raises `PropertyChanged(nameof(IsDirty))`
**only on transition** (clean→dirty, dirty→clean) via the `__wasDirty` field — so `IsDirty` becomes bindable
(enable/disable a Save button) without polling, and without a notification per keystroke.

The snapshot mechanism stays as the source of truth: `MarkClean()` captures, `IsDirty` compares. The setter
hook is a notification trigger, not a second state machine.

### 5.3 Live validation — opt-in

`[DirtyDetection(ValidateOnChange = true)]` extends the same hook to re-run `Validate()` after each tracked
set, giving as-you-type errors. Default stays **on-demand** (Save, or an explicit `Validate()` call), matching
the original plan.

### 5.4 AOT caveat (unchanged)

A handful of built-in DataAnnotations attributes reflect *inside* their own `IsValid` — notably `[Compare]`
(looks up another property by name) and custom attributes resolving services from the `ValidationContext`.
Those are **not** guaranteed trim/AOT-safe and are documented as such. The common value/format attributes
(`Required`, `Range`, `StringLength`, `MinLength`/`MaxLength`, `EmailAddress`, `RegularExpression`, `Phone`,
`Url`) are reflection-free.

## 6. Generator — `RelayCommandGenerator.cs`

Emits `<Class>.Commands.g.cs`. Method → command mapping, CTMvvm-compatible:

| Method signature | Generated |
| --- | --- |
| `void Foo()` | `IRelayCommand FooCommand` → `RelayCommand` |
| `void Foo(T arg)` | `IRelayCommand<T> FooCommand` |
| `Task Foo()` / `Task<T> Foo()` | `IAsyncRelayCommand FooCommand` → `AsyncRelayCommand` |
| `Task Foo(T arg)` | `IAsyncRelayCommand<T> FooCommand` |
| `Task Foo(CancellationToken ct)` | `IAsyncRelayCommand` + optional `FooCancelCommand` |
| `Task Foo(T arg, CancellationToken ct)` | `IAsyncRelayCommand<T>` + optional cancel command |

Naming: `Foo` → `FooCommand`; a trailing `Async` is stripped (`SaveAsync` → `SaveCommand`). The backing field
is lazily initialised (`??=`) in the getter so construction doesn't run in a field initialiser — this matters
because these ViewModels use primary constructors, whose parameters aren't assigned until afterwards.

```csharp
private global::Shiny.Input.AsyncRelayCommand? saveCommand;

public global::Shiny.Input.IAsyncRelayCommand SaveCommand => this.saveCommand ??=
    new global::Shiny.Input.AsyncRelayCommand(
        this.Save,
        () => this.CanSave,                                  // [RelayCommand(CanExecute = nameof(CanSave))]
        global::Shiny.Input.AsyncRelayCommandOptions.None,
        errorContext: new global::Shiny.ErrorContext(
            global::Shiny.ErrorSource.Command, this, "SaveCommand"));
```

`CanExecute` resolves to a `bool` property or a `bool`-returning method (parameterless, or taking the
command's `T`) — anything else is SHINY104.

## 7. Global error manager — `src/Shiny.Maui.Shell/IErrorHandler.cs`

Contract shape follows `Shiny.Mediator.IExceptionHandler` deliberately — same ordered-chain,
first-`true`-wins semantics, so the two libraries read the same way:

```csharp
namespace Shiny;

public interface IErrorHandler
{
    /// <summary>Return true if handled (stops the chain); false to pass to the next handler.</summary>
    Task<bool> Handle(ErrorContext context, Exception exception);
}

public enum ErrorSource { Command, Navigation, Lifecycle, Dialog, Validation, Unhandled }

public sealed record ErrorContext(
    ErrorSource Source,
    object? Instance,          // the ViewModel
    string? MemberName,        // e.g. "SaveCommand"
    object? Parameter = null   // the command parameter, when there is one
);
```

**Registration** — one line on the existing builder, matching `UseDialogs<T>()`:

```csharp
builder.UseShinyShell(x => x
    .AddGeneratedMaps()
    .AddErrorHandler<MyErrorHandler>()   // singleton IErrorHandler; multiple allowed, run in registration order
);
```

**Resolution without a service locator in user code.** Generated commands are constructed inside the ViewModel
and have no `IServiceProvider`. First match wins:

1. An `IErrorHandler` member on the ViewModel (primary-ctor parameter, field, or property) — discovered the
   same way `IDialogs` is (§5). Per-ViewModel override, zero ambient state.
2. The ambient chain: a static `ShinyErrorHandling` holder that `ShinyShellNavigator.Initialize` (already an
   `IMauiInitializeService` receiving `IServiceProvider`, `ShellNavigator.cs:25`) primes with a lazy
   `IEnumerable<IErrorHandler>` resolver. No DI call happens until an exception actually occurs.
3. Nothing registered → the fallback.

**Fallback when no handler returns `true`** (recommended default, switchable via
`ShinyMauiShell_CommandErrorBehavior`): log through `ILogger` and **swallow** for `ErrorSource.Command`. That is
the whole point — an unhandled throw in an `ICommand.Execute` path is an app crash today.
`FlowExceptionsToTaskScheduler = true` on an individual command opts back into CTMvvm semantics.

### 7.1 Reach

**Phase 1 wires commands only** (decision #4). Two adjacent gaps are declared now and wired in the same phase
if they prove cheap, otherwise deferred:

- **The `[SaveDirty]` invocation inside `CanNavigate()`** (§5.1) — a throw there propagates into Shell's
  navigating deferral, which is an app crash on the same footing as an async-void command. Recommended:
  route it as `ErrorSource.Validation` and treat "handled" as "block navigation".
- **The three `catch (Exception ex) { logger.LogError(...) }` sites already in `ShellNavigator.cs`**
  (lines 72, 95, 192) — currently invisible to the app. `ErrorSource.Navigation`.

## 8. Composition — where the three generators meet

- **`[ShellProperty]` on observable properties.** `GetShellProperties` (`ShinyShellGenerator.cs:272`) walks only
  `PropertyDeclarationSyntax`, so today a route parameter cannot be an `[ObservableProperty]`. It must move to
  the shared `PropertyModel[]` (§3) and accept `[ObservableProperty]` + `[property: ShellProperty]`, resolving
  the generated property name. Without this, the two halves of the library still don't compose.
- **Emission ownership.** Three files augment one class — `.Observable.g.cs`, `.Commands.g.cs`, `.Dirty.g.cs`.
  Exactly one of them declares `INavigationConfirmation`, one owns the INPC members (base class, or
  `[ObservableObject]` injection), and none may re-declare another's backing fields. One `ViewModelModel` built
  once per type, three emitters reading it.
- **SHINY009 from the original plan is deleted.** It warned that `Errors`/`IsValid` wouldn't notify when no
  accessible `OnPropertyChanged` existed — a consequence of the base class being CTMvvm's and possibly absent.
  With `Shiny.ComponentModel.ObservableObject` as the framework's own base, SHINY107 covers the same ground at
  the property level and the validation surface can assume notification. **[was: SHINY009, info]**

## 9. Diagnostics

`SHINY001`–`SHINY004` are taken by the shell generator (`001` invalid route, `002` nav extensions disabled,
`003` AI package missing, `004` AI descriptions). **The original dirty-detection plan claimed `SHINY004`–`009`,
which collides with `004`** — renumbered here into a range scheme:

| Range | Owner |
| --- | --- |
| `SHINY001`–`SHINY099` | shell / routes / AI (existing) |
| `SHINY100`–`SHINY199` | MVVM |
| `SHINY200`–`SHINY299` | dirty detection + validation |
| `SHINY300`–`SHINY399` | error manager |

**MVVM**
- **SHINY100** (error) — type with `[ObservableProperty]`/`[RelayCommand]` is not `partial`.
- **SHINY101** (error) — `CommunityToolkit.Mvvm` referenced alongside Shiny MVVM. Detected via
  `compilation.GetTypeByMetadataName("CommunityToolkit.Mvvm.ComponentModel.ObservableObject")`, exactly as the AI
  package check works today (`ShinyShellGenerator.cs:80`). Message names the two global usings to swap.
  Escapable with `ShinyMauiShell_GenerateMvvm=false`.
- **SHINY102** (error) — `[ObservableProperty]` field name yields no valid property name, or collides.
- **SHINY103** (error) — `[RelayCommand]` on an unsupported signature (2+ non-token parameters, `ref`/`out`, `async void`).
- **SHINY104** (error) — `CanExecute` target missing or wrong shape.
- **SHINY105** (error) — `IncludeCancelCommand` on a method with no `CancellationToken`.
- **SHINY106** (warning) — `[NotifyPropertyChangedFor]`/`[NotifyCanExecuteChangedFor]` names an unknown member.
- **SHINY107** (warning) — containing type has neither `ObservableObject` in its base chain nor `[ObservableObject]`.
- **SHINY108** (info) — unsupported CTMvvm surface (`[NotifyDataErrorInfo]`, `[NotifyPropertyChangedRecipients]`,
  `ObservableRecipient`, `ObservableValidator`, `TaskNotifier`), pointing at the Shiny alternative
  (validation → `[DirtyDetection]`/`Validate()`; messaging → Shiny.Mediator).

**Dirty detection + validation** *(renumbered from the original 004–009)*
- **SHINY200** (error) — `[DirtyDetection]` class is not `partial`. **[was: SHINY004]**
- **SHINY201** (error) — no `[SaveDirty]` method found. **[was: SHINY005]**
- **SHINY202** (error) — no accessible `IDialogs` member. **[was: SHINY006]**
- **SHINY203** (error) — `[SaveDirty]`/`[CancelDirty]` on an invalid method (must be parameterless, returning
  `void`/`Task`/`ValueTask`), or more than one of either. **[was: SHINY007]**
- **SHINY204** (warning) — `[DirtyDetection]` class has zero trackable members. **[was: SHINY008]**

**Error manager**
- **SHINY300** (error) — the `IErrorHandler` member named on a ViewModel is inaccessible or the wrong type.

**MSBuild toggles** — add as `CompilerVisibleProperty` to
`src/Shiny.Maui.Shell.SourceGenerators/build/Package.targets` beside the existing seven:
`ShinyMauiShell_GenerateMvvm` (default true), `ShinyMauiShell_GenerateDirtyDetection` (default true),
`ShinyMauiShell_CommandErrorBehavior` (`Handle` | `Throw`, default `Handle`).

## 10. Build order

| Phase | Work | Gate |
| --- | --- | --- |
| **0** | Shared `ViewModelModel`/`PropertyModel` extraction (§3), diagnostic renumbering (§9), `Package.targets` toggles. | Existing shell generator tests still green. |
| **1** | Runtime types: `ObservableObject`, command types, MVVM attributes, dirty/validation records, `IErrorHandler` contract. No generators yet. | A hand-written ViewModel using `SetProperty` + `new AsyncRelayCommand(...)` binds and executes in the Sample. |
| **2** | `ObservablePropertyGenerator` — fields, partial properties, `Notify*For`, partial hooks, attribute forwarding. | Sample ViewModels compile with CTMvvm removed from `Sample.csproj`. |
| **3** | `RelayCommandGenerator` — all six signature shapes, `CanExecute`, cancel command, concurrency. | Every Sample command still fires. |
| **4** | Error manager: `AddErrorHandler<T>`, ambient holder primed from `ShinyShellNavigator.Initialize`, command wiring, behavior switch. | `[RelayCommand] async Task Boom() => throw new(...)` reaches a registered handler instead of crashing. |
| **5** | `DirtyDetectionGenerator` — dirty surface, DataAnnotations validation, `CanNavigate()`, setter-driven `IsDirty` (§5.2). | Edit a field, press Back → Save/Discard dialog; invalid Save blocks silently with `Errors` populated. |
| **6** | Composition: `[property: ShellProperty]` in the shell generator, shared-model cutover (§8). | A route parameter that is also an observable property navigates and binds. |
| **7** | Sample migration (drop `CommunityToolkit.Mvvm`, swap `GlobalUsings.cs`), readme, skill, docs site, release notes. | §12. |

Phases 2 and 3 are independent and both depend on 0–1. Phase 4 depends on 3. Phase 5 depends on 2 (shared model
+ setter hook) — **this is the ordering change the merge buys: dirty detection stops being standalone and lands
after the property generator, so it never needs the field-snapshot workaround.** Phase 6 depends on 2.

## 11. Testing

- **Generator snapshots** — `ObservablePropertyGeneratorTests.cs`, `RelayCommandGeneratorTests.cs`,
  `DirtyDetectionGeneratorTests.cs` in `tests/Shiny.Maui.Shell.Tests/`, using Verify.SourceGenerators, mirroring
  `ShinyShellGeneratorTests.cs` and reusing `MockAnalyzerConfigOptionsProvider`. Cover:
  - *MVVM* — field vs partial-property form; name derivation (`_x`, `m_x`, `x`); attribute forwarding; both
    `Notify*For` attributes; all six command signatures; `CanExecute` as property and as method; cancel command;
    concurrency default.
  - *Dirty* — declared properties and `[ObservableProperty]` members; `[DirtyIgnore]`; `IsPropertyDirty` (string
    and expression overloads, including the boxed value-type `Convert` unwrap); `GetDirtyProperties()`; sync vs
    async `[SaveDirty]`; missing `[CancelDirty]`; `IDialogs` via primary ctor vs field; with and without a
    `GetDirtyText()` override; DataAnnotations on declared properties *and* via `[property:]` forwarding
    (`Required`/`Range`/`StringLength` with ctor + named args, incl. an `ErrorMessage` with `{2}` placeholders →
    "requires 3+ characters"); an `OnValidate` override; `Validate()` populating and notifying
    `Errors`/`IsValid`/`HasErrors`; transition-only `IsDirty` notification (§5.2).
  - *Diagnostics* — SHINY100–108, 200–204, 300.
- **Runtime unit tests** — `ObservableObject` (notification order changing→set→changed, comparer overloads,
  no-notify on equal value) and the command types (`IsRunning` gating, `Cancel()`, `NotifyCanExecuteChanged`,
  error routing to a fake `IErrorHandler`, `FlowExceptionsToTaskScheduler`).
  **Build note:** the test project targets plain `net10.0` and currently references only the generator project.
  The MVVM runtime types touch no MAUI API but live in a `Microsoft.Maui.Controls`-referencing assembly — if
  referencing `Shiny.Maui.Shell` from `net10.0` proves awkward, link the sources in via
  `<Compile Include="..\..\src\Shiny.Maui.Shell\ComponentModel\*.cs" />` rather than splitting out a package
  (decision #1 says no new package).
- **Sample** — an `ErrorDemoViewModel`/page (throwing command, cancellable long-running command bound to
  `IsRunning`/`CancelCommand`, a registered `IErrorHandler` surfacing failures through `IDialogs`) and a
  `DirtyFormViewModel`/page (`MarkClean()` after load, `[SaveDirty]`/`[CancelDirty]`, a `GetDirtyText()`
  override, DataAnnotations via `[property:]`, an `OnValidate` cross-field check, an error summary bound to
  `Errors`, per-field labels via `GetErrors(...)`, Save enabled off `IsValid`).
- **AOT** — the Sample already sets `IsAotCompatible` and `PublishAot` for ios/maccatalyst; a Release publish
  must stay warning-free.

### End-to-end verification

1. `dotnet build src/Shiny.Maui.Shell.SourceGenerators`, then `dotnet build src/Shiny.Maui.Shell` — generators
   compile and pack.
2. `dotnet test tests/Shiny.Maui.Shell.Tests` — snapshot + diagnostic + runtime tests pass.
3. Build `Sample`; inspect the generated `*.Observable.g.cs` / `*.Commands.g.cs` / `*.Dirty.g.cs` in `obj/`.
4. Run the Sample (`/run`): edit a dirty-page field → Back shows Save/Discard; Save persists then navigates;
   an invalid Save stays put with inline errors; Discard navigates without persisting; an unchanged page
   navigates silently. Throw from a command → the registered handler runs, the app survives.

## 12. Docs & required updates (per CLAUDE.md — not "done" without these)

1. **`readme.md`** — new "MVVM" section (the two global usings, supported attribute surface, CTMvvm
   parity/divergence table, migration note that the packages are mutually exclusive), a "Dirty Detection &
   Validation" section (attributes, `MarkClean()`, `IsDirty`/`IsPropertyDirty`/`GetDirtyProperties`, overriding
   `GetDirtyText()`, DataAnnotations + `OnValidate`, the bindable `Errors`/`IsValid` surface and the silent
   validation-block behavior), and `AddErrorHandler<T>`.
2. **Skill** (`skills/shiny-maui-shell/`) — trigger keywords: `ObservableProperty`, `RelayCommand`,
   `ObservableObject`, `NotifyPropertyChangedFor`, `NotifyCanExecuteChangedFor`, `AsyncRelayCommand`,
   `IRelayCommand`, `DirtyDetection`, `SaveDirty`, `CancelDirty`, `DirtyIgnore`, `MarkClean`, `IsDirty`,
   `IsPropertyDirty`, `GetDirtyProperties`, `Validate`, `OnValidate`, `ValidationError`, `Errors`, `HasErrors`,
   `unsaved changes`, `viewmodel validation`, `data annotations`, `IErrorHandler`, `AddErrorHandler`,
   `ErrorContext`, `CommunityToolkit.Mvvm migration`. `reference/templates.md` must stop emitting CTMvvm usings
   and its hand-rolled `isDirty` example (lines ~441-478).
3. **Docs site** (`~/Desktop/dev/documentation`) — new `mvvm.mdx`, `dirty-detection.mdx` and
   `error-handling.mdx` under the MAUI Shell node in `src/sidebar-topics.mjs`; `sourcegen.mdx` updated with the
   new generated files and the SHINY1xx/2xx/3xx diagnostics; `<RN type="feature">` release notes under the
   unreleased heading with a `### Migration from v6` block (this is breaking for anyone referencing CTMvvm).

## 13. Open questions

1. **`[ObservableObject]` class attribute — phase 2 or later?** Needed by anyone whose ViewModel already
   inherits a base class. Cheap once the setter emitter exists (~40 lines of INPC members), but a second entry
   path into the generator. *Recommendation: phase 2 — it's the escape hatch that makes "drop-in" true for
   non-trivial apps.*
2. **Main-thread marshalling of notifications.** CTMvvm raises `PropertyChanged`/`CanExecuteChanged` on whatever
   thread set the property — a background set against a bound `CollectionView` is a real crash source on
   Android. We own the base class and already have `IMainThread`, so an opt-in
   `ShinyMauiShell_MarshalNotifications` (or a static `ObservableObject.MarshalNotifications`) is available to us
   and is a genuine improvement. It is also a behavior divergence and ambient state in an otherwise pure type.
   *Recommendation: build the hook, default it off, document it.*
3. **`ErrorContext.Instance` typing.** `object?` keeps the record free of generics and matches
   `Shiny.Mediator.IExceptionHandler(IMediatorContext, Exception)`; handlers pattern-match to get the ViewModel.
   Acceptable?
4. **Do sync `RelayCommand` failures route to the error manager too?** A sync throw propagates straight out of
   `ICommand.Execute` into the platform — catching it is equally valuable but changes the semantics of a plainly
   synchronous call. *Recommendation: yes, same chain, same `Handle`/`Throw` switch.*
5. **Multiple `AddErrorHandler<T>` registrations** — the mediator precedent says yes (ordered chain, first
   `true` wins). Confirm ordering is registration order and document it.
6. **Should `[SaveDirty]` failures block navigation?** If the save throws and a handler returns `true`
   ("handled"), the user has been told — but `MarkClean()` hasn't run and the data isn't saved. *Recommendation:
   a handled save failure returns `false` from `CanNavigate()` (stay on the page); an unhandled one follows the
   `CommandErrorBehavior` switch.*

## 14. Out of scope / known limitations

- `IMessenger` / `WeakReferenceMessenger` / `ObservableRecipient` / `[NotifyPropertyChangedRecipients]` —
  Shiny.Mediator owns in-app messaging.
- `ObservableGroupedCollection`, `ReadOnlyObservableGroupedCollection`, `Ioc.Default`.
- `ObservableValidator` / `INotifyDataErrorInfo` — §5 is the validation model; there will not be two. No
  automatic `{Binding Field}`-level error adornment: apps bind `Errors`/`IsValid`/`GetErrors(member)`.
- `SetPropertyAndNotifyOnCompletion` / `TaskNotifier` — `IAsyncRelayCommand.IsRunning` covers the use case.
- Analyzer-only CTMvvm rules (the MVVMTK0xxx family) beyond the SHINY1xx set above.
- **`CanNavigate()` only fires for user-initiated navigation** (per `ShellNavigator`) — a programmatic
  `GoBack()` from a Save button won't re-trigger the guard, which is the desired behavior.
- DataAnnotations attributes that reflect at runtime (§5.4) are not trim/AOT-safe; the reflection-free built-ins
  are the supported set.
