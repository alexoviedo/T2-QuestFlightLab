# Flight Model Notes

v0.2 still uses a prototype C172-style approximation, but the model is now structured enough to support deterministic scenario tests and future replacement with better aerodynamics data.

v0.3 adds a reference-target document and moves the runtime configuration toward named groups: `C172ReferenceSpeeds`, `AeroCoefficients`, `EnginePropModelConfig`, `ControlStabilityConfig`, `LandingGearConfig`, and `TrainingReferenceTargets`.

See `docs/C172_REFERENCE_TARGETS.md` for source links and target values.

## Approximate Seed Constants

- Mass: 1111 kg
- Wing area: 16.2 m2
- Wing span: 11.0 m
- Aspect ratio: 7.45
- Stall speed clean: 48 kt
- Stall speed landing: 40 kt
- Cruise placeholder: 105 kt
- Vx placeholder: 62 kt
- Rotation placeholder: 55 kt
- Vy placeholder: 74 kt
- Vfe placeholder: 85 kt
- Never exceed placeholder: 163 kt
- Max engine power placeholder: 180 hp
- Flap settings: 0, 10, 20, 30 deg

## v0.2/v0.3 Model Changes

- Unit helpers for knots, feet, horsepower, and meters-per-second conversions.
- Power/thrust placeholder with throttle, mixture, carb heat, RPM, and power telemetry.
- Lift curve, drag polar, induced drag, side-slip drag, flap lift/drag, and low-speed/stall-warning placeholders.
- Ground handling for runway friction, toe braking, lateral friction, nosewheel steering authority, and ground roll distance.
- Flaps and trim now affect telemetry and the physics approximation.
- Pitch and bank attitude stability limits prevent deterministic tests from passing with runaway attitudes.
- Stall warning is suppressed during ordinary ground roll and records warning count/onset in autonomous evidence.
- Vy climb, stall recovery, pattern heading-change, instrument, and checklist verification are now explicit scenario evidence.

## Acceptance Metrics

The autonomous scenario runner currently checks 14 scenarios:

- preflight neutral initialization
- before-takeoff checklist state
- taxi/brake check
- takeoff roll to Vr
- rotation and climb
- Vy climb stabilization
- shallow left/right turns
- rudder yaw response
- flap deployment effect
- trim nose-up/nose-down
- slow flight / stall warning onset
- stall recovery
- pattern leg heading-change placeholder
- runway reset

The 2026-06-12 v0.3 run passed all 14 editor scenarios. This proves deterministic simulator behavior in the Unity editor only. It does not prove final C172 fidelity, real Quest runtime behavior, USB2BLE hardware behavior, or training suitability.

## Needed For Serious Fidelity

- C172 POH-derived performance tables and configuration variants.
- Validated engine/propeller model, mixture/carb heat behavior, and RPM/manifold-power relationships.
- Better ground contact model with landing gear geometry, tire friction, braking, and crosswind effects.
- Validated stall, spin, slip/skid, flap, trim, and control-authority behavior.
