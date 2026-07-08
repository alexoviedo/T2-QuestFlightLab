# Asset Sources

This project keeps committed visual assets traceable. Do not commit paid assets, unclear-license assets, Google Maps/Earth imagery, or large raw splat captures unless their license and source are explicitly acceptable for this repo.

## v0.8 Visual Baseline

The v0.8 playable visual baseline uses self-generated Unity procedural geometry/materials plus a committed imported C172 placeholder:

- imported C172 placeholder:
  - path: `QuestFlightLab/Assets/Resources/QuestFlightLab/ImportedAssets/Cessna172KogThorns/cessna172.glb`
  - source note: `QuestFlightLab/Assets/Resources/QuestFlightLab/ImportedAssets/Cessna172KogThorns/SOURCE.txt`
  - use: personal prototype placeholder cockpit/exterior visual baseline; intended to be replaced by project-owned assets later
  - limitation: do not present as production-ready, redistributable, final C172 fidelity, or project-owned art

- C172-style cockpit/exterior baseline: Unity primitive/mesh geometry generated at runtime by `QuestFirstViewRuntimeRepair`
  - left-seat pilot eye point
  - high-wing exterior silhouette, wing struts, tricycle gear, prop/spinner, transparent cabin-window openings
  - cockpit panel, glare shield, transparent windshield/side windows, pilot/co-pilot seats, two yokes, rudder pedals, throttle/mixture/carb heat controls, radio/switch/gauge accents
- airport baseline: Unity primitive cubes, spheres, and cylinders generated at runtime by `AirportRuntimeEnhancer`
- runway/apron/grass/hangar/tree/light materials: self-generated Unity `Standard` materials with project-owned color/smoothness/emission settings
- v0.9 added procedural airport detail materials for runway rubber wear, asphalt patches, concrete joints, grass variation, and safety cones; these remain self-generated Unity `Standard` materials.

No downloaded airport, terrain, HDRI, Google Maps/Earth-derived image, paid asset, PLY, or SPZ asset is committed for the v0.8 visual baseline.

## v0.9 Production Pipeline Sources

Preferred future sources/tools:

- Blender-generated geometry from `tools/generate_c172_style_assets.py`
  - source: project-owned script
  - intended use: C172-style cockpit/exterior baseline candidate
  - status: script committed; Blender not installed locally during the witness
- OpenVSP
  - source: https://openvsp.org/
  - intended use: parametric high-wing trainer exterior reference/export path
  - status: research/install gate, not committed asset output
- JSBSim
  - source: https://jsbsim-team.github.io/jsbsim/ and https://github.com/JSBSim-Team/jsbsim
  - intended use: flight-dynamics reference data, not a visual asset
- Poly Haven
  - source/license: https://polyhaven.com/license
  - intended use: small optimized CC0 PBR materials/HDRIs for cockpit, runway, apron, grass, dirt, hangars, and sky
  - status: approved source class; no new Poly Haven textures committed in this slice

## v1 Environment-Focused Sources

The v1 production visual/physics upgrade does not add new paid or downloaded visual assets.

- expanded KBDU-inspired world:
  - path: `QuestFlightLab/Assets/Scripts/Environment/KbduInspiredWorldBuilder.cs`
  - source/license: project-owned procedural Unity mesh/material generation
  - use: enlarged training environment, terrain chunks, roads, fields, airport perimeter cues, reservoir/drainage hints, local buildings, and Front Range-inspired ridge impostors
  - committed assets: source code only; no raw terrain captures, satellite imagery, paid models, or texture downloads
- render profile:
  - path: `QuestFlightLab/Assets/Scripts/Runtime/QuestRenderQualityConfigurator.cs`
  - source/license: project-owned runtime settings/evidence code
  - use: MSAA/aniso/fog/lighting/camera/shadow defaults for the visual demo path
- aircraft/cockpit:
  - retained previous imported C172 placeholder after Alex confirmed it was good enough for this chunk
  - no new aircraft model is committed in v1

OpenVSP remains the preferred future path for owned high-wing trainer exterior geometry, but no OpenVSP output is committed in v1.

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
