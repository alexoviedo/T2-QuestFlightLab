# Cockpit viewpoint calibration

The imported C172 uses an explicit aircraft-relative XR hierarchy:

```text
AircraftSimulationRoot / center of gravity
  AircraftVisualRoot
    PilotSeatAnchor
      UserViewCalibrationOffset
        XR Origin
          Camera Offset
            Main Camera (OpenXR tracked pose only)
          LeftHand Controller
          RightHand Controller
```

Aircraft motion owns the simulation root. The fixed pilot seat and the saved calibration are children of the visual aircraft. OpenXR alone owns the Main Camera local pose; calibration code never writes that tracked transform and the headset is never an aircraft pivot.

## Default pilot eye

The imported placeholder's left-seat reference is:

```text
PilotSeatAnchor local position: (-0.28, 0.94, 0.00) m
Default calibration offset:     ( 0.00, 0.00, 0.00) m
```

This resolves from the model-specific seat reference `(-0.28, 0.72, 0.00)` plus the default eye-height offset `(0.00, 0.22, 0.00)`. Calibration adjusts only `UserViewCalibrationOffset` and does not move the model or simulation root.

Startup alignment is armed as soon as the seat hierarchy exists, before the cockpit's asynchronous model load. It waits for a valid, tracked, user-present HMD pose and ten consecutive stable pose frames, then recenters exactly once by moving only the XR Origin inside the calibrated seat frame. This prevents a slow model load from exposing a stale room-scale origin as the initial cockpit pose. The tracked Main Camera is never written. Runtime instrumentation records the pending/completed state, successful recenter count, and measured position/yaw error after alignment; final headset confirmation of this path is still pending because the latest relaunch was blocked by Quest's controller-required dialog before Unity started.

## Runtime controls

Open the in-cockpit panel with the left Touch Menu button. Opening it does not pause the simulation and does not consume Xbox, HOTAS, rudder, throttle, or other flight axes.

- Left stick: left/right and forward/back.
- Right stick: up/down and calibration yaw.
- Either grip: small-step mode; released is large-step mode.
- X: recenter the tracking-space baseline inside the current seat frame.
- Y: restore the aircraft default as the current draft.
- A: save and close.
- B or Left Menu: cancel and restore the complete pre-edit draft, including XR-origin local pose.
- Editor fallback: C opens/cancels; Enter saves; Escape cancels; Home recenters; Backspace restores default.

Offsets are clamped to a realistic adjustment envelope and yaw is limited to a small correction. A failed save leaves the panel open and preserves the draft for retry.

## Persistence

Schema 5 stores only the versioned, per-aircraft calibration translation and yaw:

```text
Application.persistentDataPath/QuestFlightLab/seat_calibration/imported_c172/seat_calibration_current.json
```

Tracked head pose, XR-origin pose, model alignment, scenery, and diagnostic fields are not persisted. Legacy records migrate translation but reset the old cockpit-model yaw convention rather than mirroring it into the new user-relative frame. Reset removes the current record and restores the known aircraft default.

## Verification and limits

PlayMode coverage includes seat-frame invariance, synthetic head motion, origin-only seat recentering, stable-pose gating, aircraft inheritance, single-backend authority, Touch bindings, controller pose drivers, save/reload/reset, cancel rollback, and save-failure behavior. Visual QA captures both the default and calibrated view and the open panel.

The calibration is specific to the current imported placeholder. It is not a certified C172 eye-point model, a comfort guarantee, or evidence of training suitability. Controller bindings still require a manual Touch-controller check on the target headset.
