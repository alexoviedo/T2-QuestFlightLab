# Stabilized Approach And Go-Around Prototype Targets

QuestFlightLab v0.5 uses these targets to guide deterministic approach/go-around scenarios. This is prototype training guidance only. It is not FAA-approved training, a POH replacement, legal pilot-training credit, or validated aircraft-specific C172 procedure data.

## Sources

- FAA, Airplane Flying Handbook, Chapter 8, "Airport Traffic Patterns": https://www.faa.gov/sites/faa.gov/files/regulations_policies/handbooks_manuals/aviation/airplane_handbook/09_afh_ch8.pdf
- FAA/FAASTeam, Airplane Flying Handbook, Chapter 9, "Approaches and Landings": https://www.faasafety.gov/files/events/SO/SO15/2024/SO15134233/ApproachesAndLandings10_afh_ch9a.pdf
- QuestFlightLab C172-style speed references: `docs/C172_REFERENCE_TARGETS.md`

## Traffic Pattern Reference

The FAA Airplane Flying Handbook describes rectangular traffic patterns with upwind, crosswind, downwind, base, and final legs. It also notes that standard traffic pattern turns are left unless markings or local procedures indicate otherwise, and that common pattern altitude is usually about 1,000 ft above the airport surface. Local airport publications, Chart Supplements, and operating procedures remain authoritative.

For v0.5, KBDU references remain approximate and not for navigation. The generated pattern is a visual/scoring scaffold, not an airport procedure trainer.

## Stabilized Approach Targets

The FAA approach and landing guidance describes a stabilized final approach as one with controlled pitch, power, trim, speed, configuration, descent path, and runway alignment. QuestFlightLab translates that into deterministic prototype criteria:

| Criterion | v0.5 prototype target |
| --- | --- |
| Glide path | Nominal 3-degree descent path, scored by vertical-speed/airspeed trend |
| Runway alignment | On extended centerline, centerline deviation target under 12 m |
| Final speed | 65 KIAS target, +10/-5 KIAS tolerance |
| Configuration | Landing flaps placeholder, trim set, checklist placeholder complete |
| Descent rate | About 500-1,000 fpm, target near 650 fpm |
| Bank on final | 15 degrees or less |
| Stable gate | 300 ft AGL prototype gate for light-GA deterministic scenarios |
| Decision | Go around when unstable at or below the gate |

The 300 ft AGL gate is a prototype threshold selected from FAA light-GA stabilized-approach discussion. It is intentionally conservative for the simulator harness and not an aircraft-specific rule.

## Go-Around Targets

The FAA go-around discussion emphasizes power, attitude, and configuration. QuestFlightLab v0.5 models that as:

1. Apply high power.
2. Set a safe climb pitch placeholder.
3. Maintain directional control and runway heading placeholder.
4. Retract flaps in stages rather than instantly.
5. Establish positive climb and rejoin the upwind placeholder.

The current C172-style trainer is fixed-gear, so gear sequencing is not modeled. Carb heat, mixture, and engine response remain placeholders.

## v0.5 Scenario Coverage

- Stabilized final approach pass case.
- High/unstable approach requiring go-around.
- Low/unstable approach requiring go-around.
- Excessive sink-rate warning requiring go-around.
- Final speed deviation scoring.
- Go-around power/pitch/configuration sequence.
- Pattern-to-final transition.
- Touchdown/landing placeholder if stable.
- Reset after go-around.
- Timeline and debrief export.

## Limitations

- Not aircraft-specific POH procedure data.
- Not for navigation or real KBDU operations.
- Not FAA-approved training, BATD/AATD qualification, or pilot-training credit.
- Not final C172 fidelity.
- Does not prove fresh Quest runtime behavior or USB2BLE physical input behavior.
