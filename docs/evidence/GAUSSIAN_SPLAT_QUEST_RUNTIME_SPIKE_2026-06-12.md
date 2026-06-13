# Gaussian Splat Quest Runtime Spike Witness - 2026-06-12

## Summary

Flight Sim Core v0.6c tested the real `aras-p/UnityGaussianSplatting` renderer on a Quest 3 runtime build. Mesh fallback remained the default scene path, then the app was launched with explicit runtime scenery modes for mesh, 5k, 50k, and 100k synthetic splat samples.

Final classification: `quest_runtime_viable_small_scenic_patch` for synthetic static/background-style splat patches up to 100k splats on this one Quest 3 test, with important limits: no real airport capture was tested, no full-airport viability is proven, and no production photorealism or final Quest performance claim is made.

## Test Context

- Date/time: 2026-06-12 Mountain time / 2026-06-13 UTC artifact timestamps.
- Baseline commit at start: `2a4243c0504c4e9ebe1eb7fc65326410bae16a29`.
- Unity: `6000.3.8f1`.
- Package: `org.nesnausk.gaussian-splatting`.
- Renderer source: `aras-p/UnityGaussianSplatting`.
- Renderer package version: `v1.1.1`.
- Renderer commit: `9310dce438da726244ace17eaf6f768826435fa4`.
- Quest device: `Oculus Quest 3`, ADB product/device `eureka`.
- Runtime graphics API: Vulkan.
- Runtime GPU name: `Adreno (TM) 740`.
- APK: `QuestFlightLab\Builds\Android\QuestFlightLab-v0.1-dev.apk`.
- APK SHA256: `EC68D7D5AE26354E65AF662C1E1523452AACD108926C635AF74BED93FFC03395`.
- Artifact root: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\splat_runtime_20260612_232554`.

## Runtime Controls Added

The app now includes an experimental runtime gate controller that reads the Android intent extra `qfl_scenery_mode` and applies one of:

- `mesh`
- `splat_5k`
- `splat_50k`
- `splat_100k`

Mesh remains the default. Splat mode is opt-in and continues to report whether the real renderer was available, instantiated, and backed by a valid runtime asset. The evidence JSON records scenery mode, sample name, splat count, asset bytes, load time, frame timing, fallback status, and warnings.

## Samples

Synthetic PLY inputs were generated outside the repo, then imported into small Unity `Resources` runtime assets so Quest can load them from the APK. Raw PLY/SPZ files were not committed.

Source sample manifest:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\splat_runtime_20260612_232554\runtime_samples_final\synthetic_samples\sample_manifest.json
```

Generated PLY SHA256:

| Splats | SHA256 |
| ---: | --- |
| 5,000 | `84372AF81228DD52DE98F62D01410CE6C4D9C4AD9CD842411F4464BC868C8045` |
| 50,000 | `CD5E34B4FAD7643DE409BC0AD1029F331E8EB02F97B9FA0454D6338E1331065A` |
| 100,000 | `809A839AB58C1D08800A8EF813B8F8DF04996D05A3C0A0B249D3EE3F6C02761B` |

## Quest Runtime Results

| Mode | Active provider | Fallback | Splats | Asset bytes | Load ms | Frames | Avg ms | Max ms | Est. FPS | Result |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| `mesh` | `MeshSceneryProvider` | no | 0 | 0 | 0.00 | 458 | 14.00 | 64.24 | 71.43 | pass |
| `splat_5k` | `SplatSceneryProvider` | no | 5,000 | 352,352 | 211.81 | 462 | 14.70 | 333.46 | 68.02 | pass |
| `splat_50k` | `SplatSceneryProvider` | no | 50,000 | 2,474,688 | 213.58 | 525 | 14.58 | 332.19 | 68.61 | pass |
| `splat_100k` | `SplatSceneryProvider` | no | 100,000 | 4,949,312 | 206.68 | 461 | 14.67 | 330.83 | 68.16 | pass |

