// ChemLabSim v3 — StirringSliderView
// Thin wrapper around Unity Slider for stirring intensity (0–1).
// No logic — just forwards value changes via OnValueChanged event.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemLabSimV3.Views
{
    public class StirringSliderView : V3ViewBase
    {
        [Header("UI")]
        [SerializeField] private Slider slider;
        [SerializeField] private TextMeshProUGUI valueLabel;

        /// <summary>Fires when the user changes stirring intensity. Value is 0–1.</summary>
        public event Action<float> OnValueChanged;

        private void Awake()
        {
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.wholeNumbers = false;
                slider.onValueChanged.AddListener(HandleSliderChanged);
            }
        }

        private void OnDestroy()
        {
            if (slider != null)
                slider.onValueChanged.RemoveListener(HandleSliderChanged);
        }

        /// <summary>Set the slider to a specific value programmatically.</summary>
        public void SetValue(float stirring)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(stirring);
            UpdateLabel(stirring);
        }

        /// <summary>Get current stirring value.</summary>
        public float GetValue()
        {
            return slider != null ? slider.value : 0f;
        }

        private void HandleSliderChanged(float value)
        {
            UpdateLabel(value);
            OnValueChanged?.Invoke(value);
        }

        private void UpdateLabel(float value)
        {
            if (valueLabel != null)
                valueLabel.text = $"Stirring: {value:P0}";
        }
    }
}
