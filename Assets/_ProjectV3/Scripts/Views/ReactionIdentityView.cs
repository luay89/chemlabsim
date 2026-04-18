// ChemLabSim v3 — ReactionIdentityView
// Displays reaction name, equation, required conditions.
// Pure display — no logic. UIController pushes ReactionIdentityViewModel here.

using TMPro;
using UnityEngine;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Views
{
    public class ReactionIdentityView : V3ViewBase
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI reactionNameText;
        [SerializeField] private TextMeshProUGUI equationText;
        [SerializeField] private TextMeshProUGUI conditionsText;

        public void Render(ReactionIdentityViewModel vm)
        {
            if (!vm.IsVisible)
            {
                Hide();
                return;
            }

            Show();

            if (reactionNameText != null)
                reactionNameText.text = vm.ReactionName ?? string.Empty;

            if (equationText != null)
                equationText.text = !string.IsNullOrEmpty(vm.Equation)
                    ? $"{V3Labels.Get("equation")} {vm.Equation}"
                    : string.Empty;

            if (conditionsText != null)
            {
                string medium = $"{V3Labels.Get("requiredMedium")} {vm.RequiredMedium}";
                string actTemp = $"{V3Labels.Get("activationTemp")} {vm.ActivationTempC:F0}°C";
                string cat = $"{V3Labels.Get("catalystAllowed")} {(vm.CatalystAllowed ? V3Labels.Get("allowed") : V3Labels.Get("notAllowed"))}";
                string gas = vm.ProducesGas ? $"  |  {V3Labels.Get("producesGas")} {V3Labels.Get("yes")}" : string.Empty;
                conditionsText.text = $"{medium}  |  {actTemp}  |  {cat}{gas}";
            }
        }

        public void Clear()
        {
            if (reactionNameText != null) reactionNameText.text = string.Empty;
            if (equationText != null) equationText.text = string.Empty;
            if (conditionsText != null) conditionsText.text = string.Empty;
        }
    }
}
