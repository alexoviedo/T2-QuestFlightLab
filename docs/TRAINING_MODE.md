# Training Mode Scaffold

v0.2 adds a small training/checklist scaffold for simulator structure. It is not a real curriculum and does not provide pilot-training credit.

## Current Lesson

`Basic Takeoff Familiarization`

Steps:

- Complete before-takeoff checklist placeholders.
- Line up on Runway 08/26 centerline.
- Smooth full-throttle application.
- Maintain centerline with rudder.
- Rotate near placeholder Vr.
- Climb near placeholder Vy.
- Maintain runway heading and positive climb.
- After-takeoff cleanup placeholder.

## Current Checklist

The before-takeoff checklist is represented by placeholder `ChecklistItem` data and a `ChecklistController`. Items are intentionally generic until real checklist sources and aircraft configuration choices are selected.

Current placeholder items:

- Flight controls free and correct.
- Elevator trim set for takeoff.
- Flaps set for takeoff.
- Mixture rich.
- Carb heat cold.
- Engine instruments in green.
- Doors/windows secure.
- Runway heading and departure plan briefed.

## Runtime Behavior

`TrainingModeController` can drive a lesson prompt area in the telemetry panel and evaluate basic simulator-state milestones. The current implementation is for scaffolding and automated-test visibility, not flight instruction.

The v0.3 autonomous scenario runner verifies that the Basic Takeoff Familiarization lesson contains all required step ids and maps takeoff-related scenarios to the relevant lesson steps.

## Future Work

- Replace placeholders with aircraft-specific checklist data from approved source documents.
- Add lesson scoring that distinguishes simulator task completion from real-world proficiency.
- Add replay/debrief records once flight-state logging is stable.
