# Evidence Plan

## App Evidence

The app writes one JSON session per run with:

- Platform, Unity version, device model, app version.
- Gamepad device name, layout, manufacturer, product, interface.
- Axes/buttons observed.
- Sample rate and last input timestamps.
- Scenario markers, resets, warnings, and errors.

## Host Evidence

Capture Quest logs while testing:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_adb_logcat.ps1
```

Archive the app JSON and logcat output in the setup artifacts directory for the test run.

## Autonomous Simulator Evidence

For simulator-core work that should not require wearing the headset, use the deterministic editor scenario runner:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_editor_scenario_tests.ps1
```

The runner writes:

- `scenario_results.json`
- `scenario_results.csv`
- `flight_core_summary.md`
- `flight_pattern_summary.md`
- `flight_approach_summary.md`
- `debrief_report.json`
- `debrief_report.md`
- `approach_debrief_report.json`
- `approach_debrief_report.md`
- `timeline.json`
- `timeline.csv`
- `unity_editor_scenario_tests.log`

For v0.3 the scenario evidence also includes instrument/cockpit verification, training/checklist verification, stall warning count/onset, initial/final airspeed, altitude delta, heading change, and reference-speed error metrics.

For v0.4 the scenario evidence also includes traffic-pattern phase scoring, debrief warnings, airport/pattern reference verification, lesson prompt verification, and instrument value-refresh verification.

For v0.5 the scenario evidence also includes stabilized approach criteria, go-around decision flags, speed/altitude/descent/glide-path/centerline deviations, approach-status instrument verification, replay timeline samples, replay markers, and approach debrief recommendations.

For v0.6 Gaussian splat feasibility, use:

```powershell
python tools\generate_tiny_splat_samples.py --output-dir <artifact-root>\samples --counts 5000 50000 100000
powershell -ExecutionPolicy Bypass -File .\scripts\run_splat_spike.ps1 -ArtifactDir <artifact-root> -SampleDir <artifact-root>\samples
```

This writes `splat_editor_results.json`, `splat_editor_results.csv`, `splat_spike_summary.md`, and `unity_splat_spike.log`. The spike evidence records scenery mode, renderer detection, fallback status, synthetic sample sizes, estimated splat memory budgets, and a conservative viability classification.

For v0.6b real Gaussian renderer feasibility, use:

```powershell
python tools\generate_tiny_splat_samples.py --output-dir <artifact-root>\real_samples --counts 5000 50000 100000 --schema unity-3dgs-binary
powershell -ExecutionPolicy Bypass -File .\scripts\run_real_splat_renderer_spike.ps1 -ArtifactDir <artifact-root>\real_renderer_editor -SampleDir <artifact-root>\real_samples -ForceD3D12
```

This writes real renderer JSON/CSV/Markdown evidence, screenshots, sample hashes, package resolution logs, scenario regression evidence, PlayMode XML, Android build logs, and APK hash evidence. Treat `android_build_only` as an explicit non-Quest-runtime classification.

