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

## Classification

`defer_to_later`

Reason:

- No true Unity Gaussian splat renderer package is installed in this project.
- The v0.6 proxy point cloud is only a budget/plumbing check.
- The mesh fallback remains green through scenario, PlayMode, and APK build validation.
- A real Quest splat viability claim requires a renderer package, tiny real splat asset, Android build, Quest runtime logs, and frame timing.

## Next Splat Milestone

The next splat chunk should be isolated from flight-model/training work:

1. Add `aras-p/UnityGaussianSplatting` in a branch or throwaway worktree.
2. Import or generate one tiny renderer-compatible PLY/SPZ.
3. Confirm Unity Editor rendering.
4. Confirm Android compile/build.
5. Run Quest 3 runtime with mesh fallback toggle available.
6. Test 5k, 50k, 100k, then stop early if frame/build/runtime quality regresses.
7. Only after a tiny Quest runtime pass, investigate LOD/chunking/SPZ.

## Limitations

- No full-airport splat viability is proven.
- No production photorealism claim is made.
- No final Quest performance claim is made.
- No Gaussian splat package is committed.
- No large splat assets are committed.
- Mesh/terrain fallback remains the default shipping path.
