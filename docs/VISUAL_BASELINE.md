# Visual Baseline

## Goal

`visual_fidelity_demo` / `playable_demo` / `playable_visual_baseline` should give Alex a readable, world-locked Quest flight-demo view even when experimental Gaussian splats are disabled.

This baseline is a prototype visual pass, not production photorealism, not a final C172 cockpit, and not a full-airport scenery system.

## Current v0.9 Baseline

Runtime systems:

- `QuestFirstViewRuntimeRepair`
  - locks the pilot view to the aircraft while preserving headset pose
  - hides solid placeholder fuselage/glass that blocked the pilot view
  - hides the original blocky aircraft visuals in playtest mode
  - loads an imported C172 placeholder when available at `Resources/QuestFlightLab/ImportedAssets/Cessna172KogThorns/cessna172`
  - adds a self-generated C172-style high-wing exterior with wing struts, tricycle gear, prop/spinner, real transparent cabin-window openings, and animated control surfaces
  - places the pilot viewpoint in the left seat instead of on the aircraft centerline
  - adds a self-generated C172-style cockpit with transparent windshield/side windows, windshield frame, glare shield, panel, gauge/radio/switch accents, seats, yokes, rudder pedals, throttle, mixture, carb heat, trim, and flap cues
- `FirstViewPlaytestDiagnostics`
  - records headset/camera pose diagnostics
  - writes runtime evidence JSON
  - captures a diagnostic mono camera PNG from the Quest scene when ADB/scrcpy headset mirrors are black
- `AirportRuntimeEnhancer`
  - adds self-generated runway surface detail, rubber touchdown wear, asphalt patches, expansion joints, apron seams, tie-downs, cones, grass variation, runway lights, runway numerals/stripes, taxiway signs, apron/hangars/fuel island, tree rows, and distant foothills
  - hides large training pattern gates/labels during playtest HUD mode so the default view is not cluttered by training scaffolding
- `PlaytestHud`
  - shows a compact HUD and hides verbose telemetry/menu/performance panels by default
- `VisualQaBatchRunner`
  - captures deterministic editor screenshots/contact sheets for cockpit, HUD, runway, external aircraft, airport overview, scenic/fallback, demo flight, and viewpoint calibration without Quest hardware

## v1 Environment-Focused Upgrade

Alex confirmed the previous imported C172 placeholder cockpit/aircraft was good enough for now, so the v1 production visual pass intentionally keeps that aircraft path:

```text
Resources/QuestFlightLab/ImportedAssets/Cessna172KogThorns/cessna172
```

The current focus is the airport/world around it:

- `KbduInspiredWorldBuilder`
  - adds an 8.8 km x 7.8 km KBDU-inspired visual footprint around the existing runway scene
  - uses 81 low-cost terrain mesh chunks instead of a tiny flat ground plane
  - adds roads, field strips, airport perimeter fence cues, ramp parking stripes, drainage/reservoir cues, local industrial buildings, and far Front Range-inspired ridge impostors
  - hides older blocky placeholder foothills in playtest/HUD mode so the far-ridge system carries the skyline
- `QuestRenderQualityConfigurator`
  - applies the visual-demo render profile in runtime/visual QA
  - enables MSAA, anisotropic filtering, longer camera clip range, fog/ambient/sun tuning, and conservative shadow settings
- `VisualQaBatchRunner`
  - now captures terrain/far-scenery and ground-detail shots in addition to cockpit/runway/aircraft/HUD shots
  - records world-builder and render-quality status in JSON/Markdown reports

## Recommended Launch

```powershell
.\scripts\launch_quest_playtest.ps1 -Mode visual_fidelity_demo -CaptureLogcat -DurationSeconds 85
```

`visual_fidelity_demo` and `playable_demo` start the short deterministic demo pilot sequence automatically. `playable_visual_baseline` and `scenic_mesh_enhanced` are mesh/procedural visual-baseline aliases.

For no-headset visual inspection:

```powershell
.\scripts\run_visual_qa.ps1
```

## Splat Gate

Normal Quest playtest launches do not show the real Gaussian renderer unless a developer explicitly opts into diagnostics. If a real splat mode is requested on Quest XR, the app falls back to mesh/procedural scenery and records:

```text
Real Gaussian splat renderer disabled: XR stereo/world-lock check failed
```

Developer diagnostic override:

```powershell
.\scripts\launch_quest_playtest.ps1 -Mode scenic_splat_medium -DemoMode short_playtest -SplatDiagnostic -CaptureLogcat
```

Use the diagnostic override only for bounded renderer experiments. Do not use it as the default demo path until headset captures prove stereo/world-locked splats.

## Known Limitations

- Visuals are still placeholder quality compared with production scenery.
- The imported cockpit/aircraft remains a placeholder, not final C172 art/fidelity.
- The expanded environment is KBDU-inspired and not navigation-accurate.
- No real airport capture or surveyed scenery alignment is claimed.
- Real Gaussian splats remain blocked in default Quest playtest mode until the XR stereo/composite path is fixed.
- Editor visual QA does not prove Quest headset comfort, performance, or stereo rendering.

## v2 KBDU Environment + Render Pass

The v2 pass keeps the current imported C172 placeholder and deepens the world around it:

- footprint increases to 11.8 km x 11.8 km,
- terrain uses 121 mesh chunks with near/mid/far detail rings,
- OSM reference data informed denser taxiway/apron/building/road/water cues, but raw OSM data is not committed,
- ramp/hangar and final-approach visual QA shots were added,
- runway/taxiway/apron surfaces include more joints, cracks, shoulder wear, taxi-lane centerlines, parking/T markings, and procedural noisy materials,
- render-quality evidence now records far clip, mip limit, target FPS, and the active MSAA/aniso/LOD/shadow profile,
- visual QA reports world-budget metrics including renderer/mesh/triangle/material/texture counts.

Latest v2 visual QA artifact:

```text
C:\Users\ovied\Dev\T2\T2-QuestFlightLab-setup-artifacts\kbd_environment_physics_20260707_225453\after_visual_qa_final
```
