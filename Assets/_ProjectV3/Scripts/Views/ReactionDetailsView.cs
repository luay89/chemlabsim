// ChemLabSim v3 — ReactionDetailsView
// Displays factor indicators: medium, temperature, contact, catalyst, rate.
// Pure display — no logic. UIController pushes ReactionDetailsViewModel here.

using TMPro;
using UnityEngine;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Views
{
    public class ReactionDetailsView : V3ViewBase
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI mediumStatusText;
        [SerializeField] private TextMeshProUGUI temperatureStatusText;
        [SerializeField] private TextMeshProUGUI contactStatusText;
        [SerializeField] private TextMeshProUGUI catalystStatusText;
        [SerializeField] private TextMeshProUGUI rateText;

        public void Render(ReactionDetailsViewModel vm)
        {
            if (!vm.IsVisible)
            {
                Hide();
                return;
            }

            Show();

            if (mediumStatusText != null)
                mediumStatusText.text = $"{V3Labels.Get("medium")} {vm.MediumStatus}";

            if (temperatureStatusText != null)
                temperatureStatusText.text = $"{V3Labels.Get("temperature")} {vm.TemperatureStatus}  ({vm.TemperatureC:F0}°C / {vm.ActivationThresholdC:F0}°C)";

            if (contactStatusText != null)
                contactStatusText.text = $"{V3Labels.Get("contact")} {vm.ContactStatus}  ({vm.ContactFactor:F2})";

            if (catalystStatusText != null)
                catalystStatusText.text = $"{V3Labels.Get("catalyst")} {vm.CatalystStatus}";

            if (rateText != null)
                rateText.text = $"{V3Labels.Get("rate")} {vm.Rate01:P0}";
        }

        public void Clear()
        {
            if (mediumStatusText != null) mediumStatusText.text = string.Empty;
            if (temperatureStatusText != null) temperatureStatusText.text = string.Empty;
            if (contactStatusText != null) contactStatusText.text = string.Empty;
            if (catalystStatusText != null) catalystStatusText.text = string.Empty;
            if (rateText != null) rateText.text = string.Empty;
        }
    }
}
