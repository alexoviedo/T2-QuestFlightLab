# Playable Scenic Splat Demo Witness - 2026-06-13

## Summary

v0.7 integrates one optional procedural scenic Gaussian splat patch into the playable QuestFlightLab demo path while keeping the mesh/terrain airport as the default. The patch is synthetic/project-owned and is intended as a visual-only approach/airport background layer.

Classification: `playable_demo_scenic_splat_medium_viable`

Reason: after a Quest shell reboot/recovery, mesh fallback and the procedural scenic splat modes launched on one Quest 3, rendered visible stereo screenshots, wrote runtime evidence JSON, and stayed near the app's 72 Hz target in short captures. Low, medium, and high scenic budgets all loaded without falling back. This is still a synthetic/procedural scenic patch result, not proof of full-airport splat viability or final Quest performance.

## Build Context

- Date/time: 2026-06-13 America/Denver
- Commit under test before v0.7 commit: `bae7ab178c4fbbefb01551a55429044881e28a84`
- Unity: `6000.3.8f1`
- Renderer: `aras-p/UnityGaussianSplatting`
- Package: `org.nesnausk.gaussian-splatting`
- Package version/commit: `v1.1.1`, `9310dce438da726244ace17eaf6f768826435fa4`
- Quest device visible over ADB: `2G0YC5ZG8907TD device product:eureka model:Quest_3`
- APK: `QuestFlightLab\Builds\Android\QuestFlightLab-v0.1-dev.apk`
- APK SHA256: `774D163E50CA1A8018E201E76D0CB857AD22BC8312C236A6DB6BC5C24E71EA4E`
- APK bytes: `113554816`

Artifact root:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\playable_splat_20260613_005918
```

Post-reboot Quest runtime artifact root:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\playable_splat_runtime_recovery_20260613_015006
```

## Asset Source

Chosen path: procedural scenic splat patch.

- Source: `tools/generate_scenic_splat_patch.py`
- License/source status: project-owned generated asset
- Real-world status: synthetic, not a real KBDU capture
- Use: optional visual-only scenic/background layer
- Exclusions: not cockpit, aircraft, UI, training gates, colliders, scoring, flight model, or runway collision path

