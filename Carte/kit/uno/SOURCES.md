# Uno implementation references

The following official documentation was read on 19 October 2026. It supports the platform integration notes; Carte’s architecture and design decisions are proposals in this kit.

- [Uno project templates](https://platform.uno/docs/articles/get-started-dotnet-new.html#uno-platform-application) — blank and recommended presets, multi-platform project creation and the Skia default.
- [Custom fonts](https://platform.uno/docs/articles/features/custom-fonts.html#custom-fonts) — Content assets, `ms-appx` font references, variable-font limitations and static font manifests.
- [SVG images](https://platform.uno/docs/articles/features/svg.html#using-svg-images) — SVG asset folder, single-project Svg feature and platform behavior.
- [Google Fonts repository](https://github.com/google/fonts) — upstream font files and SIL Open Font License notices. Exact download URLs are recorded in `assets/font-sources.json`.
- [Lucide](https://lucide.dev) — line-art glyph source. The bundled runtime and exported SVGs use Lucide 1.8.0, with its license included.

The package does not pin a future Uno application SDK version. Use a coherent current template configuration, verify it on the intended targets, then commit the generated version choices.
