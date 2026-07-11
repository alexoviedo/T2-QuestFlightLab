# Production Simulator V1 Gate — 2026-07-10

## Decision

**Gate result: NOT PASSED. Recommendation: CONTINUE BUT NARROW SCOPE.**

This pass materially corrects the aircraft/XR reference frame, adds a reproducible real-data KBDU environment, proves Windows x64 and Android ARM64 JSBSim native packaging, and improves the Quest render policy. It does not yet establish an acceptable headset startup view, stable cockpit lighting on-device, 72 Hz, photorealism, or a physically acceptable native JSBSim control schedule.

Artifact root:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_sim_v1_20260710_151201
```

Native build artifact root:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_sim_v1_20260710_151319\jsbsim_native_gate
```

## Baseline and final quality scores

The final score includes the user's last valid headset feedback. It does not convert green Editor captures into headset proof.

| Quality dimension | Baseline | Final | Assessment |
| --- | ---: | ---: | --- |
| Default cockpit viewpoint | 5 | 5 | The hierarchy and preload alignment are improved, but the last runnable headset view began in the wrong position and the replacement path did not launch. |
| Sense of being seated in aircraft | 3 | 4 | The architecture is now aircraft-relative; embodied headset confirmation is still missing. |
| Cockpit readability | 6 | 6 | Static views are readable, but headset placement and temporal lighting remain unresolved. |
| Aircraft/world transform credibility | 3 | 7 | Explicit CG, visual, seat, calibration, XR, and tracked-camera ownership now pass invariance/head-motion tests. |
| Airport realism | 3 | 5 | FAA/OSM geometry and real placement replace much of the procedural sandbox, but buildings/materials remain stylized. |
| Terrain realism | 2 | 5 | USGS elevation gives real relief and scale; ground treatment remains macro/vector rather than aerial-photoreal. |
| Distant scenery realism | 3 | 5 | Real Front Range relief and stitched rings are present; atmosphere/material rendering remains visibly simplified. |
| Lighting/material quality | 3 | 4 | Stable direct/specular response and static reflections remain, but cockpit shadow maps are disabled as a stability workaround and the final result is unconfirmed in-headset. |
| Jaggedness/shimmer risk | 4 | 5 | 4x MSAA, mipmaps, anisotropy, haze, and LOD policy help; temporal headset proof is missing. |
| World scale | 6 | 8 | The 24 km real-elevation footprint supports local-area and pattern context. |
| Flight motion credibility | 4 | 4 | Unity fallback scenarios pass, but the native JSBSim physical gate fails 9/10 cases. |
| Overall headset-demo readiness | 3 | 4 | The foundation is much stronger, but the user's last headset result is still unacceptable and the final device gate is incomplete. |

Baseline source: `baseline_quality_review.md`. Final contact sheets:

- `final_visual_qa_preload_alignment\visual_qa_contact_sheet.png`
- `final_visual_qa_preload_alignment\visual_qa_before_after.png`

The final deterministic Visual QA produced 17/17 valid captures. The images are materially more geographically grounded than baseline, but they remain stylized and do not justify a production-photoreal claim.

## XR hierarchy before and after

Before:

```text
C172_Style_Trainer_Prototype
  AircraftState + SimpleAircraftPhysics
  aircraft/cockpit visuals

XR Origin  (separate world root, repositioned in LateUpdate/before-render)
  Camera Offset
    Main Camera  (TrackedPoseDriver plus manual transform writes)
      HUD / telemetry / menu
```

The old path conflated simulation and presentation, recomputed XR Origin from the camera, and manually wrote the tracked camera. That could produce the observed aircraft-around-player motion.

After:

```text
AircraftSimulationRoot  (authoritative pose; CG rotation pivot)
  CenterOfGravityReference
  AircraftVisualRoot  (single interpolated presentation owner)
    imported aircraft/cockpit model
    PilotSeatAnchor  (-0.28, 0.94, 0.00 m default eye)
      UserViewCalibrationOffset
        XR Origin
          Camera Offset
            Main Camera  (OpenXR TrackedPoseDriver only)
          LeftHand Controller
          RightHand Controller
```

The camera is not an aircraft pivot and receives no manual transform writes. Synthetic heading/bank, head-motion, aircraft-motion, recenter, and single-authority tests pass. Startup alignment is armed before asynchronous cockpit loading, waits for ten stable tracked/user-present frames, and moves only XR Origin once. This exact startup path still needs a worn-headset confirmation.

## Viewpoint calibration

