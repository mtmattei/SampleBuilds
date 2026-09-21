# Morning Drive Card: Uno Platform Spec

Status: ready to build, 2026-09-21. The ten original questions are resolved (see Resolved Decisions). Three small questions remain at the bottom; none blocks steps 1 to 13.
Verification: checked on 2026-09-21 against the Uno docs MCP, the `uno-platform` skill set (MVUX, Navigation, Extensions, Toolkit, Material and Skia skills), the Uno.Sdk 6.7.30 targets and build schema, and the Uno.Extensions 7.3.6 and Toolkit 9.1.3 assemblies in the local NuGet cache. Doc paths cited inline are Uno docs `sourcePath` values.
Source of truth for the design: `../MorningCard/morning-card.html` and its README (commit `97ef369`).
Targets: `net10.0-android` and `net10.0-desktop`.

The build happens in a fresh session rooted in this folder (so the project `.mcp.json` registers the `uno-app` server). That session reads this file cold.

## Goal

Port the Morning Drive Card from a single HTML file to an Uno Platform app that uses platform controls, MVUX, DI-registered services and Material theming, and that runs on Android and desktop with the same states, numbers and motion as the prototype.

The card answers one question at a glance: when do I leave for school drop-off? Fuel range, cabin temperature, tires and the car status support that answer.

## Capability Inventory

| # | Capability the design implies | Status | Note |
|---|---|---|---|
| 1 | Route time ruler (slack, drive, walk laid along a path) | implemented | `SKCanvasElement` with `SKPathMeasure`. This is the signature device and it is a schematic: minutes become distance along the path. |
| 2 | Map (ground under the route, school pin) | implemented on the Trip page, **schematic on the card (decided, 1a)** | The card ground stays a drawn schematic because the slack stretch has no geographic meaning (the car is parked during slack). The real, pannable map lives on the Trip page: Mapsui `MapControl`, OSM tiles, route line, home and school pins. |
| 3 | Live trip data (drive time, traffic delay, usual time) | **substituted (decided)** | No routing endpoint exists. `ITripService` contract plus a simulated implementation with latency and failure injection. |
| 4 | Live vehicle telemetry (range, cabin, tires, lock, plug, last seen) | **substituted (decided)** | No vehicle API credentials exist. `IVehicleService` contract plus the same simulated hub. |
| 5 | Remote commands (lock, climate, send destination to car) | **substituted (decided)** | Commands go to the simulated hub. The tuning panel is titled "Simulator" so the surface stays honest. |
| 6 | Clock-driven recompute (slack shrinks as time passes) | implemented | `TimeProvider` plus a minute-aligned tick feed. The prototype only moved time through a slider. |
| 7 | Loading, empty, offline states | implemented | `FeedView` templates plus visual states. Built with the `uno-component-states` process: populated card first, then transformed. |
| 8 | Light and dark theme, runtime toggle | implemented | Material theme, `ThemeService`, token dictionaries with Light and Dark entries. |
| 9 | Dialable accent hue, corner radius, motion speed | implemented | Debug tuning surface writes brush colors, a radius resource and a motion multiplier. |
| 10 | Frosted glass card over a blurred backdrop | implemented, technique pending spike R2 | First choice is the platform's Skia-native `AcrylicBrush`, then a `SaveLayer` backdrop filter, with a translucent fill over the pre-blurred bokeh as the floor. |
| 11 | Navigation from the title chevron and the tires chevron | title implemented (opens the Trip page), **tires omitted (decided, logic later)** | The tires cell keeps its chevron and does nothing. Listed in the README's known gaps. |
| 12 | "More" action | **omitted (decided, logic later)** | Keeps its look, does nothing. Listed in the README's known gaps. |
| 13 | Charging state | implemented | `IsPluggedIn` and `ChargePercent` on the snapshot, driven from the Simulator. |
| 14 | Localization and units | implemented for English, **French and a units switch omitted (decided)** | `Localization` UnoFeature, `.resw`, `x:Uid` from day one, `en` only. Units stay miles, °F and psi. |
| 15 | Reduced motion | implemented on Android, **substituted on desktop (accepted)** | `UISettings.AnimationsEnabled` is hardcoded `true` on Skia desktop, so a Reduced motion switch in the Simulator stands in for the OS setting there. |
| 16 | Accessibility (pressed state, names, focus, live region) | implemented on desktop, **at risk on Android (accepted)** | `ToggleButton`, `AutomationProperties`, focus visuals. Upstream marks Android Skia TalkBack as WIP; one device pass, result recorded. |

## Architecture Brief

### Decisions

```
Decision: MVUX for the page model.
Reason: the card is fed by two async services, has loading, none and error states, and pushes
        live updates (clock ticks, telemetry). FeedView owns those states; commands generate
        from model methods. That is the MVUX default case in uno-scaffolding.md.
Tradeoff: bindings inside FeedView templates use {Binding Data.X} and {Binding Parent.X},
          with no x:Bind compile-time checking on the data surface.
```

```
Decision: the route ruler and map ground are one SKCanvasElement.
Reason: three needs have no XAML or Composition equivalent: point-at-length along the path
        (node, dot, pill and label positions), label text with a halo stroke, and about forty
        slack dots, nodes and labels on one entrance timeline. SKPathMeasure covers positions
        and partial segments in one place. SKCanvasElement is supported on desktop and on
        net10.0-android with the SkiaRenderer feature (controls/SKCanvasElement.md).
Alternative considered: a hybrid. Partial segments alone are doable without Skia:
        CompositionGeometry.TrimStart/TrimEnd is implemented on Skia (SKPathEffect.CreateTrim)
        and Path.StrokeDashOffset is implemented too. The hybrid still needs SKPathMeasure for
        every position and gives up the halo, so it splits one drawing across two systems.
Tradeoff: labels drawn in Skia are invisible to the automation tree, so the element carries one
          composed AutomationProperties.Name sentence. XAML Opacity does not affect the
          element, so alpha is baked into paints.
```

```
Decision: everything else on the card is XAML controls.
Reason: header, ETA block, stats and actions are text, buttons and a progress bar. They need
        theming, focus, automation peers and localization, which controls provide for free.
Tradeoff: two rendering systems share one palette, so the palette is snapshotted into the Skia
          renderer on theme change and on hue change.
```

```
Decision: offline is a value, not a feed error.
Reason: the design shows stale data with "Last seen 6:58", dimmed stats and a Retry link.
        FeedView.ErrorTemplate replaces the content and its DataContext is the Exception,
        so stale data could not render there. VehicleSnapshot carries IsStale and LastSeen.
Tradeoff: a true failure with no cached snapshot still needs an ErrorTemplate (card-shaped,
          with Retry). Two paths to test.
```

```
Decision: the dials never touch the card. They write to the simulated hub; the card reads feeds.
Reason: same contract as the prototype ("the card never reads from the dials"). Swapping the
        hub for real services later changes registrations only.
Tradeoff: a slider drag travels hub -> feed -> FeedView, a longer path than a direct binding.
          Verify drag latency stays under one frame budget on Android.
```

