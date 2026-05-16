"""Event markers for epoching / condition analysis.

An events CSV has columns:
    onset_iso     ISO 8601 wall-clock UTC string (matches OpenMuse `record` packet timestamps)
    onset_s       seconds since recording start (set by `align_events`)
    label         short string condition name (e.g. "eyes_open", "stimulus_face")
    duration_s    block / trial duration (0 for instantaneous markers)
    notes         free-form metadata (optional)
"""
from __future__ import annotations
from datetime import datetime, timezone
from pathlib import Path
import pandas as pd

EVENT_COLUMNS = ("onset_iso", "onset_s", "label", "duration_s", "notes")


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def make_event(label: str, duration_s: float = 0.0, notes: str = "", onset_iso: str | None = None) -> dict:
    return {
        "onset_iso": onset_iso or now_iso(),
        "onset_s": float("nan"),
        "label": label,
        "duration_s": float(duration_s),
        "notes": notes,
    }


def save_events(events: list[dict] | pd.DataFrame, path: Path) -> Path:
    df = events if isinstance(events, pd.DataFrame) else pd.DataFrame(events)
    for col in EVENT_COLUMNS:
        if col not in df.columns:
            df[col] = "" if col in ("onset_iso", "label", "notes") else float("nan")
    df = df[list(EVENT_COLUMNS)]
    path.parent.mkdir(parents=True, exist_ok=True)
    df.to_csv(path, index=False)
    return path


def load_events(path: Path) -> pd.DataFrame:
    df = pd.read_csv(path)
    if "notes" in df.columns:
        df["notes"] = df["notes"].fillna("")
    return df


def align_events(events: pd.DataFrame, recording_t0_iso: str) -> pd.DataFrame:
    """Fill the onset_s column using the recording's wall-clock t0."""
    if events.empty:
        return events
    t0 = datetime.fromisoformat(recording_t0_iso)
    out = events.copy()
    out["onset_s"] = [
        (datetime.fromisoformat(iso) - t0).total_seconds()
        for iso in out["onset_iso"]
    ]
    return out


def first_packet_iso(record_txt_path: Path) -> str:
    """Return the ISO timestamp of the first packet in an OpenMuse .txt recording."""
    with open(record_txt_path) as f:
        first_line = f.readline().rstrip("\n")
    if not first_line:
        raise ValueError(f"Empty recording file: {record_txt_path}")
    iso = first_line.split("\t", 1)[0]
    return iso
