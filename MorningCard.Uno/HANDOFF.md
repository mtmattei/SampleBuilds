# HANDOFF — MorningCard v6 phone redesign (steps 1-9 done)
Updated: 2026-09-23

## Where we are
The v6 plan in SPEC.md is complete for the phone card, steps 1 to 9. That covers the spikes, the removal
of dark mode and the hue dial, the v6 tokens and Geist, the slack ruler (Core layout plus UI), the header,
footer and map, the checklist accordion with `CabinChoice`, all four states, and the desktop and Android
parity pass. Every state scores at least 89% against `docs/design/v6/png/phone_*.png` on both heads
(SPEC.md "v6 parity results"). The work is in the worktree `C:\Users\Platform006\UnoProjects\SampleBuilds-android`
on branch `feat/android-form-factors`. **Step 10 (resume v5 with the v6 part list) has not started.**
It stops on project restructuring and emulator downloads (see Open questions).

## Last verified state
- Build: `net10.0-desktop` and `net10.0-android` Debug, 0 warnings, 0 errors. Tests 102/102.
- Runtime: the `uno-app` MCP disconnected mid-session, so runtime checks used the Debug exe with
  `APP_NO_HOTDESIGN=1`, `scripts/Capture-Tall.ps1` and DEBUG probes. Confirmed: the cabin flow
  (`MC_PROBE_CABIN`), Retry from ETA unavailable (`MC_PROBE_RETRY`: PASSED), the ruler grow-in, the
  live-dot pulse, and all 7 states on desktop plus 5 on the `chefs36` emulator.
- Git: `db9eab8` plus this handoff. The only uncommitted files are the pre-v6 `captures/android/*`
  files from an earlier session, left alone on purpose.
- Lint: CARD 1 · HEX 0 · TOKENTHEME 10 · BACKBAR 0 · CODEBEHIND 8 · OVERLAY 0 · RESPONSIVE 0 · BUILTIN 1 · WORKAROUND 1.

## Guidance applied
| Skill / rule / decision | Applied at (file:line) |
|---|---|
| uno-toolkit: CardContentControl for shell, panel and checklist | MorningCard/Controls/MorningCardView.xaml:56 |
| uno-component-states: skeleton loading via Undefined and Progress | MorningCard/Controls/MorningCardView.xaml:15, SkeletonBar :34 |
| mvux: Retry through Parent in the ValueTemplate, proven at runtime | MorningCard/Controls/MorningCardView.xaml:289 |
| skiasharp-uno: cached-paint SKCanvasElement hatch | MorningCard/Controls/HatchFill.cs:25 |
| Spike R8: tinted tiles | MorningCard/Controls/MorningCardView.xaml.cs:154 |
| Icon memory: Lucide font (user choice) | MorningCard/Themes/Icons.xaml:13 |
| Rules file: timer pulse, not a Forever storyboard | MorningCard/Controls/MorningCardView.xaml:595 (LivePulse) |
| Workaround protocol: Chip clips at 22 px | MorningCard/Themes/CardStyles.xaml:563 |
Read, not applied: none.

## Next actions (in order)
1. The user decides on v5 step 2: split the card into a `MorningCard.UI` library (project restructure).
2. Install the TV, Wear and Automotive system images once approved (1-2 GB each), then run v5 steps 5-9
   against the v6 surface artboards (`docs/design/v6/png/android_tv.png`, `wear_os.png`, `android_auto.png`).
3. Optionally: tune `MutedTileSource.PaperMatrix`, whose first pass is busier than the artboard, and
   file the Toolkit Chip issue upstream.

## Open questions
- Should v5 go ahead (library split plus TV, Wear and Auto), and may it download the system images?
- Lint: TOKENTHEME 10 are single-value converter brushes, correct now that the app is light only (B6).
  CARD 1 is the Simulator panel, CODEBEHIND 8 is the card's lifecycle and fit handlers, and BUILTIN 1 is
  `CardShadowCanvas`, which is deliberate (see the ShadowContainer gotcha in the rules file). Clean these
  up, or add allow notes?
- Accepted v6 deviations are listed in SPEC.md under v6 Unresolved Questions (pills, pill offset, now-dot fan-out).

## Relaunch
```powershell
cd C:\Users\Platform006\UnoProjects\SampleBuilds-android\MorningCard.Uno   # root the session here for uno-app
dotnet build MorningCard/MorningCard.csproj -f net10.0-desktop
$env:APP_NO_HOTDESIGN="1"; $env:MC_STATE="loading"   # or error | empty | offline; MC_CLOCK=457 for late
Start-Process MorningCard\bin\Debug\net10.0-desktop\MorningCard.exe -WorkingDirectory MorningCard\bin\Debug\net10.0-desktop
pwsh -File scripts\Capture-Tall.ps1 -ProcessId <pid> -Out captures\x.png -Width 640 -Height 1200
# Android: boot chefs36, dotnet build -f net10.0-android -t:Run, adb -e shell setprop debug.mono.env "MC_STATE=offline"
```
