# App Links — deep linking by registering pages

A plan for inbound URL handling (`myapp://product/123`, `https://shinylib.net/product/123`) that
costs the user an array on an attribute they already write, two MSBuild properties, and one
`UseAppLinks()` call. No `AppDelegate`, no `MainActivity`, no `App` subclass, no manual URL parsing.

## The starting point

The route map is already fully described. `[ShellMap<TPage>]` gives us the route, the page, the
ViewModel and whether the route is Shell-declared or `Routing.RegisterRoute`'d; `[ShellProperty]`
gives us the navigable parameters with their CLR types and required-ness. The generator already
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
| 5 | **Android manifest entries are generated; Apple plist entries are validated, not written.** | Android has `AndroidManifestOverlay`, a first-class non-invasive merge that writes nothing into the user's source tree. The Apple side has no equivalent — see §5.3. And universal links need an Apple Developer portal capability and a server-hosted AASA file, neither of which a NuGet package can touch, so auto-editing the plist automates the easy 10% of a task that stays manual anyway. A build warning carrying the exact XML is the honest deliverable. |
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

- Collection expressions (`appLinks: ["product/{id}"]`) are believed **not** valid in attribute
  arguments — attribute arguments must be constant expressions and C# 12/13 did not extend that to
  collection expressions. Needs confirming; `new[] { ... }` is safe regardless, and moving to
  `AttributeData` (§3.1) makes the generator agnostic to which form is written.
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
