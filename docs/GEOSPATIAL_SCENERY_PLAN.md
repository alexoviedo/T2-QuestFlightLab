# Geospatial Scenery Plan

## Goal

Improve scenery credibility without turning the Quest runtime into an unbounded geospatial streaming experiment.

## Options

| Source | Use | Current Decision |
| --- | --- | --- |
| Procedural KBDU reconstruction | Immediate Quest demo runway/taxiway/apron/hangars/terrain cues | **Default now** |
| OpenStreetMap | Reference for runway/taxiway/apron outlines and local roads/buildings where licensing is compatible | **Good next offline data source** |
| USGS/NASA/open terrain | Offline terrain/height reference | **Research candidate** |
| Poly Haven / CC0 materials | Asphalt, concrete, grass, dirt, sky/HDRI/material realism | **Preferred material source** |
| Cesium for Unity / 3D Tiles | Real-world terrain/tiles research path | **Research only, not Quest default** |
| Google Photorealistic 3D Tiles | Visual reference/research only unless terms/runtime path are explicitly acceptable | **Do not commit as app assets** |
| Gaussian splats / SPZ | Small owned static scenic patches after XR stereo/world-lock fix | **Diagnostic only for now** |

## Immediate Quest-Safe Path

- Keep the enhanced mesh/procedural airport as default.
- Add runway wear, apron seams, hangars, fuel/clutter, runway lights, grass variation, foothills, and sky/lighting improvements.
- Add small CC0 material textures only after optimization and source documentation.
- Use visual QA for screenshot comparison before and after each scenery pass.

## Cesium Research Gate

Do not add Cesium for Unity to `main` until a research branch proves:

- package import does not destabilize Android build,
- runtime memory/performance are plausible for standalone Quest 3,
- offline/cache strategy is understood,
- terms/API/account requirements are documented,
- visual result is better than a local procedural KBDU for this demo.

## Splat Gate

Gaussian splats remain useful as a potential scenic-background technology, but default Quest mode must not display one-eye/headset-locked splats. The current safe classification is diagnostic-only until an XR stereo/world-lock fix is proven in headset evidence.
