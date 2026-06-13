# Gaussian Splat Spike

## Goal

Determine whether a very small static Gaussian splat can render in Quest 3 standalone at useful frame rate and visual quality.

## Time Box

Maximum 60 minutes after the mesh/terrain fallback scene builds and runs.

## Pass Criteria

- Small `.ply` or `.spz` sample renders in standalone Quest.
- Frame timing remains stable enough for the simulator slice.
- Integration does not destabilize input telemetry or the fallback airport scene.

## Fail Criteria

- APK build breaks.
- Standalone frame rate is not acceptable.
- Splat rendering requires excessive package churn or large assets.

## Current Result

Deferred during the 2026-06-12 build/device bring-up because Unity activation, Android build, ADB visibility, APK install, and runtime smoke were higher priority for v0.1.

For the v0.2 flight-core chunk, splats remained deferred because the priority was autonomous simulator testing, C172-style dynamics, and build stability. No Unity/Quest splat package was added, no large assets were imported, and no splat viability claim is made.

The optimized mesh/terrain fallback is still the implementation path.

v0.3 did not attempt a splat package/sample because the priority was C172-style reference targets, instruments, training evidence, and a green Android build. Splats remain experimental and should be revisited only after the simulator core has stronger repeatable metrics.
