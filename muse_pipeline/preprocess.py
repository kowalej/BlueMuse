"""Per-modality preprocessing. EEG is implemented here; IMU/PPG/fNIRS wrap OpenMuse."""
from __future__ import annotations
from dataclasses import dataclass, field
from typing import Dict, List, Tuple
import numpy as np
import pandas as pd
from scipy import signal

from OpenMuse.process import (
    apply_butter_filter,
    preprocess_accgyro as _om_preprocess_accgyro,
    preprocess_ppg as _om_preprocess_ppg,
    preprocess_fnirs as _om_preprocess_fnirs,
)

from .loaders import Recording

EEG_MAIN = ("EEG_TP9", "EEG_AF7", "EEG_AF8", "EEG_TP10")


@dataclass
class Preprocessed:
    eeg: pd.DataFrame                 # filtered EEG (µV), main channels only by default
    accgyro_features: pd.DataFrame    # ACC_MAG, PITCH, ROLL, GYRO_MAG (from OpenMuse)
    accgyro_raw: pd.DataFrame         # untouched accgyro (carries time)
    bvp: np.ndarray                   # fused PPG / blood volume pulse
    bvp_info: dict
    fnirs: pd.DataFrame               # HbO/HbR/HbDiff/SQI per position (µM)
    fnirs_info: dict
    sample_rates: Dict[str, float] = field(default_factory=dict)


def _filter_eeg(
    df: pd.DataFrame,
    sr: float,
    l_freq: float,
    h_freq: float,
    notch_freq: float | None,
    notch_q: float,
    main_only: bool,
) -> pd.DataFrame:
    if df.empty:
        return df
    cols = list(EEG_MAIN) if main_only else [c for c in df.columns if c != "time"]
    out = df[["time"] + cols].copy()
    arr = out[cols].to_numpy().T  # shape (n_channels, n_samples)

    # Bandpass (zero-phase) — also removes the ~700 µV DC bias.
    nyq = 0.5 * sr
    sos = signal.butter(4, [l_freq / nyq, h_freq / nyq], btype="band", output="sos")
    arr = signal.sosfiltfilt(sos, arr, axis=-1)

    # Notch (zero-phase IIR) for mains hum.
    if notch_freq is not None and notch_freq < nyq:
        b, a = signal.iirnotch(notch_freq / nyq, notch_q)
        arr = signal.filtfilt(b, a, arr, axis=-1)

    out[cols] = arr.T
    return out


def preprocess_eeg(
    df: pd.DataFrame,
    sr: float = 256.0,
    l_freq: float = 1.0,
    h_freq: float = 40.0,
    notch_freq: float | None = 60.0,
    notch_q: float = 30.0,
    main_only: bool = True,
) -> pd.DataFrame:
    """Bandpass + notch zero-phase filter EEG. Returns df with same column order."""
    return _filter_eeg(df, sr, l_freq, h_freq, notch_freq, notch_q, main_only)


def preprocess_accgyro(
    df: pd.DataFrame, sr: float = 52.0, lp_cutoff: float = 5.0,
) -> Tuple[pd.DataFrame, pd.DataFrame]:
    """Returns (features_df, raw_df_with_time). features: ACC_MAG, PITCH, ROLL, GYRO_MAG."""
    if df.empty:
        return pd.DataFrame(), df
    feat = _om_preprocess_accgyro(df, sampling_rate=int(sr), lp_cutoff=lp_cutoff)
    feat.insert(0, "time", df["time"].to_numpy())
    return feat, df


def preprocess_ppg(df: pd.DataFrame, sr: float = 64.0) -> Tuple[np.ndarray, dict]:
    """Returns (bvp, info). info contains per-channel SQI and fused weights."""
    if df.empty:
        return np.array([]), {}
    return _om_preprocess_ppg(df, sampling_rate=int(sr))


def preprocess_fnirs(df: pd.DataFrame, sr: float = 64.0) -> Tuple[pd.DataFrame, dict]:
    """Returns (hb_df, info). hb_df has *_HbO, *_HbR, *_HbDiff, *_SQI per position."""
    if df.empty:
        return pd.DataFrame(), {}
    hb_df, info = _om_preprocess_fnirs(df, sampling_rate=int(sr))
    if "time" in df.columns and not hb_df.empty:
        hb_df = hb_df.copy()
        hb_df.insert(0, "time", df["time"].to_numpy())
    return hb_df, info


def preprocess_all(rec: Recording) -> Preprocessed:
    sr = rec.sample_rates
    eeg = preprocess_eeg(rec.eeg, sr=sr.get("EEG", 256.0))
    accgyro_feat, accgyro_raw = preprocess_accgyro(rec.accgyro, sr=sr.get("ACCGYRO", 52.0))
    bvp, bvp_info = preprocess_ppg(rec.optics, sr=sr.get("OPTICS", 64.0))
    fnirs, fnirs_info = preprocess_fnirs(rec.optics, sr=sr.get("OPTICS", 64.0))
    return Preprocessed(
        eeg=eeg,
        accgyro_features=accgyro_feat,
        accgyro_raw=accgyro_raw,
        bvp=bvp,
        bvp_info=bvp_info,
        fnirs=fnirs,
        fnirs_info=fnirs_info,
        sample_rates=sr,
    )
