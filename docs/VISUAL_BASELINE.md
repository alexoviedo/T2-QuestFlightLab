# Visual Baseline

## Goal

`playable_demo` / `playable_visual_baseline` should give Alex a readable, world-locked Quest flight-demo view even when experimental Gaussian splats are disabled.

This baseline is a prototype visual pass, not production photorealism, not a final C172 cockpit, and not a full-airport scenery system.

## Current v0.8 Baseline

Runtime systems:

- `QuestFirstViewRuntimeRepair`
  - locks the pilot view to the aircraft while preserving headset pose
  - hides solid placeholder fuselage/glass that blocked the pilot view
  - hides the original blocky aircraft visuals in playtest mode
  - adds a self-generated C172-style high-wing exterior with wing struts, tricycle gear, prop/spinner, real transparent cabin-window openings, and animated control surfaces
  - places the pilot viewpoint in the left seat instead of on the aircraft centerline
  - adds a self-generated C172-style cockpit with transparent windshield/side windows, windshield frame, glare shield, panel, gauge/radio/switch accents, seats, yokes, rudder pedals, throttle, mixture, carb heat, trim, and flap cues
- `FirstViewPlaytestDiagnostics`
  - records headset/camera pose diagnostics
  - writes runtime evidence JSON
  - captures a diagnostic mono camera PNG from the Quest scene when ADB/scrcpy headset mirrors are black
- `AirportRuntimeEnhancer`
  - adds self-generated runway surface detail, runway lights, runway numerals/stripes, taxiway signs, apron/hangars/fuel island, tree rows, and distant foothills
  - hides large training pattern gates/labels during playtest HUD mode so the default view is not cluttered by training scaffolding
- `PlaytestHud`
  - shows a compact HUD and hides verbose telemetry/menu/performance panels by default

## Recommended Launch

```powershell
.\scripts\launch_quest_playtest.ps1 -Mode playable_demo -CaptureLogcat -DurationSeconds 85
```

`playable_demo` starts the short deterministic demo pilot sequence automatically. `playable_visual_baseline` and `scenic_mesh_enhanced` are mesh/procedural visual-baseline aliases.

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

- Visuals are still procedural and low-detail compared with production scenery.
- Cockpit proportions, instruments, and exterior model are C172-style placeholders, not final C172 art/fidelity.
- No real airport capture or surveyed scenery alignment is claimed.
- Real Gaussian splats remain blocked in default Quest playtest mode until the XR stereo/composite path is fixed.
