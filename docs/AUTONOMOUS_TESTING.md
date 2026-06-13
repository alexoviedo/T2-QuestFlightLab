# Autonomous Testing

The v0.2 simulator loop is designed so most flight-core changes can be tested without Alex wearing the headset.

## Preferred Path

Run the deterministic Unity editor scenario suite:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_editor_scenario_tests.ps1
```

This launches Unity batchmode, executes `QuestFlightLab.Editor.FlightCoreBatchRunner.RunDefaultScenarios`, and writes JSON, CSV, markdown, and Unity logs to a setup artifact folder.

The current v0.5 suite covers preflight neutral initialization, before-takeoff checklist state, taxi/brake, takeoff roll to Vr, rotation/climb, Vy climb stabilization, shallow turns, rudder yaw, flap deployment, trim, slow-flight/stall warning onset, stall recovery, pattern heading-change placeholder, runway reset, Basic Traffic Pattern Familiarization, pattern phase progression, scoring/debrief, instrument/UI verification, lesson prompt update, airport gate/checkpoint verification, pattern reset/retry, stabilized final approach, unstable approach/go-around cases, speed/sink deviations, go-around sequencing, approach debrief generation, timeline/replay markers, pattern-to-final transition, touchdown placeholder, and reset after go-around.

Each scenario exports initial/final speed, altitude delta, heading change, pitch/bank extrema, stall warning count/onset, control ranges, instrument verification, training scaffold verification, airport reference verification, pattern debrief score, approach debrief score, stable-gate/go-around flags, replay marker counts, and timeline sample counts where applicable.

## Supplemental PlayMode Probe

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_unity_tests.ps1 -TestPlatform PlayMode
```

This verifies the deterministic input source and mapper path, traffic-pattern lesson/scoring support, approach scoring/timeline serialization, airport reference verification, and cockpit panel binding through Unity Test Runner XML evidence. The explicit editor scenario runner remains the primary simulator-core evidence because it exports richer flight telemetry.

## Meta XR Simulator

Meta XR Simulator was not detected in the Unity editor domain during the 2026-06-12 v0.2 run. If Meta XR Simulator is added later, it should be treated as a headset-interaction approximation, not proof of Quest hardware, Bluetooth, or USB2BLE behavior.

## What This Proves

- C# compiles in Unity batchmode.
- Deterministic gamepad-like controls can drive the same aircraft-control data path.
- The prototype flight model responds consistently across scripted scenarios.
- Scenario telemetry and pass/fail evidence can be generated without the headset.
- The named cockpit instrument panel objects, Basic Traffic Pattern Familiarization scaffold, Stabilized Approach + Go-Around scaffold, airport pattern/final references, debrief reports, and replay timeline path are present in the autonomous evidence path.

## What This Does Not Prove

- Real Quest runtime behavior.
- Quest Bluetooth pairing.
- USB2BLE hardware input.
- Broad Quest compatibility.
- Final C172 fidelity.
- FAA-approved training, BATD/AATD qualification, or pilot-training credit.
