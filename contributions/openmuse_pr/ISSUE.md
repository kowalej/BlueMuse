# Proposal: add `preprocess_eeg`, `eeg_band_power`, `hrv_from_bvp` to `process.py`

Hi — first, thank you for OpenMuse. It's the only working path to LSL/Python
streaming from the Muse S Athena that I'm aware of, and decoding into clean
labelled DataFrames is great.

While building a research pipeline on top of it I noticed three small gaps
I'd be happy to fill via PR if you're open to it. I wanted to scope-check
first so I don't waste a review cycle.

## Gaps

`process.py` currently has `preprocess_accgyro`, `preprocess_ppg`, and
`preprocess_fnirs` but no `preprocess_eeg`. And while `preprocess_ppg`
produces a clean fused BVP, there's no step to turn it into the standard
HRV indices (SDNN / RMSSD / pNN50 / LF–HF) that most cardiovascular
analyses use.

## Proposal

Three functions, all reusing `apply_butter_filter` and `decode.EEG_CHANNELS`,
no new dependencies beyond `scipy.signal` (already used):

| Function | Returns | Notes |
| --- | --- | --- |
| `preprocess_eeg(df, sampling_rate=256, hp_cutoff=1.0, lp_cutoff=40.0, notch_freq=60.0, notch_q=30.0, main_only=True)` | `pd.DataFrame` | Zero-phase bandpass + optional powerline notch. Mirrors the `preprocess_accgyro` signature/idiom. The bandpass also removes the standing ~700 µV DC bias from the Athena's ADC. |
| `eeg_band_power(df, sampling_rate=256, bands=None, win_sec=2.0, step_sec=1.0)` | long-form `pd.DataFrame` | Windowed band power via Welch + frequency integration. Default bands are the standard δ θ α β γ split. Returns `[time, channel, band, power_uV2]` for easy `groupby` analyses. |
| `hrv_from_bvp(bvp, sampling_rate=64, min_hr_bpm=30, max_hr_bpm=200)` | `(summary_df, ibis_df)` | Detects systolic peaks on the BVP produced by `preprocess_ppg` (whose polarity flip is already accounted for), drops physiologically implausible IBIs, and returns time-domain (SDNN/RMSSD/pNN50/mean HR) + frequency-domain (VLF/LF/HF, LF–HF ratio, normalised units) per the Task Force 1996 conventions. Frequency-domain returned as NaN when there is <30 s of beats, by design. |

A reference implementation matching your style is at the bottom of this
issue. It's been validated against a real Athena recording (firmware 3.1.23,
preset p1041, EEG 256 Hz / OPTICS 64 Hz) and the three functions chain
together as `decode → preprocess_eeg → eeg_band_power` and
`decode → preprocess_ppg → hrv_from_bvp`.

## Scope (deliberately staying out of fNIRS)

I saw #23 and your comment that the optics → fNIRS computation is still
unclear/unvalidated, plus a recent firmware update changed optics behavior.
The three functions here intentionally don't go near that area — two are
EEG signal processing (bandpass / notch / Welch band power, no Athena-specific
assumptions beyond the channel names) and the third works on the BVP that
`preprocess_ppg` already returns, which is a signal-processing-only product.
So the PR adds capability in the well-understood parts of the chain without
expanding the surface where firmware changes or assumption drift would hurt.

## Open questions before I send a PR

1. **Scope**: are you happy with all three landing in one PR, or would you
   prefer them split (e.g. EEG functions first, HRV as a follow-up)?
2. **Naming**: I followed your `preprocess_*` convention for EEG; for the
   HRV side I went with `hrv_from_bvp` so the dependency on `preprocess_ppg`
   is explicit. Happy to rename either to match a different preference.
3. **EU mains**: I defaulted `notch_freq=60.0`. Happy to change to `None`,
   or accept a list, or follow a different convention you prefer.
4. **AUX channels in `preprocess_eeg`**: I default to dropping them
   (`main_only=True`). Toggleable, but I'd like a sanity check that
   matches your view of how the AUX channels are typically used.
5. **Tests**: do you have a preferred test-data convention I should follow,
   or should the PR ship without tests (consistent with the current code)?

If the answer to (1)/(2)/(3)/(4) is "looks fine", I'll send a PR matching
your style with the three functions plus minimal docstring tweaks. If you'd
rather not have them upstream, I'll spin them out into a sister package
that depends on OpenMuse and move on.

Thanks again for the project.

---

### Reference implementation

<details>
<summary>process.py additions (drop-in to the existing module)</summary>

```python
# (Paste contents of contributions/openmuse_pr/process_eeg_hrv.py here in the
# actual issue, or upload as a gist and link to it. The two `# =================`
# section headers match the file's current style.)
```

</details>
