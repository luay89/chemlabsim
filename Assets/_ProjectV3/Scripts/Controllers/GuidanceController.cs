// ChemLabSim v3 — Guidance Controller
// Produces semantic GuidanceState based on current lab inputs.
// Publishes GuidanceUpdatedEvent → UIController converts to GuidanceViewModel for the View.
// No text, no TMP, no localization inside this controller.
//
// Migration source: LabController.BuildGuidanceMessage() (logic only, not formatting).

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ChemLabSimV3.Data;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Controllers
{
    public class GuidanceController : V3ControllerBase
    {
        private LabInputController labInput;
        private bool guidanceDismissed;

        public GuidanceState CurrentState { get; private set; }

        protected override void OnInitialize()
        {
            labInput = FindObjectOfType<LabInputController>();

            EventBus.Subscribe<InputChangedEvent>(OnInputChanged);
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
            EventBus.Subscribe<ReactionEvaluatedEvent>(OnReactionEvaluated);
            EventBus.Subscribe<ReactionNotFoundEvent>(OnReactionNotFound);

            // Publish initial state
            PublishState(new GuidanceState
            {
                Step = GuidanceStep.SelectReagents,
                SelectedReagents = new List<string>(),
                IsVisible = true
            });

            Debug.Log("[GuidanceController] Initialized.");
        }

        protected override void OnTeardown()
        {
            EventBus.Unsubscribe<InputChangedEvent>(OnInputChanged);
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
            EventBus.Unsubscribe<ReactionEvaluatedEvent>(OnReactionEvaluated);
            EventBus.Unsubscribe<ReactionNotFoundEvent>(OnReactionNotFound);
        }

        // -- Event Handlers ------------------------------------

        private void OnInputChanged(InputChangedEvent evt)
        {
            guidanceDismissed = false;
            RefreshGuidance();
        }

        private void OnLanguageChanged(LanguageChangedEvent evt)
        {
            // Re-publish current state so the UI can re-render with new language
            if (!guidanceDismissed)
                PublishState(CurrentState);
        }

        private void OnReactionEvaluated(ReactionEvaluatedEvent evt)
        {
            guidanceDismissed = true;
            PublishState(new GuidanceState
            {
                Step = GuidanceStep.Dismissed,
                SelectedReagents = new List<string>(),
                IsVisible = false
            });
        }

        private void OnReactionNotFound(ReactionNotFoundEvent evt)
        {
            guidanceDismissed = true;
            PublishState(new GuidanceState
            {
                Step = GuidanceStep.Dismissed,
                SelectedReagents = new List<string>(),
                IsVisible = false
            });
        }

        // -- State Builder -------------------------------------

        private void RefreshGuidance()
        {
            if (labInput == null)
                labInput = FindObjectOfType<LabInputController>();

            if (labInput == null)
            {
                PublishState(new GuidanceState
                {
                    Step = GuidanceStep.SelectReagents,
                    SelectedReagents = new List<string>(),
                    IsVisible = true
                });
                return;
            }

            PublishState(BuildState(labInput.CurrentInput));
        }

        private GuidanceState BuildState(LabInputViewModel input)
        {
            var reagents = new List<string>();
            if (!string.IsNullOrWhiteSpace(input.ReagentA)) reagents.Add(input.ReagentA.Trim());
            if (!string.IsNullOrWhiteSpace(input.ReagentB)) reagents.Add(input.ReagentB.Trim());
            if (!string.IsNullOrWhiteSpace(input.ReagentC)) reagents.Add(input.ReagentC.Trim());
            if (!string.IsNullOrWhiteSpace(input.ReagentD)) reagents.Add(input.ReagentD.Trim());

            if (reagents.Count < 2)
                return new GuidanceState
                {
                    Step = GuidanceStep.SelectReagents,
                    SelectedReagents = reagents,
                    IsVisible = true
                };

            if (reagents.Distinct().Count() != reagents.Count)
                return new GuidanceState
                {
                    Step = GuidanceStep.DuplicateReagents,
                    SelectedReagents = reagents,
                    IsVisible = true
                };

            // Check if any DB reaction needs more reactants than currently selected
            bool mayNeedMore = false;
            var db = AppManager.Instance != null ? AppManager.Instance.ReactionDatabase : null;
            if (db != null && db.reactions != null)
            {
                mayNeedMore = db.reactions.Any(r =>
                    r != null &&
                    r.GetReactantFormulas().Count > reagents.Count &&
                    reagents.All(sel => r.GetReactantFormulas().Contains(sel)));
            }

            return new GuidanceState
            {
                Step = GuidanceStep.Ready,
                SelectedReagents = reagents,
                MediumIndex = input.MediumIndex,
                Temperature = input.Temperature,
                Stirring = input.Stirring,
                Grinding = input.Grinding,
                HasCatalyst = input.HasCatalyst,
                MayNeedExtraReactant = mayNeedMore,
                IsVisible = true
            };
        }

        // -- Publish -------------------------------------------

        private void PublishState(GuidanceState state)
        {
            CurrentState = state;
            EventBus.Publish(new GuidanceUpdatedEvent { State = state });
        }
    }
}
