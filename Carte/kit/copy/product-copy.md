# Carte product copy

## Brand

**Name:** Carte

**Tagline:** A little guide to good taste.

**Short description:** Photograph a menu. Find your corner of good taste.

**Product description:** Carte turns a restaurant menu into an illustrated map of flavour. Explore plates, wines and cocktails by how they might taste, understand the thinking behind each placement, and save a few choices for tonight.

## Voice

Speak as a knowledgeable dining companion: warm, concise and specific. Use ordinary sensory language such as bright, creamy, herbal, crisp or rich. Preserve original names and give a short explanation when a word may be unfamiliar. Describe a likely experience instead of declaring one item best.

Prefer “A bright, light bite with a little chilli at the finish” to technical scoring. Prefer “The menu’s citrus notes suggest crisp acidity” to an unsupported certainty. Do not describe inferred taste as a verified ingredient list.

## Main moments

**Capture:** Good taste starts with a menu.

Fit the whole page inside the frame. You can add more pages next.

**Review:** A menu full of possibilities.

Check the names and prices before we draw your map.

**Explore:** Your evening, mapped.

Find your corner.

**Explain:** Why here?

On the menu: “Sea bass, lime, green chilli, coriander.” Lime and chilli suggest bold flavour; raw fish keeps the dish light.

**Save:** Save for tonight → Saved for tonight.

**Shortlist:** For tonight. A few things that caught your eye.

**Empty shortlist:** Your evening is still unwritten.

## Errors and uncertainty

**Unclear photo:** Some lines are hard to read. Try a closer, steadier photo.

**Unknown placement:** Not enough detail to place this item yet.

**Offline:** You’re offline. Your saved menus are still here.

**Service failure:** We could not read this menu just now. Your photo is still available to retry.

**Unknown price:** Price not listed.

## Localization

The keyed string catalog is the implementation source. `Resources.resw` contains Uno-ready resources; copy it to `Strings/en-US/Resources.resw`. Keys ending in `.Text`, `.Content` or `.AutomationProperties.Name` map to `x:Uid` property localization. `Format.*` and `Brand.*` are value resources retrieved programmatically.

Keep variables out of translated string concatenation. Use placeholders and locale-aware price formatting in the view model. Choose singular/plural strings appropriately for the target language. Preserve menu-supplied proper names. Avoid hardcoding the US dollar symbol outside this illustrative fixture.

Do not ship the demo-only “Sample menu” and simulated-scan disclosures on a live captured menu; use them only in onboarding or a clearly marked sample experience.
