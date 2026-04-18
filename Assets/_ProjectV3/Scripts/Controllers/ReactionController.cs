// ChemLabSim v3 — Reaction Controller
// Orchestrates the "Mix" action: accepts a MixRequest, finds a matching reaction,
// evaluates via ReactionEvaluator, and publishes results through EventBus.
//
// Migration source: LabController.OnMix(), TryBuildEvaluationInput(),
//   TryFindReactionBySelectedReagents(), BuildSortedReagentKey().
// Reuses v2: ReactionEvaluator (static, untouched), ReactionModels (untouched).

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ChemLabSimV3.Data;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Controllers
{
    public class ReactionController : V3ControllerBase
    {
        private ReactionDB db;

        protected override void OnInitialize()
        {
            db = AppManager.Instance != null ? AppManager.Instance.ReactionDatabase : null;

            if (db == null || db.reactions == null)
                Debug.LogWarning("[ReactionController] ReactionDB is unavailable at init.");
            else
                Debug.Log($"[ReactionController] Initialized with {db.reactions.Count} reactions.");
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

            if (!TryFindReaction(request.ReagentNames, out ReactionEntry reaction))
            {
                string msg = BuildNoMatchMessage(request.ReagentNames);
                EventBus.Publish(new ReactionNotFoundEvent { Message = msg });
                return;
            }

            var input = new ReactionEvaluationInput(
                reaction,
                request.Stirring,
                request.Grinding,
                request.Temperature,
                request.Medium,
                request.HasCatalyst
            );

            ReactionEvaluationResult result = ReactionEvaluator.Evaluate(input);

            Debug.Log($"[ReactionController] Evaluated '{reaction.id}' → {result.Status} (Valid={result.IsValid})");

            EventBus.Publish(new ReactionEvaluatedEvent(input, result));
        }

        // -- Internal helpers (ported from LabController) --------

        private bool TryEnsureDatabase()
        {
            if (db != null && db.reactions != null)
                return true;

            // Retry once — AppManager may have loaded after our init.
            db = AppManager.Instance != null ? AppManager.Instance.ReactionDatabase : null;

            if (db == null || db.reactions == null)
            {
                Debug.LogError("[ReactionController] ReactionDB is still unavailable.");
                EventBus.Publish(new ReactionNotFoundEvent { Message = "Reaction database is unavailable." });
                return false;
            }

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

        private bool TryFindReaction(List<string> reagentNames, out ReactionEntry reaction)
        {
            reaction = null;
            if (db == null || db.reactions == null || reagentNames == null || reagentNames.Count < 2)
                return false;

            string key = BuildSortedReagentKey(reagentNames);
            reaction = db.reactions.FirstOrDefault(
                r => r != null && BuildSortedReagentKey(r.GetReactantFormulas()) == key);
            return reaction != null;
        }

        private static string BuildSortedReagentKey(IEnumerable<string> reagents)
        {
            return string.Join("|", reagents
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .OrderBy(x => x));
        }

        private string BuildNoMatchMessage(List<string> reagentNames)
        {
            string display = string.Join(" + ", reagentNames.Where(x => !string.IsNullOrWhiteSpace(x)));

            bool needsMore = db.reactions.Any(r =>
                r != null &&
                r.GetReactantFormulas().Count > reagentNames.Count &&
                reagentNames.All(sel => r.GetReactantFormulas().Contains(sel)));

            if (needsMore)
                return $"The selected set ({display}) looks incomplete. Some reactions need 3 or 4 reactants.";

            return $"No valid reaction matches the selected set ({display}).";
        }
    }
}
