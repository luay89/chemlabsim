// ChemLabSim v3 — ScientificExplanationView
// Displays a dynamically generated causal explanation of the reaction outcome.
// Pure display — no logic. UIController pushes ScientificExplanationViewModel here.

using TMPro;
using UnityEngine;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Views
{
    public class ScientificExplanationView : V3ViewBase
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI explanationText;

        public void Render(ScientificExplanationViewModel vm)
        {
            if (!vm.IsVisible)
            {
                Hide();
                return;
            }

            Show();

            if (explanationText != null)
                explanationText.text = vm.ExplanationText ?? string.Empty;
        }

        public void Clear()
        {
            if (explanationText != null) explanationText.text = string.Empty;
            Hide();
        }
    }
}
