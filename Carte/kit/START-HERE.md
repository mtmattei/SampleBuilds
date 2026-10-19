# Carte Uno build kit

Carte turns a photographed menu into an illustrated taste map. This kit contains the visual reference, editable assets, product copy, sample data and implementation guidance for building it with Uno Platform.

Open **index.html** for a visual package guide, or open **prototype/carte.html** directly. The prototype works offline and starts with **Menu margins**, the selected reading-led direction. Use the bottom design picker to explore **Taste atlas**. The two variants keep separate demo state.

## Start in this order

1. Explore the prototype and the nine screens in `design/previews`.
2. Read `docs/product-brief.md` and `design/interaction-spec.md` for scope and behavior.
3. Use `design/tokens.json`, `design/component-spec.md` and `assets` as the visual source of truth.
4. Read `uno/BUILD-GUIDE.md`, then give `uno/BUILD-PROMPT.md` to the implementation agent or developer.
5. Start with the fixture in `data/menu.sample.json`. Add real capture and analysis only after the fixture-driven experience works.

## Package contents

| Folder | Purpose |
| --- | --- |
| prototype | Self-contained offline HTML and the original editable fragment |
| design | PNG screens, overview board, design graph in JSON, Mermaid, SVG and PNG, tokens and component rules |
| copy | Product language, 74 keyed UI strings, CSV and Uno-compatible RESW resources |
| assets | Bundled static fonts, ink and paper SVG icons, icon registry and licenses |
| data | 18-item sample menu, JSON Schema and an axis preset catalog |
| uno | Build guide, build prompt, XAML resources and page blueprint, dependency-free C# core |
| docs | Product scope, data and analysis contract, milestones and acceptance checklist |
| validation | Browser checks, source/data validation and C# check results |

## Working and illustrative behavior

The HTML supports category changes, item selection, explanations, pairing suggestions, save/remove, a subtotal and two visual directions. Capture and review use an illustrative menu. There is no live camera, OCR, remote model, account or restaurant integration. Shortlist storage in the standalone wrapper can depend on browser support for local storage; the production app must persist it explicitly.

The C# core is a small reusable library with data models, coordinate conversion, validation, shortlist logic and service interfaces. It has executable checks. The XAML is an integration blueprint; it is not a complete compiled Uno application. Its custom `TasteMapView` remains to be implemented against the documented contract.

## Important visual decisions

Cream `#F7F1E4` and ultramarine `#203BB6` remain the primary palette. The native app should fill its window; the rounded mobile preview frame belongs to the presentation. Production typography uses larger body text than the dense HTML concept. Keep the composition and character while allowing text scaling and scrolling.

All prices and taste positions are fictional examples. Menu names, currency and prices must come from the reviewed capture in the real app. Taste placement does not establish ingredients, allergens or alcohol content.

Prepared 19 October 2026. Asset attribution and font modification details are in `assets/README.md`. Public platform references are in `uno/SOURCES.md`.
