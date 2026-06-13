# Training Mode Scaffold

v0.5 expands the training scaffold from takeoff and traffic-pattern prompts into stabilized approach/go-around familiarization. It is not a real curriculum and does not provide pilot-training credit.

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

`Stabilized Approach + Go-Around Familiarization`

Phases:

- Downwind stabilized setup.
- Abeam touchdown / power reduction placeholder.
- Base turn.
- Final intercept.
- Stabilized approach gate.
- Continue-to-landing decision.
- Unstable approach warning.
- Go-around decision.
- Go-around power/pitch/configuration.
- Climb-out / rejoin upwind placeholder.
- Landing flare/touchdown placeholder.
- After-landing or reset.

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

The v0.5 autonomous scenario runner verifies that the Basic Traffic Pattern Familiarization and Stabilized Approach + Go-Around Familiarization lessons contain required phase ids and map scenario evidence to lesson phases.

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

`ApproachScoring` adds a second deterministic debrief path for stabilized approach/go-around work:

- total approach score,
- phase scores,
- stable-gate result,
- go-around required/initiated/correct flags,
- speed/altitude/descent/glide-path/centerline deviations,
- timeline sample and replay marker counts,
- warning and recommendation lists,
- JSON and Markdown export.

`FlightTimelineRecorder` exports replay-oriented JSON/CSV samples at deterministic intervals. Replay data is for debrief and regression inspection only; it is not a certified flight-recording system.

## Future Work

- Replace placeholders with aircraft-specific checklist data from approved source documents.
- Replace source-backed prototype criteria with aircraft-specific data and instructor-reviewed lesson logic.
- Add a visual replay player once timeline data is stable.
