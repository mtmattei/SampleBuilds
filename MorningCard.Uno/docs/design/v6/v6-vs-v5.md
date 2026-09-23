# v6 design set vs v5 (2026-09-23, rev 2)

## Sources

| File | What it is |
|---|---|
| `school_dropoff_card_v6_bundle.html` | Claude Design standalone bundle (runtime, Geist woff2, car PNG inlined). **Opens in a browser; this is the source of truth.** |
| `*.html` (10) | per-page markup unpacked from the bundle; readable, and does not render on its own |
| `png/phone_*.png` | headless Edge renders of the seven phone pages, 640 wide, @1x |
| `png/android_tv.png`, `png/android_auto.png`, `png/wear_os.png` | the user's @1x exports; identical to the bundle renders |

Rev 1 of this file was written against an earlier export in which TV, Wear and Auto were still the
v5 artboards. In the bundle they are **redesigned in the v6 language**, which resolves D1, D4, D9 and
D11 of rev 1.

v5 baseline: SPEC.md v1-v5, `docs/artboards/*.png` (v5 surfaces), and the current
`MorningCardView` at `c658b2b`.

## One design language on every surface

| Token | Current phone / v5 surfaces | v6, all surfaces |
|---|---|---|
| Face | Instrument Sans / Geist-or-Roboto-Flex | **Geist** 400/500/600 (woff2 inlined in the bundle) |
| Ground | bokeh blobs (phone), blurred photo (TV, Auto) | flat `#E9E6DF` |
| Shell | glass `#9EFFFFFF` / `rgba(240,241,243,.9)` | paper `#F1EFE9` + inset top highlight, radius 26 (phone), 30 (TV), 32 (Auto) |
| Panel | `#FFFFFF` | `#FBFAF7`, ring `rgba(42,44,47,.05)` |
| Ink | `#1B2430` / `#111317` | `#2A2C2F`; 2nd `#55585C`, 3rd `#76787B`, caps `#94918A`, faint `#ADA9A1` |
| On time | `#2169E9` / `#0A66E8` | slate `#4A6B8A`, ink `#3F5C78` |
| Leave now | amber `#B5700D` | `#A6703F`, ink `#8A5A2E` |
| Late | `#D9453A` | `#9C4A3E`, ink `#8C4236` |
| Good | `#1F8A5B` / `#12A15F` | sage `#7E947A`, ink `#4F6B4C`, fill `#E4EBE1` / `#E8ECE3` |
| Walk, sun | orange `#F29A1F` | ochre `#B5824F`, ink `#8A6236`, fill `#F1E7DA` / `#F0EADF` |
| Drive fill | — | `#E1E8EF` |
| Primary | accent blue / `#15171B` pill | charcoal `#2B2D30`, radius 12 (no pills anywhere) |
| Chips, pills | fully rounded | radius 7 |
| Car | XC60 full colour / Outback full colour | Outback, `saturate(.3) contrast(.95)` on the phone |
| Dark | full dark palette (phone) | **not drawn** (D2) |

Status badges ("On time" + dots) are **gone from every surface**. The state lives in the headline
sentence and its tone colour, and on the ruler.

## Shared components, new in v6

- **Slack ruler** (phone, TV, Auto; compact on Wear): a fixed 7:20-7:56 window, one tick per minute,
  every fifth tick major. Segments: Past (hatched) · Slack "Slack · 9 min" (sage) · Drive
  "Drive · 14 min" (slate) · Walk (ochre) · After (hatched). LEAVE BY / ARRIVE / BELL values sit
  above their minute with drop lines, and the now dot and "NOW 7:24" caption sit under it. Late
  swaps ARRIVE to the right of BELL and hatches the overrun red. Labels shorten with segment width
  (≥7 min full, ≥3 short, else none; Wear draws no labels).
- **ETA block**: "14 min / DRIVE TO SCHOOL", traffic dot and line, "2 min faster than usual". The
  number is **drive only on every surface** (resolves D4).
- **Map callouts**: "Now 7:24" and "3 min walk" only. The v5 "14 min", "Bell 7:50" and
  "9 min slack" callouts moved onto the ruler.
- **Greeting**: "Good morning, Josh" over a weather line "−3° · Clear · High 9°". The weather pill is
  gone.
- **Footer vehicle line**: "● Outback · Locked". The "62 km range" suffix is gone; range lives only
  in the fuel row.
- **Fuel pill**: "Fuel this week" everywhere (resolves D11).

## Phone

