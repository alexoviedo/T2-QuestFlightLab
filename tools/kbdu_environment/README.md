# KBDU Real-Data Environment Pipeline

This standard-library Python pipeline produces bounded, Quest-oriented KBDU terrain and reference-context assets from public/open data. It does not use Google Maps, Google Earth, Street View, paid data, or raw imagery as committed content.

The shared local tangent origin is the FAA airport reference point:

- latitude: `40.03936527` degrees north
- longitude: `-105.22608958` degrees east (`105.22608958` degrees west)
- elevation: `5288 ft MSL` / `1611.7824 m MSL`
- Unity mapping: local ENU, `+X=east`, `+Y=up`, `+Z=north`

The output is explicitly **KBDU-inspired / not for navigation**.

## Sources

- FAA-derived USDOT/BTS NTAD airport and runway services: U.S. government work, unrestricted public use.
- USGS 3DEP Bare Earth DEM dynamic service: public-domain U.S. government elevation data.
- OpenStreetMap through bounded Overpass queries: ODbL 1.0; runtime attribution must show `© OpenStreetMap contributors`.
- USGS/USDA NAIP Plus: a bounded 4 km availability query is recorded. No imagery pixels are downloaded or committed in this version.

Every request URL, request hash, retrieval time, raw relative path, byte count, SHA256, source agency, and license is recorded in the external `fetch_receipt.json` and committed `kbdu_source_manifest.json`.

## Rebuild

Use Python 3.11 or newer. There are no third-party Python dependencies.

```powershell
$raw = "C:\path\outside\the\repo\kbdu_raw_YYYYMMDD"
python -B tools/kbdu_environment/fetch_kbdu_sources.py --raw-dir $raw
python -B tools/kbdu_environment/process_kbdu_environment.py --raw-dir $raw
python -B tools/kbdu_environment/validate_kbdu_assets.py --raw-dir $raw --report "C:\path\outside\the\repo\kbdu_validation.json"
python -B tools/kbdu_environment/render_kbdu_preview.py --output "C:\path\outside\the\repo\kbdu_data_preview.png"
python -B -m unittest discover -s tools/kbdu_environment/tests -v
```

If a fetch is interrupted before `fetch_receipt.json` is written, resume only that incomplete directory:

```powershell
python -B tools/kbdu_environment/fetch_kbdu_sources.py --resume --raw-dir $raw
```

Never point `--raw-dir` inside the repository. The fetcher refuses to overwrite a completed snapshot.

## Committed Outputs

- `QuestFlightLab/Assets/Resources/QuestFlightLab/Environment/KBDU/kbdu_terrain_rings.json`
  - 10 m airport patch, 40 m inner ring, 150 m mid ring, and 400 m far ring.
  - exact 4 km, 12 km, and 24 km square coverage.
  - signed int16 decimeter heights, little-endian and base64 encoded relative to the FAA datum.
  - 30,304 stored samples and 54,768 source-grid triangles; runtime seam stitching adds only the transition triangles needed to join independently sampled rings.
  - primary and turf runway terrain is smoothly blended to planes between USGS-sampled FAA endpoints.
- `QuestFlightLab/Assets/Resources/QuestFlightLab/Environment/KBDU/kbdu_reference_context.json`
  - FAA runway polygons/endpoints and selected official attributes.
  - clipped/simplified/quantized OSM aeroway, building, road, water, land-cover, and barrier features.
  - deterministic OSM-tag-driven macro material IDs and a project-authored color/roughness palette.
- `tools/kbdu_environment/kbdu_source_manifest.json`
  - source/license/raw hash/derived hash/toolchain provenance.

## NAIP Decision and Fallback

The automated inner-ring query proves NAIP catalog coverage at KBDU. The service advertises a 4000 x 4000 maximum export, while a 24 km square at 1 m resolution is roughly 24,000 x 24,000 pixels before tiling. The project does not yet have a validated seam-safe tiler, color normalizer, mip workflow, and Android ASTC import path. A one-image downsample would either exceed the 750 KB macro-texture budget or erase useful airport/field detail.

The current output therefore uses deterministic macro material regions derived from OSM categories, with a self-generated default dry-prairie material. No raw or downsampled NAIP image is committed. A future imagery pass should tile each ring, normalize seams, generate mips, enforce ASTC import settings, and capture before/after Unity Visual QA before enabling it.

## Unity Integration

`RealKbduEnvironmentBuilder` implements the integration while keeping the existing procedural mesh environment as an explicitly selectable fallback. It:

1. Load both JSON files as `TextAsset` resources.
2. Decode each terrain layer's `height_dm_little_endian_base64` in row-major south-to-north order.
3. Convert each signed value to MSL with `origin.elevation_msl_meters + value * height_quantization_meters`, then subtract the same origin elevation for Unity-local Y.
4. Build separate fixed meshes for the airport patch and near/mid/far rings; omit the declared airport cutout and stitch every finer-ring boundary vertex into the adjacent coarse transition band.
5. Keep terrain free of distance-based LOD swaps and camera culling, disable far shadows, and prevent coarse geometry from overlapping beneath an inner ring. The production camera covers the complete 24 km square through its 18 km fogged far plane.
6. Reconstruct flat vector coordinate pairs as `(points_q[i], points_q[i+1]) * coordinate_quantization_meters`; batch/instance by `macro_material_id`, category, and ring.
7. Retain `© OpenStreetMap contributors` in an accessible credits/about surface whenever the OSM-derived environment is distributed.
8. Treat FAA runway context and the runway blend as visual reference only, never as navigation or surveyed pavement data.

Set `QFL_FORCE_PROCEDURAL_KBDU=1` to exercise the preserved procedural fallback. When real terrain becomes active, any existing procedural world is disabled so its synthetic Front Range cannot overlap the USGS silhouette. The real-data path spatially combines the 5,522 source features into bounded category/material/ring batches, applies LOD and distance culling only to vector context (not terrain), and rejects the build if renderer or triangle budgets are exceeded. `TryGetRecommendedPavedRunwayStart` exposes a slope-aware centerline pose inset from the FAA runway 08 endpoint.

Enabling these assets changes scene visuals. The project rule therefore requires inspected before/after Visual QA and an integrated Quest performance report before release. Editor evidence is not on-device Quest performance evidence.
