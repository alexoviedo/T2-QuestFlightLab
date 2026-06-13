# Scenery And Splats

## Default Airport

The v0.1 scene uses an approximate, generated Boulder Municipal Airport (`KBDU`) reference:

- Powered runway: 08/26
- Approximate paved dimensions: 4100 ft x 75 ft
- Scene includes a simplified runway, taxiway, apron, windsock, pattern markers, and surrounding terrain.

The scene is not for navigation and does not import copyrighted scenery.

## Gaussian Splat Direction

Gaussian splats are treated as an experimental visual path, not a simulator dependency. The shipping fallback is optimized generated mesh/terrain and a simple sky.

v0.6 adds an optional scenery-provider abstraction and a batchmode spike harness, but no production splat renderer package is installed or committed. The current classification is `defer_to_later`: synthetic PLY budget/proxy samples were generated outside the repo, mesh fallback stayed green, PlayMode passed, and the Android APK built.

Future splat work should happen in an isolated branch/worktree with a real Unity renderer package, a tiny compatible PLY/SPZ asset, Android build validation, Quest runtime frame timing, and the mesh fallback toggle still available. Meta Spatial SDK splats, SPZ compression, and LOD/chunking remain research items, not current simulator dependencies.
