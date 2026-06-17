// ChemLabSim v3 — Audio Library
// Defines all sound effect IDs and provides a centralized registry.
// Services can use these IDs to play appropriate sounds at runtime.
// Actual .wav/.ogg files should be placed in Assets/_ProjectV3/Resources/Audio/

using System.Collections.Generic;
using UnityEngine;

namespace ChemLabSimV3.Services
{
    /// <summary>
    /// Central registry of all sound effect IDs used throughout the application.
    /// Maps logical sound names to resource paths for loading AudioClips.
    /// </summary>
    public static class AudioLibrary
    {
        // ── UI Sounds ──
        public const string UiClick = "sfx_ui_click";
        public const string UiHover = "sfx_ui_hover";
        public const string UiError = "sfx_ui_error";
        public const string UiSuccess = "sfx_ui_success";
        public const string UiLevelUp = "sfx_ui_levelup";
        public const string UiAchievement = "sfx_ui_achievement";

        // ── Lab Sounds ──
        public const string LabMix = "sfx_lab_mix";
        public const string LabStir = "sfx_lab_stir";
        public const string LabGrind = "sfx_lab_grind";
        public const string LabPour = "sfx_lab_pour";
        public const string LabBubble = "sfx_lab_bubble";
        public const string LabFizz = "sfx_lab_fizz";
        public const string LabSizzle = "sfx_lab_sizzle";
        public const string LabGasRelease = "sfx_lab_gas_release";
        public const string LabPrecipitate = "sfx_lab_precipitate";
        public const string LabHeatOn = "sfx_lab_heat_on";
        public const string LabHeatOff = "sfx_lab_heat_off";

        // ── Reaction Sounds ──
        public const string ReactionSuccess = "sfx_rxn_success";
        public const string ReactionPartial = "sfx_rxn_partial";
        public const string ReactionFail = "sfx_rxn_fail";
        public const string ReactionExplosion = "sfx_rxn_explosion";
        public const string ReactionBurn = "sfx_rxn_burn";

        // ── Guided Experiment Sounds ──
        public const string ExpStepComplete = "sfx_exp_step_complete";
        public const string ExpPerfect = "sfx_exp_perfect";
        public const string ExpPassing = "sfx_exp_passing";
        public const string ExpFailing = "sfx_exp_failing";

        // ── Fallback ──
        public const string FallbackBeep = "sfx_fallback_beep";

        /// <summary>
        /// Maps sound IDs to expected Resource paths.
        /// All audio files should be in Assets/_ProjectV3/Resources/Audio/.
        /// </summary>
        private static readonly Dictionary<string, string> SoundPathMap = new Dictionary<string, string>
        {
            { UiClick, "Audio/sfx_ui_click" },
            { UiHover, "Audio/sfx_ui_hover" },
            { UiError, "Audio/sfx_ui_error" },
            { UiSuccess, "Audio/sfx_ui_success" },
            { UiLevelUp, "Audio/sfx_ui_levelup" },
            { UiAchievement, "Audio/sfx_ui_achievement" },
            { LabMix, "Audio/sfx_lab_mix" },
            { LabStir, "Audio/sfx_lab_stir" },
            { LabPour, "Audio/sfx_lab_pour" },
            { LabBubble, "Audio/sfx_lab_bubble" },
            { LabFizz, "Audio/sfx_lab_fizz" },
            { LabSizzle, "Audio/sfx_lab_sizzle" },
            { LabGasRelease, "Audio/sfx_lab_gas_release" },
            { LabPrecipitate, "Audio/sfx_lab_precipitate" },
            { LabHeatOn, "Audio/sfx_lab_heat_on" },
            { LabHeatOff, "Audio/sfx_lab_heat_off" },
            { ReactionSuccess, "Audio/sfx_rxn_success" },
            { ReactionPartial, "Audio/sfx_rxn_partial" },
            { ReactionFail, "Audio/sfx_rxn_fail" },
            { ReactionExplosion, "Audio/sfx_rxn_explosion" },
            { ReactionBurn, "Audio/sfx_rxn_burn" },
            { ExpStepComplete, "Audio/sfx_exp_step_complete" },
            { ExpPerfect, "Audio/sfx_exp_perfect" },
            { ExpPassing, "Audio/sfx_exp_passing" },
            { ExpFailing, "Audio/sfx_exp_failing" },
            { FallbackBeep, "Audio/sfx_fallback_beep" },
        };

        /// <summary>
        /// Get the Resources path for a sound ID.
        /// Returns null if the ID is not registered.
        /// </summary>
        public static string GetPath(string soundId)
        {
            if (string.IsNullOrEmpty(soundId)) return null;
            return SoundPathMap.TryGetValue(soundId, out var path) ? path : null;
        }

        /// <summary>
        /// Try to load an AudioClip for a sound ID from Resources.
        /// Returns null if the clip is not found.
        /// </summary>
        public static AudioClip LoadClip(string soundId)
        {
            var path = GetPath(soundId);
            if (path == null) return null;
            return Resources.Load<AudioClip>(path);
        }

        /// <summary>
        /// Check if a sound ID exists in the library.
        /// </summary>
        public static bool HasSound(string soundId)
        {
            return !string.IsNullOrEmpty(soundId) && SoundPathMap.ContainsKey(soundId);
        }

        /// <summary>
        /// Get all registered sound IDs.
        /// </summary>
        public static IEnumerable<string> GetAllSoundIds()
        {
            return SoundPathMap.Keys;
        }

        /// <summary>
        /// Create placeholder beep audio clips at runtime when real audio files are not available.
        /// This generates simple sine wave tones programmatically.
        /// </summary>
        public static AudioClip GeneratePlaceholderTone(string soundId, float frequency = 440f, float duration = 0.2f)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;

                // Different sound types get different wave shapes
                switch (soundId)
                {
                    case UiClick:
                        samples[i] = Mathf.Sin(2 * Mathf.PI * 800 * t) * Mathf.Exp(-t * 20f);
                        break;
                    case UiError:
                        samples[i] = Mathf.Sin(2 * Mathf.PI * 200 * t) * Mathf.Exp(-t * 5f);
                        break;
                    case UiSuccess:
                        samples[i] = Mathf.Sin(2 * Mathf.PI * 523 * t) * Mathf.Exp(-t * 8f) * 0.5f
                                   + Mathf.Sin(2 * Mathf.PI * 659 * t) * Mathf.Exp(-t * 8f) * 0.3f;
                        break;
                    case LabBubble:
                        samples[i] = Mathf.Sin(2 * Mathf.PI * (300 + Mathf.Sin(2 * Mathf.PI * 10 * t) * 100) * t) * Mathf.Exp(-t * 8f);
                        break;
                    case LabFizz:
                        samples[i] = (Random.value * 2f - 1f) * Mathf.Exp(-t * 10f) * 0.3f;
                        break;
                    case ReactionExplosion:
                        samples[i] = (Random.value * 2f - 1f) * Mathf.Exp(-t * 3f);
                        break;
                    default:
                        samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * Mathf.Exp(-t * 10f);
                        break;
                }
            }

            var clip = AudioClip.Create(soundId + "_placeholder", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
