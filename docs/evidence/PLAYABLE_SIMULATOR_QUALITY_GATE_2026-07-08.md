# Playable Simulator Quality Gate Witness - 2026-07-08

## Scope

Milestone: Playable Simulator Quality Gate v1 - Make It Visibly Convincing and Physically Credible or Prove the Current Approach Is Wrong.

This pass preserves the current imported C172 placeholder aircraft/cockpit and focuses on the environment, render quality, default pilot view verification, and JSBSim backend direction.

## Repo / Build

- Base commit: `fe9e435cea9acc9447cc424fc9b8e1b5c25dc7cb`
- Containing commit SHA: reported by `git log` / final response after commit creation
- Branch: `main`
- Unity: `6000.3.8f1`
- APK: `QuestFlightLab/Builds/Android/QuestFlightLab-v0.1-dev.apk`
- APK SHA256: `E4050ADAB6F93736CF5E5D81CCE9C0D28039F514042750B4C50579C3E67EAA59`
- Artifact root: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\quality_gate_20260708_005435`

## Honest Quality Scores

| Category | Baseline | Final |
| --- | ---: | ---: |
| Cockpit/pilot viewpoint | 7 | 7 |
| Cockpit visual credibility | 6 | 6 |
| Runway/airport credibility | 4 | 6 |
| Terrain/large-world credibility | 3 | 5 |
| Lighting/material realism | 3 | 6 |
| Render jaggedness/shimmer risk | 5 | 6 |
| Demo-pilot readability | 6 | 6 |
| Aircraft motion credibility | 5 | 5 |
| JSBSim physical match | 3 | 4 |
| Overall headset impression risk | 4 | 6 |

Baseline verdict: stable but not visually convincing.

Final verdict: worth a headset validation pass, but still not production photorealistic and not final physics.

## Visual Improvements

- Added procedural daylight skybox to replace the flat solid-color visual QA sky.
- Increased atmospheric haze and far-clip profile for distance readability.
- Added irregular prairie/field/sage terrain color patches.
- Increased near/mid procedural terrain mesh density.
- Added additional layered Front Range-inspired ridge silhouettes and haze bands.
- Added runway/apron quality-gate surface layer:
  - faded runway paint,
  - runway aiming points,
  - aggregate/rubber/asphalt streaks,
  - gravel shoulders,
  - apron oil stains,
  - ramp markings and cone details.

## Render Quality

Final deterministic visual QA render evidence:

- MSAA: 4
- Anisotropic filtering: ForceEnable
- LOD bias: 1.90
- Shadow distance: 135 m
- Far clip: 12000 m
- Mip limit: 0
- Android visual profile eye texture scale: 1.15

Render-quality report:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\quality_gate_20260708_005435\render_quality_gate_report.md
```

## Environment / LOD

Final visual QA world evidence:

```text
profile=visual_fidelity_demo_medium size=14560x14560m chunks=169 lodGroups=462 renderers=686 meshes=686 tris~34108 materials=18 textures=16 draw=9200m
```

The environment remains KBDU-inspired and not navigation-accurate.

## Default Pilot View

- Pilot eye reference: PASS
- Seat reference: `(0.00, 0.72, 0.00)` local meters
- Default offset: `(0.00, 0.22, 0.00)` local meters
- Resolved eye: `(0.00, 0.94, 0.00)` local meters
- Viewpoint persistence probe: PASS
- Aircraft/cockpit model: existing imported C172 placeholder retained

## Visual QA

- Baseline visual QA: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\quality_gate_20260708_005435\baseline_visual_qa`
- Final visual QA: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\quality_gate_20260708_005435\after_visual_qa_final`
- Before/after contact sheet: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\quality_gate_20260708_005435\visual_qa_before_after_contact_sheet.png`
- Final result: 14/14 shots passed
- Demo-pilot motion: PASS, 534.3 m
- Splat status: diagnostic/fallback only; no Quest XR splat stereo/world-lock proof.

## JSBSim Bridge / Physics

JSBSim Editor bridge result:

- Script: `scripts\run_jsbsim_editor_bridge.ps1`
- Unity runner: `QuestFlightLab.Editor.JSBSimEditorBridgeRunner.RunBridge`
- Python sidecar: `tools\jsbsim_probe\jsbsim_editor_bridge.py`
- Artifact: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\quality_gate_20260708_005435\jsbsim_editor_bridge_pass2`
- Result: PASS
- Classification: `editor_sidecar_bridge_unity_applied`
- JSBSim samples imported: 451
- Unity proxy poses applied: 451
- Max JSBSim airspeed: 101.5 kt
- Max JSBSim AGL: 49.3 ft / 15.0 m
- Ground track: 936.6 m

Matched-control JSBSim/Unity metrics:

| Metric | Baseline | Final |
| --- | ---: | ---: |
| Mean weighted error score | 141.99 | 141.99 |
| Mean airspeed RMSE | 26.7 kt | 26.7 kt |
| Mean altitude-delta RMSE | 183.7 ft | 183.7 ft |
| Mean bank RMSE | 57.0 deg | 57.0 deg |

Unity runtime physics remains approximate and Unity-driven. Because the Editor bridge works, the next credible physics architecture step is an interactive JSBSim-driven Editor backend rather than additional small config-only tuning.

## Validation

- Visual QA: pass, 14/14
- Editor scenarios: pass, 33/33
  - `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\quality_gate_20260708_005435\after_editor_scenarios`
- PlayMode tests: pass, 24/24
  - `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\quality_gate_20260708_005435\after_playmode_tests\unity_PlayMode_test_results.xml`
- Quest APK build: pass
  - `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\quality_gate_20260708_005435\after_quest_build`
- Optional Quest smoke: skipped because `adb devices -l` listed no attached Quest device.

## Limitations

- Editor visual QA is not real Quest headset comfort, shimmer, stereo, or performance proof.
- Environment is KBDU-inspired only and not navigation-accurate.
- No production photorealism is claimed.
- No final C172 fidelity is claimed.
- No FAA-approved training, BATD/AATD qualification, or legal pilot-training credit is claimed.
- No broad Quest compatibility is claimed.
- Real Quest XR Gaussian splat stereo/world-lock remains unresolved.
