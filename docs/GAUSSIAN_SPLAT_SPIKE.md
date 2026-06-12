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

Deferred during the 2026-06-12 build/device bring-up because Unity activation, Android build, ADB visibility, APK install, and runtime smoke are higher priority for v0.1. The fallback mesh/terrain path is the v0.1 implementation path.
