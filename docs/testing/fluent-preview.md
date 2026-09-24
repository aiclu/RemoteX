# Fluent main-window preview

## Run locally

Build Debug to a separate directory:

```powershell
dotnet build Ui/Ui.csproj -c Debug --no-restore -p:OutDir=D:/Projects/RemoteX/Ui/bin/FluentPreviewTest/
```

Exit the existing RemoteX instance before opening the test executable. In the
main window menu, enable **Fluent preview**. Use **Appearance** at the bottom of
the sidebar to choose Light, Dark, Follow system, or return to Classic.
At narrow widths the sidebar labels collapse; tooltips identify the icons.

Classic colors and its saved view are not overwritten. Preview settings are
stored independently in Theme.Fluent. Existing configurations default to Classic.
The second phase extends the local Fluent scope to settings, the connection
editor and About. Session hosts, launcher windows and editor auxiliary dialogs
retain their existing implementation.
Preview is implemented with local WPF resources, not a new control framework.

## Verification in this change

- Debug build succeeds.
- 16 focused preference tests cover default values, unknown modes, system
  light/dark resolution and independent classic/preview view preferences.
- An isolated WPF rendering fixture uses the actual FluentWorkspace,
  ServerListPageView and FluentConnectionRow controls with synthetic data.
  At widths 1100 and 600 DIP, only the visible subset of 10,000 rows is realized.
  This checks initial virtualization, not interactive scroll/drag performance.
- Light/dark images at 100%, 150% and 200% render scale are produced under
  Ui/bin/FluentPreviewTest/screenshots. These are simulated-data renderings,
  not screenshots of a user's remote connections or a real multi-monitor test.
- The existing full test command is blocked by NETSDK1005: Tests/obj assets
  do not contain the project's net6.0-windows10.0.17763.0 target.

## Manual regression still required

- Toggle Classic/Fluent repeatedly; restart and verify preferences persist.
- Change the Windows light/dark setting while Follow system is active; test
  high contrast and moving the real window between monitors at different DPI.
- Verify Ctrl+F, Tab navigation, Enter/Space activation, menu focus and Escape.
- Connect/edit/create, multi-select, drag ordering, tag include/exclude and
  context menus; switch list/card/tree and return from Settings and About.
- Scroll a large grouped list; verify recycling does not retain selection
  visuals from a previous row. Check long names, larger user fonts and
  narrow/short windows.

No version bump, commit, push or release is part of this preview.

## Phase two: settings, connection editor and About (2026-09-21)

Build and launch the independent test copy (close the previous Debug instance
first so the single-instance launcher does not redirect to an older binary):

```powershell
dotnet build Ui/Ui.csproj -c Debug --no-restore -p:OutDir=D:/Projects/RemoteX/Ui/bin/FluentPagesTest/
& ./Ui/bin/FluentPagesTest/RemoteX.exe
```

Select **Settings > Theme > Appearance > Fluent preview**. Classic palettes and
view preferences remain independent. The shared appearance service updates only
opted-in resource scopes, including already open About windows. It subscribes
to Windows changes while there are subscribers and is disposed on app exit.
Settings navigation becomes a dropdown below 720 DIP. Form rows stack without
replacing their original controls or bindings; returning to wide/classic restores
their original grid columns. Save/cancel and About close remain outside scrolling
content. Data tables retain horizontal scrolling rather than hiding columns.

Reproducible checks, against that Debug output:

```powershell
dotnet test tools/FluentPreviewTests/Tests.csproj
dotnet run --project tools/FluentPreview/Render.csproj
```

These small projects use existing MSTest packages from the local cache (their
NuGet sources are intentionally empty). They do not replace the full test suite.
The render fixture bypasses application bootstrap and uses empty/synthetic data;
it does not exercise saving, connect commands, password reveal, or the updater.

Results:

