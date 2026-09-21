# Morning Drive Card

A phone-width card a car app would show first thing in the morning. It answers one question at a glance: **when do I leave for school drop-off?** Everything else on the card (fuel range, cabin temperature, tires, where the car is parked) supports that answer.

Open `morning-card.html` in a browser. The tuning panel under the card ("Dial it in") maps every slider to a state value or a CSS variable, so the states and the feel can be dialed in live.

Built with the `frontend-design` skill layered with the `frontend-design-unique` ledger protocol, and the Interface Craft workflow by Josh Puckett (CSS-variable theming, storyboard motion, live tuning dials, a critique pass before shipping).

## Architecture Brief

- Structure: one self-contained HTML file. The card is `article.mc`; the tuning panel is `aside.dials` and is not part of the component.
- State model: a plain object `S` (clock, bell, base drive, traffic delay, walk-in time, range, cabin, tire, state, motion speed). `derive()` computes drive, leave-by, slack and arrival. `render()` is the single writer to the DOM.
- Data flow: dials write to `S` or to CSS variables on `:root`, then call `render()`. The card never reads from the dials.
- Services: none. Real data would come from the vehicle telemetry and a routing API; the shape of `S` is the contract.
- Platform constraints: phone width first (390 px), works at desktop width. Fonts load from Google Fonts with a real fallback stack.
- Validation: screenshots of live, dark, late, warn, offline, loading and empty states at 390 px; console clean.

## Design Brief

- Direction: frosted glass over an out-of-focus morning backdrop, matching the supplied mockup. Blue-biased ink, one route accent (dialable hue), semantic green, amber and red kept separate from the accent.
- Signature device: the slack ruler laid along the map route. Minutes become distance along the path: a dotted grey stretch for the slack (now to leave-by), a solid drive segment with the duration pill, an amber walk segment, and the school pin at the bell. Point labels sit above the path, duration labels below it.
- Type: Instrument Sans, one family. Scale: 11.5 / 12.5 / 13 / 14 / 15 / 16 / 23 / 42.
- Spacing: 22 px card padding, 18 px between sections, map panel 214 px tall with the ETA block overlaid top-left.
- Hierarchy: greeting and trip title with the leave-in sentence, car and status at right, the map with the ETA block, a three-column stats row (range with bar, charging, tire pressure), four round actions.
- Theme: full light and dark token sets, including map ground, roads, parks and water. Dark redefines tokens only.
- Responsive: single column at every width; the route SVG uses a fixed viewBox and slices to the panel.

## Interaction Brief

- Flows: read the leave-in sentence, glance at the route, then Lock, Climate, Send to car, or More.
- Headline logic: slack of 2 min or more shows "Leave in N min to arrive on time"; under 2 min shows "Leave now"; negative slack shows "Running N min late" with the arrival if you leave now, and the route turns red with no slack dots.
- Empty state: no drop-off (pedagogical day). The route and ETA hide over the map ground; car stats and actions stay.
- Loading state: skeleton in the ETA block, the map ground stays, the route hides.
- Error state: the subtitle says the car isn't reachable with a Retry link, the car status reads "Last seen 6:58", stats dim.
- Motion: one orchestrated entrance in trip order. Slack dots appear in sequence, the drive segment draws along the path (620 ms, ease-out quart), then the walk segment, then nodes, pin and pill pop in, then labels fade. Lock and Climate answer a press with a filled circle; Send to car confirms with "Sent". `prefers-reduced-motion` skips all of it.
- Accessibility: visible focus rings, the map SVG has a title, Lock and Climate use `aria-pressed`, the tire cell is a button.
- Verification: drag Clock past 7:33 for the late state; Range under 70 and Front-left tire under 30 for warnings; Traffic delay over 6 for heavy traffic; switch state chips.

## Unresolved Questions

- Should leave-by include a configurable walk-in buffer per school, or come from the routing API?
- The charging cell is static ("Not charging"); wire it to the plug state when the model gains one.
- Units follow the mockup (miles, °F); a locale switch would flip them.
- Port to Uno Platform XAML (a `MorningCard` control) if this direction is approved.
