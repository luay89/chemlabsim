// ChemLabSim v3 — Audio Placeholder Generator (Editor Only)
// Generates placeholder .wav files in Resources/Audio/ so the AudioService
// always has sounds to play, even before real sound effects are recorded.
// Run via: Tools > ChemLabSim > Generate Placeholder Audio

using UnityEngine;
using UnityEditor;
using System.IO;

namespace ChemLabSimV3.Editor
{
    public static class AudioPlaceholderGenerator
    {
        private static readonly string OutputPath = "Assets/_ProjectV3/Resources/Audio";

        [MenuItem("Tools/ChemLabSim/Generate Placeholder Audio")]
        public static void GenerateAllPlaceholders()
        {
            if (!Directory.Exists(OutputPath))
                Directory.CreateDirectory(OutputPath);

            // Define all placeholder sounds
            var sounds = new (string name, float freq, float duration, string waveform)[]
            {
                ("sfx_ui_click",       800f,   0.1f,  "click"),
                ("sfx_ui_hover",       600f,   0.05f, "click"),
                ("sfx_ui_error",       200f,   0.3f,  "buzz"),
                ("sfx_ui_success",     880f,   0.3f,  "double"),
                ("sfx_ui_levelup",     1047f,  0.5f,  "ascending"),
                ("sfx_ui_achievement", 1319f,  0.6f,  "fanfare"),
                ("sfx_lab_mix",        440f,   0.4f,  "mix"),
                ("sfx_lab_stir",       220f,   0.8f,  "loop"),
                ("sfx_lab_pour",       350f,   1.0f,  "noise"),
                ("sfx_lab_bubble",     500f,   0.3f,  "bubble"),
                ("sfx_lab_fizz",       300f,   0.5f,  "noise"),
                ("sfx_lab_sizzle",     400f,   0.6f,  "noise"),
                ("sfx_lab_gas_release", 250f,  0.4f,  "noise"),
                ("sfx_lab_precipitate", 150f,  0.2f,  "click"),
                ("sfx_lab_heat_on",    100f,   0.5f,  "rumble"),
                ("sfx_lab_heat_off",   100f,   0.3f,  "click"),
                ("sfx_rxn_success",    660f,   0.4f,  "double"),
                ("sfx_rxn_partial",    330f,   0.3f,  "buzz"),
                ("sfx_rxn_fail",       160f,   0.4f,  "buzz"),
                ("sfx_rxn_explosion",  60f,    0.8f,  "explosion"),
                ("sfx_rxn_burn",       200f,   0.6f,  "noise"),
                ("sfx_exp_step_complete", 880f, 0.2f, "click"),
                ("sfx_exp_perfect",    1047f,  0.8f,  "fanfare"),
                ("sfx_exp_passing",    660f,   0.4f,  "double"),
                ("sfx_exp_failing",    200f,   0.3f,  "buzz"),
                ("sfx_fallback_beep",  440f,   0.15f, "click"),
            };

            int totalGenerated = 0;
            foreach (var (name, freq, duration, waveform) in sounds)
            {
                if (GenerateWav(name, freq, duration, waveform))
                    totalGenerated++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[AudioPlaceholderGenerator] Generated {totalGenerated} placeholder audio files in {OutputPath}");
        }

        private static bool GenerateWav(string name, float frequency, float duration, string waveform)
        {
            string filePath = Path.Combine(OutputPath, name + ".wav");
            if (File.Exists(filePath))
            {
                // Don't overwrite existing files (real audio takes priority)
                return false;
            }

            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * (1f / duration) * 4f);

                samples[i] = waveform switch
                {
                    "click"    => Mathf.Sin(2 * Mathf.PI * frequency * t) * envelope * 0.5f,
                    "buzz"     => (Mathf.Sin(2 * Mathf.PI * frequency * t) * 0.5f
                                 + Mathf.Sin(2 * Mathf.PI * frequency * 2 * t) * 0.3f
                                 + Mathf.Sin(2 * Mathf.PI * frequency * 3 * t) * 0.2f) * envelope,
                    "double"   => (Mathf.Sin(2 * Mathf.PI * frequency * t) * envelope
                                 + Mathf.Sin(2 * Mathf.PI * frequency * 1.5f * t) * Mathf.Exp(-(t - 0.15f) * 20f) * 0.5f) * 0.5f,
                    "ascending"=> (Mathf.Sin(2 * Mathf.PI * (frequency + t * 1000f) * t) * envelope) * 0.4f,
                    "fanfare"  => (Mathf.Sin(2 * Mathf.PI * frequency * t) * envelope * 0.4f
                                 + Mathf.Sin(2 * Mathf.PI * frequency * 1.25f * t) * envelope * 0.3f
                                 + Mathf.Sin(2 * Mathf.PI * frequency * 1.5f * t) * envelope * 0.2f) * 0.5f,
                    "mix"      => (Mathf.Sin(2 * Mathf.PI * frequency * t) * 0.4f
                                 + Random.value * 0.2f) * envelope,
                    "loop"     => Mathf.Sin(2 * Mathf.PI * frequency * t) * 0.3f * (0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * 3f * t)),
                    "noise"    => (Random.value * 2f - 1f) * envelope * 0.3f,
                    "bubble"   => Mathf.Sin(2 * Mathf.PI * (frequency + Mathf.Sin(2 * Mathf.PI * 15f * t) * 150f) * t)
                                 * Mathf.Exp(-t * 8f) * 0.4f,
                    "rumble"   => (Mathf.Sin(2 * Mathf.PI * frequency * t) * 0.3f
                                 + Mathf.Sin(2 * Mathf.PI * frequency * 0.5f * t) * 0.5f) * envelope,
                    "explosion"=> (Random.value * 2f - 1f) * Mathf.Exp(-t * 3f) * 0.8f,
                    _          => Mathf.Sin(2 * Mathf.PI * frequency * t) * envelope * 0.5f,
                };
            }

            // Write WAV file
            using var stream = new FileStream(filePath, FileMode.Create);
            using var writer = new BinaryWriter(stream);

            // WAV header
            int dataSize = sampleCount * 2; // 16-bit mono
            int fileSize = 36 + dataSize;

            writer.Write(new char[] { 'R', 'I', 'F', 'F' });
            writer.Write(fileSize);
            writer.Write(new char[] { 'W', 'A', 'V', 'E' });
            writer.Write(new char[] { 'f', 'm', 't', ' ' });
            writer.Write(16);         // chunk size
            writer.Write((short)1);   // PCM format
            writer.Write((short)1);   // mono
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2); // byte rate
            writer.Write((short)2);   // block align
            writer.Write((short)16);  // bits per sample
            writer.Write(new char[] { 'd', 'a', 't', 'a' });
            writer.Write(dataSize);

            // Write samples as 16-bit PCM
            foreach (float sample in samples)
            {
                short val = (short)(Mathf.Clamp(sample, -1f, 1f) * 32767);
                writer.Write(val);
            }

            return true;
        }
    }
}
