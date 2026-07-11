# Performance Budgets

## Quest 3 production target

`visual_fidelity_demo` targets 72 Hz on Quest 3, a 13.89 ms frame budget, no more than roughly 300 busy-scene draw calls, and no more than roughly 1.3 million visible triangles. These are acceptance targets, not guarantees.

The Android render policy currently uses:

| Setting | Android value |
| --- | --- |
| Graphics / XR | Vulkan, OpenXR, single-pass multiview |
| Anti-aliasing | 4x MSAA |
| Eye texture scale | 1.00 |
| Fixed foveation | 0.45 when supported |
| LOD bias | 1.25 |
| Texture policy | ASTC build compression, mipmaps, forced anisotropic filtering |
| Far clip / haze | 18,000 m / exponential fog |
| Directional shadows | 40 m, two cascades, low-resolution mobile profile |
| First-person cockpit shadows | Realtime casting and receiving disabled; stable direct/specular light and static sky reflections retained |
| Reflections | 128 px static sky reflection, no realtime probes |

Disabling cockpit shadow-map sampling is a stability workaround for the imported model's visible low-resolution moving patches. It is not equivalent to physically complete cockpit self-shadowing and still needs a temporal headset confirmation.

## Current world and static budget

The real-data KBDU world uses four terrain layers over 4 km, 12 km, and 24 km coverage, stitched deterministic transition bands, 5,522 source OSM features, and FAA runway geometry. The integrated builder reports 164 world renderers and approximately 121,787 world triangles before the rest of the scene is included.

Latest deterministic Editor Visual QA (`final_visual_qa_preload_alignment`):

```text
scene renderers                 781
estimated frustum renderers     104
estimated instanced draw calls   73 / 300
estimated visible triangles  97,079 / 1,300,000
unique materials / shaders       49 / 3
LOD coverage after optimization 381 / 781
```

The estimates pass the static draw-call and triangle guardrails. Editor per-shot hardware counters were unavailable, so these values are scene-audit/frustum estimates rather than Quest GPU counters.

## On-device result

The most recent valid Quest 3 timing sample was captured before the final startup-alignment, diagnostic-throttling, LOD-filtering, and cockpit-shadow changes:

```text
sample time                     2026-07-10 23:18 UTC
average frame time              14.53 ms (68.84 estimated FPS)
p95 / p99 frame time            15.15 / 23.70 ms
CPU p95                         15.46 ms
GPU timing                      unavailable (reported 0.00 ms)
frames over 13.89 ms            87 / 180 (48.3%)
instanced draw-call estimate      139 / 300
visible triangle estimate     434,431 / 1,300,000
materials / variant signatures    71 / 10
72 Hz plausibility              FAIL
```

Evidence: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_sim_v1_20260710_151201\quest_visual_refinement_smoke\pulled_render_performance\render_performance\render_performance_20260710_231814.md`.

The final APK relaunch at 23:26 UTC was intercepted by Horizon OS because the Touch controllers were unavailable/asleep, and the headset then slept. Unity never started, so the old files pulled during that attempt are not final performance evidence. The optimized build's 72 Hz result therefore remains unproven.

## Guardrails

- Do not commit Unity `Library`, `Temp`, `Obj`, `Logs`, `Builds`, APKs, raw captures, package caches, or raw geospatial downloads.
- Keep the mesh/terrain fallback available; experimental splats are not the production default.
- Add scenery only when it preserves the draw-call, triangle, memory, and temporal-stability budgets.
- Use instancing, shared materials, deterministic LOD/culling, mipmaps, and atmospheric distance treatment.
- Treat Quest frame timing, OVR/Meta GPU counters, thermals, stereo comfort, shimmer, and shadow stability as headset-only evidence.

## Next performance gate

With the headset worn and both Touch controllers awake, run three 60-second representative pattern segments after a 90-second warmup. Each run must show p95 frame time at or below 13.89 ms, no startup-alignment failure, stable mountains through head pans, and no cockpit shadow crawl. Capture Meta/OVR GPU counters if available; Unity's zero GPU timing is not sufficient.
