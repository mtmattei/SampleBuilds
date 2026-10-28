# Carte assets and attribution

## Fonts

Instrument Serif supplies regular and italic editorial text. DM Mono supplies metadata and prices. Carte Sans is a set of static instances derived from the bundled DM Sans variable source at optical size 14 and weights 400, 500 and 600. The derived family was renamed to Carte Sans and retains upstream copyright and license records.

Use the static Carte Sans files in Uno to reduce differences in variable-font support. The HTML uses the CSS alias DM Sans for these same static files to preserve the original styles. The package includes the original variable font for reproducibility, not as the recommended native font.

All upstream font files were downloaded from the official Google Fonts repository. Their SIL Open Font License notices are in `licenses`; exact URLs and derivation settings are in `font-sources.json`.

## Glyphs

The 27 base glyphs come from Lucide 1.8.0. Each SVG is supplied in ultramarine and paper colors. SVGs retain the 24-unit view box, rounded line caps and joins, and use a 1.45-unit stroke to match the prototype. Their mapping to menu items is recorded in `icons/registry.json`.

These are a consistent starter glyph library. A later brand illustration pass can combine a vessel or plate outline with a distinctive ingredient, while preserving the short text label and hit target. Do not rely on the image alone to identify an item.

Keep `licenses/Lucide-ISC.txt` and the font notices with redistributed assets. SVGs are editable vector source; PNG screens are visual references, not separate art assets to build the app from.