```
Decision: the card shadow is drawn by a Skia element, with the card's rounded rect clipped out.
Reason: ShadowContainer was the first choice and fails this design by construction. Toolkit
        9.1.3 PaintInner draws each drop shadow as a full blurred round rect, clips to the
        content, then paints the background over it. Under a 74 percent alpha glass the
        0 30 60 -30 layer darkens the lower card interior. CSS box-shadow never paints under
        the box. CardShadowCanvas draws both layers and clips with SKClipOperation.Difference.
        Its colors come from the same palette snapshot as the ruler, which also sidesteps
        ThemeResource on utu:Shadow (a DependencyObject) for the per-theme shadow colors.
Tradeoff: one more SKCanvasElement, sized 60 epx beyond the card on every side because
          drawing is clipped to the element area. ShadowContainer stays the choice for any
          opaque surface added later.
```

```
Decision: the card container is a Border, with the glass as a childless sibling Border behind
          the content.
Reason: the toolkit-card skill prescribes CardContentControl for card surfaces. This surface is
        non-interactive (no state layer, no ripple), ElevatedCard would add a second shadow,
        and the documented Skia AcrylicBrush path requires a childless element on Android.
Tradeoff: no Material card semantics. If the card ever becomes tappable as a whole, move to
          FilledCardContentControlStyle with a transparent FilledCardContentBackground.
```

```
Decision: the accent hue dial mutates the app's Accent SolidColorBrush instances in place.
Reason: the design defines accent as hsl(h 82% 52%). SemanticThemeHelper.PrimarySeed derives
        HCT tones that do not equal those values, and an OverrideSource entry freezes Primary
        against the seed anyway.
Tradeoff: Material controls in the tuning panel (Slider, Chip) read Primary brushes, so their
          lightweight-styling keys point at the app AccentBrush resources to follow the dial.
```

### Solution structure

Mirror `C:\Users\Platform006\UnoProjects\DYT` (same TFMs, Uno.Sdk 6.7.30, a Core library, xunit tests).

```
SampleBuilds/MorningCard.Uno/
  SPEC.md
  .mcp.json                     copied from ~/.claude/templates/uno/.mcp.json before first build
  global.json                   Uno.Sdk 6.7.30
  MorningCard.sln
  MorningCard/                  app head, net10.0-android;net10.0-desktop
    App.xaml(.cs)               host builder, routes, theme
    appsettings.json            embedded; LocalizationConfiguration cultures
    Presentation/
      MainPage.xaml             stage: backdrop, card, tuning region
      MainModel.cs              partial record, MVUX
      TuningPanel.xaml          UserControl view, nested default route ("Simulator")
      TuningModel.cs            partial record, MVUX, dials
      TripPage.xaml             NavigationBar, Mapsui MapControl, trip summary strip
      TripModel.cs              partial record, MVUX
    Controls/
      MorningCardView.xaml      UserControl, the card
      RouteRulerCanvas.cs       SKCanvasElement + DPs
      RouteRulerRenderer.cs     pure drawing, no XAML types
      CardShadowCanvas.cs       SKCanvasElement, two shadow layers, card rect clipped out
      BokehBackdrop.cs          SKCanvasElement, four blurred blobs
    Motion/                     entrance timeline, easing, reduced-motion probe
    Themes/
      ColorPaletteOverride.xaml Material role overrides (Dsp feature stays off)
      CardTokens.xaml           app tokens with Light and Dark theme dictionaries
      FontOverride.xaml         MaterialLight/Regular/MediumFontFamily -> Instrument Sans
      Fonts.xaml                app FontFamily resources (manifest first, per-weight fallback, R10)
      Styles/                   RoundActionButton, StatCell, TuningChip, LinkButton
    Strings/en/Resources.resw   English only in this version
    Assets/Fonts/               Instrument Sans static TTFs 400/500/600/700
  MorningCard.Core/             net10.0, no Uno references
    Models/                     TripPlan, VehicleSnapshot, TripTimeline, RouteFractions, enums
    TripMath.cs                 port of derive() and the level rules
    RouteLayout.cs              port of the fraction math from drawRoute()
    Services/                   ITripService, IVehicleService, SimulatedDriveHub
  MorningCard.Tests/            xunit 2.9.2, targets Core
```

UserControl for the card: one consumer, fixed composition. A templated control adds a generic.xaml and template parts nobody needs yet.

### Pinned versions

| Item | Version | Source |
|---|---|---|
| .NET SDK | 10.0.303 | installed |
| Uno.Sdk | 6.7.30 (latest stable on NuGet, 2026-09-21) | DYT `global.json` |
| Uno.Extensions | 7.3.6, implicit from the SDK | local NuGet cache for 6.7.30 |
| Uno.Toolkit.Skia.WinUI | 9.1.3, implicit | the SDK references it when `Toolkit` and `Skia` are both in UnoFeatures (`Uno.Implicit.Packages.ProjectSystem.targets`); no explicit PackageReference. DYT needs one only because it omits `Skia`. |
| SkiaSharp | bundled 3.119.x | no v4 feature is needed; do not opt in |
| xunit / runner / Test.Sdk | 2.9.2 / 2.8.2 / 17.12.0 | DYT tests |
| Microsoft.Extensions.TimeProvider.Testing | latest stable at scaffold time | `FakeTimeProvider` for the clock tests |
| Mapsui.Uno.WinUI | 5.1.0 | NuGet. Dependencies align with the SDK on paper; runtime on both heads is spike R1. Added in step 12, so a failed spike cannot block the card. |

UnoFeatures, every name verified against `uno.sdk/6.7.30/Sdk/Sdk.props.buildschema.json`:
`Material; Toolkit; Extensions; Hosting; Configuration; Localization; Navigation; MVUX; Logging; ThemeService; SkiaRenderer; Skia`.
`SkiaRenderer` alone brings `Uno.WinUI.Graphics2DSK` (`SKCanvasElement`) on both heads; `Skia` stays for the implicit Toolkit Skia package. `Extensions` already covers Hosting, Configuration and Logging; the explicit names stay because they are harmless and self-documenting. `Http` stays out while the services are simulated; it arrives with the first real endpoint.

Scaffold command (dry run passed, creates Android and desktop only):

```
dotnet new unoapp -o MorningCard -preset recommended -platforms android desktop -presentation mvux -theme material -theme-service -renderer skia -tfm net10.0 -http none -dsp false -tests none
```

- The installed `Uno.Templates` is 7.0.0-dev.6 and may emit a 7.0-dev `global.json`. Install the templates matching Uno.Sdk 6.7, or diff the generated project against DYT, then pin 6.7.30.
- `-dsp false` keeps `ColorPaletteOverride.xaml` hand-authored. With Dsp on, the file is regenerated from json on every build.
- Set `SupportedOSPlatformVersion` for Android to 24.0 (the SDK default is 21.0; DYT uses 24.0).
- Follow whichever Material wiring the scaffold emits. Themes 7.1.1 prefers `<utum:MaterialToolkitTheme.Colors><ut:ThemeColors OverrideSource="..."/></utum:MaterialToolkitTheme.Colors>`; `ColorOverrideSource` is deprecated and still works. `FontOverrideSource` points at `Themes/FontOverride.xaml`.
- If Android `Release` restore fails on `Microsoft.NETCore.App.Runtime.Mono`, run `dotnet workload update`.

