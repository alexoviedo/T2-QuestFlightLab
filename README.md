# Quest Flight Input Lab

Quest Flight Input Lab is a standalone Meta Quest 3 Unity prototype for proving whether USB2BLE's Xbox BLE-compatible persona can act as the primary flight controller for a C172-style training-quality simulator direction.

This is a prototype designed toward training quality. It does not claim FAA-approved training, BATD/AATD qualification, broad Quest compatibility, or final C172 flight-model fidelity.

## Current Slice

- Unity project: `QuestFlightLab`
- Target platform: Android ARM64 standalone Quest 3 through OpenXR
- Primary input path: Unity Input System `Gamepad`
- USB2BLE target persona: `xbox_wireless_controller` advertising as `Xbox Wireless Controller`
- Initial aircraft: C172-style powered trainer approximation
- Initial airport reference: approximate Boulder Municipal `KBDU`, powered Runway 08/26 only, not for navigation
- v0.2 core path: deterministic Unity editor scenario runner plus PlayMode input-mapping probe so simulator changes can be tested without wearing the headset
- v0.3 fidelity path: public C172-style reference targets, stronger deterministic flight scenarios, named cockpit/instrument verification, and Basic Takeoff Familiarization checklist evidence
- v0.4 training path: expanded cockpit/training panel, Basic Traffic Pattern Familiarization scaffold, airport pattern gates, scored debrief reports, and 21-scenario autonomous evidence
- v0.5 approach path: source-backed stabilized approach/go-around prototype targets, approach-status cockpit fields, replay timeline export, approach debrief scoring, and autonomous stable/unstable approach evidence
- v0.6 scenery path: optional Gaussian splat feasibility abstraction and budget/proxy evidence with mesh/terrain fallback still default
- v0.6b renderer gate: `aras-p/UnityGaussianSplatting` renders synthetic 5k/50k/100k samples in the Unity editor using D3D12, and the Android APK builds with the package present; Quest runtime splat rendering is not yet proven
- v0.6c Quest splat runtime gate: the same real renderer loads synthetic 5k/50k/100k samples on one Quest 3 with Vulkan/Adreno 740 evidence and stereo ADB screenshots for 50k/100k; mesh/terrain fallback remains default, and real airport-capture viability is not proven
- v0.7 playable scenery path: an opt-in, project-owned procedural scenic splat patch adds airfield/foothills background modes (`scenic_splat_low`, `scenic_splat_medium`, optional `scenic_splat_high`) while mesh/terrain fallback remains the default playable airport
- v0.8 visual recovery path: `playable_demo` / `playable_visual_baseline` uses self-generated C172-style cockpit/exterior and airport geometry, hides cluttered training gates in playtest mode, and gates the real Gaussian renderer on Quest XR because headset captures showed a one-eye/headset-locked stereo composite failure
- v0.9 autonomous visual QA path: `scripts\run_visual_qa.ps1` captures deterministic Unity Editor screenshots/contact sheets for cockpit, HUD, runway, aircraft, airport, scenic/fallback, demo-pilot, and viewpoint-calibration views without Quest, ESP32, or headset access
- v0.9 production-direction path: `visual_fidelity_demo` keeps the safe visual baseline playable while JSBSim, Blender/OpenVSP, Poly Haven-class materials, and geospatial/Cesium research are evaluated behind evidence gates
- v1 production visual/physics upgrade path: the recommended demo keeps the previous imported C172 placeholder cockpit/aircraft, expands the KBDU-inspired mesh/procedural environment to an 8.8 km x 7.8 km playable visual footprint with 81 terrain chunks, far ridge impostors, roads/field/perimeter cues, a Quest-friendly render-quality profile, and JSBSim-vs-Unity comparator evidence

## Build

Unity batchmode has been validated on Alex's Windows 11 PC with Unity `6000.3.8f1` after Unity Hub license activation:

```powershell
Set-Location C:\Users\ovied\Dev\T2\T2-QuestFlightLab
powershell -ExecutionPolicy Bypass -File .\scripts\build_quest.ps1
```

The development APK path is:

```text
QuestFlightLab\Builds\Android\QuestFlightLab-v0.1-dev.apk
```

## Deploy

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install_quest.ps1
```

If ADB does not show a Quest device, connect Quest 3 by USB-C, enable Developer Mode, approve USB debugging in the headset, and rerun the install script.

On Quest, a VR launch may be intercepted until the headset is worn and Touch controllers are awake or the in-headset launch prompt is accepted.

Recommended short visual demo:

```powershell
.\scripts\launch_quest_playtest.ps1 -Mode visual_fidelity_demo -CaptureLogcat -DurationSeconds 85
```

`scenic_splat_medium` is gated to the mesh/procedural baseline in normal Quest playtest mode until the real Gaussian renderer is proven stereo/world-locked. Use `-SplatDiagnostic` only for bounded renderer debugging.

## Autonomous Visual QA

For no-headset visual iteration:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_visual_qa.ps1
```

