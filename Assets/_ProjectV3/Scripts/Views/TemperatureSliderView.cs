// ChemLabSim v3 — TemperatureSliderView
// Thin wrapper around Unity Slider for temperature input (0–100°C).
// No logic — just forwards value changes via OnValueChanged event.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemLabSimV3.Views
{
    public class TemperatureSliderView : V3ViewBase
    {
        [Header("UI")]
        [SerializeField] private Slider slider;
        [SerializeField] private TextMeshProUGUI valueLabel;

        /// <summary>Fires when the user changes the temperature. Value is in °C.</summary>
        public event Action<float> OnValueChanged;

        private void Awake()
        {
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 100f;
                slider.wholeNumbers = true;
                slider.onValueChanged.AddListener(HandleSliderChanged);
            }
        }

        private void OnDestroy()
        {
            if (slider != null)
                slider.onValueChanged.RemoveListener(HandleSliderChanged);
        }

        /// <summary>Set the slider to a specific value programmatically.</summary>
        public void SetValue(float temperature)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(temperature);
            UpdateLabel(temperature);
        }

        /// <summary>Get current temperature value.</summary>
        public float GetValue()
        {
            return slider != null ? slider.value : 25f;
        }

        private void HandleSliderChanged(float value)
        {
            UpdateLabel(value);
            OnValueChanged?.Invoke(value);
        }

        private void UpdateLabel(float value)
        {
            if (valueLabel != null)
                valueLabel.text = $"{value:0}°C";
        }
    }
}
