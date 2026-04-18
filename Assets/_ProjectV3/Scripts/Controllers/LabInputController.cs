// ChemLabSim v3 — Lab Input Controller
// Collects values from input views, builds MixRequest, sends to ReactionController.
// No score/progress/challenge logic. No TMP formatting. No PlayerPrefs.
//
// Flow: Input Views → LabInputController.UpdateField() → OnMix() → ReactionController.RequestMix()
// Reagent options are populated from ReactionDB at init.
// Display names come from materials.json via MaterialDB.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ChemLabSimV3.Core;
using ChemLabSimV3.Data;
using ChemLabSimV3.Events;
using ChemLabSimV3.Services;
using ChemLabSimV3.Views;

namespace ChemLabSimV3.Controllers
{
    public class LabInputController : V3ControllerBase
    {
        // -- View References (wired in Inspector or scene setup) ---
        [Header("Reagent Dropdowns")]
        [SerializeField] private ReagentDropdownView reagentADropdown;
        [SerializeField] private ReagentDropdownView reagentBDropdown;
        [SerializeField] private ReagentDropdownView reagentCDropdown;
        [SerializeField] private ReagentDropdownView reagentDDropdown;

        [Header("Condition Controls")]
        [SerializeField] private MediumDropdownView mediumDropdown;
        [SerializeField] private TemperatureSliderView temperatureSlider;
        [SerializeField] private StirringSliderView stirringSlider;
        [SerializeField] private GrindingSliderView grindingSlider;
        [SerializeField] private CatalystToggleView catalystToggle;

        [Header("Actions")]
        [SerializeField] private MixButtonView mixButton;

        [Header("Controller")]
        [SerializeField] private ReactionController reactionController;

        [Header("Materials Database")]
        [SerializeField] private TextAsset materialsJsonAsset;  // Drag materials.json here in Inspector

        // -- Internal State ------------------------------------
        private LabInputViewModel inputState;
        private List<string> availableReagents = new List<string>();
        private Dictionary<string, ChemicalMaterial> materialLookup = new Dictionary<string, ChemicalMaterial>();
        private int currentLanguageIndex;

        // -- Read-only -----------------------------------------
        public LabInputViewModel CurrentInput => inputState;
        public IReadOnlyList<string> AvailableReagents => availableReagents;

        // -- Lifecycle -----------------------------------------

        protected override void OnInitialize()
        {
            if (reactionController == null)
                reactionController = FindObjectOfType<ReactionController>();

            LoadMaterialLookup();
            var langService = ServiceLocator.Get<LanguageService>();
            currentLanguageIndex = langService != null ? (int)langService.CurrentLanguage : 0;

            PopulateReagentOptions();
            SetDefaults();
            BindViews();

            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);

            Debug.Log($"[LabInputController] Initialized with {availableReagents.Count} reagents.");
        }

