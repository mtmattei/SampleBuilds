# HANDOFF — MorningCard visual pass (slack ruler, glass shell, type and spacing systems)
Updated: 2026-09-25

## Where we are
A long, user-driven visual pass on the card:
- **Slack ruler:** restyled as time blocks (sage slack, pale-blue drive, clay walk, hatched ends). It now carries the card's answer: the callout reads "Leave in 9 min" / "Time to go. Leave now" / "Running N min late", and the sentence under the title shows only for offline and no-trip mornings. Every ruler label sits above the track via `RulerLabelPanel`.
- **Shell and ground:** frosted grey glass shell (Skia-native `AcrylicBrush`) on a dark slate ground with fixed film grain (`GrainCanvas`).
- **Geometry:** concentric corners (shell 28 → white card and buttons 12 → map and checklist 4), and one spacing scale (16 / 12 / 8).
- **Type:** a 7-step type scale (11 / 12 / 14 / 18 / 22 / 28 / 34) with fixed weight roles.
- **Map:** the drive route is drawn in the ruler Drive block's pale fill, cased in the tone colour, including leave-now and late.
- **Adaptive bits:** a Phosphor walking icon replaces "Walk" when it doesn't fit (`FitOrFallbackPanel`). The headline drops under the car when it wouldn't fit beside it (`HeadlineLayoutPanel`).

**One change is uncommitted:** `RulerLabelPanel` now slides colliding labels sideways before lifting them a tier. This stops the band growing and shrinking by about 44 px as the clock runs. It builds, but has not been seen on screen.

## Last verified state
- **Build:** `net10.0-desktop` Debug, 0 warnings, 0 errors. Android not built this session. Tests: 91 passed (last run mid-session; Core untouched since).
- **Runtime:** uno-app MCP was disconnected all session. Verified by `PrintWindow` captures (`scripts/Capture-Tall.ps1`) at 620 and 380 px, light theme only. Late (`MC_CLOCK=460`), leave-now (`453`) and on-time captured for commit `777e5b7`. **Not verified: dark theme, Android, the uncommitted slide-first panel.**
- **Git:** `spec/morningcard-uno`, last commit `777e5b7`. Uncommitted: `MorningCard/Controls/RulerLabelPanel.cs`. Not pushed this session.
- **Lint:** CARD 18 · TOKENTHEME 4 · CODEBEHIND 4 · BUILTIN 1 · HEX 0 · BACKBAR 0 · OVERLAY 0 · RESPONSIVE 0.

## Guidance applied
| Skill / rule / decision | Applied at |
|---|---|
| uno-scaffolding gotcha: Skia-native acrylic path (the Composition effect brush paints nothing) | MorningCard/Themes/CardTokens.xaml:35 |
| skiasharp-uno: cached shader, alpha baked into pixels | MorningCard/Controls/GrainCanvas.cs:46 |
| memory: real icon libraries (Phosphor via Fetch-Icons.ps1) | MorningCard/Themes/Icons.xaml:37, MorningCardView.xaml:330 |
| uno-material: per-theme ThemeResource tokens, no inline hex | MorningCard/Controls/MorningCardView.xaml:298 |
| Decision: concentric radii | MorningCard/Themes/CardTokens.xaml:221 |
| impeccable:typeset: one scale, sizes only in TextStyles | MorningCard/Themes/TextStyles.xaml:124 |
| Toolkit StatusBar Light on dark ground | MorningCard/Presentation/MainPage.xaml:8 |
| Decision: content-fit panels over window breakpoints | MorningCard/Controls/FitOrFallbackPanel.cs:14, RulerLabelPanel.cs:114 |

Read, not applied: `{utu:Responsive}` (zero uses; a candidate for the narrow-width fixes below).

## Next actions (in order)
1. **Verify the uncommitted `RulerLabelPanel`.** Reconnect uno-app (`/mcp`), drag the simulator clock across the timeline at 620 and 380 px, confirm the band stays one row, then commit. **Do not run window relaunch sweeps**: the user objected to windows taking over the screen. Launch once, or use the MCP.
2. **Dark theme pass.** New since it was last run: glass shell, slate ground, ruler tokens, clay walk, `Ink3` retune, route fills. The map stays pinned to light, so the route won't match the ruler's dark Drive tint.
3. **Android run.** Checks: the acrylic path on Android (the `Border` must stay childless), status bar icons now `Light`, and the grain canvas.
4. **Narrow widths (380 px):** the title shows only "Sch…" and "XC60 · Locked" truncates. Candidate: shrink the car below 600 px with `{utu:Responsive}`.
5. **Update the SPEC.md design section** with this pass: ruler anatomy, glass shell, spacing, radius and type scales. The peer session owns the v5 section at the end of SPEC.md, so re-read it first.

## Open questions
- **Callout size:** the answer now sits in a 14 px callout; the countdown was 28 px before. Make it larger (22, like the times)?
- **Clay does two jobs:** it means both "walk" and "soon", so in leave-now a tan drive sits beside the clay walk, on the ruler and on the map.
- **Warm and cool mix:** the white card is still warm paper inside a cool grey shell.
- **Simulator state carries over between launches:** a +20 min traffic delay appeared on its own. Is that intended?
- **Lint gating counts:**
  - CARD: the ruler segments are covered by a range-level `allow` comment, which the lint doesn't honour per element.
  - TOKENTHEME: converter keys, which are false positives.
  - CODEBEHIND and BUILTIN: pre-existing.
  - Decide whether to add per-element `allow` comments.
- **Unused tokens:** `FrameBrush` / `FrameColor` no longer paint anything. Delete?

## Relaunch
```
cd C:\Users\Platform006\UnoProjects\SampleBuilds\MorningCard.Uno
dotnet build MorningCard/MorningCard.csproj -f net10.0-desktop
# Preferred: session rooted here, uno-app MCP connected, uno_app_start.
# Fallback (one launch only): $env:APP_NO_HOTDESIGN='1'; start MorningCard\bin\Debug\net10.0-desktop\MorningCard.exe
# State hooks (DEBUG): MC_CLOCK (460 = late, 453 = leave now), MC_DELAY, MC_THEME, MC_STATE
```
