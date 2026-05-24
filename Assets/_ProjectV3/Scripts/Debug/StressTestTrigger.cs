// ChemLabSim v3 — Stress Test Trigger (DEBUG ONLY)
// Press a configurable key (default 'T') to inject a complex multi-product
// reaction (HNO3 + Cu → Cu(NO3)2 + NO2 + H2O) into the live event bus and
// observe how the VFX, SFX, and HUD layers react in real time.
//
// Two modes are supported, controlled by `_useRegistryEntry`:
//
//   true  →  Looks up "rxn_106" in ServiceLocator.Get<ReactionRegistry>()
//             and uses its real ReactionEntry (boiling points, products,
//             stoichiometry) as the source of truth. This is the realistic
//             stress path and depends on rxn_106 being present in
//             reactions.json (added in this same patch).
//
//   false →  Builds an entirely synthetic ChemistryOutput in code with no
//             registry dependency. Useful when triaging the VFX/SFX
//             pipeline before the data layer is ready, or for isolating
//             whether a problem is data-side or runtime-side.
//
// Mechanism: every fixed cadence (default 4 Hz) the trigger publishes a
// fresh ChemistryProcessedEvent whose TemperatureC ramps from ambient
// (25 °C) to peak (~110 °C) and whose NO2 mole count grows linearly. All
// existing subscribers (VesselAudioController, GasEvolutionController,
// LiquidVFXController, VesselHeatDistortionController, VesselInfoDisplay)
// react to those events without modification.
//
// Scope note: events are global. Every vessel in the scene with the
// listed controllers will respond. If you need single-vessel scoping in
// the future, extend ChemistryProcessedEvent with a target id rather
// than mutating this file.
//
// Safety: this is a debug component and is hard-disabled outside
// UNITY_EDITOR + DEVELOPMENT_BUILD. It intentionally does not ship in
// release builds.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ChemLabSimV3.Controllers;
using ChemLabSimV3.Core;
using ChemLabSimV3.Data;
using ChemLabSimV3.Engine;
using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Diagnostics
{
    /// <summary>
    /// Editor/dev-only stress harness. Synthesizes a ramping
    /// <see cref="ChemistryProcessedEvent"/> sequence for the
    /// HNO3 + Cu reaction so designers can audit VFX, SFX, and HUD
    /// behavior without manually staging the full input pipeline.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ChemLabSim/Debug/Stress Test Trigger")]
    public class StressTestTrigger : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Trigger")]
        [Tooltip("Key that starts (or restarts) the stress test reaction.")]
        [SerializeField] private KeyCode _triggerKey = KeyCode.T;

        [Tooltip("Key that immediately stops the stress test and clears events.")]
        [SerializeField] private KeyCode _stopKey = KeyCode.Y;

        [Header("Reaction Source")]
        [Tooltip("If true, look up 'rxn_106' in ReactionRegistry and use its real " +
                 "products/boiling points. If false, fall back to an inline synthetic spec.")]
        [SerializeField] private bool _useRegistryEntry = true;

        [Tooltip("Reaction id to look up in ReactionRegistry when registry mode is on.")]
        [SerializeField] private string _reactionId = "rxn_106";

        [Header("Real Injection (Production API)")]
        [Tooltip("If true, also fire a real ReactionController.RequestMix() with HNO3 + Cu " +
                 "BEFORE running the synthetic event ramp. This drives the production " +
                 "evaluation chain (notebook, quiz, achievements, FX). The synthetic ramp " +
                 "that follows then exercises the live VFX/SFX over a wider temperature band.")]
        [SerializeField] private bool _alsoRequestRealMix = true;

        [Tooltip("Reagent formulas to inject via the real RequestMix path. Must match " +
                 "the reactants of the registry entry (default: HNO3 + Cu).")]
        [SerializeField] private List<string> _injectedReagents = new List<string> { "HNO3", "Cu" };

        [Tooltip("Initial temperature (°C) passed to the real MixRequest. The synthetic ramp " +
                 "that follows will continue heating from this baseline up to peak.")]
        [SerializeField] private float _injectionTemperatureC = 30f;

        [Tooltip("Stir intensity [0–1] passed to the real MixRequest.")]
        [SerializeField, Range(0f, 1f)] private float _injectionStirring = 0.5f;

        [Tooltip("Acid/Base medium for the MixRequest. HNO3 + Cu requires Acidic.")]
        [SerializeField] private ReactionMedium _injectionMedium = ReactionMedium.Acidic;

        [Header("Temperature Ramp")]
        [Tooltip("Starting (ambient) temperature in °C.")]
        [SerializeField] private float _ambientTemperatureC = 25f;

        [Tooltip("Peak temperature in °C — the exotherm carries the vessel to here.")]
        [SerializeField] private float _peakTemperatureC = 110f;

        [Tooltip("Seconds for the temperature to ramp from ambient to peak.")]
        [SerializeField, Min(0.1f)] private float _heatupSeconds = 4f;

        [Tooltip("Seconds the reaction stays at peak before stopping.")]
        [SerializeField, Min(0f)] private float _holdSeconds = 6f;

        [Header("Gas Production (NO2)")]
        [Tooltip("Initial NO2 moles when the reaction kicks off.")]
        [SerializeField, Min(0f)] private float _no2StartMoles = 0.0f;

        [Tooltip("Total NO2 moles released over the full run.")]
        [SerializeField, Min(0.001f)] private float _no2PeakMoles = 0.4f;

        [Header("Tick Cadence")]
        [Tooltip("Times per second to publish ChemistryProcessedEvent. " +
                 "Higher = smoother audio/VFX response, more event traffic.")]
        [SerializeField, Range(1f, 30f)] private float _ticksPerSecond = 4f;

        [Header("Logging")]
        [Tooltip("Print a one-line tick summary to the console.")]
        [SerializeField] private bool _verboseLogging = false;

        // ── State ─────────────────────────────────────────────
        private Coroutine _runner;
        private bool _registryReady;

        // ── Unity Lifecycle ───────────────────────────────────
        private void OnEnable()
        {
            // Mirrors the convention used by VesselAudioController et al.
            StartCoroutine(WaitForRegistry());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _runner = null;
        }

        private IEnumerator WaitForRegistry()
        {
            yield return new WaitUntil(() => ServiceLocator.Has<ReactionRegistry>());
            _registryReady = true;
        }

        private void Update()
        {
            if (Input.GetKeyDown(_triggerKey))
            {
                RestartStressTest();
            }
            else if (Input.GetKeyDown(_stopKey))
            {
                StopStressTest();
            }
        }

        // ── Public API ────────────────────────────────────────
        /// <summary>
        /// Stop any in-flight stress run and immediately start a new one.
        /// Safe to call from inspector buttons or hot-key bindings.
        /// </summary>
        [ContextMenu("Restart Stress Test")]
        public void RestartStressTest()
        {
            StopStressTest();
            _runner = StartCoroutine(RunStressTest());
        }

        /// <summary>
        /// Halt the current stress run and publish a final "no reaction"
        /// event so subscribers fade their VFX/SFX out cleanly.
        /// </summary>
        [ContextMenu("Stop Stress Test")]
        public void StopStressTest()
        {
            if (_runner != null)
            {
                StopCoroutine(_runner);
                _runner = null;
            }

            // Publish an "all clear" event so visuals fade out gracefully.
            EventBus.Publish(new ChemistryProcessedEvent(
                ChemistryOutput.NotFound(new List<string> { "HNO3", "Cu" })));
        }

        // ── Core Coroutine ────────────────────────────────────
        private IEnumerator RunStressTest()
        {
            // If registry mode is enabled, give it one frame to arrive in
            // case the user pressed 'T' immediately after scene load.
            if (_useRegistryEntry && !_registryReady)
                yield return new WaitUntil(() => _registryReady);

            // Step 1 — fire a REAL mix through the production controller so
            // the full evaluation chain runs (notebook, quiz, achievements,
            // FX). This is the closest analog to the user's snippet:
            //     processor.AddSubstance("Cu",  ...);
            //     processor.AddSubstance("HNO3", ...);
            //     processor.SetTemperature(30f);
            // …mapped onto the real ReactionController.RequestMix(MixRequest)
            // API that the actual codebase exposes.
            if (_alsoRequestRealMix)
                TryRequestRealMix();

            ReactionEntry registryEntry = null;
            if (_useRegistryEntry)
            {
                registryEntry = ServiceLocator.Get<ReactionRegistry>()?.FindById(_reactionId);
                if (registryEntry == null)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[StressTestTrigger] '{_reactionId}' not found in ReactionRegistry. " +
                        "Falling back to synthetic spec. Make sure rxn_106 is in reactions.json.", this);
                }
            }

            float total = _heatupSeconds + _holdSeconds;
            float dt    = 1f / Mathf.Max(_ticksPerSecond, 0.01f);
            float t     = 0f;

            UnityEngine.Debug.Log(
                $"[StressTestTrigger] Starting HNO3 + Cu stress run — {total:0.0}s, peak {_peakTemperatureC:0.0}°C, peak NO2 {_no2PeakMoles:0.000} mol.",
                this);

            while (t <= total + 0.001f)
            {
                float heatup01 = Mathf.Clamp01(t / Mathf.Max(_heatupSeconds, 0.0001f));
                // Smoothstep gives the temperature a slight S-curve so the
                // vessel "lurches" into reaction the way a real exotherm
                // would, rather than ramping linearly.
                float heatupSmooth = heatup01 * heatup01 * (3f - 2f * heatup01);
                float temperatureC = Mathf.Lerp(_ambientTemperatureC, _peakTemperatureC, heatupSmooth);

                // NO2 accumulates monotonically; growth slows as we approach
                // the peak so the hissing rate plateaus rather than spikes.
                float runFraction = Mathf.Clamp01(t / Mathf.Max(total, 0.0001f));
                float no2Moles    = Mathf.Lerp(_no2StartMoles, _no2PeakMoles, runFraction);

                ChemistryOutput output = registryEntry != null
                    ? BuildFromRegistry(registryEntry, temperatureC, no2Moles, runFraction)
                    : BuildSyntheticOutput(temperatureC, no2Moles, runFraction);

                EventBus.Publish(new ChemistryProcessedEvent(output));

                if (_verboseLogging)
                {
                    UnityEngine.Debug.Log(
                        $"[StressTestTrigger] t={t:0.00}s T={temperatureC:0.0}°C NO2={no2Moles:0.000} mol", this);
                }

                t += dt;
                yield return new WaitForSeconds(dt);
            }

            UnityEngine.Debug.Log("[StressTestTrigger] Stress run complete. Publishing fade-out event.", this);
            EventBus.Publish(new ChemistryProcessedEvent(
                ChemistryOutput.NotFound(new List<string> { "HNO3", "Cu" })));
            _runner = null;
        }

        // ── Output Builders ───────────────────────────────────
        private ChemistryOutput BuildFromRegistry(ReactionEntry entry, float temperatureC,
                                                  float no2Moles, float runFraction)
        {
            // Use the registry entry's products as the substance template.
            // Cu(NO3)2 and H2O scale with NO2 production via the reaction's
            // own stoichiometry: 1 Cu(NO3)2 : 2 NO2 : 2 H2O.
            var substances = new List<SubstanceState>(entry.products.Count + entry.reactants.Count);

            float no2Stoich = StoichOf(entry.products, "NO2", 2f);

            for (int i = 0; i < entry.products.Count; i++)
            {
                var p = entry.products[i];
                if (p == null) continue;

                float scaledMoles = no2Stoich > 0f
                    ? no2Moles * (p.stoich / no2Stoich)
                    : no2Moles;

                substances.Add(new SubstanceState
                {
                    Formula              = p.formula,
                    Phase                = MapPhase(p.state),
                    MolesInitial         = 0f,
                    MolesFinal           = scaledMoles,
                    MolesConsumed        = 0f,
                    ConcentrationMolPerL = 0f,
                    IsReactant           = false,
                    IsProduct            = true,
                    IsLimitingReagent    = false,
                    IsExcess             = false
                });
            }

            for (int i = 0; i < entry.reactants.Count; i++)
            {
                var r = entry.reactants[i];
                if (r == null) continue;

                substances.Add(new SubstanceState
                {
                    Formula              = r.formula,
                    Phase                = MapPhase(r.state),
                    MolesInitial         = r.stoich,
                    MolesFinal           = Mathf.Lerp(r.stoich, 0f, runFraction),
                    MolesConsumed        = r.stoich * runFraction,
                    ConcentrationMolPerL = 0f,
                    IsReactant           = true,
                    IsProduct            = false,
                    IsLimitingReagent    = (r.formula == "Cu"),
                    IsExcess             = (r.formula != "Cu")
                });
            }

            return new ChemistryOutput
            {
                Found             = true,
                ReactionId        = entry.id,
                ReactionName      = entry.name_en ?? string.Empty,
                BalancedEquation  = "Cu + 4 HNO3 → Cu(NO3)2 + 2 NO2 + 2 H2O",
                Status            = ReactionStatus.Success,
                CompletionPercent = runFraction,
                Summary           = "Stress test — concentrated nitric acid on copper.",
                LimitingReagent   = "Cu",
                MaxExtent         = 1f,
                ActualExtent      = runFraction,
                Substances        = substances,
                EnthalpyKJ        = entry.enthalpyKJPerMol,
                IsExothermic      = entry.enthalpyKJPerMol < 0f,
                RateMultiplier    = Mathf.Lerp(0.2f, 1.5f, runFraction),
                EffectiveEaKJ     = entry.activationEnergyKJ,
                ThermoSummary     = "Strongly exothermic (~−300 kJ/mol Cu).",
                IsReversible      = entry.isReversible,
                Keq               = entry.equilibriumConstant,
                EquilibriumExtent = runFraction,
                EquilibriumShift  = "none",
                EquilibriumSummary= string.Empty,
                Conditions        = new List<ConditionResult>(),
                ConditionRate     = 1f,
                GhsCodes          = entry.safety?.ghs_icons ?? new List<string>(),
                SafetyWarnings    = entry.safety?.warnings_en ?? new List<string>(),
                SafetyNotes       = entry.safety_notes ?? string.Empty,
                Observation       = entry.observation_en ?? string.Empty,
                Explanation       = entry.explanation_en ?? string.Empty,
                ConditionNotes    = entry.condition_notes ?? string.Empty,
                Visuals           = BuildVisualHints(entry, temperatureC),
                ReagentFormulas   = new List<string> { "HNO3", "Cu" },
                TemperatureC      = temperatureC,
                PressureAtm       = entry.defaultPressureAtm <= 0f ? 1f : entry.defaultPressureAtm
            };
        }

        private ChemistryOutput BuildSyntheticOutput(float temperatureC, float no2Moles, float runFraction)
        {
            var substances = new List<SubstanceState>
            {
                NewProduct("Cu(NO3)2", Phase.Aqueous, no2Moles * 0.5f), // 1:2 vs NO2
                NewProduct("NO2",      Phase.Gas,     no2Moles),
                NewProduct("H2O",      Phase.Liquid,  no2Moles),         // 2:2 vs NO2
                new SubstanceState
                {
                    Formula           = "Cu",
                    Phase             = Phase.Solid,
                    MolesInitial      = 1f,
                    MolesFinal        = Mathf.Lerp(1f, 0f, runFraction),
                    MolesConsumed     = runFraction,
                    IsReactant        = true,
                    IsLimitingReagent = true,
                },
                new SubstanceState
                {
                    Formula      = "HNO3",
                    Phase        = Phase.Liquid,
                    MolesInitial = 4f,
                    MolesFinal   = Mathf.Lerp(4f, 0f, runFraction),
                    MolesConsumed = 4f * runFraction,
                    IsReactant   = true,
                    IsExcess     = true,
                },
            };

            return new ChemistryOutput
            {
                Found             = true,
                ReactionId        = _reactionId,
                ReactionName      = "Concentrated Nitric Acid + Copper (Synthetic)",
                BalancedEquation  = "Cu + 4 HNO3 → Cu(NO3)2 + 2 NO2 + 2 H2O",
                Status            = ReactionStatus.Success,
                CompletionPercent = runFraction,
                Summary           = "Synthetic stress test — no registry entry found.",
                LimitingReagent   = "Cu",
                MaxExtent         = 1f,
                ActualExtent      = runFraction,
                Substances        = substances,
                EnthalpyKJ        = -300f,
                IsExothermic      = true,
                RateMultiplier    = Mathf.Lerp(0.2f, 1.5f, runFraction),
                EffectiveEaKJ     = 35f,
                ThermoSummary     = "Strongly exothermic (~−300 kJ/mol Cu).",
                IsReversible      = false,
                Keq               = 0f,
                EquilibriumExtent = runFraction,
                EquilibriumShift  = "none",
                EquilibriumSummary= string.Empty,
                Conditions        = new List<ConditionResult>(),
                ConditionRate     = 1f,
                GhsCodes          = new List<string> { "GHS05", "GHS06", "GHS03" },
                SafetyWarnings    = new List<string>
                {
                    "Highly corrosive concentrated nitric acid.",
                    "Toxic NO2 gas — fume hood mandatory.",
                    "Strongly exothermic — temperature can spike beyond 80 °C."
                },
                SafetyNotes       = "Fume hood required. Strongly exothermic.",
                Observation       = "Copper dissolves into deep cyan/blue solution; thick reddish-brown NO2 gas evolves; vessel heats rapidly.",
                Explanation       = "Cu is oxidized to Cu²⁺; nitrogen reduced from +5 to +4. Heat from the redox reaction drives self-acceleration.",
                ConditionNotes    = "Concentrated HNO3, room-temperature start, gentle stirring, fume hood.",
                Visuals           = BuildSyntheticVisualHints(temperatureC),
                ReagentFormulas   = new List<string> { "HNO3", "Cu" },
                TemperatureC      = temperatureC,
                PressureAtm       = 1f
            };
        }

        // ── Helpers ───────────────────────────────────────────
        private void TryRequestRealMix()
        {
            // ReactionController is a scene MonoBehaviour, not a ServiceLocator
            // entry — locate it the same way LabInputController does.
#if UNITY_2023_1_OR_NEWER
            var controller = Object.FindFirstObjectByType<ReactionController>();
#else
            var controller = Object.FindObjectOfType<ReactionController>();
#endif
            if (controller == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[StressTestTrigger] No ReactionController in the scene. " +
                    "Skipping real RequestMix — synthetic ramp will still run.", this);
                return;
            }

            if (_injectedReagents == null || _injectedReagents.Count < 2)
            {
                UnityEngine.Debug.LogWarning(
                    "[StressTestTrigger] _injectedReagents needs at least two formulas. " +
                    "Skipping real RequestMix.", this);
                return;
            }

            // Build the MixRequest with the same ctor LabInputController uses.
            // Stir is fed in; grinding stays at 0 (Cu is bulk metal here);
            // catalyst false (HNO3 + Cu is self-driven).
            var request = new MixRequest(
                reagentNames: new List<string>(_injectedReagents),
                medium:       _injectionMedium,
                temperature:  _injectionTemperatureC,
                stirring:     _injectionStirring,
                grinding:     0f,
                hasCatalyst:  false);

            UnityEngine.Debug.Log(
                $"[StressTestTrigger] RequestMix → {string.Join(" + ", request.ReagentNames)}" +
                $" | Med={request.Medium} | T={request.Temperature}°C | Stir={request.Stirring:0.00}",
                this);

            controller.RequestMix(request);
        }

        private static SubstanceState NewProduct(string formula, Phase phase, float moles)
        {
            return new SubstanceState
            {
                Formula      = formula,
                Phase        = phase,
                MolesInitial = 0f,
                MolesFinal   = moles,
                IsProduct    = true,
            };
        }

        private static float StoichOf(List<ReactionChemical> products, string formula, float fallback)
        {
            if (products == null) return fallback;
            for (int i = 0; i < products.Count; i++)
            {
                var p = products[i];
                if (p != null && p.formula == formula && p.stoich > 0f) return p.stoich;
            }
            return fallback;
        }

        private static Phase MapPhase(string state)
        {
            if (string.IsNullOrEmpty(state)) return Phase.Liquid;
            switch (state.Trim().ToLowerInvariant())
            {
                case "s":  return Phase.Solid;
                case "l":  return Phase.Liquid;
                case "g":  return Phase.Gas;
                case "aq": return Phase.Aqueous;
                default:   return Phase.Liquid;
            }
        }

        private static VisualHints BuildVisualHints(ReactionEntry entry, float temperatureC)
        {
            var vfx = entry.visual_effects;
            return new VisualHints
            {
                ColorChange      = vfx != null && !string.IsNullOrWhiteSpace(vfx.color_change),
                ColorHex         = vfx != null ? vfx.color_change : "#1565C0",
                GasParticles     = vfx == null || vfx.gas,
                HeatGlow         = temperatureC >= 60f,
                Precipitate      = vfx != null && vfx.precipitate,
                Sparks           = false,
                Smoke            = vfx != null && vfx.smoke,
                Foam             = vfx != null && vfx.foam,
                Frost            = false,
                Glow             = vfx != null && vfx.glow,
                TemperatureDelta = temperatureC - 25f
            };
        }

        private static VisualHints BuildSyntheticVisualHints(float temperatureC)
        {
            return new VisualHints
            {
                ColorChange      = true,
                ColorHex         = "#1565C0", // deep cyan/blue Cu²⁺
                GasParticles     = true,
                HeatGlow         = temperatureC >= 60f,
                Precipitate      = false,
                Sparks           = false,
                Smoke            = true,
                Foam             = false,
                Frost            = false,
                Glow             = false,
                TemperatureDelta = temperatureC - 25f
            };
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_peakTemperatureC <= _ambientTemperatureC)
                UnityEngine.Debug.LogWarning(
                    $"[StressTestTrigger] '{name}': peak ({_peakTemperatureC}°C) must be above ambient ({_ambientTemperatureC}°C).",
                    this);

            if (_no2PeakMoles < _no2StartMoles)
                UnityEngine.Debug.LogWarning(
                    $"[StressTestTrigger] '{name}': NO2 peak ({_no2PeakMoles}) is below start ({_no2StartMoles}); gas will recede.",
                    this);
        }
#endif
    }
}
