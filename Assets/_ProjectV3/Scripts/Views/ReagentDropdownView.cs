// ChemLabSim v3 — ReagentDropdownView
// Thin wrapper around TMP_Dropdown for reagent selection.
// No logic — just forwards selection changes via OnValueChanged event.
// Reused for reagent A/B/C/D slots.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ChemLabSimV3.Views
{
    public class ReagentDropdownView : V3ViewBase
    {
        [Header("UI")]
        [SerializeField] private TMP_Dropdown dropdown;
        [SerializeField] private TextMeshProUGUI label;

        /// <summary>Fires when the user selects a different reagent. Value is the reagent formula string.</summary>
        public event Action<string> OnValueChanged;

        private List<string> displayOptions = new List<string>();
        private List<string> formulaValues  = new List<string>();
        private bool hasEmptyOption;

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

        /// <summary>Populate this dropdown with reagent formulas (legacy — formulas as display text).</summary>
        public void SetOptions(List<string> reagents, bool allowEmpty)
        {
            SetOptions(reagents, reagents, allowEmpty);
        }

        /// <summary>Populate with display labels and underlying formula values.</summary>
        public void SetOptions(List<string> labels, List<string> formulas, bool allowEmpty)
        {
            displayOptions.Clear();
            formulaValues.Clear();
            hasEmptyOption = allowEmpty;

            if (allowEmpty)
            {
                displayOptions.Add("-");
                formulaValues.Add(string.Empty);
            }

            int count = Math.Min(labels != null ? labels.Count : 0,
                                 formulas != null ? formulas.Count : 0);
            for (int i = 0; i < count; i++)
            {
                displayOptions.Add(labels[i] ?? string.Empty);
                formulaValues.Add(formulas[i] ?? string.Empty);
            }

            if (dropdown != null)
            {
                dropdown.ClearOptions();
                dropdown.AddOptions(displayOptions);
                dropdown.value = 0;
                dropdown.RefreshShownValue();
            }
        }

        /// <summary>Set the label text (e.g., "Reagent A").</summary>
        public void SetLabel(string text)
        {
            if (label != null)
                label.text = text;
        }

        /// <summary>Get the currently selected reagent formula.</summary>
        public string GetSelectedValue()
        {
            if (dropdown == null || formulaValues.Count == 0)
                return string.Empty;

            int idx = dropdown.value;
            if (idx < 0 || idx >= formulaValues.Count)
                return string.Empty;

            return formulaValues[idx];
        }

        /// <summary>Try to preserve the current selection after options refresh.</summary>
        public void SelectFormula(string formula)
        {
            if (dropdown == null || string.IsNullOrEmpty(formula)) return;
            int idx = formulaValues.IndexOf(formula);
            if (idx >= 0)
            {
                dropdown.value = idx;
                dropdown.RefreshShownValue();
            }
        }

        private void HandleDropdownChanged(int index)
        {
            if (index < 0 || index >= formulaValues.Count) return;
            OnValueChanged?.Invoke(formulaValues[index]);
        }
    }
}
