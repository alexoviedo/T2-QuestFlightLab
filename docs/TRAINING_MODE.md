# Training Mode Scaffold

v0.4 expands the training scaffold from takeoff-only prompts into a Basic Traffic Pattern Familiarization prototype. It is not a real curriculum and does not provide pilot-training credit.

## Current Lesson

`Basic Traffic Pattern Familiarization`

Phases:

- Pre-takeoff checklist.
- Line up on Runway 08/26 centerline.
- Takeoff roll.
- Rotate near placeholder Vr.
- Climb out / upwind.
- Crosswind turn.
- Downwind level-off / configure.
- Abeam touchdown point / power reduction placeholder.
- Base turn.
- Final approach alignment.
- Flare/touchdown or go-around placeholder.
- After-landing/reset.

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

The v0.4 autonomous scenario runner verifies that the Basic Traffic Pattern Familiarization lesson contains all required phase ids and maps scenario evidence to pattern phases.

## Scoring And Debrief

`LessonScoring` creates a deterministic prototype debrief with:

- total score,
- phase scores,
- gate hits,
- checklist misses,
- heading/speed/altitude/bank deviations,
- stall-warning counts,
- warning list,
- JSON and Markdown export.

The score is useful for regression testing and simulator iteration. It is not a measure of real pilot proficiency.

## Future Work

- Replace placeholders with aircraft-specific checklist data from approved source documents.
- Add replay/debrief records once flight-state logging is stable.
- Add source-backed normal-pattern procedures, stabilized approach criteria, go-around criteria, and instructor/replay review.
