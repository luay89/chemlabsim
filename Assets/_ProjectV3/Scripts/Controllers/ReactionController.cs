// ChemLabSim v3 — Reaction Controller
// Orchestrates the "Mix" action: accepts a MixRequest, evaluates via ReactionEngine,
// publishes legacy-compatible events, and starts live SimulationStepper playback.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ChemLabSimV3.Data;
using ChemLabSimV3.Engine;
using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Controllers
{
    public class ReactionController : V3ControllerBase
    {
        [Header("Live Simulation (optional — auto-created if null)")]
        [SerializeField] private SimulationStepper simulationStepper;

        private ReactionDB db;
        private ReactionEngine engine;

        /// <summary>
        /// The currently running <see cref="SimulationStepper"/>, if any.
        /// Consumed by <c>HeatingController</c> / <c>StirringController</c> to route
        /// per-frame inputs (heat, grinding factor) into the active simulation.
        /// Returns <c>null</c> when no simulation is currently running.
        /// </summary>
        public SimulationStepper ActiveStepper
        {
            get
            {
                if (_activeStepper != null && _activeStepper.IsRunning)
                    return _activeStepper;

                _activeStepper = FindRunningStepper();
                return _activeStepper;
            }
        }

        private SimulationStepper _activeStepper;

        private static SimulationStepper FindRunningStepper()
        {
#if UNITY_2023_1_OR_NEWER
            var all = Object.FindObjectsByType<SimulationStepper>(FindObjectsSortMode.None);
#else
            var all = Object.FindObjectsOfType<SimulationStepper>();
#endif
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].IsRunning) return all[i];
            return null;
        }

        protected override void OnInitialize()
        {
            db = AppManager.Instance != null ? AppManager.Instance.ReactionDatabase : null;

            if (db == null || db.reactions == null)
            {
                Debug.LogWarning("[ReactionController] ReactionDB is unavailable at init.");
                engine = new ReactionEngine((ReactionDB)null);
            }
            else
            {
                engine = new ReactionEngine(db);
                Debug.Log($"[ReactionController] Initialized with {db.reactions.Count} reactions.");
            }

            EnsureSimulationRuntime();
        }

        protected override void OnTeardown() { }

        // -- Public API ------------------------------------------

        /// <summary>
        /// Entry point for a mix attempt. Called by UIController (or tests).
        /// Pure data in, events out — no UI dependencies.
        /// </summary>
        public void RequestMix(MixRequest request)
        {
            if (!TryEnsureDatabase())
                return;

            if (!ValidateRequest(request, out string validationMsg))
            {
                EventBus.Publish(new ReactionNotFoundEvent { Message = validationMsg });
                return;
            }

            ReactionEngineResult detailed = engine.ProcessDetailed(request);

            if (!detailed.Found)
            {
                string msg = string.IsNullOrWhiteSpace(detailed.Output.Summary)
                    ? BuildNoMatchMessage(request.ReagentNames)
                    : detailed.Output.Summary;
                EventBus.Publish(new ReactionNotFoundEvent { Message = msg });
                return;
            }

            var input = ReactionEvaluationAdapter.ToLegacyInput(request, detailed.Reaction);
            ReactionEvaluationResult result = ReactionEvaluationAdapter.ToLegacyResult(detailed);

            Debug.Log($"[ReactionController] Evaluated '{detailed.Reaction.id}' → {result.Status} (Valid={result.IsValid})");

            EventBus.Publish(new ReactionEvaluatedEvent(input, result));

            if (ShouldStartLiveSimulation(result))
                StartLiveSimulation(detailed, request);
        }

        // -- Simulation wiring -----------------------------------

        private static bool ShouldStartLiveSimulation(ReactionEvaluationResult result)
        {
            if (!result.IsValid) return false;
            return result.Status == ReactionStatus.Success
                || result.Status == ReactionStatus.Partial;
        }

        private void StartLiveSimulation(ReactionEngineResult detailed, MixRequest request)
        {
            SimulationStepper stepper = EnsureSimulationRuntime();
            if (stepper == null)
            {
                Debug.LogWarning("[ReactionController] SimulationStepper unavailable — skipping live playback.");
                return;
            }

            if (stepper.IsRunning)
                stepper.Stop();

            stepper.StartSimulation(
                detailed.Reaction,
                request,
                detailed.Pipeline,
                engine.Registry);

            _activeStepper = stepper;
            Debug.Log($"[ReactionController] Live simulation started for '{detailed.Reaction.id}'.");
        }

        private SimulationStepper EnsureSimulationRuntime()
        {
            if (simulationStepper != null)
            {
                EnsureSimulationBridge(simulationStepper.gameObject);
                return simulationStepper;
            }

#if UNITY_2023_1_OR_NEWER
            simulationStepper = Object.FindFirstObjectByType<SimulationStepper>();
#else
            simulationStepper = Object.FindObjectOfType<SimulationStepper>();
#endif
            if (simulationStepper != null)
            {
                EnsureSimulationBridge(simulationStepper.gameObject);
                return simulationStepper;
            }

            var runtimeGo = new GameObject("SimulationRuntime");
            runtimeGo.transform.SetParent(transform, false);
            simulationStepper = runtimeGo.AddComponent<SimulationStepper>();
            EnsureSimulationBridge(runtimeGo);

            Debug.Log("[ReactionController] Created SimulationRuntime (SimulationStepper + SimulationBridge).");
            return simulationStepper;
        }

        private static void EnsureSimulationBridge(GameObject host)
        {
            if (host.GetComponent<SimulationBridge>() == null)
                host.AddComponent<SimulationBridge>();
        }

        // -- Internal helpers ------------------------------------

        private bool TryEnsureDatabase()
        {
            if (db != null && db.reactions != null && engine != null)
                return true;

            db = AppManager.Instance != null ? AppManager.Instance.ReactionDatabase : null;

            if (db == null || db.reactions == null)
            {
                Debug.LogError("[ReactionController] ReactionDB is still unavailable.");
                EventBus.Publish(new ReactionNotFoundEvent { Message = "Reaction database is unavailable." });
                return false;
            }

            engine = new ReactionEngine(db);
            return true;
        }

        private static bool ValidateRequest(MixRequest request, out string message)
        {
            message = string.Empty;

            if (request.ReagentNames == null || request.ReagentNames.Count < 2)
            {
                message = "Choose at least two different reactants.";
                return false;
            }

            if (request.ReagentNames.Distinct().Count() != request.ReagentNames.Count)
            {
                message = "Each selected reactant must be different.";
                return false;
            }

            return true;
        }

        private string BuildNoMatchMessage(List<string> reagentNames)
        {
            string display = string.Join(" + ", reagentNames.Where(x => !string.IsNullOrWhiteSpace(x)));

            if (engine?.Registry != null && engine.Registry.NeedsMoreReagents(reagentNames))
                return $"The selected set ({display}) looks incomplete. Some reactions need 3 or 4 reactants.";

            return $"No valid reaction matches the selected set ({display}).";
        }
    }
}
