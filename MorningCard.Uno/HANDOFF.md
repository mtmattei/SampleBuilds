# HANDOFF — MorningCard Uno Platform build
Updated: 2026-09-21

## Where we are
The Morning Drive Card HTML prototype (`../MorningCard/morning-card.html`) has a full Uno Platform spec in `SPEC.md`, verified against the Uno docs MCP and the uno-platform skills, with all ten design questions resolved by the user. Implementation Plan step 1 (scaffold) is done: Android + desktop solution on Uno.Sdk 6.7.30, MVUX, Material, Toolkit, Navigation, Localization (English only), ThemeService, Skia renderer, plus empty `MorningCard.Core` and `MorningCard.Tests` (xunit) projects. The app still shows the template "Hello Uno Platform!" page. No card code exists yet.

## Last verified state
- Build: pass, `net10.0-desktop` and `net10.0-android`, Debug, 0 warnings, 0 errors.
- Runtime: desktop exe launched with `APP_NO_HOTDESIGN=1`, window shown, template page captured. Not verified through uno-app (that MCP was not loaded; the session was rooted elsewhere). Android built, never launched: no device or emulator was attached.
- Git: repo `SampleBuilds`, branch `spec/morningcard-uno`, local only, nothing pushed. Last commit is the scaffold commit.

## Next actions (in order)
1. Confirm `uno-app` MCP is loaded (`uno_health`). If only docs tools exist, the session is rooted in the wrong folder.
2. Start an Android emulator or attach the Pixel 8, launch the template app once. Day-one smoke of every head is a spec rule.
3. SPEC.md Implementation Plan step 2: run spikes R2, R4, R5, R7, R9, R10, R11 (time boxes in the Risks table) and write results back into the spec. R1 (Mapsui) runs at step 14.
4. Step 3: Core (records, `TripMath`, `RouteLayout`, service contracts, `SimulatedDriveHub`, tests). Then continue the plan in order; drive one error and one retry (step 8) before building stats and actions.

## Open questions
- SPEC.md Unresolved Questions 1 to 3: title chevron as the Trip page entry point (assumed yes), sample route area (assumed Montreal West Island), OSM tiles as the tile source (assumed yes). None blocks steps 2 to 13.
- The scaffold emits no `UseThemeSwitching()` despite `-theme-service`; add it in step 5 and confirm `IThemeService` resolves.

## Relaunch
```powershell
cd C:\Users\Platform006\UnoProjects\SampleBuilds\MorningCard.Uno
claude            # session root must be this folder so .mcp.json registers uno-app
# build:  dotnet build MorningCard\MorningCard.csproj -f net10.0-desktop
# run:    uno_app_start (preferred), or set APP_NO_HOTDESIGN=1 and start bin\Debug\net10.0-desktop\MorningCard.exe
# before any rebuild: uno_app_close, then Get-Process MorningCard | Stop-Process -Force
```
