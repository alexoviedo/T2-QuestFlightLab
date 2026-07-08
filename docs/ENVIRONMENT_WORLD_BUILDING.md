# Environment World Building

## Current Direction

The immediate Quest demo path is a controlled KBDU-inspired world, not Cesium/Google streaming and not a surveyed airport reconstruction.

`visual_fidelity_demo` currently uses:

- approximate KBDU Runway 08/26 runway/taxiway/apron geometry,
- enhanced runway markings, lights, rubber wear, apron seams, hangars, fuel/clutter, and grass variation,
- an expanded 8.8 km x 7.8 km procedural world around the airport,
- 81 terrain mesh chunks with subtle elevation variation,
- airport-adjacent roads, field strips, perimeter fencing, ramp parking cues, industrial buildings, reservoir/drainage hints, and far Front Range-inspired ridge impostors.

This is intended to look more like a believable high-plains airport training environment while staying small, local, deterministic, and build-safe for standalone Quest.

## Implementation

- `KbduApproxAirport` builds the base approximate runway scene.
- `AirportRuntimeEnhancer` improves the runway/apron detail and hides training scaffolding in playtest mode.
- `KbduInspiredWorldBuilder` adds the larger environment footprint.
- `VisualQaBatchRunner` captures cockpit, runway, aircraft, airport overview, terrain, far scenery, and ground-detail shots.

The older blocky placeholder foothills are hidden in playtest/HUD mode so the expanded world builder owns the far-scenery read.

## Asset Source

The v1 environment is project-owned procedural Unity geometry/material generation. No paid assets, raw downloaded terrain, Google imagery, or unclear-license models are committed for this pass.

## Known Limits

- Not navigation-accurate KBDU.
- Not production photorealism.
- No real satellite/photogrammetry terrain is committed.
- No Quest headset frame-time proof was captured in this chunk.
- Terrain/materials are still stylized and need a future CC0 material/HDRI pass.
