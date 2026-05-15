// ChemLabSim v3 — Vessel Info Display (per-vessel HUD)
// Isolated world-space HUD that subscribes to ChemistryProcessedEvent
// and shows the active reaction name, current temperature, and a
// derived status label ("Stable" / "Boiling" / "Evolving Gas").
//
// Conventions match the rest of the Views layer:
//   • Auto-resolves child references in Awake (no scene wiring required
//     when scaffolded by VesselTemplateBuilder).
//   • Subscribes to EventBus in OnEnable / Unsubscribes in OnDisable.
//   • Waits until ServiceLocator has ReactionRegistry before reading it.
//   • Resets to defaults on null/empty ReactionId.
//   • ClearForPooling() invoked from OnDisable to avoid object-pool ghosts.
//   • LogServiceMissingOnce() to prevent console spam.
//   • #if UNITY_EDITOR OnValidate() warns when refs are missing.
//   • LateUpdate billboards + fades so the HUD reflects the vessel's
//     final transformed position each frame.
//
// API note: status detection mirrors the logic used by
// LiquidVFXController (boil over MIN(products[*].boilingPointC)) and
// GasEvolutionController (any product where Phase == Gas && MolesFinal > 0).

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ChemLabSimV3.Core;
using ChemLabSimV3.Engine;
using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Views
{
    /// <summary>
    /// World-space HUD attached as a child of <c>[Vessel_Template]</c>.
    /// Subscribes to <see cref="ChemistryProcessedEvent"/> and renders
    /// reaction name, temperature, and a derived status label, while
    /// billboarding to the active camera and fading by distance.
    /// </summary>
    [DisallowMultipleComponent]
    public class VesselInfoDisplay : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("References")]
        [Tooltip("Root Canvas. Auto-resolved from children when null.")]
        [SerializeField] private Canvas _canvas;

        [Tooltip("CanvasGroup used for fade. Auto-resolved from children when null.")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Tooltip("Reaction-name label. Auto-resolved by child name when null.")]
        [SerializeField] private TMP_Text _reactionNameText;

        [Tooltip("Temperature label. Auto-resolved by child name when null.")]
        [SerializeField] private TMP_Text _temperatureText;

        [Tooltip("Status label (Stable / Boiling / Evolving Gas). Auto-resolved by child name when null.")]
        [SerializeField] private TMP_Text _statusText;

        [Header("Billboard")]
        [Tooltip("If true, the HUD also rolls to match camera up. " +
                 "Off by default so the HUD stays vertical in the world.")]
        [SerializeField] private bool _matchCameraRoll = false;

        [Header("Fade")]
        [Tooltip("Below this distance from the camera, alpha is fully 1.")]
        [SerializeField, Min(0f)] private float _minDistance = 0.5f;

        [Tooltip("At and beyond this distance from the camera, alpha is 0.")]
        [SerializeField, Min(0.1f)] private float _maxDistance = 6f;

        [Tooltip("Seconds to ease alpha between the previous and target value.")]
        [SerializeField, Min(0f)] private float _fadeSeconds = 0.25f;

        [Tooltip("If true, the HUD only fades in when the camera looks at it (raycast gaze).")]
        [SerializeField] private bool _useGazeFade = false;

        [Tooltip("Maximum cosine angle (1 = dead-on, 0 = 90°) at which gaze fade triggers.")]
        [SerializeField, Range(0.5f, 1f)] private float _gazeDot = 0.92f;

        [Header("Status Labels")]
        [SerializeField] private string _statusStable      = "Stable";
        [SerializeField] private string _statusBoiling     = "Boiling";
        [SerializeField] private string _statusEvolvingGas = "Evolving Gas";
        [SerializeField] private string _statusIdle        = "Idle";

        // Canonical child names used by VesselTemplateBuilder.
        public const string CanvasName        = "InfoCanvas";
        public const string ReactionNameField = "ReactionNameText";
        public const string TemperatureField  = "TemperatureText";
        public const string StatusField       = "StatusText";

        // ── State ─────────────────────────────────────────────
        private bool   _registryReady;
        private bool   _serviceErrorLogged;
        private Camera _cam;
        private string _currentReactionId;
        private float  _currentBoilingC = float.NaN; // cached MIN(products[*].boilingPointC)

        // ── Unity lifecycle ───────────────────────────────────
        private void Awake()
        {
            ResolveReferences();
        }

        private IEnumerator WaitForRegistry()
        {
            yield return new WaitUntil(() => ServiceLocator.Has<ReactionRegistry>());
            _registryReady = true;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ChemistryProcessedEvent>(OnChemistryProcessed);
            if (!_registryReady) StartCoroutine(WaitForRegistry());
            ResetToDefault();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChemistryProcessedEvent>(OnChemistryProcessed);
            StopAllCoroutines(); // prevent leaked WaitUntil from a disabled instance
            ClearForPooling();
        }

        private void LateUpdate()
        {
            if (!isActiveAndEnabled) return;
            EnsureCamera();
            if (_cam == null) return;

            BillboardToCamera();
            UpdateFade();
        }

        // ── Event handler ─────────────────────────────────────
        private void OnChemistryProcessed(ChemistryProcessedEvent evt)
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;

            var output = evt.Output;
            if (string.IsNullOrEmpty(output.ReactionId))
            {
                ResetToDefault();
                return;
            }

            // Reaction name resolution: ReactionName → registry → substances summary.
            string reactionLabel = ResolveReactionName(output);

            // Cache lowest product boiling point for status detection.
            _currentReactionId = output.ReactionId;
            _currentBoilingC   = ResolveLowestBoilingPoint(output.ReactionId);

            string status = DetermineStatus(output, _currentBoilingC);

            if (_reactionNameText != null) _reactionNameText.text = reactionLabel;
            if (_temperatureText  != null) _temperatureText.text  = $"{output.TemperatureC:0.0}°C";
            if (_statusText       != null) _statusText.text       = status;
        }

        // ── Helpers ───────────────────────────────────────────
        private void ResolveReferences()
        {
            if (_canvas == null) _canvas = GetComponentInChildren<Canvas>(true);
            if (_canvasGroup == null && _canvas != null)
            {
                _canvasGroup = _canvas.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                    _canvasGroup = _canvas.gameObject.AddComponent<CanvasGroup>();
            }

            if (_reactionNameText == null) _reactionNameText = FindChildText(ReactionNameField);
            if (_temperatureText  == null) _temperatureText  = FindChildText(TemperatureField);
            if (_statusText       == null) _statusText       = FindChildText(StatusField);
        }

        private TMP_Text FindChildText(string childName)
        {
            var t = transform.Find(childName);
            if (t != null)
            {
                var tmp = t.GetComponent<TMP_Text>();
                if (tmp != null) return tmp;
            }
            // Deep search by name as a fallback.
            var all = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].gameObject.name == childName) return all[i];
            return null;
        }

        private string ResolveReactionName(ChemistryOutput output)
        {
            if (!string.IsNullOrEmpty(output.ReactionName))
                return output.ReactionName;

            if (_registryReady && ServiceLocator.Has<ReactionRegistry>())
            {
                var entry = ServiceLocator.Get<ReactionRegistry>().FindById(output.ReactionId);
                if (entry != null)
                {
                    if (!string.IsNullOrEmpty(entry.name_en)) return entry.name_en;
                    if (!string.IsNullOrEmpty(entry.id))      return entry.id;
                }
            }
            else if (!_registryReady && !ServiceLocator.Has<ReactionRegistry>())
            {
                LogServiceMissingOnce();
            }

            // Fallback: build a compact "A + B → C" hint from the substances list.
            if (output.Substances != null && output.Substances.Count > 0)
                return BuildSubstanceSummary(output.Substances);

            return output.ReactionId;
        }

        private static string BuildSubstanceSummary(List<SubstanceState> substances)
        {
            string reactants = "";
            string products  = "";
            for (int i = 0; i < substances.Count; i++)
            {
                var s = substances[i];
                if (string.IsNullOrEmpty(s.Formula)) continue;
                if (s.IsReactant)
                    reactants += (reactants.Length == 0 ? "" : " + ") + s.Formula;
                else if (s.IsProduct)
                    products  += (products.Length  == 0 ? "" : " + ") + s.Formula;
            }
            if (reactants.Length == 0 && products.Length == 0) return "—";
            if (products.Length == 0) return reactants;
            if (reactants.Length == 0) return products;
            return reactants + " → " + products;
        }

        private float ResolveLowestBoilingPoint(string reactionId)
        {
            if (string.IsNullOrEmpty(reactionId))            return float.NaN;
            if (!_registryReady)                              return float.NaN;
            if (!ServiceLocator.Has<ReactionRegistry>())
            {
                LogServiceMissingOnce();
                return float.NaN;
            }

            var entry = ServiceLocator.Get<ReactionRegistry>().FindById(reactionId);
            if (entry == null || entry.products == null || entry.products.Count == 0)
                return float.NaN;

            float min = float.NaN;
            for (int i = 0; i < entry.products.Count; i++)
            {
                float bp = entry.products[i].boilingPointC;
                if (float.IsNaN(bp)) continue;
                if (float.IsNaN(min) || bp < min) min = bp;
            }
            return min;
        }

        private string DetermineStatus(ChemistryOutput output, float boilingC)
        {
            // Evolving Gas: any product gas with positive moles.
            if (output.Substances != null)
            {
                for (int i = 0; i < output.Substances.Count; i++)
                {
                    var s = output.Substances[i];
                    if (s.IsProduct && s.Phase == Phase.Gas && s.MolesFinal > 0f)
                    {
                        // Boiling supersedes "Evolving Gas" when temperature crosses the threshold.
                        if (!float.IsNaN(boilingC) && output.TemperatureC >= boilingC)
                            return _statusBoiling;
                        return _statusEvolvingGas;
                    }
                }
            }

            // Boiling without an explicit gas product (e.g. solvent vaporization).
            if (!float.IsNaN(boilingC) && output.TemperatureC >= boilingC)
                return _statusBoiling;

            if (!output.Found) return _statusIdle;
            return _statusStable;
        }

        private void EnsureCamera()
        {
            if (_cam != null && _cam.isActiveAndEnabled) return;
            _cam = Camera.main;
        }

        private void BillboardToCamera()
        {
            // Face the camera by aligning the HUD's forward with the camera's forward.
            // This keeps text readable regardless of which side of the vessel the user is on.
            Vector3 fwd = _cam.transform.forward;
            if (fwd.sqrMagnitude < 1e-6f) return;
            Vector3 up = _matchCameraRoll ? _cam.transform.up : Vector3.up;
            transform.rotation = Quaternion.LookRotation(fwd, up);
        }

        private void UpdateFade()
        {
            if (_canvasGroup == null) return;

            float dist = Vector3.Distance(transform.position, _cam.transform.position);
            float target = Mathf.InverseLerp(_maxDistance, _minDistance, dist); // 1 when close, 0 when far

            if (_useGazeFade)
            {
                Vector3 toHud = transform.position - _cam.transform.position;
                float sq = toHud.sqrMagnitude;
                if (sq > 1e-6f)
                {
                    Vector3 dir = toHud / Mathf.Sqrt(sq);
                    float dot = Vector3.Dot(_cam.transform.forward, dir);
                    if (dot < _gazeDot) target = 0f;
                }
            }

            float step = _fadeSeconds <= 0f ? 1f : Time.deltaTime / _fadeSeconds;
            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, target, step);

            // GPU culling: when fully faded out, disable the Canvas so it
            // skips the entire UI render pass (raycasts + batching). Re-enable
            // the moment the alpha begins to climb again.
            if (_canvas != null)
            {
                bool shouldRender = _canvasGroup.alpha > 0f;
                if (_canvas.enabled != shouldRender) _canvas.enabled = shouldRender;
            }
        }

        private void ResetToDefault()
        {
            _currentReactionId = null;
            _currentBoilingC   = float.NaN;
            if (_reactionNameText != null) _reactionNameText.text = "—";
            if (_temperatureText  != null) _temperatureText.text  = "—";
            if (_statusText       != null) _statusText.text       = _statusIdle;
        }

        private void ClearForPooling()
        {
            ResetToDefault();
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            if (_canvas != null) _canvas.enabled = false;
        }

        private void LogServiceMissingOnce()
        {
            if (_serviceErrorLogged) return;
            _serviceErrorLogged = true;
            Debug.LogError(
                $"[VesselInfoDisplay] ReactionRegistry not registered in ServiceLocator. " +
                $"Disabling '{name}' to prevent console spam.", this);
            enabled = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_maxDistance <= _minDistance) _maxDistance = _minDistance + 0.1f;

            if (_canvas == null && GetComponentInChildren<Canvas>(true) == null)
                Debug.LogWarning($"[VesselInfoDisplay] '{name}' has no child Canvas. " +
                                 "Run ChemLabSim/Vessels/Standardize Selected Vessel.", this);

            if (_reactionNameText == null && _temperatureText == null && _statusText == null)
                Debug.LogWarning($"[VesselInfoDisplay] '{name}' has no TMP_Text fields wired. " +
                                 "Run ChemLabSim/Vessels/Standardize Selected Vessel.", this);
        }
#endif
    }
}
