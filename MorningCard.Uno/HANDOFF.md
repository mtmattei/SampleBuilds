# HANDOFF — MorningCard v5 Android form factors (step 0, baseline)
Updated: 2026-09-22 15:05

## UPDATE 2026-09-23 (supersedes the sections below where they disagree)

- The ruler and title-clip defects are **fixed** in `c658b2b` (`captures/android/fix-d420.png`).
- The user added `docs/design/v6/`, which is **the new target**. The diff against v5 is in
  `docs/design/v6/v6-vs-v5.md`: the phone card is redesigned with a full state set, and TV, Wear and
  Auto are unchanged v5 sources.
- **Next action:** get answers to D1–D11 in that file, then write the v6 section in SPEC.md
  (`write-spec`), then implement v6 step A (phone redesign) before the v5 split.

## Where we are

v5 (SPEC.md, last section) has started. The work lives in a **git worktree** so the card-polish
work on `spec/morningcard-uno` stays undisturbed:
`C:\Users\Platform006\UnoProjects\SampleBuilds-android` on branch `feat/android-form-factors`,
branched from `691c52f`. Step 0 (phone emulator baseline) is **half done**. The Android build
deploys and runs, and the light live capture is in `captures/android/live.png`. The first Android
run since v1 showed two defects:

1. **The route ruler is missing on Android.** There are no ticks, rings or now-cursor, and the map
   is not fitted to the route (it shows all of Montreal). `route.json` is packaged
   (`obj/.../assets/Assets/route.json`), so the fault is at runtime and not yet diagnosed. v4 plan
   step 6 (Android verification) was therefore never done.
2. **The title is clipped** to "School dro|": the car image overlaps it at 1080 px width.

Map tiles, fonts, the strip, the checks row and the action bar render correctly.

## Last verified state

- Build: `net10.0-android` Debug, 0 warnings, 0 errors (worktree). Desktop not rebuilt this session.
- Runtime: the phone card rendered on `chefs36` (API 36, 1080x2400); `live.png` shows it. Dark and
  the state set are not captured yet.
- Git: `feat/android-form-factors`, with this handoff and `captures/android/live.png` committed.
  The **main tree** (`SampleBuilds`, `spec/morningcard-uno`) holds another session's uncommitted
  edits to `MorningCardView.xaml`, `RouteRulerRenderer.cs` and `CardTokens.xaml`. Leave them alone;
  they belong to the card-polish work.

## Next actions (in order)

1. Boot `chefs36` and **finish the baseline**: dark, loading, empty, error, offline, leave-now and
   late. Drive the states with `debug.mono.env` (see the gotcha below), one short hook per launch.
2. **Diagnose the missing ruler.** `MC_TRACE` needs a full path, which is too long for
   `debug.mono.env`. Add a short hook (e.g. `MC_TRACE=1`, which resolves to
   `ApplicationData.Current.LocalFolder` in code), then read the file with `adb shell run-as
   com.companyname.morningcard cat files/...`. Suspects: `RouteGeometryLoader` asset read on
   Android, and the fit running before `MapControl` has a size (Mapsui navigator rule).
3. Fix the ruler and the title clip in their own `fix:` commits, then commit the full baseline
   (`chore: android baseline captures`). The plan's step 2 split needs a correct phone card to
   compare against.
4. v5 step 1 spikes R3, R4, R5, R6.

## Open questions

- Should the ruler and title fixes happen before step 1? Proposed: yes (see action 3). The user has
  not answered.
- TV, Wear and Automotive system images are not installed; steps 5, 6 and 8 need them. Ask before
  running `sdkmanager` (1–2 GB each).
- The `pixel_9_pro_xl_-_api_36_1` AVD cannot boot because its `android-36.1` Play Store image is
  missing. Use `chefs36`.
- Worktree tradeoff: the App MCP only registers in a session rooted in the folder with the `.sln`.
  Start sessions in `SampleBuilds-android\MorningCard.Uno`, or merge back before desktop parity
  (steps 5–6).
- v5 Unresolved Questions 1–7 in SPEC.md are still open.

## Gotchas found this session

- **A `debug.mono.env` value of 50 characters or more aborts the app at startup** (monodroid
  `string_base<50>`: `Attempt to store too much data in a buffer (capacity: N; exceeded by: 1)`,
  SIGABRT). It looks like an app crash. Keep each value short; `MC_THEME=dark` is fine.
- In Git Bash, set `MSYS_NO_PATHCONV=1` before `adb shell setprop ... /data/...`, or the path is
  mangled and `setprop` prints its usage text.
- `adb`/`emulator` are not on PATH: `$LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe`.

## Relaunch

```powershell
cd C:\Users\Platform006\UnoProjects\SampleBuilds-android\MorningCard.Uno
& "$env:LOCALAPPDATA\Android\Sdk\emulator\emulator.exe" -avd chefs36 -no-snapshot-save -no-boot-anim
dotnet build MorningCard/MorningCard.csproj -f net10.0-android -t:Run

$adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"
& $adb shell setprop debug.mono.env "MC_THEME=dark"   # '' to clear; under 50 chars
& $adb shell am force-stop com.companyname.morningcard
& $adb shell am start -n com.companyname.morningcard/crc649f50cfeacb723221.MainActivity
Start-Sleep 16; & $adb exec-out screencap -p > captures/android/dark.png
```

Never run `pm clear` on the Debug build: it deletes the Fast Deployment assemblies (see the rules
file).
