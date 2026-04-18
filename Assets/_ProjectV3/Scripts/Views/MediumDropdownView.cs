// ChemLabSim v3 — MediumDropdownView
// Thin wrapper around TMP_Dropdown for reaction medium selection (Neutral/Acidic/Basic).
// No logic — just forwards selection changes via OnValueChanged event.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ChemLabSimV3.Views
{
    public class MediumDropdownView : V3ViewBase
    {
        [Header("UI")]
        [SerializeField] private TMP_Dropdown dropdown;
        [SerializeField] private TextMeshProUGUI label;

        /// <summary>Fires when the user selects a different medium. Value is the dropdown index (0=Neutral, 1=Acidic, 2=Basic).</summary>
        public event Action<int> OnValueChanged;

        private void Awake()
        {
            if (dropdown != null)
                dropdown.onValueChanged.AddListener(HandleDropdownChanged);
        }

        private void OnDestroy()
        {
            if (dropdown != null)
                dropdown.onValueChanged.RemoveListener(HandleDropdownChanged);
        }

        /// <summary>Populate with medium options.</summary>
        public void SetOptions(List<string> mediums)
        {
            if (dropdown != null)
            {
                dropdown.ClearOptions();
                dropdown.AddOptions(mediums);
                dropdown.value = 0;
                dropdown.RefreshShownValue();
            }
        }

        /// <summary>Get currently selected medium index.</summary>
        public int GetSelectedIndex()
        {
            return dropdown != null ? dropdown.value : 0;
        }

        private void HandleDropdownChanged(int index)
        {
            OnValueChanged?.Invoke(index);
        }
    }
}