| Region | Current | v6 |
|---|---|---|
| Header | sun, greeting, weather pill, overflow `⋯`, status strip | greeting over the weather line; "Now 7:24" right; **no overflow, no badge** |
| Title | title, chevron, headline, car + "XC60 · Locked" | title 34/40 w600, chevron; headline 17 with the emphasis at 27; car 156x82; lock line moves to the footer |
| Key times | three columns + strip | slack ruler, 104 px |
| Map | beside the ETA block; "17 min"; Open in Maps | full width, 280 tall, radius 16, scrim 97% → 0 at 62%; ETA top left; **Recenter FAB** + "Open in Maps ↗" |
| Checklist | collapsible list | outlined; header h52 with "3 checks" chip and "N of 3 ready"; rows are an **accordion** (cabin open by default) with per-row detail |
| Cabin | Suggested + Start warming | Suggested → ● Warming → **Skipped** (struck through); Start warming + Skip, then **Undo**; skipped counts as ready |
| Footer | status dot + "On time"; lock icon button; Start drive | "● Outback · Locked"; labelled **Unlock**; Start drive |

### Phone states

| State | Current | v6 |
|---|---|---|
| Leave now | badge + amber | "Leave **now** to make the bell."; LEAVE "Now"; no slack; "Traffic is clear / Leaving now still makes the bell" |
| Running late | "Running N min late. Leave now to arrive 7:51." | "Running **4 min late** for the bell."; ARRIVE 7:51 in late ink after BELL; overrun hatch; checklist open |
| Loading | FeedView progress | in-place skeleton (headline, value, ruler and ETA bars; map without route); "Checking…"; "Updating…"; Start drive disabled, Unlock enabled |
| ETA unavailable | whole-card error + Retry | partial: "Times from your saved schedule." under the headline; ruler and route still drawn; ETA slot reads "Live ETA unavailable / Traffic data didn't load / Retry" |
| No drop-off | "This morning" / "Nothing on the calendar before 9." | "No drop-off today" / "School is closed. Next run tomorrow, leave by **7:33**."; no ruler, no route; "Route shows tomorrow"; "Ready for tomorrow"; Start drive disabled |
| Car offline | "Car isn't reachable, showing {0} data." | fuel "Last read 6:10" + "No signal"; cabin "Car offline" + explanation, actions disabled; footer "Offline · last seen 6:10"; Unlock **and Start drive** disabled |
| Whole-card error, device offline, dark | exist | **not drawn** (D7, D2) |

## TV (1280x720) vs v5 `android_tv.png`

- Layout unchanged: header, trip panel (560 wide) over the checklist on the left, map panel on the
  right, footer.
- Title 36 w600 (v5 38 w700); headline 19 with the emphasis at 30; ruler caps 11 / values 21.
- Checklist always expanded, no accordion; cabin sub "Outside is −3°" (v5 "· to 20°"); Start warming
  and Skip at h40 r10. **Focus ring is a slate double ring** (v5 white + blue, scale 1.06).
- Map ETA 58/24 "14 min" + "DRIVE TO SCHOOL" 18 caps (v5 "23 min / Arrive 7:47").
- Footer Unlock / **Send to car** at h48 r12 (v5 pills r24).

## Wear (454 round) vs v5 `wear_os.png`

1. Greeting 16 + weather 13, title 26 (v5 30), **"Leave in 9 min" on one line, 17 with the number
   at 38** (v5 stacked with a 68 number), "to arrive on time." 15, compact ruler with no segment
   labels, Start drive on the bottom edge.
2. ROUTE caps, map, ETA "14 min / DRIVE TO SCHOOL / Light traffic" (v5 23 / Arrive 7:47), Open in
   Maps h48 r14.
3. "Before you go" 22, "3 checks · 2 of 3 ready" (**the dots are gone**), fuel "Fuel this week",
   cabin highlighted "Suggested · tap to start", parking scaled down and faded as the next item.
4. Car, "● Outback / Locked" (v5 "Locked · 62 km range"), **no weather pill** (moved to screen 1),
   Unlock h52 r14, Start drive.

## Auto (1440x640, parked) vs v5 `android_auto.png`

- Panel: greeting + weather, "Now 7:24" right; title 30; headline 18/28; ruler (caps 10, values 20).
- Checklist expanded with an up-chevron; cabin **Start** (h48 r12) + icon-only Skip; footer
  "● Outback · Locked" + Start drive h60 r14.
- Map: ETA 44/18 top right of the panel; callouts Now + walk only; **zoom +, −, recenter unchanged**.
- Rail: selected drop-off tile in slate.
- v5 inventory 36 still stands: a POI app gets host-drawn pane rows, so the panel styling, **the
  ruler**, the car image and the inline Start/Skip cannot appear in the car. The v6 panel is even
  further from what the host allows (D12).

## Decisions v6 reverses or reopens

