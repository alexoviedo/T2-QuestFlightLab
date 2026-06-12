# Quest Setup

## Installed Toolchain

- Unity `6000.3.8f1`
- Android Build Support, SDK, NDK, and OpenJDK under the Unity install
- Meta Quest Developer Hub ADB at `C:\Program Files\Meta Quest Developer Hub\resources\bin\adb.exe`

## Current Blocker

Unity batchmode reported:

```text
No valid Unity Editor license found. Please activate your license.
```

Open Unity Hub, sign in, and activate a valid Unity license. Then rerun:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build_quest.ps1
```

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

