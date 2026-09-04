# App Links — deep linking by registering pages

A plan for inbound URL handling (`myapp://product/123`, `https://shinylib.net/product/123`) that
costs the user an array on an attribute they already write, two MSBuild properties, and one
`UseAppLinks()` call. No `AppDelegate`, no `MainActivity`, no `App` subclass, no manual URL parsing.

## The starting point

The route map is already fully described. `[ShellMap<TPage>]` gives us the route, the page, the
ViewModel and whether the route is Shell-declared or `Routing.RegisterRoute`'d; `[ShellProperty]`
gives us the navigable parameters with their CLR types and required-ness. The generator already
turns that into `AddGeneratedMaps()`, `Routes`, `NavigateTo{X}`, `Add{X}`, `Show{X}Dialog` and the AI
route metadata. An app link is a fourth projection of the same metadata — an inbound URL template
instead of an outbound method signature.

Nothing deep-link related exists yet: no `AppLink`, no `intent-filter`, no `CFBundleURLTypes`, no
`IQueryAttributable` anywhere in the repo.

**One real gap sits underneath all of it.** `[ShellProperty]` values today only ever arrive as
already-typed CLR values, set through the generated `configure` lambda and pinned on
`ShellNavigationConfigurator` before `GoToAsync`. The library never converts a Shell query string
onto a ViewModel property — `NavigateTo(route, args)` passes parameters to `GoToAsync`, and MAUI
only applies those to `IQueryAttributable` implementors, which nothing here implements. App links
deliver strings. That conversion layer has to be built, and it has to be source-generated rather
than reflective to keep the `IsAotCompatible` promise.

## Decisions

| # | Decision | Why |
|---|---|---|
| 1 | **Templates are a `string[]` on `ShellMapAttribute`, not a separate `[AppLink]` attribute.** | A separate attribute earns its keep only if it carries per-template options. Decision #2 removes the need for those, and then the array wins on every other axis: one attribute, route and its inbound URLs visually together, and "`[AppLink]` without `[ShellMap]`" becomes unrepresentable rather than a diagnostic. |
| 2 | **Push vs. reset is inferred from `registerRoute`, never configured.** | `registerRoute: false` already means "this page is a `ShellContent` in my AppShell XAML" — the library's own documented convention. A Shell item *cannot* be pushed, a registered detail route *should* be. The answer is already in the declaration; asking for it again would be asking the user to restate what they told us. |
| 3 | **Value binding is generated, not reflected.** | `Sample.csproj` sets `IsAotCompatible` and `PublishAot` for iOS and MacCatalyst. A reflective `string` → property binder is exactly the pattern that survives testing on desktop and fails on a trimmed device build. Typed setters emitted per route also move unconvertible types from a runtime failure to a compile error. |
| 4 | **Platform delivery is hooked through `ConfigureLifecycleEvents`, not an `Application` subclass.** | The first sketch was `ShinyApplication : Application` overriding `OnAppLinkRequestReceived`, mirroring the existing `ShinyShell : Shell`. But `Application` exposes no event, only a protected virtual — and lifecycle events reach `ios.OpenUrl`, `ios.ContinueUserActivity` and `android.OnNewIntent` from *inside* the library. Strictly better: the user's own `App`, `AppDelegate` and `MainActivity` stay untouched. |
| 5 | ~~**Android manifest entries are generated;** Apple plist entries are validated, not written.~~ **Superseded — every platform is validated, not written.** See §9. | The Apple half of this held: universal links need an Apple Developer portal capability and a server-hosted AASA file, so auto-editing the plist automates the easy 10% of a task that stays manual anyway. The Android half did not survive contact — `AndroidManifestOverlay` has no stable activity name to merge onto (§9.3). A build warning carrying the exact markup is the deliverable on all three platforms. |
| 6 | **The generator moves from syntax parsing to `AttributeData`.** | `GetRouteFromAttribute` / `GetRegisterRouteFromAttribute` hand-walk `AttributeSyntax` positional arguments, with special cases for "registerRoute is the first argument when route is omitted". Threading an array argument through that is where it breaks. Prep work, not a nice-to-have. |

---

## 1. Declaring app links

```csharp
[ShellMap<ProductPage>(
    description: "Shows a product",
    appLinks: new[] { "product/{id}", "p/{id}" }
)]
public partial class ProductViewModel
{
    [ShellProperty("The product id")] public int     Id  { get; set; }
    [ShellProperty(required: false)]  public string? Tab { get; set; }
}
```

`myapp://product/123?tab=reviews` and `https://shinylib.net/p/123` both land on `ProductPage` with
`Id = 123`, `Tab = "reviews"`.

- `{token}` path segments bind to `[ShellProperty]` properties **by name, case-insensitively**.
- Query string values bind by property name too, with the same rules.
- A property may be filled by either source. Path wins if somehow both are present.
- Multiple templates per route are supported; that is the whole reason it is an array.

Two mechanical notes:

- ~~Collection expressions are believed **not** valid in attribute arguments.~~ **Wrong — verified
  they compile fine** on this toolchain (§9.1). Both forms work, and moving to `AttributeData`
  (§3.1) makes the generator agnostic to which one is written.