The max frame-time spikes are launch/load/focus-transition spikes from short runtime windows. Treat the average frame time and estimated FPS as smoke-test evidence, not final performance characterization.

## Final Validation

After the Quest runtime controls were added and the startup ordering was tightened:

- Splat abstraction spike: passed.
- Real renderer editor smoke: passed.
- Editor scenario regression: 33/33 passed.
- PlayMode tests: 15/15 passed.
- Android APK build: passed.
- Final APK install on Quest 3: passed.
- Final Quest runtime modes: mesh, 5k, 50k, and 100k passed.

Final validation artifacts:

- `final_validation_splat_spike\`
- `final_validation_real_splat_renderer\`
- `final_validation_editor_scenarios\`
- `final_validation_playmode\`
- `final_validation_build_android.log`
- `final_validation_apk_install.txt`
- `final_quest_mesh\`
- `final_quest_splat_5k\`
- `final_quest_splat_50k\`
- `final_quest_splat_100k\`

## Visual And Stereo Evidence

- `final_quest_mesh\adb_screenshot.png`
- `final_quest_mesh\pulled_scenery_runtime\scenery_runtime\quest_splat_runtime_mesh_20260613_061309.json`
- `final_quest_splat_5k\adb_screenshot.png`
- `final_quest_splat_5k\pulled_scenery_runtime\scenery_runtime\quest_splat_runtime_splat_5k_20260613_061409.json`
- `final_quest_splat_50k\adb_screenshot.png`
- `final_quest_splat_50k\pulled_scenery_runtime\scenery_runtime\quest_splat_runtime_splat_50k_20260613_061525.json`
- `final_quest_splat_100k\adb_screenshot.png`
- `final_quest_splat_100k\pulled_scenery_runtime\scenery_runtime\quest_splat_runtime_splat_100k_20260613_061644.json`

ADB screenshots for 5k, 50k, and 100k show visible synthetic splat points in both eye views. No fatal stereo/render errors appeared in the captured logcat for the tested samples. No manual in-headset stereo comfort/stutter judgment was requested for this run, so stereo quality is only evidenced by ADB screenshots and logs.

## Real Visual Asset Source Check

One real-source path was investigated after synthetic runtime passed:

- Meta's Spatial SDK Gaussian Splat sample documents bundled `.spz` assets and Quest runtime use through the experimental Spatial SDK Splat API.
- The Meta Spatial SDK Samples repository is multi-licensed: most code is MIT, while sample assets/supporting materials are covered by the Meta Platform Technologies SDK license.
- That makes the sample useful as a first-party Quest reference, but not a clean direct asset import into this Unity/OpenXR project during v0.6c.
- Niantic SPZ remains the preferred future compression/delivery candidate, but the current v0.6c Unity runtime path uses imported `GaussianSplatAsset` resources generated from PLY. SPZ import/conversion is a separate asset-pipeline task.

No real visual splat asset was imported or committed in v0.6c.

Investigation note:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\splat_runtime_20260612_232554\real_asset_source_investigation\real_asset_source_investigation.md
```

## Fallback Status

Mesh/terrain fallback remains default and green. The splat path is isolated behind runtime mode selection and is not used for cockpit geometry, aircraft geometry, UI, training gates, colliders, flight model, scoring, or lesson logic.

## Limitations

- This is one Quest 3 device, not broad Quest compatibility.
- This proves synthetic runtime splat rendering up to 100k in this project, not full-airport splat viability.
- This does not prove production photorealistic scenery.
- This does not prove final Quest performance, thermal stability, or long-session comfort.
- This does not prove a real-world airport capture pipeline.
- This does not prove FAA-approved training, BATD/AATD qualification, or legal pilot-training credit.
- This does not change C172-style flight-model fidelity.
- This does not add USB2BLE or physical HOTAS evidence.
- Mesh/terrain fallback remains the default shipping path.
