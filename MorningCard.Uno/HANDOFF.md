# HANDOFF — MorningCard Uno port
Updated: 2026-09-22

## Where we are

The card is feature complete on desktop and matches the mockup. This session closed the
`/uno-audit` + `mcp__uno__init` findings ("fix all"), then chased the two defects that survived
them. Both are now closed with evidence rather than guesses:

- **Dark theme lost the ETA number and dimmed the stats row.** Cause was ordering, not palette:
  `IValueConverter` results are evaluated once, and the page is built inside
  `InitializeNavigationAsync` — before `AccentPalette.Track` has a root to read a theme from — so
  every converter-driven brush resolved Light. `AccentPalette` now hands out one live
  `SolidColorBrush` per token key and repaints them on `ActualThemeChanged` (and once at track
  time), so evaluation order no longer matters. Verified in both themes.
- **Stray marks over the Simulator chip labels.** Not a defect in the app. A `MC_PROBE_CHIPS`
  subtree dump reports exactly one `TextBlock` per chip, untrimmed and unoverlapped; the marks are
  stale pixels left when `Capture-Tall.ps1` collapsed the default 1024x640 wide stage straight to
  520x1120, so the chip band still held text from the old layout (the stray "68" was the cabin
  temperature). Any second relayout clears it. Fixed in the capture script by sizing twice.

Both findings are written up in `~/.claude/rules/uno-scaffolding.md`.

## Last verified state

- Build: `net10.0-desktop` clean, 0 warnings, 0 errors.
- Tests: 63 passed, 0 failed.
- Runtime: desktop verified by capture in light and dark. Card, map panel, stats row, actions and
  Simulator all render correctly; chips clean.
- Android: **never launched.** No device run at any point in this project.
- Git: branch `spec/morningcard-uno`, last commit `7770fc7`. Uncommitted: the chip probe
  (`DebugHooks.ProbeChipsAsync` + `MC_PROBE_CHIPS`), `TuningModel.Modes` as a plain property,
  the `TuningPanel.xaml` comment rewrite, and `scripts/Capture-Tall.ps1`.

## Next actions (in order)

1. Launch on an Android device. Nothing about SafeArea, StatusBar, edge-to-edge or TalkBack has
   been exercised. Expect the first real findings there.
2. Long-string pass: the headline, the action labels and the stat captions have only ever been
   rendered with their design-length English strings.
3. Remaining `MOCKUP-GAPS.md` items: route path shape, warm-gradient backdrop, organic schematic
   ground. All cosmetic, all deliberate deferrals.
4. Re-run `scripts/capture-states.ps1` end to end — it still uses the single-resize path through
   `Capture-Window.ps1` directly, so its twelve state captures may carry the same stale-pixel
   artifact the tall capture just had.

## Open questions

- Should `capture-states.ps1` route through `Capture-Tall.ps1` so every state capture gets the
  double-resize settle, or keep them at the default window size and accept clipped cards?
- The App MCP (`uno_app_*`) was never available in this session — only the docs tools registered.
  Per `~/.claude/rules/uno-scaffolding.md` that needs a session rooted at the folder holding the
  `.sln`, which is `MorningCard.Uno`, not `SampleBuilds`.

## Relaunch

```powershell
cd C:\Users\Platform006\UnoProjects\SampleBuilds\MorningCard.Uno
dotnet build MorningCard/MorningCard.csproj -f net10.0-desktop
dotnet test

# Desktop capture (APP_NO_HOTDESIGN=1 opts out of UseStudio so the window appears headless)
$env:APP_NO_HOTDESIGN = '1'
$p = Start-Process .\MorningCard\bin\Debug\net10.0-desktop\MorningCard.exe -PassThru
Start-Sleep -Seconds 10
.\scripts\Capture-Tall.ps1 -ProcessId $p.Id -Out capture.png
```

Debug hooks: `MC_THEME`, `MC_STATE`, `MC_CLOCK`, `MC_DELAY`, `MC_RANGE`, `MC_TIRE`,
`MC_PROBE_RETRY`, `MC_PROBE_FOCUS`, `MC_PROBE_CHIPS`, `MC_TRACE=<file>`.

Start the next session **rooted in `MorningCard.Uno`** so the project `.mcp.json` registers the
`uno-app` server.
