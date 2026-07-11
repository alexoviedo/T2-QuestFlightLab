# Quest Visual Stability V2 Witness — 2026-07-11

## Classification

**FAIL / NOT HEADSET-ACCEPTED.** The scoped implementation, deterministic visual QA, machine temporal invariants, regression tests, Android build, and three off-head cold-launch smokes completed. Alex then reported that he could not wear the headset. The required human stereo sweep, three worn-headset launches, and final 90-second warmup plus 60-second timing measurement therefore did not run. This milestone is not classified as PASS or PARTIAL.

This is a visual-stability development witness, not a claim of production photorealism, final training suitability, navigation suitability, or FAA approval.

## Build and device identity

- Source parent SHA: `ea174b79e76fc0a53f659c4cbebf8cf849e8f7b0` on `main`. The final integration SHA is the commit containing this witness and is reported in the delivery response.
- Unity: `6000.3.8f1`.
- Device: Meta/Oculus Quest 3, Android 14.
- Device build: `UP1A.231005.007.A1`, incremental `52270740038100520`.
- Package: `com.alexoviedo.t2.questflightlab`.
- Final APK: `QuestFlightLab/Builds/Android/QuestFlightLab-v0.1-dev.apk`, 172,431,843 bytes, SHA256 `8A34934BEB691037D2258118E5088554AAFD3407051102203F7C191FA107223D`.
- Active runtime environment root: `KBDU_Approx_Airport_NotForNavigation/KBDU_RealData_World_NotForNavigation`.
- Runtime profile: `real_kbdu_usgs_faa_osm_v1`; procedural fallback inactive when real data is authoritative.

## Default eye position

Aircraft local `+Z` is forward. The corrected named setting is `defaultPilotEyeAftMeters = 0.10`, applied to `PilotSeatAnchor` as local `-Z`; the OpenXR-owned Main Camera is never written.

| Evidence | Former default | Corrected default |
| --- | ---: | ---: |
| Pilot eye local | `(-0.28, 0.94, 0.00) m` | `(-0.28, 0.94, -0.10) m` |
| Authored eye-to-panel separation | 0.317 m | 0.417 m |
| Editor outside-view ratio | 0.543 | 0.489 |
| Instrument/runway/horizon readability | PASS | PASS |

Saved calibration remains additive. Reset returns to the corrected default, startup origin recenter preserves the default offset, tracked head movement does not alter aircraft pose, and aircraft movement carries the seat frame. These paths pass PlayMode tests. Off-head Quest diagnostics reproduced local eye Z `-0.10 m` and 0.417 m panel separation on all three cold starts. Worn-user plausibility remains unconfirmed.

## Cockpit depth and shading

Blender batch baking was unavailable. The permitted deterministic fallback applies one camera-independent static vertex-depth bake to eight major glTF cabin/panel/seat/yoke meshes:

- strength `0.30`, bounded to `0.00–0.42`;
- 99,792 vertices; minimum color factor `0.709`;
- no additional material, texture sample, pass, screen-space overlay, or per-frame work;
- direct diffuse/specular response and static sky reflections remain enabled;
- instrument faces, displays, labels, and glass are excluded;
- 51/51 cockpit realtime shadow casters/receivers disabled, with zero remaining;
- six coarse AO inputs and one coarse normal input remain neutralized.

Deterministic before/after captures show stronger static cabin/panel depth and the corrected framing. Machine tests prove the bake is bounded, deterministic, idempotent, and does not reintroduce realtime cockpit shadow maps. A worn slow-head-sweep flicker witness did not run.

## Mountain temporal stability

Root cause: the authoritative USGS transforms were already static, but the 24 km far ring used a coarse 400 m render grid and each terrain ring independently recalculated normals. Reused long-lived roots could also fail to re-suppress legacy Front Range ridge, snow-cap, haze, or impostor renderers.

Correction:

- deterministic 200 m far-ring render mesh: 11,240 vertices / 21,680 triangles;
- normals sampled from one shared terrain sampler across ring seams;
- one real-data mountain source; procedural and legacy roots are mutually exclusive;
- no terrain LODGroup, crossfade, dither, distance culler, runtime regeneration, camera-facing behavior, distance scaling, or transparent haze cubes.

`MountainTemporalStabilityProbe` records renderer active state, matrix/mesh/material identity, bounds, scale, hierarchy, LOD state, distance, and clipping state. The Editor head sweep passed 9 frames / 36 renderer samples across four fixed terrain renderers with immutable transforms, meshes, and renderer set. Classification is `PASS_MACHINE_INVARIANTS_HUMAN_WITNESS_REQUIRED`; headset silhouette stability is not yet witnessed.

