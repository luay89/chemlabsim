// ChemLabSim v3 — Vessel Audio Controller
// Per-vessel 3D spatial audio driver. Subscribes to ChemistryProcessedEvent,
// resolves the reaction's lowest-product boiling point via ReactionRegistry,
// and crossfades two AudioSources:
//
//   • Bubbling — volume + pitch ramp up as output.TemperatureC approaches
//                and then exceeds the resolved boiling point.
//   • Hissing  — volume scales with the gas production rate (mol/sec)
//                derived per-tick from output.Substances (Phase.Gas).
//
// Spatial: spatialBlend = 1.0 (full 3D), Logarithmic rolloff with
// minDistance = 0.2 m, maxDistance = 5 m so the player can localize the
// vessel by sound. Clips are looped and authored as one-shot textures.
//
// API mapping (mirrors LiquidVFXController / GasEvolutionController):
//   ServiceLocator.Get<ReactionRegistry>().FindById(output.ReactionId)
//   ResolveBoilingPointC(entry.products)  → MIN(non-NaN boilingPointC)
//   Gas rate                              → Δ(SUM gas MolesFinal) / Δt
//
// Optimization & safety:
//   • Mathf.Lerp with frame-rate-independent t to avoid audio popping.
//   • AudioSources are stopped (not just muted) when fully dormant so
//     Unity's audio voices are released for other sounds.
//   • Skipped entirely when the vessel is inactive or off-screen.
//   • Per-tick state cleared on reaction change and on disable so a
//     pooled vessel cannot inherit the previous reaction's audio profile.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ChemLabSimV3.Core;
using ChemLabSimV3.Engine;
using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Views
{
    /// <summary>
    /// Drives two 3D <see cref="AudioSource"/> components on a vessel —
    /// bubbling (boiling) and hissing (gas evolution) — using data from
    /// the latest <see cref="ChemistryProcessedEvent"/>. Volumes and pitch
    /// are smoothed with <see cref="Mathf.Lerp"/> to prevent popping, and
    /// dormant sources are stopped to free CPU and audio voices.
    /// </summary>
    [DisallowMultipleComponent]
    public class VesselAudioController : MonoBehaviour
    {
        // ── Inspector — Sources ───────────────────────────────
        [Header("Audio Sources")]
        [Tooltip("AudioSource for the bubbling/boiling sound. If null, one is created on a child named 'BubblingSource'.")]
        [SerializeField] private AudioSource _bubblingSource;

        [Tooltip("AudioSource for the gas hissing sound. If null, one is created on a child named 'HissingSource'.")]
        [SerializeField] private AudioSource _hissingSource;

        [Header("Audio Clips")]
        [Tooltip("Looping bubble/boil loop. Required for bubbling to be audible.")]
        [SerializeField] private AudioClip _bubblingClip;

        [Tooltip("Looping gas/steam hiss loop. Required for hissing to be audible.")]
        [SerializeField] private AudioClip _hissingClip;

        // ── Inspector — 3D Spatial ────────────────────────────
        [Header("3D Spatial Settings")]
        [Tooltip("Full 3D blend (1.0 = positional). Applied to both sources.")]
        [SerializeField, Range(0f, 1f)] private float _spatialBlend = 1f;

        [Tooltip("Distance (m) at which the source plays at full volume.")]
        [SerializeField, Min(0.01f)] private float _minDistance = 0.2f;

        [Tooltip("Distance (m) beyond which the source is inaudible.")]
        [SerializeField, Min(0.1f)] private float _maxDistance = 5f;

        // ── Inspector — Bubbling Mapping ──────────────────────
        [Header("Bubbling (Temperature → Boiling)")]
        [Tooltip("Temperature delta (°C) below boiling point at which faint bubbling becomes audible.")]
        [SerializeField, Min(0f)] private float _preBoilWindowC = 10f;

        [Tooltip("°C above boiling that maps to maximum bubbling volume + pitch.")]
        [SerializeField, Min(0.01f)] private float _maxOverheatC = 30f;

        [Tooltip("Maximum bubbling volume.")]
        [SerializeField, Range(0f, 1f)] private float _bubblingMaxVolume = 0.85f;

        [Tooltip("Bubbling pitch at the boiling point.")]
        [SerializeField, Range(0.1f, 3f)] private float _bubblingBasePitch = 0.85f;

        [Tooltip("Bubbling pitch at maximum overheat.")]
        [SerializeField, Range(0.1f, 3f)] private float _bubblingMaxPitch = 1.25f;

        // ── Inspector — Hissing Mapping ───────────────────────
        [Header("Hissing (Gas Rate → Volume)")]
        [Tooltip("Below this rate (mol/sec) the hiss is considered dormant and stops.")]
        [SerializeField, Min(0f)] private float _minGasRate = 0.005f;

        [Tooltip("Gas production rate (mol/sec) that maps to maximum hiss volume.")]
        [SerializeField, Min(0.0001f)] private float _maxGasRate = 0.5f;

        [Tooltip("Maximum hissing volume.")]
        [SerializeField, Range(0f, 1f)] private float _hissingMaxVolume = 0.7f;

        // ── Inspector — Smoothing & Activity ──────────────────
        [Header("Smoothing")]
        [Tooltip("Smoothing speed for volume + pitch (units/sec). Higher = faster, but more popping risk.")]
        [SerializeField, Min(0.01f)] private float _lerpSpeed = 4f;

        [Tooltip("Volume below which a source is fully stopped to free a Unity audio voice.")]
        [SerializeField, Range(0f, 0.05f)] private float _stopBelowVolume = 0.005f;

        [Tooltip("Treat the vessel as 'at room temperature' when output.TemperatureC is at or below this value (°C). " +
                 "Used to silence bubbling regardless of boiling point lookup.")]
        [SerializeField] private float _ambientTemperatureC = 25f;

        // ── State ─────────────────────────────────────────────
        private float _targetBubblingVolume;
        private float _currentBubblingVolume;
        private float _targetBubblingPitch;
        private float _currentBubblingPitch;

        private float _targetHissingVolume;
        private float _currentHissingVolume;

        private bool  _registryReady;
        private bool  _isVisible = true; // default: assume visible until OnBecameInvisible fires
        private bool  _serviceErrorLogged;

        // For per-tick gas rate derivation (same pattern as GasEvolutionController).
        private string _lastReactionId;
        private float  _lastGasMoles;
        private float  _lastEventTime;

        // ── Unity Lifecycle ───────────────────────────────────
        private void Awake()
        {
            if (_bubblingSource == null) _bubblingSource = ResolveOrCreateSource("BubblingSource");
            if (_hissingSource  == null) _hissingSource  = ResolveOrCreateSource("HissingSource");

            ConfigureSource(_bubblingSource, _bubblingClip, _bubblingBasePitch);
            ConfigureSource(_hissingSource,  _hissingClip,  1f);

            _currentBubblingPitch = _bubblingBasePitch;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ChemistryProcessedEvent>(OnChemistryProcessed);
            if (!_registryReady) StartCoroutine(WaitForRegistry());
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChemistryProcessedEvent>(OnChemistryProcessed);
            StopAllCoroutines(); // prevent leaked WaitUntil from a disabled instance
            ClearForPooling();
        }

        private IEnumerator WaitForRegistry()
        {
            // Wait until ReactionRegistry is registered before processing
            // events that need a boiling-point lookup. Prevents service-not-
            // found errors during scene load.
            yield return new WaitUntil(() => ServiceLocator.Has<ReactionRegistry>());
            _registryReady = true;
        }

        // Frustum-culling driven CPU optimization — Unity invokes these on
        // GameObjects with a Renderer. Vessels carry a mesh renderer, so the
        // signals fire and we can spare the Update math + audio voices when
        // the vessel is off-screen (player still gets fully spatialized cues
        // when it re-enters the frustum because the controller resumes).
        private void OnBecameVisible()   { _isVisible = true;  }
        private void OnBecameInvisible() { _isVisible = false; }

        private void Update()
        {
            // Frame-rate-independent smoothing — avoids audio popping that
            // would occur with raw per-event jumps in volume/pitch.
            float t = 1f - Mathf.Exp(-_lerpSpeed * Time.deltaTime);

            ApplySmoothingWithPitch(_bubblingSource,
                ref _currentBubblingVolume, _targetBubblingVolume,
                ref _currentBubblingPitch,  _targetBubblingPitch, t);

            ApplySmoothingVolumeOnly(_hissingSource,
                ref _currentHissingVolume,  _targetHissingVolume, t);
        }

        // ── Event Handler ─────────────────────────────────────
        private void OnChemistryProcessed(ChemistryProcessedEvent evt)
        {
            // Resource guard: skip work for inactive or off-screen vessels.
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;
            if (!_isVisible) return;

            var output = evt.Output;

            // Clean reset path: no reaction → fade everything out.
            if (string.IsNullOrEmpty(output.ReactionId))
            {
                ResetToDefault();
                return;
            }

            UpdateBubblingTargets(output);
            UpdateHissingTargets(output);
        }

        // ── Bubbling ──────────────────────────────────────────
        private void UpdateBubblingTargets(ChemistryOutput output)
        {
            // Below ambient there is no liquid agitation worth simulating.
            if (output.TemperatureC <= _ambientTemperatureC)
            {
                _targetBubblingVolume = 0f;
                _targetBubblingPitch  = _bubblingBasePitch;
                return;
            }

            // Wait for the registry rather than guessing a boiling point.
            if (!_registryReady || !ServiceLocator.Has<ReactionRegistry>())
                return;

            var registry = ServiceLocator.Get<ReactionRegistry>();
            if (registry == null)
            {
                LogServiceMissingOnce();
                return;
            }

            var entry = registry.FindById(output.ReactionId);
            if (entry == null)
            {
                _targetBubblingVolume = 0f;
                _targetBubblingPitch  = _bubblingBasePitch;
                return;
            }

            float boilingPointC = ResolveBoilingPointC(entry.products);
            if (float.IsNaN(boilingPointC))
            {
                // No product defines a boiling point → no audible target.
                _targetBubblingVolume = 0f;
                _targetBubblingPitch  = _bubblingBasePitch;
                return;
            }

            // Pre-boil window: gentle fade-in for the last few °C before
            // boiling so the user hears the kettle start to rumble.
            float delta = output.TemperatureC - boilingPointC;

            if (delta < -_preBoilWindowC)
            {
                _targetBubblingVolume = 0f;
                _targetBubblingPitch  = _bubblingBasePitch;
                return;
            }

            float intensity01;
            if (delta < 0f)
            {
                // Approaching boiling: 0 → 0.4 of full volume.
                float approach = 1f - Mathf.Clamp01(-delta / Mathf.Max(_preBoilWindowC, 0.01f));
                intensity01    = approach * 0.4f;
            }
            else
            {
                // At/above boiling: 0.4 → 1.0 across _maxOverheatC.
                float overheatNorm = Mathf.Clamp01(delta / _maxOverheatC);
                intensity01        = Mathf.Lerp(0.4f, 1f, overheatNorm);
            }

            _targetBubblingVolume = _bubblingMaxVolume * intensity01;
            _targetBubblingPitch  = Mathf.Lerp(_bubblingBasePitch, _bubblingMaxPitch, intensity01);
        }

        // ── Hissing ───────────────────────────────────────────
        private void UpdateHissingTargets(ChemistryOutput output)
        {
            float gasMoles = SumGasMoles(output.Substances);

            // Reset per-tick history when the reaction id changes so the
            // first tick of a new reaction doesn't produce a misleading
            // negative or huge delta.
            bool sameReaction = (output.ReactionId == _lastReactionId);
            float now = Time.time;

            // Zero-division guard: floor every divisor at 0.0001 s to be
            // resilient against paused-editor frames where deltaTime == 0.
            float rawDt = sameReaction ? (now - _lastEventTime) : Mathf.Max(Time.deltaTime, 0.0001f);
            float dt    = Mathf.Max(rawDt, 0.0001f);

            float deltaMoles = sameReaction ? Mathf.Max(gasMoles - _lastGasMoles, 0f) : gasMoles;
            float ratePerSec = deltaMoles / dt;

            _lastReactionId = output.ReactionId;
            _lastGasMoles   = gasMoles;
            _lastEventTime  = now;

            if (ratePerSec < _minGasRate)
            {
                _targetHissingVolume = 0f;
                return;
            }

            float intensity01    = Mathf.Clamp01(ratePerSec / _maxGasRate);
            _targetHissingVolume = _hissingMaxVolume * intensity01;
        }

        // ── Helpers ───────────────────────────────────────────
        /// <summary>Smoothly fade both sources to silence + reset history.</summary>
        private void ResetToDefault()
        {
            _targetBubblingVolume = 0f;
            _targetBubblingPitch  = _bubblingBasePitch;
            _targetHissingVolume  = 0f;
            ResetTickHistory();
        }

        private void ResetTickHistory()
        {
            _lastReactionId = null;
            _lastGasMoles   = 0f;
            _lastEventTime  = 0f;
        }

        /// <summary>
        /// Object-pool friendly teardown: stop both AudioSources, zero their
        /// volume immediately, and clear all per-tick history. A recycled
        /// vessel must not inherit the previous reaction's audio.
        /// </summary>
        private void ClearForPooling()
        {
            _targetBubblingVolume  = 0f;
            _currentBubblingVolume = 0f;
            _targetBubblingPitch   = _bubblingBasePitch;
            _currentBubblingPitch  = _bubblingBasePitch;
            _targetHissingVolume   = 0f;
            _currentHissingVolume  = 0f;
            ResetTickHistory();

            HardStop(_bubblingSource, _bubblingBasePitch);
            HardStop(_hissingSource,  1f);
        }

        private static void HardStop(AudioSource src, float restPitch)
        {
            if (src == null) return;
            src.volume = 0f;
            src.pitch  = restPitch;
            if (src.isPlaying) src.Stop();
        }

        private void ApplySmoothingVolumeOnly(AudioSource src,
                                              ref float volumeRef, float targetVolume, float t)
        {
            if (src == null) return;

            volumeRef = Mathf.Lerp(volumeRef, targetVolume, t);

            // Stop entirely once smoothed volume is effectively zero AND the
            // target is also zero. This frees a Unity audio voice and saves
            // CPU on the mixer for vessels at room temperature / no gas.
            if (volumeRef <= _stopBelowVolume && targetVolume <= 0f)
            {
                if (src.isPlaying) src.Stop();
                src.volume = 0f;
                return;
            }

            // Volume rising from rest → resume playback.
            if (!src.isPlaying && src.clip != null && targetVolume > 0f)
                src.Play();

            src.volume = volumeRef;
        }

        private void ApplySmoothingWithPitch(AudioSource src,
                                             ref float volumeRef, float targetVolume,
                                             ref float pitchRef,  float targetPitch, float t)
        {
            if (src == null) return;

            volumeRef = Mathf.Lerp(volumeRef, targetVolume, t);
            pitchRef  = Mathf.Lerp(pitchRef,  targetPitch,  t);

            if (volumeRef <= _stopBelowVolume && targetVolume <= 0f)
            {
                if (src.isPlaying) src.Stop();
                src.volume = 0f;
                src.pitch  = _bubblingBasePitch;
                return;
            }

            if (!src.isPlaying && src.clip != null && targetVolume > 0f)
                src.Play();

            src.volume = volumeRef;
            src.pitch  = pitchRef;
        }

        private AudioSource ResolveOrCreateSource(string childName)
        {
            // Prefer an existing child of the matching name so designers can
            // pre-author per-source mixer routing in the prefab.
            Transform existing = FindChildByName(transform, childName);
            if (existing != null)
            {
                var found = existing.GetComponent<AudioSource>();
                if (found != null) return found;
                return existing.gameObject.AddComponent<AudioSource>();
            }

            var go = new GameObject(childName);
            go.transform.SetParent(transform, worldPositionStays: false);
            return go.AddComponent<AudioSource>();
        }

        private void ConfigureSource(AudioSource src, AudioClip clip, float basePitch)
        {
            if (src == null) return;

            src.clip          = clip;
            src.loop          = true;
            src.playOnAwake   = false;
            src.spatialBlend  = _spatialBlend;
            src.rolloffMode   = AudioRolloffMode.Logarithmic;
            src.minDistance   = _minDistance;
            // Keep a sane ordering even if a designer typoes the inspector.
            src.maxDistance   = Mathf.Max(_maxDistance, _minDistance + 0.01f);
            src.dopplerLevel  = 0f;  // chemistry vessels don't move fast enough to need it
            src.volume        = 0f;
            src.pitch         = basePitch;

            if (src.isPlaying) src.Stop();
        }

        private void LogServiceMissingOnce()
        {
            if (_serviceErrorLogged) return;
            _serviceErrorLogged = true;
            Debug.LogError($"[VesselAudioController] '{name}': ReactionRegistry service unavailable. Disabling Update loop to prevent log spam.", this);
            enabled = false;
        }

        private static float SumGasMoles(List<SubstanceState> substances)
        {
            if (substances == null) return 0f;
            float total = 0f;
            for (int i = 0; i < substances.Count; i++)
            {
                var s = substances[i];
                if (s.IsProduct && s.Phase == Phase.Gas && s.MolesFinal > 0f)
                    total += s.MolesFinal;
            }
            return total;
        }

        /// <summary>
        /// Returns the lowest non-NaN boiling point across the reaction's
        /// products — the substance that would boil first. Returns
        /// <see cref="float.NaN"/> if none of the products define one.
        /// Mirrors LiquidVFXController.ResolveBoilingPointC for consistency.
        /// </summary>
        private static float ResolveBoilingPointC(List<ReactionChemical> products)
        {
            if (products == null || products.Count == 0) return float.NaN;

            float lowest = float.NaN;
            for (int i = 0; i < products.Count; i++)
            {
                var p = products[i];
                if (p == null) continue;
                float bp = p.boilingPointC;
                if (float.IsNaN(bp)) continue;
                if (float.IsNaN(lowest) || bp < lowest) lowest = bp;
            }
            return lowest;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;

            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name == name) return c;
                var hit = FindChildByName(c, name);
                if (hit != null) return hit;
            }
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Designer hints — never throw, just warn. Matches the other
            // controllers' OnValidate style.
            if (_maxDistance <= _minDistance)
                Debug.LogWarning($"[VesselAudioController] '{name}': maxDistance ({_maxDistance}) must be > minDistance ({_minDistance}).", this);

            if (_bubblingMaxPitch < _bubblingBasePitch)
                Debug.LogWarning($"[VesselAudioController] '{name}': bubblingMaxPitch ({_bubblingMaxPitch}) is below basePitch ({_bubblingBasePitch}); pitch will descend with heat.", this);

            if (_bubblingClip == null && _hissingClip == null)
                Debug.LogWarning($"[VesselAudioController] '{name}': no AudioClips assigned — sources will be silent.", this);
        }
#endif
    }
}