- `string[]? appLinks = null` is a legal optional attribute parameter — `null` is a constant, an
  empty array literal would not be.

## 2. Navigation mode

| `registerRoute` | What the route is | App link navigates |
|---|---|---|
| `false` | `ShellContent` / tab / flyout item declared in AppShell XAML | Absolute `//route` — a Shell item cannot be pushed |
| `true` | `Routing.RegisterRoute`'d detail page | Relative push onto the current stack |

Warm start is trivially correct: push relative from wherever the user already is, and back returns
them there. Cold start pushes onto whatever Shell resolved as its default first item, producing
`//defaultroot/product` and a sensible back stack.

For the case where a detail route belongs under a *specific* tab, a single global option covers it,
plus a delegate as the escape hatch for a Shell whose structure violates the `registerRoute`
convention:

```csharp
public class AppLinkOptions
{
    /// Absolute route pushed onto before a cold-start relative link. Default: Shell's own default item.
    public string? DefaultRoot { get; set; }

    /// Last word on the destination URI. Overrides everything above.
    public Func<AppLinkMatch, string>? ResolveRoute { get; set; }

    /// Called when no template matches. Default: log a warning and stay put.
    public Func<Uri, Task<bool>>? OnUnhandled { get; set; }
}
```

No per-route navigation configuration. Nothing in the attribute.

**Plumbing gap:** `ShinyAppBuilder.typeMap` stores `RegisterRoute`, but nothing exposes it —
`GetRouteForViewModel`, `GetPageTypeForRoute` and friends return only types and routes. The router
needs a `GetRouteInfo(string route)` (or equivalent) returning the tuple. Trivial, but required.

## 3. Source generator

### 3.1 Prep — read attributes semantically

Replace the syntax walkers with `ISymbol.GetAttributes()` → `AttributeData` → `TypedConstant`, which
handles named arguments, positional arguments, defaults and arrays uniformly. This deletes
`GetRouteFromAttribute`, `GetRegisterRouteFromAttribute`, `GetDescriptionFromAttribute` and their
positional-index special cases. Land it as its own commit with the existing Verify snapshots as the
safety net — the snapshots should not move.

### 3.2 Emit the registrations

Per route with templates, into `AddGeneratedMaps()`:

```csharp
builder.AddAppLink<global::Sample.ProductViewModel>(
    "product/{id}",
    static (vm, v) =>
    {
        vm.Id = global::System.Int32.Parse(v["id"], global::System.Globalization.CultureInfo.InvariantCulture);
        if (v.TryGetValue("tab", out var __tab))
            vm.Tab = __tab;
    }
);
```

Fully typed, no reflection, trim-safe, and it reuses the pinning path the navigator already uses.

Supported conversions: `string`, all integral types, `float`/`double`/`decimal`, `bool`, `Guid`,
`DateTime`, `DateOnly`, `TimeOnly`, `TimeSpan`, enums (by name and by numeric value), and nullable
variants of every one. Everything parses with `InvariantCulture` — a URL is not culture-sensitive
and a German device must not reject `1.5`.

Required properties that fail to parse **do not throw**; the template simply fails to match and the
matcher falls through to the next candidate, then to `OnUnhandled`. A malformed inbound URL is a
routing miss, not a crash.

### 3.3 Reverse generation

The templates are known at compile time, so the outbound direction is nearly free and worth having
for share sheets:

```csharp
var uri = navigator.CreateProductAppLink(id: 42, tab: "reviews");
```

Emitted only when exactly one scheme or domain is configured, since otherwise there is no single
correct base to build against.

### 3.4 Diagnostics

Continuing the `SHINY00x` series:

| ID | Severity | Condition |
|---|---|---|
| `SHINY005` | Error | Template token has no matching `[ShellProperty]` on the ViewModel |
| `SHINY006` | Error | A templated property's type has no supported conversion |
| `SHINY007` | Error | Two routes declare templates that can match the same URL |
| `SHINY008` | Warning | `appLinks` declared but no `ShinyAppLinkSchemes` / `ShinyAppLinkDomains` set |
| `SHINY009` | Warning | A `required: true` `[ShellProperty]` appears in no template and no query binding is possible |

(The originally planned "`[AppLink]` without `[ShellMap]`" diagnostic is gone — decision #1 makes it
unrepresentable.)

## 4. Runtime

| File | Role |
|---|---|
| `IAppLinks.cs` | Public surface: `Task<bool> Handle(Uri uri)`, `bool TryResolve(Uri, out AppLinkMatch)` |
| `AppLinkOptions.cs` | §2 |
| `Infrastructure/AppLinkRegistry.cs` | Template store, compiled at registration into segment arrays; matching |
| `Infrastructure/AppLinkRouter.cs` | `IMauiInitializeService`; URI → route + configured ViewModel → navigate |

### Matching

Templates are compared segment-by-segment against the URI path. Ordering is by **specificity** —
literal segments beat tokens, so `product/featured` wins over `product/{id}` regardless of
registration order. Ties after specificity are a `SHINY007` error at compile time, so the runtime
never has to guess.

