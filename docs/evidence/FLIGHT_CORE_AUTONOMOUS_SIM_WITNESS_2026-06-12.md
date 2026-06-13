# Flight Core Autonomous Simulator Witness - 2026-06-12

## Summary

Flight Sim Core v0.2 was tested through Unity batchmode without requiring the headset. The deterministic editor scenario runner passed 11 of 11 scenarios, and the supplemental Unity PlayMode probe passed 2 of 2 tests.

This evidence does not prove real Quest runtime behavior, Quest Bluetooth pairing, USB2BLE hardware input, broad Quest compatibility, final C172 fidelity, Gaussian splat viability, or training suitability.

## Environment

- Commit before this evidence commit: `6ecfecd8e9a2ad3c5e04e25dde0bca88960282b7`
- Unity: `6000.3.8f1`
- Test mode: Unity batchmode editor scenario runner with deterministic input fallback
- Meta XR Simulator: not detected
- Artifact root: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\flight_core_20260612_173102`

## Results

| Check | Result | Evidence |
| --- | --- | --- |
| Editor scenario runner | PASS, 11/11 scenarios | `flight_core_summary.md`, `scenario_results.json`, `scenario_results.csv` |
| Unity PlayMode probe | PASS, 2/2 tests | `unity_PlayMode_test_results.xml` |
| Android APK build | PASS | `build_quest_stdout.txt`, `build_android.log` |

## Scenarios

| Scenario | Result |
| --- | --- |
| Neutral controls | PASS |
| Control surface sweep | PASS |
| Taxi throttle/brake check | PASS |
| Takeoff roll | PASS |
| Rotation and climb | PASS |
| Shallow left/right turns | PASS |
| Rudder yaw response | PASS |
| Flap deployment effect | PASS |
| Trim effect placeholder | PASS |
| Stall approach warning | PASS |
| Runway reset | PASS |

## APK

- Path: `C:\Users\ovied\Dev\T2\T2-QuestFlightLab\QuestFlightLab\Builds\Android\QuestFlightLab-v0.1-dev.apk`
- Size: 111,718,479 bytes
- SHA256: `04119627DDB7704CC2738F844D06DFD08B97C434E2749934F1DAC7E7E2666ABB`

## Notes

- The editor scenario runner is the primary v0.2 autonomous simulator evidence path.
- Unity Test Runner EditMode discovery was not used as the primary witness because the explicit editor runner provides richer telemetry and avoids requiring a larger assembly-definition refactor.
- Hardware install/launch was not repeated in this chunk because the goal was autonomous simulator iteration without Alex wearing the headset.
