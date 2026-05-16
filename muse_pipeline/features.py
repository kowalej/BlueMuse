"""Feature extraction: EEG band power, heart rate, motion stats."""
from __future__ import annotations
from dataclasses import dataclass, field
from typing import Dict, Tuple
import numpy as np
import pandas as pd
from scipy import signal

from .preprocess import Preprocessed, EEG_MAIN

EEG_BANDS: Dict[str, Tuple[float, float]] = {
    "delta": (1.0, 4.0),
    "theta": (4.0, 8.0),
    "alpha": (8.0, 13.0),
    "beta":  (13.0, 30.0),
    "gamma": (30.0, 45.0),
}


@dataclass
class Features:
    eeg_band_power: pd.DataFrame      # long-form: time, channel, band, power_uV2
    eeg_psd: pd.DataFrame             # full PSD averaged over recording (channel, freq, power)
    heart_rate: pd.DataFrame          # window_start, hr_bpm, n_peaks
    hrv: pd.DataFrame                 # single-row time + frequency domain HRV summary
    ibis: pd.DataFrame                # per-beat inter-beat intervals (ms)
    motion: pd.DataFrame              # single-row summary
    sample_rates: Dict[str, float] = field(default_factory=dict)


def _windowed_band_power(
    sig_arr: np.ndarray, sr: float, win_sec: float, step_sec: float,
) -> Tuple[np.ndarray, Dict[str, np.ndarray], np.ndarray, np.ndarray]:
    """Return (window_starts, band_power_dict[band->(n_win,n_ch)], freqs, mean_psd[n_ch,n_freq])."""
    n_samp = sig_arr.shape[-1]
    win = int(round(win_sec * sr))
    step = int(round(step_sec * sr))
    if n_samp < win:
        return np.array([]), {b: np.empty((0, sig_arr.shape[0])) for b in EEG_BANDS}, np.array([]), np.empty((sig_arr.shape[0], 0))

    starts = np.arange(0, n_samp - win + 1, step)
    n_win = len(starts)
    n_ch = sig_arr.shape[0]

    # Welch nperseg: cap at win, but at least 1s of data so we get ~1 Hz bins.
    nperseg = min(win, max(int(sr), 64))
    bp = {b: np.zeros((n_win, n_ch)) for b in EEG_BANDS}
    psd_accum = None
    freqs = None

    for w_idx, s in enumerate(starts):
        seg = sig_arr[:, s:s + win]
        f, P = signal.welch(seg, fs=sr, nperseg=nperseg, axis=-1)
        if psd_accum is None:
            psd_accum = np.zeros((n_ch, len(f)))
            freqs = f
        psd_accum += P
        for b_name, (lo, hi) in EEG_BANDS.items():
            mask = (f >= lo) & (f < hi)
            bp[b_name][w_idx] = np.trapezoid(P[:, mask], f[mask], axis=-1)

    psd_mean = psd_accum / n_win
    window_starts = starts / sr
    return window_starts, bp, freqs, psd_mean


def eeg_features(eeg_df: pd.DataFrame, sr: float, win_sec: float = 2.0, step_sec: float = 1.0):
    """Compute band power per window + recording-mean PSD."""
    if eeg_df.empty:
        return pd.DataFrame(), pd.DataFrame()
    chans = [c for c in eeg_df.columns if c != "time" and c in EEG_MAIN]
    arr = eeg_df[chans].to_numpy().T  # (n_ch, n_samp)
    starts, bp, freqs, psd = _windowed_band_power(arr, sr, win_sec, step_sec)

    rows = []
    for i, t in enumerate(starts):
        for j, ch in enumerate(chans):
            for b_name in EEG_BANDS:
                rows.append({
                    "time": float(t),
                    "channel": ch,
                    "band": b_name,
                    "power_uV2": float(bp[b_name][i, j]),
                })
    bp_df = pd.DataFrame(rows)

    psd_rows = []
    for j, ch in enumerate(chans):
        for k, f in enumerate(freqs):
            psd_rows.append({"channel": ch, "freq_hz": float(f), "power_uV2_per_Hz": float(psd[j, k])})
    psd_df = pd.DataFrame(psd_rows)
    return bp_df, psd_df


