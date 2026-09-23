# HANDOFF — MorningCard Android heads (TV, Wear, Auto)
Updated: 2026-09-23

## Where we are
The v6 phone card is done (steps 1-9, parity of at least 89%). All three extra Android heads are now built into **one APK**:
TV (`TvPage`, 1280x720 in a Viewbox, D-pad focus, leanback banner), Wear (`WatchPage`, 454-unit Viewbox,
static card template) and Auto (`Platforms/Android/Car`, POI `PlaceListMapTemplate`). The single APK and
the host-drawn car map both depart from v5; see SPEC.md "v7 Android heads as built".

## Last verified state
- Build: `net10.0-desktop` and `net10.0-android` Debug, 0 warnings, 0 errors. Tests 74/74.
- Runtime: TV verified on the `mc_tv` AVD (layout, D-pad, focus ring, initial focus). Wear verified on `mc_wear`
  (every section, the swipe fade fixed). Auto: the service resolves on-device through `query-services`, and
  the app still runs on the shared hub. **The Auto head has not been run in the DHU** (no Android Auto host on these emulators).
- Git: `edc4b32` feat: Android Auto head (after `e484baa` Wear and `165c47b` TV). The only uncommitted files are the old `captures/android/*` files.
- Lint: last run at the uno-audit, all gating rules 0. Not re-run after the heads.

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
| toolkit-chip: status pills as Chips, sized by scoped lightweight keys | MorningCard/Controls/MorningCardView.xaml (UserControl.Resources `ChipHeight`) |
| uno-audit: semantic lint gating 0, dead code removed | ac27715 |
Read, not applied: none.

## Next actions (in order)
1. Run the Auto head in the DHU with a Pixel (steps in Relaunch).
2. Re-run the semantic lint over `Controls/Parts`, `TvPage` and `WatchPage`.
3. Decide on the v7 open points: whether to split, getting our route drawn in the car, and the host allow-list.

## Open questions
- Is one APK acceptable, or should the Wear app be standalone (the v5 B5 split)?
- Should the car get our route overlay (the navigation category plus a `SurfaceCallback`) or keep the host map?

## Relaunch
```powershell
cd C:\Users\Platform006\UnoProjects\SampleBuilds-android\MorningCard.Uno   # root the session here for uno-app
dotnet build MorningCard/MorningCard.csproj -f net10.0-desktop
$env:APP_NO_HOTDESIGN="1"; $env:MC_STATE="loading"   # or error | empty | offline; MC_CLOCK=457 for late
Start-Process MorningCard\bin\Debug\net10.0-desktop\MorningCard.exe -WorkingDirectory MorningCard\bin\Debug\net10.0-desktop
pwsh -File scripts\Capture-Tall.ps1 -ProcessId <pid> -Out captures\x.png -Width 640 -Height 1200
# Auto (DHU): Pixel -> Android Auto settings -> tap version 10x -> Start head unit server
#   adb forward tcp:5277 tcp:5277; & "$env:LOCALAPPDATA\Android\Sdk\extras\googleuto\desktop-head-unit.exe"
# TV/Wear: emulator -avd mc_tv | mc_wear; dotnet build -f net10.0-android -t:Run -p:AdbTarget="-s emulator-XXXX"
# Android: boot chefs36, dotnet build -f net10.0-android -t:Run, adb -e shell setprop debug.mono.env "MC_STATE=offline"
```
