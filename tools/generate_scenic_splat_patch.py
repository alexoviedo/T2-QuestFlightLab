#!/usr/bin/env python3
"""Generate a deterministic procedural scenic Gaussian-splat patch.

The output is a project-owned visual proxy for the playable demo. It is not a
photogrammetry capture, not a real KBDU scan, and not a production scenery
pipeline. The PLY schema matches the UnityGaussianSplatting importer path used
by the Quest runtime splat gate.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import random
import struct
from pathlib import Path


SH0 = 0.2820948


def clamp(value: float, low: float, high: float) -> float:
    return max(low, min(high, value))


def dc(value: float) -> float:
    return (clamp(value, 0.0, 1.0) - 0.5) / SH0


def terrain_height(x: float, z: float) -> float:
    foothill = 18.0 * math.exp(-((z - 290.0) ** 2) / 21000.0)
    ridge = 30.0 * math.exp(-((z - 415.0) ** 2) / 16000.0)
    undulation = 2.2 * math.sin(x * 0.018) + 1.5 * math.sin(z * 0.021)
    return max(0.0, foothill + ridge + undulation)


def choose_region(index: int, count: int, rng: random.Random) -> str:
    t = index / max(1, count - 1)
    if t < 0.48:
        return "foothills"
    if t < 0.70:
        return "field"
    if t < 0.84:
        return "treeline"
    if t < 0.95:
        return "airport_edges"
    return "atmospheric_edge"


def gaussian_for_region(region: str, rng: random.Random) -> tuple[float, float, float, float, float, float, float, float]:
    if region == "foothills":
        x = rng.uniform(-280.0, 280.0)
        z = rng.uniform(185.0, 470.0)
        y = terrain_height(x, z) + rng.uniform(-1.5, 8.0)
        slope = clamp((z - 185.0) / 285.0, 0.0, 1.0)
        r = 0.36 + 0.18 * slope + rng.uniform(-0.04, 0.04)
        g = 0.43 + 0.14 * (1.0 - slope) + rng.uniform(-0.04, 0.04)
        b = 0.34 + 0.07 * math.sin(x * 0.03) + rng.uniform(-0.03, 0.03)
        radius = rng.uniform(1.6, 3.8)
        opacity = rng.uniform(1.05, 1.75)
    elif region == "field":
        x = rng.uniform(-190.0, 190.0)
        z = rng.uniform(45.0, 300.0)
        runway_clear = abs(x) < 34.0 and z < 210.0
        if runway_clear:
            x += math.copysign(rng.uniform(38.0, 82.0), x if abs(x) > 0.1 else rng.choice([-1.0, 1.0]))
        y = 0.15 + 0.035 * terrain_height(x, z) + rng.uniform(-0.15, 0.7)
        dry = rng.random()
        r = 0.44 + 0.15 * dry
        g = 0.50 + 0.19 * (1.0 - dry)
        b = 0.30 + 0.05 * rng.random()
        radius = rng.uniform(0.75, 2.2)
        opacity = rng.uniform(0.85, 1.45)
    elif region == "treeline":
        side = rng.choice([-1.0, 1.0])
        x = side * rng.uniform(85.0, 175.0) + rng.uniform(-10.0, 10.0)
        z = rng.uniform(65.0, 265.0)
        y = rng.uniform(2.0, 13.0) + 0.04 * terrain_height(x, z)
        r = 0.16 + rng.uniform(-0.03, 0.04)
        g = 0.31 + rng.uniform(-0.06, 0.08)
        b = 0.17 + rng.uniform(-0.04, 0.04)
        radius = rng.uniform(0.8, 2.5)
        opacity = rng.uniform(1.2, 2.2)
    elif region == "airport_edges":
        side = rng.choice([-1.0, 1.0])
        x = side * rng.uniform(58.0, 145.0)
        z = rng.uniform(38.0, 150.0)
        y = rng.uniform(1.2, 10.0)
        material = rng.random()
        if material < 0.45:
            r, g, b = 0.54, 0.56, 0.54
        elif material < 0.75:
            r, g, b = 0.47, 0.40, 0.33
        else:
            r, g, b = 0.26, 0.34, 0.42
        r += rng.uniform(-0.04, 0.04)
        g += rng.uniform(-0.04, 0.04)
        b += rng.uniform(-0.04, 0.04)
        radius = rng.uniform(0.65, 1.8)
        opacity = rng.uniform(1.2, 2.0)
    else:
        x = rng.uniform(-305.0, 305.0)
        z = rng.uniform(315.0, 520.0)
        y = terrain_height(x, z) + rng.uniform(8.0, 26.0)
        r, g, b = 0.58 + rng.uniform(-0.04, 0.04), 0.62 + rng.uniform(-0.03, 0.04), 0.68 + rng.uniform(-0.03, 0.04)
        radius = rng.uniform(2.0, 5.0)
        opacity = rng.uniform(0.45, 0.95)

    return x, y, z, r, g, b, radius, opacity


def write_unity_3dgs_binary_ply(path: Path, count: int, seed: int, label: str) -> dict:
    rng = random.Random(seed + count * 17)
    properties = [
        "x",
        "y",
        "z",
        "nx",
        "ny",
        "nz",
        "f_dc_0",
        "f_dc_1",
        "f_dc_2",
        "opacity",
        "scale_0",
        "scale_1",
        "scale_2",
        "rot_0",
        "rot_1",
        "rot_2",
        "rot_3",
    ]
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("wb") as handle:
        header = [
            "ply",
            "format binary_little_endian 1.0",
            "comment QuestFlightLab procedural scenic airfield splat proxy; synthetic project-owned asset",
            f"comment profile {label}",
            f"element vertex {count}",
        ]
        header.extend(f"property float {name}" for name in properties)
        header.append("end_header")
        handle.write(("\n".join(header) + "\n").encode("ascii"))

        for index in range(count):
            region = choose_region(index, count, rng)
            x, y, z, red, green, blue, radius, opacity = gaussian_for_region(region, rng)
            anisotropy = rng.uniform(0.72, 1.35)
            vertical_scale = radius * rng.uniform(0.45, 0.95)
            if region in {"treeline", "airport_edges"}:
                vertical_scale = radius * rng.uniform(0.95, 1.75)

            handle.write(
                struct.pack(
                    "<17f",
                    x,
                    y,
                    z,
                    0.0,
                    0.0,
                    0.0,
                    dc(red),
                    dc(green),
                    dc(blue),
                    opacity,
                    math.log(radius * anisotropy),
                    math.log(vertical_scale),
                    math.log(radius / max(0.4, anisotropy)),
                    1.0,
                    0.0,
                    0.0,
                    0.0,
                )
            )

    digest = hashlib.sha256(path.read_bytes()).hexdigest().upper()
    size = path.stat().st_size
    return {
        "path": str(path),
        "label": label,
        "count": count,
        "bytes": size,
        "megabytes": round(size / (1024 * 1024), 4),
        "sha256": digest,
        "schema": "unity_3dgs_binary_little_endian",
        "source": "QuestFlightLab procedural generator; synthetic/project-owned",
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--seed", type=int, default=1722026)
    parser.add_argument("--low-count", type=int, default=25000)
    parser.add_argument("--medium-count", type=int, default=50000)
    parser.add_argument("--high-count", type=int, default=100000)
    args = parser.parse_args()

    output_dir = Path(args.output_dir)
    profiles = [
        ("low", args.low_count, "scenic_airfield_low_25000.ply"),
        ("medium", args.medium_count, "scenic_airfield_medium_50000.ply"),
        ("high", args.high_count, "scenic_airfield_high_100000.ply"),
    ]
    manifest = {
        "description": "Procedural airfield/foothills scenic splat patch for QuestFlightLab playable demo. Not a real-world capture.",
        "seed": args.seed,
        "samples": [],
    }

    for label, count, filename in profiles:
        if count <= 0:
            raise ValueError(f"Invalid {label} count: {count}")
        manifest["samples"].append(write_unity_3dgs_binary_ply(output_dir / filename, count, args.seed, label))

    manifest_path = output_dir / "scenic_splat_manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps(manifest, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