def heart_rate_from_bvp(
    bvp: np.ndarray, sr: float, win_sec: float = 10.0, step_sec: float = 5.0,
) -> pd.DataFrame:
    """Estimate HR per sliding window via peak detection on the BVP."""
    if bvp.size == 0:
        return pd.DataFrame()
    bvp = np.asarray(bvp).ravel()
    win = int(round(win_sec * sr))
    step = int(round(step_sec * sr))
    if len(bvp) < win:
        return pd.DataFrame()

    rows = []
    # min_distance corresponds to ~220 bpm upper bound (avoid double-peaks).
    min_dist = max(int(sr * 60.0 / 220.0), 1)
    for s in range(0, len(bvp) - win + 1, step):
        seg = bvp[s:s + win]
        seg = seg - seg.mean()
        std = seg.std()
        if std < 1e-9:
            continue
        peaks, _ = signal.find_peaks(seg, distance=min_dist, prominence=0.5 * std)
        n_peaks = len(peaks)
        hr_bpm = n_peaks * 60.0 / win_sec if n_peaks > 0 else float("nan")
        rows.append({
            "window_start_s": s / sr,
            "hr_bpm": hr_bpm,
            "n_peaks": int(n_peaks),
        })
    return pd.DataFrame(rows)


def _bvp_peaks(bvp: np.ndarray, sr: float) -> np.ndarray:
    """Detect BVP peaks; reject outlier IBIs > 3 SD."""
    if bvp.size == 0:
        return np.array([], dtype=int)
    sig = bvp - bvp.mean()
    std = sig.std()
    if std < 1e-9:
        return np.array([], dtype=int)
    min_dist = max(int(sr * 60.0 / 220.0), 1)  # >=220 bpm cap
    peaks, _ = signal.find_peaks(sig, distance=min_dist, prominence=0.5 * std)
    return peaks


def hrv_from_bvp(bvp: np.ndarray, sr: float) -> Tuple[pd.DataFrame, pd.DataFrame]:
    """Time + frequency domain HRV summary from a BVP signal.

    Returns (summary_df, ibis_df). IBIs in milliseconds.
    Frequency-domain bands (Task Force 1996):
        VLF 0.003-0.04, LF 0.04-0.15, HF 0.15-0.40 Hz
    """
    peaks = _bvp_peaks(np.asarray(bvp).ravel(), sr)
    if peaks.size < 4:
        return pd.DataFrame(), pd.DataFrame()
    ibi_s = np.diff(peaks) / sr
    ibi_ms = ibi_s * 1000.0

    # Drop physiologically implausible IBIs (clip to 300-2000 ms = 30-200 bpm).
    plausible = (ibi_ms > 300) & (ibi_ms < 2000)
    ibi_ms = ibi_ms[plausible]
    if ibi_ms.size < 4:
        return pd.DataFrame(), pd.DataFrame()
    diffs = np.diff(ibi_ms)

    mean_ibi = float(ibi_ms.mean())
    sdnn = float(ibi_ms.std(ddof=1))
    rmssd = float(np.sqrt(np.mean(diffs ** 2)))
    pnn50 = float(np.mean(np.abs(diffs) > 50.0) * 100.0)
    mean_hr = 60000.0 / mean_ibi

    # Frequency domain: resample IBI series to 4 Hz then Welch.
    t_beats = np.cumsum(ibi_ms) / 1000.0
    if len(t_beats) >= 16 and t_beats[-1] - t_beats[0] >= 30.0:
        fs_resamp = 4.0
        t_uniform = np.arange(t_beats[0], t_beats[-1], 1.0 / fs_resamp)
        ibi_uniform = np.interp(t_uniform, t_beats, ibi_ms)
        ibi_uniform -= ibi_uniform.mean()
        nperseg = min(256, len(ibi_uniform))
        f, Pxx = signal.welch(ibi_uniform, fs=fs_resamp, nperseg=nperseg)
        def _bp(lo, hi):
            m = (f >= lo) & (f < hi)
            return float(np.trapezoid(Pxx[m], f[m])) if m.any() else float("nan")
        vlf = _bp(0.003, 0.04)
        lf = _bp(0.04, 0.15)
        hf = _bp(0.15, 0.40)
        lf_hf = lf / hf if hf and hf > 0 else float("nan")
        total = vlf + lf + hf
        lf_nu = 100.0 * lf / (lf + hf) if (lf + hf) > 0 else float("nan")
        hf_nu = 100.0 * hf / (lf + hf) if (lf + hf) > 0 else float("nan")
    else:
        vlf = lf = hf = lf_hf = total = lf_nu = hf_nu = float("nan")

    summary = pd.DataFrame([{
        "n_beats": int(len(ibi_ms) + 1),
        "mean_hr_bpm": mean_hr,
        "mean_ibi_ms": mean_ibi,
        "sdnn_ms": sdnn,
        "rmssd_ms": rmssd,
        "pnn50_pct": pnn50,
        "vlf_ms2": vlf,
        "lf_ms2": lf,
        "hf_ms2": hf,
        "total_power_ms2": total,
        "lf_hf_ratio": lf_hf,
        "lf_nu": lf_nu,
        "hf_nu": hf_nu,
    }])
    ibis_df = pd.DataFrame({"beat": np.arange(1, len(ibi_ms) + 1), "ibi_ms": ibi_ms})
    return summary, ibis_df


