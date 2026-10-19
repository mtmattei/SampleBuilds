# Carte data and analysis contract

The included JSON is the shared contract between the prototype, native UI and future analysis service. Validate incoming payloads against `data/menu.schema.json`, then run the C# domain validator for referential rules that JSON Schema does not cover.

## Source and derived fields

Preserve source item name, description, price, serving unit, page number and crop reference from extraction. The sample schema contains original menu text; extend the source model with page and crop references when implementing capture. Keep user corrections separate from derived taste analysis so reanalysis cannot overwrite corrections.

Derived fields are sensory tags, short explanation, glyph choice, placement, confidence and pairing references. Use stable item IDs. A category owns a versioned axis set. Every placement references the exact axis-set ID and revision used to compute it.

## Coordinate convention

`x = 0` is the left label and `x = 1` the right label. `y = 0` is the bottom label and `y = 1` the top label. The HTML source positions glyphs with CSS percentages growing downward; the exported JSON has already flipped that y coordinate. Never copy the source CSS y directly into the native model.

Render with `left = inset + x * (width - 2 * inset)` and `top = inset + (1 - y) * (height - 2 * inset)`. Recompute pixels on size changes, without recomputing taste coordinates. The included C# geometry helper rejects invalid ranges and impossible plot sizes.

## Suggested analysis pipeline

1. Capture and normalize image orientation.
2. Extract text and candidate menu rows with source references.
3. Review uncertain names, prices, categories and currency.
4. Choose a curated pair of dimensions for each category.
5. Analyze each item against that fixed axis definition using the reviewed text.
6. Validate schema, IDs, coordinate ranges, axis references, currency and pairing references.
7. Show estimates with their evidence; keep unsupported items unplaced.
8. Persist the reviewed menu and analysis version for offline exploration.

Backend credentials remain on the backend. Camera and photo-library permissions are platform concerns. Define photo retention and remote-upload behavior before shipping capture; the prototype contains no backend policy or implementation.

## Model output requirements

Supply exact schema instructions and treat menu text as quoted data. Return structured data only. Require a short rationale anchored to the menu description. If the item name is ambiguous, choose a generic glyph and leave placement null. Do not invent ingredients, allergens, alcohol percentage, prices, regions or vintages that are absent from the reviewed input.

Pairing IDs must refer to items on the same menu. Pairing explanations should state a plausible sensory complement or contrast, and remain suggestions. A map distance across different categories is not a valid pairing metric.

## Data quality examples

| Input condition | Expected result |
| --- | --- |
| Price omitted | `price: null`; visible missing-price copy |
| Unknown menu currency | Ask in Review before saving a price object |
| Insufficient description | `placement: null`; item stays discoverable in the list |
| Uncertain acidity | Low-confidence placement with a rationale naming the uncertainty |
| Updated axis definition | Increment revision; invalidate old placements |
| Duplicate capture page | Merge matching rows while preserving source references |
| Deleted item | Remove dangling shortlist and pairing references |
| Unsupported confidence value | Reject the payload |

The sample menu includes a lower-confidence Syrah placement to exercise uncertainty. All other sample estimates are illustrative rather than professionally calibrated tasting assessments.