This writes screenshots, `visual_qa_contact_sheet.png`, JSON/CSV/Markdown reports, image sanity analysis, and a Unity log to a setup-artifacts folder. It is the preferred path when Codex needs to inspect the current visual state without Quest hardware.

## Evidence

Runtime evidence JSON is written by the app under:

```text
Application.persistentDataPath\QuestFlightLab\evidence\session_<timestamp>.json
```

Use `scripts\run_adb_logcat.ps1` to capture Quest logs during input tests.

Bring-up artifacts from the first build/install pass are under:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\bringup_20260612_133231
```

Quest runtime smoke and USB2BLE Xbox input telemetry evidence from the first successful device witness are under:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\runtime_smoke_20260612_162235
```

Repo evidence notes:

- `docs/evidence/QUEST_RUNTIME_SMOKE_2026-06-12.md`
- `docs/evidence/USB2BLE_QUEST_XBOX_INPUT_WITNESS_2026-06-12.md`
- `docs/evidence/FLIGHT_CORE_AUTONOMOUS_SIM_WITNESS_2026-06-12.md`
- `docs/evidence/FLIGHT_MODEL_FIDELITY_AUTONOMOUS_WITNESS_2026-06-12.md`
- `docs/evidence/TRAFFIC_PATTERN_AUTONOMOUS_WITNESS_2026-06-12.md`
- `docs/evidence/STABILIZED_APPROACH_GO_AROUND_AUTONOMOUS_WITNESS_2026-06-12.md`
- `docs/evidence/GAUSSIAN_SPLAT_FEASIBILITY_SPIKE_2026-06-12.md`
- `docs/evidence/GAUSSIAN_SPLAT_REAL_RENDERER_SPIKE_2026-06-12.md`
- `docs/evidence/GAUSSIAN_SPLAT_QUEST_RUNTIME_SPIKE_2026-06-12.md`
- `docs/evidence/PLAYABLE_SCENIC_SPLAT_DEMO_WITNESS_2026-06-13.md`
- `docs/evidence/QUEST_VISUAL_RECOVERY_PLAYTEST_2026-07-03.md`
- `docs/evidence/AUTONOMOUS_VISUAL_QA_WITNESS_2026-07-08.md`
- `docs/evidence/PRODUCTION_FIDELITY_DIRECTION_WITNESS_2026-07-08.md`
- `docs/evidence/PRODUCTION_VISUAL_PHYSICS_UPGRADE_2026-07-08.md`

Autonomous simulator evidence from the v0.2 flight-core pass is under:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\flight_core_20260612_173102
```

Traffic-pattern autonomous evidence from the v0.4 cockpit/training pass is under:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\flight_pattern_20260612_194040
```

Stabilized approach/go-around autonomous evidence from the v0.5 pass is under:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\flight_approach_20260612_200229
```

Gaussian splat feasibility artifacts from the v0.6 spike are under:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\splat_spike_20260612_213624
```

The v0.6 result is `defer_to_later` for true Quest Gaussian splats: no production splat renderer is installed, the optional path fails safe, and the mesh/terrain fallback remains the green build path.

The v0.6b real-renderer gate is under:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\splat_renderer_20260612_220658
```

The v0.6b result is `android_build_only`: the real renderer works in Unity editor smoke tests up to 100k synthetic splats and the APK builds with the package present, but no Quest runtime splat rendering or frame timing has been proven.

The v0.6c Quest runtime splat gate is under:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\splat_runtime_20260612_232554
```

The v0.6c result is `quest_runtime_viable_small_scenic_patch` for synthetic static/background-style splat patches up to 100k splats on one Quest 3 test. It does not prove real airport captures, full-airport splats, production photorealism, or final Quest performance.

The v0.7 playable scenic splat demo artifacts are under:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\playable_splat_20260613_005918
```

The v0.7 scenic patch is procedural/project-owned and opt-in. It is not a real-world airport capture and does not replace the mesh airport.

The v0.8 visual recovery artifacts are under:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\visual_recovery_20260703_030335
```

The v0.8 result is a safer playable visual baseline with real splats classified as `blocked_xr_stereo_composite` in normal Quest playtest mode.

Run the deterministic simulator suite without the headset:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_editor_scenario_tests.ps1
```

Run the supplemental Unity PlayMode probe:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_unity_tests.ps1 -TestPlatform PlayMode
```
