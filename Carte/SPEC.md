# Carte — Uno Platform build spec

Source of truth: `kit/` (the Carte Uno build kit, unchanged). This spec maps the kit onto a compiled Uno app and records the decisions the kit leaves open.

Scope for this build: **Milestone one (fixture experience)** in full, plus the durable parts of **Milestone two** (persistence, responsive layouts, collision handling, keyboard and automation names). Capture and Review are clearly marked demo screens. No camera, OCR or model provider is implemented.

## Architecture Brief

**Structure**

| Project | Role |
| --- | --- |
| `Carte` | Uno single-project app, `net10.0-desktop` + `net10.0-browserwasm`, Skia renderer, Uno.Sdk 6.7.30 |
| `Carte.Core` | Kit starter, dependency-free `net10.0`. Kept intact; new files only: `FileMenuStore`, `TasteDescriber`, `PriceFormatter`, `MapLabelLayout` |
| `Carte.Core.Checks` | Kit console harness, extended with checks for the new core files |

**Decisions**

- Decision: MVVM (CommunityToolkit.Mvvm) with `x:Bind`.
  Reason: the kit's view-model contract is ICommand-shaped, state is synchronous toggles over one loaded document, and this is a sample build.
  Tradeoff: no `FeedView` for load/error; the single async load is handled by a small loading state on the session.
- Decision: Fluent base theme plus a Carte resource dictionary; no Material.
  Reason: Carte is a fixed bespoke identity (paper + ultramarine, three bundled families). The kit's resources target stock WinUI styles; Material would mean re-mapping MD3 roles and fighting its control templates.
  Tradeoff: diverges from the Material default; every Carte control style lives in `Themes/CarteResources.xaml`.
- Decision: one singleton `MenuSession` owns menu, shortlist, category, per-category selection, view mode and pairing context.
  Reason: Menu margins and Taste atlas are two views of the same state; Tonight must return to the same category, view and selection.
  Tradeoff: page view models are thin projections that subscribe to one `Changed` event.
- Decision: Uno.Extensions region navigation, routes `Explore` (default), `Tonight`, `Capture`, `Review`. Navigation only from view models via `INavigator`.
- Decision: `FileMenuStore` (core, testable) rooted at `ApplicationData.LocalFolder`; menu and shortlist are JSON files keyed by a sanitised menu ID, written atomically.
- Decision: strings stay in `Strings/en/Resources.resw` (neutral `en`, the template default) so every culture falls back to them. Kit content copied verbatim plus new keys.

**State model** — `MenuSession`: `Menu`, `Shortlist (TonightShortlist)`, `CategoryId`, `SelectedByCategory`, `View (Menu|Map)`, `PairFrom`, `IsLoaded`, `LoadError`. One `Changed` event; persistence after every shortlist change.

**Data flow** — packaged fixture → `MenuDocument.FromJson` (domain validation) → store → session → page view models → item presentation models (`MenuItemViewModel`) → XAML. Geometry stays in `TasteMapGeometry`; y is flipped exactly once, inside it.

**Platform constraints** — Skia desktop and WASM verified here; iOS/Android heads are deferred until camera capture (Milestone three). On WASM, `LocalFolder.CreateFolderAsync` is awaited before file IO so IDBFS is ready.

**Validation** — core checks (`dotnet run --project Carte.Core.Checks`), desktop build, Xvfb runtime captures at 412, 320, 768 and 1280 wide using DEBUG start-state hooks (`CARTE_*` env vars), WASM build.

## Design Brief

- **Direction**: a small printed restaurant guide. Paper `#F7F1E4`, ultramarine ink `#203BB6`, rules at 23 % ink, wash at 5.5 % ink. No other colours.
- **Typography** (tokens.json, production sizes): wordmark Instrument Serif italic 38/40; page title Instrument Serif 36/40; item title 28/32; body Carte Sans 16/24; secondary 14/20; metadata DM Mono 12/16 uppercase with wide tracking simulated by letter-spaced text (Skia ignores `CharacterSpacing`, so mono labels use plain uppercase); axis Instrument Serif italic 18/24; prices DM Mono tabular.
- **Spacing**: 4, 8, 12, 16, 24, 32, 48, 64. Page padding 24, 16 below 360 wide.
- **Layout**: header (wordmark + Scan menu) over a 1.5 ink rule; heading block; category tabs with a 2-unit ink underline on the selected tab; view bar (mono label + view switch); content; footer (tagline + Tonight with count badge).
- **Components**: `TasteMapView`, `MenuItemRow` (ListView template with `MiniTasteMap`), `ItemDetail`, category tabs, Tonight rows, review rows.
- **Glyphs**: Lucide SVGs, ink variant normally, paper variant inside the 44-unit ink disc when selected; dashed ring for low confidence.
- **Responsive**: < 600 single column; 600–904 centred column, max 640; ≥ 905 map or menu beside a 360-unit detail panel, content max 1120.

## Interaction Brief

- **Flows**: Explore (Menu margins first) ⇄ Taste map; select item → detail; Why here → source text + rationale; Save for tonight ⇄ Saved; Pair it → linked item in its category with a context line and a "Back to …" return; Tonight → remove, subtotal, tap a row to return to it; Scan menu → Capture (demo) → Read this menu (cancellable) → Review (demo) → Draw my taste map → Explore with the back stack cleared.
- **Input**: every glyph, row and action is a focusable control ≥ 44 × 44. Arrow keys move within the menu list; Tab follows menu order on the map.
- **Empty**: Tonight empty → "Your evening is still unwritten." + Keep exploring. Unplaced items → listed under the map with "Not enough detail to place this item yet."
- **Loading**: session load shows the heading with a quiet "Reading the menu…" line. Capture shows "Reading the menu…" during the simulated read.
- **Error**: invalid or unreadable fixture/store → service-failure copy with Try again.
- **Animation**: selection disc 160 ms opacity; press scale 0.96 instantly. When system animations are off, states change without transitions.
- **Feedback**: Save label and Tonight count update together; a polite live-region status line announces selection, save and remove.
- **Accessibility**: `AutomationProperties.Name` on every glyph in words ("Sea bass crudo, light and bold, estimated from the menu, $19"); low confidence adds "a less certain placement"; map and list are equivalent routes; focus stays on the triggering control after save, remove and category change.
- **Runtime verification**: launch under Xvfb with start-state env vars for each preview screen; compare against `kit/design/previews` at 412 wide; check 320/768/1280.

## Implementation Plan

1. Import kit, core, assets, strings; spec. *(commit)*
2. Core additions + checks. *(commit)*
3. Theme dictionary, `MenuSession`, routes, Explore skeleton; build + launch. *(commit)*
4. `TasteMapView`, menu rows, detail, pairing, responsive states; captures. *(commit)*
5. Tonight, Capture, Review; captures. *(commit)*
6. WASM build, acceptance checklist pass, README. *(commit, push)*

## Unresolved Questions

- Material vs bespoke Fluent base: confirm the bespoke choice is acceptable for this sample.
- iOS/Android heads: add now (unverifiable here) or with the capture milestone?
- Should per-category selection and view mode persist across restarts, or only the shortlist and menu (current choice)?
