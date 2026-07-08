#!/usr/bin/env python3
"""Lightweight PNG sanity checks for QuestFlightLab visual QA artifacts.

This intentionally avoids Pillow so the visual harness works on Alex's PC
without installing Python packages.
"""

from __future__ import annotations

import argparse
import json
import math
import pathlib
import struct
import sys
import zlib


def read_png_rgba(path: pathlib.Path) -> tuple[int, int, bytes]:
    data = path.read_bytes()
    if not data.startswith(b"\x89PNG\r\n\x1a\n"):
        raise ValueError("not a PNG")

    pos = 8
    width = height = None
    color_type = None
    bit_depth = None
    compressed = bytearray()

    while pos + 8 <= len(data):
        length = struct.unpack(">I", data[pos : pos + 4])[0]
        chunk_type = data[pos + 4 : pos + 8]
        chunk = data[pos + 8 : pos + 8 + length]
        pos += 12 + length

        if chunk_type == b"IHDR":
            width, height, bit_depth, color_type = struct.unpack(">IIBB", chunk[:10])[:4]
        elif chunk_type == b"IDAT":
            compressed.extend(chunk)
        elif chunk_type == b"IEND":
            break

    if width is None or height is None:
        raise ValueError("missing IHDR")
    if bit_depth != 8 or color_type not in (2, 6):
        raise ValueError(f"unsupported PNG format bit_depth={bit_depth} color_type={color_type}")

    raw = zlib.decompress(bytes(compressed))
    channels = 4 if color_type == 6 else 3
    stride = width * channels
    rows: list[bytes] = []
    prev = bytearray(stride)
    offset = 0
    for _ in range(height):
        filter_type = raw[offset]
        offset += 1
        scanline = bytearray(raw[offset : offset + stride])
        offset += stride
        recon = bytearray(stride)
        for i, value in enumerate(scanline):
            left = recon[i - channels] if i >= channels else 0
            up = prev[i]
            up_left = prev[i - channels] if i >= channels else 0
            if filter_type == 0:
                recon[i] = value
            elif filter_type == 1:
                recon[i] = (value + left) & 0xFF
            elif filter_type == 2:
                recon[i] = (value + up) & 0xFF
            elif filter_type == 3:
                recon[i] = (value + ((left + up) >> 1)) & 0xFF
            elif filter_type == 4:
                recon[i] = (value + paeth(left, up, up_left)) & 0xFF
            else:
                raise ValueError(f"unsupported PNG filter {filter_type}")
        rows.append(bytes(recon))
        prev = recon

    if channels == 4:
        rgba = b"".join(rows)
    else:
        rgba_bytes = bytearray(width * height * 4)
        dest = 0
        for row in rows:
            for i in range(0, len(row), 3):
                rgba_bytes[dest : dest + 4] = row[i : i + 3] + b"\xff"
                dest += 4
        rgba = bytes(rgba_bytes)

    return width, height, rgba


def paeth(a: int, b: int, c: int) -> int:
    p = a + b - c
    pa = abs(p - a)
    pb = abs(p - b)
    pc = abs(p - c)
    if pa <= pb and pa <= pc:
        return a
    if pb <= pc:
        return b
    return c


