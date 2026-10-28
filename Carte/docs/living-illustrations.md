# Native living illustrations

Carte now renders all 18 menu items as layered native XAML vectors in Menu margins, Taste atlas and Tonight. Steak heat rises, oysters hinge open, glasses fill, pasta steams, citrus drips and cocktail details move. Motion plays once on mouse hover or a pointer/touch tap; keyboard interaction is immediate. Repeated playback and unload cancel prior timelines.

`Controls/LivingIllustrationView.cs` owns the native drawing, input triggers, selected-state palette, static fallback and cancellation. `Controls/IllustrationCatalog.g.cs` contains the generated paths and independent per-part timelines. SVG source and the portable motion catalog live in `Assets/Illustrations`. Regenerate the C# definitions with `python tools/generate_illustrations.py` from the project root; it needs only Python's standard library.

Transforms are resolved through named Canvas layers because runtime XamlReader does not register nested Transform names. Each animated channel has its own native transform. Glass fills use a moving rectangular clip plus an opposite inner translation; the vessel shape stays fixed and liquid cannot escape its bowl. The native fill uses a quiet ink wash. Whole illustrations, labels, prices and taste-map coordinates remain fixed.

Menu rows now show plain taste descriptions and trailing prices. The miniature cross-and-dot charts have been removed. Existing selection, explanation, save, pairing, shortlist and category behavior is retained. Unknown illustration keys use the original static glyph, with a separate inverted fallback for a selected map marker.

Uno 6.7 does not implement UISettings.AnimationsEnabledChanged. The control reads AnimationsEnabled before playback and checks it every 100ms only while a sequence is active. It stops the timer with the sequence; there is no idle polling. MotionEnabled also cancels immediately when set false. Live OS reduced-motion switching still needs a manual target-device check.

## Before and after

| Before | After |
| --- | --- |
| Flat menu glyph images | 18 native layered illustrations with semantic internal motion |
| Mini quadrants on the right of menu rows | Plain taste descriptions and right-aligned prices |
| Two overlaid glyph images and whole-marker press scaling | One palette-aware drawing above the existing selection, confidence and focus rings |
| Static shortlist artwork | Same reusable native drawing component with one-shot hover/tap behavior |
| No native illustration asset pipeline | Editable SVG/JSON sources and a portable standard-library generator |

## Validation

See artifacts/motion-run-fixed.log for the native runtime and the companion validation record supplied with this update. The Windows desktop app was launched through Uno's DevServer. Native screenshots captured the oyster open, rising steak heat and the inverted wine marker. All three categories and the existing two-item shortlist were inspected without changing the saved choices. The existing 36 domain checks pass. Both net10.0-desktop and net10.0-browserwasm builds succeeded with zero errors. The portable illustration generator reproduces the checked-in catalog exactly.

The project continues to report its five existing NuGet advisories for transitive System.Security.Cryptography.Pkcs/Xml packages. Dependencies were not changed as part of this illustration update. Browser runtime, mobile heads, screen readers and a real OS reduced-motion toggle have not been newly exercised.