The in-cockpit calibration panel is available through Touch Menu with an Editor keyboard fallback. It supports forward/back, left/right, up/down, small/large step, limited yaw, recenter, save, reset, and cancel/revert. Opening it does not pause or consume Xbox/HOTAS/rudder/throttle axes.

Schema 5 persistence stores only the versioned per-aircraft calibration offset. Save/reload/reset, legacy migration, failure handling, and cancel rollback pass automated coverage. Touch-controller bindings and startup alignment still need manual Quest verification.

See [COCKPIT_VIEWPOINT_CALIBRATION.md](../COCKPIT_VIEWPOINT_CALIBRATION.md).

## Terrain and environment

The committed environment is reproducibly derived from:

- FAA/BTS public airport and runway data for KBDU/BDU.
- USGS 3DEP/The National Map public-domain elevation.
- OpenStreetMap context under ODbL 1.0 with contributor attribution.
- NAIP public imagery availability metadata only; imagery was not downloaded or committed.
- Deterministic project-generated macro materials where no committed imagery is used.

No Google Maps, Google Earth, or Street View-derived content is used.

Validated result:

- Four terrain layers: airport patch, 4 km inner, 12 km mid, and 24 km far.
- 30,304 height samples and 54,768 derived terrain triangles.
- 1,090.4 m source elevation relief over the full footprint.
- 5,522 OSM source features / 31,533 points.
- FAA runway 08/26: 1,250.39 m by 22.86 m, 89.972 degrees true; DEM endpoint delta -2.919 m.
- Runtime world: 164 renderers, approximately 121,787 world triangles.
- Fine-to-coarse borders use deterministic stitched transition bands, and the 18 km far clip covers the 16.97 km square corners. The prior view-angle clipping and overlapping independent ring seams were removed.
- Procedural mesh fallback remains available.

The latest provenance/geometry/budget validator passes all 190 checks:
`final_kbdu_validation_preload_alignment.json`. Python unit tests pass 8/8.

See [ENVIRONMENT_DATA_SOURCES.md](../ENVIRONMENT_DATA_SOURCES.md) and [ASSET_SOURCES.md](../ASSET_SOURCES.md).

## Rendering and performance

Android policy:

- Vulkan, OpenXR single-pass multiview, ASTC, multithreaded rendering.
- 4x MSAA, eye scale 1.00, LOD bias 1.25, fixed foveation 0.45 when supported.
- 18 km far clip, mipmaps, forced anisotropic filtering, haze, instancing, LOD/culling.
- Static 128 px sky reflection; realtime reflection probes disabled.
- Imported cockpit realtime shadow casting/receiving disabled for all 51 renderers; active AO/lightmap-like inputs and one coarse interior normal atlas neutralized. Direct diffuse/specular light and static sky reflections remain.

Latest Editor scene estimate:

```text
estimated instanced draw calls     73 / 300
estimated visible triangles   97,079 / 1,300,000
materials / shaders               49 / 3
LOD coverage                     381 / 781 renderers
```

Static evidence:

- `final_visual_qa_preload_alignment\performance_budget_report.md`
- `final_visual_qa_preload_alignment\render_quality_report.md`

Most recent valid Quest 3 timing, captured before the last optimization/startup/shadow changes:

```text
average frame time       14.53 ms
p95 / p99                15.15 / 23.70 ms
CPU p95                  15.46 ms
estimated FPS            68.84
frames over 13.89 ms     87 / 180
72 Hz plausibility       FAIL
```

The final optimized APK has no valid on-device timing sample. See [PERFORMANCE_BUDGETS.md](../PERFORMANCE_BUDGETS.md).

## JSBSim backend

The project now has one-authority backend coordination for:

- `UnityPrototypeFlightBackend` — validated standalone default.
- `JSBSimEditorSidecarBackend` — Editor interactive sidecar.
- `JSBSimNativeFlightBackend` — explicit opt-in only.

JSBSim 1.3.1 is pinned to revision `3b25f25e49b42d0489c04ac805674fc1450ca579` under LGPL-2.1-or-later. A compact C ABI wrapper, centralized units/frames, 120 Hz accumulator, interpolation boundary, Windows x64 DLLs, Android ARM64 shared libraries, runtime XML subset, license, and source record are present.

Native build gate:

- Windows x64: PASS, 7,200 steps, 0.008371 ms average / 0.0099 ms p95.
- Android ARM64: PASS with Unity NDK 27.2.12479018.
- Latest APK inclusion: PASS, 17 required entries, zero missing, mismatched, or wrong-architecture entries.
- Quest native execution: not run because physical acceptance fails and on-device safety/performance are unproven.

## Physics scenarios

