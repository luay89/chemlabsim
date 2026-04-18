// ChemLabSim v3 — ReactionResultView
// Premium sectioned display for reaction outcomes.
// Shows: Status, Products, Observations, Explanation, Conditions.
// Color-coded by outcome. Pure display — no logic.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Views
{
    public class ReactionResultView : V3ViewBase
    {
        [Header("Status Section")]
        [SerializeField] private TextMeshProUGUI headlineText;
        [SerializeField] private TextMeshProUGUI reactionNameText;
        [SerializeField] private TextMeshProUGUI reactionTypeText;
        [SerializeField] private Image statusIcon;
        [SerializeField] private Image statusBanner;

        [Header("Equation Section")]
        [SerializeField] private TextMeshProUGUI equationText;

        [Header("Products Section")]
        [SerializeField] private TextMeshProUGUI productsText;
        [SerializeField] private TextMeshProUGUI reactantsText;

        [Header("Observations Section")]
        [SerializeField] private TextMeshProUGUI observationsText;

        [Header("Conditions Section")]
        [SerializeField] private TextMeshProUGUI conditionNotesText;

        [Header("Explanation Section")]
        [SerializeField] private TextMeshProUGUI explanationText;

        [Header("Rate Indicator")]
        [SerializeField] private Image rateBar;
        [SerializeField] private TextMeshProUGUI rateLabel;

        // -- Color palette for outcome feedback ------------
        private static readonly Color SuccessColor  = new Color(0.18f, 0.75f, 0.35f);  // Vibrant green
        private static readonly Color PartialColor  = new Color(0.95f, 0.65f, 0.15f);  // Amber
        private static readonly Color FailColor     = new Color(0.85f, 0.22f, 0.22f);  // Red
        private static readonly Color InvalidColor  = new Color(0.45f, 0.45f, 0.50f);  // Muted gray
        private static readonly Color NotFoundColor = new Color(0.55f, 0.45f, 0.65f);  // Soft purple

        private static readonly Color SuccessBg  = new Color(0.12f, 0.25f, 0.15f, 0.85f);
        private static readonly Color PartialBg  = new Color(0.28f, 0.22f, 0.08f, 0.85f);
        private static readonly Color FailBg     = new Color(0.28f, 0.10f, 0.10f, 0.85f);
        private static readonly Color InvalidBg  = new Color(0.18f, 0.18f, 0.20f, 0.85f);
        private static readonly Color NotFoundBg = new Color(0.20f, 0.16f, 0.24f, 0.85f);

        public void Render(ReactionResultViewModel vm)
        {
            Show();

            // Status section
            if (headlineText != null)
                headlineText.text = vm.Headline ?? string.Empty;

            if (reactionNameText != null)
                reactionNameText.text = vm.ReactionName ?? string.Empty;

            if (reactionTypeText != null)
            {
                reactionTypeText.text = !string.IsNullOrEmpty(vm.ReactionType)
                    ? V3Labels.Get("reactionTypeLabel") + " " + vm.ReactionType
                    : string.Empty;
            }

            Color statusColor = GetStatusColor(vm.StatusKey);
            if (statusIcon != null)
                statusIcon.color = statusColor;
            if (statusBanner != null)
                statusBanner.color = GetBannerColor(vm.StatusKey);

            // Equation
            if (equationText != null)
                equationText.text = vm.Equation ?? string.Empty;

            // Reactants & Products
            if (reactantsText != null)
            {
                reactantsText.text = vm.Reactants != null && vm.Reactants.Count > 0
                    ? string.Join(" + ", vm.Reactants)
                    : string.Empty;
            }

            if (productsText != null)
            {
                productsText.text = vm.Products != null && vm.Products.Count > 0
                    ? "\u2192 " + string.Join(" + ", vm.Products)
                    : string.Empty;
            }

            // Observations — hide section if empty
            if (observationsText != null)
            {
                string obs = vm.ObservationText ?? string.Empty;
                observationsText.text = obs;
                observationsText.gameObject.SetActive(!string.IsNullOrEmpty(obs));
            }

            // Conditions — hide section if empty
            if (conditionNotesText != null)
            {
                string cond = vm.ConditionNotes ?? string.Empty;
                conditionNotesText.text = cond;
                conditionNotesText.gameObject.SetActive(!string.IsNullOrEmpty(cond));
            }

            // Explanation — hide section if empty
            if (explanationText != null)
            {
                string expl = vm.Explanation ?? string.Empty;
                explanationText.text = expl;
                explanationText.gameObject.SetActive(!string.IsNullOrEmpty(expl));
            }

            // Rate bar
            if (rateBar != null)
            {
                rateBar.fillAmount = Mathf.Clamp01(vm.Rate01);
                rateBar.color = Color.Lerp(FailColor, SuccessColor, vm.Rate01);
            }

            if (rateLabel != null)
                rateLabel.text = vm.Rate01 > 0f ? $"Reaction Rate: {vm.Rate01:P0}" : string.Empty;
        }

        public void Clear()
        {
            if (headlineText != null) headlineText.text = string.Empty;
            if (reactionNameText != null) reactionNameText.text = string.Empty;
            if (reactionTypeText != null) reactionTypeText.text = string.Empty;
            if (equationText != null) equationText.text = string.Empty;
            if (reactantsText != null) reactantsText.text = string.Empty;
            if (productsText != null) productsText.text = string.Empty;
            if (observationsText != null) { observationsText.text = string.Empty; observationsText.gameObject.SetActive(true); }
            if (conditionNotesText != null) { conditionNotesText.text = string.Empty; conditionNotesText.gameObject.SetActive(true); }
            if (explanationText != null) { explanationText.text = string.Empty; explanationText.gameObject.SetActive(true); }
            if (rateBar != null) rateBar.fillAmount = 0f;
            if (rateLabel != null) rateLabel.text = string.Empty;
            if (statusIcon != null) statusIcon.color = InvalidColor;
            if (statusBanner != null) statusBanner.color = InvalidBg;
        }

        private static Color GetStatusColor(string statusKey)
        {
            switch (statusKey)
            {
                case "success":  return SuccessColor;
                case "partial":  return PartialColor;
                case "fail":     return FailColor;
                case "notFound": return NotFoundColor;
                default:         return InvalidColor;
            }
        }

        private static Color GetBannerColor(string statusKey)
        {
            switch (statusKey)
            {
                case "success":  return SuccessBg;
                case "partial":  return PartialBg;
                case "fail":     return FailBg;
                case "notFound": return NotFoundBg;
                default:         return InvalidBg;
            }
        }
    }
}
