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

## v2 KBDU Environment + Physics Sources

The v2 pass adds no paid or unclear-license assets and does not commit Google-derived assets.

- OpenStreetMap reference extract:
  - source: https://overpass-api.de/api/interpreter
  - license/attribution: https://www.openstreetmap.org/copyright
  - use: non-committed reference for KBDU airport/road/building/water density and layout cues
  - artifact path: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\kbd_environment_physics_20260707_225453\osm_reference`
  - committed: no raw OSM data committed
- expanded KBDU-inspired procedural world:
  - source: project-owned Unity mesh/material generation in `KbduInspiredWorldBuilder`
  - use: terrain chunks, fields, roads, buildings, water/drainage hints, perimeter cues, far ridges
  - committed: source code only
- airport surface/detail pass:
  - source: project-owned Unity primitive/material generation in `AirportRuntimeEnhancer`
  - use: taxi-lane network, apron markings, runway cracks/wear, hangar/ramp density, procedural noisy materials
  - committed: source code only
- render-quality profile:
  - source: project-owned runtime settings/evidence code in `QuestRenderQualityConfigurator`
  - committed: source code only

The imported C172 placeholder remains unchanged in v2.

## Existing Gaussian Splat Renderer Package

The Gaussian renderer package remains:

- package: `org.nesnausk.gaussian-splatting`
- source: `https://github.com/aras-p/UnityGaussianSplatting.git?path=/package#v1.1.1`
- package license: MIT

The package license covers the renderer code/package. It does not grant rights to arbitrary captured airport splat assets. Captured splat/source-data licensing must be tracked separately before any real-world asset is committed.

## v2.1 Pilot Eye + KBDU Environment Sources

The v2.1 pass does not add paid, downloaded, or questionable-license visual assets. It preserves the current imported C172 placeholder and expands project-owned procedural environment generation:

- pilot-eye/default seat reference:
  - path: `QuestFlightLab/Assets/Scripts/Runtime/PilotViewpointConfig.cs`
  - source: project-authored configuration for the existing imported C172 placeholder
  - use: default pilot eye point and visual QA acceptance gate
- expanded KBDU-inspired procedural world:
  - path: `QuestFlightLab/Assets/Scripts/Environment/KbduInspiredWorldBuilder.cs`
  - source: project-owned procedural Unity geometry/material generation
  - use: 14.56 km x 14.56 km bounded airport/terrain environment, terrain chunks, roads, fields, reservoir/drainage hints, buildings, and far ridge impostors
- matched-control JSBSim comparator:
  - path: `tools/jsbsim_probe/run_matched_jsbsim_comparison.py`
  - source: project-authored tool using the Python `jsbsim` package and bundled `c172x`
  - use: offline physics reference oracle only

No raw OSM/USGS/Google data, no APKs, no screenshots, and no large downloaded archives are committed for v2.1.

## Quality Gate v1 Sources

The playable simulator quality gate does not add paid or downloaded visual assets. It uses project-authored procedural materials/geometry and the existing imported C172 placeholder:

- procedural sky/render profile:
  - path: `QuestFlightLab/Assets/Scripts/Runtime/QuestRenderQualityConfigurator.cs`
  - source: project-authored Unity render settings and procedural skybox material
  - use: daylight sky/atmospheric profile and Quest-safe render settings
- airport surface quality layer:
  - path: `QuestFlightLab/Assets/Scripts/Environment/AirportRuntimeEnhancer.cs`
  - source: project-authored procedural geometry/material noise
  - use: faded runway paint, aggregate streaks, shoulder gravel, apron stains, and ramp markings
- terrain/world quality layer:
  - path: `QuestFlightLab/Assets/Scripts/Environment/KbduInspiredWorldBuilder.cs`
  - source: project-authored procedural mesh/material generation
  - use: irregular prairie/field color patches, higher-detail terrain rings, ridge/haze layers
- JSBSim Editor bridge:
  - paths: `tools/jsbsim_probe/jsbsim_editor_bridge.py`, `QuestFlightLab/Assets/Editor/JSBSimEditorBridgeRunner.cs`
  - source: project-authored bridge code using Python `jsbsim 1.3.1`
  - use: Editor-only JSBSim telemetry import/proxy application proof

