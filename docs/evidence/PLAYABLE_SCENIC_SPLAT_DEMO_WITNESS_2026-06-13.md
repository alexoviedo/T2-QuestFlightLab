# Playable Scenic Splat Demo Witness - 2026-06-13

## Summary

v0.7 integrates one optional procedural scenic Gaussian splat patch into the playable QuestFlightLab demo path while keeping the mesh/terrain airport as the default. The patch is synthetic/project-owned and is intended as a visual-only approach/airport background layer.

Classification: `playable_demo_scenic_splat_blocked_runtime`

Reason: editor scenario, PlayMode, real renderer editor smoke, Android APK build, and APK install passed, but fresh v0.7 Quest runtime launch was intercepted by Meta's controller-required launch dialog before Unity received focus. ADB key events did not bypass the dialog.

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
| `scenic_splat_low` | procedural scenic | 25,000 | no | integrated, runtime launch blocked by Meta dialog |
| `scenic_splat_medium` | procedural scenic | 50,000 | no | integrated, not Quest-tested in v0.7 due launch blocker |
| `scenic_splat_high` | procedural scenic | 100,000 | no | integrated as optional upper budget, not Quest-tested in v0.7 due launch blocker |
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
| Quest mesh/scenic runtime | blocked before Unity focus | `quest_mesh_playable`, `quest_scenic_splat_low` |

PlayMode note: Direct3D11 PlayMode correctly falls back for the real renderer because the Gaussian splat compute kernel preflight reports unsupported DX11 kernels. The D3D12 editor smoke and prior Quest Vulkan runtime gate are the renderer proof paths.

## Quest Runtime Attempt

The fresh v0.7 APK installed successfully:

```text
Performing Streamed Install
Success
```

The Quest launch attempt did not reach Unity. Logcat showed:

```text
RequiresControllersLaunchInterceptor:
com.alexoviedo.t2.questflightlab/com.unity3d.player.UnityPlayerGameActivity
com.oculus.vrshell/.../LaunchCheckControllerRequiredDialogActivity
```

ADB key events (`KEYCODE_DPAD_CENTER`, `KEYCODE_ENTER`, and `KEYCODE_BUTTON_A`) did not bypass the dialog. Screenshots were zero-byte because the app never reached the Unity view during this attempt.

No v0.7 scenic runtime frame timing, stereo quality, or visual-readability claim is made.

## Result Interpretation

What v0.7 proves:

- Mesh fallback remains default and green.
- The procedural scenic splat asset path is legal/owned and repeatable.
- Scenic runtime assets import into Unity and are wired into opt-in modes.
- Existing training/approach scenario evidence remains green.
- PlayMode confirms metadata/fallback behavior.
- Android APK builds with the scenic assets included.
- APK installs on the Quest 3 connected over ADB.

What v0.7 does not prove:

- Fresh scenic splat rendering on Quest.
- Scenic frame timing on Quest.
- Headset stereo comfort for the scenic patch.
- Full-airport splat viability.
- Production photorealistic scenery.
- Real airport capture pipeline viability.
- Broad Quest compatibility.
- FAA-approved training, legal pilot-training credit, or final C172 fidelity.

## Next Action

Run one short headset-assisted runtime pass:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_quest_splat_runtime_mode.ps1 -Mode mesh -OutputDir <artifact-root>\quest_mesh_playable_accept
powershell -ExecutionPolicy Bypass -File .\scripts\run_quest_splat_runtime_mode.ps1 -Mode scenic_splat_low -OutputDir <artifact-root>\quest_scenic_splat_low_accept
powershell -ExecutionPolicy Bypass -File .\scripts\run_quest_splat_runtime_mode.ps1 -Mode scenic_splat_medium -OutputDir <artifact-root>\quest_scenic_splat_medium_accept
```

Human action required if the Meta launch check appears: wear the headset briefly, wake Touch controllers, and accept the controller-required launch prompt.
