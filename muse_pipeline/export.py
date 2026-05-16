"""Export decoded / preprocessed / feature data to disk + a one-page QC PDF."""
from __future__ import annotations
from pathlib import Path
from typing import Dict
import json
import numpy as np
import pandas as pd
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

from .loaders import Recording
from .preprocess import Preprocessed, EEG_MAIN
from .features import Features

# Standard 10-20 sensor positions for Muse main channels (used by MNE).
MUSE_MNE_MONTAGE = {
    "EEG_TP9":  "TP9",
    "EEG_AF7":  "AF7",
    "EEG_AF8":  "AF8",
    "EEG_TP10": "TP10",
}


def _save_parquet_or_csv(df: pd.DataFrame, base: Path) -> Path | None:
    """Try Parquet, fall back to CSV if pyarrow/fastparquet missing. Empty df -> None."""
    if df.empty:
        return None
    try:
        out = base.with_suffix(".parquet")
        df.to_parquet(out, index=False)
        return out
    except (ImportError, ValueError):
        out = base.with_suffix(".csv")
        df.to_csv(out, index=False)
        return out


def export_dataframes(
    rec: Recording, pre: Preprocessed, feats: Features, qc: Dict[str, pd.DataFrame], outdir: Path,
) -> Dict[str, Path]:
    outdir.mkdir(parents=True, exist_ok=True)
    written: Dict[str, Path] = {}

    def _record(key: str, p: Path | None):
        if p is not None:
            written[key] = p

    # Decoded raw per modality.
    for name, df in [("eeg_raw", rec.eeg), ("accgyro_raw", rec.accgyro),
                     ("optics_raw", rec.optics), ("battery", rec.battery)]:
        _record(name, _save_parquet_or_csv(df, outdir / name))

    # Preprocessed.
    _record("eeg_filtered",     _save_parquet_or_csv(pre.eeg, outdir / "eeg_filtered"))
    _record("accgyro_features", _save_parquet_or_csv(pre.accgyro_features, outdir / "accgyro_features"))
    _record("fnirs",            _save_parquet_or_csv(pre.fnirs, outdir / "fnirs"))
    if pre.bvp.size:
        sr = pre.sample_rates.get("OPTICS", 64.0)
        bvp_df = pd.DataFrame({"time": np.arange(len(pre.bvp)) / sr, "bvp": pre.bvp})
        _record("bvp", _save_parquet_or_csv(bvp_df, outdir / "bvp"))

    # Features.
    _record("eeg_band_power", _save_parquet_or_csv(feats.eeg_band_power, outdir / "eeg_band_power"))
    _record("eeg_psd",        _save_parquet_or_csv(feats.eeg_psd, outdir / "eeg_psd"))
    _record("heart_rate",     _save_parquet_or_csv(feats.heart_rate, outdir / "heart_rate"))
    _record("hrv",            _save_parquet_or_csv(feats.hrv, outdir / "hrv"))
    _record("ibis",           _save_parquet_or_csv(feats.ibis, outdir / "ibis"))
    _record("motion",         _save_parquet_or_csv(feats.motion, outdir / "motion"))

    # QC tables.
    qc_dir = outdir / "qc"
    qc_dir.mkdir(exist_ok=True)
    for name, df in qc.items():
        _record(f"qc_{name}", _save_parquet_or_csv(df, qc_dir / name))

    # Metadata sidecar.
    meta = {
        "source": rec.source,
        "device_address": rec.device_address,
        "firmware": rec.firmware,
        "battery_start_pct": rec.battery_start_pct,
        "sample_rates_hz": rec.sample_rates,
        "fnirs_info": {k: v for k, v in pre.fnirs_info.items() if isinstance(v, (str, int, float, list, dict))},
        "bvp_info": {k: v for k, v in pre.bvp_info.items() if isinstance(v, (str, int, float, list, dict))},
        "experimental_notices": [
            (
                "fNIRS outputs (Muse_OPTICS -> HbO/HbR/HbDiff) are EXPERIMENTAL. "
                "The maintainer of OpenMuse (issue #23, 2026-01-29) notes that the "
                "optics-to-fNIRS computation is still unclear and unvalidated, and a "
                "recent firmware update changed optics behavior. Use HbO/HbR values "
                "as a research signal, not as a measurement. EEG, IMU, BVP/HR/HRV are "
                "unaffected."
            ),
        ],
    }
    (outdir / "metadata.json").write_text(json.dumps(meta, indent=2, default=str))
    written["metadata"] = outdir / "metadata.json"
    return written


