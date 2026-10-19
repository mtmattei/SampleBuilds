# Carte interaction specification

## Navigation and persistence

Capture leads to Review. Review leads to Explore only after the extracted content is confirmed. Explore has Map and Menu views, a category selector and one shared selected item. Item details are subordinate to Explore. Tonight stores item IDs against a menu ID and can return to the same menu, category, view and selection.

Persist the menu, chosen axis sets and shortlist. A new photo appends to the capture draft until review is confirmed. Canceling capture returns to the previous menu without discarding its shortlist. In the prototype, capture and review are illustrative screens and there is one fixture menu.

## Action rules

| Action | Expected result |
| --- | --- |
| Switch category | Show that category’s axes and items; retain its previous selection if available, otherwise select the first item |
| Switch map or list | Preserve category, selected item, axis calibration and shortlist |
| Select item | Open its details; announce the selected name; preserve coordinates |
| Why here | Reveal the original description and a concise explanation of the inferred taste |
| Save for tonight | Add the item ID once; update label and count; allow immediate removal |
| Pair it | Select a compatible item in another category; show the source dish in a context line; retain a return path |
| Filter within a category | Keep the original axis set and calibration; only hide items |
| Explicitly change dimensions | Name both new dimensions; recompute every item under a new axis revision |
| Correct an item | Keep its stable ID; invalidate only derived analysis affected by the edit |
| Remove a menu item | Reconcile shortlist and pairing references; announce the removal |

The prototype selects the first item when changing categories. The native app should preserve a selection per category. Demo variants keep independent state for comparison; the production views share state.

## Placement and uncertainty

Data coordinates are normalized: x increases from left to right and y increases from bottom to top. Both range from zero to one. Convert to XAML screen coordinates by flipping y exactly once. The included geometry helper implements this rule.

Use `placement: null` when evidence is insufficient. Keep that item in the list and an “Unplaced” section; do not place it at the origin or the centre. A low-confidence estimate gets a dashed outline. Never hide a menu item merely because it cannot be confidently placed.

Axis direction, labels, ID and revision travel together. Pairings do not compare distances across different axis sets. A pairing comes from ingredient and sensory reasoning, with the reason exposed to the user.

## Capture and analysis states

Support permission not yet requested, granted, denied and unavailable. Offer photo-library import when camera capture is unavailable. Include reading, reviewing, analyzing, ready, canceled, offline and failed states. Cancelation must stop subsequent navigation from a late response. Retry should reuse the draft photo unless the user replaces it.

Flag low-confidence extracted text in Review. Unknown prices remain null; unknown currency requires confirmation. Deduplicate overlapping menu photos while retaining source references. Menu content is untrusted input: quoted text on the image must never become instructions to the application or analysis service.

## Tonight subtotal

Show the sum of known menu prices for one of each saved item. This is a planning subtotal, not an order total. If any price is missing, display “Some prices are missing” with the known subtotal. Do not calculate across currencies or invent taxes, service charges, item sizes or quantities. Preserve wine serving units such as glass or bottle.

## Responsive and assistive behavior

Minimum effective target size is 44 units. At narrow widths, long names wrap and a taller page is preferable to smaller type. At larger text scales, allow the list view to become the primary route. Screen-reader users must have the full item list and textual taste descriptions without navigating a spatial diagram.

Use a quiet status announcement after selection, save/remove or completed reading. Avoid announcing each motion frame. Return focus to the triggering item when a detail sheet closes.