Raw generated PLYs were written to artifacts, not committed:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\playable_splat_20260613_005918\scenic_asset_import\procedural_scenic_samples
```

Generated raw sample manifest:

| Profile | Splats | Raw PLY MB | SHA256 |
| --- | ---: | ---: | --- |
| low | 25,000 | 1.6217 | `DE9E032A393F587D970DC93729F09050E843CFCDED6FA4F793A842D10B8933EA` |
| medium | 50,000 | 3.2430 | `A4860B61D36D1A2C1AD89E929D1961B5899433EC80370C2BBF22BCCB4E795B62` |
| high | 100,000 | 6.4855 | `20B13E74A29FECC94FE98F38B4E71CCDE79C353826471C893D668A9C07CEC1FC` |

The Unity runtime assets are committed because the APK needs them in `Resources` for the opt-in scenic modes. Raw PLY captures remain outside the repo.

## Runtime Modes

| Mode | Profile | Splats | Default? | Status |
| --- | --- | ---: | --- | --- |
| `mesh` | mesh fallback | 0 | yes | default playable path |
| `scenic_splat_low` | procedural scenic | 25,000 | no | Quest short runtime pass after reboot |
| `scenic_splat_medium` | procedural scenic | 50,000 | no | Quest short runtime pass after reboot; current recommended splat demo budget |
| `scenic_splat_high` | procedural scenic | 100,000 | no | Quest short runtime pass after reboot; keep optional, not default |
| `splat_5k` | synthetic regression | 5,000 | no | retained |
| `splat_50k` | synthetic regression | 50,000 | no | retained |
| `splat_100k` | synthetic regression | 100,000 | no | retained |

## Validation

| Check | Result | Artifact |
| --- | --- | --- |
| Splat fallback/proxy spike | pass | `validation\splat_spike` |
| Real renderer editor smoke | pass for synthetic 5k, 50k, 100k under D3D12 | `validation\real_splat_renderer_final` |
| Editor scenario runner | 33/33 passed | `validation\editor_scenarios` |
| PlayMode tests | 17/17 passed | `validation\playmode_final` |
| Android APK build | passed | `apk_hash.txt` |
| APK install | passed | `apk_install_log.txt` |
| Quest mesh/scenic runtime | passed short post-reboot smoke | `playable_splat_runtime_recovery_20260613_015006` |

PlayMode note: Direct3D11 PlayMode correctly falls back for the real renderer because the Gaussian splat compute kernel preflight reports unsupported DX11 kernels. The D3D12 editor smoke and prior Quest Vulkan runtime gate are the renderer proof paths.

## Quest Runtime

The fresh v0.7 APK installed successfully:

```text
Performing Streamed Install
Success
```

An initial Quest launch attempt was intercepted by Meta's controller-required launch dialog before Unity received focus. ADB key events did not bypass that dialog. Alex later observed a passthrough/menu recovery issue; the headset was rebooted over ADB and returned to a normal Quest Home state.

After reboot, `adb devices -l` showed:

```text
2G0YC5ZG8907TD device product:eureka model:Quest_3 device:eureka
```

Post-reboot short runtime captures:

| Mode | Sample | Splats | Fallback | Avg frame ms | Estimated FPS | Screenshot/log/evidence path |
| --- | --- | ---: | --- | ---: | ---: | --- |
| `mesh` | mesh fallback | 0 | no | 13.884 | 72.02 | `playable_splat_runtime_recovery_20260613_015006\quest_mesh_playable_after_reboot_long_capture` |
| `scenic_splat_low` | `scenic_airfield_low_25000` | 25,000 | no | 14.768 | 67.71 | `playable_splat_runtime_recovery_20260613_015006\quest_scenic_splat_low_after_reboot` |
| `scenic_splat_medium` | `scenic_airfield_medium_50000` | 50,000 | no | 14.728 | 67.90 | `playable_splat_runtime_recovery_20260613_015006\quest_scenic_splat_medium_after_reboot` |
| `scenic_splat_high` | `scenic_airfield_high_100000` | 100,000 | no | 14.744 | 67.83 | `playable_splat_runtime_recovery_20260613_015006\quest_scenic_splat_high_after_reboot` |

Each post-reboot run captured an ADB screenshot showing the QuestFlightLab scene in both eyes. The mesh/default scene and scenic splat modes kept the cockpit/input UI readable in the captured view. No manual comfort/stereo-quality judgment was performed, so this evidence does not prove headset comfort.

## Result Interpretation

What v0.7 proves:

- Mesh fallback remains default and green.
- The procedural scenic splat asset path is legal/owned and repeatable.
- Scenic runtime assets import into Unity and are wired into opt-in modes.
- Existing training/approach scenario evidence remains green.
- PlayMode confirms metadata/fallback behavior.
- Android APK builds with the scenic assets included.
- APK installs on the Quest 3 connected over ADB.
- Mesh fallback and procedural scenic splat low/medium/high modes launch after Quest reboot.
- Scenic splat low/medium/high load the real Gaussian splat renderer with valid assets on Quest Vulkan.
- The short captures show roughly 68 FPS estimated for scenic modes on this one Quest 3.

What v0.7 does not prove:

- Full-airport splat viability.
- Production photorealistic scenery.
- Final Quest performance, thermal behavior, or comfort over long sessions.
- Headset stereo comfort for the scenic patch, because no manual in-headset comfort judgment was recorded.
- Real airport capture pipeline viability.
- Broad Quest compatibility.
- FAA-approved training, legal pilot-training credit, or final C172 fidelity.

## Next Action

Keep mesh fallback as the default. Use `scenic_splat_medium` as the next playable demo target budget, with `scenic_splat_high` available only as an opt-in upper-budget smoke mode. The next scenic milestone should improve placement/readability and add an owned real-world capture path, not attempt full-airport splats.
