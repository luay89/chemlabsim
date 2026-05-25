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
        // ──────────────────────────────────────────────────────────────
        // UI Layout Notes (the actual hierarchy is built in LabV3SceneSetup):
        //   • Root Canvas       → CanvasScaler: ScaleWithScreenSize, reference
        //                          resolution 1920×1080, MatchWidthOrHeight = 0.5
        //                          (handles 16:9 → 4:3 without clipping).
        //   • Left input column → VerticalLayoutGroup + ContentSizeFitter
        //                          (vertical = PreferredSize) so the title
        //                          "Interactive Simulation Deck" can never
        //                          overlap the reagent dropdowns below it.
        //   • Each dropdown row → LayoutElement with minHeight ≥ 48 so TMP
        //                          dropdowns don't get clipped on tall aspects.
        //                          DO NOT nest TMP_Dropdown under another
        //                          VerticalLayoutGroup — it manages its own
        //                          template (see LabController.cs line ~1259).
        //   • Vessel viewport   → anchored to the right half on its own panel
        //                          so the dropdowns and the flask never share
        //                          screen space, regardless of aspect ratio.
        // ──────────────────────────────────────────────────────────────

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

        [Header("Vessel Integration (optional)")]
        [Tooltip("Drag a MonoBehaviour that implements IVesselContainer (e.g. ConicalFlaskContainer " +
                 "or ReactionVesselView). Leave unassigned to rely on MaterialPreviewChangedEvent only.")]
        [SerializeField] private MonoBehaviour vesselContainer;

        // Resolved interface reference (cached in OnInitialize). Reference type — safe to null-check.
        private IVesselContainer vessel;

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

            // Resolve the optional vessel hook. Unity can't serialize interface fields
            // directly, so we accept a MonoBehaviour in the Inspector and cast here.
            vessel = vesselContainer as IVesselContainer;
            if (vesselContainer != null && vessel == null)
            {
                Debug.LogWarning($"[LabInputController] '{vesselContainer.GetType().Name}' is assigned " +
                                 "to the Vessel Integration slot but does not implement IVesselContainer.");
            }

            LoadMaterialLookup();

            // Force the SecureReactionLoader to finish decrypting reactions.bytes
            // before we bind dropdowns. Without this guarantee, controllers wired
            // in by LabV3SceneSetup can race AppManager.Awake on first scene load
            // and end up with an empty reagent list ("No valid reaction matches").
            if (AppManager.Instance != null && !AppManager.Instance.EnsureDatabaseLoaded())
            {
                Debug.LogWarning("[LabInputController] AppManager.EnsureDatabaseLoaded() failed; " +
                                 "dropdowns will fall back to materials.json formulas.");
            }

            var langService = ServiceLocator.Get<LanguageService>();
            currentLanguageIndex = langService != null ? (int)langService.CurrentLanguage : 0;

            PopulateReagentOptions();
            SetDefaults();
            BindViews();

            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);

            // Emit an initial idle preview so the vessel view renders the
            // default Reagent A's physical state immediately after the scene loads.
            PublishMaterialPreview();

            // Seed the environmental VFX pipeline with the default slider values
            // so FXController has a baseline before the user touches anything.
            PublishEnvironment();

            Debug.Log($"[LabInputController] Initialized with {availableReagents.Count} reagents.");
        }

        protected override void OnTeardown()
        {
            UnbindViews();
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        // -- Material Lookup -----------------------------------

#if UNITY_EDITOR
        private const string EditorMaterialsJsonPath = "Assets/_Project/DataSrc/materials.json";
#endif

        private void LoadMaterialLookup()
        {
            materialLookup.Clear();

            string jsonText = materialsJsonAsset != null ? materialsJsonAsset.text : null;

#if UNITY_EDITOR
            // Editor-only safety net: if the TextAsset is unassigned (e.g. when
            // opening LabV3 directly without prior bake), read the source file
            // straight off disk so the UI never starts with an empty material list.
            if (string.IsNullOrEmpty(jsonText) && System.IO.File.Exists(EditorMaterialsJsonPath))
            {
                try
                {
                    jsonText = System.IO.File.ReadAllText(EditorMaterialsJsonPath);
                    Debug.Log("[LabInputController] materialsJsonAsset was unassigned; loaded materials.json directly from disk (Editor only).");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LabInputController] Editor fallback failed to read materials.json: {ex.Message}");
                }
            }
#endif

            if (string.IsNullOrEmpty(jsonText))
                return;

            try
            {
                List<ChemicalMaterial> materials = ParseMaterialsJson(jsonText);
                if (materials == null || materials.Count == 0)
                {
                    Debug.LogWarning("[LabInputController] materials.json parsed to zero entries. Check JSON shape.");
                    return;
                }

                foreach (var mat in materials)
                {
                    if (mat == null || string.IsNullOrEmpty(mat.formula)) continue;
                    if (!materialLookup.ContainsKey(mat.formula))
                        materialLookup[mat.formula] = mat;
                }

                Debug.Log($"[LabInputController] Loaded {materialLookup.Count} materials from JSON.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LabInputController] Failed to parse materials JSON: {ex.Message}");
            }
        }

        // materials.json is a bare top-level JSON array ("[ { ... }, ... ]").
        // JsonUtility cannot deserialise root-level arrays, so we try the object
        // shape first and fall back to wrapping the array under a known field.
        [Serializable]
        private class MaterialListWrapper
        {
            public List<ChemicalMaterial> items = new List<ChemicalMaterial>();
        }

        private static List<ChemicalMaterial> ParseMaterialsJson(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText))
                return null;

            // Case 1: object shape { "materials": [...] }
            try
            {
                var db = JsonUtility.FromJson<MaterialDB>(jsonText);
                if (db != null && db.materials != null && db.materials.Count > 0)
                    return db.materials;
            }
            catch { /* fall through to array handling */ }

            // Case 2: bare array shape "[ ... ]" — wrap so JsonUtility accepts it.
            string trimmed = jsonText.TrimStart();
            if (trimmed.StartsWith("["))
            {
                string wrapped = "{\"items\":" + jsonText + "}";
                var wrapper = JsonUtility.FromJson<MaterialListWrapper>(wrapped);
                if (wrapper != null && wrapper.items != null)
                    return wrapper.items;
            }

            return null;
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
            bool dbAvailable = db != null && db.reactions != null && db.reactions.Count > 0;

            if (dbAvailable)
            {
                availableReagents = db.reactions
                    .SelectMany(r => r != null ? r.GetReactantFormulas() : new List<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();
            }
            else
            {
                // Fallback: SecureReactionLoader couldn't decrypt/parse reactions.bytes
                // (or the blob is stale and validation rejected it). Surface every
                // formula declared in materials.json so the UI is never blank — the
                // user can still browse the chemical catalog, and the mix action will
                // surface a clear "no reaction" message until the DB is regenerated.
                Debug.LogWarning("[LabInputController] ReactionDB unavailable — falling back to materials.json formulas for the reagent dropdowns.");
                availableReagents = materialLookup.Keys
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .Distinct()
                    .ToList();
            }

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

        private void OnReagentAChanged(string value)
        {
            inputState.ReagentA = value;
            NotifyInputChanged();
            PublishMaterialPreview();
        }
        private void OnReagentBChanged(string value) { inputState.ReagentB = value; NotifyInputChanged(); }
        private void OnReagentCChanged(string value) { inputState.ReagentC = value; NotifyInputChanged(); }
        private void OnReagentDChanged(string value) { inputState.ReagentD = value; NotifyInputChanged(); }
        private void OnMediumChanged(int index)      { inputState.MediumIndex = index; NotifyInputChanged(); }
        private void OnTemperatureChanged(float val)  { inputState.Temperature = val; NotifyInputChanged(); PublishEnvironment(); }
        private void OnStirringChanged(float val)     { inputState.Stirring = val; NotifyInputChanged(); PublishEnvironment(); }
        private void OnGrindingChanged(float val)     { inputState.Grinding = val; NotifyInputChanged(); }
        private void OnCatalystChanged(bool val)      { inputState.HasCatalyst = val; NotifyInputChanged(); }

        private static void NotifyInputChanged()
        {
            EventBus.Publish(new InputChangedEvent());
        }

        /// <summary>
        /// Publishes the current temperature and stirring values so
        /// <see cref="FXController"/> can drive continuous environmental VFX
        /// (steam emission, screen distortion, liquid vortex) in real time.
        /// </summary>
        private void PublishEnvironment()
        {
            // LabInputViewModel is a struct, so it is always a valid value.
            EventBus.Publish(new EnvironmentChangedEvent
            {
                Temperature = inputState.Temperature,
                Stirring    = inputState.Stirring
            });
        }

        /// <summary>
        /// Publishes a <see cref="MaterialPreviewChangedEvent"/> describing the
        /// currently selected Reagent A so that vessel/3D views can show an idle
        /// physical-state preview (powder pile, tinted liquid, transparent gas)
        /// without coupling the view layer to <see cref="AppManager"/> or the JSON DB.
        /// </summary>
        private void PublishMaterialPreview()
        {
            // LabInputViewModel is a struct, so ReagentA is read directly.
            string formula = inputState.ReagentA;
            string state = "solid";
            string colorHex = string.Empty;

            if (!string.IsNullOrEmpty(formula)
                && materialLookup.TryGetValue(formula, out var mat) && mat != null)
            {
                state = mat.GetState();
                colorHex = mat.color ?? string.Empty;
            }

            EventBus.Publish(new MaterialPreviewChangedEvent
            {
                Formula = formula ?? string.Empty,
                State = state,
                ColorHex = colorHex
            });

            // Drive the optional vessel hook (3D flask, future presenters). No-op when unwired.
            UpdateVesselVisuals(state, ParseHexColor(colorHex));
        }

        // ── Vessel Integration ──────────────────────────────────────

        /// <summary>
        /// Forwards an idle-preview update to any wired <see cref="IVesselContainer"/>
        /// implementation (e.g. <c>ConicalFlaskContainer</c>). Safe to call when no
        /// vessel is wired — the call becomes a no-op. Public so external systems
        /// (e.g. scripted tutorials) can also drive the visual flask directly.
        /// </summary>
        /// <param name="physicalState">"solid", "liquid", "gas", or anything else (treated as Unknown).</param>
        /// <param name="substanceColor">RGBA tint; alpha 0 signals "no colour info".</param>
        public void UpdateVesselVisuals(string physicalState, Color substanceColor)
        {
            if (vessel == null) return;
            vessel.UpdateVesselVisuals(ParsePhysicalState(physicalState), substanceColor);
        }

        private static PhysicalState ParsePhysicalState(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return PhysicalState.Unknown;
            switch (raw.Trim().ToLowerInvariant())
            {
                case "solid":  return PhysicalState.Solid;
                case "liquid": return PhysicalState.Liquid;
                case "gas":    return PhysicalState.Gas;
                default:       return PhysicalState.Unknown;
            }
        }

        private static Color ParseHexColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.clear;
            var s = hex.StartsWith("#") ? hex : "#" + hex;
            return ColorUtility.TryParseHtmlString(s, out var c) ? c : Color.clear;
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
