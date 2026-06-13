#!/usr/bin/env python3
"""Generate deterministic tiny synthetic PLY point clouds for the splat spike.

These files are budget/proxy fixtures only. They are not photogrammetry captures
and are not a production Gaussian splat asset pipeline.
"""

from __future__ import annotations

import argparse
import json
import math
import random
from pathlib import Path


def write_ply(path: Path, count: int, seed: int) -> dict:
    rng = random.Random(seed + count)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="ascii", newline="\n") as handle:
        handle.write("ply\n")
        handle.write("format ascii 1.0\n")
        handle.write("comment QuestFlightLab synthetic splat proxy sample; not a production capture\n")
        handle.write(f"element vertex {count}\n")
        handle.write("property float x\n")
        handle.write("property float y\n")
        handle.write("property float z\n")
        handle.write("property uchar red\n")
        handle.write("property uchar green\n")
        handle.write("property uchar blue\n")
        handle.write("property uchar alpha\n")
        handle.write("end_header\n")
        for index in range(count):
            t = index / max(1, count - 1)
            angle = t * math.tau * 31.0
            radius = 22.0 + 10.0 * math.sin(t * math.pi * 3.0)
            jitter = (rng.random() - 0.5) * 1.5
            x = math.cos(angle) * radius + jitter
            y = 2.0 + 10.0 * math.sin(t * math.pi) + (rng.random() - 0.5) * 0.8
            z = math.sin(angle) * radius + (rng.random() - 0.5) * 1.5
            red = int(80 + 130 * t)
            green = int(120 + 80 * (1.0 - t))
            blue = int(145 + 50 * math.sin(t * math.pi))
            alpha = 180
            handle.write(f"{x:.4f} {y:.4f} {z:.4f} {red} {green} {blue} {alpha}\n")

    size = path.stat().st_size
    return {
        "path": str(path),
        "count": count,
        "bytes": size,
        "megabytes": round(size / (1024 * 1024), 4),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--counts", nargs="+", type=int, default=[5000, 50000, 100000])
    parser.add_argument("--seed", type=int, default=172)
    args = parser.parse_args()

    output_dir = Path(args.output_dir)
    manifest = {
        "description": "QuestFlightLab synthetic Gaussian-splat proxy samples; not production captures.",
        "samples": [],
    }
    for count in args.counts:
        if count <= 0:
            raise ValueError(f"Invalid count: {count}")
        sample_path = output_dir / f"synthetic_splats_{count}.ply"
        manifest["samples"].append(write_ply(sample_path, count, args.seed))

    manifest_path = output_dir / "sample_manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps(manifest, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
