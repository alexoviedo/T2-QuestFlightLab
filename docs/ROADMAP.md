# Roadmap

## v0.1 Quest Flight Input Lab

- Standalone Quest 3 Unity project.
- Unity Input System `Gamepad` telemetry.
- USB2BLE Xbox persona mapping.
- Simple C172-style trainer and approximate small airport scene.
- Evidence logging, FPS/performance HUD, build/deploy scripts.

## Next Milestones

1. Iterate the v2 KBDU-inspired environment with optimized CC0 PBR materials/HDRI, better vegetation/field/road variation, and a Quest runtime frame-time smoke.
2. Build matched-control JSBSim profiles for the same Unity editor scenarios, then tune Unity flight behavior against reference envelopes.
3. Add a real terrain/elevation source gate, likely USGS 3DEP-derived offline height data, while keeping the Quest runtime local and bounded.
4. Establish an owned/OpenVSP/Blender aircraft asset path when the environment baseline is convincing enough to revisit cockpit/exterior art.
5. Expand cockpit/instrument layout from placeholders toward inspectable analog/G1000-style training views.
6. Tighten stabilized approach, landing flare, and go-around behavior with aircraft-specific data and better instructor/debrief review.
7. Use splats only for small static scenic/background patches after Quest XR stereo/world-lock evidence; keep mesh/procedural scenery as the default playable path.
8. Add sailplane aerotow, thermals, ridge lift, and towplane operations.
9. Add dynamic wind/weather.
10. Add instructor station, multiplayer, logbook, airport database, and lesson syllabus.