### State model

Core records (immutable, `partial` where they carry an `Id`):

```csharp
public enum TrafficLevel { Light, Moderate, Heavy }       // delay <= 1, <= 6, else
public enum CardTone { Ok, LeaveNow, Late }               // slack >= 2, 0..1, < 0
public enum StatLevel { Normal, Warn, Bad }

public record TripPlan(string Title, TimeOnly Bell, int BaseDriveMin, int UsualDriveMin,
                       int DelayMin, int WalkMin);

public record VehicleSnapshot(string Name, bool IsLocked, bool ClimateOn, int CabinTemp,
                              int Range, int RangeMax, int TireFrontLeftPsi,
                              bool IsPluggedIn, int ChargePercent,
                              bool IsStale, TimeOnly LastSeen);

public record GeoPoint(double Longitude, double Latitude);
public record TripRoute(GeoPoint Home, GeoPoint School, IReadOnlyList<GeoPoint> Line);  // sample data

public record TripTimeline(int DriveMin, TimeOnly LeaveBy, int SlackMin, bool IsLate,
                           TimeOnly Start, TimeOnly Arrive, int TotalMin, CardTone Tone,
                           TrafficLevel Traffic, int VsUsualMin);

public record RouteFractions(double Leave, double Arrive, int SlackVisibleMin);
```

`TripMath.Derive(TripPlan, TimeOnly now)` is a line-for-line port of `derive()`:
`drive = base + delay; leaveBy = bell - walk - drive; slack = leaveBy - now; late = slack < 0; start = late ? now : leaveBy; arrive = start + drive`.
`RouteLayout.Fractions` ports `slackVis = late ? 0 : min(slack, 12); T = slackVis + drive + walk; fLeave = slackVis / T; fArrive = (slackVis + drive) / T`.
Thresholds: range warn < 70, bad < 30, bar width `max(2, 100 * range / rangeMax)` percent; tire warn < 30 ("FL low"), bad < 26 ("FL {n} psi").

`MainModel` (partial record, services by constructor):

| Member | Type | Source |
|---|---|---|
| `Now` | `IFeed<TimeOnly>` | `Feed.AsyncEnumerable(hub.WatchClock)`. One source: the hub merges the minute-aligned `TimeProvider` tick with the Clock dial override, because a feed cannot be reassigned |
| `Trip` | `IFeed<TripPlan>` | `ITripService.Watch()`; None means no drop-off today |
| `Vehicle` | `IFeed<VehicleSnapshot>` | `IVehicleService.Watch()` |
| `Timeline` | `IFeed<TripTimeline>` | `Feed.Combine(Trip, Now)` projected through `TripMath.Derive` |
| `ToggleLock`, `ToggleClimate`, `SendToCar`, `Retry` | generated commands | call the services; read current values with `await Vehicle` inside the method, never through `CommandParameter` from a FeedView template |

`SendToCar` awaits the service, sets a `SendConfirmed` `IState<bool>` to true and returns. The 1400 ms reset runs outside the command (a timer owned by the model), because a generated command stays in its executing state, and its button disabled, for as long as the method runs.

`Feed.Combine` fast-starts to None when a parent is None (`CombineFeedHelper.fastStartForNone`), so `Timeline` is None on a no-trip day while `Vehicle`, a separate feed, stays live. A null from `Watch()` is the documented None signal. Confirm the `NoneTemplate` path in spike R9.

Every `Watch(CancellationToken)` iterator marks its token `[EnumeratorCancellation]`; MVUX cancels it when the last subscription ends.

`TuningModel` holds `IState<T>` per dial, two-way bound with `{Binding X, Mode=TwoWay}`, each forwarding to the hub or to the theme tokens through `State.Value(this, ...).ForEach(...)`. `IState<int>` and `IState<double>` only; no nullable value types.

```
Decision: the tuning panel is its own view (TuningPanel UserControl + TuningModel), hosted in a
          ContentControl region on MainPage through a nested default route.
Reason: ViewMap<TView, TModel> gives one model per view, and a TuningModel exposed as a plain
        property of MainModel would hand XAML raw IState objects with no bindable proxy. A region
        keeps construction in DI and keeps simulator state out of the card's model. If the panel
        ever becomes DEBUG-only, the route registration is the single switch.
Tradeoff: one more route and a region for a panel that never navigates. Fallback if the region
          misbehaves: DataContext = ActivatorUtilities.CreateInstance<TuningViewModel>(services),
          the DYT convention.
```

`MorningCardView` inherits the page DataContext (`MainViewModel`) and has no model of its own.

Known MVUX traps to respect (from `uno-scaffolding.md`): no feed or state named `Model`; records with `required` members or `record struct` values break bindable generation; a Retry button inside `ErrorTemplate` binds with `ElementName` to the named `FeedView`; equal records are deduped, so every hub push must differ in value.

### Navigation model

`Uno.Extensions.Navigation`, region-based, routes registered centrally in `App.xaml.cs`. No code-behind navigation. Three routes: Main (default), its nested Tuning region, and Trip.

The scaffold (step 1, done) is shell-less: the generated `App.xaml.cs` calls `MainWindow.InitializeNavigationAsync(() => Task.FromResult(builder.Build()), initialRoute: "Main")` and registers flat routes. Existing project code wins for local conventions, so the app keeps that shape. There is no `Shell.xaml` and no `ShellModel`. The ViewMap names the model record and `ReactiveViewModelMappings.ViewModelMappings` bridges it to the generated ViewModel.

```csharp
views.Register(
    new ViewMap<MainPage, MainModel>(),
    new ViewMap<TuningPanel, TuningModel>(),
    new ViewMap<TripPage, TripModel>());

routes.Register(
    new RouteMap("Main", View: views.FindByViewModel<MainModel>(), IsDefault: true, Nested:
    [
        new RouteMap("Tuning", View: views.FindByViewModel<TuningModel>(), IsDefault: true)
    ]),
    new RouteMap("Trip", View: views.FindByViewModel<TripModel>()));
```

If forward and back navigation between two root routes misbehaves without a shell host (5 minute check in step 14), add the Shell from the DYT convention: `new ViewMap(ViewModel: typeof(ShellModel))`, a root `RouteMap("")` with these routes nested, and `builder.NavigateAsync<Shell>()`. `Shell` carries no `Region.Attached`.

The title button navigates in XAML with `uen:Navigation.Request="Trip"`. `TripPage` has a `utu:NavigationBar` whose back button returns to Main. `TripModel` reads the same `ITripService` and `Now` sources, so the page needs no navigation data. The tires cell and the More button carry no request and no command in this version.

Trip page (capability 2): Mapsui `MapControl` filling the page under the NavigationBar, `OpenStreetMap.CreateTileLayer(userAgent)`, the route as a `MemoryLayer` line in Accent (Red when late), home and school `PointFeature` pins, initial view fitted to the route extent with padding, pan and zoom enabled. A summary strip under the bar repeats leave-by, drive time and arrival from `TripTimeline`. `TripRoute` comes from `ITripService.GetRouteAsync` and is fixed sample data. Without a network the tiles stay blank and the route line still draws; the strip says "Map tiles unavailable offline." in that case.

