// ChemLabSim v3 — QuizHintView
// Displays a thought-provoking question about the reaction outcome.
// Pure display — no logic. UIController pushes QuizHintViewModel here.

using TMPro;
using UnityEngine;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Views
{
    public class QuizHintView : V3ViewBase
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI questionText;

        public void Render(QuizHintViewModel vm)
        {
            if (!vm.IsVisible || string.IsNullOrEmpty(vm.QuestionText))
            {
                Hide();
                return;
            }

            Show();

            if (questionText != null)
                questionText.text = vm.QuestionText;
        }

        public void Clear()
        {
            if (questionText != null) questionText.text = string.Empty;
            Hide();
        }
    }
}