## Water geometry and material

Root cause: active OSM waterways used unsmoothed generic ribbons/centroid fans only 0.045 m above terrain; the fallback used thin transparent cubes at roughly 0.018 m. This caused angular paths, strong specular striping, and depth competition.

Correction:

- 601/620 OSM linear features and 180/180 water polygons render; 19 invalid/self-intersecting or turn-threshold lines are rejected;
- endpoint-preserving smoothing and 12 m arc-length resampling;
- 13,655 output centerline points, 23,978 water triangles, and 11,864 bank triangles;
- 166 near-airport bank ribbons;
- fixed polygon triangulation for reservoirs and deterministic ribbon meshes for waterways;
- nominal and measured minimum terrain separation `0.180 m`;
- one shared opaque, ZWrite-enabled, non-animated water material;
- no transparent overlap, SSR, refraction, realtime reflection probe, water LOD, or distance-based renderer switching;
- spatial batching remains bounded instead of creating one renderer per feature;
- procedural fallback uses the same stable mesh/material path rather than thin cubes.

Static before/after QA shows smooth drainage paths and no zebra/depth striping. Temporal headset confirmation remains unrun. Reservoirs are contextual fixed-level polygons, not a hydrologically cut terrain simulation.

## CC0 ground material and anti-tiling

The former active ground used a repeating generated 96×96 grayscale detail map. It is replaced by three official Poly Haven 1024×1024 diffuse maps under CC0 1.0:

| Asset | Author | SHA256 |
| --- | --- | --- |
| Withered Grass | Charlotte Baglioni | `8BEBFB639EF74F651BC445526C46C845BEEE52A86D831848B82EB2BBBF2D98EE` |
| Sparse Grass | Amal Kumar | `AE94F2B34597B9108EEFD88217F55ECCAEC6D6B382E858A478EE92DF90E66617` |
| Dry Ground 01 | Rob Tuytel | `75222FC97A82B635A09CF8F2891DD58BC42E49599B9FDA920B2C50193FE85E9F` |

Exact official download URLs and authorship are recorded in `docs/ASSET_SOURCES.md`. No archive or raw higher-resolution download is committed. Import policy enforces mip streaming, Repeat wrap, trilinear filtering, anisotropic level 4, 1024 maximum, and Android ASTC 6×6 HQ.

The shared three-sample Quest shader uses 15 m micro detail, 220 m macro variation, OSM land-cover blends, and a 350–2,400 m micro-detail fade. Terrain rings retain globally continuous world mapping. Context batches vary secondary maps through deterministic `MaterialPropertyBlock` rotation/phase without cloning materials. Static cockpit, overview, and pattern-altitude captures show no obvious tile grid; headset distance shimmer remains unconfirmed.

## Baseline and final performance evidence

The baseline worn-headset run is valid and was reparsed from app PID `24021`, filtering app VrApi rows with `Fov=2` / `SF=1.00` and excluding vrshell rows.

| Metric | Baseline worn Quest | Final integrated |
| --- | ---: | ---: |
| Delivered interval average | 13.918 ms | NOT RUN on-head |
| Delivered interval p95 / p99 | 16.388 / 19.729 ms | NOT RUN on-head |
| Samples over 13.889 ms | 91/180, 50.56% | NOT RUN on-head |
| VrApi App average / p95 | 6.238 / 7.612 ms | NOT RUN on-head |
| VrApi CPU&GPU average / p95 | 10.974 / 13.334 ms | NOT RUN on-head |
| VrApi CPU&GPU samples over budget | 5.0% | NOT RUN on-head |
| GPU / CPU utilization average | 56.1% / 23.3% | NOT RUN on-head |
| Stale / tear | 127 / 0 over 60 app rows | NOT RUN on-head |
| Draw calls | 114 estimated | 75 Editor estimate; on-device NOT RUN |
| Visible triangles | 376,911 estimated | 124,075 Editor estimate; on-device NOT RUN |
| Real-data environment total | 121,787 triangles / 164 renderers | 166,775 triangles / 178 renderers |
| Runtime materials / textures | 71 scene materials / 46 textures | 17 environment materials / 4 environment textures |

Direct GPU milliseconds were unavailable. Baseline evidence supports a mixed GPU/compositor-pacing diagnosis: combined timing and stale frames reach the cadence budget without either utilization counter showing exclusive saturation. Unity `Time.unscaledDeltaTime` is delivered cadence and naturally centers near 13.889 ms at synchronized 72 Hz; the new evidence runner reports it separately from app workload rather than silently substituting one for the other.