Scheme and host are not part of the template: any configured scheme or domain can serve any
template. This is deliberate — it means adding a second domain later needs no attribute changes.

### Navigation

The router resolves the ViewModel from DI, runs the generated setter, `EnqueueResolved`s it on
`ShellNavigationConfigurator`, then navigates — the exact same path as `NavigateTo<TViewModel>`.
That is not an implementation convenience; it means app-link navigation inherits every Android
timing fix already fought for in the pinned-ViewModel model, rather than opening a second code path
that has to rediscover them.

### Cold start

The part that always breaks. A link can arrive before DI, before `Shell.Current`, and before the
first page appears. The router holds a single pending URI and flushes it once the Shell is live —
`ShinyShellNavigator.Initialize` and `AppOnPageAppearing` are the natural points, both of which
already exist for the initial-page case. A second link arriving before the flush replaces the first;
users do not deep-link twice in 200ms and queueing them would produce a nonsense stack.

This needs an explicit test rather than manual device checking.

## 5. Platform wiring

### 5.1 Configuration

Schemes and domains are needed at build time (manifests) and runtime (reverse generation), so MSBuild
properties are the single source of truth:

```xml
<PropertyGroup>
  <ShinyAppLinkSchemes>myapp</ShinyAppLinkSchemes>
  <ShinyAppLinkDomains>shinylib.net;www.shinylib.net</ShinyAppLinkDomains>
</PropertyGroup>
```

Both become `CompilerVisibleProperty` entries in `build/Package.targets`, which already ships to
`build/` and `buildTransitive/` from `Shiny.Maui.Shell.csproj`.

### 5.2 Delivery — `UseAppLinks()`

```csharp
.UseShinyShell(x => x
    .AddGeneratedMaps()
    .UseAppLinks()
)
```

Registers, from inside the library, via `ConfigureLifecycleEvents`:

| Platform | Hook | Carries |
|---|---|---|
| iOS / MacCatalyst | `ios.ContinueUserActivity` | Universal links (MAUI also forwards these to `SendOnAppLinkRequestReceived` already) |
| iOS / MacCatalyst | `ios.OpenUrl` | Custom schemes — MAUI does **not** forward these |
| Android | `android.OnNewIntent` | Warm start |
| Android | `android.OnCreate` | Cold start, from the launch `Intent` |
| Windows | `windows.OnLaunched` | Protocol activation |

Exact hook signatures need confirming against .NET 10 MAUI. If any platform turns out not to be
reachable this way, the fallback for that platform alone is a documented one-line call to
`IAppLinks.Handle(uri)` — `IAppLinks` is public precisely so this stays possible.

### 5.3 Android — generated

A target writes an overlay into `$(IntermediateOutputPath)` and adds it as `AndroidManifestOverlay`:

```xml
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
  <application>
    <activity android:name="crc64...MainActivity">
      <intent-filter android:autoVerify="true">
        <action android:name="android.intent.action.VIEW" />
        <category android:name="android.intent.category.DEFAULT" />
        <category android:name="android.intent.category.BROWSABLE" />
        <data android:scheme="https" android:host="shinylib.net" />
      </intent-filter>
    </activity>
  </application>
</manifest>
```

Nothing is written into the user's source tree. Needs verifying that `AndroidManifestOverlay`
behaves as expected in .NET 10 and that the activity can be targeted without hardcoding the mangled
CRC name.

App Links (verified, no disambiguation dialog) additionally need
`https://<domain>/.well-known/assetlinks.json` with the app's signing certificate fingerprint —
server-side, documented, not automated.

### 5.4 Apple — validated

Two independent mechanisms, two different files.

**Universal links (`https://`) → `Entitlements.plist`:**

```xml
<key>com.apple.developer.associated-domains</key>
<array>
    <string>applinks:shinylib.net</string>
    <string>applinks:www.shinylib.net</string>
</array>
```

One `applinks:` entry per domain — no scheme, no path, since paths are decided server-side.
Subdomains are not implicit: `shinylib.net` does not cover `www.shinylib.net`. Appending
`?mode=developer` enables on-device testing without waiting on Apple's CDN cache.

**Custom schemes (`myapp://`) → `Info.plist`:**

```xml
<key>CFBundleURLTypes</key>
<array>
    <dict>
        <key>CFBundleTypeRole</key>
        <string>Editor</string>
        <key>CFBundleURLName</key>
        <string>org.shiny.maui</string>
        <key>CFBundleURLSchemes</key>
        <array>
            <string>myapp</string>
        </array>
    </dict>
</array>
```

`CFBundleURLName` is a reverse-DNS identifier, conventionally the bundle id. `LSApplicationQueriesSchemes`
is **not** needed — that is only for `CanOpenUrl` against other apps.

MacCatalyst takes the same keys in its own `Info.plist` and `Entitlements.plist`; the existing
sandbox entitlements stay.

**Why this is validated rather than injected** — the Sample demonstrates every obstacle:

1. There is no `Platforms/iOS/Entitlements.plist` at all. Only MacCatalyst has one. Associated
   domains means *creating* a file, not merging into one.
