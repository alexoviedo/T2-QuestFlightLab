# USB2BLE Quest Test Plan

## Setup

1. Configure USB2BLE for the Xbox practical profile and static-random identity:

```powershell
Set-Location C:\Users\ovied\Dev\T2\T2-main
python tools\configure_board.py --port <PORT> preset flight-pack-xbox
python tools\configure_board.py --port <PORT> save
python tools\configure_board.py --port <PORT> start-configured
```

2. If configuring manually through the serial control plane, ensure:

```text
selected_persona=xbox_wireless_controller
identity_strategy=persona_static_random_experimental
```

3. Pair Quest 3 Bluetooth with `Xbox Wireless Controller`.
4. Launch Quest Flight Input Lab.

## Verification

- Telemetry panel shows a connected Unity Input System `Gamepad`.
- Device name/layout/description are logged.
- Left stick changes roll/elevator.
- Right stick X changes rudder.
- Triggers change toe brake bars.
- A writes an evidence marker.
- B resets the aircraft to the runway.
- X/Y move the flaps placeholder.
- D-pad changes trim/test marker telemetry.

## Evidence

Collect:

- App session JSON.
- Quest logcat.
- Notes on pairing result and whether USB2BLE virtual input moved Unity telemetry.

## 2026-06-12 Witness

A first Quest 3 witness passed for the Xbox path after Windows was stopped from reclaiming the USB2BLE Xbox identity and Alex paired `Xbox Wireless Controller` in Quest Bluetooth settings.

Observed in QuestFlightLab app evidence:

- Unity Input System display name: `Xbox Wireless Controller`
- Unity layout: `XboxOneGamepadAndroid`
- Axes/triggers mapped to aircraft control telemetry from `-1..1` or `0..1` as expected.
- A/B/X/Y button reports were observed.

Not yet proven in this witness:

- D-pad telemetry on Quest.
- Physical HOTAS movement.
- Broad Quest compatibility across devices or OS versions.
