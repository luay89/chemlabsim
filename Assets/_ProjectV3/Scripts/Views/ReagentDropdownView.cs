// ChemLabSim v3 — ReagentDropdownView
// Thin wrapper around TMP_Dropdown for reagent selection.
// No logic — just forwards selection changes via OnValueChanged event.
// Reused for reagent A/B/C/D slots.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        // The reagent list holds ~90 entries. The scene template's ScrollRect used
        // Unity defaults (scroll sensitivity 1, no scrollbar), so the mouse wheel
        // moved the list ~1px per notch and most reagents (e.g. NaOH) were unreachable.
        private const float ListHeight        = 280f;
        private const float ScrollSensitivity = 30f;
        private const float ScrollbarWidth    = 10f;

        private void Awake()
        {
            if (dropdown != null)
            {
                dropdown.onValueChanged.AddListener(HandleDropdownChanged);
                ConfigureListScrolling();
            }
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

        /// <summary>Make the dropdown list scrollable: vertical only, usable wheel speed, visible scrollbar.</summary>
        private void ConfigureListScrolling()
        {
            RectTransform template = dropdown.template;
            if (template == null) return;

            var scroll = template.GetComponent<ScrollRect>();
            if (scroll == null) return;

            scroll.horizontal        = false;
            scroll.vertical          = true;
            scroll.movementType      = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = ScrollSensitivity;

            if (template.sizeDelta.y < ListHeight)
                template.sizeDelta = new Vector2(template.sizeDelta.x, ListHeight);

            if (scroll.verticalScrollbar == null)
            {
                scroll.verticalScrollbar = CreateVerticalScrollbar(template);
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
                scroll.verticalScrollbarSpacing = 0f;
            }
        }

        private static Scrollbar CreateVerticalScrollbar(RectTransform template)
        {
            var barGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            var barRect = (RectTransform)barGo.transform;
            barRect.SetParent(template, false);
            barRect.anchorMin = new Vector2(1, 0);
            barRect.anchorMax = new Vector2(1, 1);
            barRect.pivot = new Vector2(1, 1);
            barRect.sizeDelta = new Vector2(ScrollbarWidth, 0);
            barGo.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.14f);

            var areaGo = new GameObject("Sliding Area", typeof(RectTransform));
            var areaRect = (RectTransform)areaGo.transform;
            areaRect.SetParent(barRect, false);
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = Vector2.zero;
            areaRect.offsetMax = Vector2.zero;

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var handleRect = (RectTransform)handleGo.transform;
            handleRect.SetParent(areaRect, false);
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;
            var handleImg = handleGo.GetComponent<Image>();
            handleImg.color = new Color(0.45f, 0.6f, 0.85f);

            var bar = barGo.GetComponent<Scrollbar>();
            bar.handleRect = handleRect;
            bar.targetGraphic = handleImg;
            bar.direction = Scrollbar.Direction.BottomToTop;
            return bar;
        }

        private void HandleDropdownChanged(int index)
        {
            if (index < 0 || index >= formulaValues.Count) return;
            OnValueChanged?.Invoke(formulaValues[index]);
        }
    }
}