- Debug build: successful, 0 errors; existing nullable/dependency warnings remain.
- Focused tests: 22 passed (16 preferences + 6 palette/layout/lifetime cases).
- Settings, editor, About, all settings subviews and protocol forms load and
  render in light/dark at 1000 and 600 DIP. PNGs use 100%, 150%, 200% render
  scales; settings/editor also have 18-DIP body-font samples. This is **not** a
  real monitor DPI test, nor a full interactive regression.
- Screenshots: `Ui/bin/FluentPagesTest/screenshots/`, e.g.
  `Settings-Light-1000-100.png`, `Settings-Dark-600-100.png`,
  `Editor-Dark-1000-100.png`, `About-Light-480-100.png`.
- `ssh_rust.dll` exists in the test output and successfully loads with
  `NativeLibrary.Load`; no dependency download was required.
- Full `dotnet test Tests/Tests.csproj --no-restore` remains blocked by
  `NETSDK1005` (missing net6 Windows target in the existing assets file).

Still requires a real-window check: Windows theme/high-contrast changes,
multi-monitor DPI, keyboard navigation and dropdowns, settings persistence,
all protocols' create/edit/save/cancel and validation, credential operations,
About links/update status transitions, and actual scroll-to-bottom behavior.
No real update, external link opening, credential changes or remote connection
was performed during validation. Installed version stays 1.0.16; no commit,
push, tag or release was made.

## Phase 3 — launcher and common dialogs

Local Debug output: `Ui/bin/FluentDialogsTest/RemoteX.exe` (version 1.0.16).
The fixture and focused-test projects now reference this output:

```powershell
dotnet build Ui/Ui.csproj -c Debug --no-restore -p:OutDir=D:/Projects/RemoteX/Ui/bin/FluentDialogsTest/
dotnet test tools/FluentPreviewTests/Tests.csproj --no-restore
dotnet run --project tools/FluentPreview/Render.csproj --no-restore
```

The launcher uses font-aware search/result/action heights and caps Fluent lists
at eight results, with scrolling thereafter. Classic sizing remains separate.
Message/confirmation, input, password, icon, alternate credential, argument,
data-source selection/configuration and custom connection-info surfaces opt into
local styles. Dialog body scrolling is separate from existing action buttons.
Narrow field grids preserve and restore row/column definitions and bindings.
Native RDP connection information, connection logic and updater are unchanged.

Validation on 2026-09-21:

- Debug build: passed (112 warnings, zero errors).
- Focused tests: 34 passed, including dimensions, eight-row cap, large fonts,
  work-area constraints, palette/high-contrast resolution, scope disposal and
  reversible field layout/binding preservation.
- `ssh_rust.dll` exists in the output and loads successfully with NativeLibrary.
- Isolated XAML fixtures render light/dark dialog and quick-connect samples at
  100%, 150%, 200%; files are under `Ui/bin/FluentDialogsTest/screenshots`.
  Narrow fixtures use 360-DIP dialogs and 280-DIP quick connect, with 18-DIP
  fonts. Connection-info values are explicitly synthetic, not session metrics.
- Full legacy test project still fails with NETSDK1005: its existing assets
  lack the net6.0-windows10.0.17763.0 target. No complete-suite pass is claimed.

Rendering is detached from native windows: bitmap scaling is not a real
per-monitor DPI test. Actual window placement, system theme/high-contrast changes,
launcher hotkeys/arrows/Enter/Escape/focus hiding, action/notes interactions,
quick-connect parsing and dialog default/cancel/validation behavior still require
interactive verification. No real connection, credential modification, database
write or upgrade was executed. Classic visual regression and native modal/Owner
behavior also remain manual checks. No version change, commit, push, tag or release.

## Card and tree continuation — 2026-09-24

The existing card and tree views now opt into the shared appearance scope.
Classic card dimensions and tree templates are retained and restored on mode
switch. Fluent cards show an address/subtitle tooltip, persistent selection and
menu affordances, a solid rounded surface and focus/hover outlines. Their cells
retain the existing virtualizing wrap panel. Fluent tree headers have 40-DIP
minimum height, rounded hover/selection surfaces, a selection bar, focus outline
and bound expand/collapse buttons. Existing drag/drop, commands, notes and
context menus are retained; no protocol or session-host code changed.

