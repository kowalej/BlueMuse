"""Top-level pipeline runner."""
from __future__ import annotations
from pathlib import Path
from typing import Dict
import time

from .loaders import load_recording
from .preprocess import preprocess_all
from .features import compute_features
from .quality import compute_quality
from .export import export_all


def run_pipeline(input_path: str | Path, outdir: str | Path, firmware: str | None = None) -> Dict[str, Path]:
    """Run the full offline pipeline on an OpenMuse `.txt` recording.

    firmware: optional override; otherwise read from `recording_metadata.json`
    sidecar next to the input file (if present).
    """
    input_path = Path(input_path)
    outdir = Path(outdir)
    t0 = time.time()
    print(f"[1/5] decoding   {input_path}")
    rec = load_recording(input_path, firmware=firmware)
    if rec.firmware:
        print(f"      firmware: {rec.firmware}  device: {rec.device_address or '(unknown)'}")
    print(f"      sample rates (Hz): {rec.sample_rates}")

    print("[2/5] preprocess")
    pre = preprocess_all(rec)

    print("[3/5] features")
    feats = compute_features(pre)

    print("[4/5] quality")
    qc = compute_quality(rec, pre)

    print(f"[5/5] export -> {outdir}")
    written = export_all(rec, pre, feats, qc, outdir)
    print(f"done in {time.time() - t0:.1f}s — {len(written)} artifacts")
    return written