2. No `CodesignEntitlements` property is set anywhere in the repo. Whether the iOS/MacCatalyst SDK
   defaults it to the platform folder needs verifying — if it does not, creating the file achieves
   nothing.
3. Merging into a hand-edited plist needs real semantics: append if the array key exists, create if
   not, never clobber. Either we mutate the user's source tree, or we generate into `obj/` and
   repoint the SDK, which depends on (2).
4. Universal links need an App ID capability and a regenerated provisioning profile regardless. The
   plist edit is the easy tenth of a task that stays manual.

So: a build target reads the plists and emits a warning containing the exact XML above, pre-filled
with the user's configured schemes and domains, naming the file to paste it into. Zero risk, still
saves the user from guessing at key names.

### 5.5 Windows

`Package.appxmanifest` protocol extension, same validator-warning treatment. `WindowsPackageType`
is `None` in the Sample, so this is untestable there without changes — lowest priority.

## 6. Out of scope

- **Outbound URL opening.** `Launcher.OpenAsync` already exists and is not our problem.
- **AASA / `assetlinks.json` generation.** They need a team ID and a signing fingerprint the build
  does not reliably know, and they are server-deployed. Documented, with the required shape.
- **Provisioning and Developer portal setup.** Unautomatable.
- **Deferred deep linking** (install-then-link attribution). A different product entirely.
- **Per-template scheme or host restriction.** Any configured scheme serves any template; adding a
  domain should not mean editing attributes.
- **Dialog routes as app-link targets.** `Show{X}Dialog` is a modal awaiting a result — an inbound
  URL has nobody to return that result to.

## 7. To verify before building

1. Collection expressions in attribute arguments — assumed unsupported (§1).
2. `ConfigureLifecycleEvents` hook signatures on each platform in .NET 10 MAUI (§5.2).
3. `AndroidManifestOverlay` behavior in .NET 10, and targeting the activity without the CRC name (§5.3).
4. Whether the iOS/MacCatalyst SDK defaults `CodesignEntitlements` to the platform folder (§5.4).
5. Whether MAUI forwards Android launch intents to `SendOnAppLinkRequestReceived` on its own.

## 8. Work

| Step | Change |
|---|---|
| 1 | Generator: migrate to `AttributeData` reads; snapshots must not move |
| 2 | String → typed value conversion + generated setters; `ShinyAppBuilder.GetRouteInfo` |
| 3 | `ShellMapAttribute.appLinks`; `ShellMapInfo.AppLinks`; `AddAppLink` registration codegen |
| 4 | `IAppLinks`, `AppLinkOptions`, `AppLinkRegistry` (compile + match + specificity ordering) |
| 5 | `AppLinkRouter` — resolve, set, pin, navigate; `registerRoute` mode inference; cold-start flush |
| 6 | `UseAppLinks()` + `ConfigureLifecycleEvents` hooks per platform |
| 7 | `SHINY005`–`SHINY009` |
| 8 | Android `AndroidManifestOverlay` generation |
| 9 | Apple + Windows plist/manifest validator warnings |
| 10 | Reverse URL generation (`CreateXAppLink`) |
| 11 | Generator tests (each template shape, each conversion, each diagnostic) + registry matcher tests + cold-start flush test |
| 12 | Sample: app links on `ProductPage`-style route, scheme + domain configured, Android manifest wired |
| 13 | `readme.md`, `skills/shiny-maui-shell` (SKILL.md trigger keywords + reference), docs site `applinks.mdx` + `sidebar-topics.mjs` + release note |


---

## 9. What changed during implementation

The plan survived mostly intact. Five things did not, and one obstacle turned out not to exist.

### 9.1 Collection expressions in attributes — the plan was wrong

`appLinks: ["product/{id}"]` compiles. Verified by building both forms against the repo's toolchain
before writing any generator code. Both `new[] { ... }` and the collection expression produce the
same `TypedConstant`, so §3.1's move to `AttributeData` made the question moot anyway — but the
caution in §1 was unfounded and the docs should show the collection expression, which is what a
consumer will reach for.

### 9.2 `CodesignEntitlements` — the obstacle dissolved

§5.4 obstacle #2 assumed creating `Platforms/iOS/Entitlements.plist` might achieve nothing without
also setting `CodesignEntitlements`. It does not: evaluating the property showed MacCatalyst already
resolving to `Platforms/MacCatalyst/Entitlements.plist` purely because the file exists, and creating
the iOS one made that property resolve too. **Creating the file is sufficient.** The validator says
so explicitly rather than sending people to add a property they do not need.

### 9.3 Android cannot be generated — decision #5 was half wrong

The merged manifest names the launcher activity `crc64b28577ed8416fd3b.MainActivity` — a CRC64 hash
of its namespace. MSBuild cannot compute that, and the Android manifest merger matches activities by
`android:name`, so an `AndroidManifestOverlay` has nothing stable to merge onto and would append a
second activity rather than amend the real one. Computing the hash in a custom task would break
silently the moment `MainActivity` moved namespace.