No raw screenshots, raw logs, APKs, raw DEM/OSM downloads, Google-derived assets, or large texture archives are committed for the quality-gate pass.

## Production Simulator V1 KBDU Real-Data Environment

The production-demo environment uses small, bounded derivatives generated by `tools/kbdu_environment`. Raw network responses remain outside Git at:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_sim_v1_20260710_151201\workstream_b_kbdu\raw_20260710
```

- FAA / USDOT BTS NTAD airport and runway services:
  - source: FAA-derived `NTAD_Aviation_Facilities` and `Runways_View` ArcGIS FeatureServer queries bounded to `ARPT_ID=BDU`
  - license/status: United States government work; unrestricted public use
  - attribution: Federal Aviation Administration (FAA); USDOT Bureau of Transportation Statistics (BTS)
  - use: FAA airport reference point, KBDU identifiers, runway 08/26 endpoints, dimensions, surface, lighting, and condition metadata
- USGS 3DEP Bare Earth DEM dynamic service:
  - source: `https://elevation.nationalmap.gov/arcgis/rest/services/3DEPElevation/ImageServer`
  - license/status: USGS-authored United States government data; public domain / no use restrictions
  - attribution: U.S. Geological Survey 3D Elevation Program (3DEP)
  - use: 30,304 quantized height samples across 10 m, 40 m, 150 m, and 400 m terrain layers, including runway endpoint grade evidence
- OpenStreetMap through Overpass API:
  - source: `https://overpass-api.de/api/interpreter`
  - license: Open Database License 1.0 (ODbL-1.0)
  - required attribution: `© OpenStreetMap contributors`; `https://www.openstreetmap.org/copyright`
  - use: 5,522 clipped/simplified/quantized aeroway, barrier, building, land-cover, road, and water features; distributed derivative provenance is recorded in `tools/kbdu_environment/kbdu_source_manifest.json`
- USGS/USDA NAIP Plus availability service:
  - source: `https://imagery.nationalmap.gov/arcgis/rest/services/USGSNAIPPlus/ImageServer`
  - license/status: United States government orthoimagery; public domain
  - use: bounded availability query only; no imagery pixels were downloaded or committed because the current pipeline does not yet prove seam-safe tiling, color normalization, mips, and Android ASTC output within budget
- project-authored macro material fallback:
  - source: deterministic palette, micro-detail texture, and OSM-tag mapping generated by project code
  - use: Quest-bounded terrain and context rendering in place of aerial imagery

Committed derivatives are `kbdu_terrain_rings.json` and `kbdu_reference_context.json` under `Assets/Resources/QuestFlightLab/Environment/KBDU`. The exact requests, timestamps, raw and derived hashes, license records, budgets, and toolchain hashes are pinned in `tools/kbdu_environment/kbdu_source_manifest.json`. No Google Maps, Google Earth, Street View, paid, or unclear-license content is used. The result is not for navigation, surveying, FAA approval, or training credit.

## Quest Visual Stability V2 CC0 Ground Materials

