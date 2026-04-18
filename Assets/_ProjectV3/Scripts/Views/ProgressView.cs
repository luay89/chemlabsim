// ChemLabSim v3 — ProgressView
// Displays session progress: score, level, experiments. Pure display — no logic.
// UIController pushes ProgressViewModel here.

using TMPro;
using UnityEngine;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Views
{
    public class ProgressView : V3ViewBase
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI scoreDeltaText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI lessonTitleText;
        [SerializeField] private TextMeshProUGUI experimentsText;
        [SerializeField] private TextMeshProUGUI levelUpText;

        /// <summary>Called by UIController when progress state changes.</summary>
        public void Render(ProgressViewModel vm)
        {
            Show();

            if (scoreText != null)
                scoreText.text = $"{V3Labels.Get("score")} {vm.Score}";

            if (scoreDeltaText != null)
            {
                if (vm.ScoreDelta != 0)
                {
                    scoreDeltaText.text = vm.ScoreDelta > 0 ? $"+{vm.ScoreDelta}" : $"{vm.ScoreDelta}";
                    scoreDeltaText.gameObject.SetActive(true);
                }
                else
                {
                    scoreDeltaText.gameObject.SetActive(false);
                }
            }

            if (levelText != null)
                levelText.text = $"{V3Labels.Get("level")} {vm.CurrentLevel}";

            if (lessonTitleText != null)
                lessonTitleText.text = vm.LessonTitle ?? string.Empty;

            if (experimentsText != null)
                experimentsText.text = $"{V3Labels.Get("experiments")} {vm.SuccessfulExperiments}/{vm.TotalExperiments}";

            if (levelUpText != null)
            {
                if (vm.JustLeveledUp)
                {
                    levelUpText.text = $"{V3Labels.Get("levelUp")} {vm.NewLevelTitle}";
                    levelUpText.gameObject.SetActive(true);
                }
                else
                {
                    levelUpText.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>Reset view to initial state.</summary>
        public void Clear()
        {
            if (scoreText != null) scoreText.text = $"{V3Labels.Get("score")} 0";
            if (scoreDeltaText != null) scoreDeltaText.gameObject.SetActive(false);
            if (levelText != null) levelText.text = $"{V3Labels.Get("level")} 1";
            if (lessonTitleText != null) lessonTitleText.text = string.Empty;
            if (experimentsText != null) experimentsText.text = $"{V3Labels.Get("experiments")} 0/0";
            if (levelUpText != null) levelUpText.gameObject.SetActive(false);
        }
    }
}
