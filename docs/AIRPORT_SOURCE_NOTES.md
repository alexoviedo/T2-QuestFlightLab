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