Host builder chain: keep the generated order (`UseLogging`, `UseConfiguration(...EmbeddedSource<App>().Section<AppConfig>())`, `UseLocalization()`, `ConfigureServices`, `UseNavigation(ReactiveViewModelMappings.ViewModelMappings, RegisterRoutes)`). Configuration must stay ahead of `UseLocalization()`, because `LocalizationService` takes `IOptions<LocalizationConfiguration>` and `IWritableOptions<LocalizationSettings>`. The scaffold was generated with `-theme-service` and emits no `UseThemeSwitching()` call; add it before `UseNavigation` in step 5 and confirm `IThemeService` resolves (the documented registration call, Chefs `ThemeService.md`).

`appsettings.json` (embedded) declares `"LocalizationConfiguration": { "Cultures": [ "en" ] }`; the `Strings/` folder names match those entries exactly. Adding French later means a `Strings/fr` folder plus `"fr"` in that list (`fr` serves fr-CA devices through resource fallback), and a culture change applies on the next app start.

Theme: inject `IThemeService` (`Uno.Extensions.Toolkit`), toggle with `await SetThemeAsync(AppTheme.Dark)`. The service persists the choice, and the window root does not exist before `InitializeNavigationAsync` returns, so the `MC_THEME` hook applies after navigation and overrides the saved value. In `TuningModel`: `State.Value(this, () => _theme.Theme).ForEach(async (t, ct) => await _theme.SetThemeAsync(t))`.

### Services and data flow

```
TuningModel (dials) --> SimulatedDriveHub --> ITripService.Watch() / IVehicleService.Watch()
                                                   |
TimeProvider ----------> Now feed -----------------+--> MainModel feeds --> FeedView --> MorningCardView
                                                                                  |
MorningCardView commands --> MainModel methods --> IVehicleService ---------------+
```

- `ITripService`: `IAsyncEnumerable<TripPlan?> Watch(CancellationToken)`, `ValueTask<TripRoute> GetRouteAsync(CancellationToken)`, `ValueTask RefreshAsync(CancellationToken)`.
- `IVehicleService`: `IAsyncEnumerable<VehicleSnapshot> Watch(...)`, `SetLockedAsync`, `SetClimateAsync`, `SendDestinationAsync`, `RefreshAsync`.
- `SimulatedDriveHub`: singleton registered for both interfaces. Holds the knobs, a connection mode (Live, Loading, Offline, NoTrip), 400 to 800 ms command latency, and a failure switch so one error and one retry can be driven before the second surface is built.
- `TimeProvider.System` registered as a singleton; tests use `FakeTimeProvider`.
- Registrations use plain `AddSingleton` overloads. If `AddHttpClient<T>` arrives later, use the parameterless overload (the factory-lambda trap in `uno-scaffolding.md`).

### Platform constraints

- Both heads use the Skia renderer, so `SKCanvasElement` is supported on both. Keep the `IsSupportedOnCurrentPlatform()` guard with a plain fallback panel.
- Android: edge-to-edge is on by default (.NET 9+, SDK 35+) and enforced on Android 15+. `utu:SafeArea.Insets="Left,Top,Right,Bottom"` goes on the ScrollViewer content, so `BokehBackdrop` stays full-bleed. On the Page: `utu:StatusBar.Foreground="Auto"` (an enum: Light, Dark, Auto, AutoInverse) and `utu:StatusBar.Background="{ThemeResource GroundBrush}"`; both are no-ops on desktop.
- Android back: the gesture exits the single page. On Android 16+ `Handled` is ignored and any live `BackRequested` subscription consumes back presses (`features/hardware-back-button.md`); check on an API 36 image whether `Uno.Extensions.Navigation` holds one.
- Android accessibility: the docs status table marks "Android Skia TalkBack" as WIP (`features/accessibility/index.md`). Names and the live region are set on both heads and verified on desktop; TalkBack is tested on the device and recorded as a gap if it fails.
- Desktop: gate `MainWindow.UseStudio()` behind `APP_NO_HOTDESIGN`; never enter Hot Design with this app (the primary surface is an `SKCanvasElement`). Window size: `MainWindow.AppWindow.Resize(...)` before `NavigateAsync` is the documented route (`features/windows-ui-xaml-window.md`), and `uno-scaffolding.md` records intermittent clipped first frames with it. Do not resize; the 400 max width makes the card independent of window size, and captures pin content size instead.
- `CompositionEffectBrush` painted nothing on Skia desktop when measured on Uno.Sdk 6.6.42. The docs describe a Skia-native `AcrylicBrush` path that avoids it (R2). No other Composition blur.
- Reduced motion: `UISettings.AnimationsEnabled` is real on Android and hardcoded `true` on Skia desktop (`UISettings.cs`, release/stable/6.7). A Reduced motion switch in the tuning panel is the desktop source; on Android the two are OR-ed.
- Storyboards: only `Opacity` and a single Translate, Rotate, Scale or Composite transform are GPU-bound; no `TransformGroup`, one transform property per animation, no shared Transform or Animation resources (`features/working-with-animations.md`). Width changes are dependent animations.
- A held `CompositionTarget.Rendering` subscription costs 50 to 70 percent of a core. The entrance subscribes once and unsubscribes on completion; the skeleton pulse runs on a 30 fps `DispatcherTimer` and stops on state exit.
- Render thread rules: renderer reads plain snapshot fields; paints, paths, typefaces and `SKPathMeasure` are created once and reused; nothing allocates per frame.
- Fonts: static Instrument Sans TTFs (variable fonts are "not supported on Skia", `features/custom-fonts.md`), Content build action, `ms-appx:///Assets/Fonts/File.ttf#Internal Family Name`, internal names verified from the `name` table (reuse `UnoBusiness/scripts/05-fetch-fonts.ps1`). First choice is the documented `.ttf.manifest` font manifest that maps `FontWeight` to static files, so one `FontFamily` serves all four weights; fallback is one `FontFamily` resource per weight (R10). `FontOverride.xaml` supplies `MaterialLightFontFamily`, `MaterialRegularFontFamily` and `MaterialMediumFontFamily`, otherwise Slider, Chip and Button text stays Roboto. The same files load as `SKTypeface` for the canvas labels (R11).

### Testing and validation

- Unit tests (Core): `TripMath` for the default case (now 7:24, bell 7:50 gives slack 9, leave 7:33, arrive 7:47, total 17), slack 1 (LeaveNow), now 7:40 (Late, no slack fraction), slack cap 12, traffic boundaries 1/2 and 6/7, vs-usual faster, slower and equal, range and tire thresholds, bar floor of 2 percent. `RouteLayout` fractions sum and ordering.
- Runtime: `uno-verify` loop on desktop with the App MCP; Android smoke on day one.
- DEBUG env hooks pin the start state without a pointer: `MC_STATE` (live, loading, offline, empty), `MC_CLOCK` (minutes since midnight), `MC_THEME`, `MC_DELAY`.
- Parity: capture each state at 390 epx wide in light and dark and compare against headless captures of the HTML at the same state.
- Two widths (390, 1280) and two heights (844, 600).

