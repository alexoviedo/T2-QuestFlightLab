# Quest Runtime Smoke - 2026-06-12

## Scope

This witness covers one local Quest 3 runtime smoke for Quest Flight Input Lab v0.1. It does not claim broad Quest compatibility, production readiness, FAA/training suitability, or final flight-model fidelity.

## Environment

- Repo state under test: `e707b12399c6b198da5d09e0129c9edbc3262ec2` plus the local `androidResizeableActivity=false` Quest launch patch
- Unity: `6000.3.8f1`
- Device: Quest 3, product/device `eureka`
- ADB serial: `2G0YC5ZG8907TD`
- Package: `com.alexoviedo.t2.questflightlab`
- Activity: `com.unity3d.player.UnityPlayerGameActivity`
- APK: `QuestFlightLab/Builds/Android/QuestFlightLab-v0.1-dev.apk`
- APK SHA256: `BDF4452200DE3D2E0C852909FD28B1FCFB03DA2936C0D127E202F8C71DC35F12`

## Result

PASS for first runtime smoke on the connected Quest 3.

Evidence captured:

- APK built and installed.
- ADB reported the Quest as `device`.
- Quest launch initially required Alex to wake/confirm Touch-controller prompt handling in headset.
- After that prompt, `dumpsys activity` showed `UnityPlayerGameActivity` resumed, visible, and focused.
- App PID was `21903` during the visible runtime check.
- Screenshot showed the QuestFlightLab scene, telemetry panel, runway/airport environment, aircraft/control visuals, and FPS HUD.
- Captured logcat showed no immediate `FATAL`, `AndroidRuntime`, `am_crash`, or `Force finishing` app crash lines in the smoke window.
- Runtime evidence JSON was written and later pulled from the Quest app data directory.

## Artifacts

Artifact root:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\runtime_smoke_20260612_162235
```

Key files:

- `quest_runtime_smoke_summary.md`
- `quest_runtime_visible_screenshot.png`
- `logcat_after_alex_visible.txt`
- `activity_after_alex_visible.txt`
- `pid_after_alex_visible.txt`
- `evidence_pull_after_visible/evidence/session_20260612_223655.json`

## Limitations

- This is one Quest 3 smoke test.
- The in-headset controller-required prompt still required manual acceptance.
- The smoke test alone did not prove USB2BLE input; that was tested afterward in a separate witness.

## Follow-Up Build Validation

After documentation and build-script cleanup, the project was rebuilt using the committed scene without regenerating `InputLab.unity`.

- Follow-up APK SHA256: `7743CDD065D23D0F690EDB46FB1CD81816B54884ADB6FC0DB3705658915F9B4F`
- Follow-up install result: `adb install -r` succeeded.
- Follow-up launch result while the headset was doffed: Quest shell again showed `LaunchCheckControllerRequiredDialogActivity`.

The in-headset visual smoke was not repeated for the follow-up APK to avoid another headset on/off cycle.