So Android gets the same treatment as Apple: a warning carrying a pasteable `[IntentFilter]`. One
attribute, the documented .NET MAUI route, and it cannot silently corrupt a manifest. The validation
reads the **merged** manifest (hooked on `Build`) rather than `Platforms/Android/AndroidManifest.xml`,
because `[IntentFilter]` attributes only appear after the manifest is generated — checking the source
manifest produced a false positive on a correctly configured app.

One related trap: the hook must be on a *public* target. `AfterTargets="_GenerateJavaStubs"` silently
did nothing, because a consuming project that imports the targets before the SDK's cannot resolve a
private target name.

### 9.4 Lifecycle hooks — confirmed, with exact signatures

Verified against the real MAUI reference assemblies rather than assumed:

| Hook | Signature |
|---|---|
| `iOSLifecycle.OpenUrl` | `(UIApplication, NSUrl, NSDictionary) -> bool` |
| `iOSLifecycle.ContinueUserActivity` | `(UIApplication, NSUserActivity, UIApplicationRestorationHandler) -> bool` |
| `AndroidLifecycle.OnCreate` | `(Activity, Bundle) -> void` |
| `AndroidLifecycle.OnNewIntent` | `(Activity, Intent) -> void` |

Both return `bool` on iOS, so the platform gets a synchronous answer from `TryResolve` while the
navigation itself completes on its own schedule.

### 9.5 `AppLinkRoutes` extracted from the router

The push-vs-reset rule started as a private method on `AppLinkRouter`. It is pure logic over a match,
a registration and the options, and it is the single most important decision the feature makes — so
it moved to `Infrastructure/AppLinkRoutes.cs` where it is directly testable without standing up a
Shell. Seven tests pin it.

### 9.6 Cold-start flush is not unit tested

Touching any MAUI static (`Shell.Current`) from a plain test host **hangs** — confirmed by a probe
that had to be killed. The test project deliberately avoids referencing MAUI for exactly this reason
(`NavigatorContractTests` reads source as text instead). The registry, the binders and the route rule
are all covered behaviourally; the ~15 lines that queue a cold-start URI and flush it on the first
`PageAppearing` are covered only by the Sample. That is a real gap, and the honest place to close it
is a device/emulator run, not a mock.

### 9.7 Test count

66 → 115. Twenty-six generator tests (templates, every conversion, all five diagnostics, both URI
builder gates), sixteen registry tests (custom-scheme host handling, specificity ordering, escaping,
`+`-as-space, path-beats-query), seven route-rule tests.

---

## 10. App icon shortcuts

Long-press quick actions on the home screen icon. **Built** — see §12 for what changed on contact.

**MAUI already ships the platform layer — `Microsoft.Maui.ApplicationModel.AppActions`.** This
section is only about what sits on top of it.

### 10.1 Positions taken and abandoned

Rewritten four times. Recording why, because the errors are more useful than the conclusions.

1. **"Dynamic only, skip static."** Rejected static because it implied manifest edits and the CRC64
   activity-name problem (§9.3). Sound reasoning, wrong conclusion: *declared statically* does not
   imply *installed via manifest*.
2. **"Shortcuts carry an app link URI."** Wrong layer. It forced a route to have a public app link
   before it could have a private quick action, and round-tripped typed values through
   percent-encoded strings between two in-process components.
3. **"Write the platform code ourselves."** **MAUI already has this API.** Two drafts specified
   `AppShortcuts.iOS.cs` / `.Android.cs` reimplementing `UIApplicationShortcutItem` and
   `ShortcutManagerCompat`, plus a warning that "icons do not unify" — a problem MAUI had solved.
   §9.4 and the then-§10.6 carefully verified the *lifecycle hooks* without ever asking whether the
   **feature** existed one layer up. Checking a low-level seam is not a substitute for checking the
   framework's own surface first.
4. **"A separate `[AppShortcut]` attribute."** Argued for on the grounds that decision #1's
   criterion (a separate attribute earns its keep only if it carries per-item options) cuts that way
   for four options. Overruled, correctly: `[AppShortcut]` without `[ShellMap]` is inert anyway, so
   the separation buys nothing real, and named properties (§10.4) cost no constructor bloat.

### 10.2 What MAUI already provides

| API | Gives us |
|---|---|
| `AppAction(id, title, subtitle = null, icon = null)` | The cross-platform model, **including the icon abstraction** |
| `AppActions.Current.SetAsync(...)` / `GetAsync()` | Dynamic install and read-back |
| `AppActions.Current.IsSupported` | Graceful degradation (Android needs API 25+) |
| `IEssentialsBuilder.OnAppAction(Action<AppAction>)` | Activation callback registered at build time |
| `IEssentialsBuilder.AddAppAction(...)` / `EssentialsExtensions.AddAppAction(...)` | Startup registration; chainable, returns `IEssentialsBuilder` |
| `IPlatformAppActions.PerformActionForShortcutItem` | The iOS hook — wired internally, not our problem |

No platform files, no lifecycle hooks, no icon design. None of it should be written.

