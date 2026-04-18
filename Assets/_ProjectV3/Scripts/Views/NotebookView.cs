// ChemLabSim v3 — NotebookView
// Displays experiment history as a formatted text log.
// Pure display — no logic. NotebookController pushes data here.

using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Views
{
    public class NotebookView : V3ViewBase
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI entriesText;

        /// <summary>Render the full experiment log from newest to oldest.</summary>
        public void Render(IReadOnlyList<NotebookEntry> entries)
        {
            Show();

            if (entriesText == null) return;

            if (entries == null || entries.Count == 0)
            {
                entriesText.text = V3Labels.Get("noExperimentsYet");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"<b>{V3Labels.Get("recentExperiments")}</b>");

            // Newest first
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                string outcome = V3Labels.Get(e.OutcomeKey);
                string color = GetOutcomeColor(e.OutcomeKey);

                sb.Append($"<color=#888>{e.Number}) </color>");
                sb.Append(e.ReagentSummary);
                sb.Append($" <color=#888>|</color> {e.MediumName}");
                sb.Append($" <color=#888>|</color> {e.TemperatureC:F0}°C");
                sb.AppendLine($" <color=#888>-></color> <color={color}>{outcome}</color>");
            }

            entriesText.text = sb.ToString().TrimEnd();
        }

        /// <summary>Reset view.</summary>
        public void Clear()
        {
            if (entriesText != null) entriesText.text = string.Empty;
        }

        private static string GetOutcomeColor(string outcomeKey)
        {
            switch (outcomeKey)
            {
                case "success": return "#33CC33";
                case "partial": return "#FFB833";
                case "fail":    return "#CC3333";
                default:        return "#999999";
            }
        }
    }
}
