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
import struct
from pathlib import Path


def write_proxy_ascii_ply(path: Path, count: int, seed: int) -> dict:
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
        "schema": "proxy_ascii_xyz_rgba",
    }


def write_unity_3dgs_binary_ply(path: Path, count: int, seed: int) -> dict:
    rng = random.Random(seed + count)
    path.parent.mkdir(parents=True, exist_ok=True)
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
    with path.open("wb") as handle:
        header = [
            "ply",
            "format binary_little_endian 1.0",
            "comment QuestFlightLab synthetic 3DGS smoke sample; not a production capture",
            f"element vertex {count}",
        ]
        header.extend(f"property float {name}" for name in properties)
        header.append("end_header")
        handle.write(("\n".join(header) + "\n").encode("ascii"))

        for index in range(count):
            t = index / max(1, count - 1)
            angle = t * math.tau * 31.0
            radius = 5.0 + 4.0 * math.sin(t * math.pi * 3.0)
            x = math.cos(angle) * radius + (rng.random() - 0.5) * 0.25
            y = 1.5 + 3.0 * math.sin(t * math.pi) + (rng.random() - 0.5) * 0.2
            z = math.sin(angle) * radius + (rng.random() - 0.5) * 0.25
            nx = ny = nz = 0.0

            red = 0.4 + 0.35 * t
            green = 0.55 + 0.25 * (1.0 - t)
            blue = 0.65 + 0.15 * math.sin(t * math.pi)
            sh0 = 0.2820948
            f_dc_0 = (red - 0.5) / sh0
            f_dc_1 = (green - 0.5) / sh0
            f_dc_2 = (blue - 0.5) / sh0
            opacity = 2.2
            log_scale = math.log(0.06 + 0.03 * rng.random())
            scale_0 = log_scale
            scale_1 = log_scale
            scale_2 = log_scale
            rot_0 = 1.0
            rot_1 = rot_2 = rot_3 = 0.0

            handle.write(
                struct.pack(
                    "<17f",
                    x,
                    y,
                    z,
                    nx,
                    ny,
                    nz,
                    f_dc_0,
                    f_dc_1,
                    f_dc_2,
                    opacity,
                    scale_0,
                    scale_1,
                    scale_2,
                    rot_0,
                    rot_1,
                    rot_2,
                    rot_3,
                )
            )

    size = path.stat().st_size
    return {
        "path": str(path),
        "count": count,
        "bytes": size,
        "megabytes": round(size / (1024 * 1024), 4),
        "schema": "unity_3dgs_binary_little_endian",
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--counts", nargs="+", type=int, default=[5000, 50000, 100000])
    parser.add_argument("--seed", type=int, default=172)
    parser.add_argument(
        "--schema",
        choices=["proxy-ascii", "unity-3dgs-binary"],
        default="proxy-ascii",
        help="proxy-ascii is the v0.6 budget fixture; unity-3dgs-binary is renderer-compatible with UnityGaussianSplatting.",
    )
    args = parser.parse_args()

    output_dir = Path(args.output_dir)
    manifest = {
        "description": "QuestFlightLab synthetic Gaussian-splat samples; not production captures.",
        "schema": args.schema,
        "samples": [],
    }
    for count in args.counts:
        if count <= 0:
            raise ValueError(f"Invalid count: {count}")
        sample_path = output_dir / f"synthetic_splats_{count}.ply"
        if args.schema == "unity-3dgs-binary":
            manifest["samples"].append(write_unity_3dgs_binary_ply(sample_path, count, args.seed))
        else:
            manifest["samples"].append(write_proxy_ascii_ply(sample_path, count, args.seed))

    manifest_path = output_dir / "sample_manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps(manifest, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
