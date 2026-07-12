# Environment Data Sources

## Current v2 Sources

The v2 KBDU environment pass used a small OpenStreetMap reference extract for Boulder Municipal Airport context.

- Source: OpenStreetMap data through Overpass API
- Overpass endpoint: https://overpass-api.de/api/interpreter
- OSM copyright/license page: https://www.openstreetmap.org/copyright
- License: Open Database License (ODbL) for OpenStreetMap data
- Local artifact only: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\kbd_environment_physics_20260707_225453\osm_reference`
- Committed data: none

The artifact query covered approximately:

```text
south/west/north/east = 40.015, -105.255, 40.062, -105.190
```

The extract was used as a reference for airport density and surrounding features, including runway/taxiway/apron counts, nearby roads, buildings, and water/field cues. Raw OSM JSON is not committed to the repo.

## Procedural Implementation

The committed v2 world remains project-owned procedural Unity geometry/material generation:

- `KbduInspiredWorldBuilder`
- `AirportRuntimeEnhancer`
- `QuestRenderQualityConfigurator`

No Google Maps/Earth/Street View imagery, paid assets, or unclear-license assets are committed.

## Future Data Candidates

- OpenStreetMap-derived simplified road/building/apron geometry, if ODbL attribution and share-alike obligations are intentionally handled.
- USGS 3DEP / The National Map elevation data for an offline height reference.
- Poly Haven CC0 materials/HDRIs for optimized runway, concrete, grass, dirt, metal, and sky assets.
- Blender-generated mesh assets for repeatable hangars, signs, runway lights, terrain impostors, and airport clutter.

## Limits

The current environment is KBDU-inspired, not surveyed, not navigation-accurate, and not a production airport database.

## v2.1 Data Use

The v2.1 production visual/physics pass did not commit new external raw data. It continued to use the prior non-committed OSM/Overpass reference as context and implemented the larger environment through project-owned procedural generation.

Artifact root for the v2.1 witness:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_v2_20260707_233843
```

The next data step should be a bounded USGS 3DEP/The National Map elevation import proof that keeps raw downloads in artifacts and commits only small processed data if it materially improves the Quest demo.

## Quality Gate v1 Data Use

The quality-gate pass did not commit new external raw data. It uses project-authored procedural terrain/material/sky changes on top of the existing KBDU-inspired environment.

Artifact root:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\quality_gate_20260708_005435
```

The next realism step remains a bounded USGS 3DEP elevation proof. Raw terrain downloads should stay in artifacts unless a small optimized derivative is intentionally committed with source/attribution.

## Production Simulator V1 Real-Data Path

The bounded elevation proof is now implemented as an optional/default-if-valid real-data path, with the earlier project-authored procedural world retained behind `QFL_FORCE_PROCEDURAL_KBDU=1` and as the automatic failure fallback.

- shared local tangent origin: FAA KBDU airport reference point, `40.03936527 N`, `105.22608958 W`, `5288 ft / 1611.7824 m MSL`
- coordinate mapping: local ENU, Unity `+X=east`, `+Y=up`, `+Z=north`
- terrain: 30,304 USGS 3DEP samples in four resolution layers spanning a 24 km square; 54,768 source-grid triangles plus a small deterministic runtime seam stitch
- runway: FAA 08/26 geometry with a USGS-sampled endpoint elevation delta of `-2.919 m` over approximately `1250.39 m`; the rendered pavement and markings follow that grade
- reference context: 5,522 bounded OSM features / 31,533 quantized points, combined at runtime into spatial/material/category batches rather than one renderer per feature
- imagery: NAIP Plus coverage availability recorded, with no pixels downloaded or committed; deterministic project-authored macro materials remain active

The committed files are intentionally compact derivatives:

- `QuestFlightLab/Assets/Resources/QuestFlightLab/Environment/KBDU/kbdu_terrain_rings.json`
- `QuestFlightLab/Assets/Resources/QuestFlightLab/Environment/KBDU/kbdu_reference_context.json`
- `tools/kbdu_environment/kbdu_source_manifest.json`

The manifest pins source requests, retrieval time, raw/derived SHA256 values, source agencies, licenses, attribution, budgets, and generator/runtime toolchain hashes. Raw FAA/USGS/OSM responses remain in the external workstream artifact directory, not in Git. See `docs/ASSET_SOURCES.md` for the source/license record.

This path improves geographic context but remains simplified, quantized, and visually stylized. It is not an aeronautical database, surveyed airport reconstruction, navigation product, performance proof for Quest hardware, or final photorealistic environment.

The runtime terrain layers are fixed meshes, not camera-dependent terrain LODs. Each outer ring now replaces its first coarse cell band with a transition mesh that shares every finer-ring boundary vertex. This removes the former 50 m overlap at the 2 km join and the independently sampled height discontinuity at the 6 km join. The production camera's fogged 18 km far plane encloses the full 24 km square, including its 16.97 km corners, so view angle does not slice the mountain silhouette. If a procedural fallback world already exists when real data becomes valid, it is deactivated rather than rendered over the USGS terrain.

## Quest Visual Stability V2 Water And Ground Rendering

The committed OSM derivative remains the source for 620 linear waterways and 180 water polygons. Runtime construction no longer feeds water through generic unsmoothed ribbons or centroid fans. `WaterwayMeshBuilder` now:

- applies deterministic endpoint-preserving corner cutting and 12 m arc-length resampling to linear features;
- rejects self-intersecting centerlines and enforces a 55° maximum resampled turn;
- generates arc-length-UV ribbon meshes with near-airport dirt banks;
- ear-clips actual reservoir polygons at one fixed horizontal level;
- maintains at least 0.12 m accepted terrain separation (0.18 m nominal clearance) instead of the previous 0.045 m near-coplanar layer;
- uses one opaque, ZWrite-enabled, non-animated material with no transparency, refraction, screen-space reflection, or realtime probe dependency;
- keeps water renderers free of distance LOD/crossfade so a seated head turn cannot swap their mesh or enabled state.

The explicit procedural fallback uses the same stable mesh/material path instead of thin transparent reservoir cubes. This geometry remains contextual and not hydrologically surveyed. Ground land-cover rendering uses the three Poly Haven CC0 maps and import policy documented in `docs/ASSET_SOURCES.md`; the OSM/FAA/USGS data licenses and attribution above are unchanged.

## Production Vertical Slice V2 Authored Path

The production scene consumes `Assets/Production/Environment/ProductionEnvironmentRoot.prefab`. Its terrain, FAA runway, OSM context, Boulder Reservoir, materials, and macro texture are baked in the Editor and referenced directly; runtime code validates/reports the contract but does not construct the production environment.

- near zone: 4 km USGS terrain plus bounded airport/context geometry;
- mid zone: 12 km lower-detail USGS terrain and selected combined OSM context;
- far zone: one immutable USGS-derived 24 km terrain mesh with no `LODGroup`, crossfade, camera scaling, or runtime rebuild;
- runway: one FAA endpoint-derived linear grade, one offline terrain blend, one tessellated pavement mesh shared by rendering and collision, and one combined depth-biased marking mesh;
- water: Boulder Reservoir only, with opaque ZWrite water and a stable shore bank; low-value minor hydrography is excluded;
- macro appearance: unique 1024×1024 project-authored USGS/OSM land-cover derivative with irregular warped parcels and continuous variation, plus the documented Poly Haven CC0 microdetail.

The macro map is a muted synthetic fallback, not public-domain orthophoto imagery. Pattern-altitude and headset Visual QA remain required before claiming it is visually convincing.
