# Quest Flight Input Lab v0.1

Quest Flight Input Lab is a standalone Meta Quest 3 Unity prototype for proving whether USB2BLE's Xbox BLE-compatible persona can act as the primary flight controller for a C172-style training-quality simulator direction.

This is a prototype designed toward training quality. It does not claim FAA-approved training, BATD/AATD qualification, broad Quest compatibility, or final C172 flight-model fidelity.

## Current Slice

- Unity project: `QuestFlightLab`
- Target platform: Android ARM64 standalone Quest 3 through OpenXR
- Primary input path: Unity Input System `Gamepad`
- USB2BLE target persona: `xbox_wireless_controller` advertising as `Xbox Wireless Controller`
- Initial aircraft: C172-style powered trainer approximation
- Initial airport reference: approximate Boulder Municipal `KBDU`, powered Runway 08/26 only, not for navigation

## Build

Unity batchmode is currently expected to require a valid Unity license/login on this machine. After Unity Hub is signed in and the editor is activated:

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

## Evidence

Runtime evidence JSON is written by the app under:

```text
Application.persistentDataPath\QuestFlightLab\evidence\session_<timestamp>.json
```

Use `scripts\run_adb_logcat.ps1` to capture Quest logs during input tests.

