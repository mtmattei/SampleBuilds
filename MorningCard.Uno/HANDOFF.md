# HANDOFF — MorningCard Uno Platform build
Updated: 2026-09-21

## Where we are
SPEC.md Implementation Plan steps 1 through 13 are done. The card runs on desktop with the route
ruler, all its states, the Simulator panel, the entrance motion and the Skia card shadow. A mockup
arrived late in the session that is a newer reference than the HTML prototype this was ported from;
four of its differences are closed and the rest are catalogued in `MOCKUP-GAPS.md`, two of which
need a decision or an asset from you.

Steps 14 to 18 (trip page with the Mapsui map, localization pass, Android pass, README,
verification run) have not started.

## Last verified state
- Build: pass, `net10.0-desktop`, Debug, 0 warnings, 0 errors.
- Tests: 64 pass (`MorningCard.Tests`).
- Runtime, desktop: all twelve states captured to `captures/`. Error and retry proven end to end by
  a debug probe (`MC_PROBE_RETRY=1`), which asserts the card returns to its live state. Entrance
  captured mid-flight at `MC_MOTION=0.25`; idle CPU after it settles is 0.5% of one core, so the
  one-shot timer does release.
- Runtime, Android: built clean at step 3, **never launched** - no device or emulator attached.
- Not verified through the uno-app MCP: the session was rooted one level up, so captures went
  through `PrintWindow` with a screen-copy fallback.
- Git: branch `spec/morningcard-uno`, 12 commits, nothing pushed.

## Next actions (in order)
1. Answer the two open questions in `MOCKUP-GAPS.md`: the car image asset (#9) and whether the card
   ground becomes a real map (#10, which would reopen SPEC Q1).
2. Close the cheap mockup gaps: #5 pill overlap, #6 route path shape, #7 panel height, #8 backdrop.
3. Chip artifact: the Offline and No trip chips intermittently draw a stray mark over the end of
   their label. Ruled out the item template, clipping, the group style and the font override by
   measurement; it is intermittent, so most likely a measure race trimming the label. Needs a
   visual-tree snapshot through the App MCP while it is visible.
4. Android smoke on a device: safe area, the four modes, back gesture, one TalkBack pass.
5. Spike R2 (the documented Skia `AcrylicBrush` path for the glass) - the translucent fill over the
   pre-blurred bokeh is the current floor and looks right, so this is an upgrade, not a blocker.
6. Then SPEC steps 14 to 18.

## Watch out for
- **The capture method perturbs the app.** `PrintWindow` returns a blank bitmap for this Skia GL
  window, so the script foregrounds it and screen-copies; a physical mouse resting over the window
  then hovers or drags a control. One capture came back with the traffic slider moved to 7. Use the
  App MCP where possible, and check the Simulator values in any capture before trusting it.
- `Remove-Item Env:FOO` is blocked in this sandbox; clear a variable with `Set-Item Env:FOO ''`.
- Close the app before any rebuild or the exe stays locked (`MSB3027`).

## Relaunch
```powershell
cd C:\Users\Platform006\UnoProjects\SampleBuilds\MorningCard.Uno
claude            # session root must be this folder so .mcp.json registers uno-app
# build:  dotnet build MorningCard\MorningCard.csproj -f net10.0-desktop
# run:    uno_app_start (preferred), else $env:APP_NO_HOTDESIGN=1 and start
#         MorningCard\bin\Debug\net10.0-desktop\MorningCard.exe
# states: pwsh -File scripts\capture-states.ps1
```
