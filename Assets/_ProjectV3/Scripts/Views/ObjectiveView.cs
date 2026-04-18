// ChemLabSim v3 — ObjectiveView
// Displays current per-level lesson objective status. Pure display — no logic.
// UIController pushes data here from ObjectiveAssignedEvent / ObjectiveCompletedEvent.

using TMPro;
using UnityEngine;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Views
{
    public class ObjectiveView : V3ViewBase
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI objectiveTitleText;
        [SerializeField] private TextMeshProUGUI objectiveStatusText;

        /// <summary>Show newly assigned objective.</summary>
        public void RenderAssigned(string title, int level)
        {
            Show();
            if (objectiveTitleText != null)
                objectiveTitleText.text = $"{V3Labels.Get("objective")} (Lv{level}): {title}";
            if (objectiveStatusText != null)
                objectiveStatusText.text = V3Labels.Get("inProgress");
        }

        /// <summary>Show objective completed.</summary>
        public void RenderCompleted()
        {
            Show();
            if (objectiveStatusText != null)
                objectiveStatusText.text = V3Labels.Get("completed");
        }

        /// <summary>Reset view.</summary>
        public void Clear()
        {
            if (objectiveTitleText != null) objectiveTitleText.text = string.Empty;
            if (objectiveStatusText != null) objectiveStatusText.text = string.Empty;
        }
    }
}
