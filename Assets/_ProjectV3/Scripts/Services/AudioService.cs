// ChemLabSim v3 — Audio Service (Skeleton)
// Centralizes all SFX and UI sounds. Does not exist in v2.

using UnityEngine;

namespace ChemLabSimV3.Services
{
    public class AudioService : IService
    {
        // TODO: AudioSource pool for overlapping SFX playback.
        // TODO: Sound registry (ScriptableObject or JSON) mapping soundId → AudioClip.

        private float masterVolume = 1f;
        private float sfxVolume = 1f;
        private float uiVolume = 1f;

        public void Initialize()
        {
            Debug.Log("[AudioService] Initialized.");
            // TODO: Create AudioSource pool on a persistent GameObject.
            // TODO: Load volume settings from SaveService.
        }

        public void Dispose()
        {
            Debug.Log("[AudioService] Disposed.");
        }

        /// <summary>Play a reaction or environment sound effect by ID.</summary>
        public void PlaySFX(string soundId, float volume = 1f)
        {
            // TODO: Look up AudioClip from registry and play via pool.
            Debug.Log($"[AudioService] PlaySFX: {soundId}");
        }

        /// <summary>Play a UI interaction sound.</summary>
        public void PlayUI(string uiSoundId)
        {
            // TODO: Look up UI AudioClip and play.
            Debug.Log($"[AudioService] PlayUI: {uiSoundId}");
        }

        public void SetMasterVolume(float value) => masterVolume = Mathf.Clamp01(value);
        public void SetSFXVolume(float value)    => sfxVolume = Mathf.Clamp01(value);
        public void SetUIVolume(float value)     => uiVolume = Mathf.Clamp01(value);

        public float MasterVolume => masterVolume;
        public float SFXVolume    => sfxVolume;
        public float UIVolume     => uiVolume;
    }
}
