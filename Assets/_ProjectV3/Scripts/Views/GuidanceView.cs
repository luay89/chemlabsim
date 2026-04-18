// ChemLabSim v3 — GuidanceView
// Displays context-sensitive hints. Pure display — no logic.
// UIController pushes GuidanceViewModel here.

using TMPro;
using UnityEngine;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Views
{
    public class GuidanceView : V3ViewBase
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI hintText;

        /// <summary>Called by UIController when guidance changes.</summary>
        public void Render(GuidanceViewModel vm)
        {
            if (vm.IsVisible)
            {
                Show();
                if (hintText != null)
                    hintText.text = vm.HintText ?? string.Empty;
            }
            else
            {
                Hide();
            }
        }

        /// <summary>Reset view.</summary>
        public void Clear()
        {
            if (hintText != null) hintText.text = string.Empty;
            Hide();
        }
    }
}
