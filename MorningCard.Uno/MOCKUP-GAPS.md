# Mockup parity: what matches and what does not

Reference: the mockup supplied 2026-09-21 (a newer reference than `../MorningCard/morning-card.html`,
which this port was built from). Where the two disagree, the mockup wins.

## Scope, decided 2026-09-21

**The card is the whole sample.** Nothing sits behind it: no real map, no routing service, no trip
detail page, no navigation. The card shows the route ruler and the car, and that is the deliverable.

Two consequences, both settled:

- **The map ground stays schematic** (grid, parks, water, drawn in Skia). The mockup's realistic
  street map is a mockup convenience. A real map on the card was considered and dropped: Mapsui was
  added, verified to resolve and build clean against the SDK's bundled SkiaSharp 3.119.2, and then
  removed as an unused dependency. If it is ever wanted back, that build-level check is the one
  piece of the R1 spike already done.
- **The car stays a vector illustration.** The mockup's photograph is not reproducible here: the
  licensable Commons photos are street shots needing a background cutout, and a hand-drawn
  near-photo at 96x56 epx reads worse than a confident illustration. See the note below.
- **The title chevron is inert**, like the tires cell and More, because there is no page behind it.

## Closed

| # | Gap | Fix |
|---|---|---|
| 1 | Lock drew as a filled blue circle while the car was locked; the mockup draws all four circles neutral | Lock is a plain Button, not a toggle. Fill is not how this design reports lock state - the header line and the button's own label carry it. |
| 2 | The drive pill sat below and right of the route, in the open corner | Above the drive midpoint, riding the line |
| 3 | No grey marker for "now"; the label pointed at the car dot | A 3.5px Ink2 node at 26% along the slack stretch, where the mockup puts it |
| 4 | The school pin was a teardrop on an 18px leader | A 26px rounded square with a short tail, directly above the bell node |
| 5 | The car was a flat grey side-view wagon at 92x42 | A white SUV profile at the mockup's proportions, 96x56 |

## Open, cheap, not yet done

| # | Gap | Note |
|---|---|---|
| 6 | The drive pill now crowds the "Arrive 7:47" label | Needs a nudge, or the label moved |
| 7 | Route path shape: the mockup runs a flatter stepped S-curve much further right | One path string, `RouteRulerRenderer.RouteData` |
| 8 | Map panel proportion: the mockup panel reads wider and shorter than 214 epx (roughly 177 at 400 wide, measured off a scaled screenshot, so confirm) | `MapPanelHeight` |
| 9 | Backdrop: the mockup is one warm cream gradient with a soft vignette; the port has four multi-colour bokeh blobs from the prototype | `BokehBackdrop.xaml` and the four Blob tokens |
| 10 | The schematic ground could read closer to the mockup while staying schematic: fewer, more organic roads and softer park shapes instead of a uniform rotated grid | `RouteRulerRenderer.DrawGround` |

## The car, in detail

Three attempts, and the honest result is "better, not matching":

1. Ported the prototype's flat grey side-view wagon. Clean, but a wagon and the wrong colour.
2. Redrew as an SUV with gradients, cladding, roof rails. At 92x42 the wheels dominated and it read
   as a sedan.
3. Fixed wheel proportion and glass tone, then enlarged to 96x56. Now white, taller, clearer, and
   still more sedan than XC60.

To go further, the options are a supplied cutout PNG at `Assets/Car/xc60.png` (only `CarGlyph`
changes), or committing to a flatter, more deliberately illustrative silhouette, which reads better
at this size than a half-realistic drawing. Licensed photo candidates, both needing attribution and
a cutout: [XC60 2022](https://commons.wikimedia.org/wiki/File:Volvo_XC60_(2022)_(53945549588).jpg)
CC BY 2.0, and [XC60 2017](https://commons.wikimedia.org/wiki/File:Volvo_XC60_(2017).jpg) CC BY-SA
4.0, which is the right front three-quarter angle.

## Numbers in the mockup that do not add up

The mockup reads "Leave in 12 min", "24 min", "14 min" drive, "3 min walk" and "9 min slack" at once.
Those are mutually inconsistent: a 14 minute drive plus a 3 minute walk is 17, not 24, and 9 minutes
of slack is not 12. The port derives every one of these from one clock and one plan, so it shows 17
and "Leave in 9 min" for the mockup's 7:24 / 7:50 / 14 / 3 inputs. **The derivation is kept and the
mockup's figures are treated as placeholder text.** Say so if any single figure is the intended one
and the others should follow from it.
