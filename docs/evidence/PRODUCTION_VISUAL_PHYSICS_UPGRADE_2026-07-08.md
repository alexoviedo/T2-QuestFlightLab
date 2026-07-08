# Production Visual + Physics Upgrade Witness - 2026-07-08

## Scope

Milestone: Production Visual + Physics Upgrade v1.

This witness records an environment-focused production upgrade. After Alex said the previous cockpit/aircraft looked good enough for now, the imported C172 placeholder was retained and the generated replacement asset attempt was not committed.

## Repo / Build

- Validation base commit: `47653f89a093f215acabda7b23e4c0180b3d03c3`
- Branch: `main`
- Unity: `6000.3.8f1`
- APK: `QuestFlightLab/Builds/Android/QuestFlightLab-v0.1-dev.apk`
- APK SHA256: `2639B0CC602849E034B692949DEF23C8CC00D485E168440EF32ED40142640839`
- Artifact root: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_20260707_220924`

## Tools

- Unity batchmode visual QA, scenario runner, PlayMode tests, and Android build.
- Blender 5.1.2 was installed/evaluated, but no new Blender-generated aircraft asset was committed after the cockpit/aircraft direction was reset to the previous imported placeholder.
- JSBSim Python package probe output was reused as an offline reference oracle.
- OpenVSP CLI was not available in this chunk.

## Visual / Environment Result

- Current recommended mode: `visual_fidelity_demo`
- Aircraft/cockpit: previous imported C172 placeholder retained from `Resources/QuestFlightLab/ImportedAssets/Cessna172KogThorns/cessna172`
- Environment: expanded KBDU-inspired local world
  - 8.8 km x 7.8 km visual footprint
  - 81 mesh terrain chunks
  - local roads, field strips, perimeter fence cues, ramp parking stripes, industrial buildings, reservoir/drainage hints
  - low-cost Front Range-inspired ridge impostors
  - old blocky foothill placeholders hidden in playtest/HUD mode
- Render quality: visual QA/runtime profile reports MSAA 4, anisotropic filtering forced on, LOD bias 1.55, shadow distance 95 m
- Splat status: real Gaussian splats remain fallback/diagnostic only; editor visual QA reports an experimental proxy/fallback, not Quest XR splat proof

## Visual QA

- Final visual QA artifacts: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_20260707_220924\after_visual_qa_env_focus_v3`
- Contact sheet: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_20260707_220924\after_visual_qa_env_focus_v3\visual_qa_contact_sheet.png`
- Summary: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_20260707_220924\after_visual_qa_env_focus_v3\visual_qa_summary.md`
- Result: 12/12 visual shots passed
- World-builder status: `profile=visual_fidelity_demo_medium size=8800x7800m chunks=81 lodGroups=63`
- Demo-pilot motion: pass, 534.3 m
- Viewpoint persistence probe: pass

## Physics / JSBSim

- JSBSim comparator artifacts: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_20260707_220924\jsbsim_unity_comparison_after`
- Classification: `reference_oracle_only`
- Current open-loop comparison:
  - Unity takeoff-roll speed is higher than current JSBSim open-loop reference.
  - Unity rotation/climb altitude gain is higher than current JSBSim open-loop reference.
  - Unity shallow-turn bank response is much lower than current JSBSim open-loop reference.
  - Approach speed is the closest trend match.
- Unity runtime remains prototype physics, not JSBSim-backed runtime.

## Validation

- Visual QA: pass
- Editor scenarios: 33/33 pass
  - `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_20260707_220924\after_editor_scenarios`
- PlayMode tests: 24/24 pass
  - `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_20260707_220924\after_playmode_tests\unity_PlayMode_test_results.xml`
- Quest APK build: pass
  - build evidence: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\production_visual_physics_20260707_220924\after_quest_build`
- Optional Quest runtime smoke: not run in this chunk

## Limitations

- Editor visual QA is not real Quest headset comfort, stereo, or performance proof.
- Environment is KBDU-inspired only and not navigation-accurate.
- No production photorealism is claimed.
- No final C172 fidelity is claimed.
- No FAA/BATD/AATD/training suitability is claimed.
- No broad Quest compatibility is claimed.
- Real Quest XR Gaussian splat stereo/world-lock remains unresolved.
