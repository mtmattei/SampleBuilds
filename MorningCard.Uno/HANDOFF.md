# HANDOFF — MorningCard Uno port
Updated: 2026-09-22

## Where we are

The card is feature complete on desktop **and Android**, and matches the mockup. This session
closed the `/uno-audit` + `mcp__uno__init` findings ("fix all"), then chased the two defects that
survived them, then took the first Android run of the project. Both desktop defects are closed with
evidence rather than guesses:

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
- Android: **runs on the emulator** (`sdk_gphone64_x86_64`, API 36). Fonts, the Skia route canvas,
  the stats row and the actions all render; SafeArea holds the card below the status bar. Light and
  dark both verified, dark by flipping system night mode **while the app was running**, which is
  what proves the converter repaint path and not just the startup path.
- All twelve state captures re-shot through the settling resize: `captures/*.png`.
- Git: branch `spec/morningcard-uno`, clean.

## Found and fixed on the Android run

- **Mode chip row overflowed the phone width** — four chips measure 431 epx against about 340
  available, so "No trip" was cut in half. Wrapped in a horizontal `ScrollViewer` (Material's
  phone chip-row pattern); scrolling verified with an `adb input swipe`. ChipGroup lays out in a
  `StackPanel` and the wrapping panels are unreliable on this stack, so scrolling is the honest fix.
- **Simulator panel failed contrast in dark** — `GlassSecondaryColor` was `#0FFFFFFF`, a 6% white
  wash, so the panel had no ground of its own and its body text sat straight on the backdrop glow
  at about 1.5:1. Now `#D9141A23`: 5.5:1 over the brightest glow, 8.3:1 over bare ground. Desktop
  dark improved by the same change.

## Next actions (in order)

1. Long-string pass: the headline, the action labels and the stat captions have only ever been
   rendered with their design-length English strings. The stats row already wraps to two lines on
   a phone ("Not charging", "Tire pressure"), so it has the least headroom.
2. TalkBack pass on the emulator. Never exercised; `MC_PROBE_FOCUS` covers tab order on desktop
   only.
3. Remaining `MOCKUP-GAPS.md` items: route path shape, warm-gradient backdrop, organic schematic
   ground. All cosmetic, all deliberate deferrals.
4. Landscape / short-viewport check on the phone. Per `capability-coverage.md` two viewport
   *heights* matter as much as two widths, and only portrait has been seen.

## Open questions

- The App MCP (`uno_app_*`) was never available in this session — only the docs tools registered.
  Per `~/.claude/rules/uno-scaffolding.md` that needs a session rooted at the folder holding the
  `.sln`, which is `MorningCard.Uno`, not `SampleBuilds`. Everything above was verified with
  `adb` and `PrintWindow` instead.
- The chip row scrolls rather than wraps. Fine on a phone; on a narrow *desktop* window it is less
  discoverable, since there is no touch affordance. Worth revisiting only if desktop ever needs to
  go below ~470 epx wide.

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
