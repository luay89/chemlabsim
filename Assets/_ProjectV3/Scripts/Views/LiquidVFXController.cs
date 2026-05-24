// ChemLabSim v3 — Liquid VFX Controller
// Isolated boiling-effect driver. Subscribes to ChemistryProcessedEvent,
// looks up the reaction's boiling threshold via ReactionRegistry, and
// drives a single ParticleSystem (bubbles) whose intensity scales with
// how far the temperature exceeds the threshold.
//
// API note: The original prompt referenced a few APIs that don't exist
// in this codebase. Per the "Do NOT modify any existing scripts" rule,
// this file maps the intent onto the real public surface:
//   ServiceLocator.Get<ReactionRegistry>().GetReaction(id)
//       → ServiceLocator.Get<ReactionRegistry>().FindById(output.ReactionId)
//   output.Temperature
//       → output.TemperatureC
//   entry.boiling_point
//       → MIN(non-NaN entry.products[*].boilingPointC) — the substance
//         that would boil first. boilingPointC lives on ReactionChemical,
//         not on ReactionEntry, so the lowest product boiling point is
//         the physically meaningful threshold for "the mixture boils".

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
    /// Plays a bubble <see cref="ParticleSystem"/> when the reaction's
    /// observed temperature meets or exceeds the boiling threshold of the
    /// lowest-boiling product. Stops emission when the mixture cools.
    /// </summary>
    [DisallowMultipleComponent]
    public class LiquidVFXController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Particle System")]
        [Tooltip("Bubble particles. If null, the first ParticleSystem in children is used.")]
        [SerializeField] private ParticleSystem _bubbles;

        [Header("Intensity Mapping")]
        [Tooltip("Base emission rate at the boiling point (particles/sec).")]
        [SerializeField, Min(0f)] private float _baseRate = 8f;

        [Tooltip("Maximum emission rate at MaxOverheatC above boiling (particles/sec).")]
        [SerializeField, Min(0f)] private float _maxRate = 80f;

        [Tooltip("Temperature delta (°C) above boiling that maps to maximum intensity.")]
        [SerializeField, Min(0.01f)] private float _maxOverheatC = 30f;

        [Tooltip("Particle start size at boiling.")]
        [SerializeField, Min(0f)] private float _baseSize = 0.06f;

        [Tooltip("Particle start size at maximum intensity.")]
        [SerializeField, Min(0f)] private float _maxSize = 0.16f;

        [Tooltip("Smoothing speed for emission rate (units/sec).")]
        [SerializeField, Min(0f)] private float _lerpSpeed = 4f;

        // ── State ─────────────────────────────────────────────
        private ParticleSystem.EmissionModule _emission;
        private ParticleSystem.MainModule     _main;
        private bool _hasParticles;

        private float _targetRate;
        private float _currentRate;
        private float _targetSize;
        private float _currentSize;
        private bool  _isBoiling;
        private bool  _registryReady;
        private bool  _isVisible = true; // default: assume visible until OnBecameInvisible fires
        private bool  _serviceErrorLogged;

        // ── Unity Lifecycle ───────────────────────────────────
        private void Awake()
        {
            if (_bubbles == null)
                _bubbles = GetComponentInChildren<ParticleSystem>(true);

            if (_bubbles != null)
            {
                _emission = _bubbles.emission;
                _main     = _bubbles.main;
                _hasParticles = true;

                _emission.rateOverTime = 0f;
                _main.startSize = _baseSize;
                if (_bubbles.isPlaying)
                    _bubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            _currentSize = _baseSize;
            _targetSize  = _baseSize;
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
            if (_hasParticles && _bubbles != null && _bubbles.isPlaying)
                _bubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ClearForPooling();
        }

        private IEnumerator WaitForRegistry()
        {
            // Wait until ReactionRegistry is registered before processing
            // events that need a lookup. Prevents service-not-found errors
            // during scene load.
            yield return new WaitUntil(() => ServiceLocator.Has<ReactionRegistry>());
            _registryReady = true;
        }

        // Frustum-culling driven CPU optimization. Note: requires the
        // ParticleSystem (or another Renderer on this GameObject) to be the
        // visibility source — Unity calls these on objects with a Renderer.
        private void OnBecameVisible()   { _isVisible = true;  }
        private void OnBecameInvisible() { _isVisible = false; }

        private void Update()
        {
            if (!_hasParticles) return;

            // Frame-rate-independent smoothing of rate + size.
            float t = 1f - Mathf.Exp(-_lerpSpeed * Time.deltaTime);
            _currentRate = Mathf.Lerp(_currentRate, _targetRate, t);
            _currentSize = Mathf.Lerp(_currentSize, _targetSize, t);

            _emission.rateOverTime = _currentRate;
            _main.startSize        = _currentSize;

            // Cooling-down state: once smoothed rate is effectively zero, stop.
            if (!_isBoiling && _currentRate <= 0.05f && _bubbles.isPlaying)
                _bubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // ── Event Handler ─────────────────────────────────────
        private void OnChemistryProcessed(ChemistryProcessedEvent evt)
        {
            // Resource guard: skip work for inactive or off-screen vessels.
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;
            if (!_isVisible) return;
            if (!_hasParticles) return;

            var output = evt.Output;
            if (string.IsNullOrEmpty(output.ReactionId) || TotalProductMoles(output.Substances) <= 0f)
            {
                ResetToDefault();
                return;
            }

            // Safety: registry may not be registered yet (early scene load).
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
                StopBoiling();
                return;
            }

            float boilingPointC = ResolveBoilingPointC(entry.products);
            if (float.IsNaN(boilingPointC))
            {
                // No product has a defined boiling point → nothing to compare against.
                StopBoiling();
                return;
            }

            float overheat = output.TemperatureC - boilingPointC;
            if (overheat < 0f)
            {
                StopBoiling();
                return;
            }

            // Boiling: scale rate + size by overheat fraction.
            float intensity01 = Mathf.Clamp01(overheat / _maxOverheatC);
            _targetRate  = Mathf.Lerp(_baseRate, _maxRate, intensity01);
            _targetSize  = Mathf.Lerp(_baseSize, _maxSize, intensity01);
            _isBoiling   = true;

            if (!_bubbles.isPlaying)
                _bubbles.Play();
        }

        // ── Helpers ───────────────────────────────────────────
        /// <summary>Smoothly fade out bubbles and return to the dormant default.</summary>
        private void ResetToDefault() => StopBoiling();

        private void StopBoiling()
        {
            _isBoiling  = false;
            _targetRate = 0f;
            _targetSize = _baseSize;
            // Actual Stop() happens in Update once _currentRate decays to ~0.
        }

        private static float TotalProductMoles(List<SubstanceState> substances)
        {
            if (substances == null) return 0f;
            float total = 0f;
            for (int i = 0; i < substances.Count; i++)
            {
                var s = substances[i];
                if (s.IsProduct && s.MolesFinal > 0f) total += s.MolesFinal;
            }
            return total;
        }

        /// <summary>
        /// Object-pool friendly teardown: stop AND clear all live particles
        /// (not just emission) and reset smoothing state. Without the Clear,
        /// the vessel reactivates with the previous reaction's bubbles still
        /// in mid-air.
        /// </summary>
        private void ClearForPooling()
        {
            _isBoiling   = false;
            _targetRate  = 0f;
            _currentRate = 0f;
            _targetSize  = _baseSize;
            _currentSize = _baseSize;

            if (_hasParticles && _bubbles != null)
            {
                _emission.rateOverTime = 0f;
                _main.startSize        = _baseSize;
                _bubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void LogServiceMissingOnce()
        {
            if (_serviceErrorLogged) return;
            _serviceErrorLogged = true;
            Debug.LogError($"[LiquidVFXController] '{name}': ReactionRegistry service unavailable. Disabling Update loop to prevent log spam.", this);
            enabled = false;
        }

        /// <summary>
        /// Returns the lowest non-NaN boiling point across the reaction's
        /// products — the substance that would boil first. Returns
        /// <see cref="float.NaN"/> if none of the products define one.
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_bubbles == null && GetComponentInChildren<ParticleSystem>(true) == null)
                Debug.LogWarning($"[LiquidVFXController] '{name}': no ParticleSystem assigned and none found in children.", this);
        }
#endif
    }
}