Prefer the builder-level `OnAppAction` over the static `AppActions.OnAppAction` event: MAUI owns the
subscription lifetime and it cannot miss an early activation. The cost is that a builder callback has
no DI yet, so services resolve lazily through `IPlatformApplication.Current?.Services` — the pattern
`AppLinkLifecycle.Dispatch` already uses.

### 10.3 What is still missing

MAUI's API is stringly-typed and disconnected from navigation — `AddAppAction("search_id", ...)` and
then a hand-written `switch` over `e.AppAction.Id` that must be kept in sync by hand, where a typo is
a silent no-op. That is the gap `[ShellMap]` already closes for `Routing.RegisterRoute`.

### 10.4 Declaration — named properties on `ShellMap`

No separate attribute. A shortcut without a route mapping is inert, so the separation would buy
nothing; and C# named property initializers keep the constructor at four parameters.

```csharp
[ShellMap<HomePage>(
    registerRoute: false,
    appLinks: ["home"],
    Shortcut         = "Home",
    ShortcutSubtitle = "Back to the demo list",
    ShortcutIcon     = "home",
    ShortcutOrder    = 0
)]
public partial class HomeViewModel : ObservableObject { }
```

```csharp
public sealed class ShellMapAttribute<TPage>(
    string? route = null,
    bool registerRoute = true,
    string? description = null,
    string[]? appLinks = null
) : Attribute
{
    // ...existing members...

    /// <summary>Quick action title. Setting this declares a home screen shortcut for the route.</summary>
    public string? Shortcut { get; set; }
    public string? ShortcutSubtitle { get; set; }
    public string? ShortcutIcon { get; set; }
    public int ShortcutOrder { get; set; }
}
```

`Shortcut` (the title) is the trigger — non-null means "this route has a quick action".

No generator plumbing is needed to read these: `GetStringArg` / `GetBoolArg` already fall back to
`AttributeData.NamedArguments` by name (§3.1).

**Known limitation:** an attribute literal cannot be localized. If shortcut titles need translating,
this design is a dead end and a `ShortcutProvider = typeof(...)` escape hatch resolving from
resources at runtime is the answer. Decide before shipping, not after.

### 10.5 Registration API — the generator is a thin wrapper

The generator emits calls into a **public** API rather than emitting `ConfigureEssentials` directly,
so that turning source generation off does not take the feature with it. This mirrors
`AddGeneratedMaps()` being a generated wrapper over the public `builder.Add<TPage, TViewModel>()`.

```csharp
public ShinyAppBuilder AddAppShortcut<TViewModel>(
    string title,
    string? subtitle = null,
    string? icon = null,
    int order = 0,
    string? id = null,                      // defaults to the route
    Action<TViewModel>? configure = null
) where TViewModel : class;
```

Generated:

```csharp
builder.AddAppShortcut<Sample.HomeViewModel>("Home", "Back to the demo list", "home", order: 0);
```

Hand-written, for anyone not using the generator — and note this form handles parameterised routes:

```csharp
builder.AddAppShortcut<ProductViewModel>(
    "Featured", icon: "star", id: "featured-product", configure: vm => vm.Id = 42
);
```

**Why the lambda works even though shortcuts outlive the process:** only the *id* is persisted by
iOS and Android. The lambda lives in our registration table, which is rebuilt every launch, so on
activation we resolve id → registration → run the lambda. Nothing needs serializing. It mirrors
`NavigateTo<TViewModel>(configure)` exactly, so it introduces no new concept.

`id` must be overridable, or two shortcuts to the same route with different values are impossible.

### 10.6 Diagnostics

| ID | Severity | Condition |
|---|---|---|
| **SHINY010** | Error | `Shortcut` set on a route that has a required `[ShellProperty]` |
| **SHINY011** | Warning | More than four shortcuts declared — iOS silently drops the excess |
| **SHINY012** | Error | A `Shortcut*` property set without `Shortcut` (the title) |

**SHINY010** keys on **required `[ShellProperty]`**, not on whether an app link template has tokens.
They usually coincide but not always: a route can require parameters while declaring no template, and
a token can map to an optional property. The message must name the offending property *and* point at
the way forward — `AddAppShortcut<T>(configure: …)` (§10.5) registers exactly this case by hand. A
compile error that names the escape hatch is worth far more than one that only says no.

**SHINY012** exists because named properties give up what a constructor parameter guaranteed. With a
separate `[AppShortcut("Home")]` the title could not be omitted; with `ShortcutIcon = "home"` and no
`Shortcut`, the declaration silently produces nothing. This is the one real cost of §10.4 and it is
worth closing with a diagnostic rather than documentation.

**SHINY011** earns its place because the platform failure is silent — four items appear, the fifth
does not, and nothing anywhere says why. `AddAppShortcut` should log the equivalent warning at
runtime, since hand-registering users get no compile-time check at all.

Manual registrants also get no SHINY010; the `configure` lambda is how they satisfy required
properties, and nothing verifies that they did.

### 10.7 Delivery

`AppAction.Id` → registration lookup → optional `configure` → `AppLinkRoutes.Build` → navigate.

Reusing `AppLinkRoutes.Build` means the `registerRoute` push-vs-reset inference (§2) applies to
shortcuts for free. It needs one refactor — splitting the rule out from the app-link-shaped arguments
it currently takes:

