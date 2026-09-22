# Mockup parity: what matches and what does not

Reference: the mockup supplied 2026-09-21 (a newer reference than `../MorningCard/morning-card.html`,
which this port was built from). Where the two disagree, the mockup wins.

## Closed in this pass

| # | Gap | Fix |
|---|---|---|
| 1 | Lock drew as a filled blue circle while the car was locked; the mockup draws all four circles neutral | Lock is a plain Button, not a toggle. Fill is not how this design reports lock state - the header line and the button's own label carry it. |
| 2 | The drive pill sat below and right of the route, in the open corner | Above the drive midpoint, riding the line |
| 3 | No grey marker for "now"; the label pointed at the car dot | A 3.5px Ink2 node at 26% along the slack stretch, which is where the mockup puts it |
| 4 | The school pin was a teardrop on an 18px leader | A 26px rounded square with a short tail, directly above the bell node |

## Open, fixable from the image

| # | Gap | Note |
|---|---|---|
| 5 | The pill now overlaps the "Arrive 7:47" label | Needs a nudge, or the label moved. Cheap. |
| 6 | Route path shape: the mockup runs a stepped S-curve much further right and flatter; the port uses a smooth diagonal | One path string in `RouteRulerRenderer.RouteData`. Worth redrawing against the mockup. |
| 7 | Map panel proportion: the mockup panel reads wider and shorter than the port's 214 epx | Measured off a scaled screenshot, so treat as approximate: about 177 epx tall at 400 wide. Confirm before changing. |
| 8 | Backdrop: the mockup is a single warm cream gradient with a soft vignette; the port has four multi-colour bokeh blobs from the HTML prototype | `BokehBackdrop.xaml` plus the four Blob tokens |

## Open, needs a decision or an asset

| # | Gap | What is needed |
|---|---|---|
| 9 | **The car.** The mockup shows a photographic white Volvo XC60, three-quarter front view. The port draws a flat grey side-view wagon, ported from the prototype's SVG. | A real image asset. A hand-drawn vector will not pass for a product photo, and inventing one would be worse than the honest placeholder. Supply a PNG and it drops into `CarGlyph`. |
| 10 | **The map ground.** The mockup shows a realistic street map: organic roads at varied angles, large soft park shapes, a blue river. The port draws a uniform rotated grid, which is what SPEC decision 1a asked for ("restyle the ground so it reads as a diagram, not cartography"). | This contradicts decision 1a. Either the card gets a real map after all (which reopens Q1c: the slack stretch has no geographic meaning, so the ruler would have to change), or the ground gets richer while staying schematic. A decision, not a task. |

## Numbers in the mockup that do not add up

The mockup reads "Leave in 12 min", "24 min", "14 min" drive, "3 min walk" and "9 min slack" at once.
Those are mutually inconsistent: a 14 minute drive plus a 3 minute walk is 17, not 24, and 9 minutes of
slack is not 12. The port derives every one of these from one clock and one plan, so it shows 17 and
"Leave in 9 min" for the mockup's 7:24 / 7:50 / 14 / 3 inputs. **The derivation is kept and the
mockup's figures are treated as placeholder text**, which is the usual state of a static mockup. Say so
if any single figure is the intended one and the others should follow from it.