## Design Brief

### Visual direction

Frosted glass card over an out-of-focus morning backdrop. Blue-biased ink, one route accent with a dialable hue, semantic green, amber and red kept apart from the accent. One type family.

### Tokens

`CardTokens.xaml`, Light and Dark theme dictionaries, consumed with `ThemeResource` on standard properties. No hex in pages or controls.

| Token | Light | Dark |
|---|---|---|
| Ground | #D9D6D0 | #10151C |
| BlobA / B / C / D | #D9B37A / #A9C4DD / #8F9AA6 / #F1E6D2 | #5A4322 / #23405A / #2C3540 / #3A3126 |
| Glass | white 74% | #181E26 72% |
| GlassEdge | white 70% | white 14% |
| Glass2 (tuning panel) | white 55% | white 6% |
| Ink / Ink2 / Ink3 | #1B2430 / #5B6570 / #98A1AB | #F0F3F6 / #A9B3BD / #6F7A86 |
| Line | #1B2430 10% | white 10% |
| Accent / AccentInk / OnAccent | hsl(h 82% 52%) / hsl(h 70% 42%) / #FFFFFF | hsl(h 90% 66%) / hsl(h 90% 72%) / #0F151C |
| Good / GoodInk | #1F8A5B / #1B7A51 | #3CCF8A / #5FDC9E |
| Amber / AmberInk | #E8931C / #B8690A | #F0A63A / #F5BD66 |
| Red / RedInk | #D9453A / #B7352C | #EF6A5F / #F58A80 |
| Map / MapRoad / MapPark / MapWater / Pin | #ECEFEC / #FFFFFF / #D6E7CF / #BCD7F2 / #3A424B | #1A222B / #26303B / #1F3328 / #223D5C / #E6EBF0 |
| Chip | #1B2430 6% | white 8% |

Default hue 218. Material role mapping in `ColorPaletteOverride.xaml`: Primary = Accent, OnPrimary = OnAccent, Surface and Background = Ground, OnSurface = Ink, OnSurfaceVariant = Ink2, Outline = Ink3, Error = Red. Good and Amber stay app tokens.

Hue dial: compute the four accent colors with an HSL helper and assign them to the shared `SolidColorBrush` instances, then push a new palette snapshot to the canvas.

### Typography

Instrument Sans, weights 400, 500, 600, 700. Named `TextBlock` styles in `Themes/`, each `BasedOn` a Material type-scale key (Themes 7.1.1: `Display|Headline|Title|Body|Label` x `Large|Medium|Small`, plus `LabelExtraSmall` and `CaptionLarge|Medium|Small`) with `FontSize`, `FontWeight` and `FontFamily` setters. That file is the one place that sets sizes; pages never do.

| Style | Size | Weight | BasedOn | Used for |
|---|---|---|---|---|
| EtaNumber | 42 | 700 | DisplayMedium | ETA minutes |
| CardTitle | 23 | 600 | TitleLarge | "School drop-off" |
| EtaUnit | 18 | 600 | TitleMedium | "min" |
| StatValue | 16 | 600 | TitleMedium | stat values |
| CardBody | 15 | 400 | BodyLarge | subtitle, arrival |
| CardLabel | 14 | 400 / 600 | BodyMedium | greeting, traffic level |
| ActionLabel | 13 | 400 / 500 | LabelLarge | action labels, tuning text |
| Caption | 12.5 | 400 | BodySmall | captions, car status, secondary lines |

Canvas text (11.5 map labels, 12 pill) is drawn by Skia from the same font files.
Title: weight 600, line height 1.15. ETA: weight 700, line height 1.0.
`CharacterSpacing` is a no-op on the Skia text stack, so the -0.02em and -0.03em tracking on the title and ETA is dropped. Tabular numerals depend on R5.

### Layout

- Stage: `ScrollViewer`, content max width 400 (824 in the two-column state), 16 side padding, 36 top, 56 bottom, 24 between card and tuning panel. Use star columns with `MaxWidth`, never a centered content-sized panel.
- Card: padding 22, 18 between sections, corner radius 34 (dialable 12 to 44), 1 px GlassEdge border. Shadow: two layers, `0 30 60 -30` at #141C28 45% and `0 2 6 0` at #141C28 8% (dark: black 80% and 30%).
- Header: grid `*` and 92, gap 10. Left: greeting row (18 sun icon, 8 gap), title button with 14 chevron at 6 gap, subtitle. Right: car illustration 92 by 42, name (12.5, weight 500, Ink), status (12.5, Ink2).
- Map panel: height 214, corner radius 20, 1 px Line border, Map fill, clipped. Canvas fills the panel and scales the 356 by 214 design space uniformly to fill, centered. ETA block overlays top-left at 16, 14 with max width 54 percent. A left-to-right fade of the Map color from 92 percent to 0 at 55 percent width sits under the ETA block, drawn in the canvas.
- Route path (design space): `M34 186 L102 180 Q140 178 164 160 L200 130 Q222 112 244 88 L258 74 Q268 62 276 56`. Segment stroke 5, round caps. Slack dots radius 2.1, step 7, from 12 after the start to 10 before the leave node. Car dot radius 6.5 with 3 white stroke, glow radius 14 at 18 percent. Nodes radius 5 (bell node 4, Amber) with 2.5 white stroke. Drive pill 58 by 24, radius 7, centered at the drive midpoint plus (30, 32). Pin with an 18 leader above the bell node. Labels: points above the path, durations below; the Leave label moves below the path when the gap from Now to Leave is 84 or less, and the slack caption hides in that case. Label halo: 3 px stroke in the Map color under the fill.
- Stats: three columns 1.05*, 1*, 1.05*, Line dividers, cell padding 9 left and 8 right. Icon 20, value 16 weight 600, caption 12.5 Ink2. Range bar spans the first cell: height 6, radius 3, Chip track, 10 above. Tires cell is a button with a 12 chevron.
  Charging cell: unplugged shows "—" over "Not charging", as in the prototype. Plugged in shows "{n}%" over "Charging", value and icon in GoodInk; at 100 the caption reads "Charged".
  Fix the prototype defect here: at 390 wide "All good" collides with the chevron. Give the chevron its own 12 px column and let the value trim.
- Actions: four equal columns, gap 6, top padding 4. Circle 56, corner radius 28 (half the height; an oversized radius renders oval on Skia), Chip fill, icon 24 with 1.7 stroke. Label 13, secondary line 12.5 Ink2, 8 under the circle.
- Subtitle fix: the default sentence wraps to two lines at 390. Accept the wrap, and reserve two lines of height so the map does not jump between states.

### Component hierarchy

