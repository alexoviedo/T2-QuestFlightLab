# JSBSim Live Editor Driver Witness - 2026-07-08

## Summary

This witness records an Editor-only JSBSim live-driver gate. Unity sends per-frame controls and timesteps to a Python JSBSim sidecar, receives `c172x` aircraft state, and applies each returned pose to the visible imported C172 in the KBDU-inspired scene.

This is not a Quest runtime backend yet.

## Evidence

- Artifact root: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\jsbsim_live_driver_20260708_012959`
- Source revision before this run: `d281bb8783db92ea61f8857f0d8147ef0fef6bfa`
- Unity: `6000.3.8f1`
- JSBSim: `1.3.1`
- Aircraft: `c172x`
- Driver script: `scripts/run_jsbsim_live_editor_driver.ps1`
- Unity runner: `QuestFlightLab.Editor.JSBSimLiveEditorDriverRunner.RunLiveDriver`
- Python sidecar: `tools/jsbsim_probe/jsbsim_live_sidecar.py`
- Final live-driver report: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\jsbsim_live_driver_20260708_012959\live_driver_final\jsbsim_live_driver_report.json`
- Final live-driver summary: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\jsbsim_live_driver_20260708_012959\live_driver_final\jsbsim_live_driver_summary.md`
- Final live-driver frame CSV: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\jsbsim_live_driver_20260708_012959\live_driver_final\jsbsim_live_driver_frames.csv`
- Final live-driver screenshots: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\jsbsim_live_driver_20260708_012959\live_driver_final\screenshots`

## Live-Driver Result

- Classification: `editor_interactive_sidecar_frame_loop`
- Status: `PASS`
- Duration: 60.0 seconds
- Timestep: 0.033 seconds
- Controls/timesteps sent: 1,801
- JSBSim poses applied to Unity aircraft root: 1,801
- Pose changed: `true`
- Airborne: `true`
- Ground track: 879.4 m
- Max AGL: 9.9 m
- Max airspeed: 89.7 kt
- Max absolute bank reported by JSBSim: 62.3 deg
- Final heading: 232.4 deg
- Cockpit/aircraft asset: existing imported C172 placeholder preserved, 51 renderers
- World: `visual_fidelity_demo_medium`, 169 chunks, 462 LOD groups, 686 renderers, about 34,108 triangles, 9,200 m draw profile

## Validation

- Visual QA: passed, 14/14 shots
- Editor scenarios: passed, 33/33
- PlayMode tests: passed, 24/24
- Android APK build: passed
- APK SHA256: `9F0281D650468A48433EE5446B77194F6536ACF29EA28F2D8FF1E4670AEEE865`

## Quest Smoke

ADB was checked with both PATH `adb` and the Meta Quest Developer Hub `adb.exe`. No Quest device was listed, so install/launch/screenshot smoke could not be run in this chunk.

Status: `skipped_no_adb_device`

## Interpretation

The live-driver architecture is now proven in Editor: Unity can own the scene loop, send controls to JSBSim, and display JSBSim state on the current aircraft model. This is the right next step beyond the earlier batch/import bridge.

The result also exposes the next physics problem: the current `c172x` reset/control schedule is not a final flight-control law. Heading and bank transients remain visible in telemetry, so JSBSim runtime replacement needs a stable initialization/control-law pass before Android/Quest integration.

## Limitations

- Editor-only sidecar bridge.
- Not Android/Quest JSBSim runtime integration.
- Not final C172 fidelity.
- Not FAA-approved or legal training credit.
- Not broad Quest compatibility.
- Not final Quest performance or thermal proof.
- Quest headset smoke was not run because no ADB device was available.