The local output remains `Ui/bin/FluentDialogsTest/RemoteX.exe`, version 1.0.16.
Debug build passed (112 warnings, zero errors). Focused tests: 37 passed, including
three new card sizing/classic restoration and tree-template state tests.
The renderer now also produces `Cards-*` and `Tree-*` light/dark samples using
synthetic data, at normal/large fonts and 100%/150%/200% bitmap scales.
These are layout checks, not evidence of native mouse/keyboard, menu, drag/drop
or real multi-monitor DPI correctness. Large live-list virtualization/performance
and actual classic-mode switching remain to be exercised interactively.
No connection, database write, version bump, commit, push or release was performed.

## Remaining application surfaces — 2026-09-24

Session tabs now use solid rounded headers, a selected underline, focus outlines
and larger reconnect/close hit areas. Window action buttons and tab context menus
use the same local Fluent tokens. Dragablz's PART_Thumb, drag/dock behavior, all
commands, the 30-DIP title-bar height, content border thickness and maximized
8-DIP margins remain unchanged. No session-host/rendering code was modified.

`FluentSessionAppearance` applies only new palette keys at window level; it does
not alias legacy resources or enable Fluent form styles inside protocol content.
Header/button/menu subtrees opt in separately. Hidden/unloaded scopes detach
their appearance listeners. Fullscreen changes are limited to the application's
window background: the floating RDP connection bar belongs to ActiveX, not to a
RemoteX toolbar, and is deliberately unchanged.

The remaining startup guidance, error-report, alternate-address and breaking-change
alert windows also use local Fluent palettes/button styles. Their existing
workflows and layouts are retained; this is not an updater fix. Early error UI
falls back to classic appearance if the service/container is unavailable.

Latest checks: Debug build passed (112 warnings, zero errors); 38 focused tests
passed, including session resource isolation, classic restoration, subscription
cleanup and missing-service fallback. `ssh_rust.dll` loads successfully. Actual
header styles and window-button XAML are rendered with synthetic data as
`SessionHeaders-*` and `SessionWindowButtons-*` in the existing screenshot folder.
No real TabWindowView/protocol host is constructed by the renderer.

Still unverified: live drag-out/docking, reconnect/close, native fullscreen return,
multi-monitor DPI, keyboard input routing to remote sessions, system high contrast,
and interactive flows in the four low-frequency windows. The complete legacy test
suite remains blocked by NETSDK1005 (missing net6 Windows restore target).
System file pickers and the native RDP connection-info/connection-bar windows
remain system-rendered. Version stays 1.0.16; no commit, push, tag or release.

## Main-window menu deduplication

Fluent mode collapses the complete legacy menu anchor (including its old update
dot) on connection, settings and editor pages. Collapsing the anchor closes the
popup and clears the toggle using SetCurrentValue, retaining its binding and the
classic menu instance. Classic mode restores the 45-DIP anchor. Settings Back and
editor Save/Cancel remain the navigation routes back to the connection page.

Exit is available at the end of Fluent More, using the existing CmdExit; ID sort
uses the existing CmdReOrder/IdAsc path. Other sort toggle semantics are unchanged.
The About icon retains its update badge even when sidebar text is collapsed;
tooltip and accessibility help expose the existing NewVersion string. No update
checks, download actions or shutdown commands were invoked for validation.

Debug build passed (112 warnings, no errors); focused suite: 41 passed, including
the actual menu-style collapse/restore measurement, popup-binding preservation
and exit/ID command wiring. Isolated About samples verify no-update/update states
in compact/expanded sidebars and both palettes at 100%, 150%, 200% bitmap scales
(`MenuAboutCompact-*`, `MenuAboutExpanded-*`). Real keyboard focus traversal and
opening the classic popup again after switching still require interactive checks.
The full legacy-suite NETSDK1005 blocker is unchanged. Output is still
`Ui/bin/FluentDialogsTest/RemoteX.exe`; version stays 1.0.16, with no commit or release.
