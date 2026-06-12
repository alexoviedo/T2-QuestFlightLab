# Quest Setup

## Installed Toolchain

- Unity `6000.3.8f1`
- Android Build Support, SDK, NDK, and OpenJDK under the Unity install
- Meta Quest Developer Hub ADB at `C:\Program Files\Meta Quest Developer Hub\resources\bin\adb.exe`

## Current Status

Unity Hub license activation was required during bring-up and is now working for batchmode builds on this PC. If the license expires or Unity reports another license error, open Unity Hub, sign in, refresh the Unity Personal license, and rerun:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build_quest.ps1
```

The first Quest 3 runtime smoke passed on 2026-06-12 after the app was rebuilt with Android `resizeableActivity` disabled and Alex accepted the Quest controller-required launch prompt in headset.

Quest launch can still be intercepted while the headset is doffed or Touch controllers are asleep. If the app appears not to focus after install, put on the headset once, wake the Touch controllers, and accept or continue past the launch prompt.

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
- Runtime scene visibility was confirmed in headset and by ADB screenshot.
- USB2BLE's Xbox persona was paired as `Xbox Wireless Controller` and observed by Unity Input System as `XboxOneGamepadAndroid` during the input witness.