def export_mne_fif(eeg_filt: pd.DataFrame, sr: float, path: Path) -> Path | None:
    """Export filtered EEG to a MNE Raw FIF file with a 10-20 montage."""
    if eeg_filt.empty:
        return None
    try:
        import mne
    except ImportError:
        return None
    chans = [c for c in EEG_MAIN if c in eeg_filt.columns]
    arr = eeg_filt[chans].to_numpy().T * 1e-6  # MNE expects volts
    ch_names = [MUSE_MNE_MONTAGE[c] for c in chans]
    info = mne.create_info(ch_names=ch_names, sfreq=sr, ch_types=["eeg"] * len(ch_names))
    raw = mne.io.RawArray(arr, info, verbose=False)
    try:
        raw.set_montage("standard_1020", on_missing="ignore", verbose=False)
    except Exception:
        pass
    path.parent.mkdir(parents=True, exist_ok=True)
    raw.save(path, overwrite=True, verbose=False)
    return path


def qc_report(rec: Recording, pre: Preprocessed, feats: Features, qc: Dict[str, pd.DataFrame], path: Path) -> Path:
    """One-page multipanel QC plot."""
    fig, axes = plt.subplots(4, 2, figsize=(14, 16))
    fw = f" • firmware {rec.firmware}" if rec.firmware else ""
    fig.suptitle(f"Muse S Athena QC — {Path(rec.source).name}{fw}", fontsize=14)
    fig.text(
        0.5, 0.965,
        "fNIRS outputs are EXPERIMENTAL — see OpenMuse issue #23. "
        "EEG, IMU, BVP/HR/HRV are validated signal processing.",
        ha="center", fontsize=9, color="#b91c1c",
    )

    # 1) Filtered EEG, first 10 s
    ax = axes[0, 0]
    if not pre.eeg.empty:
        chans = [c for c in EEG_MAIN if c in pre.eeg.columns]
        sr = rec.sample_rates.get("EEG", 256.0)
        n = min(int(sr * 10), len(pre.eeg))
        t = pre.eeg["time"].to_numpy()[:n]
        for i, ch in enumerate(chans):
            ax.plot(t, pre.eeg[ch].to_numpy()[:n] + i * 200, lw=0.6, label=ch)
        ax.set_title("Filtered EEG (1-40 Hz, 60 Hz notch) — first 10 s, offset 200 µV/ch")
        ax.set_xlabel("time (s)"); ax.set_ylabel("µV (offset)")
        ax.legend(loc="upper right", fontsize=7)
    else:
        ax.text(0.5, 0.5, "no EEG", ha="center", va="center"); ax.set_axis_off()

    # 2) EEG mean PSD
    ax = axes[0, 1]
    if not feats.eeg_psd.empty:
        for ch, sub in feats.eeg_psd.groupby("channel"):
            ax.semilogy(sub["freq_hz"], sub["power_uV2_per_Hz"], lw=0.8, label=ch)
        ax.set_xlim(0, 50); ax.set_title("EEG mean PSD")
        ax.set_xlabel("Hz"); ax.set_ylabel("µV²/Hz")
        ax.legend(fontsize=7)
    else:
        ax.text(0.5, 0.5, "no PSD", ha="center", va="center"); ax.set_axis_off()

    # 3) Band power over time, alpha as a representative band, all channels
    ax = axes[1, 0]
    if not feats.eeg_band_power.empty:
        alpha = feats.eeg_band_power[feats.eeg_band_power["band"] == "alpha"]
        for ch, sub in alpha.groupby("channel"):
            ax.plot(sub["time"], sub["power_uV2"], label=ch, lw=0.9)
        ax.set_title("Alpha (8-13 Hz) band power over time")
        ax.set_xlabel("time (s)"); ax.set_ylabel("µV²"); ax.legend(fontsize=7)
    else:
        ax.text(0.5, 0.5, "no band power", ha="center", va="center"); ax.set_axis_off()

    # 4) ACC + GYRO magnitudes
    ax = axes[1, 1]
    if not pre.accgyro_features.empty:
        ax.plot(pre.accgyro_features["time"], pre.accgyro_features["ACC_MAG"], label="|ACC| (g)", lw=0.8)
        ax2 = ax.twinx()
        ax2.plot(pre.accgyro_features["time"], pre.accgyro_features["GYRO_MAG"], color="tab:orange", label="|GYRO| (°/s)", lw=0.8)
        ax.set_title("Movement"); ax.set_xlabel("time (s)")
        ax.set_ylabel("|ACC| g"); ax2.set_ylabel("|GYRO| °/s")
    else:
        ax.text(0.5, 0.5, "no IMU", ha="center", va="center"); ax.set_axis_off()

    # 5) BVP
    ax = axes[2, 0]
    if pre.bvp.size:
        sr = rec.sample_rates.get("OPTICS", 64.0)
        n = min(int(sr * 30), len(pre.bvp))
        t = np.arange(n) / sr
        ax.plot(t, pre.bvp[:n], lw=0.8)
        ax.set_title("Fused BVP — first 30 s"); ax.set_xlabel("time (s)")
    else:
        ax.text(0.5, 0.5, "no BVP", ha="center", va="center"); ax.set_axis_off()

    # 6) Heart rate over time
    ax = axes[2, 1]
    if not feats.heart_rate.empty:
        ax.plot(feats.heart_rate["window_start_s"], feats.heart_rate["hr_bpm"], "-o", ms=3)
        ax.set_title("Heart rate (BVP peak count)")
        ax.set_xlabel("window start (s)"); ax.set_ylabel("bpm")
    else:
        ax.text(0.5, 0.5, "no HR", ha="center", va="center"); ax.set_axis_off()

    # 7) fNIRS HbDiff per position  (EXPERIMENTAL)
    ax = axes[3, 0]
    if not pre.fnirs.empty:
        cols = [c for c in pre.fnirs.columns if c.endswith("_HbDiff")]
        for c in cols:
            ax.plot(pre.fnirs["time"], pre.fnirs[c], label=c, lw=0.8)
        ax.set_title("fNIRS HbDiff per position (HbO - HbR)  [EXPERIMENTAL]", color="#b91c1c")
        ax.set_xlabel("time (s)"); ax.set_ylabel("µM"); ax.legend(fontsize=7)
    else:
        ax.text(0.5, 0.5, "no fNIRS", ha="center", va="center"); ax.set_axis_off()

    # 8) EEG quality bars
    ax = axes[3, 1]
    if not qc.get("eeg", pd.DataFrame()).empty:
        eeg_q = qc["eeg"]
        ax.bar(eeg_q["channel"], eeg_q["sqi"])
        ax.set_ylim(0, 1); ax.set_title("EEG SQI (0=bad, 1=good)")
        ax.set_ylabel("SQI")
        for i, sat in enumerate(eeg_q["saturation_pct"]):
            ax.text(i, eeg_q["sqi"].iloc[i] + 0.02, f"{sat:.0f}% sat", ha="center", fontsize=7)
    else:
        ax.text(0.5, 0.5, "no EEG QC", ha="center", va="center"); ax.set_axis_off()

    fig.tight_layout(rect=(0, 0, 1, 0.97))
    path.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(path, dpi=120)
    plt.close(fig)
    return path


def export_all(rec: Recording, pre: Preprocessed, feats: Features, qc: Dict[str, pd.DataFrame], outdir: Path) -> Dict[str, Path]:
    outdir = Path(outdir)
    written = export_dataframes(rec, pre, feats, qc, outdir)
    fif = export_mne_fif(pre.eeg, rec.sample_rates.get("EEG", 256.0), outdir / "eeg-raw.fif")
    if fif is not None:
        written["mne_fif"] = fif
    written["qc_pdf"] = qc_report(rec, pre, feats, qc, outdir / "qc_report.pdf")
    return written
