# KBDU Visual/Physics v2.1 Witness - 2026-07-08

## Scope

Milestone: Production Visual + Physics v2 - Pilot Eye View, KBDU Terrain/Materials, Render Quality, LOD, and Matched JSBSim Calibration.

This witness records a focused pass that preserves the current imported C172 placeholder aircraft/cockpit and improves the default pilot eye point, KBDU-inspired procedural world, render/LOD budgets, and JSBSim reference evidence.

## Repo / Build

- Working tree base commit before this change: `6f012e7347a8a091c4282eafdfa7417800b64465`
- Evidence-preparation commit SHA before final amend: `019181d0192d5694bddeb967ad2052042c5fd5ca`
- Containing commit SHA: reported by `git log` / final response after commit creation, because embedding a commit's own SHA in the same commit would change the SHA.
- Branch: `main`
- Unity: `6000.3.8f1`
- APK: `QuestFlightLab/Builds/Android/QuestFlightLab-v0.1-dev.apk`
- APK SHA256: `D2F32639E877589B8F81D9A1456FFED5C3C7BB852AFB8C43694FB7648C57DE4E`
- Artifact root: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_v2_20260707_233843`

## Data / Asset Sources

- Existing imported C172 placeholder cockpit/aircraft retained:
  - `QuestFlightLab/Assets/Resources/QuestFlightLab/ImportedAssets/Cessna172KogThorns/cessna172.glb`
  - no replacement/remodeling in this chunk
- Project-owned procedural Unity generation:
  - `KbduInspiredWorldBuilder`
  - `QuestRenderQualityConfigurator`
  - `PilotViewpointConfig`
- Prior OpenStreetMap/Overpass reference remains non-committed artifact context only:
  - endpoint: `https://overpass-api.de/api/interpreter`
  - attribution/license page: `https://www.openstreetmap.org/copyright`
- JSBSim reference:
  - Python package `jsbsim 1.3.1`
  - bundled aircraft `c172x`
- No paid assets, Google-derived imagery, raw downloaded terrain, APKs, screenshots, or large asset archives were committed.

## Default Pilot View

- Default imported C172 seat reference: `(0.00, 0.72, 0.00)` local meters
- Default pilot-eye offset: `(0.00, 0.22, 0.00)` local meters
- Resolved default pilot eye: `(0.00, 0.94, 0.00)` local meters
- Visual QA result: `PASS`
- Final cockpit shot outside-view ratio: `0.527`
- Viewpoint persistence probe: `PASS`

## Environment / Terrain

- Recommended mode: `visual_fidelity_demo`
- Footprint: 14.56 km x 14.56 km KBDU-inspired visual area
- Terrain chunks: 169
- Detail rings: near/mid/far procedural mesh density
- LOD groups: 387 in final visual QA
- Renderers/meshes: 611/611 in final visual QA
- Approximate world triangles: 20,148 in final visual QA budget evidence
- Materials/textures: 15/14 in final visual QA budget evidence
- Draw distance: 9.2 km
- Added/expanded cues: dry grass patches, field parcels, farm tracks, tree-line segments, airport-adjacent roads, reservoir/drainage hints, far buildings, and farther Front Range-style ridge impostors.

## Render Quality / LOD

- Final render quality evidence: MSAA 4, anisotropic filtering forced on, LOD bias 1.75, shadow distance 115 m, far clip 11000 m, mip limit 0
- Android runtime configurator keeps the visual profile conservative and does not add expensive post-processing.
- Render-quality report: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_v2_20260707_233843\render_quality_v2_report.md`

## Visual QA

- Baseline visual QA: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_v2_20260707_233843\baseline_visual_qa`
- Final visual QA: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_v2_20260707_233843\after_visual_qa_final`
- Final contact sheet: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_v2_20260707_233843\after_visual_qa_final\visual_qa_contact_sheet.png`
- Result: 14/14 visual shots passed
- Demo-pilot motion: pass, 534.3 m
- Splat status: diagnostic/fallback only. The editor proxy is not a Quest XR splat proof.

## Physics / JSBSim

- Comparator: `tools/jsbsim_probe/run_matched_jsbsim_comparison.py`
- Classification: `reference_oracle_only`
- Baseline matched comparison: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_v2_20260707_233843\baseline_matched_jsbsim_comparison`
- Final matched comparison: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_v2_20260707_233843\after_matched_jsbsim_comparison`

| Metric | Baseline | v2.1 | Direction |
| --- | ---: | ---: | --- |
| Mean weighted error score | 142.14 | 141.99 | slightly closer |
| Mean airspeed RMSE | 26.9 kt | 26.7 kt | slightly closer |
| Mean altitude-delta RMSE | 184.0 ft | 183.7 ft | slightly closer |
| Mean bank RMSE | 57.0 deg | 57.0 deg | unchanged |

Runtime Unity physics remains approximate and is not JSBSim-backed. More aggressive config-only tuning broke traffic-pattern and stabilized-approach gates, so deeper fidelity should use a JSBSim bridge/runtime feasibility path.

## Validation

- Visual QA: pass, 14/14
- Editor scenarios: pass, 33/33
  - `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_v2_20260707_233843\after_editor_scenarios_final2`
- PlayMode tests: pass
  - `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_v2_20260707_233843\after_playmode_tests\unity_PlayMode_test_results.xml`
- Quest APK build: pass
  - `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_v2_20260707_233843\after_quest_build`
- Optional Quest runtime smoke: skipped because ADB reported no attached Quest device.

## Limitations

- Editor visual QA is not real Quest headset comfort, shimmer, stereo, or performance proof.
- Environment is KBDU-inspired only and not navigation-accurate.
- No production photorealism is claimed.
- No final C172 fidelity is claimed.
- No FAA-approved training, BATD/AATD qualification, or legal pilot-training credit is claimed.
- No broad Quest compatibility is claimed.
- Real Quest XR Gaussian splat stereo/world-lock remains unresolved.