For v0.6c Quest runtime Gaussian splat gating, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\prepare_splat_runtime_samples.ps1 -ArtifactDir <artifact-root>\runtime_samples -Counts 5000,50000,100000
powershell -ExecutionPolicy Bypass -File .\scripts\run_quest_splat_runtime_mode.ps1 -Mode mesh -OutputDir <artifact-root>\quest_mesh_fallback_runtime
powershell -ExecutionPolicy Bypass -File .\scripts\run_quest_splat_runtime_mode.ps1 -Mode splat_5k -OutputDir <artifact-root>\quest_splat_5k
powershell -ExecutionPolicy Bypass -File .\scripts\run_quest_splat_runtime_mode.ps1 -Mode splat_50k -OutputDir <artifact-root>\quest_splat_50k
powershell -ExecutionPolicy Bypass -File .\scripts\run_quest_splat_runtime_mode.ps1 -Mode splat_100k -OutputDir <artifact-root>\quest_splat_100k
```

This pulls runtime JSON evidence from `Application.persistentDataPath\QuestFlightLab\scenery_runtime`, captures logcat/activity state, and attempts ADB screenshots. Treat the v0.6c `quest_runtime_viable_small_scenic_patch` result as synthetic-only unless a real owned/licensed visual asset is also tested.

For v0.7 playable scenic splat demo mode, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\prepare_playable_scenic_splat.ps1 -ArtifactDir <artifact-root>\scenic_asset_import
powershell -ExecutionPolicy Bypass -File .\scripts\run_quest_splat_runtime_mode.ps1 -Mode mesh -OutputDir <artifact-root>\quest_mesh_playable
powershell -ExecutionPolicy Bypass -File .\scripts\run_quest_splat_runtime_mode.ps1 -Mode scenic_splat_low -OutputDir <artifact-root>\quest_scenic_splat_low
powershell -ExecutionPolicy Bypass -File .\scripts\run_quest_splat_runtime_mode.ps1 -Mode scenic_splat_medium -OutputDir <artifact-root>\quest_scenic_splat_medium
powershell -ExecutionPolicy Bypass -File .\scripts\run_quest_splat_runtime_mode.ps1 -Mode scenic_splat_high -OutputDir <artifact-root>\quest_scenic_splat_high
```

This generates raw procedural PLYs in the artifact root, imports small Unity runtime assets, captures Quest logcat/screenshots/evidence JSON, and records whether the scenic modes improve the playable demo without compromising the mesh fallback. The v0.7 asset is procedural and project-owned, not a real airport capture.

For v0.8 playable visual recovery, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_editor_scenario_tests.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\run_unity_tests.ps1 -TestPlatform EditMode
powershell -ExecutionPolicy Bypass -File .\scripts\run_unity_tests.ps1 -TestPlatform PlayMode
powershell -ExecutionPolicy Bypass -File .\scripts\build_quest.ps1
.\scripts\launch_quest_playtest.ps1 -Mode playable_demo -CaptureLogcat -DurationSeconds 85
.\scripts\launch_quest_playtest.ps1 -Mode scenic_splat_medium -DemoMode short_playtest -CaptureLogcat
```

This captures the recommended mesh/procedural visual baseline, demo-pilot motion evidence, head-pose diagnostics, and the Quest XR splat safety gate. Treat `scenic_splat_medium` as `blocked_xr_stereo_composite` unless `-SplatDiagnostic` is used and new headset captures prove stereo/world-locked output.

For autonomous visual QA without Quest/ESP32/headset access, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_visual_qa.ps1
```

This executes `QuestFlightLab.Editor.VisualQaBatchRunner.RunVisualQa`, captures deterministic editor screenshots, creates `visual_qa_contact_sheet.png`, writes `visual_qa_report.json`, `visual_qa_report.csv`, `visual_qa_summary.md`, and runs `tools\visual_qa_analyze.py` to produce `visual_qa_analysis.json` and `visual_qa_analysis.md`. Treat this as fail-fast visual evidence only; it does not prove Quest comfort, Quest stereo rendering, USB2BLE hardware input, production visual quality, or Quest Gaussian splat viability.