        protected override void OnTeardown()
        {
            UnbindViews();
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        // -- Material Lookup -----------------------------------

        private void LoadMaterialLookup()
        {
            materialLookup.Clear();

            if (materialsJsonAsset != null)
            {
                try
                {
                    var db = JsonUtility.FromJson<MaterialDB>(materialsJsonAsset.text);
                    if (db?.materials != null)
                    {
                        foreach (var mat in db.materials)
                        {
                            if (!string.IsNullOrEmpty(mat.formula) && !materialLookup.ContainsKey(mat.formula))
                                materialLookup[mat.formula] = mat;
                        }
                        Debug.Log($"[LabInputController] Loaded {materialLookup.Count} materials from JSON.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LabInputController] Failed to parse materials JSON: {ex.Message}");
                }
            }
        }

        private string GetDisplayLabel(string formula)
        {
            if (materialLookup.TryGetValue(formula, out var mat))
            {
                string name = mat.GetDisplayName(0); // English only
                if (!string.IsNullOrEmpty(name) && name != formula)
                    return $"{name}  ({formula})";
            }
            return formula;
        }

        // -- Populate from ReactionDB --------------------------

        private void PopulateReagentOptions()
        {
            var db = AppManager.Instance != null ? AppManager.Instance.ReactionDatabase : null;
            if (db == null || db.reactions == null)
            {
                Debug.LogWarning("[LabInputController] ReactionDB unavailable — reagent dropdowns will be empty.");
                return;
            }

            availableReagents = db.reactions
                .SelectMany(r => r != null ? r.GetReactantFormulas() : new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            // Sort by display name in current language
            availableReagents.Sort((a, b) =>
                string.Compare(GetDisplayLabel(a), GetDisplayLabel(b), StringComparison.CurrentCultureIgnoreCase));

            // Build parallel label list
            var labels = availableReagents.Select(f => GetDisplayLabel(f)).ToList();

            // Populate dropdowns with labels + formulas
            if (reagentADropdown != null) reagentADropdown.SetOptions(labels, availableReagents, allowEmpty: false);
            if (reagentBDropdown != null) reagentBDropdown.SetOptions(labels, availableReagents, allowEmpty: false);
            if (reagentCDropdown != null) reagentCDropdown.SetOptions(labels, availableReagents, allowEmpty: true);
            if (reagentDDropdown != null) reagentDDropdown.SetOptions(labels, availableReagents, allowEmpty: true);

            // Medium always has 3 fixed options
            if (mediumDropdown != null)
                mediumDropdown.SetOptions(new List<string> { "Neutral", "Acidic", "Basic" });
        }

        // -- Language Change Handler ---------------------------

        private void OnLanguageChanged(LanguageChangedEvent evt)
        {
            currentLanguageIndex = evt.LanguageIndex;
            RefreshDropdownLabels();
        }

        private void RefreshDropdownLabels()
        {
            if (availableReagents.Count == 0) return;

            // Capture current selections
            string selA = reagentADropdown != null ? reagentADropdown.GetSelectedValue() : string.Empty;
            string selB = reagentBDropdown != null ? reagentBDropdown.GetSelectedValue() : string.Empty;
            string selC = reagentCDropdown != null ? reagentCDropdown.GetSelectedValue() : string.Empty;
            string selD = reagentDDropdown != null ? reagentDDropdown.GetSelectedValue() : string.Empty;

            // Re-sort by new language
            availableReagents.Sort((a, b) =>
                string.Compare(GetDisplayLabel(a), GetDisplayLabel(b), StringComparison.CurrentCultureIgnoreCase));

            var labels = availableReagents.Select(f => GetDisplayLabel(f)).ToList();

            // Re-populate and restore selections
            if (reagentADropdown != null) { reagentADropdown.SetOptions(labels, availableReagents, allowEmpty: false); reagentADropdown.SelectFormula(selA); }
            if (reagentBDropdown != null) { reagentBDropdown.SetOptions(labels, availableReagents, allowEmpty: false); reagentBDropdown.SelectFormula(selB); }
            if (reagentCDropdown != null) { reagentCDropdown.SetOptions(labels, availableReagents, allowEmpty: true);  reagentCDropdown.SelectFormula(selC); }
            if (reagentDDropdown != null) { reagentDDropdown.SetOptions(labels, availableReagents, allowEmpty: true);  reagentDDropdown.SelectFormula(selD); }
        }

        private void SetDefaults()
        {
            inputState = new LabInputViewModel
            {
                ReagentA = availableReagents.Count > 0 ? availableReagents[0] : string.Empty,
                ReagentB = availableReagents.Count > 1 ? availableReagents[1] : string.Empty,
                ReagentC = string.Empty,
                ReagentD = string.Empty,
                MediumIndex = 0,
                Temperature = 25f,
                Stirring = 0.5f,
                Grinding = 0f,
                HasCatalyst = false
            };

            // Push defaults to views
            if (temperatureSlider != null) temperatureSlider.SetValue(inputState.Temperature);
            if (stirringSlider != null) stirringSlider.SetValue(inputState.Stirring);
            if (grindingSlider != null) grindingSlider.SetValue(inputState.Grinding);
            if (catalystToggle != null) catalystToggle.SetValue(inputState.HasCatalyst);
        }

        // -- View Binding --------------------------------------

        private void BindViews()
        {
            if (reagentADropdown != null) reagentADropdown.OnValueChanged += OnReagentAChanged;
            if (reagentBDropdown != null) reagentBDropdown.OnValueChanged += OnReagentBChanged;
            if (reagentCDropdown != null) reagentCDropdown.OnValueChanged += OnReagentCChanged;
            if (reagentDDropdown != null) reagentDDropdown.OnValueChanged += OnReagentDChanged;
            if (mediumDropdown != null) mediumDropdown.OnValueChanged += OnMediumChanged;
            if (temperatureSlider != null) temperatureSlider.OnValueChanged += OnTemperatureChanged;
            if (stirringSlider != null) stirringSlider.OnValueChanged += OnStirringChanged;
            if (grindingSlider != null) grindingSlider.OnValueChanged += OnGrindingChanged;
            if (catalystToggle != null) catalystToggle.OnValueChanged += OnCatalystChanged;
            if (mixButton != null) mixButton.OnMixClicked += OnMix;
        }

        private void UnbindViews()
        {
            if (reagentADropdown != null) reagentADropdown.OnValueChanged -= OnReagentAChanged;
            if (reagentBDropdown != null) reagentBDropdown.OnValueChanged -= OnReagentBChanged;
            if (reagentCDropdown != null) reagentCDropdown.OnValueChanged -= OnReagentCChanged;
            if (reagentDDropdown != null) reagentDDropdown.OnValueChanged -= OnReagentDChanged;
            if (mediumDropdown != null) mediumDropdown.OnValueChanged -= OnMediumChanged;
            if (temperatureSlider != null) temperatureSlider.OnValueChanged -= OnTemperatureChanged;
            if (stirringSlider != null) stirringSlider.OnValueChanged -= OnStirringChanged;
            if (grindingSlider != null) grindingSlider.OnValueChanged -= OnGrindingChanged;
            if (catalystToggle != null) catalystToggle.OnValueChanged -= OnCatalystChanged;
            if (mixButton != null) mixButton.OnMixClicked -= OnMix;
        }

        // -- Callbacks from Views ------------------------------

        private void OnReagentAChanged(string value) { inputState.ReagentA = value; NotifyInputChanged(); }
        private void OnReagentBChanged(string value) { inputState.ReagentB = value; NotifyInputChanged(); }
        private void OnReagentCChanged(string value) { inputState.ReagentC = value; NotifyInputChanged(); }
        private void OnReagentDChanged(string value) { inputState.ReagentD = value; NotifyInputChanged(); }
        private void OnMediumChanged(int index)      { inputState.MediumIndex = index; NotifyInputChanged(); }
        private void OnTemperatureChanged(float val)  { inputState.Temperature = val; NotifyInputChanged(); }
        private void OnStirringChanged(float val)     { inputState.Stirring = val; NotifyInputChanged(); }
        private void OnGrindingChanged(float val)     { inputState.Grinding = val; NotifyInputChanged(); }
        private void OnCatalystChanged(bool val)      { inputState.HasCatalyst = val; NotifyInputChanged(); }

        private static void NotifyInputChanged()
        {
            EventBus.Publish(new InputChangedEvent());
        }

        // -- Mix Action ----------------------------------------

        private void OnMix()
        {
            if (reactionController == null)
            {
                reactionController = FindObjectOfType<ReactionController>();
                if (reactionController == null)
                {
                    Debug.LogError("[LabInputController] ReactionController not found.");
                    return;
                }
            }

            var request = inputState.ToMixRequest();
            Debug.Log($"[LabInputController] Mix → {string.Join(" + ", request.ReagentNames)} | Med={request.Medium} | T={request.Temperature}°C");
            reactionController.RequestMix(request);
        }
    }
}
