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
- Final `main` APK SHA256: `AC3743955EB5FDF3D2BBE1B0DEB293B2638A8138A36EDCA51C130D28208E4804`.

Quest runtime:

- Not attempted for v0.6b.
- No Quest splat visibility, stereo quality, or frame timing claim is made.

## v0.6c Quest Runtime Gate

v0.6c tested the real renderer path on Quest 3 with mesh fallback first, then synthetic splat samples.

Artifact root:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\splat_runtime_20260612_232554
```

Runtime package/build:

- Unity: `6000.3.8f1`
- Renderer: `aras-p/UnityGaussianSplatting`
- Package: `org.nesnausk.gaussian-splatting`
- Package version/commit: `v1.1.1`, `9310dce438da726244ace17eaf6f768826435fa4`
- Quest device: `Oculus Quest 3` / `eureka`
- Runtime graphics API: Vulkan on `Adreno (TM) 740`
- APK SHA256: `EC68D7D5AE26354E65AF662C1E1523452AACD108926C635AF74BED93FFC03395`

Quest runtime results:

| Mode | Provider | Fallback | Splats | Asset bytes | Load ms | Avg frame ms | Est. FPS | Result |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| `mesh` | `MeshSceneryProvider` | no | 0 | 0 | 0.00 | 14.00 | 71.43 | pass |
| `splat_5k` | `SplatSceneryProvider` | no | 5,000 | 352,352 | 211.81 | 14.70 | 68.02 | pass |
| `splat_50k` | `SplatSceneryProvider` | no | 50,000 | 2,474,688 | 213.58 | 14.58 | 68.61 | pass |
| `splat_100k` | `SplatSceneryProvider` | no | 100,000 | 4,949,312 | 206.68 | 14.67 | 68.16 | pass |

ADB screenshots for 5k, 50k, and 100k show visible synthetic splat points in both eye views.

Real asset source check:

- Meta's Spatial SDK Gaussian Splat sample is useful Quest/runtime reference material, but it uses the Spatial SDK experimental Splat API and bundled `.spz` assets under Meta SDK/supporting-material licensing.
- No Meta sample asset was imported into this Unity/OpenXR project.
- Niantic SPZ remains a future compression/delivery candidate; SPZ import/conversion is a separate asset-pipeline task.

v0.6c evidence:

```text
docs/evidence/GAUSSIAN_SPLAT_QUEST_RUNTIME_SPIKE_2026-06-12.md
```

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

v0.6c classification: `quest_runtime_viable_small_scenic_patch` for synthetic static/background-style splat patches up to 100k splats on this one Quest 3 test.

Reason:

- Mesh fallback runtime smoke passed first.
- The real renderer instantiated on Quest for 5k, 50k, and 100k synthetic samples.
- No splat runtime test fell back to mesh.
- Logcat captured renderer activation and evidence writes.
- App evidence captured frame timing for each mode.
- ADB screenshots captured visible stereo splat points for 50k and 100k.

Important caveat: this is synthetic-only runtime evidence. It does not prove full-airport capture viability, production photorealistic scenery, long-session thermal behavior, or a real asset pipeline.

## Next Splat Milestone

The next splat chunk should remain isolated from flight-model/training work:

1. Capture or source a tiny owned/licensed real-world SPZ/PLY sample.
2. Convert/import it into the current Unity renderer path without committing large raw assets.
3. Test 50k and 100k visible budgets against real overdraw, not just synthetic points.
4. Add explicit LOD/chunking/global-budget controls before increasing beyond 100k.
5. Investigate SPZ compression/conversion as an asset-pipeline step.
6. Keep mesh/terrain fallback as the default shipping path.

## Limitations

- No full-airport splat viability is proven.
- No production photorealism claim is made.
- No final Quest performance claim is made beyond the short synthetic v0.6c runtime windows.
- No large splat assets are committed.
- Mesh/terrain fallback remains the default shipping path.
