#!/usr/bin/env python3
"""Compare JSBSim probe telemetry against Unity scenario evidence.

This is an offline calibration aid. It does not prove final C172 fidelity and
does not replace Unity runtime physics with JSBSim.
"""

from __future__ import annotations

import argparse
import csv
import json
import pathlib
from dataclasses import asdict, dataclass


@dataclass
class ComparisonRow:
    phase: str
    jsbsim_metric: str
    jsbsim_value: float
    unity_scenario: str
    unity_metric: str
    unity_value: float
    delta: float
    interpretation: str


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--jsbsim-json", required=True)
    parser.add_argument("--jsbsim-csv", required=True)
    parser.add_argument("--unity-scenario-csv", required=True)
    parser.add_argument("--output-dir", required=True)
    args = parser.parse_args()

    out = pathlib.Path(args.output_dir)
    out.mkdir(parents=True, exist_ok=True)
    js_report = json.loads(pathlib.Path(args.jsbsim_json).read_text(encoding="utf-8"))
    js_samples = read_csv(pathlib.Path(args.jsbsim_csv))
    unity = {row["id"]: row for row in read_csv(pathlib.Path(args.unity_scenario_csv))}

    rows = [
        compare(
            "takeoff roll speed",
            max_value(js_samples, "airspeed_kt", phase="takeoff-roll"),
            "takeoff_roll_to_vr",
            float(unity["takeoff_roll_to_vr"]["final_airspeed_kts"]),
            "JSBSim max airspeed during open-loop takeoff-roll",
            "Unity final airspeed in takeoff-roll scenario",
        ),
        compare(
            "rotation/climb altitude",
            max_value(js_samples, "agl_ft", phase="rotate/climb"),
            "rotation_climb_to_altitude",
            float(unity["rotation_climb_to_altitude"]["altitude_delta_ft"]),
            "JSBSim max AGL during open-loop rotate/climb",
            "Unity altitude delta in rotation/climb scenario",
        ),
        compare(
            "shallow turn bank",
            max_abs(js_samples, "bank_deg", phases={"shallow-left-turn", "shallow-right-turn"}),
            "shallow_left_right_turns",
            max(abs(float(unity["shallow_left_right_turns"]["min_bank_deg"])), abs(float(unity["shallow_left_right_turns"]["max_bank_deg"]))),
            "JSBSim max absolute bank in turn phases",
            "Unity max absolute bank in shallow-turn scenario",
        ),
        compare(
            "approach speed",
            average_value(js_samples, "airspeed_kt", phase="approach-preview"),
            "stabilized_final_approach",
            float(unity["stabilized_final_approach"]["final_airspeed_kts"]),
            "JSBSim average airspeed during open-loop approach-preview",
            "Unity final airspeed in stabilized approach pass scenario",
        ),
        compare(
            "go-around speed",
            average_value(js_samples, "airspeed_kt", phase="go-around-placeholder"),
            "go_around_sequence",
            float(unity["go_around_sequence"]["final_airspeed_kts"]),
            "JSBSim average airspeed during open-loop go-around-placeholder",
            "Unity final airspeed in go-around scenario",
        ),
    ]

    report = {
        "tool": "JSBSim/Unity offline comparator",
        "jsbsim_aircraft": js_report.get("aircraft", ""),
        "jsbsim_probe": str(pathlib.Path(args.jsbsim_json)),
        "unity_scenarios": str(pathlib.Path(args.unity_scenario_csv)),
        "classification": "reference_oracle_only",
        "rows": [asdict(row) for row in rows],
        "recommendations": [
            "Use the comparison to find trend mismatches, not pass/fail fidelity.",
            "Tune Unity runtime conservatively; the current JSBSim profile is open-loop and not a calibrated autopilot.",
            "Next step is matched-control JSBSim profiles for the same Unity scenario definitions.",
        ],
        "limitations": [
            "JSBSim and Unity profiles do not yet use identical control scripts.",
            "No Android/Quest JSBSim runtime integration is proven.",
            "This does not claim final C172 fidelity or training suitability.",
        ],
    }
    (out / "jsbsim_unity_comparison.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    write_markdown(out / "jsbsim_unity_comparison.md", report)
    print(json.dumps(report, indent=2))
    return 0


def read_csv(path: pathlib.Path) -> list[dict[str, str]]:
    with path.open(newline="", encoding="utf-8") as handle:
        return list(csv.DictReader(handle))


def phase_rows(samples: list[dict[str, str]], phase: str | None = None, phases: set[str] | None = None) -> list[dict[str, str]]:
    if phase is not None:
        return [row for row in samples if row.get("phase") == phase]
    if phases is not None:
        return [row for row in samples if row.get("phase") in phases]
    return samples


def max_value(samples: list[dict[str, str]], key: str, phase: str | None = None) -> float:
    rows = phase_rows(samples, phase=phase)
    return max((float(row[key]) for row in rows), default=0.0)


def max_abs(samples: list[dict[str, str]], key: str, phases: set[str]) -> float:
    rows = phase_rows(samples, phases=phases)
    return max((abs(float(row[key])) for row in rows), default=0.0)


def average_value(samples: list[dict[str, str]], key: str, phase: str) -> float:
    rows = phase_rows(samples, phase=phase)
    values = [float(row[key]) for row in rows]
    return sum(values) / len(values) if values else 0.0


def compare(
    phase: str,
    js_value: float,
    unity_id: str,
    unity_value: float,
    js_metric: str,
    unity_metric: str,
) -> ComparisonRow:
    delta = unity_value - js_value
    ratio = abs(delta) / max(1.0, abs(js_value))
    if ratio < 0.2:
        interpretation = "roughly similar trend magnitude"
    elif unity_value > js_value:
        interpretation = "Unity higher than current JSBSim open-loop reference; inspect lift/thrust/control profile before further tuning"
    else:
        interpretation = "Unity lower than current JSBSim open-loop reference; inspect drag/thrust/control profile before further tuning"
    return ComparisonRow(phase, js_metric, js_value, unity_id, unity_metric, unity_value, delta, interpretation)


def write_markdown(path: pathlib.Path, report: dict) -> None:
    lines = [
        "# JSBSim / Unity Calibration Comparison",
        "",
        f"- Classification: `{report['classification']}`",
        f"- JSBSim aircraft: `{report['jsbsim_aircraft']}`",
        "",
        "| Phase | JSBSim | Unity | Delta | Interpretation |",
        "| --- | ---: | ---: | ---: | --- |",
    ]
    for row in report["rows"]:
        lines.append(
            f"| {row['phase']} | {row['jsbsim_value']:.1f} | {row['unity_value']:.1f} | {row['delta']:.1f} | {row['interpretation']} |"
        )
    lines.extend(["", "## Recommendations", ""])
    lines.extend(f"- {item}" for item in report["recommendations"])
    lines.extend(["", "## Limitations", ""])
    lines.extend(f"- {item}" for item in report["limitations"])
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


if __name__ == "__main__":
    raise SystemExit(main())
