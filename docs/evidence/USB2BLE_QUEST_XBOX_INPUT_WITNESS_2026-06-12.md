# USB2BLE Quest Xbox Input Witness - 2026-06-12

## Scope

This witness covers USB2BLE's Xbox BLE-compatible persona paired to one Quest 3 and observed by Quest Flight Input Lab through Unity Input System `Gamepad` telemetry. It does not claim broad Quest compatibility, physical HOTAS movement, Xbox console support, FAA/training suitability, or final C172 fidelity.

## Environment

- Repo state under test: `e707b12399c6b198da5d09e0129c9edbc3262ec2` plus the local `androidResizeableActivity=false` Quest launch patch
- Unity: `6000.3.8f1`
- Quest: Quest 3, product/device `eureka`, ADB serial `2G0YC5ZG8907TD`
- Package: `com.alexoviedo.t2.questflightlab`
- Activity: `com.unity3d.player.UnityPlayerGameActivity`
- APK SHA256: `BDF4452200DE3D2E0C852909FD28B1FCFB03DA2936C0D127E202F8C71DC35F12`
- USB2BLE serial port: `COM3`, `USB-Enhanced-SERIAL CH343`
- USB2BLE persona: `xbox_wireless_controller`
- BLE identity strategy: `persona_static_random_experimental`
- BLE address reported by target: `CB:B3:AE:FA:FC:EF`
- Quest Bluetooth name paired by Alex: `Xbox Wireless Controller`

## Manual Actions

- Alex confirmed the QuestFlightLab app was visible after accepting the Quest launch prompt.
- Windows had previously claimed the USB2BLE Xbox pairing; an elevated Windows device removal helper was approved to stop Windows from immediately reconnecting.
- Alex paired the Quest 3 Bluetooth entry named `Xbox Wireless Controller`.

## Input Scenarios

The first sequence used USB2BLE virtual Flight Pack input through the Xbox BLE bridge:

- neutral
- left stick right/left
- left stick forward/back
- right stick X rudder left/right
- left trigger and right trigger toe-brake tests

The second sequence used direct Xbox test reports:

- neutral
- A/B/X/Y button reports

D-pad test scenario names returned `ERROR:Generic` from the current firmware command set in this run, so D-pad Quest telemetry is not claimed here.

## Result

PASS for Unity Input System Xbox gamepad detection and deterministic USB2BLE input telemetry on this one Quest 3.

App evidence reduction from `session_20260612_223655.json`:

- Total app samples: `5375`
- Connected gamepad samples: `2318`
- Active input samples: `146`
- Unity display name: `Xbox Wireless Controller`
- Unity layout: `XboxOneGamepadAndroid`
- Unity device id: `25`
- Button samples: South `9`, East `7`, West `7`, North `8`
- D-pad active samples: `0`

Observed gamepad/control ranges:

| Field | Min | Max |
| --- | ---: | ---: |
| leftStickX | -1.0 | 1.0 |
| leftStickY | -1.0 | 1.0 |
| rightStickX | -1.0 | 1.0 |
| leftTrigger | 0.0 | 1.0 |
| rightTrigger | 0.0 | 1.0 |
| aileron | -1.0 | 1.0 |
| elevator | -1.0 | 1.0 |
| rudder | -1.0 | 1.0 |
| leftToeBrake | 0.0 | 1.0 |
| rightToeBrake | 0.0 | 1.0 |

The app evidence proves the Quest app saw the Xbox-style gamepad and mapped its axes/triggers/buttons into the aircraft control-state telemetry. The control-surface visual movement was not separately witnessed during the virtual input sequence.

## Artifacts

Artifact root:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\runtime_smoke_20260612_162235\usb2ble_quest_xbox_input
```

Key files:

- `serial_state_after_quest_pair_connected.txt`
- `serial_virtual_input_sequence.txt`
- `serial_xbox_button_test_sequence.txt`
- `serial_cleanup_after_virtual_sequence.txt`
- `quest_logcat_virtual_input_sequence.txt`
- `quest_logcat_xbox_button_test_sequence.txt`
- `evidence_after_button_sequence_pause/evidence/session_20260612_223655.json`
- `evidence_reduction_summary_after_buttons.json`
- `evidence_axis_control_stats_after_buttons.json`
- `evidence_active_input_samples_after_buttons.csv`

## Limitations

- Single Quest 3 and one local USB2BLE target only.
- Bluetooth pairing required manual headset action.
- Windows pairing cleanup required UAC because Windows had already claimed the same USB2BLE Xbox identity.
- Physical HOTAS movement was not used; inputs were deterministic/virtual USB2BLE Xbox reports.
- D-pad was not proven on Quest in this run.
- No broad Quest/USB2BLE compatibility claim is made from this witness.