```csharp
public static string Build(string route, bool registerRoute, bool coldStart, string? defaultRoot)
{
    if (!registerRoute)
        return "//" + route;

    if (coldStart && !string.IsNullOrWhiteSpace(defaultRoot))
        return defaultRoot!.TrimEnd('/') + "/" + route;

    return route;
}

public static string Build(AppLinkMatch match, RegisteredAppLink link, bool coldStart, AppLinkOptions options)
    => options.ResolveRoute?.Invoke(match)
       ?? Build(match.Route, link.RegisterRoute, coldStart, options.DefaultRoot);
}
```

The seven existing `AppLinkRouteTests` pass unchanged against the overload.

To verify during implementation: whether activation can fire before Shell exists on a cold start. If
it can, generalise `AppLinkRouter`'s pending queue to hold either activation kind rather than
duplicating it.

### 10.8 Remaining gotchas

- **iOS caps at 4 items** and drops the rest silently — hence SHINY011.
- **`AppActions.IsSupported` can be false** (Android below API 25). Registration should no-op quietly
  rather than throw.
- **`ShortcutOrder` must be explicit**; source order across files is not something the generator
  should promise.
- **Titles truncate hard** on both platforms and neither reports it.

*(The earlier "constant values in the attribute" refinement is dropped. It existed to smuggle values
through an attribute; §10.5's `configure` lambda does the same job typed, with no question about
where the values live in `AppAction`.)*

### 10.9 Work

| Step | Change |
|---|---|
| 1 | `ShellMapAttribute` — `Shortcut`, `ShortcutSubtitle`, `ShortcutIcon`, `ShortcutOrder` named properties |
| 2 | `ShinyAppBuilder.AddAppShortcut<TViewModel>(...)` + registration table; runtime cap warning |
| 3 | Generator: read the named properties, SHINY010/011/012, emit `AddAppShortcut` calls into `AddGeneratedMaps()` |
| 4 | `AppLinkRoutes.Build` primitive overload (§10.7) |
| 5 | `UseAppShortcuts()` — `ConfigureEssentials` + `OnAppAction`, resolve id → registration → navigate |
| 6 | Generator tests (each diagnostic, emitted registration); id → route resolution tests |
| 7 | Sample: a shortcut on `HomeViewModel`, verified on the simulator |
| 8 | `readme.md`, skill, docs site page + release note |

## 11. ~~Known gap~~: `UIScene` — FIXED (§14)

Surfaced while checking the shortcut hooks. `iOSLifecycle` exposes **`SceneOpenUrl(UIScene, NSSet<UIOpenUrlContext>) -> bool`**
and **`SceneContinueUserActivity`** alongside the `AppDelegate`-level hooks §5.2 installed.

An app that adopts `UIScene` — multi-window on iPad — does **not** get `AppDelegate.OpenUrl`; it gets
`SceneOpenUrl`. The shipped iOS wiring only covers the non-scene path.

This is latent rather than broken: the Sample is not scene-based, which is why the simulator run in
§9 passed. But any consumer who enables scenes gets silently dead custom-scheme links, and silence is
the worst failure mode for this feature.

**Fix:** hook `SceneOpenUrl` and `SceneContinueUserActivity` next to the existing pair and dispatch
identically — `Dispatch` is already the shared entry point, so it is a handful of lines. Both
variants firing is harmless: the second call finds the same URI and re-navigates to the route the app
is already on.

Worth doing independently of §10, and before it — §10.2's `PerformActionForShortcutItem` is
`AppDelegate`-level too and will have the same scene-shaped hole.


---

## 12. Shortcuts — what changed during implementation

§10's design held. Four notes.

### 12.1 Named-argument reads needed their own helpers

`GetStringArg(attr, index, name)` checks `ConstructorArguments[index]` *before* falling back to
named arguments, so reading `Shortcut` with any in-range index would have silently returned the
`route` constructor argument instead. Named property initializers never appear in
`ConstructorArguments`, so `GetNamedString` / `GetNamedInt` deliberately take no index. The §10.4
claim that "no generator plumbing is needed" was wrong in that one detail.

### 12.2 SHINY010 fires per required property, not per route

A route with three required properties reports three diagnostics, each naming its own property.
Noisier, but each message carries the exact `configure: x => x.Foo = ...` the developer needs, and a
single message listing three properties helps less.

### 12.3 The registry is deliberately case-sensitive

`AppShortcutRegistry.Find` uses `StringComparison.Ordinal`. The id round-trips through the platform
verbatim, so lenient matching would paper over a real mismatch between what was registered and what
came back. `AppLinkRegistry` is case-*insensitive* for values by contrast, because those come from a
URL a human may have typed. Different provenance, different rule.

### 12.4 Verified, and not

Verified: generation, all three diagnostics, ordering, registry lookup, the `configure` lambda, and
that the Sample builds and runs on the simulator with shortcuts registered (139 tests, up from 128).