def analyze_png(path: pathlib.Path) -> dict:
    width, height, rgba = read_png_rgba(path)
    pixel_count = max(1, width * height)
    bg = rgba[-4:-1]
    mean = [0.0, 0.0, 0.0]
    non_bg = 0
    green_text = 0

    for i in range(0, len(rgba), 4):
        r, g, b = rgba[i], rgba[i + 1], rgba[i + 2]
        mean[0] += r
        mean[1] += g
        mean[2] += b
        if abs(r - bg[0]) + abs(g - bg[1]) + abs(b - bg[2]) > 36:
            non_bg += 1
        if g > 130 and r < 150 and b > 80:
            green_text += 1

    mean = [v / pixel_count for v in mean]
    variance = 0.0
    for i in range(0, len(rgba), 4):
        variance += (rgba[i] - mean[0]) ** 2
        variance += (rgba[i + 1] - mean[1]) ** 2
        variance += (rgba[i + 2] - mean[2]) ** 2
    variance /= pixel_count * 3

    return {
        "path": str(path),
        "width": width,
        "height": height,
        "color_variance": variance,
        "non_background_ratio": non_bg / pixel_count,
        "green_text_ratio": green_text / pixel_count,
        "image_not_blank": variance > 18 and non_bg / pixel_count > 0.035,
        "giant_ui_overlay_detected": green_text / pixel_count > 0.16,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, help="Visual QA artifact directory")
    parser.add_argument("--fail-on-errors", action="store_true")
    args = parser.parse_args()

    root = pathlib.Path(args.input)
    report_path = root / "visual_qa_report.json"
    screenshots = sorted((root / "screenshots").glob("*.png"))
    checks = []
    errors: list[str] = []

    if report_path.exists():
        report = json.loads(report_path.read_text(encoding="utf-8-sig"))
    else:
        report = {}
        errors.append(f"missing report: {report_path}")

    hud_by_name = {}
    for shot in report.get("shots", []):
        shot_path = pathlib.Path(shot.get("screenshotPath", ""))
        if shot_path.name:
            hud_by_name[shot_path.name] = bool(shot.get("hudVisible", False))

    if len(screenshots) < 10:
        errors.append(f"expected at least 10 screenshots, found {len(screenshots)}")

    for screenshot in screenshots:
        try:
            stats = analyze_png(screenshot)
            checks.append(stats)
            if not stats["image_not_blank"]:
                errors.append(f"blank/solid-looking screenshot: {screenshot.name}")
            if not hud_by_name.get(screenshot.name, False):
                stats["giant_ui_overlay_detected"] = False
            if stats["giant_ui_overlay_detected"]:
                errors.append(f"giant HUD-colored overlay suspected: {screenshot.name}")
        except Exception as exc:  # noqa: BLE001 - this is a CLI sanity helper
            errors.append(f"{screenshot.name}: {exc}")

    if report and not report.get("passed", False):
        errors.append("Unity visual QA report marked overall pass=false")

    analysis = {
        "input": str(root),
        "screenshot_count": len(screenshots),
        "unity_report_passed": report.get("passed") if report else None,
        "checks": checks,
        "errors": errors,
        "passed": not errors,
    }
    (root / "visual_qa_analysis.json").write_text(json.dumps(analysis, indent=2), encoding="utf-8")
    (root / "visual_qa_analysis.md").write_text(markdown(analysis), encoding="utf-8")

    if errors:
        for error in errors:
            print(f"VISUAL_QA_ERROR: {error}", file=sys.stderr)
        return 2 if args.fail_on_errors else 0

    print(f"Visual QA analysis passed for {root}")
    return 0


def markdown(analysis: dict) -> str:
    lines = [
        "# Visual QA Image Analysis",
        "",
        f"- Input: `{analysis['input']}`",
        f"- Screenshot count: {analysis['screenshot_count']}",
        f"- Unity report passed: {analysis['unity_report_passed']}",
        f"- Analysis passed: {analysis['passed']}",
        "",
        "| Screenshot | Not Blank | Non-Background | Green Text | Variance |",
        "| --- | --- | ---: | ---: | ---: |",
    ]
    for check in analysis["checks"]:
        lines.append(
            "| `{}` | {} | {:.3f} | {:.3f} | {:.1f} |".format(
                pathlib.Path(check["path"]).name,
                check["image_not_blank"],
                check["non_background_ratio"],
                check["green_text_ratio"],
                check["color_variance"],
            )
        )
    if analysis["errors"]:
        lines += ["", "## Errors"]
        lines += [f"- {error}" for error in analysis["errors"]]
    return "\n".join(lines) + "\n"


if __name__ == "__main__":
    raise SystemExit(main())