| v5 decision | v6 | Effect |
|---|---|---|
| C: keep the XC60 | Outback on every surface, one image | **reopens C** (D3) |
| B4: map locked | Recenter on the phone; zoom + recenter in Auto | **reopens B4** on the phone (D5); car already open (v5 UQ3) |
| Skip undefined | Skipped state + Undo on the phone | **resolves v5 UQ4** for the phone; TV/Auto/Wear follow the same model |
| Overflow menu (Refresh, Open in Maps, Replay) | none | Refresh and Replay lose their entry point (D6) |
| ETA = door to bell (17); surfaces 23 | 14, drive only, everywhere | resolved (was D4) |
| Status badge | removed everywhere | state is carried by sentence + tone + ruler; still not colour-only, because the sentence names it |
| v5 ramp table (TV 54/27/81, Wear 30/68/40) | superseded | new ramp below |

### v6 type ramp (px at artboard scale)

| Role | Phone | TV | Wear | Auto |
|---|---|---|---|---|
| Greeting / weather | 15 / 16 | 20 / 15 | 16 / 13 | 18 / 14 |
| Title | 34 | 36 | 26 | 30 |
| Headline / emphasis | 17 / 27 | 19 / 30 | 17 / 38 | 18 / 28 |
| Ruler caps / values | 10 / 19 | 11 / 21 | 11 / 17 | 10 / 20 |
| ETA number / unit | 30 / 15 | 58 / 24 | 34 / 15 | 44 / 18 |
| Row title / meta | 15.5 / 13 | 19 / 14 | 19 / 15 | 19 / 14 |
| Primary action h | 40 | 48 | 100 (edge) | 60 |

## Capability inventory delta

| # | Capability | Status | Note |
|---|---|---|---|
| 38 | Slack ruler | to implement | one control, four sizes; XAML star-column grid, or `SKCanvasElement` for hatches and ticks |
| 39 | Muted map | substitution candidate | the artboard map is drawn; keep Mapsui + OSM and tint the tiles (D10) |
| 40 | Recenter (phone) | to implement if B4 reopens | re-run the existing fit |
| 41 | Cabin Skip / Undo | to implement | `Cabin { Pending, Warming, Skipped }` replaces the bool |
| 42 | Checklist accordion (phone) | to implement | one expanded-row key in state |
| 43 | Partial ETA error | to implement | `EtaView.Failed` exists; move it out of the whole-card error into the ETA slot |
| 44 | Skeleton loading | to implement | per-region placeholders (`uno-component-states`) |
| 45 | Car-offline degradation | to implement | `IsStale` exists; add last-seen time and disabled actions |
| 46 | Geist | to implement | static TTFs per weight, underscore filenames (Android asset rule) |
| 47 | Outback image | to implement if D3 | the bundle carries a 99 KB PNG; name it `outback.png` |

## Unresolved questions

- **D2 Dark.** v6 draws no dark theme. Derive a v6 dark palette, or drop dark on the phone and TV?
  (The car still needs a night treatment, v5 UQ1.)
- **D3 Car.** Switch to the Outback everywhere (image, name, alt text), reversing decision C?
- **D5 Recenter on the phone.** Honour it, which means unlocking pan and zoom (reopens B4), or omit it?
- **D6 Overflow removed.** Where do Refresh and Replay go: pull-to-refresh plus the Simulator, or a
  small overflow kept against the design?
- **D7 Missing states.** Whole-card error and device offline are not drawn. Derive them from the
  ETA-unavailable and car-offline treatments?
- **D8 Start drive while the car is offline.** v6 disables it; navigation does not need the car.
  Keep it enabled?
- **D9 Greeting.** "Good morning, Josh" is canonical (5 of 7 phone pages and all surfaces). Two
  artboard defects to correct, not copy: the phone weather line has a stray leading comma at 13 px
  where the clean pages use 16 px, and leave-now/running-late omit the name. Also: where does "Josh"
  come from? It is a new profile field (simulated in this sample).
- **D10 Map style.** Tint real OSM tiles toward the muted palette, or leave them stock?
- **D12 Auto panel.** The v6 Auto artboard is a custom panel that the Car App Library does not allow
  for a POI app. Keep v5's mapping (host rows + pane actions, ruler omitted) and record the ruler as
  an omission?
- **D13 No drop-off Recenter.** The artboard keeps Recenter and Open in Maps on a map with no route.
  Hide both there?

## Proposed effect on the plan

- Step 0 (baseline): finish light plus the drawn states only; dark only if D2 keeps it.
- **v6 step A, phone redesign, before the v5 split:** tokens + Geist, header/footer, slack ruler,
  full-width map, checklist accordion + Skip/Undo, then the four v6 states. Build the ruler and the
  ETA block as standalone parts from the start, because TV, Wear and Auto reuse them.
- The v5 part list gains `SlackRuler`; `TripPanel` loses the strip. Rewrite the v5 ramp dictionaries
  from the table above.
- Parity targets for steps 5-8 become `png/android_tv.png`, `png/wear_os.png` and
  `png/android_auto.png`, replacing `docs/artboards/*`.
