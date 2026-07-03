# Asset Sources

This project keeps committed visual assets traceable. Do not commit paid assets, unclear-license assets, Google Maps/Earth imagery, or large raw splat captures unless their license and source are explicitly acceptable for this repo.

## v0.8 Visual Baseline

The v0.8 playable visual baseline uses self-generated Unity procedural geometry and materials:

- C172-style cockpit/exterior baseline: Unity primitive/mesh geometry generated at runtime by `QuestFirstViewRuntimeRepair`
  - left-seat pilot eye point
  - high-wing exterior silhouette, wing struts, tricycle gear, prop/spinner, transparent cabin-window openings
  - cockpit panel, glare shield, transparent windshield/side windows, pilot/co-pilot seats, two yokes, rudder pedals, throttle/mixture/carb heat controls, radio/switch/gauge accents
- airport baseline: Unity primitive cubes, spheres, and cylinders generated at runtime by `AirportRuntimeEnhancer`
- runway/apron/grass/hangar/tree/light materials: self-generated Unity `Standard` materials with project-owned color/smoothness/emission settings

No downloaded cockpit, aircraft, airport, terrain, HDRI, Google Maps/Earth-derived image, paid asset, PLY, or SPZ asset is committed for the v0.8 visual baseline.

## Existing Gaussian Splat Renderer Package

The Gaussian renderer package remains:

- package: `org.nesnausk.gaussian-splatting`
- source: `https://github.com/aras-p/UnityGaussianSplatting.git?path=/package#v1.1.1`
- package license: MIT

The package license covers the renderer code/package. It does not grant rights to arbitrary captured airport splat assets. Captured splat/source-data licensing must be tracked separately before any real-world asset is committed.

## Rules For Future Visual Assets

- Prefer self-generated procedural Unity/Blender assets for quick iteration.
- Use CC0 assets only from sources with clear license pages, and record source URL, author/source, license, and import date.
- Keep assets small and Quest-appropriate.
- Keep source scripts for generated meshes/materials when practical.
- Do not commit Google Maps/Earth/photos as textures or model source unless terms are explicitly acceptable.
