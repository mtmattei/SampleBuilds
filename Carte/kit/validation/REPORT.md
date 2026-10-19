# Carte package validation

Validated on 19 October 2026.

## HTML prototype

The self-contained prototype rendered in a separate headless Chrome session with networking disabled. Fonts, icons and screen content loaded without external requests or script errors. The checks exercised both design variants, three category maps, item explanations, save/remove behavior, a three-item shortlist, pairing context and the simulated capture-to-review flow.

Nine screens were exported at device scale 2. The package includes those PNGs, a four-screen overview and two diagram PNGs. The original fragment is retained separately for editing; it uses its original host conventions and is not the standalone entry point.

The browser viewport was checked at 320, 390 and 760 pixels. The standalone wrapper uses 16-pixel outer padding, so the corresponding inner frame widths were 288, 358 and 728 pixels. No horizontal document overflow was detected. The app surface caps at 412 logical pixels. Production native text scaling and assistive technology behavior remain acceptance checks for the Uno implementation.

## Data and assets

The sample menu is checked against its JSON Schema. Item IDs, category references, pairing references, graph references and linked glyph files are checked for consistency. JSON files and XML-based XAML, RESW, SVG and project files are parsed for syntax. PNG files are decoded and verified. Static font instances are checked for the absence of a variable-font table.

Detailed dimensions, counts and the calculated foreground contrast are recorded in `package-checks.json`. The contrast calculation concerns full-opacity ink on paper; translucent rules are decorative.

## C# starter

The dependency-free `Carte.Core` and its console check project compiled and ran using .NET SDK 10.0.303, targeting .NET 10. The result was **PASS: 19 domain checks**. These include fixture parsing, round-trip serialization, axis direction, resize geometry, invalid coordinates, stale axes, duplicate IDs, missing-price handling and shortlist behavior.

## Remaining implementation verification

The XAML is an integration blueprint and has XML syntax validation only. No native Uno application or custom TasteMapView was compiled in this package. Camera permissions, OCR, live analysis, storage services, platform screen readers, high contrast and native reduced motion require implementation and target-device validation. The sample capture does not access a camera or upload a file.