```
MainPage
  BokehBackdrop                      SKCanvasElement, full-bleed, behind everything
  ScrollViewer > Grid (SafeArea insets, star columns, MaxWidth by visual state)
    MorningCardView                  UserControl
      CardShadowCanvas               SKCanvasElement, margin -60, card rect clipped out
      Border (glass)                 childless, AcrylicBrush per R2, radius, GlassEdge border
      Grid (content)
        Header                       TextBlocks, title Button, car illustration (Paths in a Viewbox)
        MapPanel (Border, clipped)
          RouteRulerCanvas           SKCanvasElement
          mvux:FeedView (Timeline)   Value: ETA block, Progress: skeleton, None: "No drop-off today"
        Stats                        Grid, ProgressBar, Button
        Actions                      ToggleButton x2, Button x2
    ContentControl (uen:Region)      hosts TuningPanel
      TuningPanel ("Simulator")      utu:ChipGroup, Slider x9, ToggleSwitch x2, Button x2
TripPage
  utu:NavigationBar                  back to Main
  Summary strip                      leave-by, drive, arrival
  mapsui:MapControl                  OSM tiles, route line, home and school pins
```

Simulator controls: state chips (Live, Loading, Offline, No trip); sliders for Clock, Traffic delay, Range, Cabin temperature, Front-left tire, Charge, Route hue, Corner radius, Motion speed; switches for Plugged in and Reduced motion; buttons for Replay entrance and Toggle theme. The Charge slider is enabled only while Plugged in is on.

- Icons are stroked `Path` elements inside a `Viewbox`, with data copied from the SVGs. `SvgImageSource` and Resizetizer output render as images with no stroke recolor, and `PathIcon` is fill-only and clips. All arcs in the source icons are circular, which the Uno path parser requires. `Path.Stroke` does not inherit `Foreground`: inside the button styles bind `Stroke` to the templated parent's `Foreground` so icons follow checked and disabled states.
- Lock and Climate base on Material's `IconToggleButtonStyle` with `Width` and `Height` 56 and `CornerRadius` 28. Lightweight keys: `IconToggleButtonBackground` (Chip), `IconToggleButtonBackgroundChecked` plus its `CheckedPointerOver`, `CheckedPressed`, `CheckedDisabled` variants (Accent), `IconToggleButtonForegroundChecked` plus variants (OnAccent). `ut:ControlExtensions.AlternateContent` holds the open-shackle icon for the unchecked lock. Verify the template honors `CornerRadius`; retemplate only if it does not.
- State chips: `utu:ChipGroup SelectionMode="Single"` with `Style="{StaticResource FilterChipGroupStyle}"` (an unstyled Chip renders bare; Material has no segmented style, only Cupertino ones). `ItemsSource` is the four connection modes, `SelectedItem="{Binding Mode, Mode=TwoWay}"` on an `IState<ConnectionMode>`. No `.Selection()` operator for a static list.
- `ProgressBar` carries the range so the value reaches automation. Material 7.1.1 exposes `ProgressBarBackground`, `ProgressBarForeground`, `ProgressBarHeight` and `ProgressBarMinWidth` only: colors and the 6 px height are reachable, the 3 px radius depends on the template binding `CornerRadius`, and the 2 percent floor is applied to the bound value in the model (R4).
- Focus visuals are `FrameworkElement` properties set in each interactive style: `UseSystemFocusVisuals="True"`, `FocusVisualPrimaryBrush="{ThemeResource AccentBrush}"`, `FocusVisualPrimaryThickness="2"`, `FocusVisualSecondaryThickness="0"`, `FocusVisualMargin="-3"`. The ring follows the control's `CornerRadius`; there is no separate focus radius. Material paints its own focused overlay (`IconToggleButtonStateCircleOpacityFocused`), so set that opacity key to 0 to avoid a double indicator.

### Theme usage

Material theme through the `Material` UnoFeature. `ThemeService` drives the runtime toggle. The canvas re-snapshots its palette on `ActualThemeChanged`.

### Responsive behavior

- Under 900 epx wide: single column, card then tuning panel, page scrolls.
- 900 and over: two columns, card left (400), tuning panel right (400), 24 gap, both top-aligned, centered as a pair. A `VisualStateManager` state with `AdaptiveTrigger MinWindowWidth="900"` sets the tuning region's `Grid.Row` and `Grid.Column` and the stage `MaxWidth` (400 to 824). Docs guidance: visual states for structure changes, `utu:Responsive` for property values; its default `Wide` breakpoint is 800 and its use on attached properties is undocumented. `ResponsiveView` is ruled out because it swaps templates, which would recreate the card and replay the entrance.
- Android landscape gets the two-column layout when width allows; otherwise it scrolls. No fixed heights outside the 214 map panel.

## Interaction Brief

### User flows

1. Open the app. Read the leave-in sentence, glance at the route.
2. Lock, Climate, Send to car. More is inert in this version.
3. Tap the title to open the Trip page: pan and zoom the real route, read the summary strip, go back. The tires cell is inert in this version.
4. Simulator: switch connection mode, drag dials, plug the car in, replay the entrance, toggle the theme.

### Headline logic

| Condition | Subtitle | Tone |
|---|---|---|
| slack >= 2 | "Leave in {n} min to arrive on time." | Ok |
| 0 <= slack < 2 | "Leave now to arrive on time." | LeaveNow |
| slack < 0 | "Running {n} min late. Leave now to arrive {h:mm}." | Late: subtitle and ETA in RedInk, drive segment, arrive node and pill in Red, no slack dots, no Leave node, Now, Arrive and Bell labels in RedInk |
| no trip | "Nothing on the calendar before 9." | none |
| stale vehicle | "Car isn't reachable, showing {h:mm} data." plus a Retry link button | none |

Traffic line: "{Level} traffic" in GoodInk, AmberInk or RedInk with a 9 px dot, then "{n} min faster than usual", "{n} min slower than usual" or "About usual".
All strings live in `.resw` with format placeholders.

### Input behavior

- Lock and Climate are `ToggleButton`s. `IsChecked` binds one-way from the snapshot; the click runs the command. Checked: Accent circle, OnAccent icon. Lock label reads "Lock" when locked and "Unlock" when unlocked, matching the prototype; the car status reads "Locked • 68°" or "Unlocked • 68°". Climate secondary line reads "68°" or "On, 68°".
- Commands are optimistic: the pressed state flips at once, the hub confirms after its latency, a failure reverts the state and surfaces the stale subtitle.
- Send to car: label reads "Sent" for 1400 ms, then returns.
- While the vehicle is stale, the four actions are disabled and stats render in Ink3.
- Minimum touch target 48; the circles are 56.

### Empty state

No drop-off today (`Trip` is None). The route and the ETA block hide; the map ground stays; centered title "No drop-off today" (22, weight 600) and a note, max 30 characters per line, "Pedagogical day at school. Nothing else is planned this morning." Car stats and actions stay live.

### Loading state

Map ground stays, the route hides, a skeleton replaces the ETA block at 16, 16: bars of 120 by 14, 110 by 44 (radius 10), 90 by 14, gap 10, Chip fill, radius 6. Pulse to 50 percent opacity over 1.4 s, driven by a 30 fps `DispatcherTimer`, stopped on exit.
On a cold start the vehicle feed is also loading, which the prototype never showed: stat values render as skeleton bars of the same height as the text they replace, the car status line is a skeleton bar, and actions are disabled. Layout must not shift between loading and live.

