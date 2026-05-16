"""Load an OpenMuse `record` .txt file into decoded DataFrames."""
from __future__ import annotations
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, Optional
import json
import pandas as pd

from OpenMuse.decode import decode_rawdata

SIDECAR_NAME = "recording_metadata.json"


@dataclass
class Recording:
    """Decoded recording grouped by modality. Times in seconds since start."""
    eeg: pd.DataFrame
    accgyro: pd.DataFrame
    optics: pd.DataFrame
    battery: pd.DataFrame
    sample_rates: Dict[str, float] = field(default_factory=dict)
    source: str = ""
    firmware: Optional[str] = None       # device firmware (e.g. "3.1.23")
    device_address: Optional[str] = None  # CoreBluetooth UUID or MAC
    battery_start_pct: Optional[float] = None

    def __getitem__(self, key: str) -> pd.DataFrame:
        return getattr(self, key.lower())


def _estimate_rate(df: pd.DataFrame) -> float:
    if df.empty or "time" not in df.columns or len(df) < 2:
        return float("nan")
    t = df["time"].to_numpy()
    duration = t[-1] - t[0]
    return (len(df) - 1) / duration if duration > 0 else float("nan")


def _normalize_time(df: pd.DataFrame, t0: float) -> pd.DataFrame:
    if df.empty or "time" not in df.columns:
        return df
    df = df.copy()
    df["time"] = df["time"] - t0
    return df


def _read_sidecar(path: Path) -> dict:
    """Look for `recording_metadata.json` next to the .txt and load it if present."""
    sidecar = path.parent / SIDECAR_NAME
    if not sidecar.exists():
        return {}
    try:
        return json.loads(sidecar.read_text())
    except (OSError, json.JSONDecodeError):
        return {}


def load_recording(path: str | Path, firmware: str | None = None) -> Recording:
    """Decode an OpenMuse .txt recording into a Recording object.

    Pulls firmware/device metadata from `recording_metadata.json` sidecar
    when present; the `firmware` kwarg overrides the sidecar value.
    """
    path = Path(path)
    with open(path) as f:
        lines = f.read().splitlines()
    decoded = decode_rawdata(lines)
    side = _read_sidecar(path)

    # Pick a common t0 (earliest sample across modalities) so all clocks share an origin.
    t0_candidates = [
        df["time"].iloc[0] for df in decoded.values()
        if isinstance(df, pd.DataFrame) and not df.empty and "time" in df.columns
    ]
    t0 = min(t0_candidates) if t0_candidates else 0.0

    eeg = _normalize_time(decoded.get("EEG", pd.DataFrame()), t0)
    accgyro = _normalize_time(decoded.get("ACCGYRO", pd.DataFrame()), t0)
    optics = _normalize_time(decoded.get("OPTICS", pd.DataFrame()), t0)
    battery = _normalize_time(decoded.get("BATTERY", pd.DataFrame()), t0)

    rates = {
        "EEG": _estimate_rate(eeg),
        "ACCGYRO": _estimate_rate(accgyro),
        "OPTICS": _estimate_rate(optics),
        "BATTERY": _estimate_rate(battery),
    }

    return Recording(
        eeg=eeg,
        accgyro=accgyro,
        optics=optics,
        battery=battery,
        sample_rates=rates,
        source=str(path),
        firmware=firmware or side.get("firmware"),
        device_address=side.get("device_address"),
        battery_start_pct=side.get("battery_start_pct"),
    )
