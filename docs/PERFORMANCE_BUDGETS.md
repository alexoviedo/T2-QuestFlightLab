# Performance Budgets

## Quest Default Direction

The playable Quest demo should prefer deterministic, local, bounded scenery over streaming or heavy renderer experiments.

Current `visual_fidelity_demo` budget:

| Area | Current Budget |
| --- | --- |
| Environment footprint | 8.8 km x 7.8 km KBDU-inspired visual area |
| Terrain chunks | 81 mesh chunks |
| Far scenery | Low-cost ridge impostor meshes |
| Airport clutter | Procedural hangars, lights, cones, field/road/perimeter cues |
| Visual QA LOD groups | 63 in the 2026-07-08 run |
| Render quality | 4x MSAA in visual QA/runtime visual profile; Android project default Medium uses MSAA |
| Texture source | Runtime procedural material/noise textures; no large downloaded textures committed |
| Gaussian splats | Diagnostic/fallback only until Quest XR stereo/world-lock issue is fixed |

## Instrumentation

`WorldPerformanceBudget` records the world profile, footprint, terrain chunk count, LOD group count, and notes. `VisualQaBatchRunner` includes this status and the active render-quality profile in its Markdown/JSON output.

## Guardrails

- Do not commit large raw terrain, APK, screenshot, video, or splat files.
- Do not add full geospatial streaming to `main` until a research branch proves build stability and memory/performance feasibility.
- Keep `visual_fidelity_demo` green in visual QA, editor scenarios, PlayMode, and APK build after scenery changes.
- Prefer LOD/impostors and small optimized CC0/procedural assets over large photogrammetry drops.

## Current Limitations

These budgets are editor/build guardrails, not final Quest runtime performance proof. A future Quest smoke must capture headset/logcat/frame timing for the visual-fidelity mode before claiming headset performance.
