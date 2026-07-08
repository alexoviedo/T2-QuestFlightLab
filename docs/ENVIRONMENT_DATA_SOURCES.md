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
