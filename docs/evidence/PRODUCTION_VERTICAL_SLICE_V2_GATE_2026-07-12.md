# Production Vertical Slice V2 gate

Date: 2026-07-12
Repository: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab`
Working branch: `production/vertical-slice-v2`
Integration base SHA: `ce56961fcca7087e550417a75dc92815dcd8ec38`
Unity: `6000.3.8f1`
Device reached by ADB: Meta Quest 3 (`eureka`), Android 14 / API 34, build `52270740038100520`

The source commit containing this witness is the final delivery SHA; a Git commit cannot embed its own hash. The delivery record must pair that SHA with this document.

## Gate result

**Decision B — KEEP R&D REPO, START CLEAN PRODUCT REPO.**

The clean authored slice is a useful integrated result and is visibly better than the legacy scene, but this is not a product-quality PASS. The final 16-view inspection scores about **4–5/10 visually**, the production backend remains the Unity prototype, native JSBSim failed one critical case, and no worn Quest performance or stereo-temporal acceptance could be collected. The existing repository should remain the R&D/source repository while the proven production subset is extracted according to [the extraction manifest](PRODUCTION_VERTICAL_SLICE_V2_EXTRACTION_MANIFEST_2026-07-12.md).

Machine Visual QA classification was `PASS_MACHINE_VISUAL_QA_HUMAN_HEADSET_REQUIRED`. That means image integrity, authored contracts, static budgets, and identity invariants passed; it does not override the visual defects below or establish headset quality.

## Architecture: legacy versus production

Legacy `InputLab` remains a regression scene assembled by accumulated runtime repair/bootstrap, procedural environment, scenery-mode, debug/training UI, and backend-selection paths. Alex's temporal headset observations remain authoritative: static screenshots did not predict mountain morphing, tiled grass, unrealistic water, or perceived cockpit position.

`Assets/Scenes/ProductionVerticalSlice.unity` now serializes the important composition before frame zero:

```text
ProductionWorldRoot
├── EnvironmentRoot / ProductionEnvironmentRoot (baked prefab)
├── RunwayRoot (integration anchor; no duplicate renderer)
├── ProductionInputRoot
├── AircraftSimulationRoot (one selected backend / one pose authority)
│   ├── CenterOfGravityReference
│   └── AircraftVisualRoot
│       ├── ImportedAircraftExterior
│       ├── ImportedCockpitInterior
│       ├── AnimatedControlSurfaces
│       └── PilotSeatAnchor
│           └── UserViewCalibrationOffset
│               └── XR Origin
│                   ├── Camera Offset / Main Camera
│                   ├── Left Touch Controller
│                   └── Right Touch Controller
└── ProductionLightingRoot / Sun
```

The final machine contract found exactly one enabled XR Origin, no production `QuestFirstViewRuntimeRepair`, authored rig/camera/controller/backend references, suppressed legacy bootstrap/UI, and no simultaneous native/Unity pose authority. The tracked Main Camera remains OpenXR-owned. Calibration changes `UserViewCalibrationOffset`, not the camera or aircraft pivot.

## Reused and retired systems

| System | Result |
| --- | --- |
| Imported C172 | Reused with explicit interior/exterior visibility and authored hinge wrappers; legal/source limitation remains. |
| Input abstraction | Reused. USB2BLE and HOTAS are not required by the slice and should not enter the clean product dependency closure. |
| `AircraftReferenceFrameRig` | Reused after cleanup as presentation/reference-frame logic, not a hierarchy constructor. |
| Seat persistence concepts | Reused through `PilotSeatProfile` and `ProductionSeatCalibrationController`. |
| FAA/USGS/OSM pipeline | Reused to bake a smaller production environment. |
| Unity prototype backend | Retained as the sole production authority. |
| Native JSBSim | Improved and retained as R&D; not promoted. |
| Visual QA / Quest evidence tools | Extended for the authored production scene. |
| `InputLab`, runtime first-view repair, procedural KBDU builder, splats, sidecar backend, training UI, verbose HUD | Legacy-only; not production dependencies. |

## Pilot viewpoint and cockpit

- Legacy default was reported too close despite a prior 0.10 m aft correction.
- Production nominal eye is `(-0.28, 0.94, -0.18) m` in the imported aircraft seat frame.
- Measured production eye-to-panel distance is `0.497 m`.
- Default and calibrated captures preserve readable primary instruments, glare shield, centerline, runway, and horizon; outside-view ratios were `0.544` and `0.558`.
- Reset/persistence/recenter and aircraft-relative seat behavior passed PlayMode coverage.
- Cockpit-view propeller occlusion was removed without hiding the external aircraft.

Inspection limitation: the panel remains darker and flatter than a convincing finished cockpit, and the required instrument/yoke QA framing shows instruments but does not clearly prove the yoke presentation. A wearer was unavailable, so “no physical step backward needed” remains unconfirmed.

## Aircraft exterior and animation

The imported GLB contains 123 nodes, 51 meshes, 16 materials, 23 cockpit renderers, 28 exterior renderers, and no source animation clips or skins. The full exterior is now explicit and reads substantially better in the external views. Six independently identifiable aerodynamic meshes are driven from the active backend state: left/right ailerons, left/right flaps, and left/right elevators. The pilot yoke is also driven. Static and control-sweep views visibly differ.

The source has no safe semantic/pivot mapping for a separate rudder, pedals, throttle, mixture, trim, wheels/steering, or propeller. Those items remain static rather than being faked. Redistribution/product rights for this placeholder model must be resolved before extraction.

## Environment result

### Ground and terrain

- Baked coverage is a high-detail 4 km near zone, lower-detail 12 km mid zone, and one fixed 24 km USGS-derived far mesh.
- `ProductionKbduMacroAlbedo.png` is one unique 1024×1024 project-authored derivative of pinned USGS elevation/slope and OSM land-cover/aeroway/road data. It does not repeat a photographic tile.
- Near microdetail uses three documented Poly Haven CC0 1K maps with mips, trilinear filtering, anisotropy 4, world-space sampling, distance fade, and Android ASTC 6×6 import settings.
- No Google Maps, Earth, or Street View content is used.

The repeating-photo defect is gone in the inspected images, but the replacement is muted, synthetic, and blocky. Pattern-altitude land-cover lacks the fidelity expected of a convincing product. This is a technical anti-repetition success and a visual-quality limitation.

### Mountains

The far source is one immutable mesh, `Terrain_Far24km_Immutable_USGS_Mesh`, with 11,240 vertices. A 25-heading sweep from -180° through +180° kept the same mesh instance, vertex count, matrix hash, renderer state, and enabled-renderer set. It has no `LODGroup`, distance culler, crossfade, billboard, camera-relative scale, or runtime rebuild.

The static identity gate therefore passes. The inspected silhouette is smoother than the legacy ridge stack but remains coarse/faceted. Stereo perceptual stability still requires a worn head sweep and is not claimed.

### Water

Production retains only OSM Boulder Reservoir way `35597714`; low-value minor hydrography is omitted. The reservoir is a triangulated shoreline mesh with a smoothed shore-bank interface, opaque ZWrite material, no transparent overlap, no screen-space reflection/refraction, no animated UVs, and `0.180 m` measured minimum terrain separation.

This removes the legacy coplanar/zebra-risk path and sharp cube/ribbon presentation. The final inspection still reads the reservoir as flat blue geometry with limited material depth. Water is structurally stable but not yet visually natural.

### Authoritative runway

The FAA 08/26 endpoints, dimensions, heading, and USGS grade drive one tessellated pavement/collision mesh, one offline terrain blend, one combined depth-biased marking mesh, and the aircraft spawn/reset.

| Measurement | Final |
| --- | ---: |
| Maximum runway-to-terrain gap | `0.004676 m` |
| Maximum marking-to-runway gap | `0.001000 m` |
| Collision-surface disagreement | `0.000000 m` |
| Duplicate production runway renderers | none |

The former floating threshold slabs were replaced by correctly bounded stripe geometry, and the malformed vertical road batch was excluded. Close inspection no longer shows a visible floating gap, although runway materials and surrounding airport buildings remain visually simple.

## Physics authority and native JSBSim gate

Production remains serialized to `UnityPrototypeFlightBackend`; this is the only active pose authority. Native JSBSim improved from 1/10 baseline scenarios to 10/11 final scenarios and 9/10 critical scenarios without relaxing the gate:

| Native scenario | Critical | Result |
| --- | --- | --- |
| Idle / brake hold | yes | PASS |
| Taxi acceleration / braking | yes | PASS |
| Takeoff roll | yes | PASS |
| Rotation | yes | PASS |
| Vy climb | yes | PASS |
| Trimmed level flight | yes | PASS |
| Shallow left turn | yes | PASS |
| Shallow right turn | yes | PASS |
| Slow flight / stall warning / recovery | no | PASS |
| Approach | yes | PASS |
| Go-around | yes | **FAIL** — second-half recovery `67.93 ft`, required `75 ft` |

Final native evidence had no nonfinite state, ran 27,360 steps at 120 Hz, allocated zero managed bytes in steady state, averaged `0.0232 ms` per Windows step, and had `0.0813 ms` worst-scenario p95. These are Windows results, not Quest CPU timing or a handling-quality endorsement. See `PRODUCTION_VERTICAL_SLICE_V2_JSBSIM_GATE_2026-07-12.md` for the full state envelopes and binary hashes.

## Visual QA scorecard

Scores are blunt lead-review judgments, not machine metrics.

| Area | Legacy | Production | Final review |
| --- | ---: | ---: | --- |
| Default viewpoint | 5/10 | 6/10 | Geometry is plausible; wearer confirmation missing. |
| Cockpit presentation | 5/10 | 5/10 | Cleaner composition, but still dark/flat. |
| Mountains | 3/10 | 5/10 | Identity is immutable; silhouette remains coarse. |
| Ground / macro appearance | 2/10 | 4/10 | No repeated photo grid; synthetic/blocky appearance remains. |
| Water | 2/10 | 3/10 | Stable geometry/material; still visually flat. |
| Runway / ground contact | 3/10 | 6/10 | Measured alignment and bounded markings are materially better. |
| Aircraft exterior | 4/10 | 7/10 | Full exterior is explicit and readable. |
| Control animation | 1/10 | 6/10 | Six surfaces and yoke work; other controls remain unsupported. |
| Physical credibility | 1/10 | 3/10 | One authority and much stronger native evidence, but production still uses the prototype backend. |
| Overall visual quality | **3/10** | **4–5/10** | Clearly better, still not a convincing product slice. |

All 16 required production images were inspected at 1280×720. Flight-phase images are deliberately labeled presentation poses; they are not physics evidence.

## Rendering and Quest performance

| Metric | Legacy evidence | Production evidence | Target |
| --- | ---: | ---: | ---: |
| Average application frame time | `13.88 ms` worn quick sample | **NOT RUN** | `< 13.89 ms` with headroom |
| p95 | `15.64 ms` | **NOT RUN** | `<= 13.89 ms` |
| Frames over 13.89 ms | `53.3%` | **NOT RUN** | `<= 5%` |
| Max estimated draws | about `114` | `87` Editor estimate | `<= 180` |
| Max estimated visible triangles | about `376,911` | `346,876` Editor estimate | `<= 700,000` |
| Scene materials | not recorded in that sample | `28` | `<= 40` |

The production APK built and installed successfully: 172,540,498 bytes, SHA-256 `4175de085e61e4b3bd41e882405a0862a5821cad69c4af9881c9894da4969e47`. Unity warned that both Android native JSBSim libraries are not 16 KB aligned; that is a future Android 15 compatibility blocker even though native JSBSim is not selected in production.

The exact integrated `main` source was rebuilt before push: 172,540,493 bytes, SHA-256 `89f8c8ddfc986e8092fe458087161d242e4547d3cbe0de284879ea6539c347a5`. The byte/hash difference is Unity build nondeterminism; source and validation contracts are unchanged.

### Why the 90+60-second Quest profile is not reported

Two unattended attempts were preserved. The first found the headset asleep and never acquired an app PID. The second safely woke the device, but Horizon OS intercepted launch with `RequiresControllersLaunchInterceptor` and focused `LaunchCheckControllerRequiredDialogActivity`; Guardian simultaneously reported room-position setup. The modal requires physically awake Touch controllers and a user selection. Scriptable-testing overrides require the logged-in account's Store PIN, which was unavailable and was not guessed. The app process never started, so there are no Unity/app frame metrics and no app crash to classify.

The original proximity automation and stay-awake state were restored (`prox_far`, `automation_enable`, stay-on `0`), the app/dialog was stopped, and the headset returned asleep. Human acceptance is `NOT_RUN_NO_USER_AVAILABLE`, not PASS or FAIL.

## Tests and builds executed

| Gate | Result |
| --- | --- |
| Legacy Visual QA | 17/17 machine PASS; product FAIL by prior headset observation and inspected imagery |
| Legacy PlayMode baseline | 66/66 PASS |
| Final standard Editor scenario suite | PASS |
| Production environment EditMode | 5/5 PASS |
| Final integrated PlayMode | 88/88 PASS |
| Production Visual QA | 16/16 image-integrity/machine PASS; all 16 manually inspected; product quality remains 4–5/10 |
| Far-terrain camera sweep | 25/25 headings immutable |
| Native JSBSim V2 | 10/11 total, 9/10 critical; promotion FAIL |
| Production Android build | PASS |
| Standard/legacy Android build | PASS |
| Quest install | PASS |
| Quest 90 s warmup + 60 s profile | NOT RUN — Horizon OS requires user/controller/Store-PIN interaction before app process launch |
| Human headset acceptance | NOT RUN — no wearer available |

## Artifact index

Artifact root: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_vertical_slice_v2_20260712_020951`

