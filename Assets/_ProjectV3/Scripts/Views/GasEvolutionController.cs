// ChemLabSim v3 — Gas Evolution Controller
// Isolated vapor/smoke driver. Subscribes to ChemistryProcessedEvent,
// derives gaseous-product yield from the chemistry output, and drives
// a single ParticleSystem (vapor) whose emission rate scales with the
// rate of gas production between successive ticks.
//
// API note: The original prompt referenced a few APIs that don't exist
// verbatim in this codebase. Per the "Do NOT modify any existing scripts"
// rule, this file maps the intent onto the real public surface:
//   ChemistryOutput.GasYield
//       → SUM(s.MolesFinal) over s in output.Substances
//         where s.IsProduct && s.Phase == Phase.Gas
//   modernEntry.visual_effects.vapor_color
//       → No vapor_color field exists on ReactionVisualEffects today.
//         We honor the user's intent: if a future field is added it can
//         be wired in here; for now we fall back to entry.visual_effects
//         .color_change (when the reaction is flagged .gas), and
//         finally to a light-grey steam tint. The two fallbacks let
//         existing data drive the look without modifying any other file.
//
// Positioning: searches the vessel for a child Transform named
// "GasExitPoint" (preferred) or "Rim", then parents the ParticleSystem
// there so emission happens at the top of the vessel rather than at
// the centre of the liquid.

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
    /// Plays a vapor/smoke <see cref="ParticleSystem"/> in proportion to the
    /// rate of gas production reported by the chemistry engine. Looks up the
    /// reaction's vapor tint via <see cref="ReactionRegistry"/> and falls
    /// back to a neutral steam color when none is defined.
    /// </summary>
    [DisallowMultipleComponent]
    public class GasEvolutionController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Particle System")]
        [Tooltip("Vapor particles. If null, a child ParticleSystem is searched. " +
                 "It is auto-parented to GasExitPoint/Rim when found.")]
        [SerializeField] private ParticleSystem _vapor;

        [Header("Emission Mapping")]
        [Tooltip("Maximum emission rate when gas production rate >= MaxGasRate (particles/sec).")]
        [SerializeField, Min(0f)] private float _maxEmissionRate = 60f;

        [Tooltip("Gas production rate (mol/sec) that maps to maximum emission.")]
        [SerializeField, Min(0.0001f)] private float _maxGasRate = 0.5f;

        [Tooltip("Below this rate (mol/sec) the vapor is considered dormant and stops.")]
        [SerializeField, Min(0f)] private float _minGasRate = 0.005f;

        [Tooltip("Smoothing speed for emission rate (units/sec).")]
        [SerializeField, Min(0f)] private float _lerpSpeed = 4f;

        [Header("Color")]
        [Tooltip("Default tint when the reaction does not define a vapor color.")]
        [SerializeField] private Color _defaultVaporColor = new Color(0.92f, 0.92f, 0.92f, 0.6f);

        [Tooltip("Per-tick ±jitter applied to the vapor alpha to break up uniform smoke.")]
        [SerializeField, Range(0f, 0.4f)] private float _alphaJitter = 0.12f;

        [Header("Positioning")]
        [Tooltip("Preferred child Transform name to emit from.")]
        [SerializeField] private string _primaryAnchorName  = "GasExitPoint";
        [Tooltip("Fallback child Transform name to emit from.")]
        [SerializeField] private string _fallbackAnchorName = "Rim";

        // ── State ─────────────────────────────────────────────
        private ParticleSystem.EmissionModule _emission;
        private ParticleSystem.MainModule     _main;
        private bool  _hasParticles;
        private bool  _registryReady;
        private bool  _isVisible = true; // default: assume visible until OnBecameInvisible fires
        private bool  _serviceErrorLogged;

        private float _targetRate;
        private float _currentRate;
        private bool  _isEvolving;

        // For deriving "rate of gas production in the current tick".
        private string _lastReactionId;
        private float  _lastGasMoles;
        private float  _lastEventTime;

        // ── Unity Lifecycle ───────────────────────────────────
        private void Awake()
        {
            if (_vapor == null)
                _vapor = GetComponentInChildren<ParticleSystem>(true);

            if (_vapor != null)
            {
                ReparentToAnchor(_vapor.transform);

                _emission = _vapor.emission;
                _main     = _vapor.main;
                _hasParticles = true;

                _emission.rateOverTime = 0f;
                _main.startColor       = _defaultVaporColor;

                if (_vapor.isPlaying)
                    _vapor.Stop(true, ParticleSystemStopBehavior.StopEmitting);
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
            if (_hasParticles && _vapor != null && _vapor.isPlaying)
                _vapor.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ClearForPooling();
        }

        private IEnumerator WaitForRegistry()
        {
            // Wait until ReactionRegistry is registered before resolving
            // vapor tints. Prevents service-not-found errors during scene load.
            yield return new WaitUntil(() => ServiceLocator.Has<ReactionRegistry>());
            _registryReady = true;
        }

        // Frustum-culling driven CPU optimization. Unity invokes these on
        // GameObjects with a Renderer (e.g. the ParticleSystem's renderer).
        private void OnBecameVisible()   { _isVisible = true;  }
        private void OnBecameInvisible() { _isVisible = false; }

        private void Update()
        {
            if (!_hasParticles) return;

            float t = 1f - Mathf.Exp(-_lerpSpeed * Time.deltaTime);
            _currentRate = Mathf.Lerp(_currentRate, _targetRate, t);
            _emission.rateOverTime = _currentRate;

            if (!_isEvolving && _currentRate <= 0.05f && _vapor.isPlaying)
                _vapor.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // ── Event Handler ─────────────────────────────────────
        private void OnChemistryProcessed(ChemistryProcessedEvent evt)
        {
            // Resource guard: skip work for inactive or off-screen vessels.
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;
            if (!_isVisible) return;
            if (!_hasParticles) return;

            var output = evt.Output;
            if (!output.Found || string.IsNullOrEmpty(output.ReactionId))
            {
                ResetToDefault();
                return;
            }

            float gasMoles = SumGasMoles(output.Substances);
            if (gasMoles <= 0f)
            {
                // Clean reset: 0-mole event → fade out, no rate maths needed.
                ResetToDefault();
                return;
            }

            // Derive a per-tick rate (mol/sec). Reset the running total when
            // the reaction id changes so the first tick of a new reaction
            // doesn't produce a misleading negative or huge delta.
            bool sameReaction = (output.ReactionId == _lastReactionId);
            float now = Time.time;

            // Zero-division guard: floor every divisor at 0.0001 s. Belt-and-
            // braces — also wrap Time.deltaTime so a paused editor frame
            // (deltaTime == 0) cannot crash the calculation.
            float rawDt = sameReaction ? (now - _lastEventTime) : Mathf.Max(Time.deltaTime, 0.0001f);
            float dt    = Mathf.Max(rawDt, 0.0001f);

            float deltaMoles = sameReaction
                ? Mathf.Max(gasMoles - _lastGasMoles, 0f)
                : gasMoles;
            float ratePerSec = deltaMoles / dt;

            _lastReactionId = output.ReactionId;
            _lastGasMoles   = gasMoles;
            _lastEventTime  = now;

            if (ratePerSec < _minGasRate)
            {
                StopEvolution();
                return;
            }

            // Resolve tint: dominant-gas palette → reaction JSON → default steam.
            // Apply a small alpha jitter so the smoke reads as swirling, not flat.
            string dominantGas = ResolveDominantGasFormula(output.Substances);
            Color tint = ResolveVaporColor(output.ReactionId, dominantGas);
            tint.a = Mathf.Clamp01(tint.a + Random.Range(-_alphaJitter, _alphaJitter));
            _main.startColor = tint;

            float intensity01 = Mathf.Clamp01(ratePerSec / _maxGasRate);
            _targetRate = _maxEmissionRate * intensity01;
            _isEvolving = true;

            if (!_vapor.isPlaying) _vapor.Play();
        }

        // ── Helpers ───────────────────────────────────────────
        /// <summary>Smoothly fade vapor out and clear all per-reaction history.</summary>
        private void ResetToDefault()
        {
            StopEvolution();
            ResetTickHistory();
        }

        private void StopEvolution()
        {
            _isEvolving = false;
            _targetRate = 0f;
            if (_hasParticles) _main.startColor = _defaultVaporColor;
            // Actual Stop() happens in Update once _currentRate decays to ~0.
        }

        private void ResetTickHistory()
        {
            _lastReactionId = null;
            _lastGasMoles   = 0f;
            _lastEventTime  = 0f;
        }

        /// <summary>
        /// Object-pool friendly teardown: stop AND clear live vapor particles,
        /// reset color to the default steam, and clear all per-tick history.
        /// A recycled vessel must not inherit the previous reaction's smoke.
        /// </summary>
        private void ClearForPooling()
        {
            _isEvolving  = false;
            _targetRate  = 0f;
            _currentRate = 0f;
            ResetTickHistory();

            if (_hasParticles && _vapor != null)
            {
                _emission.rateOverTime = 0f;
                _main.startColor       = _defaultVaporColor;
                _vapor.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void LogServiceMissingOnce()
        {
            if (_serviceErrorLogged) return;
            _serviceErrorLogged = true;
            Debug.LogError($"[GasEvolutionController] '{name}': ReactionRegistry service unavailable. Disabling Update loop to prevent log spam.", this);
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
        /// Returns the formula of the gas-phase product with the highest
        /// MolesFinal in the substance list, or null when none qualify.
        /// Used to drive the chemical-specific vapor tint.
        /// </summary>
        private static string ResolveDominantGasFormula(List<SubstanceState> substances)
        {
            if (substances == null) return null;
            string best = null;
            float bestMoles = 0f;
            for (int i = 0; i < substances.Count; i++)
            {
                var s = substances[i];
                if (!s.IsProduct || s.Phase != Phase.Gas) continue;
                if (s.MolesFinal > bestMoles)
                {
                    bestMoles = s.MolesFinal;
                    best      = s.Formula;
                }
            }
            return best;
        }

        private Color ResolveVaporColor(string reactionId, string dominantGasFormula)
        {
            // 1) Chemical-specific palette wins when we have a known dominant gas.
            //    Preserves the vapor-default alpha so it still reads as gas.
            if (GasColorPalette.HasMapping(dominantGasFormula))
            {
                Color paletteTint = GasColorPalette.Resolve(dominantGasFormula);
                paletteTint.a = _defaultVaporColor.a;
                return paletteTint;
            }

            // 2) Fall back to the reaction's JSON-driven color_change (if flagged as gas).
            if (ServiceLocator.Has<ReactionRegistry>())
            {
                var registry = ServiceLocator.Get<ReactionRegistry>();
                if (registry == null)
                {
                    LogServiceMissingOnce();
                    return _defaultVaporColor;
                }

                var entry = registry.FindById(reactionId);
                var vfx   = entry != null ? entry.visual_effects : null;
                if (vfx != null && vfx.gas && !string.IsNullOrWhiteSpace(vfx.color_change) &&
                    ColorUtility.TryParseHtmlString(vfx.color_change.Trim(), out Color parsed))
                {
                    parsed.a = _defaultVaporColor.a;
                    return parsed;
                }
            }

            // 3) Safety net: translucent steam.
            return _defaultVaporColor;
        }

        private void ReparentToAnchor(Transform particleTransform)
        {
            Transform anchor = FindChildByName(transform, _primaryAnchorName)
                            ?? FindChildByName(transform, _fallbackAnchorName);
            if (anchor == null || anchor == particleTransform.parent) return;

            particleTransform.SetParent(anchor, worldPositionStays: false);
            particleTransform.localPosition = Vector3.zero;
            particleTransform.localRotation = Quaternion.identity;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;

            // Depth-first recursive search — vessels may nest the anchor
            // under a mesh child (e.g., Beaker/Mesh/Rim).
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
            if (_vapor == null && GetComponentInChildren<ParticleSystem>(true) == null)
                Debug.LogWarning($"[GasEvolutionController] '{name}': no ParticleSystem assigned and none found in children.", this);
        }
#endif
    }
}
