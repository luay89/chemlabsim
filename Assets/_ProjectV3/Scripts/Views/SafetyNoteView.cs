// ChemLabSim v3 — SafetyNoteView
// Displays GHS codes and bilingual safety warnings for the reaction.
// Pure display — no logic. UIController pushes SafetyNoteViewModel here.

using TMPro;
using UnityEngine;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Views
{
    public class SafetyNoteView : V3ViewBase
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI ghsCodesText;
        [SerializeField] private TextMeshProUGUI warningsText;
        [SerializeField] private TextMeshProUGUI safetyNotesText;

        public void Render(SafetyNoteViewModel vm)
        {
            if (!vm.IsVisible)
            {
                Hide();
                return;
            }

            Show();

            if (ghsCodesText != null)
                ghsCodesText.text = !string.IsNullOrEmpty(vm.GhsCodes) ? vm.GhsCodes : string.Empty;

            if (warningsText != null)
                warningsText.text = !string.IsNullOrEmpty(vm.WarningsText) ? vm.WarningsText : V3Labels.Get("noSafetyData");

            if (safetyNotesText != null)
                safetyNotesText.text = !string.IsNullOrEmpty(vm.SafetyNotes) ? vm.SafetyNotes : string.Empty;
        }

        public void Clear()
        {
            if (ghsCodesText != null) ghsCodesText.text = string.Empty;
            if (warningsText != null) warningsText.text = string.Empty;
            if (safetyNotesText != null) safetyNotesText.text = string.Empty;
            Hide();
        }
    }
}
