# HANDOFF — MorningCard v3 (card redesign)
Updated: 2026-09-22 11:05

## Where we are

The card was redesigned twice in this session. **v2** replaced the stats row and action circles with
three list rows and put a real Mapsui/OSM map under the route. **v3** (mock: `refeference2.png` and
the three-frame screenshot) added a state badge, a timeline strip, a "Before you go" checklist, a
bottom action bar, and **restored the three service commands v2 had deleted**. A `/ui-craft` and
`/impeccable:audit` pass followed; four of five craft findings and the audit's one critical are
fixed. The card builds clean on both heads and is verified on desktop in light and dark.

**SPEC.md is three versions behind.** It carries a v2 delta only. v3 is undocumented — most
importantly the command restoration, which reverses v2 decision A recorded in that same file.

## Last verified state

- Build: `net10.0-desktop` and `net10.0-android` both clean, 0 warnings, 0 errors.
- Tests: 62 passed, 0 failed.
- Runtime: desktop verified by capture in light and dark, expanded and collapsed, plus the three
  tone states (on time / leave now / running late). **Android has not been run since v1.**
- Git: branch `spec/morningcard-uno`, last commit `84207cd`, working tree clean.

## Next actions (in order)

1. **Rework the route on the map.** The schematic ruler cuts across streets it does not follow. The
   decision to keep it schematic predates the timeline strip; now that the strip carries slack,
   drive and walk proportionally, the map no longer has to encode time at all. Proposed split:
   strip owns time, map owns geography. Concretely: a fixed polyline of real Plateau coordinates
   drawn as a Mapsui line layer (so it survives pan and zoom), pins at leave/arrive/bell with the
   time labels as callouts, and the dotted slack run removed from the map entirely — the car has
   not moved, so it has no geographic length. Confirm the split before building.
2. **Write the v3 delta into SPEC.md**, with the command restoration recorded as a reversal of v2
   decision A, and the capability inventory updated.
3. **Run v2/v3 on Android.** Everything since v1 is desktop-only. The emulator (`emulator-5554`)
   was working; `dotnet build MorningCard/MorningCard.csproj -f net10.0-android -t:Run`.
4. **Touch targets** (audit H2): overflow is 28x24, row chevrons 16x16. Fails WCAG 2.5.8 and
   matters on the phone head.
5. **Delete dead code**: `TireFrontLeftPsi` and its Simulator slider, `RouteRulerRenderer.DrawGround`,
   `CarView.FuelPercent`, and the `CarTile*` tokens now that the car has no tile.

## Open questions

- The reveal motion is verified by build, not by eye: a 260ms animation cannot be caught by a
  capture with ~2s latency. Unverified rather than confirmed.
- The overflow menu opens (trace confirms `ShowAt`) but has never been photographed — `PrintWindow`
  does not capture popup layers.
- "Skip" from the mock was dropped rather than shipped inert; it needs a meaning first.
- Weather and the parking note are seeded on the hub like everything else. Fine for a sample,
  but no real source exists.

## Recurring trap worth knowing before touching the card

**Ink resolved from the app theme while its ground does not follow the theme.** Three occurrences
this session: the converter brushes, the Skia route labels, and the ETA block (which went invisible
in dark on a deliberately-light scrim). Anything drawn on the map takes the map's palette — the map
is light in both themes because OSM has no dark style.

Second trap: **`{Binding Parent.X}` resolves commands but not state from inside a `FeedView`
template.** It cost two defects here. Carry state on the feed the template already binds to.

## Relaunch

```powershell
cd C:\Users\Platform006\UnoProjects\SampleBuilds\MorningCard.Uno
dotnet build MorningCard/MorningCard.csproj -f net10.0-desktop
dotnet test

# Kill first: the running exe locks the build output (MSB3027).
Get-Process MorningCard -ErrorAction SilentlyContinue | Stop-Process -Force
$env:APP_NO_HOTDESIGN = '1'   # UseStudio() never creates the window without a DevServer
$p = Start-Process .\MorningCard\bin\Debug\net10.0-desktop\MorningCard.exe -PassThru
Start-Sleep -Seconds 14
.\scripts\Capture-Tall.ps1 -ProcessId $p.Id -Out capture.png -Height 1220
```

Debug hooks: `MC_THEME`, `MC_STATE` (offline/empty/error), `MC_CLOCK`, `MC_DELAY`, `MC_RANGE`,
`MC_OPEN` (checks|menu), `MC_PROBE_RETRY`, `MC_PROBE_FOCUS`, `MC_PROBE_CHIPS`, `MC_TRACE=<file>`.
`scripts/capture-states.ps1` shoots all eleven states.

Start the next session **rooted in `MorningCard.Uno`** so the project `.mcp.json` registers the
`uno-app` server — it was never available this session, and everything above was verified with
`adb` and `PrintWindow` instead.
