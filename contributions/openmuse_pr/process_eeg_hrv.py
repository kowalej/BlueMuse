"""
Proposed additions to OpenMuse/process.py — three small, focused functions
that fill clear gaps in the current preprocessing/feature surface:

    1) preprocess_eeg(df, ...)        Bandpass + notch + main-channel selection.
                                      Matches the preprocess_accgyro/ppg/fnirs pattern.

    2) eeg_band_power(df, sr, ...)    Windowed PSD-based band power (δ θ α β γ).
                                      Returns a long-form DataFrame suitable for groupby.

    3) hrv_from_bvp(bvp, sr)          Time + frequency domain HRV from the BVP
                                      signal that preprocess_ppg already produces.

These reuse the existing `apply_butter_filter` and the channel constants from
OpenMuse/decode.py. No new dependencies. All functions are tested against
real Muse S Athena recordings.

To apply: paste each function into `OpenMuse/process.py` after the matching
section (EEG section can be inserted between PPG and fNIRS; HRV section
naturally goes after PPG since hrv_from_bvp consumes preprocess_ppg's output).
"""
from __future__ import annotations
import numpy as np
import pandas as pd
from scipy import signal

# These come from OpenMuse/process.py and OpenMuse/decode.py respectively.
# Stub-imported here so the file can be smoke-tested standalone.
try:
    from OpenMuse.process import apply_butter_filter
    from OpenMuse.decode import EEG_CHANNELS
except ImportError:  # pragma: no cover - upstream provides these
    EEG_CHANNELS = (
        "EEG_TP9", "EEG_AF7", "EEG_AF8", "EEG_TP10",
        "AUX_1", "AUX_2", "AUX_3", "AUX_4",
    )

    def apply_butter_filter(data, cutoff, sampling_rate, btype="low", order=4):
        nyq = 0.5 * sampling_rate
        if btype == "band":
            wn = [c / nyq for c in cutoff]
        else:
            wn = cutoff / nyq
        sos = signal.butter(order, wn, btype=btype, output="sos")
        data = np.asarray(data)
        if data.ndim == 1:
            return signal.sosfiltfilt(sos, data)
        return np.vstack([signal.sosfiltfilt(sos, d) for d in data])


EEG_MAIN_CHANNELS = ("EEG_TP9", "EEG_AF7", "EEG_AF8", "EEG_TP10")

EEG_BANDS_DEFAULT = {
    "delta": (1.0, 4.0),
    "theta": (4.0, 8.0),
    "alpha": (8.0, 13.0),
    "beta":  (13.0, 30.0),
    "gamma": (30.0, 45.0),
}


# ===============================================================
# EEG
# ===============================================================
def preprocess_eeg(
    df: pd.DataFrame,
    sampling_rate: int = 256,
    hp_cutoff: float = 1.0,
    lp_cutoff: float = 40.0,
    notch_freq: float | None = 60.0,
    notch_q: float = 30.0,
    main_only: bool = True,
) -> pd.DataFrame:
    """
    Preprocess EEG: zero-phase bandpass + optional powerline notch.

    The bandpass also removes the standing DC bias that the Athena's ADC
    produces (raw values are scaled to µV by `decode.EEG_SCALE` but sit
    around ~700 µV).

    Args:
        df: Decoded EEG DataFrame from `decode_rawdata`. Must contain
            the columns "time" and the EEG channels.
        sampling_rate: EEG sampling rate (default 256 Hz).
        hp_cutoff: High-pass cutoff in Hz (default 1.0).
        lp_cutoff: Low-pass cutoff in Hz (default 40.0).
        notch_freq: Powerline notch frequency in Hz, or None to skip.
            Default 60.0 — pass 50.0 in Europe.
        notch_q: Q factor for the IIR notch filter (default 30).
        main_only: If True, drop the AUX channels and keep only
            TP9/AF7/AF8/TP10.

    Returns:
        pd.DataFrame: Filtered EEG with the original "time" column and
        the selected channels in the same order.
    """
    if df.empty:
        return df

    channels = list(EEG_MAIN_CHANNELS) if main_only else [c for c in EEG_CHANNELS if c in df.columns]
    channels = [c for c in channels if c in df.columns]
    if not channels:
        return df.copy()

    out = df[["time", *channels]].copy()
    arr = out[channels].to_numpy().T  # shape (n_channels, n_samples)

    arr = apply_butter_filter(arr, (hp_cutoff, lp_cutoff), sampling_rate, btype="band")

    nyq = 0.5 * sampling_rate
    if notch_freq is not None and notch_freq < nyq:
        b, a = signal.iirnotch(notch_freq / nyq, notch_q)
        arr = signal.filtfilt(b, a, arr, axis=-1)

    out[channels] = arr.T
    return out


