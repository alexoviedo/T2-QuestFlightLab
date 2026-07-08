# Cockpit Viewpoint Calibration

The playtest cockpit view supports a small persisted seat/viewpoint adjustment so a player can align their VR eye point with the C172 placeholder cockpit.

## Runtime Controls

In Quest playtest mode:

- `A`: open seat adjustment mode; press again to save and exit.
- left stick: move left/right and forward/back.
- right stick up/down: move the pilot eye higher/lower.
- right stick left/right: adjust cockpit yaw.
- `X`: recenter forward.
- `B`: reset offset/yaw while adjustment is active.
- grip + sticks: quick adjust without staying in seat mode.
- both grips: fine adjustment scale.

The playtest HUD shows the current seat offset as:

```text
SEAT X ... Y ... Z ... YAW ...
```

## Persistence

Calibration is stored as JSON through `CockpitViewpointPersistence` at:

```text
Application.persistentDataPath\QuestFlightLab\seat_calibration\seat_calibration_current.json
```

The JSON includes:

- schema version,
- generated timestamp,
- scenery/demo mode,
- imported C172 cockpit model eye,
- pilot view offset,
- cockpit yaw,
- resolved pilot eye local position,
- instructions.

## Automated Verification

`QuestFlightLabPlayModeTests.CockpitViewpointPersistenceSavesLoadsAndResetsCurrentCalibration` verifies save, load, value round-trip, and reset/delete behavior.

`scripts\run_visual_qa.ps1` also performs a persistence probe and records the result in `visual_qa_report.json` and `visual_qa_summary.md`.

## Limits

This calibration aligns the current placeholder cockpit. It is not a final C172 seating model, not proof of real Quest comfort, and not a substitute for future project-owned cockpit geometry.