For v0.9 production-fidelity direction work, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_visual_qa.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\run_editor_scenario_tests.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\run_unity_tests.ps1 -TestPlatform PlayMode
python .\tools\generate_c172_style_assets.py --dry-run --output <artifact-root>\asset_pipeline\c172_style_baseline.glb
python .\tools\jsbsim_probe\run_c172_probe.py --output-dir <artifact-root>\jsbsim_probe\run
powershell -ExecutionPolicy Bypass -File .\scripts\build_quest.ps1
```

This captures visual QA/contact sheets, Unity simulator regressions, PlayMode tests, a dry-run/generated aircraft asset manifest, JSBSim C172-like reference telemetry, build logs, and APK hash evidence. Treat JSBSim as an offline reference oracle until Android/Quest native integration is proven. Treat Blender/OpenVSP/Cesium outputs as research or import candidates until visual QA and Android build evidence pass.

For v1 production visual/physics upgrade work, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_visual_qa.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\run_editor_scenario_tests.ps1
python .\tools\jsbsim_probe\compare_jsbsim_unity.py --jsbsim-json <artifact-root>\jsbsim_probe\v1_reference\jsbsim_c172_probe.json --jsbsim-csv <artifact-root>\jsbsim_probe\v1_reference\jsbsim_c172_probe.csv --unity-scenario-csv <artifact-root>\after_editor_scenarios\scenario_results.csv --output-dir <artifact-root>\jsbsim_unity_comparison_after
powershell -ExecutionPolicy Bypass -File .\scripts\run_unity_tests.ps1 -TestPlatform PlayMode
powershell -ExecutionPolicy Bypass -File .\scripts\build_quest.ps1
```

This captures the expanded environment visual QA/contact sheet, world-builder budget, render-quality profile, editor scenario evidence, JSBSim/Unity comparison report, PlayMode XML, Android build logs, and APK hash. Treat the environment as KBDU-inspired only and the JSBSim comparison as reference-oracle evidence only.

For v2 KBDU environment/rendering/physics work, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_visual_qa.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\run_editor_scenario_tests.ps1
python .\tools\jsbsim_probe\run_c172_probe.py --output-dir <artifact-root>\jsbsim_reference
python .\tools\jsbsim_probe\compare_jsbsim_unity.py --jsbsim-json <artifact-root>\jsbsim_reference\jsbsim_c172_probe.json --jsbsim-csv <artifact-root>\jsbsim_reference\jsbsim_c172_probe.csv --unity-scenario-csv <artifact-root>\after_editor_scenarios\scenario_results.csv --output-dir <artifact-root>\after_jsbsim_comparison
powershell -ExecutionPolicy Bypass -File .\scripts\run_unity_tests.ps1 -TestPlatform PlayMode
powershell -ExecutionPolicy Bypass -File .\scripts\build_quest.ps1
```

The v2 witness should include the OSM/Overpass reference artifact path, visual QA before/after contact sheets, world performance budget, render-quality profile, JSBSim comparison before/after table, scenario/PlayMode results, and APK hash. Treat OpenStreetMap-derived context as KBDU-inspired reference unless intentionally committed and attributed under its license obligations.

The supplemental PlayMode probe is:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_unity_tests.ps1 -TestPlatform PlayMode
```

This produces a Unity Test Runner XML file when the local Unity Test Framework command-line path is healthy. The editor scenario runner is the primary v0.2 flight-core evidence path.

## 2026-06-12 Bring-Up Artifact Root

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\bringup_20260612_133231
```

This folder contains repo state, Unity logs, ADB state, APK install logs, manifest dumps, startup logcat, and pulled app evidence JSON where available.

## 2026-06-12 Runtime Smoke And Input Artifact Root

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\runtime_smoke_20260612_162235
```

This folder contains the Quest runtime smoke screenshot/logs, pulled app evidence JSON, USB2BLE serial transcripts, Quest logcat during input replay, and reduced JSON/CSV summaries proving the Unity Input System observed the Xbox-style gamepad telemetry.

## 2026-06-12 Flight Core Artifact Root

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\flight_core_20260612_173102
```

This folder contains deterministic Unity editor scenario evidence, PlayMode Test Runner XML, Unity logs, Android build logs, the scenario CSV/JSON, and the v0.2 flight-core summary.

## 2026-06-12 Flight Fidelity Artifact Root

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\flight_fidelity_20260612_185938
```

This folder contains the v0.3 implementation review, Unity scenario runner logs, scenario JSON/CSV/summary, PlayMode XML, Android build logs, and APK hash evidence.