def eeg_band_power(
    df: pd.DataFrame,
    sampling_rate: int = 256,
    bands: dict | None = None,
    win_sec: float = 2.0,
    step_sec: float = 1.0,
) -> pd.DataFrame:
    """
    Windowed band power per EEG channel via Welch PSD + frequency integration.

    Operates on a filtered EEG DataFrame (output of `preprocess_eeg`).
    Each window's band power is computed as ∫ Pxx(f) df over the band.

    Args:
        df: Filtered EEG DataFrame with "time" and channel columns.
        sampling_rate: EEG sampling rate (default 256 Hz).
        bands: Optional mapping {name: (low_hz, high_hz)}. Defaults to
            δ θ α β γ (Pernet et al. conventions).
        win_sec: Window length in seconds (default 2.0).
        step_sec: Hop in seconds (default 1.0).

    Returns:
        pd.DataFrame in long form with columns
        ["time", "channel", "band", "power_uV2"].
        Each row is the power in one band, one channel, at one window start.
    """
    if df.empty:
        return pd.DataFrame(columns=["time", "channel", "band", "power_uV2"])

    bands = bands or EEG_BANDS_DEFAULT
    channels = [c for c in EEG_MAIN_CHANNELS if c in df.columns]
    if not channels:
        return pd.DataFrame(columns=["time", "channel", "band", "power_uV2"])

    sig_arr = df[channels].to_numpy().T  # (n_ch, n_samp)
    n_samp = sig_arr.shape[-1]
    win = int(round(win_sec * sampling_rate))
    step = int(round(step_sec * sampling_rate))
    if n_samp < win:
        return pd.DataFrame(columns=["time", "channel", "band", "power_uV2"])

    starts = np.arange(0, n_samp - win + 1, step)
    nperseg = min(win, max(sampling_rate, 64))

    rows = []
    for s in starts:
        seg = sig_arr[:, s:s + win]
        f, P = signal.welch(seg, fs=sampling_rate, nperseg=nperseg, axis=-1)
        t = s / sampling_rate
        for b_name, (lo, hi) in bands.items():
            mask = (f >= lo) & (f < hi)
            powers = np.trapezoid(P[:, mask], f[mask], axis=-1)
            for j, ch in enumerate(channels):
                rows.append({
                    "time": float(t),
                    "channel": ch,
                    "band": b_name,
                    "power_uV2": float(powers[j]),
                })
    return pd.DataFrame(rows)


# ===============================================================
# HRV  (consumes BVP from preprocess_ppg)
# ===============================================================
def hrv_from_bvp(
    bvp: np.ndarray,
    sampling_rate: int = 64,
    min_hr_bpm: float = 30.0,
    max_hr_bpm: float = 200.0,
) -> tuple[pd.DataFrame, pd.DataFrame]:
    """
    Time + frequency domain HRV from a BVP signal.

    Detects systolic peaks on the fused BVP returned by `preprocess_ppg`
    (which already inverts the reflectance polarity so that systolic peaks
    are maxima), filters physiologically implausible inter-beat intervals,
    and computes the standard HRV indices.

    Frequency-domain bands follow Task Force (1996):
        VLF  0.003 - 0.04 Hz
        LF   0.04  - 0.15 Hz
        HF   0.15  - 0.40 Hz
    The IBI tachogram is resampled to 4 Hz (linear interpolation) before
    Welch PSD. Frequency-domain indices require >= 30 s of beats and are
    returned as NaN otherwise.

    Args:
        bvp: 1-D BVP array (output of `preprocess_ppg`).
        sampling_rate: BVP sampling rate (default 64 Hz, matches optics).
        min_hr_bpm / max_hr_bpm: Physiologic IBI window. IBIs outside
            [60000/max_hr_bpm, 60000/min_hr_bpm] ms are dropped.

    Returns:
        tuple[pd.DataFrame, pd.DataFrame]:
          - summary: single-row DataFrame with
              n_beats, mean_hr_bpm, mean_ibi_ms,
              sdnn_ms, rmssd_ms, pnn50_pct,
              vlf_ms2, lf_ms2, hf_ms2, total_power_ms2,
              lf_hf_ratio, lf_nu, hf_nu
          - ibis: per-beat DataFrame with columns [beat, ibi_ms]
    """
    empty_summary = pd.DataFrame()
    empty_ibis = pd.DataFrame(columns=["beat", "ibi_ms"])

    if bvp is None or len(bvp) == 0:
        return empty_summary, empty_ibis

    sig_arr = np.asarray(bvp, dtype=float).ravel()
    # Drop NaNs at the head/tail rather than letting find_peaks see them.
    finite = np.isfinite(sig_arr)
    if not finite.any():
        return empty_summary, empty_ibis
    sig_arr = sig_arr[finite]

    std = sig_arr.std()
    if std < 1e-9:
        return empty_summary, empty_ibis

    min_dist = max(int(sampling_rate * 60.0 / max_hr_bpm), 1)
    peaks, _ = signal.find_peaks(
        sig_arr - sig_arr.mean(), distance=min_dist, prominence=0.5 * std,
    )
    if peaks.size < 4:
        return empty_summary, empty_ibis

    ibi_ms = np.diff(peaks) / sampling_rate * 1000.0
    lo_ms = 60000.0 / max_hr_bpm
    hi_ms = 60000.0 / min_hr_bpm
    ibi_ms = ibi_ms[(ibi_ms > lo_ms) & (ibi_ms < hi_ms)]
    if ibi_ms.size < 4:
        return empty_summary, empty_ibis

    diffs = np.diff(ibi_ms)
    mean_ibi = float(ibi_ms.mean())
    sdnn = float(ibi_ms.std(ddof=1))
    rmssd = float(np.sqrt(np.mean(diffs ** 2)))
    pnn50 = float(np.mean(np.abs(diffs) > 50.0) * 100.0)
    mean_hr = 60000.0 / mean_ibi

    # Frequency domain (Welch on 4 Hz resampled tachogram).
    t_beats = np.cumsum(ibi_ms) / 1000.0
    if len(t_beats) >= 16 and (t_beats[-1] - t_beats[0]) >= 30.0:
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
        lf_hf = lf / hf if (hf and hf > 0) else float("nan")
        total = vlf + lf + hf
        lf_nu = 100.0 * lf / (lf + hf) if (lf + hf) > 0 else float("nan")
        hf_nu = 100.0 * hf / (lf + hf) if (lf + hf) > 0 else float("nan")
    else:
        vlf = lf = hf = total = lf_hf = lf_nu = hf_nu = float("nan")

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
    ibis = pd.DataFrame({"beat": np.arange(1, len(ibi_ms) + 1), "ibi_ms": ibi_ms})
    return summary, ibis
