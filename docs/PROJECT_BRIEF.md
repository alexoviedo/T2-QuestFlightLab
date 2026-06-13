# Project Brief

## Goal

Build the first vertical slice of a standalone Meta Quest 3 native flight simulator that can test USB2BLE as a BLE Xbox/gamepad controller and serve as the seed for a serious C172-style flight-training simulator direction.

## v0.1 Definition

Quest Flight Input Lab v0.1 focuses on one proof path:

- Quest standalone APK built from Unity.
- Unity Input System detects an Xbox-style gamepad.
- USB2BLE Xbox practical mapping drives aircraft controls.
- A simple C172-style trainer moves around a small airport scene.
- Live telemetry and evidence logs prove input behavior.
- A Gaussian splat feasibility note exists, but optimized mesh/terrain remains the fallback.

## v0.2 Flight Core Direction

Flight Sim Core v0.2 shifts the next proof path from headset bring-up to autonomous simulator iteration:

- Deterministic Unity editor scenarios exercise neutral controls, control sweeps, taxi, takeoff roll, rotation/climb, turns, rudder, flaps, trim, stall warning, and runway reset.
- A PlayMode probe verifies the deterministic gamepad source can drive the same mapper used by USB2BLE/Xbox input.
- The C172-style physics are still approximate, but now include explicit unit conversions, power, mixture/carb heat placeholders, lift/drag parameters, ground roll/braking, flaps/trim effects, and pitch/bank stability limits.
- The training scaffold exposes a first Basic Takeoff Familiarization sequence and before-takeoff checklist placeholders.

## v0.3 Fidelity Direction

Flight Model Fidelity v0.3 adds public C172-style reference targets, a more data-driven config, richer autonomous scenarios, named cockpit/instrument verification, stall warning/recovery evidence, and a stronger Basic Takeoff Familiarization scaffold.

## Claim Boundary

This prototype is not FAA-approved training, not a BATD/AATD, not pilot-training credit, not a broad Quest compatibility claim, and not final C172 fidelity.
