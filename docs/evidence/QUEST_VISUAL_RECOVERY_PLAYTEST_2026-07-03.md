# Quest Visual Recovery Playtest - 2026-07-03

## Summary

v0.8 improves the first Quest view with a safer playable visual baseline and gates the broken real Gaussian splat renderer out of normal Quest playtest mode.

## Build

- Repo commit before local changes: `64699ad790d46312b4e0f41d7eaf4d63f03ee2f2`
- Unity: `6000.3.8f1`
- Quest model: `Oculus Quest 3`
- Graphics API: Vulkan / `Adreno (TM) 740`
- Latest installed APK SHA256 after the C172 cockpit/window pass: `FA5268045E5D4CBAA7FF486DC3DC6B81C6972A2A101701FACB13F293E6887F08`
- Artifact root: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\visual_recovery_20260703_030335`

## Modes

- Recommended visual baseline: `playable_demo`
- Demo mode: `short_playtest`
- Splat-request fallback check: `scenic_splat_medium`
- Real splat status: `blocked_xr_stereo_composite` / diagnostic-only

## Evidence

- Before broken splat capture:
  - `before_scenic_splat_medium_nonblack_002s.png`
  - `before_scenic_splat_medium_nonblack_010s.png`
  - `before_scenic_splat_medium_nonblack_018s.png`
- After visual recovery screenshots:
  - `after_visual_recovery_v2\playable_demo.png`
  - `after_visual_recovery_v2\scenic_splat_medium_gated.png`
  - `after_visual_recovery_v2\playable_demo_long_capture\adb_screenshot.png`
- After rejected generic cockpit placeholder screenshots:
  - `after_cockpit_model_v2\adb_screenshot.png`
  - `after_cockpit_model_v2\cockpit_model_v2_now_1.png`
  - `after_cockpit_model_v2\cockpit_model_v2_now_2.png`
- C172 procedural cockpit/exterior diagnostic-camera screenshots:
  - `after_c172_diagnostic_camera_capture\pulled_first_view_diagnostics\first_view_diagnostics\quest_first_view_startup_20260703_103458.png`
  - `after_c172_diagnostic_camera_capture\pulled_first_view_diagnostics\first_view_diagnostics\quest_first_view_demo_20260703_103503.png`
  - `after_c172_scale_cockpit_pass\pulled_first_view_diagnostics\first_view_diagnostics\quest_first_view_startup_20260703_104143.png`
  - `after_c172_scale_cockpit_pass\pulled_first_view_diagnostics\first_view_diagnostics\quest_first_view_demo_20260703_104148.png`
- Latest C172 cockpit/window-shell final build:
  - `after_c172_final_window_shell_pass\apk_sha256.txt`
  - `after_c172_final_window_shell_pass\apk_install_log.txt`
- Gated splat evidence:
  - `after_visual_recovery_v2\scenic_splat_medium_gated_capture\pulled_scenery_runtime\scenery_runtime\quest_splat_runtime_scenic_splat_medium_20260703_092058.json`
- Head tracking evidence:
  - `after_visual_recovery_v2\scenic_splat_medium_gated_capture\pulled_first_view_diagnostics\first_view_diagnostics\quest_first_view_20260703_092623.json`
  - `after_cockpit_model_v2\pulled_first_view_diagnostics\first_view_diagnostics\quest_first_view_20260703_095514.json`
- Demo movement evidence:
  - `after_visual_recovery_v2\playable_demo_long_capture\pulled_demo_pilot\demo_pilot\quest_demo_pilot_20260703_092842.json`
  - `after_cockpit_model_v2\pulled_demo_pilot\demo_pilot\quest_demo_pilot_20260703_095514.json`
- Playable demo scenery evidence:
  - `after_cockpit_model_v2\pulled_scenery_runtime\scenery_runtime\quest_splat_runtime_playable_demo_20260703_095524.json`
- Logcat:
  - `after_visual_recovery_v2\scenic_splat_medium_gated_capture\logcat.txt`
  - `after_visual_recovery_v2\playable_demo_long_capture\logcat.txt`
  - `after_cockpit_model_v2\logcat.txt`
  - `after_c172_diagnostic_camera_capture\logcat.txt`
  - `after_c172_scale_cockpit_pass\logcat.txt`

## Results

- Head tracking: evidence reports `headPoseChanged=true`, XR enabled/device active/loader active, and tracked head pose.
- HUD readability: compact Playtest HUD active; verbose panels hidden.
- Cockpit/aircraft visibility: C172-style procedural cockpit/exterior, yoke, instruments, windshield frame, left-seat pilot placement, nose/wing cues, runway, and transparent window openings are implemented in the final build. Diagnostic-camera captures proved the runway/HUD/nose reference path; fresh headset confirmation of the final window-shell pass is still pending.
- Airport visibility: runway markings, lights, hangars, trees, foothills, and runway/airport context visible.
- Demo aircraft motion: long demo evidence reports `visualFlightEnvelopeApplied=true`, `aircraftMoved=true`, `aircraftAirborne=true`, distance from start about `999.7 m`, phase `shallow left bank`.
- Splat status: `scenic_splat_medium` requested, but active mode was `MeshFallback`; evidence warning: `Real Gaussian splat renderer disabled: XR stereo/world-lock check failed`.
- Frame timing: gated scenic evidence estimated about `71.3 FPS` during the short capture.

## Asset Sources

v0.8 visual improvements use self-generated Unity procedural primitives/materials for the C172-style cockpit/exterior and airport baseline. A generic imported cockpit-control placeholder was tried during iteration, rejected by Alex as wrong aircraft/scale, removed from the playable path, and not committed as a required asset. The final exterior no longer uses an opaque solid cabin shell behind transparent window panes; cabin windows are represented as openings with transparent glass/frame pieces. See `docs/ASSET_SOURCES.md`.

## Manual Observations

Alex reported before this fix that head tracking had begun working, but the splat appeared one-eye/headset-locked and not tied to world geometry.

After the v0.8 visual recovery build, Alex confirmed:

- head tracking works,
- HUD is readable,
- cockpit/aircraft cues are visible,
- runway/airport are visible,
- scenery is only a little better, not by much,
- splats are not visible.

Interpretation: first-view usability is recovered enough to see and read the cockpit/runway/HUD, but visual quality remains a major blocker for a convincing demo. Real splats are not visible because they are gated off in normal Quest playtest mode after headset captures showed the renderer was not stereo/world-locked.

Follow-up from Alex: the cockpit/aircraft still looked wrong for a Cessna: scale was off, the body was unrealistic, cockpit details did not match the target aircraft, cockpit windows were opaque, and the pilot seat was misplaced. The playable path was then changed to remove the generic imported placeholder and generate a left-seat C172-style cockpit/exterior with transparent window openings and more realistic controls.

The latest final APK installed successfully, but the last attempted fresh Quest launch was intercepted by the Quest `RequiresControllersLaunchInterceptor` controller prompt. That prevented fresh Unity logs and fresh first-view PNGs for the final window-shell pass until Alex wakes/accepts the Touch controller launch prompt in-headset.

Rating/top-three visual issues and final headset confirmation of the C172 cockpit/window pass are pending in `human_visual_confirmation.md`.

## Limitations

- Short Quest playtest/capture only.
- Not broad Quest compatibility.
- Not production photorealism.
- Not full-airport splat viability.
- Not final C172 fidelity.
- Not FAA/BATD/AATD/training suitability.
- Not final performance/thermal proof.
- Real Gaussian splats are not fixed; they are safely gated in normal Quest playtest mode.
- Latest C172 visuals are procedural placeholders for short playability, not a final modeled C172.
