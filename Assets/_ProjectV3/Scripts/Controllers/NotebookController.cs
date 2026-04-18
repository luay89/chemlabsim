// ChemLabSim v3 — Notebook Controller
// In-memory log of all experiments. Restores from SaveService on init,
// records new entries from ReactionEvaluatedEvent, publishes NotebookUpdatedEvent.

using System.Collections.Generic;
using UnityEngine;
using ChemLabSimV3.Core;
using ChemLabSimV3.Data;
using ChemLabSimV3.Events;
using ChemLabSimV3.Services;
using ChemLabSimV3.Views;

namespace ChemLabSimV3.Controllers
{
    public class NotebookController : V3ControllerBase
    {
        [Header("View")]
        [SerializeField] private NotebookView notebookView;

        private const int MaxEntries = 20;
        private readonly List<NotebookEntry> entries = new List<NotebookEntry>();
        private int entryCounter;

        public IReadOnlyList<NotebookEntry> Entries => entries;

        protected override void OnInitialize()
        {
            entries.Clear();
            entryCounter = 0;
            var save = ServiceLocator.Get<SaveService>();
            if (save != null && save.HasSave())
            {
                var nb = save.GetSaveData().notebook;
                foreach (var e in nb.entries)
                {
                    entries.Add(new NotebookEntry
                    {
                        Number = e.number,
                        ReagentSummary = e.reagentSummary,
                        OutcomeKey = e.outcomeKey,
                        MediumName = e.mediumName,
                        TemperatureC = e.temperatureC
                    });
                }
                entryCounter = nb.entryCounter;
            }
            EventBus.Subscribe<ReactionEvaluatedEvent>(OnReactionEvaluated);
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);

            if (notebookView != null) notebookView.Render(entries);
            Debug.Log($"[NotebookController] Initialized. Restored {entries.Count} entries.");
        }

        protected override void OnTeardown()
        {
            EventBus.Unsubscribe<ReactionEvaluatedEvent>(OnReactionEvaluated);
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        private void OnReactionEvaluated(ReactionEvaluatedEvent evt)
        {
            entryCounter++;

            string outcomeKey;
            if (!evt.Result.IsValid)
                outcomeKey = "invalid";
            else if (evt.Result.Status == ReactionStatus.Success)
                outcomeKey = "success";
            else if (evt.Result.Status == ReactionStatus.Partial)
                outcomeKey = "partial";
            else
                outcomeKey = "fail";

            string reagentSummary = evt.Input.reaction != null
                ? string.Join(" + ", evt.Input.reaction.GetReactantFormulas())
                : "-";

            var entry = new NotebookEntry
            {
                Number = entryCounter,
                ReagentSummary = reagentSummary,
                OutcomeKey = outcomeKey,
                MediumName = evt.Input.medium.ToString(),
                TemperatureC = evt.Input.temperatureC
            };

            entries.Add(entry);

            // Cap at MaxEntries (remove oldest)
            while (entries.Count > MaxEntries)
                entries.RemoveAt(0);

            if (notebookView != null) notebookView.Render(entries);

            EventBus.Publish(new NotebookUpdatedEvent
            {
                Entries = entries,
                EntryCounter = entryCounter
            });

            Debug.Log($"[NotebookController] Entry #{entry.Number}: {reagentSummary} → {outcomeKey}");
        }

        private void OnLanguageChanged(LanguageChangedEvent evt)
        {
            // Re-render with current entries to pick up localized labels
            if (notebookView != null) notebookView.Render(entries);
        }
    }
}
