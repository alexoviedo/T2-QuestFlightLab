# Flight Model Notes

v0.2 still uses a prototype C172-style approximation, but the model is now structured enough to support deterministic scenario tests and future replacement with better aerodynamics data.

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

## v0.2 Model Changes

- Unit helpers for knots, feet, horsepower, and meters-per-second conversions.
- Power/thrust placeholder with throttle, mixture, carb heat, RPM, and power telemetry.
- Lift curve, drag polar, induced drag, side-slip drag, flap lift/drag, and low-speed/stall-warning placeholders.
- Ground handling for runway friction, toe braking, lateral friction, nosewheel steering authority, and ground roll distance.
- Flaps and trim now affect telemetry and the physics approximation.
- Pitch and bank attitude stability limits prevent deterministic tests from passing with runaway attitudes.

## Acceptance Metrics

The autonomous scenario runner currently checks 11 scenarios:

- neutral controls
- control surface sweep
- taxi throttle/brake check
- takeoff roll
- rotation and climb
- shallow left/right turns
- rudder yaw response
- flap deployment effect
- trim effect placeholder
- stall approach warning
- runway reset

The 2026-06-12 run passed all 11 editor scenarios. This proves deterministic simulator behavior in the Unity editor only. It does not prove final C172 fidelity, real Quest runtime behavior, USB2BLE hardware behavior, or training suitability.

## Needed For Serious Fidelity

- C172 POH-derived performance tables and configuration variants.
- Validated engine/propeller model, mixture/carb heat behavior, and RPM/manifold-power relationships.
- Better ground contact model with landing gear geometry, tire friction, braking, and crosswind effects.
- Validated stall, spin, slip/skid, flap, trim, and control-authority behavior.