Three official Poly Haven 1K diffuse maps are committed as the optimized runtime sources. Poly Haven publishes these assets under [CC0 1.0](https://polyhaven.com/license); attribution is not legally required, but authorship and exact downloads remain recorded here. Files were downloaded directly as individual JPG maps on `2026-07-11T06:55:03Z`–`06:55:04Z`; no ZIP/raw archive is committed.

| Runtime use | Asset / author | Asset page and exact file | Source and committed resolution | Committed SHA256 |
| --- | --- | --- | --- | --- |
| dry prairie | Withered Grass / Charlotte Baglioni | [asset](https://polyhaven.com/a/withered_grass), [1K diffuse JPG](https://dl.polyhaven.org/file/ph-assets/Textures/jpg/1k/withered_grass/withered_grass_diff_1k.jpg) | 1024×1024 → 1024×1024 | `8BEBFB639EF74F651BC445526C46C845BEEE52A86D831848B82EB2BBBF2D98EE` |
| short/sparse green grass | Sparse Grass / Amal Kumar | [asset](https://polyhaven.com/a/sparse_grass), [1K diffuse JPG](https://dl.polyhaven.org/file/ph-assets/Textures/jpg/1k/sparse_grass/sparse_grass_diff_1k.jpg) | 1024×1024 → 1024×1024 | `AE94F2B34597B9108EEFD88217F55ECCAEC6D6B382E858A478EE92DF90E66617` |
| dry soil | Dry Ground 01 / Rob Tuytel | [asset](https://polyhaven.com/a/dry_ground_01), [1K diffuse JPG](https://dl.polyhaven.org/file/ph-assets/Textures/jpg/1k/dry_ground_01/dry_ground_01_diff_1k.jpg) | 1024×1024 → 1024×1024 | `75222FC97A82B635A09CF8F2891DD58BC42E49599B9FDA920B2C50193FE85E9F` |

`EnvironmentGroundTextureImportPolicy` enforces mipmaps, mip streaming, Repeat wrap, trilinear filtering, anisotropic level 4, a 1024 maximum, and Android ASTC 6×6 high-quality compression. `KbduGroundAntiTile` samples each source once (three texture samples total), blends them with stable world-space macro/mid variation, and fades micro detail from 350 m to 2.4 km to control Quest shimmer. Terrain rings keep one global world mapping so their shared boundaries remain continuous. Context/land-cover batches vary only the secondary texture rotations and phases through a deterministic `MaterialPropertyBlock`; they retain shared material instances and do not reset the primary world-space prairie layer. No normal/displacement map or dense 3D grass field is used.

## Production Vertical Slice V2 Baked Environment

`Assets/Production/Environment/ProductionEnvironmentRoot.prefab` and its `Generated`, `Materials`, and `Textures` children are deterministic project-authored outputs of `ProductionEnvironmentPrefabBaker`. The authoring inputs are the already-pinned compact derivatives above; no new external download or raw archive is committed.

- terrain meshes: derived from USGS 3DEP snapshot `20260710T214309Z` (United States government data; public domain), baked as fixed near 4 km, mid 12 km, and immutable far 24 km mesh assets;
- runway: FAA/BTS runway 08/26 endpoints and dimensions effective `2026-04-16` (United States government work; unrestricted public use), with an offline project-authored terrain flatten/blend and one shared visual/collision mesh;
- airport/context geometry: bounded OSM derivative under ODbL-1.0 with required attribution `© OpenStreetMap contributors`;
- essential water: OSM Boulder Reservoir polygon, way `35597714`, retained as the sole production water body with a project-authored stable shore-bank interface; minor ditches, streams, ponds, and reservoirs are not baked into this slice;
- `ProductionKbduMacroAlbedo.png`: a project-authored 1024×1024 deterministic derivative of the pinned USGS elevation/slope and OSM land-cover, road, and aeroway data. It uses continuous low-frequency variation and warped irregular parcels; it is unique over the 12 km context and imported with mips, trilinear filtering, anisotropy 4, Clamp wrap, and Android ASTC 6×6;
- microdetail: the three Poly Haven CC0 1.0 maps recorded in the preceding section, sampled stochastically in world space and faded with distance;
- production ground, runway-marking, and water shaders: project-authored source code with no third-party shader content.

The macro albedo is intentionally a muted synthetic land-cover derivative, not an orthophoto or a claim of photorealism. It contains no Google Maps, Google Earth, Street View, paid, or unclear-license pixels. All geometry is simplified and labeled KBDU-inspired / not for navigation, surveying, FAA approval, or training credit.

## Rules For Future Visual Assets

- Prefer self-generated procedural Unity/Blender assets for quick iteration.
- Use CC0 assets only from sources with clear license pages, and record source URL, author/source, license, and import date.
- Keep assets small and Quest-appropriate.
- Keep source scripts for generated meshes/materials when practical.
- Do not commit Google Maps/Earth/photos as textures or model source unless terms are explicitly acceptable.