The final hard performance gate is **NOT RUN**. Editor estimates are inside draw/triangle limits but are not substitutes for the required final Quest capture.

## Three-launch table

These are cold-start machine smokes with the headset unworn, not the requested worn-headset acceptance launches.

| Launch | Real environment | Eye / panel | Cockpit policy | Water | Faults / thermal | Worn visual gate |
| --- | --- | --- | --- | --- | --- | --- |
| Off-head 1 | 178 renderers / 166,775 tris | -0.10 m / 0.417 m | depth 0.30; 0 casters/receivers | 0.180 m; opaque | none / none | NOT RUN |
| Off-head 2 | 178 renderers / 166,775 tris | -0.10 m / 0.417 m | depth 0.30; 0 casters/receivers | 0.180 m; opaque | none / none | NOT RUN |
| Off-head 3 | 178 renderers / 166,775 tris | -0.10 m / 0.417 m | depth 0.30; 0 casters/receivers | 0.180 m; opaque | none / none | NOT RUN |

All three correctly reported `waiting for tracked HMD`, zero seat stable frames, and zero startup recenter count. No crash, ANR, OOM, Unity exception, or thermal warning occurred. The app was force-stopped after each capture and the original Quest stay-awake setting was restored.

## Tests and build

- Visual QA: 17/17 PASS.
- Editor scenarios: 33/33 PASS.
- PlayMode: 66/66 PASS.
- KBDU data/provenance validation: 8/8 PASS.
- Mountain machine sweep and mutation detection: PASS.
- Water smoothing, self-intersection, depth separation, deterministic output, batching, opaque material, and budget tests: PASS.
- Ground world-space continuity/material-property, import, mip/filter, and ASTC tests: PASS.
- Temporal performance metadata/percentile/observer-allocation tests: 4/4 PASS.
- Android build: PASS.
- Temporal PowerShell AST and baseline parser regression: PASS.
- Native JSBSim regression: expected FAIL, 1/10 scenarios, unchanged in classification and still opt-in. No flight-model file was modified.

## Evidence paths

- Artifact root: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\visual_stability_v2_20260711_004045`.
- Baseline report: `baseline_temporal_visual_report.md`.
- Final deterministic screenshots/contact sheet: `final_visual_qa\visual_qa_contact_sheet.png`.
- Ground/water before-after sheet: `workstream_c_water_ground_visual_qa\visual_qa_before_after.png`.
- Baseline worn screenshot: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_sim_v1_20260710_151201\quest_acceptance_20260711_000758\adb_screenshot_final.png`.
- Final off-head Quest startup screenshot: `final_off_head_quest_smoke_rerun\pulled_device_evidence\first_view_diagnostics\quest_first_view_startup_20260711_074815.png`.
- Final Visual QA: `final_visual_qa\visual_qa_summary.md`.
- Final Editor scenarios: `final_editor_scenarios\flight_core_summary.md`.
- Final PlayMode: `final_playmode\unity_PlayMode_test_results.xml`.
- Final native JSBSim: `final_jsbsim_native_validation\jsbsim_native_scenario_summary.md`.
- Baseline metric parser: `final_parser_regression\quest_temporal_visual_gate_summary.md`.
- Off-head launches: `final_off_head_quest_smoke_rerun`, `final_off_head_quest_launch_2`, and `final_off_head_quest_launch_3`.
- APK build log: `QuestFlightLab\Logs\build_android.log` (generated, not committed).
- Final screen recording: NOT CREATED because Alex could not wear the headset. Earlier baseline framebuffer recording attempts were unsupported/zero-byte and are not treated as evidence.

## User confirmation and remaining blockers

Alex supplied the baseline visual observations used by this gate. He later explicitly stated that he could not currently put on the headset. There is therefore no final user confirmation for eye comfort, cockpit temporal stability, mountain silhouette stability, water flicker, grass shimmer, or 72 Hz timing.

Remaining blockers:

1. Three worn-headset cold launches with both controllers awake.
2. Human slow pan across cockpit, mountains, water, and grass.
3. Exact 90-second warmup plus 60-second final capture and, if required, one targeted optimization/rerun.

The next milestone is to run `scripts/run_quest_temporal_visual_gate.ps1` with the headset worn, classify this V2 gate from the resulting evidence, and only then return to the deferred physics milestone.
