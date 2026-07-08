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

## v0.4 Traffic Pattern Direction

Flight Sim Core v0.4 adds a more complete generated cockpit/training panel, Basic Traffic Pattern Familiarization scaffold, airport pattern gates/checkpoints, scored debrief reports, and 21-scenario autonomous evidence. This is still a deterministic training-quality prototype direction, not a validated instructional system.

## v0.5 Approach Direction

Flight Sim Core v0.5 adds source-backed stabilized approach/go-around prototype targets, a Stabilized Approach + Go-Around Familiarization lesson scaffold, final-approach airport gates, cockpit approach-status fields, replay timeline export, approach debrief scoring, and autonomous stable/unstable approach evidence. This improves the simulator seed without claiming aircraft-specific procedure fidelity or real-world training credit.

## v0.6 Scenery Spike Direction

Flight Sim Core v0.6 adds an optional scenery-provider abstraction and a bounded Gaussian splat feasibility harness. The result keeps mesh/terrain as the default scenery path and classifies true Unity/Quest Gaussian splats as deferred until a real renderer package, tiny compatible asset, Android build, and Quest runtime frame timing are proven.

## v0.6b Real Renderer Gate

Flight Sim Core v0.6b adds `aras-p/UnityGaussianSplatting` v1.1.1 as an isolated experimental renderer package, a renderer-compatible binary 3DGS sample generator path, and a real editor renderer smoke. Synthetic 5k, 50k, and 100k samples render in the Unity editor when forced to D3D12, and the Android APK builds with the package present. This is classified as `android_build_only`, not Quest splat viability, because no headset runtime splat render/frame timing was attempted.

## Claim Boundary

This prototype is not FAA-approved training, not a BATD/AATD, not pilot-training credit, not a broad Quest compatibility claim, and not final C172 fidelity.

## v0.9 Production Direction

The next direction is an evidence-gated fidelity pipeline, not more blind placeholder work:

- `visual_fidelity_demo` is the recommended safe demo alias while visual quality improves.
- JSBSim is the candidate serious flight-dynamics reference path, first as an offline oracle for Unity tests.
- Blender/OpenVSP form the preferred aircraft geometry path for owned C172-style cockpit/exterior assets.
- Poly Haven or similarly clear CC0 sources are preferred for PBR materials/HDRIs.
- Cesium/3D Tiles/geospatial work remains a research gate until Quest runtime, licensing, and offline/cache constraints are understood.
- Gaussian splats remain diagnostic or small-background candidates until Quest XR stereo/world-lock evidence improves.

## v1 Production Visual + Physics Direction

The v1 milestone keeps the previous imported C172 placeholder aircraft/cockpit because it was good enough for current iteration, then moves effort to the world and physics evidence:

- `visual_fidelity_demo` now builds a larger KBDU-inspired environment around the airport with terrain chunks, local roads/fields, perimeter cues, industrial buildings, reservoir/drainage hints, and far ridge scenery.
- A render-quality configurator applies MSAA/aniso/fog/lighting/camera defaults and records evidence for visual QA.
- Visual QA captures additional far-scenery and ground-detail shots.
- JSBSim comparison moves from a standalone probe to an offline Unity-scenario comparator.
- Unity runtime physics remains approximate; no JSBSim runtime backend, final C172 fidelity, or training suitability is claimed.

## v2 KBDU Environment + Physics Direction

The v2 milestone keeps the current imported C172 placeholder cockpit/aircraft and focuses on the world/physics weaknesses:

- `visual_fidelity_demo` now uses an 11.8 km x 11.8 km KBDU-inspired world with 121 terrain chunks and near/mid/far detail rings.
- OpenStreetMap/Overpass data was used as a non-committed reference for airport/road/building/water density; the committed scene remains procedural and approximate.
- Airport/runway detail includes denser taxi-lanes/connectors, ramp/hangar details, runway cracks/wear, field parcels, tree-line cues, roads, reservoir/drainage hints, and larger Front Range-style ridges.
- Render-quality evidence now records far clip, mipmap limit, target FPS, MSAA/aniso/LOD/shadow settings, and visual QA captures ramp/hangar plus final-approach distance-cue shots.
- Unity flight config was tuned modestly against the JSBSim comparison to reduce over-climb/over-acceleration and improve shallow-turn trend, while JSBSim remains an offline reference oracle.

## v2.1 Pilot Eye + Matched-Control Direction

The v2.1 milestone preserves the imported C172 placeholder and tightens the first-view and evidence loops:

- Default pilot eye for the imported C172 placeholder is now a project-owned reference config: seat local `(0.00, 0.72, 0.00)` m, default offset `(0.00, 0.22, 0.00)` m, resolved eye `(0.00, 0.94, 0.00)` m.
- `visual_fidelity_demo` expands the KBDU-inspired procedural world to 14.56 km x 14.56 km with 169 chunks, denser field/road/reservoir/ridge cues, and refreshed LOD/render-quality evidence.
- Matched-control JSBSim/Unity scenarios now compare takeoff, climb, turns, approach, and go-around profiles.
- The accepted runtime tune is intentionally small because more aggressive config-only tuning broke existing traffic-pattern and approach gates.
