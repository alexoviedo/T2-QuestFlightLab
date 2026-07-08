# Flight Model Notes

v0.2 still uses a prototype C172-style approximation, but the model is now structured enough to support deterministic scenario tests and future replacement with better aerodynamics data.

v0.3 adds a reference-target document and moves the runtime configuration toward named groups: `C172ReferenceSpeeds`, `AeroCoefficients`, `EnginePropModelConfig`, `ControlStabilityConfig`, `LandingGearConfig`, and `TrainingReferenceTargets`.

v0.4 keeps the physics changes targeted: the deterministic pattern profile now supports a longer climb-out, shallower pattern turns, staged flap/power placeholders, and reset/retry evidence. It does not attempt a large aerodynamic rewrite.

v0.5 adds targeted support for approach/go-around training evidence: approach configuration now references the configured approach speed, go-around power with flaps gets a mild deterministic climb/pitch bias, and `TrainingReferenceTargets` includes stable-approach gate, final-approach speed, descent-rate, bank, glide-path, and go-around climb targets. This is still not a landing-gear, tire, flare, or aircraft-specific POH model.

v1 adds a JSBSim/Unity offline comparator and a conservative config retune. JSBSim is now used as a reference oracle for trend gaps, not as the Unity runtime physics backend. The current comparison shows Unity still accelerates/climbs faster than the open-loop JSBSim `c172x` probe and turns less aggressively in the shallow-turn scenario, so the next physics chunk should build matched-control JSBSim profiles before heavier tuning.

v2 makes another conservative JSBSim-informed tune while preserving the current runtime model architecture. Static/max thrust, clean lift/drag, induced drag, pitch damping/stability, and prototype pitch limits were adjusted to reduce the strongest over-climb/over-acceleration trend without breaking the existing 33-scenario suite. The open-loop comparison improved modestly, but Unity still accelerates/climbs faster than the JSBSim reference and turns less aggressively, so JSBSim remains an offline oracle rather than a runtime backend.

v2.1 adds matched-control JSBSim/Unity scenario twins for takeoff, climb, turns, approach, and go-around. The final accepted tune keeps the proven lift/drag/pitch/roll envelope and only slightly reduces static/max thrust, because more aggressive config-only tuning broke the traffic-pattern and stabilized-approach regression gates. The matched-control aggregate improved slightly from 142.14 to 141.99 weighted error, with airspeed RMSE improving from 26.9 kt to 26.7 kt. This is calibration progress, not final fidelity.

Quality Gate v1 does not retune Unity physics further because the JSBSim Editor bridge now runs successfully. Unity can invoke a Python JSBSim sidecar, import 451 samples, and apply 451 proxy poses in Editor. The matched-control comparator remains unchanged at 141.99 weighted error, which reinforces that the serious fidelity path should move from config-only Unity tuning toward an interactive JSBSim-driven Editor backend.

See `docs/C172_REFERENCE_TARGETS.md` for source links and target values.

## Approximate Seed Constants

- Mass: 1157 kg
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
- Final approach placeholder: 65 kt with +10/-5 kt tolerance
- Stable approach gate placeholder: 300 ft AGL
- Final descent-rate placeholder: about -650 fpm, accepted band about -300 to -1000 fpm
- Final bank limit placeholder: 15 deg
- Go-around climb response placeholder: high power, positive pitch, staged flap reduction

## v0.2/v0.3 Model Changes

- Unit helpers for knots, feet, horsepower, and meters-per-second conversions.
- Power/thrust placeholder with throttle, mixture, carb heat, RPM, and power telemetry.
- Lift curve, drag polar, induced drag, side-slip drag, flap lift/drag, and low-speed/stall-warning placeholders.
- Ground handling for runway friction, toe braking, lateral friction, nosewheel steering authority, and ground roll distance.
- Flaps and trim now affect telemetry and the physics approximation.
- Pitch and bank attitude stability limits prevent deterministic tests from passing with runaway attitudes.
- Stall warning is suppressed during ordinary ground roll and records warning count/onset in autonomous evidence.
- Vy climb, stall recovery, pattern heading-change, instrument, and checklist verification are now explicit scenario evidence.
- v0.5 adds approach reference-speed telemetry, final-approach scoring fields, go-around decision markers, and timeline/debrief exports.
- v1 modestly increases damping/stability and reduces prototype pitch/bank/rate caps to keep the trainer from feeling too light while JSBSim comparison work matures.
- v2 reduces thrust/pitch response further, increases induced drag slightly, and raises roll authority/bank limits enough to move shallow-turn response closer to the JSBSim trend while keeping editor scenarios green.
- v2.1 keeps the runtime model inside the green scenario envelope and reduces static/max thrust slightly from 3600/4100 N to 3550/4050 N. Attempts to increase roll/turn authority further produced traffic-pattern stall warnings and were backed out.
- Quality Gate v1 adds JSBSim sidecar bridge evidence and leaves runtime Unity physics unchanged.

## Acceptance Metrics

The v0.5 autonomous scenario runner checks the v0.4 traffic-pattern set plus stabilized approach/go-around scenarios. The v0.4 set was:

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
- Basic Traffic Pattern Familiarization
- traffic pattern phase progression
- traffic pattern scoring/debrief
- instrument/UI verification
- lesson panel prompt update
- airport gate/checkpoint verification
- pattern reset/retry

The 2026-06-12 v0.4 run passed all 21 editor scenarios. This proves deterministic simulator behavior in the Unity editor only. It does not prove final C172 fidelity, real Quest runtime behavior, USB2BLE hardware behavior, or training suitability.

The v0.5 suite adds stable final approach, high/low/fast/sink unstable approaches, go-around sequencing, approach debrief generation, replay timeline markers, approach-status instrument verification, pattern-to-final transition, touchdown placeholder, and reset after go-around. These are regression metrics for simulator iteration, not proof of real landing or go-around training quality.

## Needed For Serious Fidelity

- C172 POH-derived performance tables and configuration variants.
- Validated engine/propeller model, mixture/carb heat behavior, and RPM/manifold-power relationships.
- Better ground contact model with landing gear geometry, tire friction, braking, and crosswind effects.
- Validated stall, spin, slip/skid, flap, trim, and control-authority behavior.
- JSBSim runtime/bridge feasibility for deeper tuning, because matched-control config-only tuning now exposes limitations in the current prototype Unity physics model.
- A clear decision on whether JSBSim should drive editor-only reference testing, a desktop bridge, or a native Android runtime backend.
