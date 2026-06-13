# Training Mode Scaffold

v0.2 adds a small training/checklist scaffold for simulator structure. It is not a real curriculum and does not provide pilot-training credit.

## Current Lesson

`Basic Takeoff Familiarization`

Steps:

- Before takeoff checklist placeholder.
- Align on runway.
- Smooth throttle application.
- Maintain centerline with rudder.
- Rotate near placeholder Vr.
- Climb at placeholder Vy.
- Maintain heading and positive climb.

## Current Checklist

The before-takeoff checklist is represented by placeholder `ChecklistItem` data and a `ChecklistController`. Items are intentionally generic until real checklist sources and aircraft configuration choices are selected.

## Runtime Behavior

`TrainingModeController` can drive a lesson prompt area in the telemetry panel and evaluate basic simulator-state milestones. The current implementation is for scaffolding and automated-test visibility, not flight instruction.

## Future Work

- Replace placeholders with aircraft-specific checklist data from approved source documents.
- Add lesson scoring that distinguishes simulator task completion from real-world proficiency.
- Add replay/debrief records once flight-state logging is stable.
