# Roadmap

## v0.1 Quest Flight Input Lab

- Standalone Quest 3 Unity project.
- Unity Input System `Gamepad` telemetry.
- USB2BLE Xbox persona mapping.
- Simple C172-style trainer and approximate small airport scene.
- Evidence logging, FPS/performance HUD, build/deploy scripts.

## Next Milestones

1. Iterate the v1 expanded KBDU-inspired environment: improve terrain/material realism, add optimized CC0 PBR materials/HDRI, refine far scenery, and capture Quest runtime frame timing.
2. Build matched-control JSBSim profiles for the same Unity editor scenarios, then tune Unity flight behavior against reference envelopes.
3. Establish an owned/OpenVSP/Blender aircraft asset path when the environment baseline is convincing enough to revisit cockpit/exterior art.
4. Expand cockpit/instrument layout from placeholders toward inspectable analog/G1000-style training views.
5. Tighten stabilized approach, landing flare, and go-around behavior with aircraft-specific data and better instructor/debrief review.
6. Use splats only for small static scenic/background patches after Quest XR stereo/world-lock evidence; keep mesh/procedural scenery as the default playable path.
7. Add sailplane aerotow, thermals, ridge lift, and towplane operations.
8. Add dynamic wind/weather.
9. Add instructor station, multiplayer, logbook, airport database, and lesson syllabus.
