# Roadmap

## v0.1 Quest Flight Input Lab

- Standalone Quest 3 Unity project.
- Unity Input System `Gamepad` telemetry.
- USB2BLE Xbox persona mapping.
- Simple C172-style trainer and approximate small airport scene.
- Evidence logging, FPS/performance HUD, build/deploy scripts.

## Next Milestones

1. Replace prototype aero/engine placeholders with better C172/C152 data and validated acceptance metrics.
2. Expand cockpit/instrument layout from generated text placeholders toward inspectable analog/G1000-style training views.
3. Tighten stabilized approach, landing flare, and go-around behavior with aircraft-specific data and better instructor/debrief review.
4. Run a Quest 3 runtime Gaussian splat smoke with the now-integrated real renderer, a tiny committed-or-generated test asset, explicit mesh fallback toggle, logcat, screenshot, and frame timing before expanding any scenic budget.
5. Add sailplane aerotow, thermals, ridge lift, and towplane operations.
6. Add dynamic wind/weather.
7. Add instructor station, multiplayer, logbook, airport database, and lesson syllabus.
