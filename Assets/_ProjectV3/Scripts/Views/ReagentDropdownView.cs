// ChemLabSim v3 — ReagentDropdownView
// Thin wrapper around TMP_Dropdown for reagent selection, with a search box
// that filters the list by name or formula.
// No chemistry logic — just forwards selection changes via OnValueChanged event.
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

        // Every selectable entry (incl. the optional "-" row) and the subset currently
        // listed in the dropdown after the search filter is applied.
        private readonly List<string> allLabels      = new List<string>();
        private readonly List<string> allFormulas    = new List<string>();
        private readonly List<string> displayOptions = new List<string>();
        private readonly List<string> formulaValues  = new List<string>();
        private string selectedFormula = string.Empty;
        private TMP_InputField searchField;

        // The reagent list holds ~90 entries sorted by English name, so NaOH sits far
        // down under "Sodium Hydroxide". A fast wheel, a visible scrollbar and a search
        // box (name or formula) keep every reagent reachable.
        private const float ListHeight        = 280f;
        private const float ScrollSensitivity = 60f;
        private const float ScrollbarWidth    = 10f;

        private static readonly Color SearchTextColor    = Color.white;
        private static readonly Color SearchNoMatchColor = new Color(1f, 0.45f, 0.45f);

        private void Awake()
        {
            if (dropdown != null)
            {
                dropdown.onValueChanged.AddListener(HandleDropdownChanged);
                ConfigureListScrolling();
            }

            if (label != null)
            {
                searchField = CreateSearchField(label);
                searchField.onValueChanged.AddListener(HandleSearchChanged);
            }
        }

        private void OnDestroy()
        {
            if (dropdown != null)
                dropdown.onValueChanged.RemoveListener(HandleDropdownChanged);
            if (searchField != null)
                searchField.onValueChanged.RemoveListener(HandleSearchChanged);
        }

        /// <summary>Populate this dropdown with reagent formulas (legacy — formulas as display text).</summary>
        public void SetOptions(List<string> reagents, bool allowEmpty)
        {
            SetOptions(reagents, reagents, allowEmpty);
        }

        /// <summary>Populate with display labels and underlying formula values.</summary>
        public void SetOptions(List<string> labels, List<string> formulas, bool allowEmpty)
        {
            allLabels.Clear();
            allFormulas.Clear();

            if (allowEmpty)
            {
                allLabels.Add("-");
                allFormulas.Add(string.Empty);
            }

            int count = Math.Min(labels != null ? labels.Count : 0,
                                 formulas != null ? formulas.Count : 0);
            for (int i = 0; i < count; i++)
            {
                allLabels.Add(labels[i] ?? string.Empty);
                allFormulas.Add(formulas[i] ?? string.Empty);
            }

            if (searchField != null)
            {
                searchField.SetTextWithoutNotify(string.Empty);
                searchField.textComponent.color = SearchTextColor;
            }

            ShowEntries(null);
            selectedFormula = formulaValues.Count > 0 ? formulaValues[0] : string.Empty;

            if (dropdown != null)
            {
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
            return selectedFormula;
        }

        /// <summary>Try to preserve the current selection after options refresh.</summary>
        public void SelectFormula(string formula)
        {
            if (dropdown == null || string.IsNullOrEmpty(formula)) return;
            if (!allFormulas.Contains(formula)) return;

            // The formula may be hidden by an active search filter — drop the filter first.
            if (!formulaValues.Contains(formula))
            {
                if (searchField != null) searchField.SetTextWithoutNotify(string.Empty);
                ShowEntries(null);
            }

            selectedFormula = formula;
            dropdown.value = formulaValues.IndexOf(formula);
            dropdown.RefreshShownValue();
        }

        // -- Search filter -------------------------------------

        private void HandleSearchChanged(string text)
        {
            string query = text != null ? text.Trim() : string.Empty;
            bool hasQuery = query.Length > 0;
            bool found = ShowEntries(hasQuery ? query : null);

            if (searchField != null)
                searchField.textComponent.color = found ? SearchTextColor : SearchNoMatchColor;
            if (dropdown == null) return;

            int idx = formulaValues.IndexOf(selectedFormula);
            if (hasQuery && found)
                idx = 0;   // best match becomes the selection as the user types
            if (idx < 0) idx = 0;

            dropdown.SetValueWithoutNotify(idx);
            dropdown.RefreshShownValue();

            if (idx < formulaValues.Count && formulaValues[idx] != selectedFormula)
            {
                selectedFormula = formulaValues[idx];
                OnValueChanged?.Invoke(selectedFormula);
            }
        }

        /// <summary>
        /// Lists the entries matching <paramref name="query"/> (name or formula, case-insensitive),
        /// best match first. A null query, or a query without matches, lists everything.
        /// Returns false only when a query had no matches.
        /// </summary>
        private bool ShowEntries(string query)
        {
            var matches = new List<int>();
            if (query != null)
            {
                for (int rank = 0; rank < 3; rank++)
                {
                    for (int i = 0; i < allFormulas.Count; i++)
                    {
                        if (allFormulas[i].Length > 0 && MatchRank(allLabels[i], allFormulas[i], query) == rank)
                            matches.Add(i);
                    }
                }
            }

            bool found = matches.Count > 0;
            if (!found)
            {
                for (int i = 0; i < allFormulas.Count; i++)
                    matches.Add(i);
            }

            displayOptions.Clear();
            formulaValues.Clear();
            foreach (int i in matches)
            {
                displayOptions.Add(allLabels[i]);
                formulaValues.Add(allFormulas[i]);
            }

            if (dropdown != null)
            {
                dropdown.Hide();
                dropdown.ClearOptions();
                dropdown.AddOptions(displayOptions);
            }

            return found || query == null;
        }

        /// <summary>0 = formula equals the query, 1 = formula or name starts with it, 2 = contains it, -1 = no match.</summary>
        private static int MatchRank(string label, string formula, string query)
        {
            const StringComparison cmp = StringComparison.OrdinalIgnoreCase;
            if (string.Equals(formula, query, cmp)) return 0;
            if (formula.StartsWith(query, cmp) || label.StartsWith(query, cmp)) return 1;
            if (label.IndexOf(query, cmp) >= 0 || formula.IndexOf(query, cmp) >= 0) return 2;
            return -1;
        }

        /// <summary>Builds a compact search box on the right side of the row label.</summary>
        private static TMP_InputField CreateSearchField(TextMeshProUGUI rowLabel)
        {
            var go = new GameObject("SearchField", typeof(RectTransform), typeof(Image));
            go.SetActive(false);   // configure before TMP_InputField.OnEnable runs
            var rect = (RectTransform)go.transform;
            rect.SetParent(rowLabel.transform, false);
            rect.anchorMin = new Vector2(0.42f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(0f, -2f);
            rect.offsetMax = new Vector2(0f, 2f);
            go.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.1f);

            var areaGo = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            var areaRect = (RectTransform)areaGo.transform;
            areaRect.SetParent(rect, false);
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(6f, 0f);
            areaRect.offsetMax = new Vector2(-6f, 0f);

            var placeholder = CreateSearchText(areaRect, "Placeholder", rowLabel.font);
            placeholder.text = "Search name or formula";
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(0.55f, 0.6f, 0.7f);

            var text = CreateSearchText(areaRect, "Text", rowLabel.font);
            text.color = SearchTextColor;

            var field = go.AddComponent<TMP_InputField>();
            field.textViewport = areaRect;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 40;
            field.targetGraphic = go.GetComponent<Image>();

            go.SetActive(true);
            return field;
        }

        private static TextMeshProUGUI CreateSearchText(RectTransform parent, string name, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.fontSize = 12;
            tmp.enableWordWrapping = false;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            return tmp;
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
            selectedFormula = formulaValues[index];
            OnValueChanged?.Invoke(selectedFormula);
        }
    }
}
