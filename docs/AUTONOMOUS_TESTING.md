# Autonomous Testing

The v0.2 simulator loop is designed so most flight-core changes can be tested without Alex wearing the headset.

## Preferred Path

Run the deterministic Unity editor scenario suite:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_editor_scenario_tests.ps1
```

This launches Unity batchmode, executes `QuestFlightLab.Editor.FlightCoreBatchRunner.RunDefaultScenarios`, and writes JSON, CSV, markdown, and Unity logs to a setup artifact folder.

The current suite covers neutral controls, control sweep, taxi/brake, takeoff roll, rotation/climb, shallow turns, rudder yaw, flap deployment, trim, stall warning, and runway reset.

## Supplemental PlayMode Probe

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_unity_tests.ps1 -TestPlatform PlayMode
```

This verifies the deterministic input source and mapper path through Unity Test Runner XML evidence. The explicit editor scenario runner remains the primary simulator-core evidence because it exports richer flight telemetry.

## Meta XR Simulator

Meta XR Simulator was not detected in the Unity editor domain during the 2026-06-12 v0.2 run. If Meta XR Simulator is added later, it should be treated as a headset-interaction approximation, not proof of Quest hardware, Bluetooth, or USB2BLE behavior.

## What This Proves

- C# compiles in Unity batchmode.
- Deterministic gamepad-like controls can drive the same aircraft-control data path.
- The prototype flight model responds consistently across scripted scenarios.
- Scenario telemetry and pass/fail evidence can be generated without the headset.

## What This Does Not Prove

- Real Quest runtime behavior.
- Quest Bluetooth pairing.
- USB2BLE hardware input.
- Broad Quest compatibility.
- Final C172 fidelity.
- FAA-approved training, BATD/AATD qualification, or pilot-training credit.
