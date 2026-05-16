"""Cued eyes-open / eyes-closed recording.

Spawns `OpenMuse record` as a subprocess to dump raw BLE packets to disk,
while this script drives on-screen cues and writes a synchronised events.csv.

Both files share the same wall-clock (UTC) timestamps, so events align with
the recording by matching ISO timestamps after the fact.

Default protocol:
    5 s ready countdown
    block 1: 30 s eyes open
    block 2: 30 s eyes closed
    block 3: 30 s eyes open
    block 4: 30 s eyes closed
    (= 125 s total)

Usage:
    python -m experiments.eyes_open_closed --address <muse-addr> --outdir runs/eoec_$(date +%Y%m%d_%H%M%S)
"""
from __future__ import annotations
import argparse
import json
import re
import shutil
import subprocess
import sys
import threading
import time
from pathlib import Path

from muse_pipeline.events import make_event, save_events, now_iso
from muse_pipeline.loaders import SIDECAR_NAME

# Matches lines like: "Connected to device (firmware 3.1.23): battery 28.75%"
_RE_DEVICE = re.compile(r"firmware\s+(\S+).*?battery\s+([\d.]+)", re.IGNORECASE)

DEFAULT_BLOCKS = [
    ("eyes_open",   30.0),
    ("eyes_closed", 30.0),
    ("eyes_open",   30.0),
    ("eyes_closed", 30.0),
]


def _ready(seconds: int) -> None:
    print(f"\nGet ready. Recording in...")
    for i in range(seconds, 0, -1):
        print(f"  {i}", flush=True)
        time.sleep(1)


def _print_cue(label: str, duration: float) -> None:
    if label == "eyes_open":
        msg = f"  ▸ EYES OPEN — fix gaze on a calm point   ({duration:.0f} s)"
    elif label == "eyes_closed":
        msg = f"  ▸ EYES CLOSED — relax, breathe normally  ({duration:.0f} s)"
    else:
        msg = f"  ▸ {label}                                ({duration:.0f} s)"
    print("\n" + msg, flush=True)


def run_protocol(address: str, outdir: Path, blocks=DEFAULT_BLOCKS, ready_s: int = 5,
                 record_cmd: str = "OpenMuse") -> Path:
    if shutil.which(record_cmd) is None:
        sys.exit(f"`{record_cmd}` not on PATH — activate the muse conda env first.")
    outdir.mkdir(parents=True, exist_ok=True)
    raw_path = outdir / "raw.txt"
    events_path = outdir / "events.csv"

    total_s = ready_s + sum(d for _, d in blocks) + 2  # +2 s tail buffer
    cmd = [record_cmd, "record", "--address", address, "--duration", str(int(total_s)),
           "--outfile", str(raw_path)]
    print(f"Spawning recorder: {' '.join(cmd)}\n(total {total_s} s)")
    env = {"PYTHONUNBUFFERED": "1"}
    proc = subprocess.Popen(
        cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
        env={**__import__("os").environ, **env},
    )

    # Tee stdout: print live AND parse out firmware/battery on the first "Connected to device" line.
    captured = {"firmware": None, "battery_start_pct": None}

    def _pump():
        for line in proc.stdout:  # type: ignore[union-attr]
            sys.stdout.write(line)
            sys.stdout.flush()
            if captured["firmware"] is None:
                m = _RE_DEVICE.search(line)
                if m:
                    captured["firmware"] = m.group(1)
                    captured["battery_start_pct"] = float(m.group(2))

    pump_thread = threading.Thread(target=_pump, daemon=True)
    pump_thread.start()

    events: list[dict] = []
    try:
        events.append(make_event("session_start", duration_s=0.0))
        _ready(ready_s)
        for label, dur in blocks:
            iso = now_iso()
            _print_cue(label, dur)
            events.append(make_event(label, duration_s=dur, onset_iso=iso))
            time.sleep(dur)
        events.append(make_event("session_end", duration_s=0.0))
        print("\nDone cuing. Waiting for recorder to flush...")
        proc.wait(timeout=total_s + 30)
    except KeyboardInterrupt:
        print("\nInterrupted; stopping recorder.")
        proc.terminate()
        proc.wait(timeout=10)
        events.append(make_event("session_aborted", duration_s=0.0))

    save_events(events, events_path)

    # Sidecar metadata next to raw.txt so the loader can pick up firmware/device info.
    sidecar = {
        "device_address": address,
        "firmware": captured["firmware"],
        "battery_start_pct": captured["battery_start_pct"],
        "protocol": "eyes_open_closed",
        "blocks": [{"label": l, "duration_s": d} for l, d in blocks],
    }
    (outdir / SIDECAR_NAME).write_text(json.dumps(sidecar, indent=2))

    print(f"\nRecording: {raw_path}")
    print(f"Events:    {events_path}")
    print(f"Metadata:  {outdir / SIDECAR_NAME}")
    return outdir


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--address", required=True, help="Muse address (CoreBluetooth UUID on macOS)")
    p.add_argument("--outdir",  required=True, type=Path, help="Where to write raw.txt + events.csv")
    p.add_argument("--ready",   type=int, default=5, help="Pre-roll countdown in seconds (default 5)")
    p.add_argument("--block",   type=float, default=30.0, help="Block duration in seconds (default 30)")
    p.add_argument("--n-blocks", type=int, default=4, help="Number of alternating O/C blocks (default 4)")
    p.add_argument("--first",   choices=("open", "closed"), default="open")
    args = p.parse_args()

    seq = ["eyes_open", "eyes_closed"] if args.first == "open" else ["eyes_closed", "eyes_open"]
    blocks = [(seq[i % 2], args.block) for i in range(args.n_blocks)]
    run_protocol(args.address, args.outdir, blocks=blocks, ready_s=args.ready)


if __name__ == "__main__":
    main()