### Error states

- Stale vehicle (the designed "offline" state): described above. Retry calls `IVehicleService.RefreshAsync`.
- Hard failure with no cached data: card-shaped `ErrorTemplate` in the map panel, one sentence and a Retry button bound with `ElementName` to the `FeedView`. Retry must change an upstream value in the hub so the feed re-evaluates; `FeedView.Refresh` alone replays a cached upstream error.
- Drive one error and one retry end to end before building the stats and actions rows.

### Animations

All durations divide by the motion speed (0.25 to 2, default 1). Reduced motion skips every item below.

| Element | Motion |
|---|---|
| ETA block | opacity 0 to 1, translate Y 4 to 0, 320 ms, delay 60, KeySpline `0.25,1 0.5,1` |
| Slack dots | opacity 0 to 1, 160 ms each, delay 120 + 18 per dot |
| Drive segment | draws from its start along the path, 620 ms, same ease, starts at 140 + 18 per dot |
| Walk segment | 260 ms, starts at 85 percent of the drive duration after the drive starts |
| Nodes, pin, pill | scale 0.4 to 1 and opacity 0 to 1 from bottom center, 320 ms, back-out bezier `0.34,1.4 0.64,1` evaluated in C# inside the renderer (peaks at 1.053 near 63 percent), start at 85 percent of the walk |
| Labels | opacity 0 to 1, 260 ms, 120 after the nodes start |
| Range bar | `ProgressBar.Value` animated 300 ms, ease `0.25,1 0.5,1`, `EnableDependentAnimation="True"`; with the R4 fallback, `ScaleTransform.ScaleX` on the fill |
| Action press | circle scale 0.94, 140 ms `0.2,0.7 0.2,1`; fill change 160 ms |
| Tuning button press | scale 0.98 |

A `KeySpline` control point must stay within 0 to 1, so the back-out curve cannot be one. Anywhere XAML needs it, use `BackEase EasingMode="EaseOut" Amplitude="0.3"` (5.1 percent overshoot near 63 percent).
Press scale: the prototype's 0.94 on the 56 px circles wins over the 0.98 house default in `xaml-design-polish`, because the approved design states the number; 0.98 stays on the rectangular tuning buttons.

The route entrance is one timeline evaluated inside the renderer from elapsed time. A single `CompositionTarget.Rendering` subscription calls `Invalidate()` and unsubscribes when the timeline ends (about 1.7 s at speed 1). XAML storyboards stop the previous board on the same target before replay, so a held end value cannot mask the replay.
The entrance plays on first live data, on a connection mode change back to live, and on Replay. It does not play for loading or empty.

### Accessibility

- `RouteRulerCanvas` sets `AutomationProperties.Name` to a composed sentence, for example "Route to school. Now 7:24. Leave 7:33 after 9 minutes of slack. 14 minute drive, arrive 7:47. 3 minute walk. Bell 7:50."
- The subtitle is a polite live region.
- Title, tires cell and every action have names; the ETA block reads as one phrase.
- Visible focus: 2 px Accent ring, 3 px outside the control, following its corner radius (properties listed in the Design Brief).
- Android TalkBack on the Skia renderer is WIP upstream. Treat a failure there as a recorded gap, never as a pass.
- Light and dark ink on glass meet 4.5:1 for body text; verify Ink2 on Glass over the BlobA corner.

### Runtime verification steps

1. Desktop, `uno_app_start`, visual-tree snapshot: card present, FeedView in Value state, subtitle text equals "Leave in 9 min to arrive on time."
2. `MC_CLOCK=460`: late tone, red route, no slack dots, subtitle "Running 7 min late. Leave now to arrive 7:54."
3. Tuning: Range 60 gives warn colors, Range 20 gives bad; tire 29 gives "FL low", tire 25 gives "FL 25 psi"; delay 7 gives "Heavy traffic".
4. Connection modes: Loading shows the skeleton and no route; Offline shows the stale subtitle, "Last seen 6:58", disabled actions; Retry returns to Live and asserts the hub served a new snapshot; No trip shows the empty panel with live stats.
5. Lock and Climate through client-coordinate clicks (ToggleButton peers reject the default action). Assert status text and label changes.
6. Theme toggle: canvas colors follow; hue dial at 0 and 120: route, pill, Lock circle and focus ring follow.
7. Entrance: set speed 0.25 on a fresh process and capture mid-flight; confirm the Rendering hook releases by sampling CPU at idle (under 2 percent of a core).
8. Android: install, cold start, safe area with the backdrop full-bleed under the status bar, all four modes, back gesture (repeat on an API 36 image), 60 s idle CPU, rotate to landscape, one TalkBack pass over the card (record the result either way).
9. 390 by 844, 390 by 600, 1280 by 800, 1280 by 600 captures in light and dark.

## Implementation Plan

Each step ends with a build, the listed check and a conventional commit. Invoke the named skills before writing in their area.

1. **Scaffold: DONE 2026-09-21.** Both heads build with 0 warnings on Uno.Sdk 6.7.30; the desktop head launches to the template page; Android has not been launched yet (no device was attached). The installed templates were 6.8.0-dev.23 and emitted that SDK version, which was pinned back to 6.7.30. Original instruction, for the record: (`uno-platform-agent`) run the scaffold command from the Pinned versions section, reconcile against DYT, pin Uno.Sdk 6.7.30, Android min API 24, copy `.mcp.json`, add Core and Tests projects, gate `UseStudio()`. Build both heads. Launch both heads with the template page. `feat: scaffold MorningCard solution`
2. **Spikes R1 to R6**, time-boxed, results written back into this spec. `docs: record spike results`
3. **Core** (`dotnet-csharp`): records, `TripMath`, `RouteLayout`, service contracts, `SimulatedDriveHub`, tests. `feat(core): trip math and simulated hub`
4. **Theme and fonts** (`uno-material`): tokens, palette override, font resources, text styles, HSL helper. `feat(theme): card tokens and type`
5. **Model and hosting** (`mvux`, `uno-extensions-services`, `uno-navigation`): DI registrations, routes, `MainModel`, env hooks. `feat: main model and hosting`
6. **Populated card, static** (`uno-toolkit`, `winui-xaml`): `MorningCardView` with live bindings, no canvas yet. Verify text at 390. `feat(card): populated layout`
7. **Route ruler** (`skiasharp-uno`): renderer, canvas, palette snapshot, automation name. Parity capture against the HTML. `feat(card): route ruler canvas`
8. **One error and one retry**, end to end, before anything else grows. `feat(card): stale and retry path`
9. **States** (`uno-component-states`): loading, empty, hard error, late and leave-now tones. `feat(card): loading, empty and error states`
10. **Actions**: commands, optimistic toggles, Sent confirmation, disabled rules. `feat(card): vehicle actions`
11. **Tuning panel and responsive layout** (`uno-toolkit`): chips, sliders, two-column breakpoint. `feat: tuning panel`
12. **Motion** (`xaml-design-polish`): entrance timeline, press feedback, range bar, skeleton pulse, reduced motion. `feat(motion): entrance and feedback`
13. **Backdrop and glass**: `BokehBackdrop`, shadow, glass technique per R2 and R3. `feat: backdrop and glass`
14. **Trip page** (`uno-navigation`, `uno-toolkit`): spike R1 first, then add Mapsui.Uno.WinUI 5.1.0, `TripPage`, `TripModel`, the Trip route, the title `Navigation.Request`, sample `TripRoute`. Verify pan and zoom on both heads. `feat(trip): trip page with map`
15. **Localization** (`winui-xaml`, `uno-extensions-services`): move every string to `Strings/en/Resources.resw` with `x:Uid`. `feat: localization`
16. **Android pass**: safe area, status bar, touch targets, TalkBack pass, frame timing with `dumpsys SurfaceFlinger --timestats`. `fix(android): ...`
17. **README**: what the sample shows, how to run both heads, the Simulator, and a Known gaps list (tires chevron and More are inert, data and commands are simulated, English only, desktop reduced motion comes from the Simulator, Android TalkBack result). `docs: readme`
18. **Verification run** (`uno-verify`), all steps above, then `handoff`.