def motion_stats(accgyro_features: pd.DataFrame, accgyro_raw: pd.DataFrame) -> pd.DataFrame:
    if accgyro_raw.empty:
        return pd.DataFrame()
    raw_cols = [c for c in ("ACC_X", "ACC_Y", "ACC_Z", "GYRO_X", "GYRO_Y", "GYRO_Z") if c in accgyro_raw.columns]
    raw = accgyro_raw[raw_cols].to_numpy()
    acc_xyz = raw[:, :3]
    gyro_xyz = raw[:, 3:]
    summary = {
        "duration_s": float(accgyro_raw["time"].iloc[-1] - accgyro_raw["time"].iloc[0]) if "time" in accgyro_raw.columns else float("nan"),
        "acc_mag_mean_g": float(np.linalg.norm(acc_xyz, axis=1).mean()),
        "acc_mag_std_g":  float(np.linalg.norm(acc_xyz, axis=1).std()),
        "gyro_mag_mean_dps": float(np.linalg.norm(gyro_xyz, axis=1).mean()),
        "gyro_mag_std_dps":  float(np.linalg.norm(gyro_xyz, axis=1).std()),
    }
    if not accgyro_features.empty:
        summary["pitch_mean_deg"] = float(accgyro_features["ACC_PITCH"].mean())
        summary["roll_mean_deg"]  = float(accgyro_features["ACC_ROLL"].mean())
    return pd.DataFrame([summary])


def compute_features(pre: Preprocessed) -> Features:
    sr = pre.sample_rates
    bp_df, psd_df = eeg_features(pre.eeg, sr=sr.get("EEG", 256.0))
    hr_df = heart_rate_from_bvp(pre.bvp, sr=sr.get("OPTICS", 64.0))
    hrv_df, ibis_df = hrv_from_bvp(pre.bvp, sr=sr.get("OPTICS", 64.0))
    motion = motion_stats(pre.accgyro_features, pre.accgyro_raw)
    return Features(
        eeg_band_power=bp_df, eeg_psd=psd_df, heart_rate=hr_df,
        hrv=hrv_df, ibis=ibis_df, motion=motion, sample_rates=sr,
    )
