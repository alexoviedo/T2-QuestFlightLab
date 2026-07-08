# Roadmap

## v0.1 Quest Flight Input Lab

- Standalone Quest 3 Unity project.
- Unity Input System `Gamepad` telemetry.
- USB2BLE Xbox persona mapping.
- Simple C172-style trainer and approximate small airport scene.
- Evidence logging, FPS/performance HUD, build/deploy scripts.

## Next Milestones

1. Establish the production fidelity pipeline: JSBSim reference oracle, Blender/OpenVSP aircraft asset generation, CC0 material sourcing, geospatial research gates, visual QA evidence, and APK build stability.
2. Replace prototype aero/engine placeholders with better C172/C152 data, JSBSim-backed reference envelopes, and validated acceptance metrics.
3. Replace placeholder cockpit/exterior visuals with an optimized generated/imported C172-style aircraft asset that passes visual QA and Quest build gates.
4. Expand cockpit/instrument layout from generated placeholders toward inspectable analog/G1000-style training views.
5. Tighten stabilized approach, landing flare, and go-around behavior with aircraft-specific data and better instructor/debrief review.
6. Use splats only for small static scenic/background patches after Quest XR stereo/world-lock evidence; keep mesh/procedural scenery as the default playable path.
7. Add sailplane aerotow, thermals, ridge lift, and towplane operations.
8. Add dynamic wind/weather.
9. Add instructor station, multiplayer, logbook, airport database, and lesson syllabus.
