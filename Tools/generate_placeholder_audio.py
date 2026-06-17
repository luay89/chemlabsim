#!/usr/bin/env python3
"""Generate placeholder WAV files for ChemLabSim audio system.
Run this from the project root: python3 Tools/generate_placeholder_audio.py
Output goes to Assets/_ProjectV3/Resources/Audio/
"""

import os
import struct
import math
import random

OUTPUT_DIR = "Assets/_ProjectV3/Resources/Audio"
SAMPLE_RATE = 44100

SOUNDS = [
    # (name, freq, duration_sec, waveform_type)
    ("sfx_ui_click", 800, 0.1, "click"),
    ("sfx_ui_hover", 600, 0.05, "click"),
    ("sfx_ui_error", 200, 0.3, "buzz"),
    ("sfx_ui_success", 880, 0.3, "double"),
    ("sfx_ui_levelup", 1047, 0.5, "ascending"),
    ("sfx_ui_achievement", 1319, 0.6, "fanfare"),
    ("sfx_lab_mix", 440, 0.4, "mix"),
    ("sfx_lab_stir", 220, 0.8, "loop"),
    ("sfx_lab_pour", 350, 1.0, "noise"),
    ("sfx_lab_bubble", 500, 0.3, "bubble"),
    ("sfx_lab_fizz", 300, 0.5, "noise"),
    ("sfx_lab_sizzle", 400, 0.6, "noise"),
    ("sfx_lab_gas_release", 250, 0.4, "noise"),
    ("sfx_lab_precipitate", 150, 0.2, "click"),
    ("sfx_lab_heat_on", 100, 0.5, "rumble"),
    ("sfx_lab_heat_off", 100, 0.3, "click"),
    ("sfx_rxn_success", 660, 0.4, "double"),
    ("sfx_rxn_partial", 330, 0.3, "buzz"),
    ("sfx_rxn_fail", 160, 0.4, "buzz"),
    ("sfx_rxn_explosion", 60, 0.8, "explosion"),
    ("sfx_rxn_burn", 200, 0.6, "noise"),
    ("sfx_exp_step_complete", 880, 0.2, "click"),
    ("sfx_exp_perfect", 1047, 0.8, "fanfare"),
    ("sfx_exp_passing", 660, 0.4, "double"),
    ("sfx_exp_failing", 200, 0.3, "buzz"),
    ("sfx_fallback_beep", 440, 0.15, "click"),
]


def generate_wav(filename, freq, duration, wavetype):
    filepath = os.path.join(OUTPUT_DIR, filename)
    if os.path.exists(filepath):
        return False  # don't overwrite

    num_samples = int(SAMPLE_RATE * duration)
    samples = []

    for i in range(num_samples):
        t = i / SAMPLE_RATE
        env = math.exp(-t * (1.0 / duration) * 4.0)

        if wavetype == "click":
            val = math.sin(2 * math.pi * freq * t) * env * 0.5
        elif wavetype == "buzz":
            val = (math.sin(2 * math.pi * freq * t) * 0.5 +
                   math.sin(2 * math.pi * freq * 2 * t) * 0.3 +
                   math.sin(2 * math.pi * freq * 3 * t) * 0.2) * env
        elif wavetype == "double":
            val = (math.sin(2 * math.pi * freq * t) * env +
                   math.sin(2 * math.pi * freq * 1.5 * t) * math.exp(-max(0, t - 0.15) * 20) * 0.5) * 0.5
        elif wavetype == "ascending":
            val = math.sin(2 * math.pi * (freq + t * 1000) * t) * env * 0.4
        elif wavetype == "fanfare":
            val = (math.sin(2 * math.pi * freq * t) * env * 0.4 +
                   math.sin(2 * math.pi * freq * 1.25 * t) * env * 0.3 +
                   math.sin(2 * math.pi * freq * 1.5 * t) * env * 0.2) * 0.5
        elif wavetype == "mix":
            val = (math.sin(2 * math.pi * freq * t) * 0.4 +
                   (random.random() * 2 - 1) * 0.2) * env
        elif wavetype == "loop":
            val = math.sin(2 * math.pi * freq * t) * 0.3 * (0.5 + 0.5 * math.sin(2 * math.pi * 3 * t))
        elif wavetype == "noise":
            val = (random.random() * 2 - 1) * env * 0.3
        elif wavetype == "bubble":
            val = (math.sin(2 * math.pi * (freq + math.sin(2 * math.pi * 15 * t) * 150) * t) *
                   math.exp(-t * 8) * 0.4)
        elif wavetype == "rumble":
            val = (math.sin(2 * math.pi * freq * t) * 0.3 +
                   math.sin(2 * math.pi * freq * 0.5 * t) * 0.5) * env
        elif wavetype == "explosion":
            val = (random.random() * 2 - 1) * math.exp(-t * 3) * 0.8
        else:
            val = math.sin(2 * math.pi * freq * t) * env * 0.5

        # Clamp to 16-bit range
        val = max(-1.0, min(1.0, val))
        samples.append(int(val * 32767))

    # Write WAV file
    data_size = num_samples * 2
    file_size = 36 + data_size

    with open(filepath, "wb") as f:
        # RIFF header
        f.write(b"RIFF")
        f.write(struct.pack("<I", file_size))
        f.write(b"WAVE")

        # fmt chunk
        f.write(b"fmt ")
        f.write(struct.pack("<I", 16))  # chunk size
        f.write(struct.pack("<H", 1))   # PCM
        f.write(struct.pack("<H", 1))   # mono
        f.write(struct.pack("<I", SAMPLE_RATE))
        f.write(struct.pack("<I", SAMPLE_RATE * 2))  # byte rate
        f.write(struct.pack("<H", 2))   # block align
        f.write(struct.pack("<H", 16))  # bits per sample

        # data chunk
        f.write(b"data")
        f.write(struct.pack("<I", data_size))

        # samples
        for s in samples:
            f.write(struct.pack("<h", s))

    return True


def main():
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    count = 0
    for name, freq, dur, wtype in SOUNDS:
        filename = f"{name}.wav"
        if generate_wav(filename, freq, dur, wtype):
            count += 1
            print(f"  Created: {filename}")
        else:
            print(f"  Skipped (exists): {filename}")

    print(f"\nGenerated {count} placeholder audio files in {OUTPUT_DIR}/")
    print("Replace them with real recordings when available.")


if __name__ == "__main__":
    main()