## 2026-06-12 Traffic Pattern Artifact Root

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\flight_pattern_20260612_194040
```

This folder contains the v0.4 implementation review, Unity scenario runner logs, scenario JSON/CSV/summary, traffic-pattern debrief JSON/Markdown, PlayMode XML, Android build logs, and APK hash evidence.

## 2026-06-12 Stabilized Approach Artifact Root

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\flight_approach_20260612_200229
```

This folder contains the v0.5 implementation review, Unity scenario runner logs, scenario JSON/CSV/summary, approach debrief JSON/Markdown, replay timeline JSON/CSV, PlayMode XML, Android build logs, and APK hash evidence when validation completes.

## 2026-06-12 Gaussian Splat Spike Artifact Root

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\splat_spike_20260612_213624
```

This folder contains the v0.6 splat spike logs, synthetic PLY samples generated outside the repo, editor spike JSON/CSV/Markdown, v0.5 scenario regression evidence, PlayMode XML, Android build logs, and APK hash evidence. The result defers true Quest Gaussian splat viability until a real renderer package and headset runtime frame timing are tested.

## 2026-06-12 Real Gaussian Renderer Artifact Root

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\splat_renderer_20260612_220658
```

This folder contains the v0.6b renderer selection note, package probe logs, renderer-compatible binary PLY samples and hashes, real editor renderer screenshots/results, mesh fallback scenario regression evidence, PlayMode XML, Android build logs, and APK hash evidence. The result is `android_build_only`: editor renderer smoke and Android build pass, Quest runtime splat rendering not yet proven.

## 2026-06-12 Quest Runtime Gaussian Splat Artifact Root

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\splat_runtime_20260612_232554
```

This folder contains v0.6c Quest runtime mesh fallback evidence, synthetic 5k/50k/100k splat runtime JSON/logcat/screenshots, the synthetic sample manifest and hashes, and the real visual asset source investigation note. The result is synthetic-only `quest_runtime_viable_small_scenic_patch` on one Quest 3 test; full-airport splats and real capture pipelines remain unproven.

## 2026-06-13 Playable Scenic Splat Artifact Root

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\playable_splat_20260613_005918
```

This folder contains the v0.7 procedural scenic splat generation manifest, Unity import logs, validation logs, APK hash evidence, and Quest runtime mesh/scenic-mode evidence when the runtime smoke completes.

## 2026-07-03 Visual Recovery Artifact Root

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\visual_recovery_20260703_030335
```

This folder contains the v0.8 before/after headset screenshots, logcat, head-pose diagnostics, demo-pilot evidence, splat-gate evidence, APK hash, and splat XR diagnosis. The result is a playable mesh/procedural visual baseline with real Quest splats gated as `blocked_xr_stereo_composite` in normal playtest mode.

## 2026-07-07 KBDU Environment/Physics v2 Artifact Root

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\kbd_environment_physics_20260707_225453
```

This folder contains repo/tooling state, baseline and after visual QA, OSM reference extracts kept outside the repo, render-quality notes, editor scenario results, PlayMode XML, JSBSim comparison before/after reports, Android build logs, and APK hash evidence.

## 2026-07-08 Production Visual/Physics v2.1 Artifact Root

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_v2_20260707_233843
```

This folder contains repo/tooling state, baseline audit notes, baseline/final visual QA, corrected pilot-eye evidence, render-quality report, editor scenario results, PlayMode XML, matched-control JSBSim comparison before/after reports, copied Android build logs, APK hash evidence, and optional Quest-smoke notes. The result is editor/build evidence only; no headset runtime performance, comfort, or splat stereo proof was captured.

## 2026-07-08 Playable Simulator Quality Gate Artifact Root

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\quality_gate_20260708_005435
```

This folder contains repo/tooling state, an honest baseline quality score, minimum demo target, baseline/final visual QA, before/after contact sheet, render-quality report, JSBSim Editor bridge proof, matched-control JSBSim comparison, editor scenario results, PlayMode XML, copied Android build logs, APK hash evidence, and optional Quest-smoke notes. The result is still editor/build evidence only; no headset runtime frame timing or shimmer proof was captured.
