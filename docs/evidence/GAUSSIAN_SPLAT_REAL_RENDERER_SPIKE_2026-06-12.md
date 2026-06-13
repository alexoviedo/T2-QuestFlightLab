# Gaussian Splat Real Renderer Spike Witness - 2026-06-12

## Summary

Flight Sim Core v0.6b tested a real Unity Gaussian splat renderer rather than the v0.6 proxy-only path. The spike used `aras-p/UnityGaussianSplatting` v1.1.1, generated renderer-compatible binary little-endian synthetic PLY samples outside the repo, created real `GaussianSplatAsset` sidecars in an ignored local generated folder, instantiated `GaussianSplatRenderer`, rendered editor screenshots with D3D12, reran mesh-fallback simulator regressions, reran PlayMode tests, and built the Android APK with the renderer package present.

Final classification: `android_build_only`.

This means editor rendering and Android build passed, but Quest runtime splat rendering, stereo quality, and headset frame timing were not proven.

## Build And Test Context

- Date/time: 2026-06-12 evening Mountain time / 2026-06-13 UTC artifact timestamps.
- Baseline commit: `486fb36b136921f516fd804a47c9f7d272d98483`.
- v0.6b commit: recorded in Git history for this file after commit.
- Unity: `6000.3.8f1`.
- Renderer package: `aras-p/UnityGaussianSplatting`.
- Renderer package version: `v1.1.1`.
- Renderer commit: `9310dce438da726244ace17eaf6f768826435fa4`.
- Package URL: `https://github.com/aras-p/UnityGaussianSplatting.git?path=/package#v1.1.1`.
- Editor graphics API for render smoke: Direct3D12.
- Artifact root: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\splat_renderer_20260612_220658`.
- Renderer selection note: `renderer_selection.md`.

## Renderer Selection

`aras-p/UnityGaussianSplatting` was selected because it is the upstream Unity package, has a usable `package/` folder, supports PLY/SPZ asset creation, and includes VR fixes credited from the older `ninjamode/Unity-VR-Gaussian-Splatting` path. The older ninjamode fork remains a Quest/VR reference, not the committed dependency.

Important caveats from upstream and related reports:

- Known working graphics APIs are D3D12, Metal, and Vulkan.
- DX11 is not a supported renderer path.
- Mobile/Android and Quest reports remain mixed.
- Splat source/capture licensing must be considered separately from the MIT viewer package.

## Sample Assets

Samples were generated outside the repo:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\splat_renderer_20260612_220658\real_samples
```

Schema: `unity_3dgs_binary_little_endian`.

Required PLY properties generated:

- `x`, `y`, `z`
- `nx`, `ny`, `nz`
- `f_dc_0`, `f_dc_1`, `f_dc_2`
- `opacity`
- `scale_0`, `scale_1`, `scale_2`
- `rot_0`, `rot_1`, `rot_2`, `rot_3`

| Splats | PLY Size | SHA256 |
| ---: | ---: | --- |
| 5,000 | 340,491 bytes | `84372AF81228DD52DE98F62D01410CE6C4D9C4AD9CD842411F4464BC868C8045` |
| 50,000 | 3,400,492 bytes | `CD5E34B4FAD7643DE409BC0AD1029F331E8EB02F97B9FA0454D6338E1331065A` |
| 100,000 | 6,800,493 bytes | `809A839AB58C1D08800A8EF813B8F8DF04996D05A3C0A0B249D3EE3F6C02761B` |

No PLY/SPZ samples or generated `GaussianSplatAsset` sidecars are committed.

## Editor Real Renderer Result

Command:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_real_splat_renderer_spike.ps1 -ArtifactDir <artifact-root>\real_renderer_editor -SampleDir <artifact-root>\real_samples -ForceD3D12
```

Result: editor render smoke passed for 5k, 50k, and 100k synthetic samples.

| Splats | Result | Input MB | Generated Asset MB | Valid Renderer | Nonblack Pixels | Avg Render ms |
| ---: | --- | ---: | ---: | --- | ---: | ---: |
| 5,000 | `editor_render_pass` | 0.325 | 0.337 | yes | 53,163 | 107.046 |
| 50,000 | `editor_render_pass` | 3.243 | 2.361 | yes | 57,154 | 0.214 |
| 100,000 | `editor_render_pass` | 6.485 | 4.721 | yes | 57,863 | 0.181 |

Screenshots:

- `real_splat_5000_editor.png`
- `real_splat_50000_editor.png`
- `real_splat_100000_editor.png`

The first 5k render includes one-time setup/import/warmup and should not be interpreted as steady-state frame timing.

## Android Build Gate

With the renderer package present:

- Mesh fallback scenario regression: 33/33 passed.
- PlayMode tests: 15/15 passed.
- Android APK build: passed.

APK:

- Path: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab\QuestFlightLab\Builds\Android\QuestFlightLab-v0.1-dev.apk`
- Size: 113,256,827 bytes
- SHA256: `6A95646F6B4F9520FFF87CF99DCF4D53EA8D368CC76350E9A33BBFCE76280359`

The Android build log includes `Packages/org.nesnausk.gaussian-splatting/Runtime/GaussianSplatRenderer.cs`, `GaussianSplatAsset.cs`, `GpuSorting.cs`, and related package runtime scripts in the build report.

## Quest Runtime

Quest runtime was not attempted for v0.6b. No headset splat visibility, stereo artifact, frame-rate, or runtime stability claim is made.

The next splat milestone must bind a tiny renderer asset into an experimental scene/toggle, install on Quest 3, capture logcat/screenshot/frame timing, and keep mesh fallback available.

## Artifacts

- `repo_state.txt`
- `tooling_state.txt`
- `isolation_plan.md`
- `renderer_selection.md`
- `baseline\*`
- `renderer_inspection\*`
- `package_probe\unity_package_probe_validate.log`
- `real_harness_compile_probe\unity_real_harness_validate.log`
- `real_samples\sample_manifest.json`
- `real_samples\sample_hashes.txt`
- `real_renderer_editor\real_splat_editor_results.json`
- `real_renderer_editor\real_splat_editor_results.csv`
- `real_renderer_editor\real_splat_editor_summary.md`
- `real_renderer_editor\real_splat_5000_editor.png`
- `real_renderer_editor\real_splat_50000_editor.png`
- `real_renderer_editor\real_splat_100000_editor.png`
- `real_renderer_editor\unity_real_splat_renderer.log`
- `android_gate_with_renderer\unity_editor_scenario_tests.log`
- `android_gate_with_renderer\unity_PlayMode_test_results.xml`
- `android_gate_with_renderer\build_android.log`
- `android_gate_with_renderer\apk_hash.txt`

## Limitations

- This does not prove Quest Gaussian splat runtime viability.
- This does not prove full-airport splat viability.
- This does not prove production photorealistic scenery.
- This does not prove final Quest performance.
- This does not prove useful visual quality for real scenic captures.
- This does not change C172-style flight-model fidelity.
- This does not prove USB2BLE physical input behavior.
- This is not FAA-approved training, BATD/AATD qualification, or legal pilot-training credit.
- Mesh/terrain fallback remains the default shipping path.
