# Carte milestones and acceptance checks

## Milestone one Fixture experience

- [ ] The app builds and launches on the chosen initial Uno target.
- [ ] All 18 fixture items and their menu prices appear once in the correct category.
- [ ] Menu and Map views share selection and shortlist state.
- [ ] Category changes display the matching axis labels and item positions.
- [ ] The JSON y direction matches the map; high values appear near the top.
- [ ] Why here reveals both the original description and rationale.
- [ ] Low-confidence Syrah has a dashed ring and an accessible uncertainty label.
- [ ] Saving twice toggles the item without duplicates; removal updates the count.
- [ ] Three fixture choices yield the expected known-price subtotal.
- [ ] Pairing selects the linked wine and keeps a route back to its source dish.

## Milestone two Responsive and durable behavior

- [ ] Shortlists and reviewed menus survive a restart and remain scoped to menu ID.
- [ ] 320, 390, 768 and 1280-unit layouts remain usable.
- [ ] Larger text wraps without losing actions or prices.
- [ ] Touch targets are at least 44 by 44 and do not overlap.
- [ ] Dense points use label offsets, clustering or a list fallback.
- [ ] Unknown placements remain listed; they are not plotted at zero.
- [ ] All items and controls are reachable by keyboard and screen reader.
- [ ] Focus survives selection, save/remove and sheet closure.
- [ ] Reduced motion and high contrast work on the actual platform.
- [ ] No remote resources are required to display a saved menu.

## Milestone three Capture and extraction

- [ ] Camera permission denied and camera unavailable states offer photo picking.
- [ ] Multi-page capture preserves order and permits retaking or removing a page.
- [ ] Duplicate or overlapping photos do not duplicate menu rows.
- [ ] Unclear names and prices can be corrected before analysis.
- [ ] Unknown currency requires confirmation; missing price remains missing.
- [ ] Cancellation, offline, retry and late-response behavior are deterministic.
- [ ] Source text and photo references remain available for explanations.

## Milestone four Taste analysis

- [ ] Output passes JSON Schema and domain validation before reaching the UI.
- [ ] A selected axis set remains fixed while filtering and selecting.
- [ ] Explicit axis changes create a new revision and invalidate old placements.
- [ ] Unsupported item evidence yields null or low-confidence placement.
- [ ] No ingredient, price, allergen or alcohol-content claims are invented.
- [ ] Pairing links resolve to actual items on the same menu.
- [ ] Human review across diverse real menus checks whether the chosen dimensions help users choose.

## Build handoff evidence

Record SDK and package versions, built targets, device or browser versions, test results and reference screenshots. The included validation records cover the offline HTML and the C# domain starter. They do not establish a passing native Uno build or real OCR/camera behavior.
