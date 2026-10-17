"""Project the native UI onto the user-approved TAB5 image without redrawing it.

Requires Pillow, numpy and opencv-python. Coordinates refer to the approved
1254 x 1254 image, clockwise from the active LCD's top-left corner.
"""

import argparse
import hashlib
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


LCD_CORNERS = np.float32([[372, 362], [1136, 525], [944, 849], [121, 605]])


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--hardware", type=Path, required=True)
    parser.add_argument("--screen", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args()
    base = np.array(Image.open(args.hardware).convert("RGB"))
    screen = np.array(Image.open(args.screen).convert("RGB"))
    if base.shape != (1254, 1254, 3) or screen.shape != (720, 1280, 3):
        raise ValueError("Expected approved 1254x1254 base and native 1280x720 UI")
    if args.output.resolve() in {args.hardware.resolve(), args.screen.resolve()}:
        raise ValueError("Output must not overwrite either input")

    height, width = base.shape[:2]
    sh, sw = screen.shape[:2]
    src = np.float32([[0, 0], [sw - 1, 0], [sw - 1, sh - 1], [0, sh - 1]])
    transform = cv2.getPerspectiveTransform(src, LCD_CORNERS)
    scale = 4
    high_transform = np.diag([scale, scale, 1.0]) @ transform
    high_size = (width * scale, height * scale)
    projected = cv2.warpPerspective(
        screen, high_transform, high_size,
        flags=cv2.INTER_LANCZOS4, borderMode=cv2.BORDER_REPLICATE,
    )
    mask = np.zeros((height * scale, width * scale), dtype=np.uint8)
    cv2.fillConvexPoly(mask, np.rint(LCD_CORNERS * scale).astype(np.int32), 255)
    coverage = mask.astype(np.float32) / 255.0
    premultiplied = projected.astype(np.float32) * coverage[:, :, None]
    layer = cv2.resize(premultiplied, (width, height), interpolation=cv2.INTER_AREA)
    alpha = cv2.resize(coverage, (width, height), interpolation=cv2.INTER_AREA)
    result = np.rint(layer + base.astype(np.float32) * (1 - alpha[:, :, None]))
    result = np.clip(result, 0, 255).astype(np.uint8)
    outside = alpha == 0
    changed_outside = int(np.count_nonzero(np.any(result != base, axis=2) & outside))
    if changed_outside:
        raise AssertionError("Hardware/background pixels changed outside LCD")
    mapped = cv2.perspectiveTransform(src[None, :, :], transform)[0]
    error = float(np.max(np.abs(mapped - LCD_CORNERS)))
    if error > 0.001:
        raise AssertionError("Screen corner projection failed")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(result).save(args.output)
    saved = np.array(Image.open(args.output).convert("RGB"))
    if not np.array_equal(saved[outside], base[outside]):
        raise AssertionError("Saved PNG changed pixels outside LCD")
    report = {
        "method": "single planar homography; 4x antialiasing; no generative editing",
        "hardware_sha256": sha256(args.hardware),
        "screen_sha256": sha256(args.screen),
        "output_sha256": sha256(args.output),
        "lcd_corners_tl_tr_br_bl": LCD_CORNERS.tolist(),
        "homography": transform.tolist(),
        "changed_pixels_outside_lcd": changed_outside,
        "unchanged_pixels_outside_lcd": int(np.count_nonzero(outside)),
        "max_corner_error_px": error,
    }
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