### Risks (time-boxed spikes)

| # | Risk | Box | Fallback |
|---|---|---|---|
| R1 | Mapsui.Uno.WinUI 5.1.0 at runtime on both Skia heads (run at step 14). Dependencies align on paper (SkiaSharp >= 3.119.2, Uno.WinUI >= 6.5.153, Graphics2DSK; net9.0 and net9.0-android35.0 TFMs). Control `Mapsui.UI.WinUI.MapControl`, `OpenStreetMap.CreateTileLayer(userAgent)` (the OSM tile policy requires a user agent), route as a `MemoryLayer` with a `GeometryFeature` line, pin as a `PointFeature`. Uno's own `MapControl` has no desktop support. | 15 min | WebView over a JS map, or defer the map to a later version |
| R2 | Glass blur, in order: (1) the documented Skia-native acrylic on the childless glass Border: `<AcrylicBrush AlwaysUseFallback="False" media:AcrylicBrushExtensions.UseCompositionEffectBrush="False" TintColor=".." TintOpacity=".."/>` (`features/shapes-and-brushes.md`), on 6.7.135 desktop and Android; (2) `SaveLayer` with a blur plus saturate backdrop filter in an `SKCanvasElement` | 15 min | Translucent Glass fill over the already blurred bokeh. The visual difference is small because the backdrop is out of focus by design. |
| R3 | Closed by decision: the shadow is Skia-drawn (see Decisions). Remaining check: the clip-out follows the radius dial and the theme. | 5 min | none needed |
| R4 | Material `ProgressBar`: 6 px and colors through lightweight keys, radius 3 only if the template binds `CornerRadius` | 10 min | Two `Border`s in a `Grid`, `ScaleX` animation, `AutomationProperties` range text |
| R5 | Tabular numerals: `Typography.NumeralAlignment` on Uno Skia text (no doc page found), and the `tnum` feature in canvas labels | 10 min | Accept proportional numerals; fixed-width slot for the ETA number |
| R6 | Closed. `AnimationsEnabled` is real on Android and hardcoded `true` on desktop, so the tuning panel carries a Reduced motion switch. | 0 | none needed |
| R10 | `.ttf.manifest` font manifest resolves four static weights from one `FontFamily` on both heads | 10 min | One `FontFamily` resource per weight, the validated route in `uno-scaffolding.md` |
| R11 | `SKTypeface` from a packaged font on Android: `StorageFile.GetFileFromApplicationUriAsync` then `OpenStreamForReadAsync` into `SKTypeface.FromStream` (file access is documented, the Skia step is not) | 10 min | Embed the four TTFs as assembly resources for the canvas only |
| R7 | Slider drag through hub, feed and FeedView on Android stays smooth | 10 min | Throttle hub pushes to one per frame |
| R8 | Closed. `SkiaRenderer` brings `Uno.WinUI.Graphics2DSK` on both heads (SDK targets, line 101). | 0 | none needed |
| R9 | `Feed.Combine(Trip, Now)` reaches `FeedView.NoneTemplate` when `Trip` is None. The None fast-start is confirmed from IL only. | 5 min | Project `Timeline` from a hub-side combined stream and return null there |

After two failed fix cycles on one root cause, stop and write a Debug Checkpoint.

## Resolved Decisions (user, 2026-09-21)

| # | Question | Decision |
|---|---|---|
| 1 | Map | **1a.** The card keeps the Skia route ruler over the drawn ground as a glance surface. A Trip page carries a real, pannable Mapsui map of the route. |
| 2 | Data and commands | Simulated hub behind `ITripService` and `IVehicleService`. No `Http` feature. |
| 3 | Dead buttons | Left as they are in the prototype; logic comes later. The tires cell and More keep their look and do nothing. See Unresolved Question 1 for the title chevron. |
| 4 | Charging cell | Add plug state: `IsPluggedIn` and `ChargePercent` on `VehicleSnapshot`, plus tuning controls. |
| 5 | Languages and units | English only for now. Strings still live in `.resw` with `x:Uid`, so French is an added folder later. Units stay miles, °F and psi, as in the mockup (delegated call). |
| 6 | Tuning panel | Ships in all builds (delegated call). It is the "Dial it in" panel under the card in the prototype: the state chips and sliders that drive the simulated car and trip. In a sample it is the only way a viewer can see the late, warning, offline, loading and empty states, so it is part of the demonstration. It is titled "Simulator". |
| 7 | Repo layout | Confirmed: `SampleBuilds/MorningCard.Uno/`, prototype untouched. |
| 8 | Walk-in buffer | Stays a per-school field on `TripPlan`, supplied by the trip service (delegated call). A routing API reports drive time; the walk from the curb to the door belongs to the school record. |
| 9 | Cold-start loading | Accepted (delegated call): stats values and car status render as skeleton bars, actions disabled, no layout shift. |
| 10 | Platform gaps | Both accepted as recorded risks (delegated call). Desktop reduced motion comes from the Simulator switch. Android TalkBack gets one device pass and its result is written into the README either way. |

Accepted gap, stated plainly per the capability rule: the tires chevron and the More button look tappable and do nothing in this version. They are listed in the README's known gaps so the omission stays visible.

## Unresolved Questions

1. **Title chevron.** Decision 1a needs an entry point for the Trip page, and decision 3 leaves dead buttons alone. This spec resolves the overlap by wiring only the title ("School drop-off >") to the Trip page and leaving the tires cell and More inert. If the title should stay inert too, the Trip page needs another entry point; say which.
2. **Sample route geography.** The Trip page needs real coordinates. The simulated service ships one fixed sample polyline (home to a school, about 14 minutes of driving) and labels it as sample data. Any preferred area, or is a generic Montreal West Island route fine?
3. **OSM tiles.** The Trip page map loads OpenStreetMap tiles over the network with an app user agent, which is the one real remote dependency in the app. Fine for a sample, or should it use a different tile source?
