# v6 design set vs v5 (2026-09-23)

Source: `docs/design/v6/*.html`, Claude Design component exports. The runtime script and image
assets were not exported, so the files do not render in a browser; this diff is read from markup and
the embedded state script. v5 baseline: SPEC.md v1-v5 plus the current `MorningCardView` (commit
`c658b2b`).

## Scope of the change

| Artboard | vs v5 | Weight |
|---|---|---|
| `on-time`, `leave-now`, `running-late` | **Phone card redesigned**: new palette, new face, new ruler, new header and footer | major |
| `loading`, `eta-unavailable`, `no-dropoff`, `car-offline` | **Phone state set, now specified** (v5 had captures only, no artboards) | major |
| `tv.html` | HTML source of `docs/artboards/android_tv.png`; same content and layout | none |
| `wear.html` | HTML source of `wear_os.png` | none |
| `auto.html` | HTML source of `android_auto.png` | none |

The TV, Wear and Auto artboards are unchanged from v5 and still use the **v5 palette** (`#0A66E8`
blue, `#F29A1F` orange, `#111317` ink). The phone moved to a muted palette. See D1.

## Phone: tokens

| Token | Current (v5 light) | v6 |
|---|---|---|
| Face | Instrument Sans | **Geist** (400/500/600/700; `@font-face` stripped from the export) |
| Shell | glass `#9EFFFFFF` over bokeh | opaque paper `#F1EFE9`, radius 26, padding 8 |
| Panel | `#FFFFFF` | `#FBFAF7`, radius 20, 1px ring `rgba(42,44,47,.05)` |
| Ground | bokeh blobs | flat `#E9E6DF` (no bokeh) |
| Ink | `#1B2430` | `#2A2C2F`; secondary `#55585C`, tertiary `#76787B`, caps `#94918A` |
| Accent (on time) | `#2169E9` | slate `#4A6B8A` / ink `#3F5C78` |
| Leave now tone | amber `#B5700D` | `#A6703F` / ink `#8A5A2E` |
| Late tone | danger `#D9453A` | `#9C4A3E` / ink `#8C4236` |
| Good | `#1F8A5B` | sage `#7E947A` / ink `#4F6B4C` |
| Walk / sun | orange / `#E8912B` | ochre `#B5824F`, walk ink `#8A6236`, walk fill `#F1E7DA` |
| Primary button | accent blue | charcoal `#2B2D30`, radius 12, h 40 |
| Dark theme | full dark palette | **none drawn** (D2) |
| Car image | full colour | `saturate(.3) contrast(.95)`, multiply blend, 156x82 |

## Phone: layout, top to bottom

| Region | Current | v6 |
|---|---|---|
| Header | sun, "Good morning", weather pill, overflow `⋯`; status strip "Now 7:24 · On time" | sun, "Good morning" over a second line "−3° · Clear · High 9°" (16px, no pill); right: "Now 7:24". **No overflow, no status badge.** |
| Title block | title, chevron, headline, car + "XC60 · Locked" under it | title 34/40, chevron, headline 17 with the emphasis at 27 (e.g. **9 min**); car only, **lock line moves to the footer** |
| Key times | LEAVE BY / ARRIVE / BELL columns + a dotted/blue/amber strip | **New slack ruler** (104 px): fixed 7:20-7:56 window, one tick per minute (major every 5); segments Past (hatched) · Slack "Slack · 9 min" (sage) · Drive "Drive · 14 min" (slate) · Walk (ochre) · After (hatched); Late adds an overrun hatch in red over 7:50-7:54. LEAVE BY / ARRIVE / BELL sit **on the ruler at their minute**, with 1 px drop lines; now dot + "NOW 7:24" caption under it. Labels shorten by segment width (≥7 min full, ≥3 short, else none). |
| Map | half-width map panel beside the ETA block ("17 min", traffic); ruler overlay; "Leave 7:33" + "Bell 7:50" callouts; Open in Maps | **full-width map, 280 tall**, radius 16; white scrim left to right (97% → 0 at 62%); ETA top-left: **"14 min / DRIVE TO SCHOOL"**, traffic dot + line, sub-line; callouts "Now 7:24" and "3 min walk" only; bottom right: **Recenter FAB** + "Open in Maps ↗" pill |
| Checklist | collapsed header: "Before you go", "3 of 3 ready", chevron; rows below | outlined container, header h 52: icon, "Before you go", "3 checks" chip, "N of 3 ready", chevron (collapsed by default). Rows are an **accordion** (one open, cabin open by default), each with its own chevron and detail |
| Fuel row | Enough for today | "62 km of range" / "Enough for today", pill **"Fuel this week"** (TV and Auto say "Fuel soon") |
| Cabin row | Suggested + Start warming | pill Suggested → **● Warming** → **Skipped**; detail "Warm to 20° so the car is ready when you leave at 7:33." + "Remote start" chip; **Start warming + Skip**, then **Undo** once decided. Skipped strikes the title through. Ready count 2 while pending, 3 once warming *or skipped*. |
| Parking row | Rue Rachel Est / Until 9:30 | same, plus detail "Street parking on Rue Rachel Est. No parking from 9:30, so drop-off only." |
| Footer | status dot + "On time"; icon-only lock button; Start drive | "● Outback · Locked" (pulsing live dot); **"Unlock" labelled ghost button**; charcoal "Start drive". Status text is gone from the footer too. |

## Phone: states

