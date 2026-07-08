# KBDU Environment, Rendering, and Physics Upgrade Witness - 2026-07-08

## Scope

Milestone: Production Environment + Physics v2.

This witness records an environment-focused upgrade. The current imported C172 placeholder cockpit/aircraft was intentionally preserved so this chunk could focus on KBDU-inspired terrain, airport detail, render quality, LOD/budget evidence, and JSBSim-informed tuning.

## Repo / Build

- Validation base commit: `7012d1eb0fc32d57e1af871f4bba694ac5545578`
- Branch: `main`
- Unity: `6000.3.8f1`
- APK: `QuestFlightLab/Builds/Android/QuestFlightLab-v0.1-dev.apk`
- APK SHA256: `3863F6CC317DB13622BBEDDA35B49B5B9434AACEBF796D0EEDE4E2D3C7FC38E9`
- Artifact root: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\kbd_environment_physics_20260707_225453`

## Data / Asset Sources

- OpenStreetMap reference extract through Overpass API:
  - endpoint: `https://overpass-api.de/api/interpreter`
  - attribution/license page: `https://www.openstreetmap.org/copyright`
  - local artifact only: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\kbd_environment_physics_20260707_225453\osm_reference`
  - committed raw data: none
- Project-owned Unity procedural geometry/material generation:
  - `KbduInspiredWorldBuilder`
  - `AirportRuntimeEnhancer`
  - `QuestRenderQualityConfigurator`
- No paid assets, Google-derived imagery, raw downloaded terrain, or unclear-license assets were committed.

## Environment Result

- Recommended mode: `visual_fidelity_demo`
- Aircraft/cockpit: existing imported C172 placeholder retained from `Resources/QuestFlightLab/ImportedAssets/Cessna172KogThorns/cessna172`
- Environment footprint: 11.8 km x 11.8 km KBDU-inspired visual area
- Terrain: 121 mesh chunks with near/mid/far detail rings
- Airport/world additions:
  - denser taxiway/taxi-lane and connector network
  - ramp/hangar detail and additional industrial buildings
  - runway cracks, expansion joints, shoulder wear, apron seams, parking/T markings
  - field parcels/furrows, local roads, reservoir/drainage hints, tree-line cues
  - longer Front Range-inspired far ridge impostors and snow-cap hints

## Render Quality / LOD

- Visual QA render-quality evidence: MSAA 4, anisotropic filtering forced on, LOD bias 1.65, shadow distance 105 m, far clip 9000 m, mip limit 0
- Android runtime configurator sets a conservative Quest target frame rate of 72 and an eye texture resolution scale of 1.08.
- World-builder budget evidence: `profile=visual_fidelity_demo_medium size=11800x11800m chunks=121 lodGroups=174 renderers=404 meshes=404 tris~13704 materials=14 textures=13 draw=7200m`
- This is budget instrumentation and build/editor evidence, not final Quest performance proof.

## Visual QA

- Baseline visual QA: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\kbd_environment_physics_20260707_225453\baseline_visual_qa`
- Final visual QA: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\kbd_environment_physics_20260707_225453\after_visual_qa_final`
- Final contact sheet: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\kbd_environment_physics_20260707_225453\after_visual_qa_final\visual_qa_contact_sheet.png`
- Result: 14/14 visual shots passed
- New/expanded shots include ramp/hangar detail and final-approach distance cues.
- Demo-pilot motion: pass, 534.3 m
- Viewpoint persistence probe: pass
- Splat status: diagnostic/fallback only; editor visual QA proxy is not Quest XR splat proof.

## Physics / JSBSim

- JSBSim reference: Python package `jsbsim 1.3.1`, aircraft `c172x`, reset `reset00`
- Baseline comparison: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\kbd_environment_physics_20260707_225453\baseline_jsbsim_comparison`
- Final comparison: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\kbd_environment_physics_20260707_225453\after_jsbsim_comparison`
- Classification: `reference_oracle_only`

| Phase | Baseline Delta | v2 Delta | Direction |
| --- | ---: | ---: | --- |
| Takeoff roll speed | +23.4 kt | +22.2 kt | slightly closer |
| Rotation/climb altitude | +91.6 ft | +77.2 ft | closer |
| Shallow turn bank | -48.2 deg | -45.2 deg | slightly closer |
| Approach speed | +8.7 kt | +7.6 kt | closer |
| Go-around speed | +36.4 kt | +35.1 kt | slightly closer |

Unity runtime physics remains a prototype approximation and is not JSBSim-backed.

## Validation

- Visual QA: pass, 14/14 shots
- Editor scenarios: pass, 33/33
  - `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\kbd_environment_physics_20260707_225453\after_editor_scenarios_third`
- PlayMode tests: pass, 24/24
  - `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\kbd_environment_physics_20260707_225453\after_playmode_tests\unity_PlayMode_test_results.xml`
- Quest APK build: pass
  - `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\kbd_environment_physics_20260707_225453\after_quest_build`
- Optional Quest runtime smoke: not run; no ADB Quest device was attached.

## Limitations

- Editor visual QA is not real Quest headset comfort, stereo, or performance proof.
- Environment is KBDU-inspired only and not navigation-accurate.
- No production photorealism is claimed.
- No final C172 fidelity is claimed.
- No FAA/BATD/AATD/training suitability is claimed.
- No broad Quest compatibility is claimed.
- Real Quest XR Gaussian splat stereo/world-lock remains unresolved.
