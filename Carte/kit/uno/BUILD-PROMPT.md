# Carte implementation prompt

Build Carte as an Uno Platform app using the files in this package. Read START-HERE.md, docs/product-brief.md, design/interaction-spec.md, design/component-spec.md and uno/BUILD-GUIDE.md before editing code.

Preserve the cream paper and ultramarine identity, Instrument Serif titles, bundled static Carte Sans body text, DM Mono metadata and line-art glyphs. Use design/tokens.json as the production token source. Build the reading-led Menu margins view and the Taste atlas view as two presentations of the same menu, selected item and shortlist. The prototype opens Menu margins first.

Begin with data/menu.sample.json. Implement category switching, stable category-specific axes, map and list selection, item detail, source evidence, uncertainty, save/remove, known-price subtotal and pairing navigation. Use the supplied C# core for the data model and geometry. Data y increases upward; flip it exactly once when rendering. Pairings must use fixture links or explicit sensory reasoning, not distances across different axis sets.

Create reusable XAML components and centralized resource dictionaries. Wire actions through view-model commands. Implement the TasteMapView dependency properties described in BUILD-GUIDE.md. Use native list controls for the reading view, a measured map surface for the quadrant, and proper automation labels, keyboard focus and 44-unit touch targets. Preserve text scaling and provide a list fallback when the map is crowded. Use visual states for selection and focus and honor reduced motion.

Add a fixture-backed persistence layer before network services. Capture and review can initially be clearly marked demo screens. Do not imply OCR or camera access works until those services are implemented and tested. Introduce camera/photo picking, extraction and taste-analysis providers behind the supplied interfaces in separate milestones.

Run the core checks, build the actual Uno target, and exercise the visible UI. Compare the result to design/previews at a 412-unit reference width and test narrow, expanded and larger-text layouts. Use docs/acceptance-checklist.md. Report exactly which targets and flows were validated, and identify any remaining provider or platform integration work. Keep the original sample menu and licenses intact.
