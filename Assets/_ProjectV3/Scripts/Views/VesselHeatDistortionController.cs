// ChemLabSim v3 — Vessel Heat Distortion Controller
// Isolated, per-vessel heat-haze driver. Subscribes to ChemistryProcessedEvent
// and drives a single distortion-shader material on a child "HeatVolume"
// renderer, fading its strength in/out smoothly with the reported temperature.
//
// Why this lives next to HeatDistortionController.cs (and not in it):
//   The existing HeatDistortionController is a camera-attached, fullscreen
//   post-process driven by ChemFxTriggeredEvent. This script is a per-vessel
//   *world-space* effect driven by ChemistryProcessedEvent, matching the
//   ServiceLocator + EventBus + WaitUntil + OnBecame{Visible,Invisible} +
//   OnValidate conventions of LiquidColorController, LiquidVFXController,
//   and GasEvolutionController. They are complementary, not redundant.
//
// API note: distortion-shader property names vary across projects
// (URP-style shaders typically expose "_DistortionStrength"; legacy
// glass/refraction shaders use "_BumpAmt"). Both property IDs are
// resolved at Awake; whichever the bound material exposes is written
// via a MaterialPropertyBlock — the shared material is never mutated.

using System.Collections;
using UnityEngine;
using ChemLabSimV3.Core;
using ChemLabSimV3.Engine;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Views
{
    /// <summary>
    /// Drives a per-vessel heat-haze distortion effect on a child renderer
    /// when the reaction temperature rises sufficiently above ambient. The
    /// volume renderer is disabled entirely while dormant so the GPU does
    /// not pay for a transparent shell every frame.
    /// </summary>
    [DisallowMultipleComponent]
    public class VesselHeatDistortionController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Heat Volume")]
        [Tooltip("Renderer using a distortion shader. If null, a child named " +
                 "'HeatVolume' is searched, then the first child Renderer.")]
        [SerializeField] private Renderer _heatVolume;

        [Tooltip("Preferred child name to search for when _heatVolume is null.")]
        [SerializeField] private string _heatVolumeName = "HeatVolume";

        [Header("Shader Properties")]
        [Tooltip("Primary distortion strength property (URP-style shaders).")]
        [SerializeField] private string _primaryStrengthProperty  = "_DistortionStrength";
        [Tooltip("Fallback distortion strength property (legacy glass / refraction shaders).")]
        [SerializeField] private string _fallbackStrengthProperty = "_BumpAmt";

        [Header("Temperature Mapping")]
        [Tooltip("Ambient room temperature in °C. Below ambient + threshold the haze is off.")]
        [SerializeField] private float _ambientTemperatureC = 25f;

        [Tooltip("°C above ambient required before the haze begins to appear (default 25 → ~50 °C trigger).")]
        [SerializeField, Min(0f)] private float _activationDeltaC = 25f;

        [Tooltip("°C above the activation point that maps to maximum distortion.")]
        [SerializeField, Min(0.01f)] private float _maxOverheatC = 75f;

        [Header("Distortion Range")]
        [Tooltip("Shader value at the activation threshold.")]
        [SerializeField, Min(0f)] private float _baseStrength = 0.05f;

        [Tooltip("Shader value at maximum intensity.")]
        [SerializeField, Min(0f)] private float _maxStrength  = 0.6f;

        [Header("Smoothing")]
        [Tooltip("Lerp speed for distortion strength (units/sec).")]
        [SerializeField, Min(0f)] private float _lerpSpeed = 3f;

        [Tooltip("Strength at or below which the heat volume GameObject is disabled.")]
        [SerializeField, Min(0f)] private float _disableBelow = 0.005f;

        // ── State ─────────────────────────────────────────────
        private MaterialPropertyBlock _mpb;
        private int  _primaryPropId;
        private int  _fallbackPropId;
        private bool _hasPrimaryProp;
        private bool _hasFallbackProp;

        private float _targetStrength;
        private float _currentStrength;
        private bool  _registryReady;
        private bool  _isVisible = true; // default: assume visible until OnBecameInvisible fires
        private bool  _serviceErrorLogged;

        // ── Unity Lifecycle ───────────────────────────────────
        private void Awake()
        {
            if (_heatVolume == null) _heatVolume = ResolveHeatVolume();

            _mpb = new MaterialPropertyBlock();
            _primaryPropId  = Shader.PropertyToID(_primaryStrengthProperty);
            _fallbackPropId = Shader.PropertyToID(_fallbackStrengthProperty);

            if (_heatVolume != null && _heatVolume.sharedMaterial != null)
            {
                var mat = _heatVolume.sharedMaterial;
                _hasPrimaryProp  = mat.HasProperty(_primaryPropId);
                _hasFallbackProp = mat.HasProperty(_fallbackPropId);

                // Start hidden — both visually (zero strength) and on the CPU
                // (Renderer disabled) until the first hot event arrives.
                ApplyStrengthToBlock(0f);
                _heatVolume.enabled = false;
            }
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
            // Wait until ReactionRegistry is registered. Even though this
            // controller doesn't currently consult per-reaction data, holding
            // off keeps boot ordering identical to the other view scripts.
            yield return new WaitUntil(() => ServiceLocator.Has<ReactionRegistry>());
            _registryReady = true;
        }

        // Frustum-culling driven CPU optimization — Unity invokes these on
        // GameObjects with a Renderer (the heat volume itself).
        private void OnBecameVisible()   { _isVisible = true;  }
        private void OnBecameInvisible() { _isVisible = false; }

        private void LateUpdate()
        {
            // LateUpdate so the haze samples the vessel's *final* transformed
            // position for the frame — important if a parent rig (lab arm,
            // physics drag, animation) moves the vessel during Update().
            if (_heatVolume == null) return;
            if (!_hasPrimaryProp && !_hasFallbackProp) return;

            // Frame-rate-independent smoothing.
            float t = 1f - Mathf.Exp(-_lerpSpeed * Time.deltaTime);
            _currentStrength = Mathf.Lerp(_currentStrength, _targetStrength, t);

            if (_currentStrength > _disableBelow)
            {
                if (!_heatVolume.enabled) _heatVolume.enabled = true;
                ApplyStrengthToBlock(_currentStrength);
            }
            else if (_heatVolume.enabled && _targetStrength <= 0f)
            {
                ApplyStrengthToBlock(0f);
                _heatVolume.enabled = false;
            }
        }

        // ── Event Handler ─────────────────────────────────────
        private void OnChemistryProcessed(ChemistryProcessedEvent evt)
        {
            // Resource guard: skip work for inactive or off-screen vessels.
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;
            if (!_isVisible) return;
            if (_heatVolume == null || (!_hasPrimaryProp && !_hasFallbackProp)) return;

            var output = evt.Output;

            // Clean reset path: no reaction → fade haze out.
            if (string.IsNullOrEmpty(output.ReactionId))
            {
                ResetToDefault();
                return;
            }

            // Safety: registry may not be registered yet (early scene load).
            if (!_registryReady) return;
            if (!ServiceLocator.Has<ReactionRegistry>() || ServiceLocator.Get<ReactionRegistry>() == null)
            {
                LogServiceMissingOnce();
                return;
            }

            float overAmbient = output.TemperatureC - _ambientTemperatureC;
            if (overAmbient < _activationDeltaC)
            {
                // Below the trigger threshold — fade out smoothly.
                ResetToDefault();
                return;
            }

            float overheat    = overAmbient - _activationDeltaC;
            float intensity01 = Mathf.Clamp01(overheat / _maxOverheatC);
            _targetStrength   = Mathf.Lerp(_baseStrength, _maxStrength, intensity01);
        }

        // ── Helpers ───────────────────────────────────────────
        /// <summary>Smoothly fade the haze out and let LateUpdate() disable the renderer.</summary>
        private void ResetToDefault()
        {
            _targetStrength = 0f;
        }

        /// <summary>
        /// Object-pool friendly teardown: zero the smoothing state, clear the
        /// MaterialPropertyBlock so a recycled vessel cannot inherit a hot
        /// distortion value, and disable the heat volume renderer.
        /// </summary>
        private void ClearForPooling()
        {
            _currentStrength = 0f;
            _targetStrength  = 0f;

            if (_heatVolume != null)
            {
                if (_mpb != null)
                {
                    _heatVolume.GetPropertyBlock(_mpb);
                    _mpb.Clear();
                    if (_hasPrimaryProp)  _mpb.SetFloat(_primaryPropId,  0f);
                    if (_hasFallbackProp) _mpb.SetFloat(_fallbackPropId, 0f);
                    _heatVolume.SetPropertyBlock(_mpb);
                }
                _heatVolume.enabled = false;
            }
        }

        private void LogServiceMissingOnce()
        {
            if (_serviceErrorLogged) return;
            _serviceErrorLogged = true;
            Debug.LogError($"[VesselHeatDistortionController] '{name}': ReactionRegistry service unavailable. Disabling LateUpdate loop to prevent log spam.", this);
            enabled = false;
        }

        private void ApplyStrengthToBlock(float value)
        {
            _heatVolume.GetPropertyBlock(_mpb);
            if (_hasPrimaryProp)  _mpb.SetFloat(_primaryPropId,  value);
            if (_hasFallbackProp) _mpb.SetFloat(_fallbackPropId, value);
            _heatVolume.SetPropertyBlock(_mpb);
        }

        private Renderer ResolveHeatVolume()
        {
            // Prefer a child named "HeatVolume" so the script latches onto
            // the intended object even when the vessel has multiple renderers.
            Transform named = FindChildByName(transform, _heatVolumeName);
            if (named != null)
            {
                var r = named.GetComponent<Renderer>();
                if (r != null) return r;
            }

            // Fallback: first Renderer in any child (excluding self to avoid
            // accidentally driving the liquid mesh).
            var all = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].transform != transform) return all[i];
            }
            return null;
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
            if (_heatVolume == null && ResolveHeatVolume() == null)
                Debug.LogWarning($"[VesselHeatDistortionController] '{name}': no HeatVolume Renderer assigned and none found in children.", this);
        }
#endif
    }
}
