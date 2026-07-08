# Production Fidelity Direction Witness - 2026-07-08

## Scope

This witness records the v0.9 architecture/pipeline direction for improving visual and physical fidelity. It is not a Quest headset comfort test and does not claim final C172 fidelity, production photorealism, FAA/BATD/AATD suitability, broad Quest compatibility, or final Quest performance.

## Repo / Build

- Branch: `main`
- Commit under test before this slice: `728b349545f1944d6999ab1d77a9245f20de2371`
- Unity: `6000.3.8f1`
- Artifact root: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_direction_20260707_212309`
- Baseline APK SHA256: `261DCE117431CC2FB75FD2D3282A758C77EE3F9ED96B864902A9EC37EF089140`
- Post-change APK SHA256: `A4C3DF692323ADFB1B9790E1AA341A27A14782D6602763D91220C2265BB15EDA`

The final pushed commit for this witness is recorded in the commit history and final operator response because a committed file cannot know its own final SHA without changing that SHA.

## Current Audit

- Visual QA baseline passed with deterministic Unity Editor camera capture.
- Contact sheet: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_direction_20260707_212309\baseline_visual_qa\visual_qa_contact_sheet.png`
- Cockpit asset status: imported C172 placeholder plus runtime-generated C172-style visual cues.
- Mesh scenery status: active and stable.
- Scenic/splat status: editor proxy/fallback only; not Quest XR stereo/world-lock proof.
- Viewpoint persistence: passed visual QA probe.
- Demo pilot motion: passed visual QA probe, 534.3 m measured pose displacement.

## Tooling Inventory

- Python and Git available.
- Unity available through configured build scripts.
- ADB available through Meta Quest Developer Hub path.
- Blender not found on PATH.
- OpenVSP not found on PATH.
- JSBSim command-line executable not found on PATH, but Python package probe succeeded in artifact-local venv.
- Cesium for Unity package not present in the Unity project.

## JSBSim Result

The local JSBSim Python package probe succeeded with bundled `c172x`:

- Output: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_direction_20260707_212309\jsbsim_probe\run_c172x_reset_final`
- Duration: 170 s
- Samples: 341
- Max airspeed: 86.6 kt
- Max AGL: 34.1 ft
- Max absolute bank: 65.7 deg
- Classification: feasible as offline reference oracle, not ready as Quest runtime backend.

## Aircraft / Asset Pipeline Result

- Added `tools/generate_c172_style_assets.py` as a Blender-ready C172-style baseline generation script.
- Because Blender is not installed locally, the immediate validation path is manifest/dry-run until Blender or OpenVSP is installed.
- Added `docs/AIRCRAFT_ASSET_PIPELINE.md` for Blender/OpenVSP-to-Unity workflow.

## Visual / Scenery Direction

- Added `visual_fidelity_demo` as the recommended production-direction demo alias.
- Kept the real Gaussian splat renderer out of default playtest mode.
- Added lightweight procedural runway/airport visual details to the existing safe runtime enhancer: rubber touchdown marks, asphalt patches, expansion joints, apron seams, tie-down markings, cones, and grass variation.
- Added `docs/GEOSPATIAL_SCENERY_PLAN.md` to keep Cesium/3D Tiles/geospatial streaming in a research gate instead of default Quest runtime.

## Validation

- Visual QA: pass
  - `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_direction_20260707_212309\after_visual_qa`
  - contact sheet: `after_visual_qa\visual_qa_contact_sheet.png`
- Editor scenario tests: pass
  - `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_direction_20260707_212309\after_editor_scenarios`
- PlayMode tests: pass
  - `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_direction_20260707_212309\after_playmode_tests\unity_PlayMode_test_results.xml`
- JSBSim probe rerun: pass
  - `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_direction_20260707_212309\jsbsim_probe\final_validation`
- Asset pipeline dry run: pass with Blender unavailable
  - `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_direction_20260707_212309\asset_pipeline\c172_style_baseline.manifest.json`
- Quest APK build: pass
  - logs/hash copied to `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_direction_20260707_212309\after_quest_build`

## Limitations

- No Quest hardware run was attempted in this chunk.
- No Blender/OpenVSP install was completed in this chunk.
- No new production-quality aircraft asset was imported.
- No Android/Quest JSBSim runtime integration was attempted.
- Current splats remain diagnostic/fallback unless future headset evidence proves stereo/world-locked behavior.
