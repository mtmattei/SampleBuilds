# Carte component specification

`tokens.json` defines production values. PNGs show the visual concept. When the compact concept conflicts with text scaling or a 44-unit target, follow the production values and reflow the content.

## Foundations

| Foundation | Specification |
| --- | --- |
| Surface | Paper `#F7F1E4`; subtle texture is optional and decorative |
| Foreground | Ultramarine `#203BB6`; 7.90:1 against paper at full opacity |
| Rules | Ink at 23 percent opacity, 1 unit; structure only, never essential text |
| Soft fill | Ink at 5.5 percent opacity |
| Title | Instrument Serif regular; 36/40 for page titles, 28/32 for item titles |
| Body | Carte Sans static regular; 16/24 for paragraphs, 14/20 for secondary text |
| Metadata | DM Mono regular; 12/16 minimum in production |
| Axes | Instrument Serif italic, 18/24; plain readable words |
| Price | DM Mono, tabular numerals; locale-aware display |
| Spacing | 4, 8, 12, 16, 24, 32, 48, 64 |
| Corners | 4 on actions, 20 on sheets, circular selected glyph |
| Input target | At least 44 by 44 effective units |

## Component contracts

| Component | Inputs | Actions and state | Uno composition |
| --- | --- | --- | --- |
| AppHeader | Brand and current menu | Scan, back | Grid, TextBlock, Button with ImageIcon or Image |
| CategorySelector | Categories and selected ID | Select category; expose selected state | ItemsRepeater containing a Grid with ToggleButton, or a platform selection control with custom style |
| TasteMapView | Axis set, placements, selected ID | Select item, inspect cluster; never infer a new axis set during layout | Custom UserControl with measured plot surface, ordinary focusable Buttons and text labels |
| MenuItemRow | Item, placement, selected state | Select, expand details | ListView with Grid root template; preserve virtualization |
| MiniTasteMap | Position and matching axis set | Decorative overview; description lives in automation text | Small Grid with two rules and an Ellipse |
| ItemDetail | Item, original text, rationale, saved state | Save, remove, pair, expand rationale | Grid or StackPanel in a sheet or side panel |
| ShortlistView | Saved IDs and matching menu | Remove, return to explore | ListView and known-price subtotal |
| ScanReviewRow | Extracted text, optional parsed price, confidence | Correct, confirm, remove a false positive | Editable row with TextBox and validation text |

## Layout

Below 600 effective pixels, use a single column with 16 to 24 units of edge padding. Let the page scroll; do not compress map labels to make an entire screen fit. Selection reveals a sheet or detail section beneath the map. The reading view expands the selected row.

Between 600 and 904, keep the primary content centered and let the item detail occupy a second section when space permits. At 905 and above, use a map or menu list beside a 320 to 380-unit detail panel. Preserve a sensible maximum content width, approximately 1120 units.

The map reserves space for axis ends and label bounds. Glyph centres are computed from normalized data after measurement. Keep label text at least 12 units in production. When points collide, reposition labels with short leader lines or group them into a clearly labeled cluster. Never move the semantic coordinate to make the chart look less crowded. The list view is always available.

## States

Use named visual states for normal, pointer over, pressed, selected, disabled, keyboard focused and low-confidence markers. Selection uses an ink disc and paper glyph. Focus uses a separate 2-unit visible ring. Low confidence uses a dashed outline and an accessible label. Do not rely on color alone.

In the map, clicking a glyph preserves category and axis set. A save action updates only the shortlist and its count. Press feedback uses scale 0.96. Selection transitions last about 160 ms; deliberate layout changes may use 220 ms. Respect reduced-motion preferences and avoid initial entrance choreography.

## Native accessibility

Use `AutomationProperties.Name` for every actionable glyph and describe its taste and position in words. Keep natural reading order aligned with the menu list. Make map and list equivalent routes to each item. Preserve keyboard focus after a save or category change. Test larger text, screen readers and high contrast. Permit system contrast behavior even though the ordinary visual identity uses a fixed cream-and-blue palette.

The native app must use explicit item names and states, never icon names such as “fish button.” A suitable label is “Sea bass crudo, light and bold, estimated from the menu, 19 US dollars.”
