// ChemLabSim v3 — CatalystToggleView
// Thin wrapper around Unity Toggle for catalyst on/off.
// No logic — just forwards value changes via OnValueChanged event.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemLabSimV3.Views
{
    public class CatalystToggleView : V3ViewBase
    {
        [Header("UI")]
        [SerializeField] private Toggle toggle;
        [SerializeField] private TextMeshProUGUI label;

        /// <summary>Fires when the user toggles catalyst. True = applied.</summary>
        public event Action<bool> OnValueChanged;

        private void Awake()
        {
            if (toggle != null)
                toggle.onValueChanged.AddListener(HandleToggleChanged);
        }

        private void OnDestroy()
        {
            if (toggle != null)
                toggle.onValueChanged.RemoveListener(HandleToggleChanged);
        }

        /// <summary>Set the toggle programmatically.</summary>
        public void SetValue(bool hasCatalyst)
        {
            if (toggle != null)
                toggle.SetIsOnWithoutNotify(hasCatalyst);
        }

        /// <summary>Get current toggle value.</summary>
        public bool GetValue()
        {
            return toggle != null && toggle.isOn;
        }

        private void HandleToggleChanged(bool value)
        {
            OnValueChanged?.Invoke(value);
        }
    }
}