- Unity prototype Editor suite: PASS, 33/33 scenarios.
- JSBSim live Editor sidecar: PASS plumbing, 1,801/1,801 samples applied over 60 seconds.
- JSBSim native physical gate: **FAIL, 1/10**. Only ground idle/brake hold passes.

Takeoff heading, rotation timing/climb, Vy climb, level trim, coordinated turns, slow flight, stall recovery, final approach, and go-around violate their physical envelopes. Native step performance passes, but model initialization and control/trim mapping are not acceptable. Native JSBSim therefore remains opt-in and is not the `visual_fidelity_demo` default.

Evidence:

- `final_editor_scenarios_preload_alignment\flight_core_summary.md`
- `final_jsbsim_live_preload_alignment\jsbsim_live_driver_summary.md`
- `final_jsbsim_native_physical_gate\jsbsim_native_scenario_summary.md`

## APK

Final exact-source build:

```text
Path:    QuestFlightLab\Builds\Android\QuestFlightLab-v0.1-dev.apk
Bytes:   172,137,133
SHA256:  15BEBB95B7F5FF8610E63B24A2FB29EF78EFA142E6EF6D0C807B5F8D40D2FA7F
```

The APK itself is ignored and not committed. Inclusion report:
`final_apk_preload_alignment_jsbsim_inclusion.json`.

## Quest runtime result

The user's last valid headset observation was **FAIL**: the app began from the wrong cockpit position and the low-resolution/flickering cockpit shadow effect remained.

Source changes made after that observation:

- Startup seat alignment now begins before the asynchronous cockpit load and recenters only XR Origin after a stable worn-HMD pose.
- Cockpit shadow-map sampling is completely removed, with direct/specular light and static reflections retained.
- Terrain rings are stitched and the far clip covers the full square to remove view-angle/distance morph causes.
- Diagnostics/HUD refresh work is throttled and performance capture now waits for environment, aircraft, alignment, and initial evidence readiness.

The attempted final relaunch at 23:26 UTC did not test those changes. Horizon OS intercepted the app with `common_system_dialog_app_launch_blocked_controller_required`; Unity never started, no app process/crash/ANR occurred, and the unmounted headset went to sleep. The subsequently pulled reports were older device history. Therefore startup position, shadow stability, mountain stability, Touch calibration, and final 72 Hz remain unproven.

Evidence: `quest_startup_shadow_perf_fix_smoke\logcat.txt`, `power.txt`, and `window.txt`.

## Final validation

| Gate | Result |
| --- | --- |
| PlayMode | PASS, 50/50 |
| Deterministic Visual QA | PASS, 17/17 |
| Editor flight scenarios | PASS, 33/33 |
| JSBSim live Editor driver | PASS, 1,801/1,801 |
| KBDU Python tests | PASS, 8/8 |
| KBDU provenance/geometry/budgets | PASS, 190/190 |
| Windows/Android native build | PASS |
| JSBSim native physical scenarios | **FAIL, 1/10** |
| Android APK build | PASS |
| APK native/data inclusion | PASS, 17/17 |
| Quest 72 Hz | **FAIL on last valid sample; final build unproven** |
| Final startup/shadow/mountain headset acceptance | **NOT RUN; OS launch intercept** |

## Limitations and recommendation

Remaining blockers:

- The final preload startup alignment and no-shadow cockpit policy have not been observed in-headset.
- The last valid Quest timing misses 72 Hz and has no usable GPU timing.
- The environment is geospatially grounded but still stylized; no aerial imagery is committed.
- Disabling cockpit shadow maps favors stability over physically complete self-shadowing.
- Native JSBSim fails 9/10 physical scenarios and is not authoritative on Quest.
- Touch menu lifecycle, comfort, shimmer, thermal stability, and mountain temporal stability require human headset checks.
- This is not evidence of FAA approval, training credit, certified C172 fidelity, production photorealism, or broad device compatibility.

**Continue but narrow scope:** freeze additional environment expansion, keep the Unity prototype backend as default, retain native JSBSim as opt-in, and focus the next increment exclusively on headset visual stability and timing.

## Exact next milestone

**Quest 3 visual-stability acceptance gate:** perform three cold launches with the headset worn and both Touch controllers awake. Each launch must record one successful origin-only startup seat alignment with no camera writes; manually prove calibration open/save/reload/reset/recenter; show no cockpit shadow crawl and no mountain morph during head pans and pattern flight; then capture a clean 60-second timing window after a 90-second warmup with p95 at or below 13.89 ms. Do not promote native JSBSim until this headset gate passes.