- exact-main PlayMode rerun: `main_validation_playmode_restored\unity_PlayMode_test_results.xml`
- exact-main production Visual QA: `main_validation_visual_qa\`
- exact-main production APK manifest/build log: `main_validation_build\`
- legacy contact sheet: `baseline_visual_qa\visual_qa_contact_sheet.png`
- final 16-view contact sheet: `production_visual_qa_acceptance_final\visual_qa_contact_sheet.png`
- side-by-side sheet: `production_visual_qa_acceptance_final\visual_qa_before_after.png`
- full-resolution views: `production_visual_qa_acceptance_final\screenshots\`
- machine report: `production_visual_qa_acceptance_final\visual_qa_report.json`
- mountain sweep: `production_visual_qa_acceptance_final\production_mountain_camera_sweep.json`
- integrated PlayMode results: `playmode_after_player_guards\unity_PlayMode_test_results.xml`
- JSBSim gate: `workstream_c_jsbsim\production_v2_gate_try2\`
- APK manifest/build log: `production_quest_build_final_rerun3\`
- blocked Quest attempts: `quest_unattended_90s_60s_final\` and `quest_unattended_90s_60s_awake\`
- controller-interceptor framebuffer: `quest_controller_launch_interceptor.png`

Raw screenshots, recordings, logs, APKs, and build directories remain outside Git.

## Limitations and exact next milestone

This witness does not claim photorealism, training suitability, final C172 fidelity, JSBSim promotion, broad device compatibility, or 72 Hz Quest performance. Missing evidence is not treated as success.

The exact next milestone is **Clean Product Extraction + Worn Quest Reality Gate**:

1. create a minimal product repository from the attached extraction manifest, keeping this repository as R&D;
2. replace the muted synthetic macro presentation, flat reservoir material, coarse far-terrain appearance, and dark cockpit lighting with licensed Quest-budgeted production art while preserving the proven immutable/alignment contracts;
3. keep Unity prototype as the sole backend until the unchanged JSBSim go-around gate passes;
4. with a user and both controllers awake, complete three cold launches, the 16-view/head-sweep visual review, and a 90-second warmup plus 60-second measurement meeting p95 `<= 13.89 ms` and `<= 5%` over-budget frames.

Only that worn gate can justify continuing the clean standalone product repository. If the extracted slice remains visually below 6/10 or cannot hold 72 Hz after one evidence-driven optimization pass, the next decision is PCVR rather than another legacy-repo repair cycle.
