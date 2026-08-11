# Plan — Custom Shell Chrome (`Shiny.Maui.Shell.Chrome`)

Status: **planning, not started.** Target: v6.x, additive.

Goal: let an app replace the tab bar and flyout that .NET MAUI Shell renders natively, so
"wild" chrome is possible — a center tab that opens a context menu, arbitrary bar heights,
custom animations, drawers from the right/top/bottom — without giving up Shell routing,
`[ShellMap]` source generation, or ViewModel lifecycle.

## Decisions locked

| Decision | Choice |
|:--|:--|
| Packaging | New **`Shiny.Maui.Shell.Chrome`** package, depends on `Shiny.Maui.Controls` |
| Upstream changes | **Allowed** — extend `Shiny.Maui.Controls` (`~/Desktop/dev/controls`) rather than fork |
| Platforms | **Full parity** — iOS, Android, Mac Catalyst, Windows |
| Window-overlay host | **Spike first** (Phase 0), commit only on evidence |

---

## 1. Why a new layer

The tab bar and flyout are owned by platform renderers. MAUI exposes styling knobs, not
structure. Verified against MAUI 10.0.51:

| Surface | iOS / Catalyst | Android | Windows |
|:--|:--|:--|:--|
| Renderer seam | `ShellRenderer` (compat), `IShellItemRenderer`, `IShellFlyoutRenderer`, `IShellContext` | `ShellRenderer` (compat), `CreateShellFlyoutRenderer`, `IShellItemRenderer` | `ShellHandler` → `NavigationView` |
| Custom tab bar | subclass the `UITabBarController` renderer | swap `BottomNavigationView` | very limited |
| Flyout placement | left only | left only (`DrawerLayout` supports `END`, unused) | left only |

The current library already pays for this: `ShellTabBadgePlatform.*` reaches into
`UITabBarItem` / `BottomNavigationView` **by index**, and `DisableShellFlyoutSwipeHandler`
disables pan gestures by walking the native view tree. Both are workarounds for having no
structural seam.

**Rejected: the renderer route.** Three divergent implementations, nothing usable on
Windows, no path at all to top/bottom drawers, and breakage risk on every MAUI point
release. Chrome will touch only public bindable APIs.

## 2. Architecture — mirror the Shell tree, never replace it

Suppress the native chrome (`Shell.SetTabBarIsVisible(page, false)`,
`FlyoutBehavior="Disabled"`) and render the bar and drawer as ordinary MAUI views driven
by `Shell.Items`.

**Hard constraint:** the Shell tree stays the source of truth. Routes, `//route`
navigation, `[ShellMap]`, `ShinyRouteFactory`, `INavigationAware` / `IPageLifecycleAware`,
and the existing tests all keep working untouched. Chrome is a *view over* `Shell.Items`
plus a supplemental item list.

```
ShinyShell
 ├── Shell.Items                 ← unchanged, source of truth
 └── ShellChrome (opt-in)
      ├── ShellChromeState       ← POCO: selected index, badges, drawer state, visibility
      │                            headless-testable in tests/Shiny.Maui.Shell.Tests (net10.0)
      ├── IChromeHost            ← WHERE it renders (§3)
      ├── ShinyTabBar   : ShellChromePart
      └── ShinyDrawer   : ShellChromePart
```

Default stays `NativeChrome` — no behaviour change, no cost, for anyone who does not opt
in. `Shiny.Maui.Shell` itself gains nothing except (optionally) the `SelectTab` navigator
members; everything else lives in the new package.

## 3. Host strategy

Where the chrome view is parented.

| | **A. Page-content host** | **B. Window-overlay host** |
|:--|:--|:--|
| Mechanism | walk to the leaf `ContentPage`, wrap `Content` in a `Grid` | insert `chromeView.ToPlatform(ctx)` into the platform root (iOS `UIWindow`, Android `android.R.id.content`, WinUI root panel) |
| Platform code | none | ~120–180 LOC × 4 |
| Proven? | **yes** — `ToastManager` in Shiny.Maui.Controls does exactly this today | no — nothing in Controls does window-level overlay |
| Center FAB floating above content | ✗ clipped to page | ✓ |
| Continuous animation across tab switch | ✗ restarts | ✓ |
| Drawer covers Shell nav bar | ✗ | ✓ |
| Safe areas | free via MAUI 10 `SafeAreaEdges` | manual |
| Z-order vs. dialogs/toasts | none | needs a layer-priority model |

Host A is the default and ships first. The chrome views and `ShellChromeState` are
host-agnostic, so B can land later without reworking them.

### Phase 0 spike — exit criteria

Build both hosts throwaway on iOS + Android with a dummy bar, then answer:

1. Does a center FAB overflowing the bar upward by ~24dp render un-clipped under host A?
   *(expected: no — this is the main case for B)*
2. With `ShellChromeState` shared, is the visual discontinuity on tab switch under host A
   acceptable, or does it read as a flicker?
3. Does re-wrapping `page.Content` conflict with `ShinyShell.OnNavigated`'s
   BindingContext assignment ordering?
4. Android edge-to-edge (API 35+) insets under both hosts.
5. Per-navigation cost of the page wrap.

**Decision rule:** if ≥2 of the "wild" scenarios fail under host A, build host B in Phase 2.
Otherwise defer B behind an opt-in `.UseWindowOverlayHost()` flag.

> ⚠️ **Known collision.** `ToastManager` already mutates the leaf page's `Content`. If
> Chrome does the same independently, the two fight over the same property. This is the
> concrete reason the shared host must be extracted upstream (§7, item 2) rather than
> reimplemented here.

## 4. Tab bar

The item model has to be richer than `ShellContent` — that is the whole point of the
center-button ask.

```csharp
public abstract class TabItem : BindableObject      // Title, Icon, Badge, IsEnabled, IsVisible, Order
public sealed class NavigationTabItem : TabItem     // Route or ViewModel -> switches Shell.CurrentItem
public sealed class ActionTabItem     : TabItem     // Command only, never navigates
public sealed class MenuTabItem       : TabItem     // Items + MenuTemplate — the "center popup" case
```

```xml
<shiny:ShinyShell ...>
    <shiny:ShinyShell.TabBar>
        <shiny:ShinyTabBar Height="72" Placement="Bottom"
                           ItemTemplate="{StaticResource TabTpl}"
                           SelectedItemTemplate="{StaticResource TabSelTpl}"
                           SelectionAnimation="{StaticResource Springy}">
            <shiny:NavigationTabItem Route="HomePage" Title="Home" Icon="home.png" />
            <shiny:MenuTabItem Title="Create" Icon="plus.png"
                               MenuTemplate="{StaticResource RadialMenu}">
                <shiny:TabMenuItem Title="New Order" Command="{Binding NewOrder}" />
                <shiny:TabMenuItem Title="Scan"      Command="{Binding Scan}" />
            </shiny:MenuTabItem>
            <shiny:NavigationTabItem Route="BadgeDemoPage" Title="Badges" />
        </shiny:ShinyTabBar>
    </shiny:ShinyShell.TabBar>
</shiny:ShinyShell>
```

- **Auto-mirror mode** — omit the items and the bar populates from `Shell.Items`, so an
  existing shell gets custom chrome with one line of markup.
- **Tap interception** — `bool OnItemTapped(TabItem)`; returning `true` swallows
  navigation. This is how `MenuTabItem` opens its menu instead of routing.
- **Per-page visibility** — attached `ShinyShell.TabBarVisibility` =
  `Visible | Hidden | AutoHideOnScroll`, animated. Default policy mirrors native
  (hidden on pushed pages).
- **Badges** — `ShellTabBadgeManager` currently resolves a tab *index* and pokes the
  native item; that breaks the moment the bar is managed. Route badges through
  `ShellChromeState`, and extend beyond int-only to dot / text / count-overflow modes.
- **`MenuTabItem` rendering** reuses `FabMenu` from Shiny.Maui.Controls (479 LOC, already
  does expanding action menus) rather than reimplementing.

### Additive `INavigator` members

There is no way to programmatically switch tabs today. Add:

```csharp
Task SelectTab(string route);
Task SelectTab<TViewModel>();
```

`INavigator` is public and externally implementable, so ship these as **default interface
implementations** to keep the change non-breaking.

## 5. Drawer / flyout

```xml
<shiny:ShinyDrawer Placement="Right"              <!-- Left | Right | Top | Bottom -->
                   PresentationMode="Overlay"     <!-- Overlay | Push | Reveal | Squeeze -->
                   Size="0.8*"                    <!-- absolute or fraction -->
                   ScrimBrush="#80000000"
                   EdgeSwipeEnabled="True" EdgeSwipeWidth="20"
                   OpenAnimation="{StaticResource DrawerIn}"
                   ItemTemplate="{StaticResource FlyoutItemTpl}"
                   HeaderTemplate="..." FooterTemplate="..." />
```

- Mirrors `FlyoutItem` / `MenuItem` / `Shell.ItemTemplate`, so existing flyout markup keeps
  working.
- Activated by `FlyoutBehavior="Disabled"` plus the existing
  `DisableShellFlyoutSwipeHandler` to kill the native edge gesture.
- Built on `FloatingPanel`'s pan/detent/animation engine (688 LOC) — but that engine is
  currently `Bottom | BottomTabs | Top` only. Left/Right come from the upstream work in §7.
