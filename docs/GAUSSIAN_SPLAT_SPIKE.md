# Gaussian Splat Feasibility Spike

## Goal

Determine whether Gaussian splats are a viable future path for static scenic realism on standalone Quest 3 without destabilizing the simulator. The mesh/terrain airport remains the default and required fallback.

This spike is not a photorealistic airport claim, not a production scenery pipeline, and not evidence of final Quest performance.

## Intended QuestFlightLab Use Case

Gaussian splats, if later proven viable, are only candidates for:

- static airport/scenic background patches,
- distant approach-area context,
- optional visual enhancement layers.

They are not intended for:

- cockpit geometry,
- aircraft geometry,
- UI or training gates,
- colliders or physics,
- flight-model, scoring, or lesson logic,
- a full airport database in the near term.

## Renderer Candidates

1. [`aras-p/UnityGaussianSplatting`](https://github.com/aras-p/UnityGaussianSplatting)
   - Unity-native Gaussian splat visualization path.
   - Supports PLY and Scaniverse SPZ in current public docs.
   - Project notes indicate platform caveats and no large future development commitment, so this must be tested cautiously before Android/Quest adoption.
2. [`ninjamode/Unity-VR-Gaussian-Splatting`](https://github.com/ninjamode/Unity-VR-Gaussian-Splatting)
   - Older VR/Quest-oriented fork and useful proof/reference.
   - Reports Quest 3 can reach 72 FPS up to roughly 400k Gaussians with proper settings, but describes itself as experimental/unmaintained and points back toward upstream VR support.
3. [Meta Spatial SDK Gaussian Splat sample](https://developers.meta.com/horizon/documentation/spatial-sdk/spatial-sdk-sample-gaussiansplat/)
   - Useful Meta/Quest research reference.
   - It uses the Spatial SDK experimental Splat API and SPZ assets, not this Unity/OpenXR project path.
4. [PlayCanvas/SuperSplat performance guidance](https://developer.playcanvas.com/user-manual/gaussian-splatting/building/performance/)
   - Useful optimization reference, especially on splat count, depth sorting, fill-rate/overdraw, LOD streaming, and global budgets.
   - Not a native Unity APK renderer path.
5. [Niantic SPZ](https://github.com/nianticlabs/spz)
   - Future compressed delivery/asset-pipeline candidate.
   - Treat as a later asset-size/runtime-loading investigation, not a v0.6 blocker.

## Budget Hypotheses

Initial Quest 3 budget tiers:

| Budget | Purpose |
| ---: | --- |
| 5k splats | Tiny smoke/proxy asset and asset-pipeline plumbing. |
| 50k splats | Minimum useful static scenic patch candidate. |
| 100k splats | Upper bound for this first offline spike sample. |
| 200k splats | Estimate only unless lower budgets are proven healthy. |
| 400k splats | Aspirational upper reference from VR/Quest reports; requires real renderer/runtime proof. |

Best-practice hypotheses:

- Keep viewers away from near-field splat surfaces to reduce overdraw.
- Trim/prune captures aggressively.
- Chunk by airport/approach area.
- Load only nearby chunks.
- Add future LOD streaming and global splat budgets.
- Consider SPZ/compression after renderer viability is proven.
- Keep mesh fallback as the shipping scene.

## Fast-Fail Criteria

Stop or defer if:

- package restore or Unity compile breaks,
- Android APK build fails,
- Quest runtime crashes,
- frame time is too high,
- stereo artifacts appear,
- there is no safe mesh fallback,
- asset size is too large,
- renderer integration is brittle or demands a large refactor.

## v0.6 Implementation

v0.6 adds an optional scenery-provider layer:

- `SceneryMode`
- `SceneryVisualProvider`
- `MeshSceneryProvider`
- `SplatSceneryProvider`
- `SceneryModeController`
- `SceneryPerformanceProbe`
- `SceneryEvidenceLogger`

The default mode is `MeshFallback`. The experimental splat path is off by default and compiles without any external splat renderer package installed. If no renderer is detected, the app keeps the mesh fallback and logs the missing renderer.

The spike also adds:

- `scripts/run_splat_spike.ps1`
- `tools/generate_tiny_splat_samples.py`
- `QuestFlightLab.Editor.SplatSpikeBatchRunner.RunSplatSpike`

Synthetic PLY samples are generated into the artifact folder only. They are budget/proxy fixtures, not committed scenic assets and not production Gaussian splat captures.

## v0.6 Result

Artifact root:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\splat_spike_20260612_213624
```

Results:

- Unity: `6000.3.8f1`
- Editor graphics device: Direct3D11
- Renderer package detected: none
- Synthetic sample budgets generated: 5k, 50k, 100k
- Estimated-only budgets retained: 200k, 400k
- Editor spike runner: passed
- v0.5 deterministic scenario regression: 33/33 passed
- PlayMode regression: 15/15 passed
- Android APK build: passed
- Quest runtime: not attempted for v0.6
- Viability classification: `defer_to_later`

Budget evidence:

| Splats | Synthetic Asset MB | Estimated GPU MB | Result |
| ---: | ---: | ---: | --- |
| 5,000 | 0.189 | 0.229 | generated/proxy only |
| 50,000 | 1.890 | 2.289 | generated/proxy only |
| 100,000 | 3.780 | 4.578 | generated/proxy only |
| 200,000 | n/a | 9.155 | estimate only |
| 400,000 | n/a | 18.311 | estimate only |

The GPU estimate uses the public `UnityGaussianSplatting` memory note of roughly 48 bytes per splat for sorting/cache planning. It is a planning estimate, not runtime memory proof.

## v0.6b Real Renderer Gate

v0.6b tested a real renderer package:

- Renderer: [`aras-p/UnityGaussianSplatting`](https://github.com/aras-p/UnityGaussianSplatting)
- Package URL: `https://github.com/aras-p/UnityGaussianSplatting.git?path=/package#v1.1.1`
- Package commit: `9310dce438da726244ace17eaf6f768826435fa4`
- Package name: `org.nesnausk.gaussian-splatting`
- License: MIT for the viewer/package code; splat capture/source licensing remains a separate concern.

Artifact root:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\splat_renderer_20260612_220658
```

The v0.6b sample generator adds `--schema unity-3dgs-binary`, producing binary little-endian PLY files with the properties required by UnityGaussianSplatting:

- `x`, `y`, `z`
- `nx`, `ny`, `nz`
- `f_dc_0`, `f_dc_1`, `f_dc_2`
- `opacity`
- `scale_0`, `scale_1`, `scale_2`
- `rot_0`, `rot_1`, `rot_2`, `rot_3`

Editor smoke used D3D12 because upstream documents D3D12/Metal/Vulkan as known-good graphics APIs and says DX11 is not supported.

| Splats | Input PLY MB | Generated Asset MB | Editor Result | Pixels | Avg Render ms |
| ---: | ---: | ---: | --- | ---: | ---: |
| 5,000 | 0.325 | 0.337 | `editor_render_pass` | 53,163 | 107.046 |
| 50,000 | 3.243 | 2.361 | `editor_render_pass` | 57,154 | 0.214 |
| 100,000 | 6.485 | 4.721 | `editor_render_pass` | 57,863 | 0.181 |

The first 5k render includes one-time setup/import/render warmup cost and should not be read as steady-state performance.

Android gate:

- Mesh fallback scenario regression: 33/33 passed.
- PlayMode: 15/15 passed.
- Android APK build with package present: passed.
- APK SHA256: `6A95646F6B4F9520FFF87CF99DCF4D53EA8D368CC76350E9A33BBFCE76280359`.

Quest runtime:

- Not attempted for v0.6b.
- No Quest splat visibility, stereo quality, or frame timing claim is made.

## Classification

v0.6 classification: `defer_to_later`

Reason:

- At the time of v0.6, no true Unity Gaussian splat renderer package was installed in this project.
- The v0.6 proxy point cloud is only a budget/plumbing check.
- The mesh fallback remains green through scenario, PlayMode, and APK build validation.
- A real Quest splat viability claim requires a renderer package, tiny real splat asset, Android build, Quest runtime logs, and frame timing.

v0.6b classification: `android_build_only`

Reason:

- The real renderer package resolves and compiles.
- Synthetic 5k/50k/100k samples render in the Unity editor with D3D12.
- The Android APK builds with the package present.
- No Quest runtime splat render was attempted, so this is not Quest viability.

## Next Splat Milestone

The next splat chunk should remain isolated from flight-model/training work:

1. Bind a tiny generated/committed-or-runtime-generated `GaussianSplatAsset` into an experimental scene or toggle.
2. Build and install on Quest 3.
3. Capture logcat, screenshot, and frame timing.
4. Confirm mesh fallback can still be selected.
5. Test 5k first, then 50k and 100k only if runtime remains stable.
6. Only after a tiny Quest runtime pass, investigate LOD/chunking/SPZ.

## Limitations

- No full-airport splat viability is proven.
- No production photorealism claim is made.
- No final Quest performance claim is made.
- No large splat assets are committed.
- Mesh/terrain fallback remains the default shipping path.
