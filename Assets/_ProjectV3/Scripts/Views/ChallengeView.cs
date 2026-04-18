// ChemLabSim v3 — ChallengeView
// Displays current per-level challenge status. Pure display — no logic.
// UIController pushes data here from ChallengeAssignedEvent / ChallengeCompletedEvent.

using TMPro;
using UnityEngine;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Views
{
    public class ChallengeView : V3ViewBase
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI challengeTitleText;
        [SerializeField] private TextMeshProUGUI challengeStatusText;

        /// <summary>Show newly assigned challenge.</summary>
        public void RenderAssigned(string title, int level)
        {
            Show();
            if (challengeTitleText != null)
                challengeTitleText.text = $"{V3Labels.Get("challenge")} (Lv{level}): {title}";
            if (challengeStatusText != null)
                challengeStatusText.text = V3Labels.Get("inProgress");
        }

        /// <summary>Show challenge completed.</summary>
        public void RenderCompleted(int rewardPoints)
        {
            Show();
            if (challengeStatusText != null)
                challengeStatusText.text = $"{V3Labels.Get("completed")} +{rewardPoints}";
        }

        /// <summary>Reset view.</summary>
        public void Clear()
        {
            if (challengeTitleText != null) challengeTitleText.text = string.Empty;
            if (challengeStatusText != null) challengeStatusText.text = string.Empty;
        }
    }
}
