// ChemLabSim v3 — Liquid Color Controller
// Isolated visual transition driver. Subscribes to ChemistryProcessedEvent,
// looks up the reaction's target color via ReactionRegistry, and lerps
// the renderer's _LiquidColor (with _BaseColor fallback) using a
// MaterialPropertyBlock — never instantiates a material at runtime.
//
// API note: The original prompt referenced a few APIs that don't exist
// in this codebase. Per the "No Refactoring" rule, this file uses the
// real public surface that was confirmed during the recent cleanup:
//   ReactionRegistry.Instance.GetReaction(id)  →  resolved via
//       ServiceLocator.Get<ReactionRegistry>().FindById(output.ReactionId)
//   modernEntry.ProductColor                   →  entry.visual_effects.color_change
//   Output.Progress / Output.Density           →  output.CompletionPercent / 100f

using System.Collections;
using UnityEngine;
using ChemLabSimV3.Core;
using ChemLabSimV3.Engine;
using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Views
{
    /// <summary>
    /// Smoothly transitions the liquid color of a single vessel renderer in
    /// response to <see cref="ChemistryProcessedEvent"/>. Designed to coexist
    /// with <see cref="ContainerFillController"/>; either can drive the same
    /// material because both write through a <see cref="MaterialPropertyBlock"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class LiquidColorController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Renderer")]
        [Tooltip("Renderer to tint. If null, the Renderer on this GameObject is used.")]
        [SerializeField] private Renderer _renderer;

        [Header("Shader Properties")]
        [Tooltip("Primary shader color property (matches ChemLabSim/Liquid).")]
        [SerializeField] private string _primaryProperty  = "_LiquidColor";
        [Tooltip("Fallback shader color property (matches URP/Lit).")]
        [SerializeField] private string _fallbackProperty = "_BaseColor";

        [Header("Lerp")]
        [Tooltip("Color blend speed when the chemistry engine progresses (units/sec).")]
        [SerializeField, Min(0f)] private float _lerpSpeed = 2f;

        // ── State ─────────────────────────────────────────────
        private MaterialPropertyBlock _mpb;
        private int _primaryPropId;
        private int _fallbackPropId;
        private bool _hasPrimaryProp;
        private bool _hasFallbackProp;

        private Color _baseColor;     // color before the current reaction started
        private Color _targetColor;   // hex from ReactionEntry.visual_effects.color_change
        private Color _currentColor;  // smoothed value written to the MPB
        private Color _clearColor;    // pristine "clear water" reset target
        private float _progress01;    // 0..1 from output.CompletionPercent
        private bool _hasTarget;
        private bool _registryReady;
        private bool _isVisible = true; // default: assume visible until OnBecameInvisible fires
        private bool _serviceErrorLogged;

        // ── Unity Lifecycle ───────────────────────────────────
        private void Awake()
        {
            if (_renderer == null) _renderer = GetComponent<Renderer>();

            _mpb = new MaterialPropertyBlock();
            _primaryPropId  = Shader.PropertyToID(_primaryProperty);
            _fallbackPropId = Shader.PropertyToID(_fallbackProperty);

            if (_renderer != null && _renderer.sharedMaterial != null)
            {
                var mat = _renderer.sharedMaterial;
                _hasPrimaryProp  = mat.HasProperty(_primaryPropId);
                _hasFallbackProp = mat.HasProperty(_fallbackPropId);

                if (_hasPrimaryProp)        _baseColor = mat.GetColor(_primaryPropId);
                else if (_hasFallbackProp)  _baseColor = mat.GetColor(_fallbackPropId);
                else                        _baseColor = Color.white;
            }
            else
            {
                _baseColor = Color.white;
            }

            _currentColor = _baseColor;
            _targetColor  = _baseColor;
            _clearColor   = _baseColor;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ChemistryProcessedEvent>(OnChemistryProcessed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChemistryProcessedEvent>(OnChemistryProcessed);
            ClearForPooling();
        }

        private IEnumerator Start()
        {
            // Wait until ReactionRegistry is registered so the first event we
            // receive can resolve cleanly. Avoids "Service Not Found" noise
            // during scene load.
            yield return new WaitUntil(() => ServiceLocator.Has<ReactionRegistry>());
            _registryReady = true;
        }

        // Frustum-culling driven CPU optimization — stop processing events
        // for vessels not currently on screen. Works because this controller
        // requires a Renderer.
        private void OnBecameVisible()   { _isVisible = true;  }
        private void OnBecameInvisible() { _isVisible = false; }

        private void Update()
        {
            if (_renderer == null) return;
            if (!_hasPrimaryProp && !_hasFallbackProp) return;

            // Two-stage interpolation: blend base→target by reaction progress,
            // then smooth toward that blended value over time so partial
            // reactions visibly sit between the original and product color.
            Color blendTarget = _hasTarget
                ? Color.Lerp(_baseColor, _targetColor, Mathf.Clamp01(_progress01))
                : _baseColor;

            float t = 1f - Mathf.Exp(-_lerpSpeed * Time.deltaTime);
            _currentColor = Color.Lerp(_currentColor, blendTarget, t);

            _renderer.GetPropertyBlock(_mpb);
            if (_hasPrimaryProp)  _mpb.SetColor(_primaryPropId,  _currentColor);
            if (_hasFallbackProp) _mpb.SetColor(_fallbackPropId, _currentColor);
            _renderer.SetPropertyBlock(_mpb);
        }

        // ── Event Handler ─────────────────────────────────────
        private void OnChemistryProcessed(ChemistryProcessedEvent evt)
        {
            // Resource guard: skip work for inactive or off-screen vessels.
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;
            if (!_isVisible) return;

            var output = evt.Output;
            if (string.IsNullOrEmpty(output.ReactionId))
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
            if (entry == null) return;

            // The data model stores the product color as a hex string under
            // visual_effects.color_change. Skip silently if absent or invalid.
            string hex = entry.visual_effects != null ? entry.visual_effects.color_change : null;
            if (string.IsNullOrWhiteSpace(hex)) return;
            if (!ColorUtility.TryParseHtmlString(hex.Trim(), out Color parsed)) return;

            _baseColor   = _currentColor;
            _targetColor = parsed;
            _hasTarget   = true;
            _progress01  = Mathf.Clamp01(output.CompletionPercent / 100f);
        }

        // ── Helpers ───────────────────────────────────────────
        /// <summary>Smoothly fade back to the pristine "clear water" color.</summary>
        private void ResetToDefault()
        {
            _baseColor   = _currentColor;
            _targetColor = _clearColor;
            _hasTarget   = true;
            _progress01  = 1f;
        }

        /// <summary>
        /// Object-pool friendly teardown: snap visuals back to the captured
        /// clear-water color, clear the MaterialPropertyBlock, and reset all
        /// per-reaction smoothing state so a recycled vessel cannot inherit
        /// "ghost" colors from a previous user.
        /// </summary>
        private void ClearForPooling()
        {
            _baseColor    = _clearColor;
            _targetColor  = _clearColor;
            _currentColor = _clearColor;
            _progress01   = 0f;
            _hasTarget    = false;

            if (_renderer != null && _mpb != null)
            {
                _renderer.GetPropertyBlock(_mpb);
                _mpb.Clear();
                if (_hasPrimaryProp)  _mpb.SetColor(_primaryPropId,  _clearColor);
                if (_hasFallbackProp) _mpb.SetColor(_fallbackPropId, _clearColor);
                _renderer.SetPropertyBlock(_mpb);
            }
        }

        private void LogServiceMissingOnce()
        {
            if (_serviceErrorLogged) return;
            _serviceErrorLogged = true;
            Debug.LogError($"[LiquidColorController] '{name}': ReactionRegistry service unavailable. Disabling Update loop to prevent log spam.", this);
            enabled = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_renderer == null && GetComponent<Renderer>() == null)
                Debug.LogWarning($"[LiquidColorController] '{name}': no Renderer assigned and none on this GameObject.", this);
        }
#endif
    }
}
