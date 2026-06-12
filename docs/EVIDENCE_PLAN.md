# Evidence Plan

## App Evidence

The app writes one JSON session per run with:

- Platform, Unity version, device model, app version.
- Gamepad device name, layout, manufacturer, product, interface.
- Axes/buttons observed.
- Sample rate and last input timestamps.
- Scenario markers, resets, warnings, and errors.

## Host Evidence

Capture Quest logs while testing:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_adb_logcat.ps1
```

Archive the app JSON and logcat output in the setup artifacts directory for the test run.

## 2026-06-12 Bring-Up Artifact Root

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\bringup_20260612_133231
```

This folder contains repo state, Unity logs, ADB state, APK install logs, manifest dumps, startup logcat, and pulled app evidence JSON where available.