**Not verified: that the quick actions actually appear on the home screen, or that activation
navigates.** Driving a long-press needs Accessibility permission for `osascript`, which is not
granted on the development machine; `simctl` offers no shortcut-activation command; and iOS `Debug`
logging does not reach `simctl launch --console`, so even the registration could not be confirmed
from outside the app. Searching the simulator's data container for the shortcut title found nothing,
which is inconclusive rather than negative — SpringBoard owns that state.

This is a genuine gap of the same shape as §9.6. The honest close is a manual long-press on a
simulator or device, or a temporary in-app screen calling `AppActions.Current.GetAsync()` and
rendering the result — the latter would also make the Sample demonstrate the feature, which it
currently does not.


---

## 13. Registration became implicit

`UseAppLinks()` and `UseAppShortcuts()` were opt-in switches: declare a template, then remember a
second call or nothing happens. Both are now implicit in `AddGeneratedMaps()`.

**Declaring is the opt-in.** Writing `appLinks: ["product/{id}"]` or `Shortcut = "Search"` is an
unambiguous statement of intent; asking for it twice bought nothing but a failure mode. This
restores the framing the feature started from — "basically registering pages".

What changed:

- `RegisterAppLinks()` calls `AppLinkLifecycle.Register(builder)` whenever any template exists.
- `RegisterAppShortcuts()` hands the set to `ConfigureEssentials` whenever any shortcut exists.
- `UseAppLinks(Action<AppLinkOptions>?)` survives as **optional tuning only**.
- `UseAppShortcuts()` is **deleted** — it had nothing to configure.
- `AppLinksNotConfiguredWarning` and `AppShortcutsNotConfiguredWarning` are deleted. The condition
  they reported cannot occur any more, which is a better outcome than reporting it well.

Platform hooks are still installed only when something is declared, so an app with neither feature
hooks nothing.

**Also fixed:** the `OnAppAction` callback resolved `AppShortcutRouter` and returned silently when
resolution failed, so a tapped shortcut would do nothing with no explanation — the exact silent
failure SHINY011 exists to prevent elsewhere. It now logs an error naming the id.

Re-verified on the simulator after the change: `shinyshell://detail/no-usecall?highlight=7` navigates
and binds correctly with `AddGeneratedMaps()` as the only call. Shortcut activation remains
unverified for the reasons in §12.4.


---

## 14. `UIScene` fixed, and localized shortcut text

### 14.1 MAUI does not bridge the scene delegate — confirmed from source

§11 was right, and worth confirming rather than assuming either way.
`MauiUISceneDelegate.OpenUrl` reads:

```csharp
[Export("scene:openURLContexts:")]
public virtual bool OpenUrl(UIScene scene, NSSet<UIOpenUrlContext> urlContexts)
{
    GetServiceProvider()?.InvokeLifecycleEvents<iOSLifecycle.SceneOpenUrl>(...);
}
```

Only the Scene-prefixed event. Same for `ContinueUserActivity`. MAUI ships both delegates but
forwards between neither, so a scene-based app got nothing from the `AppDelegate` hooks.

Both variants are now hooked and dispatch identically through the existing shared `Dispatch`. iOS
calls one delegate or the other, never both, so this cannot double-deliver — and neither Sample
plist declares `UIApplicationSceneManifest` (confirmed), which is why the earlier simulator runs
passed and why this was latent rather than broken.

### 14.2 `ActivationDeduplicator` — a guard the scene work exposed

§11 originally claimed both variants firing would be "harmless: the second call re-navigates to the
route the app is already on". That was wrong — a second navigation **pushes a duplicate page**.

The mutual exclusivity above makes it moot for iOS, but the same double-delivery is reachable on
Android: `OnCreate` re-runs with the original intent when the activity is recreated, so a rotation
on an app without the right `ConfigurationChanges` would re-fire the deep link. An identical
activation within one second is now ignored. Six tests; the timestamp refreshes on every call so a
rapid stream stays suppressed rather than slipping through once the original ages out.

### 14.3 Localized shortcut titles

`Shortcut` and `ShortcutSubtitle` are attribute literals — the limitation flagged in §10.4. Resolved
with `IAppShortcutText` + `UseShortcutText<T>()`: the declared string becomes a resource key, with
the literal as its own fallback so a missing resource degrades to readable text.

Two details that shaped the implementation:

**Resolution cannot happen where the shortcuts are installed.** The first attempt resolved text
inside the `ConfigureEssentials` callback via `IPlatformApplication.Current?.Services`. That
delegate runs during `Build()`, before any service provider exists, so it would have silently
fallen back to the default every time. The declared strings are installed as a baseline, and an
`IMauiInitializeService` re-pushes through the provider once the app is up — but **only when a
custom provider is registered**, so apps that do not localize pay nothing.

**Refresh is not optional.** Installed shortcuts keep their text until pushed again, so without
`IAppShortcuts.Refresh()` "localized" would silently mean "localized as of last launch".

Scope note: this covers the shortcut title and subtitle only. `ShellMap`'s `description:` — the
AI-facing string baked into `GeneratedRouteInfo` and the AI prompt — is untouched, and it is a
genuinely different question, since an AI tool description arguably should follow the model rather
than the user's language.
