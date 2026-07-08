# Production Fidelity Strategy

## Goal

Move Quest Flight Input Lab toward a credible standalone Quest 3 flight-simulator demo without pretending that the current prototype is already production photoreal, FAA/BATD/AATD suitable, final C172 fidelity, or broadly Quest compatible.

The working split is:

- **Playable demo now:** keep the mesh/procedural visual path stable, readable, and buildable.
- **Professional visual pipeline:** generate or import optimized aircraft, cockpit, airport, terrain, and material assets through reproducible tools.
- **High-fidelity physics path:** use JSBSim as the serious reference backend before replacing Unity runtime physics.
- **Evidence gates:** every major fidelity upgrade needs visual QA, scenario tests, PlayMode tests, and Android build evidence before it becomes the default.

## Tool Decisions

| Area | Decision | Why |
| --- | --- | --- |
| Flight dynamics | **Go: JSBSim as offline reference oracle first.** | JSBSim provides mature aircraft XML/property simulation and bundled C172-like definitions. The local probe runs, but open-loop control is not yet calibrated enough to replace Unity runtime physics. |
| Aircraft geometry | **Go: Blender procedural pipeline now; OpenVSP research/install next.** | Blender can generate owned Quest-light meshes and cockpit iterations quickly. OpenVSP is better for parametric aircraft geometry, but it is not installed locally yet. |
| Materials/HDRI | **Go: Poly Haven or equivalent CC0 sources.** | CC0 material/HDRI sources keep future redistribution clean and let the visual pass improve without committing unclear assets. |
| Real geospatial scenery | **Research only: Cesium for Unity / 3D Tiles.** | Cesium is a credible research path for terrain and 3D Tiles, but streaming photoreal world data is not automatically Quest-safe or license-simple. |
| Gaussian splats | **Diagnostic only unless stereo/world-lock is proven.** | Headset evidence showed one-eye/headset-locked behavior. Splat modes may remain useful for small static patches, but they must not ruin the playable mode. |
| Quest runtime | **Keep build stability sacred.** | No heavy package or asset import becomes default until visual QA, scenario tests, PlayMode tests, and APK build pass. |

Primary references:

- JSBSim: https://jsbsim-team.github.io/jsbsim/ and https://github.com/JSBSim-Team/jsbsim
- OpenVSP: https://openvsp.org/
- Cesium for Unity: https://cesium.com/learn/unity/
- Poly Haven license: https://polyhaven.com/license

## Recommended Modes

- `visual_fidelity_demo`: recommended production-direction visual demo alias. Uses the safe mesh/procedural baseline, short demo-pilot motion, clean HUD, and cockpit calibration path.
- `playable_demo`: existing short playtest demo alias, still valid.
- `scenic_splat_medium`: experimental/diagnostic until Quest XR stereo/world-lock is fixed.

## Immediate Implementation Priorities

1. Use `scripts/run_visual_qa.ps1` on every visual iteration and inspect the contact sheet before touching Quest hardware.
2. Keep improving self-generated/Blender-generated C172-style cockpit and exterior geometry, with transparent windows, correct left-seat view, yokes, pedals, panel, glare shield, seats, wing/cowl references, and realistic scale.
3. Add small CC0/optimized PBR materials for asphalt, concrete, cockpit plastic, glass, painted metal, grass, dirt, and hangars.
4. Use JSBSim probe outputs as an offline oracle for expected C172-like trends: takeoff roll, climb, pitch/bank/heading response, approach, and go-around.
5. Treat OpenVSP as the next aircraft geometry gate: parametric high-wing trainer exterior, export validation, Unity import sizing, and visual QA comparison.
6. Keep geospatial/Cesium work in a research branch or isolated proof until Quest memory, runtime, licensing, and offline operation are understood.

## Go / No-Go Gates

**Go to default playable demo only if:**

- visual QA screenshots are nonblank and readable,
- cockpit, aircraft exterior, runway, airport, HUD, demo motion, and calibration views pass sanity checks,
- editor scenarios pass,
- PlayMode tests pass,
- APK build passes,
- Quest runtime evidence exists for any hardware-specific claim.

**No-go for default demo if:**

- the asset requires login/runtime network access,
- it breaks APK build,
- it creates one-eye/headset-locked XR output,
- it commits huge raw downloads,
- it has unknown runtime cost on Quest,
- it makes the first view harder to understand.

## Current Classification

As of the production-direction witness, the app has a stable autonomous visual QA loop and a safer visual baseline, but it still does not have production art, final C172 geometry, final flight dynamics, or Quest-proven photoreal scenery. The next best chunk is an asset-pipeline sprint: install/use Blender or OpenVSP, generate an owned high-wing trainer/cockpit asset, optimize it for Unity/Quest, and compare it against the current placeholder through visual QA.