- Drawer and a bottom tab bar can occupy the same edge → the host needs a **layer-priority**
  model: dialogs/toasts above drawer above tab bar.

### `IShellChrome` service

Singleton for ViewModels, registered alongside `INavigator`:

```csharp
Task OpenDrawer();  Task CloseDrawer();  Task ToggleDrawer();
Task ShowTabBar();  Task HideTabBar();
Task SelectTab(string route);
```

Plus XAML attached properties matching the existing `Navigate.*` style.

## 6. Escape hatch

Managed chrome will never be complete. Cheap insurance (~40 LOC):

```csharp
builder.UseShinyShell(x => x.UseChrome(c => c.Customize(
    ios:     tabBarController => { /* UITabBarController */ },
    android: bottomNav        => { /* BottomNavigationView */ },
    windows: navView          => { /* NavigationView */ })));
```

A per-platform callback handed the live native object once attached, for the
"I need this one native trick" case.

## 7. Upstream work in `Shiny.Maui.Controls`

Do these in `~/Desktop/dev/controls` first; Chrome pins a minimum Controls version.

1. **`FloatingPanelPosition` += `Left`, `Right`.** Generalize `FloatingPanel`'s axis
   handling — the `IsBottom` boolean gate becomes an orientation concept. Additive enum
   values, non-breaking. Benefits both repos.
2. **Extract a shared host.** `ToastManager`'s leaf-page walk + `Content` re-wrap becomes a
   public, reference-counted `WindowChromeHost` that Toast, `FloatingPanel`, and Chrome all
   share. Without this, Toast and Chrome fight over `page.Content` (§3).
3. **Android safe-area insets.** `ToastManager`'s inset helpers are iOS-only and return `0`
   on Android — wrong under edge-to-edge (API 35+). Move to MAUI 10
   `SafeAreaEdges`/`SafeAreaRegions`.
4. **`FabMenu` anchoring.** Verify it can anchor to an arbitrary point (a center tab)
   rather than only a screen corner; add `Anchor`/`Placement` if not.
5. **Version floor** — bump `Shiny.Maui.Controls` in `Directory.packages.props`
   (currently `1.0.1-beta-0127`) and note the cross-repo release ordering.

## 8. Phases

| Phase | Deliverable |
|:--|:--|
| **0. Spike** | Both hosts, throwaway, iOS + Android. Answer the five questions in §3 and record the decision here. |
| **1. Upstream** | The five Controls changes in §7, released as a Controls beta. |
| **2. Foundation** | `src/Shiny.Maui.Shell.Chrome` project (4 TFMs), `ShellChromeState`, `IChromeHost` + page host, `IShellChrome`, `INavigator.SelectTab` DIMs, badge re-routing. Unit tests on the state POCO. |
| **3. Tab bar** | Item model, templates, auto-mirror, tap interception, visibility policy, `MenuTabItem` via `FabMenu`. Window-overlay host lands here *if* Phase 0 says so. |
| **4. Drawer** | Four placements, four presentation modes, edge-swipe, scrim, Android predictive-back integration. |
| **5. Escape hatches** | Per-platform `Customize` callbacks. |
| **6. Docs — required** | `readme.md`; `skills/shiny-maui-shell/SKILL.md` incl. trigger keywords for every new type/attribute; new `chrome.mdx` in `~/Desktop/dev/documentation` + `src/sidebar-topics.mjs` entry; release note under a `### TBD` heading. |
| **7. Optional** | Source-gen: validate `NavigationTabItem.Route` against generated `Routes` (new `SHINY002`), generate typed `SelectTabHome()`. Custom nav bar / header as a third chrome part. |

Sample app: **one** `ChromeDemoShell` proving center-FAB + right drawer. Not a gallery.

## 9. Risks

| Risk | Mitigation |
|:--|:--|
| **Accessibility** — a hand-drawn bar loses VoiceOver/TalkBack tab semantics that the native bar gives free | `SemanticProperties` + explicit traits on every built-in template. Acceptance criterion for Phase 3, not a follow-up. |
| Toast/Chrome `page.Content` collision | shared upstream host (§7.2) |
| Android predictive back vs. open drawer | register an `OnBackPressedDispatcher` callback from the drawer |
| iOS modal pages cover the overlay | correct behaviour — document it |
| Safe areas, keyboard, rotation, desktop window resize | insets fed into `ShellChromeState`; per-platform test matrix |
| MAUI version drift | public bindable APIs only; no renderer subclassing |
| AOT / trimming | `DataTemplate` + item types need `[DynamicallyAccessedMembers]`, matching the existing `ShinyAppBuilder` pattern |
| Cross-repo release coupling | Chrome pins a Controls minimum; Controls beta ships before Chrome Phase 2 |
