"""Per-modality quality metrics."""
from __future__ import annotations
from typing import Dict
import numpy as np
import pandas as pd
from scipy import signal

from .loaders import Recording
from .preprocess import Preprocessed, EEG_MAIN

# OpenMuse decodes EEG to a 14-bit ADC range scaled into µV (0..1450 µV).
# Saturation = sample at either rail.
EEG_RAIL_LO = 0.0
EEG_RAIL_HI = 1450.0


def eeg_quality(raw_eeg: pd.DataFrame, filt_eeg: pd.DataFrame, sr: float) -> pd.DataFrame:
    """Per-channel EEG quality: saturation %, std, line-noise ratio, simple SQI in [0, 1]."""
    if raw_eeg.empty or filt_eeg.empty:
        return pd.DataFrame()
    chans = [c for c in EEG_MAIN if c in raw_eeg.columns and c in filt_eeg.columns]
    rows = []
    nyq = 0.5 * sr
    for ch in chans:
        raw = raw_eeg[ch].to_numpy()
        filt = filt_eeg[ch].to_numpy()
        sat_pct = float(((raw <= EEG_RAIL_LO + 1e-6) | (raw >= EEG_RAIL_HI - 1e-6)).mean() * 100.0)
        std_uv = float(filt.std())

        # Line-noise (~60 Hz ± 1 Hz) power vs broadband 1–40 Hz power on the filtered signal.
        if len(filt) >= int(sr * 4):
            f, P = signal.welch(filt, fs=sr, nperseg=int(sr * 4))
            band_mask = (f >= 1) & (f <= 40)
            line_mask = (f >= 59) & (f <= 61)
            band_pow = float(P[band_mask].sum())
            line_pow = float(P[line_mask].sum())
            line_ratio = line_pow / band_pow if band_pow > 0 else float("nan")
        else:
            line_ratio = float("nan")

        # SQI heuristic: penalise saturation, runaway std, and dominant line noise.
        # Healthy EEG typically std 5–80 µV after 1–40 Hz bandpass.
        std_score = float(np.clip(1.0 - max(0.0, (std_uv - 80.0) / 200.0), 0.0, 1.0)) if std_uv > 0 else 0.0
        if std_uv < 1.0:
            std_score = 0.0  # too flat = disconnected
        sat_score = float(np.clip(1.0 - sat_pct / 5.0, 0.0, 1.0))   # >5% saturated -> 0
        line_score = float(np.clip(1.0 - (line_ratio if line_ratio == line_ratio else 0.0) * 5.0, 0.0, 1.0))
        sqi = float(np.clip(0.45 * sat_score + 0.35 * std_score + 0.20 * line_score, 0.0, 1.0))

        rows.append({
            "channel": ch,
            "saturation_pct": sat_pct,
            "std_uV": std_uv,
            "line_noise_ratio_60Hz": line_ratio,
            "sqi": sqi,
        })
    return pd.DataFrame(rows)


def fnirs_quality(fnirs: pd.DataFrame) -> pd.DataFrame:
    """Mean SQI per fNIRS position (from OpenMuse's anticorrelation SQI)."""
    if fnirs.empty:
        return pd.DataFrame()
    sqi_cols = [c for c in fnirs.columns if c.endswith("_SQI")]
    rows = []
    for c in sqi_cols:
        pos = c.replace("_SQI", "")
        v = fnirs[c].to_numpy()
        v = v[np.isfinite(v)]
        rows.append({
            "position": pos,
            "sqi_mean": float(v.mean()) if v.size else float("nan"),
            "sqi_min": float(v.min())  if v.size else float("nan"),
            "good_pct": float((v > 0.3).mean() * 100.0) if v.size else float("nan"),
        })
    return pd.DataFrame(rows)


def battery_summary(battery: pd.DataFrame) -> pd.DataFrame:
    if battery.empty:
        return pd.DataFrame()
    col = "battery_percent" if "battery_percent" in battery.columns else battery.columns[-1]
    return pd.DataFrame([{
        "samples": int(len(battery)),
        "start_pct": float(battery[col].iloc[0]),
        "end_pct": float(battery[col].iloc[-1]),
        "drop_pct": float(battery[col].iloc[0] - battery[col].iloc[-1]),
    }])


def recording_summary(rec: Recording) -> pd.DataFrame:
    rows = []
    for name, df in [("EEG", rec.eeg), ("ACCGYRO", rec.accgyro), ("OPTICS", rec.optics), ("BATTERY", rec.battery)]:
        if df.empty or "time" not in df.columns:
            rows.append({"modality": name, "samples": int(len(df)), "duration_s": 0.0, "rate_hz": float("nan")})
            continue
        dur = float(df["time"].iloc[-1] - df["time"].iloc[0])
        rate = (len(df) - 1) / dur if dur > 0 else float("nan")
        rows.append({"modality": name, "samples": int(len(df)), "duration_s": dur, "rate_hz": float(rate)})
    return pd.DataFrame(rows)


def compute_quality(rec: Recording, pre: Preprocessed) -> Dict[str, pd.DataFrame]:
    return {
        "summary": recording_summary(rec),
        "eeg": eeg_quality(rec.eeg, pre.eeg, sr=rec.sample_rates.get("EEG", 256.0)),
        "fnirs": fnirs_quality(pre.fnirs),
        "battery": battery_summary(rec.battery),
    }
