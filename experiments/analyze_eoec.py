"""Analyse an eyes-open / eyes-closed run.

1. Runs the standard pipeline on raw.txt -> out/.
2. Loads events.csv, aligns timestamps to the recording.
3. Computes per-condition band power from filtered EEG.
4. Writes condition_band_power.csv + eoec_report.pdf with:
       - α (8-13 Hz) power per channel, open vs closed (the canonical test)
       - PSD overlay per condition
       - Open/closed ratio with statistical significance per channel (Welch's t-test)
"""
from __future__ import annotations
import argparse
from pathlib import Path
import numpy as np
import pandas as pd
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from scipy import stats

from muse_pipeline.runner import run_pipeline
from muse_pipeline.loaders import load_recording
from muse_pipeline.preprocess import preprocess_all, EEG_MAIN
from muse_pipeline.events import load_events, align_events, first_packet_iso
from muse_pipeline.epochs import band_power_by_condition
from muse_pipeline.features import EEG_BANDS


def _sub_epoch(events: pd.DataFrame, epoch_s: float = 2.0) -> pd.DataFrame:
    """Split each long block into sequential non-overlapping epoch_s sub-trials.
    This is what gives us multiple samples per condition for statistics."""
    rows = []
    for _, ev in events.iterrows():
        dur = float(ev["duration_s"]) if ev["duration_s"] else 0.0
        if dur < epoch_s:
            continue
        n_sub = int(dur // epoch_s)
        for k in range(n_sub):
            rows.append({
                "onset_iso": ev["onset_iso"],
                "onset_s": float(ev["onset_s"]) + k * epoch_s,
                "label": ev["label"],
                "duration_s": epoch_s,
                "notes": f"sub_{k}",
            })
    return pd.DataFrame(rows)


def analyze(rundir: Path, epoch_s: float = 2.0) -> Path:
    raw_txt = rundir / "raw.txt"
    events_csv = rundir / "events.csv"
    out = rundir / "analysis"
    out.mkdir(exist_ok=True)

    # 1) Full pipeline.
    run_pipeline(raw_txt, out / "pipeline")

    # 2) Reload filtered EEG (already saved by pipeline) so we don't recompute.
    rec = load_recording(raw_txt)
    pre = preprocess_all(rec)
    sr = rec.sample_rates.get("EEG", 256.0)

    # 3) Align events.
    t0_iso = first_packet_iso(raw_txt)
    events = load_events(events_csv)
    events = align_events(events, t0_iso)
    cond_events = events[events["label"].isin(["eyes_open", "eyes_closed"])].copy()
    sub_events = _sub_epoch(cond_events, epoch_s=epoch_s)
    sub_events.to_csv(out / "sub_events.csv", index=False)

    # 4) Band power per sub-epoch.
    bp = band_power_by_condition(pre.eeg, sub_events, sr=sr, bands=EEG_BANDS)
    bp.to_csv(out / "condition_band_power.csv", index=False)

    # 5) Stats: Welch's t-test, eyes_closed > eyes_open in alpha.
    stat_rows = []
    for band in EEG_BANDS:
        for ch in [c for c in EEG_MAIN if c in pre.eeg.columns]:
            sub = bp[(bp["band"] == band) & (bp["channel"] == ch)]
            o = sub.loc[sub["label"] == "eyes_open", "power_uV2"].to_numpy()
            c = sub.loc[sub["label"] == "eyes_closed", "power_uV2"].to_numpy()
            if len(o) < 2 or len(c) < 2:
                continue
            t, p = stats.ttest_ind(c, o, equal_var=False)
            stat_rows.append({
                "band": band, "channel": ch,
                "n_open": len(o), "n_closed": len(c),
                "mean_open_uV2": float(o.mean()), "mean_closed_uV2": float(c.mean()),
                "ratio_closed_over_open": float(c.mean() / o.mean()) if o.mean() > 0 else float("nan"),
                "t": float(t), "p_value": float(p),
            })
    stats_df = pd.DataFrame(stat_rows).sort_values(["band", "channel"])
    stats_df.to_csv(out / "stats.csv", index=False)

    # 6) Plot.
    fig, axes = plt.subplots(2, 2, figsize=(13, 10))
    fig.suptitle(f"Eyes-open vs eyes-closed — {rundir.name}", fontsize=13)

    # α power per channel, two boxes per channel.
    ax = axes[0, 0]
    alpha = bp[bp["band"] == "alpha"]
    chans = sorted(alpha["channel"].unique())
    open_data   = [alpha.loc[(alpha["channel"] == ch) & (alpha["label"] == "eyes_open"),   "power_uV2"].to_numpy() for ch in chans]
    closed_data = [alpha.loc[(alpha["channel"] == ch) & (alpha["label"] == "eyes_closed"), "power_uV2"].to_numpy() for ch in chans]
    x = np.arange(len(chans))
    ax.boxplot(open_data,   positions=x - 0.18, widths=0.3, patch_artist=True,
               boxprops=dict(facecolor="#9ec5fe"))
    ax.boxplot(closed_data, positions=x + 0.18, widths=0.3, patch_artist=True,
               boxprops=dict(facecolor="#f8b4b4"))
    ax.set_xticks(x); ax.set_xticklabels(chans)
    ax.set_title(f"Alpha (8-13 Hz) power per channel  •  {epoch_s:.0f}s epochs")
    ax.set_ylabel("µV²")
    ax.legend([plt.Rectangle((0,0),1,1,fc="#9ec5fe"), plt.Rectangle((0,0),1,1,fc="#f8b4b4")],
              ["eyes_open", "eyes_closed"], loc="upper right")

    # PSD overlay (whole-block PSD per condition).
    ax = axes[0, 1]
    from scipy import signal as _sig
    for cond, color in [("eyes_open", "tab:blue"), ("eyes_closed", "tab:red")]:
        blocks = events[events["label"] == cond]
        psds_per_ch = {ch: [] for ch in chans}
        for _, ev in blocks.iterrows():
            onset = float(ev["onset_s"]); dur = float(ev["duration_s"])
            t = pre.eeg["time"].to_numpy()
            mask = (t >= onset) & (t < onset + dur)
            if mask.sum() < int(sr): continue
            for ch in chans:
                seg = pre.eeg.loc[mask, ch].to_numpy()
                f, P = _sig.welch(seg, fs=sr, nperseg=min(int(sr*2), len(seg)))
                psds_per_ch[ch].append(P)
        if not any(psds_per_ch.values()):
            continue
        # Average PSD across channels and blocks for a compact overlay.
        avg = np.mean([np.mean(np.array(v), axis=0) for v in psds_per_ch.values() if v], axis=0)
        ax.semilogy(f, avg, label=cond, color=color, lw=1.2)
    ax.axvspan(8, 13, alpha=0.1, color="green")
    ax.set_xlim(1, 40); ax.set_title("Channel-averaged PSD per condition  •  α band shaded")
    ax.set_xlabel("Hz"); ax.set_ylabel("µV²/Hz"); ax.legend()

    # Ratio closed/open with significance.
    ax = axes[1, 0]
    if not stats_df.empty:
        alpha_stats = stats_df[stats_df["band"] == "alpha"]
        sig = alpha_stats["p_value"] < 0.05
        colors = ["#16a34a" if s else "#9ca3af" for s in sig]
        ax.bar(alpha_stats["channel"], alpha_stats["ratio_closed_over_open"], color=colors)
        ax.axhline(1.0, ls="--", color="k", lw=0.8)
        for i, (_, r) in enumerate(alpha_stats.iterrows()):
            ax.text(i, r["ratio_closed_over_open"] + 0.02,
                    f"p={r['p_value']:.3g}", ha="center", fontsize=8)
        ax.set_title("α power ratio (closed / open)  •  green = p<0.05 (Welch t-test)")
        ax.set_ylabel("ratio")
    else:
        ax.text(0.5, 0.5, "not enough data", ha="center", va="center"); ax.set_axis_off()

    # All bands ratio table.
    ax = axes[1, 1]
    ax.axis("off")
    if not stats_df.empty:
        pivot = stats_df.pivot(index="band", columns="channel", values="ratio_closed_over_open")
        pivot = pivot.reindex(list(EEG_BANDS.keys()))
        tbl = ax.table(cellText=np.round(pivot.values, 2),
                       rowLabels=pivot.index, colLabels=pivot.columns,
                       loc="center", cellLoc="center")
        tbl.auto_set_font_size(False); tbl.set_fontsize(9); tbl.scale(1.0, 1.4)
        ax.set_title("closed / open power ratio per band")

    fig.tight_layout(rect=(0, 0, 1, 0.96))
    report = out / "eoec_report.pdf"
    fig.savefig(report, dpi=120)
    plt.close(fig)
    print(f"wrote {report}")
    return out


def main():
    p = argparse.ArgumentParser()
    p.add_argument("rundir", type=Path, help="Directory holding raw.txt + events.csv")
    p.add_argument("--epoch", type=float, default=2.0, help="Sub-epoch length in seconds (default 2.0)")
    args = p.parse_args()
    analyze(args.rundir, epoch_s=args.epoch)


if __name__ == "__main__":
    main()
