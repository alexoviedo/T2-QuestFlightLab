# Scenery And Splats

## Default Airport

The v0.1 scene uses an approximate, generated Boulder Municipal Airport (`KBDU`) reference:

- Powered runway: 08/26
- Approximate paved dimensions: 4100 ft x 75 ft
- Scene includes a simplified runway, taxiway, apron, windsock, pattern markers, and surrounding terrain.

The scene is not for navigation and does not import copyrighted scenery.

## Gaussian Splat Direction

Gaussian splats are treated as an experimental visual path, not a simulator dependency. The shipping fallback is optimized generated mesh/terrain and a simple sky.

v0.6 adds an optional scenery-provider abstraction and a batchmode spike harness. v0.6b adds the real `aras-p/UnityGaussianSplatting` v1.1.1 package as an experimental renderer path. Synthetic renderer-compatible PLY samples were generated outside the repo, 5k/50k/100k samples rendered in the Unity editor with D3D12, mesh fallback stayed green, PlayMode passed, and the Android APK built with the package present.

The current classification is `android_build_only`, not Quest splat viability. Future splat work should run a Quest 3 runtime smoke with a tiny compatible PLY/SPZ asset, frame timing, logcat, screenshot evidence, and the mesh fallback toggle still available. Meta Spatial SDK splats, SPZ compression, and LOD/chunking remain research items, not current simulator dependencies.