| State | Current | v6 |
|---|---|---|
| On time | exists | redesigned as above |
| Leave now | badge + amber | headline "Leave **now** to make the bell."; LEAVE shows "Now"; no slack segment; traffic "Traffic is clear / Leaving now still makes the bell" |
| Running late | "Running N min late. Leave now to arrive 7:51." | "Running **4 min late** for the bell."; ARRIVE 7:51 in late ink; overrun hatch on the ruler; checklist open |
| Loading | FeedView progress | **in-place skeleton**: headline bars, value bars under each caps label, one bar for the ruler, map tiles without route, ETA bars; checklist "Checking…"; footer "Updating…"; Start drive disabled |
| ETA unavailable | whole-card error panel + Retry | **partial**: the card renders, title adds "Times from your saved schedule."; map shows "Live ETA unavailable / Traffic data didn't load / Retry" in the ETA slot |
| No drop-off | title "This morning", "Nothing on the calendar before 9." | title **"No drop-off today"**, "School is closed. Next run tomorrow, leave by **7:33**."; ruler hidden; map without route, "Route shows tomorrow"; checklist "Ready for tomorrow"; Start drive disabled |
| Car offline | "Car isn't reachable, showing {0} data." | per-row: fuel "Last read 6:10" + "No signal"; cabin pill "Car offline" + explanation, Start/Skip disabled; footer "Offline · last seen 6:10"; **Unlock and Start drive disabled** |
| Whole-card error | exists | **not drawn** |
| Device offline | exists (`offline` capture) | **not drawn** (car-offline is a different thing) |
| Dark | exists | **not drawn** |

## Decisions v6 reverses or reopens

| v5 decision | v6 says | Effect |
|---|---|---|
| C: keep the XC60 | car is "Silver Outback" on every surface | **Reopens C** (D3) |
| B4: map locked | Recenter FAB on the phone | **Reopens B4 on the phone**; recenter implies pan/zoom is possible |
| v3 Q: Skip has no meaning | Skip → "Skipped" state, counts as ready, Undo returns to pending | **Resolves v5 UQ4** on the phone; TV/Auto still show Skip without the Skipped visuals |
| Overflow menu (Refresh, Open in Maps, Replay) | no overflow | Refresh and Replay lose their entry point (D6) |
| ETA = door to bell (17) | ETA = drive only (14); TV/Wear/Auto show 23 | **Three numbers on four surfaces** for "how long" (D4) |
| Status badge "On time" | not rendered on the phone (defined in the script, unused) | status carried by tone + sentence only; fine for colour-blind users only because the sentence names it |

## Capability inventory delta

| # | Capability | Status | Note |
|---|---|---|---|
| 38 | Slack ruler (minute-scale timeline) | to implement | replaces the strip; XAML Grid of star columns, or `SKCanvasElement` for the hatches and ticks |
| 39 | Map as OSM tiles styled muted | substitution candidate | the artboard map is a stylised drawing; keep Mapsui (real map) and tint tiles with a colour filter to reach the muted look. Raise in UQ |
| 40 | Recenter | to implement if B4 reopens | Mapsui navigator re-fit (`CenterOnAndZoomTo` / the existing fit) |
| 41 | Cabin Skip / Undo | to implement | new model state: `Cabin { Pending, Warming, Skipped }` replaces the bool |
| 42 | Per-row accordion | to implement | one expanded row key in state |
| 43 | Partial ETA error with Retry in the map | to implement | `EtaView.Failed` already exists; move it out of the whole-card error |
| 44 | Skeleton loading | to implement | per-region placeholders in the templates (see `uno-component-states`) |
| 45 | Car-offline per-row degradation | to implement | `IsStale` exists; needs last-seen time and disabled actions |
| 46 | Geist font | to implement | static TTFs per weight, underscore filenames (Android asset rule) |

## Unresolved questions

1. **D1 Palette split.** The phone is muted slate/ochre; TV, Wear and Auto are still v5 blue/orange.
   Restyle those three to the v6 palette, or keep two palettes on purpose?
2. **D2 Dark theme.** v6 has no dark artboards. Derive a dark v6 palette ourselves, or drop dark on
   the phone? (The car still needs a night treatment either way, v5 UQ1.)
3. **D3 Car.** Switch to the Outback (new image + name everywhere), or keep the XC60 as decision C
   said?
4. **D4 ETA semantics.** Phone map shows 14 (drive), TV/Wear/Auto show 23 (now to arrival), current
   phone shows 17 (drive + walk). Pick one number and its caption for all surfaces.
5. **D5 Recenter on phone.** Honour it (unlock pan/zoom, reopen B4) or omit the FAB?
6. **D6 Overflow removed.** Refresh and Replay lose their entry point. Move Refresh to pull-to-refresh
   or the header, and Replay to the Simulator, or keep a small overflow?
7. **D7 Missing states.** Whole-card error, device offline and dark are not drawn. Derive them from
   the v6 language (proposed: error = ETA-unavailable treatment plus a disabled checklist), or keep
   the current ones?
8. **D8 Start drive while the car is offline.** v6 disables it, but navigation does not need the
   car. Keep it enabled?
9. **D9 Greeting.** `on-time.html` says "Good morning, Josh" with a stray leading comma on the
   weather line and a 13px weather line; the other six say "Good morning" at 16px. Treat the six as
   canonical?
10. **D10 Map style.** Tint real OSM tiles toward the artboard's muted palette, or leave them stock?
11. **D11 Fuel pill copy.** Phone "Fuel this week" vs TV/Auto "Fuel soon".

## Proposed effect on the v5 plan

- Step 0 (baseline) is still worth finishing, since it documents the pre-v6 card, but only light plus the
  four states v6 draws. Dark only if D2 keeps it.
- Insert **v6 step A (phone redesign)** before the v5 split (step 2): tokens and face, header,
  slack ruler, full-width map, checklist accordion + Skip/Undo, footer, then the four v6 states.
  Splitting the old card first would mean splitting it twice.
- TV/Wear/Auto steps (5-9) wait on D1.
