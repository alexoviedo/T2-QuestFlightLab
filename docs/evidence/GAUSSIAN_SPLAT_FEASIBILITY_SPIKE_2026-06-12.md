# Gaussian Splat Feasibility Spike Witness - 2026-06-12

## Summary

Flight Sim Core v0.6 adds an optional scenery-provider abstraction and a bounded Gaussian splat feasibility harness while preserving the mesh/terrain fallback as the default path. The spike did not install or commit a production Gaussian splat renderer. It generated tiny synthetic PLY samples for budget/proxy plumbing, ran the editor spike, reran the v0.5 autonomous simulator suite, reran PlayMode tests, and built the Android APK.

Viability classification: `defer_to_later`.

## Build And Test Context

- Date/time: 2026-06-12 evening Mountain time / 2026-06-13 UTC artifact timestamps.
- Validation start commit: `5b9e215459a9067b03a24bd2f11e6ee55b3a33fa`.
- v0.6 commit: recorded in Git history for this file after commit.
- Unity: `6000.3.8f1`.
- Test mode: Unity batchmode editor spike runner, deterministic editor scenario runner, Unity PlayMode tests, Android APK build.
- Graphics API observed in editor spike: Direct3D11.
- Render pipeline reported by spike: Built-in/Core or project default.
- Artifact root: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\splat_spike_20260612_213624`.
- Reference document: `docs/GAUSSIAN_SPLAT_SPIKE.md`.

## Renderer/Package Result

No Unity Gaussian splat renderer package was detected in the project. The v0.6 code therefore kept splats optional and isolated:

- `MeshSceneryProvider`: active default, mesh fallback.
- `SplatSceneryProvider`: experimental, off by default, fails safe when no renderer package is present.
- `SceneryModeController`: default mode is `MeshFallback`.
- `SceneryEvidenceLogger`: writes spike JSON/CSV/Markdown evidence.

The proxy point cloud created during the spike is not a true Gaussian splat renderer and is not Quest visual proof.

## Sample Assets/Budgets

Synthetic PLY samples were generated into the artifact folder only:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\splat_spike_20260612_213624\samples
```

| Splats | Asset Size | Estimated GPU Memory | Result |
| ---: | ---: | ---: | --- |
| 5,000 | 198,414 bytes / 0.189 MB | 0.229 MB | generated/proxy only |
| 50,000 | 1,981,781 bytes / 1.890 MB | 2.289 MB | generated/proxy only |
| 100,000 | 3,963,402 bytes / 3.780 MB | 4.578 MB | generated/proxy only |
| 200,000 | not generated | 9.155 MB | estimate only |
| 400,000 | not generated | 18.311 MB | estimate only |

The GPU memory estimate uses a planning approximation of 48 bytes per splat from public UnityGaussianSplatting notes. It is not runtime memory proof.

## Editor Spike Result

`scripts/run_splat_spike.ps1` completed successfully.

Generated outputs:

- `splat_editor_results.json`
- `splat_editor_results.csv`
- `splat_spike_summary.md`
- `unity_splat_spike.log`

Provider summary:

| Provider | Active Mode | Fallback Used | Renderer Available |
| --- | --- | --- | --- |
| MeshSceneryProvider | MeshFallback | no | yes |
| SplatSceneryProvider, renderer disabled/missing | MeshFallback | yes | no |
| SplatSceneryProvider, proxy enabled | ExperimentalSplatProxy | yes | no |

## Regression Result

Editor scenario runner result: 33/33 passed.

This confirms the v0.5 stabilized approach/go-around, traffic pattern, replay/debrief, airport reference, and cockpit/instrument scenario evidence stayed green with the new optional scenery layer present.

PlayMode result: 15/15 passed.

The new PlayMode coverage includes `SceneryModeControllerDefaultsToMeshFallback`.

## APK Build

- APK path: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab\QuestFlightLab\Builds\Android\QuestFlightLab-v0.1-dev.apk`
- APK size: 111,994,498 bytes
- APK SHA256: `92F977920A7395810ED46DF05DAC9D2D2D63378299F78C973C940260705AC1CF`
- Build result: passed.

## Quest Runtime

Quest runtime was not attempted for v0.6. This witness does not prove Gaussian splat rendering, frame time, stereo quality, or visual value on Quest hardware.

## Research Inputs

The spike design was informed by:

- `aras-p/UnityGaussianSplatting`: Unity-native splat renderer candidate, platform caveats, PLY/SPZ support, and memory planning notes.
- `ninjamode/Unity-VR-Gaussian-Splatting`: older VR/Quest proof/reference with reported Quest 3 budget claims, but experimental/unmaintained.
- PlayCanvas/SuperSplat performance guidance: splat count, depth sorting, fill-rate/overdraw, LOD streaming, and global budget concerns.
- Niantic SPZ: compressed splat delivery candidate for later asset-pipeline work.
- Meta Spatial SDK Gaussian Splat sample: future Quest/Spatial SDK research path, not a Unity/OpenXR dependency in this project.

Links are recorded in `docs/GAUSSIAN_SPLAT_SPIKE.md`.

## Artifacts

- `operator_notes.md`
- `repo_state.txt`
- `tooling_state.txt`
- `samples\synthetic_splats_5000.ply`
- `samples\synthetic_splats_50000.ply`
- `samples\synthetic_splats_100000.ply`
- `samples\sample_manifest.json`
- `unity_splat_spike.log`
- `splat_editor_results.json`
- `splat_editor_results.csv`
- `splat_spike_summary.md`
- `unity_editor_scenario_tests.log`
- `scenario_results.json`
- `scenario_results.csv`
- `flight_approach_summary.md`
- `unity_PlayMode_test_results.xml`
- `unity_PlayMode_tests.log`
- `build_android.log`
- `apk_hash.txt`

## Limitations

- This does not prove full-airport splat viability.
- This does not prove production photorealistic scenery.
- This does not prove final Quest performance.
- This does not prove Quest runtime behavior for splats.
- This does not prove a true Unity Gaussian splat renderer package works on Android.
- This does not change C172-style flight-model fidelity.
- This does not prove USB2BLE physical input behavior.
- This is not FAA-approved training, BATD/AATD qualification, or legal pilot-training credit.
- Mesh/terrain fallback remains the default shipping path.
