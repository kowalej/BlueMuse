"""Muse S Athena offline analysis pipeline.

Stages:
    loaders     raw .txt (OpenMuse record output)  -> decoded DataFrames per modality
    preprocess  filter / scale / derive            -> cleaned DataFrames
    features    band power / HR / motion stats     -> feature DataFrames
    quality     saturation / SQI / summary         -> per-modality QC dicts
    export      parquet + MNE-FIF + QC report PDF  -> files on disk

Default sample rates (from OpenMuse decoder):
    EEG     256 Hz   8 channels (TP9, AF7, AF8, TP10 + AUX_1..4)
    ACCGYRO  52 Hz   ACC_X/Y/Z (g), GYRO_X/Y/Z (deg/s)
    OPTICS   64 Hz   16 channels (4 optodes x 4 wavelengths)
    BATTERY  ~1 Hz   battery_percent
"""
from .loaders import load_recording
from .preprocess import preprocess_all
from .features import compute_features
from .quality import compute_quality
from .export import export_all
from .runner import run_pipeline

__all__ = [
    "load_recording",
    "preprocess_all",
    "compute_features",
    "compute_quality",
    "export_all",
    "run_pipeline",
]
