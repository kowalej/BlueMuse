"""CLI: python -m muse_pipeline run <input.txt> <outdir>"""
import argparse
from pathlib import Path
from .runner import run_pipeline


def main():
    p = argparse.ArgumentParser(prog="muse_pipeline")
    sub = p.add_subparsers(dest="cmd", required=True)

    r = sub.add_parser("run", help="Run pipeline on an OpenMuse .txt recording")
    r.add_argument("input", type=Path, help="Input .txt from `OpenMuse record`")
    r.add_argument("outdir", type=Path, help="Output directory for artifacts")
    r.add_argument("--firmware", type=str, default=None,
                   help="Device firmware (e.g. 3.1.23). Overrides recording_metadata.json.")

    args = p.parse_args()
    if args.cmd == "run":
        run_pipeline(args.input, args.outdir, firmware=args.firmware)


if __name__ == "__main__":
    main()
