# Quest Setup

## Installed Toolchain

- Unity `6000.3.8f1`
- Android Build Support, SDK, NDK, and OpenJDK under the Unity install
- Meta Quest Developer Hub ADB at `C:\Program Files\Meta Quest Developer Hub\resources\bin\adb.exe`

## Current Blocker

Unity Hub license activation was required during bring-up and is now working for batchmode builds on this PC. If the license expires or Unity reports another license error, open Unity Hub, sign in, refresh the Unity Personal license, and rerun:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build_quest.ps1
```

The current remaining runtime smoke gate is in-headset launch focus: Quest logs show the VR launch can be intercepted by a controller-required prompt while the headset is doffed or controllers are asleep.

## Device Deploy Setup

1. Enable Developer Mode for the Quest account/device.
2. Connect Quest 3 by USB-C.
3. Put on the headset and approve USB debugging/RSA when prompted.
4. Confirm the host sees the headset:

```powershell
& "C:\Program Files\Meta Quest Developer Hub\resources\bin\adb.exe" devices
```

5. Build and install:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install_quest.ps1
```

6. Put on the headset, wake the Touch controllers if prompted, and accept any in-headset launch prompt for Quest Flight Input Lab.

## Verified Bring-Up State On 2026-06-12

- ADB device: `2G0YC5ZG8907TD`, model `Quest_3`, product/device `eureka`.
- Package id: `com.alexoviedo.t2.questflightlab`.
- APK built and installed successfully.
- APK manifest includes `com.oculus.intent.category.VR`, `android.hardware.vr.headtracking`, and `com.oculus.supportedDevices` with `eureka`.
- Full runtime scene visibility still requires in-headset confirmation.
