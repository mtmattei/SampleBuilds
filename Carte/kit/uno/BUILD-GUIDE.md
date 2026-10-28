# Building Carte with Uno Platform

Build the reviewed menu experience first with the fixture data. Use XAML and C# with a shared view model and a custom map control. Then integrate capture, extraction and analysis behind the included service interfaces.

## Project setup

Use the current Uno template wizard for the intended platforms and local tooling. A verified minimal starting command is:

```powershell
dotnet new install Uno.Templates
dotnet new unoapp -preset=blank -o Carte
```

The recommended preset is also available if you want Uno.Extensions scaffolding. Current Uno templates use Skia by default from Uno.Sdk 6.0 for iOS, Android and WebAssembly. Keep the template-generated SDK and package versions together; pin them in source control after verifying the project builds. These template commands are documented in [Uno project templates](https://platform.uno/docs/articles/get-started-dotnet-new.html#uno-platform-application).

For a new implementation, start with desktop and WebAssembly to validate the fixture-driven UI, then add mobile capture on the actual target devices. An existing Uno repository should keep its established MVVM or MVUX pattern and navigation framework.

## Integrate the kit

1. Copy `assets/fonts` into `Assets/Fonts`. Use the static Carte Sans files for production; the variable source file is included for reproducibility.
2. Copy `assets/icons` SVGs into `Assets/Svg`. Use the `-paper.svg` variant inside the selected ink disc. Keep attribution files with the distributed app.
3. Copy `copy/Resources.resw` into `Strings/en-US/Resources.resw`.
4. Copy `data/menu.sample.json` as an app content asset.
5. Add `uno/starter/Carte.Core` to the solution and reference it from the app. It targets .NET 10 and has no third-party dependencies.
6. Copy `CarteResources.xaml` into `Themes` and merge it from `App.xaml` after the base theme resources.
7. Adapt `ExplorePage.blueprint.xaml` to the generated page namespace and implement its view-model and custom-control contracts below.
8. Use the PNGs as comparison references at 412 logical units wide. Reflow the native implementation for larger production text and responsive layouts.

The starter’s local `NuGet.Config` clears sources only for its dependency-free checks. **Do not copy that file into the Uno application**, which needs normal package feeds.

## Font and image integration

Bundled fonts use `ms-appx:///Assets/Fonts/File.ttf#Family Name`. Uno documents Content assets and font manifests for platform consistency. This kit supplies static fonts because the current font guidance identifies limitations for variable fonts on Skia. See [Uno custom fonts](https://platform.uno/docs/articles/features/custom-fonts.html#variable-fonts-and-font-manifest).

Keep direct SVG files in `Assets/Svg` to avoid unintended Resizetizer conversion. For the documented single-project iOS/Android SVG route, enable the `Svg` Uno feature. Use `Stretch="Uniform"` for glyphs and test the simple stroke SVGs on every target. See [Uno SVG images](https://platform.uno/docs/articles/features/svg.html#how-to-use-svg).

## View model contract

Expose `MenuName`, `MenuSubtitle`, `Categories`, `SelectedCategory`, `VisibleItems`, `SelectedItem`, `MapVisibility`, `ListVisibility`, `RationaleVisibility`, `OtherViewLabel`, `SaveLabel`, `TonightLabel` and `CanPair`. Item presentation models expose `Name`, `Description`, `Rationale`, `GlyphUri`, `SensorySummary`, `PriceText`, normalized `X` and `Y`, `Confidence` and `IsSaved`.

Commands are `ScanMenuCommand`, `ToggleViewCommand`, `ToggleRationaleCommand`, `ToggleSavedCommand`, `PairCommand` and `ShowTonightCommand`. Use `ICommand` bindings with MVVM; follow the generated command binding pattern if the existing project uses MVUX. Business logic and navigation belong in the view model or navigation service, not click handlers in page code-behind.

Format prices and localized sentences in presentation properties. Do not use WPF-only `StringFormat`, `x:Static` or `x:Reference` patterns in WinUI XAML.

## TasteMapView contract

Implement a `UserControl` with bindable `ItemsSource`, `AxisSet` and `SelectedItem` dependency properties. Include a measured plot area, axis-end text outside the plot and ordinary focusable controls for the items. Use the supplied `TasteMapGeometry` to calculate pixel positions after measurement and on resize. Code-behind is appropriate for this purely visual layout operation; it should not call view-model business methods.

Each glyph target is at least 44 by 44. Use one visual state group for normal, hovered, pressed, selected and focused states, with a separate low-confidence indicator. Keep semantic positions stable when labels are offset. If a group cannot fit, offer an item cluster or the list view. Unplaced items stay outside the plotted set and remain accessible in the menu list.

The supplied screen is a **blueprint**, not a compilable standalone page until this control, its bindings, code-behind class and navigation are added. Native Uno UI builds and device permission flows are not validated by the core-library checks.

## Check the included core

From the kit root, with a .NET 10 SDK:

```powershell
dotnet run --project uno/starter/Carte.Core.Checks --configuration Release
```

The checks cover fixture parsing, JSON round-tripping, coordinate direction, resize geometry, invalid data, stale axes, duplicate IDs, target collisions and shortlist totals. They are a small dependency-free console harness, not a replacement for UI and integration tests.

## Service integration order

Use a fixture-backed store and analysis service first. Add local menu persistence, then platform photo picking, then camera capture, then OCR and structured analysis. Keep OCR and taste inference separate so users can correct text before taste estimates are generated. Use cancelable operations and ensure a canceled request cannot later navigate to a stale result.

Add a real integration test using a photographed menu only after the provider and retention behavior are chosen. API keys must stay out of the shipped client. Do not upload menu photos silently when a user is only exploring a saved menu.
