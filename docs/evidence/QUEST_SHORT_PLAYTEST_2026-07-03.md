# Quest Short Playtest - 2026-07-03

Short subjective Quest 3 play test for the v0.7 scenic splat demo path.

## Test Context

- Date/time: 2026-07-03 00:08-00:18 America/Denver
- Commit: `caa67b3419257678ccb270b7a4ea272bf1bad92e`
- Branch: `main`
- Unity: `6000.3.8f1`
- Quest model: `Oculus Quest 3`
- APK: `QuestFlightLab\Builds\Android\QuestFlightLab-v0.1-dev.apk`
- APK SHA256: `774D163E50CA1A8018E201E76D0CB857AD22BC8312C236A6DB6BC5C24E71EA4E`
- Scenery launch mode: `scenic_splat_medium`
- Runtime sample loaded: `scenic_airfield_medium_50000`
- Input source used: USB2BLE Xbox persona connected to Quest, with target-side virtual normalized-input replay bridged through the Xbox BLE path.

## Result

The app installed and launched on the connected Quest 3. Runtime scenery evidence says `scenic_splat_medium` loaded without fallback, with 50,000 splats and no warnings.

The subjective play test did not pass the intended visual/playability bar. Alex reported seeing an overlay with text, but nothing resembling splats, an aircraft, or an airport, and said head movement did not change the view. Alex also reported that the text overlay was disorganized with substantial overlap.

ADB screenshot evidence shows stereo rendering with runway/mesh-like airport geometry behind the UI, but the telemetry/training/menu panels overlap badly and dominate the view. The screenshot does not clearly prove useful scenic Gaussian splat visibility.

## Input And Motion Evidence

USB2BLE reported the Xbox persona connected. The automated sequence started virtual input, started the bridge, published stick/rudder/toe-brake frames, and ended with:

- `published=141`
- `skipped_not_connected=0`
- `skipped_not_ready=0`
- `last_error=none`

The Quest app evidence session recorded:

- Active gamepad: `Xbox Wireless Controller`
- Layout: `XboxOneGamepadAndroid`
- Observed axes: `leftStickX`, `leftStickY`
- Control ranges: aileron `-1..1`, elevator `-1..1`, rudder `0..0`, toe brakes `0..0`, throttle fixed at about `0.72`
- Flight telemetry ranges: airspeed `0..121.38 kt`, altitude `0..734.85 ft`, bank about `-60.45..60.43 deg`

This proves some app-side gamepad and aircraft-motion evidence, but it does not prove that Alex could visually confirm controls, control surfaces, splats, or aircraft motion in the headset.

## Performance And Comfort

Logcat VrApi samples during the USB2BLE sequence were mostly near the 72 Hz app target, with examples around `66-73/72` and some stale-frame bursts. This is only a short log sample, not final performance or thermal evidence.

Comfort was not fully assessed. The reported lack of convincing head-tracked scene response and the overlapping text are playability/comfort risks.

## Manual Actions

- Alex wore/checked the Quest headset.
- Alex reported live visual/playability observations.
- No code changes were made for the play test.
- USB2BLE pairing did not require a long debugging detour; the target already reported BLE connected.

## Artifacts

Artifact root:

`C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\playtest_20260703_000850`

Key files:

- `repo_state.txt`
- `apk_info.txt`
- `adb_devices.txt`
- `apk_install_log.txt`
- `quest_scenic_splat_medium_launch\logcat.txt`
- `quest_scenic_splat_medium_launch\pulled_scenery_runtime\scenery_runtime\quest_splat_runtime_scenic_splat_medium_20260703_061032.json`
- `quest_failed_visual_state_screenshot.png`
- `visual_observation.txt`
- `usb2ble_automated_playtest\usb2ble_control_sequence_transcript.txt`
- `usb2ble_automated_playtest\quest_logcat_during_usb2ble_sequence.txt`
- `usb2ble_automated_playtest\evidence\session_20260703_061030.json`
- `playtest_observation_summary.md`

## Limitations

- Short subjective play test only.
- Not broad Quest compatibility evidence.
- Not production photorealistic scenery evidence.
- Not full-airport splat viability evidence.
- Not final C172 fidelity evidence.
- Not FAA, BATD, AATD, or training suitability evidence.
- Not final performance or thermal stability proof.
- USB2BLE evidence used target-side virtual replay through the Xbox BLE path; it did not prove a full physical-control flight session.
- Scenic splat visibility was not subjectively confirmed in the headset despite runtime JSON reporting that the medium splat sample loaded.

## Recommended Next Chunk

Fix the first-view Quest demo presentation before adding features:

1. Declutter/reposition telemetry, training, touch-menu, and evidence-path text so panels do not overlap in stereo.
2. Add a very small launch/debug presentation mode that hides training/debug text and shows only FPS, gamepad status, basic controls, and scenery mode.
3. Verify head-tracked scene readability with fresh screenshots and a short headset observation before revisiting splat quality or flight-model work.
