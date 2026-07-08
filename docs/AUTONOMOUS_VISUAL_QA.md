# Autonomous Visual QA

This project now has a no-headset visual feedback path for Codex/local development.

## Primary Path

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run_visual_qa.ps1
```

This launches Unity batchmode and executes `QuestFlightLab.Editor.VisualQaBatchRunner.RunVisualQa`. It builds a disposable editor scene, loads the current airport/scenery systems, loads the imported C172 placeholder, positions deterministic cameras, captures screenshots, runs simple image sanity checks, and writes a report.

Outputs include:

- `screenshots\*.png`
- `visual_qa_contact_sheet.png`
- `visual_qa_report.json`
- `visual_qa_report.csv`
- `visual_qa_summary.md`
- `visual_qa_analysis.json`
- `visual_qa_analysis.md`
- `unity_visual_qa.log`

## Captured Views

The current required shot set is:

1. cockpit pilot view, mesh fallback
2. cockpit pilot view, scenic splat medium or fallback
3. instrument/HUD readability close view
4. runway centerline/takeoff view
5. external aircraft view
6. airport overview
7. splat scenic patch view or explicit fallback
8. demo-pilot takeoff/climb moment
9. demo-pilot shallow turn
10. viewpoint calibration UI/state view

## What It Checks

The checks are intentionally fail-fast, not aesthetic judgment:

- screenshot dimensions are valid,
- image is not blank or solid color,
- HUD-colored pixels do not dominate HUD-enabled frames,
- cockpit/imported C172 asset exists,
- runway geometry exists,
- camera is not accidentally left at the world origin,
- demo-pilot aircraft pose changes over time,
- cockpit viewpoint persistence saves, loads, and resets.

## Simulator Status

Meta XR Simulator was not detected on the local machine during the 2026-07 visual harness run. Unity XR Interaction Toolkit simulator package/settings were detected, but the deterministic editor camera harness is the primary path because it works in batchmode without Quest hardware, Touch controllers, ESP32, or a headset.

## What This Proves

- Codex can inspect visual output locally through generated screenshots and a contact sheet.
- The app can render cockpit, HUD, runway, airport, external aircraft, scenic/fallback, demo, and calibration views in the Unity editor.
- Obvious blank/solid/giant-overlay failures can be detected automatically.
- Cockpit viewpoint persistence has automated coverage.

## What This Does Not Prove

- Real Quest headset comfort, stereo rendering, or performance.
- Broad Quest compatibility.
- USB2BLE or physical HOTAS behavior.
- Production photorealism.
- Final C172 fidelity.
- FAA/BATD/AATD/training suitability.
- Quest XR Gaussian splats are stereo/world-locked.
