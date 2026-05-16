"""Epoching: turn continuous EEG + events into MNE Epochs and per-condition band power.

Two entry points:
    epoch_eeg(eeg_filt, events, tmin, tmax)      -> mne.Epochs
    band_power_by_condition(eeg_filt, events, ...) -> tidy DataFrame
"""
from __future__ import annotations
from typing import Dict, Iterable, Tuple
import numpy as np
import pandas as pd
from scipy import signal

from .preprocess import EEG_MAIN
from .export import MUSE_MNE_MONTAGE
from .features import EEG_BANDS


def _to_mne_raw(eeg_filt: pd.DataFrame, sr: float):
    import mne
    chans = [c for c in EEG_MAIN if c in eeg_filt.columns]
    arr = eeg_filt[chans].to_numpy().T * 1e-6  # MNE expects volts
    ch_names = [MUSE_MNE_MONTAGE[c] for c in chans]
    info = mne.create_info(ch_names=ch_names, sfreq=sr, ch_types=["eeg"] * len(ch_names))
    raw = mne.io.RawArray(arr, info, verbose=False)
    try:
        raw.set_montage("standard_1020", on_missing="ignore", verbose=False)
    except Exception:
        pass
    return raw


def _events_to_mne_array(events: pd.DataFrame, sr: float) -> Tuple[np.ndarray, Dict[str, int]]:
    """Convert tidy events df to MNE (samples, prev, code) array + label->code map."""
    labels = sorted(events["label"].dropna().unique().tolist())
    label_to_code = {lab: i + 1 for i, lab in enumerate(labels)}
    rows = []
    for _, ev in events.iterrows():
        s = int(round(float(ev["onset_s"]) * sr))
        rows.append([s, 0, label_to_code[ev["label"]]])
    arr = np.array(rows, dtype=int) if rows else np.empty((0, 3), dtype=int)
    return arr, label_to_code


def epoch_eeg(eeg_filt: pd.DataFrame, events: pd.DataFrame, sr: float,
              tmin: float = 0.0, tmax: float = 5.0, baseline=None,
              reject_uv: float | None = 200.0):
    """Build an mne.Epochs from filtered EEG and event onsets.

    reject_uv: peak-to-peak amplitude rejection threshold (µV). Pass None to disable.
    """
    import mne
    raw = _to_mne_raw(eeg_filt, sr)
    ev_arr, label_map = _events_to_mne_array(events, sr)
    reject = {"eeg": reject_uv * 1e-6} if reject_uv else None
    epochs = mne.Epochs(
        raw, ev_arr, event_id=label_map,
        tmin=tmin, tmax=tmax, baseline=baseline, reject=reject,
        preload=True, verbose=False,
    )
    return epochs


def _band_power(arr: np.ndarray, sr: float, lo: float, hi: float, nperseg: int) -> np.ndarray:
    """Welch-PSD band power. arr shape (n_ch, n_samp). Returns shape (n_ch,)."""
    f, P = signal.welch(arr, fs=sr, nperseg=min(nperseg, arr.shape[-1]), axis=-1)
    mask = (f >= lo) & (f < hi)
    return np.trapezoid(P[:, mask], f[mask], axis=-1)


def band_power_by_condition(
    eeg_filt: pd.DataFrame, events: pd.DataFrame, sr: float,
    bands: Dict[str, Tuple[float, float]] = EEG_BANDS,
) -> pd.DataFrame:
    """For each event, compute per-channel band power over [onset_s, onset_s + duration_s].

    Returns long-form DataFrame: epoch_idx, label, channel, band, power_uV2, duration_s.
    """
    if eeg_filt.empty or events.empty:
        return pd.DataFrame()
    chans = [c for c in EEG_MAIN if c in eeg_filt.columns]
    t = eeg_filt["time"].to_numpy()
    data = eeg_filt[chans].to_numpy().T  # (n_ch, n_samp)
    nperseg = int(sr * 2)  # 2 s windows -> ~0.5 Hz bins

    rows = []
    for i, ev in events.iterrows():
        onset = float(ev["onset_s"])
        dur = float(ev["duration_s"]) if ev["duration_s"] and not np.isnan(ev["duration_s"]) else 0.0
        if dur <= 0:
            continue
        end = onset + dur
        mask = (t >= onset) & (t < end)
        if mask.sum() < int(sr):  # need >=1 s of data
            continue
        seg = data[:, mask]
        for band, (lo, hi) in bands.items():
            powers = _band_power(seg, sr, lo, hi, nperseg=nperseg)
            for j, ch in enumerate(chans):
                rows.append({
                    "epoch_idx": int(i),
                    "label": ev["label"],
                    "channel": ch,
                    "band": band,
                    "power_uV2": float(powers[j]),
                    "duration_s": dur,
                })
    return pd.DataFrame(rows)
