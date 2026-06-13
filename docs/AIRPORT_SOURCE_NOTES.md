# Airport Source Notes

## Initial Field

v0.1 uses Boulder Municipal Airport (`KBDU` / `BDU`) as an approximate generated small-airport reference.

Planning references:

- City of Boulder airport page: https://bouldercolorado.gov/government/departments/airport
- AirNav KBDU summary: https://www.airnav.com/airport/KBDU

## Approximation Boundary

The scene represents powered Runway 08/26, an apron, a taxiway, a windsock, and simple VFR pattern cues. It is not a chart, not for navigation, and not an operational airport model.

## v0.2 Environment Pass

The generated mesh fallback remains the implementation path. Runtime helpers add clearer runway 08/26 markings, threshold bars, edge lines, hold-short bars, airport/debug labels, windsock, apron/taxiway geometry, traffic-pattern markers, and a runway reset/start reference.

v0.3 adds a runtime toggle component for debug labels so training runs can hide or show approximate airport labels. The layout is approximate and intentionally lightweight. It is suitable for simulator iteration and input/flight-state testing, not for navigation or airport procedure training.

## v0.4 Pattern Training References

v0.4 adds lightweight generated references for the Basic Traffic Pattern Familiarization scaffold:

- upwind, crosswind, downwind, abeam, base, and final gates,
- pattern-altitude band placeholder,
- touchdown-zone marker,
- approach-path/PAPI-style placeholder,
- pattern-box boundary markers,
- airport label noting the scene is approximate and not for navigation.

These objects support autonomous verification and visual orientation only. They are not charted KBDU geometry and are not suitable for real airport procedures.

## v0.5 Approach And Go-Around References

v0.5 adds lightweight generated references for stabilized approach/go-around scenarios:

- extended centerline for Runway 08,
- outer, middle, and 300 ft AGL placeholder final approach gates,
- touchdown-zone marker,
- go-around/climb-out gate,
- PAPI/VASI-style placeholder,
- existing downwind/base/final checkpoints.

These references support deterministic scoring, cockpit instrumentation checks, and debrief evidence. They are approximate visual aids only, not surveyed runway geometry, not a chart, and not for navigation or real KBDU procedure training.

## v0.6 Scenery Fallback And Splat Spike

v0.6 adds an optional scenery-provider abstraction so future scenic rendering experiments can be isolated from the airport/training geometry. The default `MeshFallback` provider still uses the generated KBDU mesh/terrain scene and remains the only validated scenery path for the simulator slice.

The Gaussian splat path is experimental, off by default, and currently classified as deferred for true Quest rendering. The v0.6 synthetic PLY samples are artifact-only budget/proxy fixtures, not airport source data, not surveyed scenery, and not a replacement for the mesh fallback.

v0.6b adds a real Unity Gaussian splat renderer package and artifact-only synthetic renderer samples. These samples are abstract point-cloud smoke tests, not KBDU scenery captures, not airport source data, and not suitable for navigation or procedure training. The approximate mesh airport remains the default validated environment.
