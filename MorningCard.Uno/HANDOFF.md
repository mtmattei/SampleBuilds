# HANDOFF — MorningCard v4 (route ruler on real streets, then a design pass)
Updated: 2026-09-22 16:10

## Where we are

v4 replaced the schematic route with real geometry. Ticks are placed **by time** along a committed
OSRM route, so their spacing carries speed, and slack became a **dwell ring** at the pin where the
waiting happens — a parked car has no geographic length. The projection runs on the UI thread
(`RulerProjection`) so the Skia renderer only ever sees pixels; the map is fitted once and then
pan/zoom/rotation-locked. A long design pass followed, driven by the user against the two artboards
in `~/Downloads`: the two-layer shell was restored, the card was measured to within 5% of the
reference, the scrim profile was taken off the mockup by measurement, and every hand-drawn icon was
replaced with a real set.

## Last verified state

- **Build**: `net10.0-desktop` and `net10.0-android` both clean, **0 warnings, 0 errors**.
- **Tests**: 90 passed, 0 failed. (Was 100 — 10 covered tire/range-bar maths that is now deleted.)
- **Runtime**: desktop verified by capture and by measurement throughout. **Dark theme and Android
  have not been run at all this session** — see Next actions 1 and 2.
- **Git**: branch `spec/morningcard-uno`, last commit `15bb239`, working tree clean, pushed to
  `origin` (upstream now set).

## Next actions (in order)

1. **Run dark theme.** Highest risk and cheapest check. This session added seven per-theme scrim
   stop colours, a new `CardSurfaceBrush`, moved the ETA ink back to theme-following brushes
   (deleting `MapLateInkConverter`), and added an icon font. None of it has rendered dark. The
   Simulator has a "Toggle theme" button, or `MC_THEME`.
2. **Run Android.** Nothing since v1. It now carries a Skia renderer rewrite, an `ms-appx` JSON
   asset load, a locked Mapsui map, a new icon font and a 27pt padding scale.
   `dotnet build MorningCard/MorningCard.csproj -f net10.0-android -t:Run`.
3. **Write the v4/v5 design delta into SPEC.md.** The v4 section documents the ruler decision but
   predates the whole design pass: the shell, the parity numbers, the scrim profile, the icon set.
   Note a peer session (SchoolDropoff) owns the **v5 Android form factors** section at the end of
   that file — re-read before editing so it is not clobbered.
4. **Touch targets** (still open from v3): overflow is 28x24, row chevrons 16x16. Fails WCAG 2.5.8
   and matters on the phone head.
5. **The cabin row's second level.** The mockup expands it to "Warm to 20° while you drive", a
   `Remote start · −3° → 20°` chip and Start warming / Skip buttons. Ours shows the collapsed line
   because the seed has climate on. Skip still has no meaning — that is why it was dropped in v3.

## Open questions

- **The route runs east→west; the mockup's runs southwest→northeast.** So our Bell pin lands in the
  washed part of the scrim where the mockup's lands in the clear. The callout draws above the scrim
  and stays legible, but the pin sits on washed tiles. Only fixable by changing the geography.
- **The walk leg is 230m of real pedestrian routing** (Valhalla, 4.9 km/h) — but OSRM's demo server
  answers `/foot/` with a car, and the first asset shipped that way. If the asset is ever
  regenerated, check the km/h the script prints; it warns above 8.
- **The subtitle at 13pt is deliberately outside the 5% parity band** (reference implies ~17). User
  instruction, not drift.
- Entrance motion is still verified by build only — 1700ms against ~2s capture latency.

## Recurring traps worth knowing before touching this card

- **Ink resolved from the app theme while its ground does not follow the theme.** Four occurrences
  across v3/v4. The current arrangement is consistent — scrim and ETA ink both follow the theme —
  but anything drawn *on the map* still takes the map's palette, because OSM has no dark style.
- **`{Binding Parent.X}` resolves commands but not state from inside a `FeedView` template.** Cost
  two defects. Carry state on the feed the template already binds to — that is why `CarView` has
  `StatusBadge`/`Tone` and `HeaderView` has `IsLocked`.
- **The running exe locks the build output** (`MSB3027`). Kill before every rebuild.
- **`uno_app_start` refs are per-launch.** Take a fresh snapshot after every relaunch or every ref
  is stale.
- **An element screenshot has no backdrop composited**, so the translucent shell and the opaque
  inner card both render pure white and the hairline between them vanishes. Measure the two-layer
  geometry from a *window* capture, not an element one.

## Relaunch

```powershell
cd C:\Users\Platform006\UnoProjects\SampleBuilds\MorningCard.Uno
dotnet build MorningCard/MorningCard.csproj -f net10.0-desktop
dotnet test

# Kill first: the running exe locks the build output (MSB3027).
Get-Process MorningCard -ErrorAction SilentlyContinue | Stop-Process -Force

# Preferred: the uno-app MCP (needs a session rooted in THIS folder for .mcp.json to register).
#   uno_app_start -> visual tree -> element screenshots.
# Fallback when the MCP is down, which happened mid-session:
$env:APP_NO_HOTDESIGN = '1'   # UseStudio() never creates the window without a DevServer
$p = Start-Process .\MorningCard\bin\Debug\net10.0-desktop\MorningCard.exe -PassThru
Start-Sleep -Seconds 16
.\scripts\Capture-Tall.ps1 -ProcessId $p.Id -Out capture.png -Height 800 -Width 620
```

Debug hooks: `MC_THEME`, `MC_STATE` (offline/empty/error), `MC_CLOCK`, `MC_DELAY`, `MC_RANGE`,
`MC_OPEN` (checks|menu), `MC_PROBE_RETRY`, `MC_PROBE_FOCUS`, `MC_TRACE=<file>`.
`scripts/capture-states.ps1` shoots every state.

Generators, run by hand, never from the app: `scripts/Fetch-Route.ps1` (OSRM drive + Valhalla walk
→ `Assets/route.json`), `scripts/Fetch-Icons.ps1` (Phosphor TTF + generated `Themes/Icons.xaml`).
