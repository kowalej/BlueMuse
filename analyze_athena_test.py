"""Decode athena_test.txt (raw BLE notifications) into DataFrames and summarize."""
from pathlib import Path
import numpy as np
from OpenMuse.decode import decode_rawdata

INFILE = Path(__file__).parent / "athena_test.txt"

with open(INFILE) as f:
    messages = f.read().splitlines()
print(f"Loaded {len(messages)} raw notification lines from {INFILE.name}\n")

data = decode_rawdata(messages)

for name, df in data.items():
    if df.empty:
        print(f"{name}: empty")
        continue
    n = len(df)
    t = df["time"].to_numpy()
    duration = t[-1] - t[0]
    rate = (n - 1) / duration if duration > 0 else float("nan")
    chan_cols = [c for c in df.columns if c != "time"]
    print(f"{name}: {n} samples over {duration:.2f}s  →  ~{rate:.1f} Hz")
    print(f"  channels: {chan_cols}")
    if name in ("EEG", "OPTICS", "ACCGYRO"):
        arr = df[chan_cols].to_numpy()
        print(f"  per-channel mean : {np.array2string(arr.mean(axis=0), precision=2)}")
        print(f"  per-channel std  : {np.array2string(arr.std(axis=0),  precision=2)}")
        print(f"  per-channel min  : {np.array2string(arr.min(axis=0),  precision=2)}")
        print(f"  per-channel max  : {np.array2string(arr.max(axis=0),  precision=2)}")
    elif name == "BATTERY":
        print(f"  battery_percent: {df['battery_percent'].iloc[0]:.2f} → {df['battery_percent'].iloc[-1]:.2f}")
    print()
