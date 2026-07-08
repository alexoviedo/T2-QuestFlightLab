# Cockpit Viewpoint Calibration

The playtest cockpit view supports a small persisted seat/viewpoint adjustment so a player can align their VR eye point with the C172 placeholder cockpit.

## Default Pilot Eye Reference

The default view should be usable before any player calibration. For the imported C172 placeholder, the v2.1 default reference is:

```text
Seat reference local: (0.00, 0.72, 0.00) m
Default pilot-eye offset: (0.00, 0.22, 0.00) m
Resolved default pilot eye: (0.00, 0.94, 0.00) m
```

Calibration offsets are additive to this reference. They should not be used to compensate for a bad default seated view.

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
- imported C172 seat reference,
- imported C172 default pilot-eye offset,
- pilot view offset,
- cockpit yaw,
- resolved pilot eye local position,
- instructions.

## Automated Verification

`QuestFlightLabPlayModeTests.CockpitViewpointPersistenceSavesLoadsAndResetsCurrentCalibration` verifies save, load, value round-trip, and reset/delete behavior.

`scripts\run_visual_qa.ps1` also performs a persistence probe and records the result in `visual_qa_report.json` and `visual_qa_summary.md`.

## Limits

This calibration aligns the current placeholder cockpit. It is not a final C172 seating model, not proof of real Quest comfort, and not a substitute for future project-owned cockpit geometry.
