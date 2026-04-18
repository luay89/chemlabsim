using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public class LabController : MonoBehaviour
{
    [Header("UI / Reaction Inputs")]
    [FormerlySerializedAs("reagentA")]
    [SerializeField] private TMP_Dropdown reagentADropdown;
    [FormerlySerializedAs("reagentB")]
    [SerializeField] private TMP_Dropdown reagentBDropdown;
    [SerializeField] private TMP_Dropdown reagentCDropdown;
    [SerializeField] private TMP_Dropdown reagentDDropdown;
    [FormerlySerializedAs("mediumPH")]
    [SerializeField] private TMP_Dropdown mediumDropdown;
    [FormerlySerializedAs("stirring01")]
    [SerializeField] private Slider stirringSlider;
    [FormerlySerializedAs("grinding01")]
    [SerializeField] private Slider grindingSlider;
    [FormerlySerializedAs("temperatureC")]
    [SerializeField] private Slider temperatureSlider;
    [SerializeField] private Toggle catalystToggle;

    [Header("UI / Actions")]
    [SerializeField] private Button mixButton;
    [SerializeField] private Button backButton;

    [Header("UI / Output")]
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private TextMeshProUGUI historyText;

    [Header("Optional FX")]
    [SerializeField] private ParticleSystem gasFx;
    [SerializeField] private ParticleSystem successFx;
    [SerializeField] private ParticleSystem failFx;
    [SerializeField] private ParticleSystem catalystFx;
    [SerializeField] private ParticleSystem heatFx;
    [SerializeField] private ParticleSystem precipitateFx;

    private ReactionDB db;
    private const string BuildVersion = "1.0.0";
    private const int MaxHistoryEntries = 5;
    private const float ResultFeedbackDuration = 0.16f;
    private const float SuccessAlphaBoost = 1f;
    private const float FailureAlphaBoost = 0.75f;
    private readonly List<ExperimentHistoryEntry> experimentHistory = new List<ExperimentHistoryEntry>();
    private Coroutine resultFeedbackRoutine;
    private ReactionEvaluationResult lastEvaluationResult;
    private ReactionEvaluationInput lastEvaluationInput;
    private int lastEvaluationScoreDelta;
    private bool hasLastEvaluation;
    private bool guidanceDismissed;
    private int sessionScore;
    private int sessionTotalExperiments;
    private int sessionSuccessCount;
    private int sessionInvalidExperiments;
    private int sessionBestScore;
    private int sessionStreak;
    private int lastScoreDelta;

    // -- Level / Lesson Progression ----------------------------
    private int currentLevel = 1;
    private string currentLessonTitle = "Basic Reactions";
    private int successfulExperimentsInLevel;
    private const int NextLevelRequirement = 2;
    private string lastLevelUpTitle;

    private static readonly string[] LessonTitles =
    {
        "Basic Reactions",
        "Medium and Temperature",
        "Catalyst and Contact",
        "Advanced Reaction Conditions"
    };
    // -- End Level / Lesson Fields -----------------------------

    // -- Lesson Objectives --------------------------------------
    private string currentObjectiveTitle;
    private bool objectiveCompleted;
    private bool objectiveJustCompleted;

    private static readonly string[] ObjectiveTitles =
    {
        "Perform one valid successful reaction.",
        "Complete a reaction using the correct medium.",
        "Complete a reaction with strong contact or proper catalyst use.",
        "Complete an advanced successful reaction under correct conditions."
    };
    // -- End Lesson Objectives ----------------------------------

    // -- Challenge Mode ----------------------------------------
    private string currentChallengeTitle;
    private bool challengeCompleted;
    private const int ChallengeRewardPoints = 10;
    private bool challengeJustCompleted;
    private int lastChallengeReward;

    private struct ChallengeDefinition
    {
        public string Title;
        public int Level;
    }

    private static readonly ChallengeDefinition[] ChallengeDefinitions =
    {
        new ChallengeDefinition { Title = "Complete a successful reaction without catalyst", Level = 1 },
        new ChallengeDefinition { Title = "Use the correct medium in one attempt", Level = 2 },
        new ChallengeDefinition { Title = "Reach a strong contact factor (>=1.2)", Level = 3 },
        new ChallengeDefinition { Title = "Complete two successful reactions in a row", Level = 4 }
    };
    // -- End Challenge Mode Fields -----------------------------

    // -- Save / Load Progress ----------------------------------
    private const string SaveKeyPrefix = "ChemLab_";
    // -- End Save / Load Fields --------------------------------

    // -- Achievements ------------------------------------------
    private readonly HashSet<string> unlockedAchievements = new HashSet<string>();
    private string lastUnlockedAchievement;
    private bool achievementJustUnlocked;

    private const string AchievFirstReaction   = "First Successful Reaction";
    private const string AchievReachLevel2     = "Reach Level 2";
    private const string Achiev5Experiments    = "Complete 5 Experiments";
    private const string AchievFirstChallenge  = "Complete First Challenge";
    private const string AchievScore100        = "Reach 100 Score";
    private const string AchievUseCatalyst     = "Use Catalyst Correctly";
    // -- End Achievements Fields -------------------------------

    private UnityEngine.Events.UnityAction<int> guidanceDropdownListener;
    private UnityEngine.Events.UnityAction<float> guidanceSliderListener;
    private UnityEngine.Events.UnityAction<bool> guidanceToggleListener;

    private struct ExperimentHistoryEntry
    {
        public string ReagentSummary;
        public int MediumIndex;
        public string OutcomeKey;
    }

    private enum MediumUi
    {
        Neutral = 0,
        Acidic = 1,
        Basic = 2
    }

    private const string MenuSceneName = "Menu";

    private static string L(string english, string arabic)
    {
        return english;
    }

    private static string OptionalReagentLabel()
    {
        return "Optional";
    }

    private static bool IsOptionalReagentSelection(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        string trimmed = value.Trim();
        return trimmed == "Optional";
    }

    // -- UI Formatting (Rich Text) -----------------------------
    private static class C
    {
        public const string Gold    = "#FFD54F";
        public const string Sky     = "#90CAF9";
        public const string Green   = "#66BB6A";
        public const string Red     = "#EF5350";
        public const string Orange  = "#FFA726";
        public const string Cyan    = "#4FC3F7";
        public const string Gray    = "#9E9E9E";
        public const string White   = "#E0E0E0";
        public const string Dim     = "#546E7A";
        public const string Amber   = "#FFD54F";
        public const string Purple  = "#CE93D8";
        public const string Teal    = "#4DB6AC";
        public const string Lime    = "#A5D6A7";
        public const string Rose    = "#F48FB1";
        public const string Slate   = "#78909C";
        public const string Steel   = "#B0BEC5";
    }

    private static string Clr(string hex, string t) => $"<color={hex}>{t}</color>";
    private static string B(string t)               => $"<b>{t}</b>";
    private static string Sz(int pct, string t)      => $"<size={pct}%>{t}</size>";
    private static string SectionHeader(string title) => $"\n{Sz(110, Clr(C.Gold, B(title)))}\n{Clr(C.Dim, "- - - - - - - - - - - - - - - -")}\n";
    private static string ThinDivider()              => Clr(C.Slate, ". . . . . . . . . . . . . . . .");
    private static string Lbl(string label, string val) => $"{Clr(C.Steel, label + ":")} {val}";
    private static string Indicator(bool ok)         => ok ? Clr(C.Green, "[+]") : Clr(C.Red, "[-]");
    private static string IndicatorWarn()            => Clr(C.Orange, "[~]");
    // -- End UI Formatting -------------------------------------

    // -- Dynamic Slider Value Labels ---------------------------
    private const string SliderLabelName = "_ValueLabel";
    private TextMeshProUGUI tempValueLabel;
    private TextMeshProUGUI stirValueLabel;
    private TextMeshProUGUI grindValueLabel;
    // -- End Dynamic Slider Value Labels -----------------------

    private Button languageButton;
    private TextMeshProUGUI languageButtonLabel;
    private TextMeshProUGUI hudTitleText;
    private TextMeshProUGUI hudSubtitleText;
    private TextMeshProUGUI hudStatusText;
    private RectTransform reactionDashboardRect;
    private Image reactionDashboardImage;
    private TextMeshProUGUI reactionDashboardTitleText;
    private TextMeshProUGUI reactionDashboardStateText;
    private TextMeshProUGUI reactionDashboardMetricsText;
    private TextMeshProUGUI reactionDashboardOutcomeText;
    private TextMeshProUGUI reagentALabelText;
    private TextMeshProUGUI reagentBLabelText;
    private TextMeshProUGUI reagentCLabelText;
    private TextMeshProUGUI reagentDLabelText;
    private TextMeshProUGUI mediumLabelText;
    private TextMeshProUGUI stirringLabelText;
    private TextMeshProUGUI grindingLabelText;
    private TextMeshProUGUI temperatureLabelText;
    private TextMeshProUGUI catalystLabelText;
    private TextMeshProUGUI resultPanelLabelText;

    // -- Scrollable Result Panel --------------------------------
    private ScrollRect resultScrollRect;
    // -- End Scrollable Result Panel ----------------------------
    private float reactionDashboardAnchorY = -120f;

    private struct AnimatedGlow
    {
        public Graphic Graphic;
        public Vector3 BaseScale;
        public float BaseAlpha;
        public float PulseAmplitude;
        public float PulseSpeed;
        public float ScaleAmplitude;
    }

    private readonly List<AnimatedGlow> animatedGlows = new List<AnimatedGlow>();
    private ParticleSystem ambientLabFx;
    private Material runtimeParticleFxMaterial;

    private void OnEnable()
    {
        if (!ValidateUiReferences())
            return;

        EnsureAdditionalReagentDropdowns();

        mixButton.onClick.RemoveListener(OnMix);
        mixButton.onClick.AddListener(OnMix);

        backButton.onClick.RemoveListener(OnBack);
        backButton.onClick.AddListener(OnBack);

        guidanceDropdownListener = _ => UpdateGuidanceMessage();
        guidanceSliderListener = _ => { UpdateGuidanceMessage(); UpdateSliderLabels(); };
        guidanceToggleListener = _ => UpdateGuidanceMessage();

        if (reagentADropdown != null) reagentADropdown.onValueChanged.AddListener(guidanceDropdownListener);
        if (reagentBDropdown != null) reagentBDropdown.onValueChanged.AddListener(guidanceDropdownListener);
        if (reagentCDropdown != null) reagentCDropdown.onValueChanged.AddListener(guidanceDropdownListener);
        if (reagentDDropdown != null) reagentDDropdown.onValueChanged.AddListener(guidanceDropdownListener);
        if (mediumDropdown != null)   mediumDropdown.onValueChanged.AddListener(guidanceDropdownListener);
        if (stirringSlider != null)   stirringSlider.onValueChanged.AddListener(guidanceSliderListener);
        if (grindingSlider != null)   grindingSlider.onValueChanged.AddListener(guidanceSliderListener);
        if (temperatureSlider != null) temperatureSlider.onValueChanged.AddListener(guidanceSliderListener);
        if (catalystToggle != null)   catalystToggle.onValueChanged.AddListener(guidanceToggleListener);
        AppLanguageSettings.LanguageChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        SaveProgress();

        if (resultFeedbackRoutine != null)
        {
            StopCoroutine(resultFeedbackRoutine);
            resultFeedbackRoutine = null;
        }

        if (mixButton != null)
            mixButton.onClick.RemoveListener(OnMix);

        if (backButton != null)
            backButton.onClick.RemoveListener(OnBack);

        if (reagentADropdown != null) reagentADropdown.onValueChanged.RemoveListener(guidanceDropdownListener);
        if (reagentBDropdown != null) reagentBDropdown.onValueChanged.RemoveListener(guidanceDropdownListener);
        if (reagentCDropdown != null) reagentCDropdown.onValueChanged.RemoveListener(guidanceDropdownListener);
        if (reagentDDropdown != null) reagentDDropdown.onValueChanged.RemoveListener(guidanceDropdownListener);
        if (mediumDropdown != null)   mediumDropdown.onValueChanged.RemoveListener(guidanceDropdownListener);
        if (stirringSlider != null)   stirringSlider.onValueChanged.RemoveListener(guidanceSliderListener);
        if (grindingSlider != null)   grindingSlider.onValueChanged.RemoveListener(guidanceSliderListener);
        if (temperatureSlider != null) temperatureSlider.onValueChanged.RemoveListener(guidanceSliderListener);
        if (catalystToggle != null)   catalystToggle.onValueChanged.RemoveListener(guidanceToggleListener);
        AppLanguageSettings.LanguageChanged -= OnLanguageChanged;
    }

    private void Start()
    {
        if (!ValidateUiReferences())
            return;

        EnsureAdditionalReagentDropdowns();
        ConfigureCanvasAndLayout();

        if (AppManager.Instance == null)
        {
            SetResult(L("AppManager is missing.", ""));
            return;
        }

        db = AppManager.Instance.ReactionDatabase;

        if (db == null)
        {
            SetResult(L("Reaction database is not loaded.", ""));
            return;
        }

        if (db.reactions == null || db.reactions.Count == 0)
        {
            SetResult(L("Reaction database is empty.", ""));
            return;
        }

        PopulateReagentDropdowns();
        PopulateMediumDropdown();

        stirringSlider.value = 0.5f;
        grindingSlider.value = 0.5f;
        temperatureSlider.value = 25f;

        AdjustTemperatureSliderRange();

        guidanceDismissed = false;

        CreateSliderValueLabels();
        if (resultText != null) resultText.richText = true;
        SetupScrollableResult();
        CreateHistoryPanelIfNeeded();
        EnsureLanguageButton();
        CreateLabHud();
        CreateReactionDashboard();
        CreateImmersiveLabBackdrop();
        EnsureReactionFxSetup();

        currentLevel = 1;
        currentLessonTitle = GetLessonTitleForLevel(currentLevel);
        successfulExperimentsInLevel = 0;
        lastLevelUpTitle = null;

        currentChallengeTitle = GetChallengeForCurrentLevel();
        challengeCompleted = false;
        challengeJustCompleted = false;
        lastChallengeReward = 0;

        currentObjectiveTitle = GetObjectiveForCurrentLevel();
        objectiveCompleted = false;
        objectiveJustCompleted = false;

        LoadProgress();

        ApplyLocalizedUi();
        UpdateGuidanceMessage();
    }

    private void Update()
    {
        if (animatedGlows.Count == 0)
            return;

        float t = Time.unscaledTime;
        for (int i = 0; i < animatedGlows.Count; i++)
        {
            AnimatedGlow glow = animatedGlows[i];
            if (glow.Graphic == null)
                continue;

            float pulse = 0.5f + 0.5f * Mathf.Sin(t * glow.PulseSpeed + i * 0.9f);
            Color color = glow.Graphic.color;
            color.a = glow.BaseAlpha + (pulse - 0.5f) * glow.PulseAmplitude;
            glow.Graphic.color = color;

            if (glow.Graphic.rectTransform != null)
            {
                float scale = 1f + (pulse - 0.5f) * glow.ScaleAmplitude;
                glow.Graphic.rectTransform.localScale = glow.BaseScale * scale;
            }
        }
    }

    private bool ValidateUiReferences()
    {
        bool ok = true;

        if (reagentADropdown == null) { Debug.LogError("LabController: Missing Inspector reference 'reagentADropdown'."); ok = false; }
        if (reagentBDropdown == null) { Debug.LogError("LabController: Missing Inspector reference 'reagentBDropdown'."); ok = false; }
        if (mediumDropdown == null) { Debug.LogError("LabController: Missing Inspector reference 'mediumDropdown'."); ok = false; }
        if (stirringSlider == null) { Debug.LogError("LabController: Missing Inspector reference 'stirringSlider'."); ok = false; }
        if (grindingSlider == null) { Debug.LogError("LabController: Missing Inspector reference 'grindingSlider'."); ok = false; }
        if (temperatureSlider == null) { Debug.LogError("LabController: Missing Inspector reference 'temperatureSlider'."); ok = false; }
        if (catalystToggle == null) { Debug.LogError("LabController: Missing Inspector reference 'catalystToggle'."); ok = false; }
        if (mixButton == null) { Debug.LogError("LabController: Missing Inspector reference 'mixButton'."); ok = false; }
        if (backButton == null) { Debug.LogError("LabController: Missing Inspector reference 'backButton'."); ok = false; }
        if (resultText == null) { Debug.LogError("LabController: Missing Inspector reference 'resultText'."); ok = false; }

        if (!ok)
        {
            Debug.LogError("LabController: One or more UI references are missing. Disabling component.");

            if (resultText != null)
                resultText.text = L("UI setup is incomplete.", "");

            enabled = false;
        }

        return ok;
    }

    // -- Canvas Scaler & Responsive Layout ----------------------

    private void ConfigureCanvasAndLayout()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // Fix CanvasScaler
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Resize UI elements for 1080-wide reference
        const float elementW = 680f;
        const float dropH = 64f;
        const float sliderH = 48f;
        const float btnH = 72f;

        // Top section starts higher now
        float y = 640f;
        const float gap = 18f;

        // Dropdown stack
        ResizeElement(reagentADropdown, 0f, y, elementW, dropH);
        y -= dropH + gap;
        ResizeElement(reagentBDropdown, 0f, y, elementW, dropH);
        y -= dropH + gap;
        ResizeElement(reagentCDropdown, 0f, y, elementW, dropH);
        y -= dropH + gap;
        ResizeElement(reagentDDropdown, 0f, y, elementW, dropH);
        y -= dropH + gap;
        ResizeElement(mediumDropdown, 0f, y, elementW, dropH);
        y -= dropH + gap + 8f;

        // Sliders
        ResizeElement(stirringSlider, 0f, y, elementW, sliderH);
        y -= sliderH + gap;
        ResizeElement(grindingSlider, 0f, y, elementW, sliderH);
        y -= sliderH + gap;
        ResizeElement(temperatureSlider, 0f, y, elementW, sliderH);
        y -= sliderH + gap + 8f;

        // Toggle
        if (catalystToggle != null)
        {
            var toggleRect = catalystToggle.GetComponent<RectTransform>();
            if (toggleRect != null)
            {
                toggleRect.anchorMin = new Vector2(0.5f, 0.5f);
                toggleRect.anchorMax = new Vector2(0.5f, 0.5f);
                toggleRect.pivot = new Vector2(0.5f, 0.5f);
                toggleRect.anchoredPosition = new Vector2(0f, y);
                toggleRect.sizeDelta = new Vector2(elementW, 50f);

                // Scale toggle label if it exists
                var toggleLabel = catalystToggle.GetComponentInChildren<TextMeshProUGUI>();
                if (toggleLabel != null) toggleLabel.fontSize = Mathf.Max(toggleLabel.fontSize, 26f);
            }
            y -= 50f + gap + 8f;
        }

        // Mix button
        ResizeElement(mixButton, 0f, y, elementW, btnH);
        y -= btnH + gap;
        reactionDashboardAnchorY = y;
        const float dashboardH = 124f;
        y -= dashboardH + gap;

        // Result text panel will be created by SetupScrollableResult later,
        // but update the base resultText position so it anchors correctly
        if (resultText != null)
        {
            var rr = resultText.rectTransform;
            rr.anchorMin = new Vector2(0.5f, 0.5f);
            rr.anchorMax = new Vector2(0.5f, 0.5f);
            rr.pivot = new Vector2(0.5f, 1f);
            rr.anchoredPosition = new Vector2(0f, y);
            rr.sizeDelta = new Vector2(elementW, 356f);
        }
        y -= 356f + gap;

        // Back button (below result panel)
        ResizeElement(backButton, 0f, y, elementW, btnH);
        y -= btnH + gap;

        // Scale up dropdown fonts
        ScaleDropdownFont(reagentADropdown, 28);
        ScaleDropdownFont(reagentBDropdown, 28);
        ScaleDropdownFont(reagentCDropdown, 28);
        ScaleDropdownFont(reagentDDropdown, 28);
        ScaleDropdownFont(mediumDropdown, 30);

        // Scale up button fonts
        ScaleButtonFont(mixButton, 32);
        ScaleButtonFont(backButton, 30);

        // Style buttons
        StyleLabButton(mixButton, new Color32(25, 115, 78, 255), new Color32(40, 150, 100, 255));
        StyleLabButton(backButton, new Color32(55, 58, 78, 255), new Color32(72, 78, 102, 255));

        // Style dropdowns for visibility
        StyleDropdownVisuals(reagentADropdown);
        StyleDropdownVisuals(reagentBDropdown);
        StyleDropdownVisuals(reagentCDropdown);
        StyleDropdownVisuals(reagentDDropdown);
        StyleDropdownVisuals(mediumDropdown, openUpward: true);
    }

    private void EnsureAdditionalReagentDropdowns()
    {
        if (reagentBDropdown == null)
            return;

        reagentCDropdown ??= CreateRuntimeReagentDropdown(reagentBDropdown, "_ReagentC");
        reagentDDropdown ??= CreateRuntimeReagentDropdown(reagentBDropdown, "_ReagentD");
    }

    private TMP_Dropdown CreateRuntimeReagentDropdown(TMP_Dropdown source, string name)
    {
        if (source == null || source.transform.parent == null)
            return null;

        var clone = Instantiate(source.gameObject, source.transform.parent);
        clone.name = name;

        var dropdown = clone.GetComponent<TMP_Dropdown>();
        if (dropdown == null)
            return null;

        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.ClearOptions();

        var label = dropdown.captionText;
        if (label != null)
        {
            label.text = OptionalReagentLabel();
            label.isRightToLeftText = false;
        }

        return dropdown;
    }

    private void EnsureLanguageButton()
    {
        if (languageButton != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var buttonGo = new GameObject("_LanguageButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.SetParent(canvas.transform, false);
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-18f, -18f);
        buttonRect.sizeDelta = new Vector2(170f, 52f);

        languageButton = buttonGo.GetComponent<Button>();
        var buttonImage = buttonGo.GetComponent<Image>();
        buttonImage.color = Color.white;

        var labelGo = new GameObject("_Label", typeof(RectTransform));
        labelGo.transform.SetParent(buttonRect, false);
        languageButtonLabel = labelGo.AddComponent<TextMeshProUGUI>();
        languageButtonLabel.fontSize = 22;
        languageButtonLabel.fontStyle = FontStyles.Bold;
        languageButtonLabel.color = new Color32(230, 230, 230, 255);
        languageButtonLabel.alignment = TextAlignmentOptions.Center;
        languageButtonLabel.raycastTarget = false;

        var labelRect = languageButtonLabel.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        StyleLabButton(languageButton, new Color32(38, 58, 94, 255), new Color32(52, 79, 125, 255));
        languageButton.onClick.RemoveAllListeners();
        languageButton.onClick.AddListener(ToggleLanguage);
    }

    private void CreateLabHud()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        Transform existingRoot = canvas.transform.Find("_LabHudRoot");
        RectTransform rootRect;
        if (existingRoot == null)
        {
            var rootGo = new GameObject("_LabHudRoot", typeof(RectTransform));
            rootRect = rootGo.GetComponent<RectTransform>();
            rootRect.SetParent(canvas.transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
        }
        else
        {
            rootRect = existingRoot.GetComponent<RectTransform>();
        }

        if (hudTitleText == null)
        {
            var headerPanel = CreateHudPanel(rootRect, "_HeaderPanel", new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(760f, 132f), new Color(0.04f, 0.08f, 0.13f, 0.88f));
            hudTitleText = CreateHudText(headerPanel, "_HudTitle", new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(680f, 44f), 38, new Color(0.92f, 0.97f, 1f, 1f), TextAlignmentOptions.Center);
            hudSubtitleText = CreateHudText(headerPanel, "_HudSubtitle", new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(680f, 28f), 18, new Color(0.56f, 0.82f, 1f, 0.95f), TextAlignmentOptions.Center);
            hudStatusText = CreateHudText(headerPanel, "_HudStatus", new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(700f, 26f), 18, new Color(0.79f, 0.88f, 0.96f, 0.92f), TextAlignmentOptions.Center);
        }

        reagentALabelText ??= CreateFieldLabel(rootRect, "_ReagentALabel", new Vector2(-340f, 700f));
        reagentBLabelText ??= CreateFieldLabel(rootRect, "_ReagentBLabel", new Vector2(-340f, 618f));
        reagentCLabelText ??= CreateFieldLabel(rootRect, "_ReagentCLabel", new Vector2(-340f, 536f));
        reagentDLabelText ??= CreateFieldLabel(rootRect, "_ReagentDLabel", new Vector2(-340f, 454f));
        mediumLabelText ??= CreateFieldLabel(rootRect, "_MediumLabel", new Vector2(-340f, 372f));
        stirringLabelText ??= CreateFieldLabel(rootRect, "_StirringLabel", new Vector2(-340f, 292f));
        grindingLabelText ??= CreateFieldLabel(rootRect, "_GrindingLabel", new Vector2(-340f, 226f));
        temperatureLabelText ??= CreateFieldLabel(rootRect, "_TemperatureLabel", new Vector2(-340f, 160f));
        catalystLabelText ??= CreateFieldLabel(rootRect, "_CatalystLabel", new Vector2(-340f, 88f));
        resultPanelLabelText ??= CreateFieldLabel(rootRect, "_ResultPanelLabel", new Vector2(-340f, -34f), 18, new Color(1f, 0.83f, 0.35f, 0.95f), new Vector2(280f, 28f));
    }

    private void CreateReactionDashboard()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("_ReactionDashboard");
        if (existing != null)
        {
            reactionDashboardRect = existing.GetComponent<RectTransform>();
            reactionDashboardImage = existing.GetComponent<Image>();
            reactionDashboardTitleText = existing.Find("_Title")?.GetComponent<TextMeshProUGUI>();
            reactionDashboardStateText = existing.Find("_State")?.GetComponent<TextMeshProUGUI>();
            reactionDashboardMetricsText = existing.Find("_Metrics")?.GetComponent<TextMeshProUGUI>();
            reactionDashboardOutcomeText = existing.Find("_Outcome")?.GetComponent<TextMeshProUGUI>();
            RefreshReactionDashboard();
            return;
        }

        var panelGo = new GameObject("_ReactionDashboard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        reactionDashboardRect = panelGo.GetComponent<RectTransform>();
        reactionDashboardRect.SetParent(canvas.transform, false);
        reactionDashboardRect.anchorMin = new Vector2(0.5f, 0.5f);
        reactionDashboardRect.anchorMax = new Vector2(0.5f, 0.5f);
        reactionDashboardRect.pivot = new Vector2(0.5f, 1f);
        reactionDashboardRect.anchoredPosition = new Vector2(0f, reactionDashboardAnchorY);
        reactionDashboardRect.sizeDelta = new Vector2(680f, 124f);

        reactionDashboardImage = panelGo.GetComponent<Image>();
        reactionDashboardImage.color = new Color(0.04f, 0.10f, 0.17f, 0.94f);
        reactionDashboardImage.raycastTarget = false;
        RegisterAnimatedGlow(reactionDashboardImage, 0.04f, 0.75f, 0.010f);

        reactionDashboardTitleText = CreateDashboardText(reactionDashboardRect, "_Title", new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(280f, 24f), 18, new Color(0.56f, 0.86f, 1f, 0.96f), TextAlignmentOptions.TopLeft);
        reactionDashboardStateText = CreateDashboardText(reactionDashboardRect, "_State", new Vector2(0f, 1f), new Vector2(18f, -38f), new Vector2(644f, 28f), 24, Color.white, TextAlignmentOptions.TopLeft);
        reactionDashboardMetricsText = CreateDashboardText(reactionDashboardRect, "_Metrics", new Vector2(0f, 1f), new Vector2(18f, -66f), new Vector2(644f, 22f), 17, new Color(0.80f, 0.90f, 0.98f, 0.92f), TextAlignmentOptions.TopLeft);
        reactionDashboardOutcomeText = CreateDashboardText(reactionDashboardRect, "_Outcome", new Vector2(0f, 1f), new Vector2(18f, -90f), new Vector2(644f, 34f), 16, new Color(0.95f, 0.97f, 1f, 0.86f), TextAlignmentOptions.TopLeft);
        reactionDashboardOutcomeText.enableWordWrapping = true;
        reactionDashboardOutcomeText.overflowMode = TextOverflowModes.Ellipsis;
        RefreshReactionDashboard();
    }

    private TextMeshProUGUI CreateDashboardText(RectTransform parent, string name, Vector2 anchorMin, Vector2 offset, Vector2 size, int fontSize, Color color, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMin;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        text.richText = true;
        text.enableAutoSizing = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private void CreateImmersiveLabBackdrop()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        if (canvas.transform.Find("_LabImmersiveBackdrop") != null)
            return;

        var backdrop = new GameObject("_LabImmersiveBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.SetParent(canvas.transform, false);
        backdropRect.SetAsFirstSibling();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;

        var backdropImage = backdrop.GetComponent<Image>();
        backdropImage.color = new Color(0.03f, 0.06f, 0.10f, 0.28f);
        backdropImage.raycastTarget = false;

        CreateDecorativeGlow(backdropRect, "_LeftGlow", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(240f, 1300f), new Vector2(-70f, 0f), new Color(0.10f, 0.50f, 0.92f, 0.14f), 0.10f, 0.50f, 0.06f);
        CreateDecorativeGlow(backdropRect, "_RightGlow", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(260f, 1300f), new Vector2(80f, 0f), new Color(0.04f, 0.88f, 0.60f, 0.12f), 0.09f, 0.60f, 0.05f);
        CreateDecorativeGlow(backdropRect, "_TopBand", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(940f, 120f), new Vector2(0f, -70f), new Color(0.14f, 0.72f, 1f, 0.10f), 0.08f, 0.70f, 0.03f);
        CreateDecorativeGlow(backdropRect, "_ControlGlass", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(760f, 620f), new Vector2(0f, -365f), new Color(0.05f, 0.08f, 0.13f, 0.50f), 0.04f, 0.40f, 0.02f);
        CreateDecorativeGlow(backdropRect, "_ResultHalo", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(800f, 540f), new Vector2(0f, -220f), new Color(0.06f, 0.58f, 0.95f, 0.10f), 0.06f, 0.48f, 0.02f);
        CreateDecorativeGlow(backdropRect, "_FloorBeam", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1000f, 180f), new Vector2(0f, 120f), new Color(0.02f, 0.42f, 0.72f, 0.08f), 0.06f, 0.62f, 0.04f);
        CreateDataRail(backdropRect, "_TopScanA", new Vector2(0.5f, 1f), new Vector2(0f, -134f), new Vector2(840f, 2f), new Color(0.35f, 0.86f, 1f, 0.18f));
        CreateDataRail(backdropRect, "_TopScanB", new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(760f, 1f), new Color(0.45f, 1f, 0.82f, 0.13f));
        CreateDataRail(backdropRect, "_MidScanA", new Vector2(0.5f, 0.5f), new Vector2(0f, 92f), new Vector2(900f, 1f), new Color(0.28f, 0.75f, 1f, 0.12f));
        CreateDataRail(backdropRect, "_CtrlDivider", new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(720f, 1f), new Color(0.40f, 0.85f, 0.95f, 0.10f));
        CreateDataRail(backdropRect, "_MidScanB", new Vector2(0.5f, 0.5f), new Vector2(0f, -462f), new Vector2(900f, 1f), new Color(0.30f, 0.92f, 0.84f, 0.10f));

        if (mixButton != null && mixButton.TryGetComponent<Image>(out var mixImage))
            RegisterAnimatedGlow(mixImage, 0.07f, 2.0f, 0.04f);

        if (languageButton != null && languageButton.TryGetComponent<Image>(out var langImage))
            RegisterAnimatedGlow(langImage, 0.06f, 2.2f, 0.04f);
    }

    private RectTransform CreateHudPanel(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        RegisterAnimatedGlow(image, 0.025f, 0.5f, 0.008f);
        return rect;
    }

    private TextMeshProUGUI CreateHudText(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, int fontSize, Color color, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, anchor.y >= 0.5f ? 1f : 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        text.richText = true;
        text.enableAutoSizing = false;
        return text;
    }

    private TextMeshProUGUI CreateFieldLabel(RectTransform parent, string name, Vector2 position, int fontSize = 17, Color? color = null, Vector2? size = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size ?? new Vector2(280f, 24f);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = color ?? new Color(0.57f, 0.88f, 1f, 0.94f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        text.richText = true;
        text.enableAutoSizing = false;
        return text;
    }

    private void CreateDataRail(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        RegisterAnimatedGlow(image, 0.045f, 0.95f, 0.01f);
    }

    private void CreateDecorativeGlow(
        RectTransform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 size,
        Vector2 anchoredPosition,
        Color color,
        float pulseAmplitude,
        float pulseSpeed,
        float scaleAmplitude)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        RegisterAnimatedGlow(image, pulseAmplitude, pulseSpeed, scaleAmplitude);
    }

    private void RegisterAnimatedGlow(Graphic graphic, float pulseAmplitude, float pulseSpeed, float scaleAmplitude)
    {
        if (graphic == null)
            return;

        animatedGlows.Add(new AnimatedGlow
        {
            Graphic = graphic,
            BaseScale = graphic.rectTransform.localScale,
            BaseAlpha = graphic.color.a,
            PulseAmplitude = pulseAmplitude,
            PulseSpeed = pulseSpeed,
            ScaleAmplitude = scaleAmplitude
        });
    }

    private void EnsureReactionFxSetup()
    {
        Transform fxParent = Camera.main != null ? Camera.main.transform : transform;

        if (ambientLabFx == null)
        {
            ambientLabFx = CreateParticleFx(
                fxParent,
                "_AmbientLabFx",
                new Vector3(0f, 0f, 4.5f),
                loop: true,
                startColor: new Color(0.55f, 0.88f, 1f, 0.10f),
                endColor: new Color(0.05f, 0.95f, 0.75f, 0.02f),
                startLifetime: 5.5f,
                startSpeed: 0.08f,
                startSize: 0.06f,
                gravity: -0.005f,
                emissionRate: 16f,
                shapeScale: new Vector3(3.2f, 2f, 0.6f),
                burstCount: 0,
                noiseStrength: 0.15f);
            ambientLabFx.Play();
        }

        gasFx ??= CreateParticleFx(
            fxParent, "_GasFx", new Vector3(0f, -0.55f, 4f), false,
            new Color(0.85f, 0.95f, 1f, 0.65f), new Color(0.80f, 0.92f, 1f, 0f),
            2.8f, 0.55f, 0.18f, -0.02f, 0f, new Vector3(0.35f, 0.12f, 0.2f), 24, 0.22f);

        successFx ??= CreateParticleFx(
            fxParent, "_SuccessFx", new Vector3(0f, -0.2f, 4f), false,
            new Color(0.30f, 1f, 0.66f, 0.95f), new Color(0.95f, 1f, 0.95f, 0f),
            1.1f, 1.35f, 0.12f, 0f, 0f, new Vector3(0.22f, 0.08f, 0.18f), 20, 0.12f);

        failFx ??= CreateParticleFx(
            fxParent, "_FailFx", new Vector3(0f, -0.25f, 4f), false,
            new Color(1f, 0.35f, 0.35f, 0.7f), new Color(0.42f, 0.08f, 0.08f, 0f),
            1.7f, 0.85f, 0.15f, 0f, 0f, new Vector3(0.28f, 0.08f, 0.18f), 16, 0.18f);

        catalystFx ??= CreateParticleFx(
            fxParent, "_CatalystFx", new Vector3(0f, -0.4f, 4f), false,
            new Color(0.45f, 0.95f, 1f, 0.9f), new Color(0.35f, 0.70f, 1f, 0f),
            1.45f, 0.95f, 0.10f, 0f, 0f, new Vector3(0.18f, 0.18f, 0.18f), 18, 0.3f);

        heatFx ??= CreateParticleFx(
            fxParent, "_HeatFx", new Vector3(0f, -0.45f, 4f), false,
            new Color(1f, 0.65f, 0.22f, 0.7f), new Color(1f, 0.18f, 0.08f, 0f),
            1.6f, 0.65f, 0.16f, -0.03f, 0f, new Vector3(0.30f, 0.10f, 0.20f), 20, 0.2f);

        precipitateFx ??= CreateParticleFx(
            fxParent, "_PrecipitateFx", new Vector3(0f, -0.62f, 4f), false,
            new Color(0.96f, 0.96f, 0.90f, 0.92f), new Color(0.82f, 0.82f, 0.76f, 0.12f),
            2.4f, 0.14f, 0.12f, 0.06f, 0f, new Vector3(0.42f, 0.08f, 0.18f), 28, 0.08f);
    }

    private ParticleSystem CreateParticleFx(
        Transform parent,
        string name,
        Vector3 localPosition,
        bool loop,
        Color startColor,
        Color endColor,
        float startLifetime,
        float startSpeed,
        float startSize,
        float gravity,
        float emissionRate,
        Vector3 shapeScale,
        short burstCount,
        float noiseStrength)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = loop;
        main.duration = Mathf.Max(1.2f, startLifetime);
        main.startLifetime = startLifetime;
        main.startSpeed = startSpeed;
        main.startSize = startSize;
        main.startColor = startColor;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = loop ? 256 : 64;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = emissionRate;
        if (burstCount > 0)
        {
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, burstCount)
            });
        }

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = shapeScale;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var colorGradient = new Gradient();
        colorGradient.SetKeys(
            new[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(endColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(startColor.a, 0f),
                new GradientAlphaKey(Mathf.Max(startColor.a * 0.5f, endColor.a), 0.55f),
                new GradientAlphaKey(endColor.a, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(colorGradient);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.25f),
            new Keyframe(0.18f, 1f),
            new Keyframe(1f, 1.35f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var noise = ps.noise;
        noise.enabled = noiseStrength > 0f;
        noise.strength = noiseStrength;
        noise.frequency = 0.45f;
        noise.scrollSpeed = 0.12f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        Material particleMaterial = GetRuntimeParticleFxMaterial();
        if (particleMaterial != null)
            renderer.sharedMaterial = particleMaterial;

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    private Material GetRuntimeParticleFxMaterial()
    {
        if (runtimeParticleFxMaterial != null)
            return runtimeParticleFxMaterial;

        Shader shader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
            Shader.Find("Particles/Standard Unlit") ??
            Shader.Find("Sprites/Default") ??
            Shader.Find("Unlit/Transparent");

        if (shader == null)
        {
            Debug.LogWarning("LabController: Could not find a compatible particle shader for runtime FX.");
            return null;
        }

        runtimeParticleFxMaterial = new Material(shader)
        {
            name = "_RuntimeParticleFxMaterial"
        };
        runtimeParticleFxMaterial.enableInstancing = true;

        if (runtimeParticleFxMaterial.HasProperty("_BaseMap"))
            runtimeParticleFxMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
        if (runtimeParticleFxMaterial.HasProperty("_MainTex"))
            runtimeParticleFxMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
        if (runtimeParticleFxMaterial.HasProperty("_BaseColor"))
            runtimeParticleFxMaterial.SetColor("_BaseColor", Color.white);
        if (runtimeParticleFxMaterial.HasProperty("_Color"))
            runtimeParticleFxMaterial.SetColor("_Color", Color.white);
        if (runtimeParticleFxMaterial.HasProperty("_Surface"))
            runtimeParticleFxMaterial.SetFloat("_Surface", 1f);
        if (runtimeParticleFxMaterial.HasProperty("_Blend"))
            runtimeParticleFxMaterial.SetFloat("_Blend", 0f);
        if (runtimeParticleFxMaterial.HasProperty("_Cull"))
            runtimeParticleFxMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        if (runtimeParticleFxMaterial.HasProperty("_ZWrite"))
            runtimeParticleFxMaterial.SetFloat("_ZWrite", 0f);

        return runtimeParticleFxMaterial;
    }

    private static void ResizeElement(Component comp, float x, float y, float w, float h)
    {
        if (comp == null) return;
        var rect = comp.GetComponent<RectTransform>();
        if (rect == null) return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
    }

    private static void ScaleDropdownFont(TMP_Dropdown dd, int size)
    {
        if (dd == null) return;
        var label = dd.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.fontSize = size;
        // Also scale the caption text
        if (dd.captionText != null) dd.captionText.fontSize = size;
    }

    private static void ScaleButtonFont(Button btn, int size)
    {
        if (btn == null) return;
        var label = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
        }
    }

    private static void StyleLabButton(Button btn, Color32 normal, Color32 hover)
    {
        if (btn == null) return;
        ColorBlock cb = btn.colors;
        cb.normalColor = normal;
        cb.highlightedColor = hover;
        cb.pressedColor = new Color32(
            (byte)(normal.r * 0.55f), (byte)(normal.g * 0.55f), (byte)(normal.b * 0.55f), 255);
        cb.selectedColor = hover;
        cb.fadeDuration = 0.15f;
        btn.colors = cb;

        Image img = btn.GetComponent<Image>();
        if (img != null) img.color = Color.white;

        var label = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.color = new Color(0.95f, 0.97f, 1f, 1f);
    }

    private static void StyleDropdownVisuals(TMP_Dropdown dd, bool openUpward = false)
    {
        if (dd == null) return;

        // Style the main dropdown button background
        var ddImage = dd.GetComponent<Image>();
        if (ddImage != null)
            ddImage.color = new Color(0.95f, 0.95f, 0.97f, 1f);

        // Caption text — dark on light background
        if (dd.captionText != null)
            dd.captionText.color = new Color(0.10f, 0.10f, 0.15f, 1f);

        // --- Template (the popup that appears when dropdown opens) ---
        var template = dd.template;
        if (template == null) return;

        if (openUpward)
        {
            template.anchorMin = new Vector2(0f, 1f);
            template.anchorMax = new Vector2(1f, 1f);
            template.pivot = new Vector2(0.5f, 0f);
            template.anchoredPosition = new Vector2(0f, 6f);
        }
        else
        {
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = new Vector2(0f, -6f);
        }

        // TMP_Dropdown uses template.sizeDelta.y as the max popup height.
        // Items are cloned from the first child of Content at the item's rect height.
        // Do NOT add VerticalLayoutGroup — TMP_Dropdown manages layout internally.

        var popupCanvas = template.GetComponent<Canvas>();
        if (popupCanvas == null)
            popupCanvas = template.gameObject.AddComponent<Canvas>();
        popupCanvas.overrideSorting = true;
        popupCanvas.sortingOrder = 500;

        if (template.GetComponent<GraphicRaycaster>() == null)
            template.gameObject.AddComponent<GraphicRaycaster>();

        // Dark background for the popup
        var templateImage = template.GetComponent<Image>();
        if (templateImage != null)
            templateImage.color = new Color(0.10f, 0.12f, 0.18f, 1f);

        // Set popup max height large enough for 3 items + padding
        const float itemH = 48f;
        int optionCount = dd.options != null && dd.options.Count > 0 ? dd.options.Count : 3;
        int visibleItems = Mathf.Clamp(optionCount, 3, 5);
        float popupHeight = itemH * visibleItems + 14f;
        template.sizeDelta = new Vector2(template.sizeDelta.x, popupHeight);
        template.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, popupHeight);

        // --- Viewport ---
        Transform viewport = template.Find("Viewport");
        if (viewport != null)
        {
            var vpImg = viewport.GetComponent<Image>();
            if (vpImg != null)
                vpImg.color = new Color(0.10f, 0.12f, 0.18f, 1f);
        }

        // --- Content (parent of item clones) ---
        Transform contentTr = template.Find("Viewport/Content");
        if (contentTr != null)
        {
            // Remove any VerticalLayoutGroup we may have added before (conflicts with TMP_Dropdown)
            var vlg = contentTr.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) Object.Destroy(vlg);

            // Pin content to top-stretch so TMP_Dropdown can grow it downward
            var contentRect = contentTr.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.pivot     = new Vector2(0.5f, 1f);
            }
        }

        // --- Item template (the row that gets cloned for each option) ---
        Transform itemToggle = dd.itemText != null ? dd.itemText.transform.parent : null;
        if (itemToggle != null)
        {
            // TMP_Dropdown reads rect.height from the item template to size rows.
            // Pin anchors to top-stretch so rect.height == sizeDelta.y exactly.
            var itemRect = itemToggle.GetComponent<RectTransform>();
            if (itemRect != null)
            {
                itemRect.anchorMin = new Vector2(0f, 1f);
                itemRect.anchorMax = new Vector2(1f, 1f);
                itemRect.pivot     = new Vector2(0.5f, 1f);
                itemRect.sizeDelta = new Vector2(0f, itemH);
            }

            // Add LayoutElement so size is respected
            var le = itemToggle.GetComponent<LayoutElement>();
            if (le == null) le = itemToggle.gameObject.AddComponent<LayoutElement>();
            le.minHeight = itemH;
            le.preferredHeight = itemH;

            // Item background
            var itemBg = itemToggle.GetComponent<Image>();
            if (itemBg != null)
                itemBg.color = Color.white;

            // Toggle color transitions
            var toggle = itemToggle.GetComponent<Toggle>();
            if (toggle != null)
            {
                toggle.transition = Selectable.Transition.ColorTint;
                if (itemBg != null) toggle.targetGraphic = itemBg;

                ColorBlock tcb = toggle.colors;
                tcb.normalColor      = new Color(0.12f, 0.15f, 0.22f, 1f);
                tcb.highlightedColor = new Color(0.18f, 0.30f, 0.48f, 1f);
                tcb.pressedColor     = new Color(0.08f, 0.12f, 0.20f, 1f);
                tcb.selectedColor    = new Color(0.14f, 0.35f, 0.52f, 1f);
                tcb.colorMultiplier  = 1f;
                tcb.fadeDuration     = 0.12f;
                toggle.colors = tcb;
            }

            // Checkmark icon
            var checkmark = itemToggle.Find("Item Checkmark");
            if (checkmark != null)
            {
                var checkImg = checkmark.GetComponent<Image>();
                if (checkImg != null)
                    checkImg.color = new Color(0.45f, 0.90f, 1f, 0.95f);
            }
        }

        // --- Item text ---
        if (dd.itemText != null)
        {
            dd.itemText.color = new Color(0.95f, 0.96f, 1f, 1f);
            dd.itemText.fontSize = 24f;
            dd.itemText.fontStyle = FontStyles.Bold;
            dd.itemText.alignment = TextAlignmentOptions.MidlineLeft;
            dd.itemText.margin = new Vector4(14f, 0f, 8f, 0f);
        }

        // --- Scrollbar ---
        var scrollbar = template.GetComponentInChildren<Scrollbar>(true);
        if (scrollbar != null)
        {
            var sbImage = scrollbar.GetComponent<Image>();
            if (sbImage != null)
                sbImage.color = new Color(0.08f, 0.10f, 0.15f, 1f);
            if (scrollbar.targetGraphic != null)
                scrollbar.targetGraphic.color = new Color(0.35f, 0.50f, 0.65f, 1f);
        }
    }

    // -- End Canvas Scaler & Responsive Layout ------------------

    private void PopulateReagentDropdowns()
    {
        if (reagentADropdown == null || reagentBDropdown == null)
        {
            Debug.LogError("LabController: Reagent dropdown references are missing.");
            return;
        }

        if (db == null || db.reactions == null)
        {
            Debug.LogWarning("LabController: Reaction database is unavailable. Keeping current reagent dropdown options as fallback.");
            return;
        }

        // NOTE: Dropdown content is sourced from ReactionDB to keep runtime options consistent.
        // NOTE: If localized labels are added later, keep mapping to stable internal IDs.
        var chems = db.reactions
            .SelectMany(r => r != null ? r.GetReactantFormulas() : new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (chems.Count == 0)
        {
            Debug.LogWarning("LabController: No reagents found in database. Keeping current reagent dropdown options as fallback.");
            return;
        }

        PopulateReagentDropdown(reagentADropdown, chems, allowOptionalNone: false);
        PopulateReagentDropdown(reagentBDropdown, chems, allowOptionalNone: false);
        PopulateReagentDropdown(reagentCDropdown, chems, allowOptionalNone: true);
        PopulateReagentDropdown(reagentDDropdown, chems, allowOptionalNone: true);
    }

    private void PopulateReagentDropdown(TMP_Dropdown dropdown, List<string> chemicals, bool allowOptionalNone)
    {
        if (dropdown == null)
            return;

        int preservedIndex = dropdown.value;
        var options = new List<string>();

        if (allowOptionalNone)
            options.Add(OptionalReagentLabel());

        options.AddRange(chemicals);

        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.value = Mathf.Clamp(preservedIndex, 0, options.Count - 1);
        dropdown.RefreshShownValue();
    }

    private void PopulateMediumDropdown()
    {
        if (mediumDropdown == null)
        {
            Debug.LogError("LabController: Medium dropdown reference is missing.");
            return;
        }

        // Keep the medium UI fixed to the evaluator's canonical enum order.
        // Deriving options from the current dataset can hide valid UI choices such as "Basic"
        // when no seeded reaction uses them yet, which also breaks value-to-enum mapping.
        int preservedIndex = mediumDropdown.value;
        var mediums = new List<string>
        {
            GetMediumLabelForIndex((int)MediumUi.Neutral),
            GetMediumLabelForIndex((int)MediumUi.Acidic),
            GetMediumLabelForIndex((int)MediumUi.Basic)
        };

        if (db != null && db.reactions != null)
        {
            var unsupportedMediums = db.reactions
                .Where(r => r != null)
                .Select(r => r.requiredMedium)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizeMediumLabel)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Where(x => x != "Neutral" && x != "Acidic" && x != "Basic")
                .Distinct()
                .ToList();

            if (unsupportedMediums.Count > 0)
            {
                Debug.LogWarning(
                    $"LabController: Ignoring unsupported medium labels in reaction data: {string.Join(", ", unsupportedMediums)}");
            }
        }

        mediumDropdown.ClearOptions();
        mediumDropdown.AddOptions(mediums);
        mediumDropdown.value = Mathf.Clamp(preservedIndex, 0, mediums.Count - 1);
        mediumDropdown.RefreshShownValue();
        StyleDropdownVisuals(mediumDropdown, openUpward: true);
    }

    private string GetMediumLabelForIndex(int index)
    {
        return index switch
        {
            (int)MediumUi.Acidic => L("Acidic", ""),
            (int)MediumUi.Basic => L("Basic", ""),
            _ => L("Neutral", "")
        };
    }

    private string NormalizeMediumLabel(string medium)
    {
        if (string.IsNullOrWhiteSpace(medium))
            return string.Empty;

        string normalized = medium.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "neutral":
                return "Neutral";
            case "acidic":
                return "Acidic";
            case "basic":
                return "Basic";
            default:
                return medium.Trim();
        }
    }

    private void AdjustTemperatureSliderRange()
    {
        if (temperatureSlider == null || db == null || db.reactions == null) return;

        float maxTemp = 100f;
        foreach (var rx in db.reactions)
        {
            if (rx != null && rx.activationTempC > maxTemp)
                maxTemp = rx.activationTempC;
        }

        float ceiling = Mathf.Ceil(maxTemp / 50f) * 50f;
        if (ceiling < 100f) ceiling = 100f;

        temperatureSlider.minValue = 0f;
        temperatureSlider.maxValue = ceiling;
    }

    private void OnBack()
    {
        SceneManager.LoadScene(MenuSceneName);
    }

    private void ToggleLanguage()
    {
        AppLanguageSettings.ToggleLanguage();
    }

    private void OnLanguageChanged(AppLanguage _)
    {
        ApplyLocalizedUi();
    }

    private void ApplyLocalizedUi()
    {
        if (mixButton != null)
        {
            var mixLabel = mixButton.GetComponentInChildren<TextMeshProUGUI>();
            if (mixLabel != null)
            {
                mixLabel.text = "Mix";
                mixLabel.fontStyle = FontStyles.Bold;
            }
        }

        if (backButton != null)
        {
            var backLabel = backButton.GetComponentInChildren<TextMeshProUGUI>();
            if (backLabel != null)
            {
                backLabel.text = "Back";
                backLabel.fontStyle = FontStyles.Bold;
            }
        }

        if (catalystToggle != null)
        {
            var toggleLabel = catalystToggle.GetComponentInChildren<TextMeshProUGUI>();
            if (toggleLabel != null)
            {
                toggleLabel.text = "Catalyst";
                toggleLabel.alignment = TextAlignmentOptions.Left;
            }
        }

        if (languageButtonLabel != null)
            languageButtonLabel.text = "English";
        PopulateReagentDropdowns();
        PopulateMediumDropdown();
        ApplyDropdownLocalization(reagentADropdown);
        ApplyDropdownLocalization(reagentBDropdown);
        ApplyDropdownLocalization(reagentCDropdown);
        ApplyDropdownLocalization(reagentDDropdown);
        ApplyDropdownLocalization(mediumDropdown);
        ApplyContentLocalization();
        ApplyHudLocalization();
        RefreshReactionDashboard();

        if (historyText != null)
            historyText.text = BuildHistoryText();

        if (hasLastEvaluation)
        {
            ApplyResultColor(lastEvaluationResult);
            SetResult(BuildResultMessage(lastEvaluationResult, lastEvaluationInput, lastEvaluationScoreDelta));
        }
        else if (!guidanceDismissed)
            UpdateGuidanceMessage();
    }

    private void ApplyHudLocalization()
    {
        if (hudTitleText != null)
        {
            hudTitleText.alignment = TextAlignmentOptions.Center;
            hudTitleText.text = "<color=#8FE7FF>Interactive Simulation Deck</color>";
        }

        if (hudSubtitleText != null)
        {
            hudSubtitleText.alignment = TextAlignmentOptions.Center;
            hudSubtitleText.text = "Reactive analysis, energy control, and guided lab feedback";
        }

        AppLanguageSettings.ApplyText(reagentALabelText, "Reagent A", "", TextAlignmentOptions.MidlineLeft);
        AppLanguageSettings.ApplyText(reagentBLabelText, "Reagent B", "", TextAlignmentOptions.MidlineLeft);
        AppLanguageSettings.ApplyText(reagentCLabelText, "Reagent C", "", TextAlignmentOptions.MidlineLeft);
        AppLanguageSettings.ApplyText(reagentDLabelText, "Reagent D", "", TextAlignmentOptions.MidlineLeft);
        AppLanguageSettings.ApplyText(mediumLabelText, "Medium", "", TextAlignmentOptions.MidlineLeft);
        AppLanguageSettings.ApplyText(stirringLabelText, "Stirring", "", TextAlignmentOptions.MidlineLeft);
        AppLanguageSettings.ApplyText(grindingLabelText, "Grinding", "", TextAlignmentOptions.MidlineLeft);
        AppLanguageSettings.ApplyText(temperatureLabelText, "Temperature", "", TextAlignmentOptions.MidlineLeft);
        AppLanguageSettings.ApplyText(catalystLabelText, "Catalyst", "", TextAlignmentOptions.MidlineLeft);
        AppLanguageSettings.ApplyText(resultPanelLabelText, "Result", "", TextAlignmentOptions.MidlineLeft);

        UpdateHudStatus();
    }

    private void UpdateHudStatus()
    {
        if (hudStatusText == null)
            return;

        string status = $"Language: English   |   Score: {sessionScore}   |   Level: {currentLevel}   |   Experiments: {sessionTotalExperiments}";

        hudStatusText.isRightToLeftText = false;
        hudStatusText.alignment = TextAlignmentOptions.Center;
        hudStatusText.text = status;
    }

    private void RefreshReactionDashboard()
    {
        if (reactionDashboardRect != null)
            reactionDashboardRect.anchoredPosition = new Vector2(0f, reactionDashboardAnchorY);

        if (reactionDashboardTitleText == null || reactionDashboardStateText == null ||
            reactionDashboardMetricsText == null || reactionDashboardOutcomeText == null)
            return;

        List<string> selectedReagents = GetSelectedReagentSelections();
        bool hasEnoughReactants = selectedReagents.Count >= 2;
        bool duplicateReactants = selectedReagents.Distinct().Count() != selectedReagents.Count;
        string medium = GetSelectedDropdownText(mediumDropdown);
        float temperature = temperatureSlider != null ? temperatureSlider.value : 25f;
        float normalizedTemperature = temperatureSlider != null && temperatureSlider.maxValue > temperatureSlider.minValue
            ? Mathf.InverseLerp(temperatureSlider.minValue, temperatureSlider.maxValue, temperature)
            : 0.25f;
        float stirring = stirringSlider != null ? stirringSlider.value : 0.5f;
        float grinding = grindingSlider != null ? grindingSlider.value : 0.5f;
        float contactEstimate = Mathf.Clamp01((stirring + grinding) * 0.5f);
        bool catalystEnabled = catalystToggle != null && catalystToggle.isOn;

        reactionDashboardTitleText.alignment = TextAlignmentOptions.TopLeft;
        reactionDashboardStateText.alignment = TextAlignmentOptions.TopLeft;
        reactionDashboardMetricsText.alignment = TextAlignmentOptions.TopLeft;
        reactionDashboardOutcomeText.alignment = TextAlignmentOptions.TopLeft;

        reactionDashboardTitleText.text = Clr(C.Sky, B("Live Reaction Chamber"));

        Color panelColor = new Color(0.05f, 0.12f, 0.18f, 0.92f);
        string stateText;
        string outcomeText;

        bool canUseLastEvaluation = hasLastEvaluation && IsCurrentSetupMatchingLastEvaluation(selectedReagents, temperature, stirring, grinding, catalystEnabled);

        if (canUseLastEvaluation)
        {
            if (!lastEvaluationResult.IsValid)
            {
                panelColor = new Color(0.24f, 0.18f, 0.08f, 0.94f);
                stateText = Clr(C.Orange, B(L("No valid reaction profile detected", "")));
                outcomeText = Clr(C.White, L("Select a known reactant set from the database to continue.", ""));
            }
            else if (lastEvaluationResult.MediumMismatch)
            {
                panelColor = new Color(0.24f, 0.10f, 0.10f, 0.94f);
                string requiredMedium = lastEvaluationInput.reaction != null
                    ? LocalizeMediumName(lastEvaluationInput.reaction.requiredMedium)
                    : L("the required medium", "");
                stateText = Clr(C.Red, B(L("Medium mismatch in the chamber", "")));
                outcomeText = Clr(C.White, L($"Switch the medium to {requiredMedium} and mix again.", ""));
            }
            else if (lastEvaluationResult.ActivationNotReached && !DidReact(lastEvaluationResult))
            {
                panelColor = new Color(0.18f, 0.10f, 0.10f, 0.94f);
                stateText = Clr(C.Red, B(L("Activation energy not reached", "")));
                outcomeText = Clr(C.White, L("Increase temperature or use a catalyst if the reaction allows it.", ""));
            }
            else if (lastEvaluationResult.Status == ReactionStatus.Partial)
            {
                panelColor = new Color(0.24f, 0.16f, 0.05f, 0.94f);
                stateText = Clr(C.Orange, B(L("Partial conversion observed", "")));
                outcomeText = Clr(C.White, BuildDashboardOutcomeSummary(lastEvaluationResult, lastEvaluationInput));
            }
            else if (lastEvaluationResult.Status == ReactionStatus.Success)
            {
                panelColor = new Color(0.07f, 0.18f, 0.12f, 0.94f);
                stateText = Clr(C.Green, B(L("Reaction progressing successfully", "")));
                outcomeText = Clr(C.White, BuildDashboardOutcomeSummary(lastEvaluationResult, lastEvaluationInput));
            }
            else
            {
                stateText = Clr(C.Gray, B(L("Reaction idle", "")));
                outcomeText = Clr(C.White, L("Adjust the setup and run the chamber again.", ""));
            }
        }
        else if (!hasEnoughReactants)
        {
            stateText = Clr(C.Sky, B(L("Awaiting at least two reactants", "")));
            outcomeText = Clr(C.White, L("Choose two or more different reactants to unlock the reaction analysis.", ""));
        }
        else if (duplicateReactants)
        {
            panelColor = new Color(0.18f, 0.12f, 0.08f, 0.94f);
            stateText = Clr(C.Orange, B(L("Duplicate reactants selected", "")));
            outcomeText = Clr(C.White, L("Use different reactants in each slot for a valid laboratory setup.", ""));
        }
        else
        {
            bool mayNeedMoreReactants = db != null && db.reactions != null && db.reactions.Any(r =>
                r != null &&
                r.GetReactantFormulas().Count > selectedReagents.Count &&
                selectedReagents.All(sel => r.GetReactantFormulas().Contains(sel)));

            stateText = Clr(C.Cyan, B(L("Setup ready for chamber analysis", "")));
            outcomeText = mayNeedMoreReactants
                ? Clr(C.White, L("This combination may need a third or fourth reactant for full conversion.", ""))
                : Clr(C.White, L("Press Mix to inspect products, color change, gas release, and thermal behavior.", ""));
        }

        reactionDashboardImage.color = panelColor;

        string metricLine = L(
            $"Reactants {selectedReagents.Count}   |   Medium {medium}   |   Catalyst {(catalystEnabled ? "On" : "Off")}",
            "");

        string energyBar = BuildMeterBar(normalizedTemperature, C.Orange);
        string contactBar = BuildMeterBar(contactEstimate, C.Cyan);
        string telemetryLine = L(
            $"Energy {energyBar} {temperature:F0}°C   |   Contact {contactBar} {(contactEstimate * 100f):F0}%",
            "");

        reactionDashboardStateText.text = stateText;
        reactionDashboardMetricsText.text = metricLine;
        reactionDashboardOutcomeText.text = $"{telemetryLine}\n{outcomeText}";
    }

    private bool IsCurrentSetupMatchingLastEvaluation(List<string> selectedReagents, float temperature, float stirring, float grinding, bool catalystEnabled)
    {
        if (!hasLastEvaluation)
            return false;

        ReactionEntry currentReaction = null;
        if (!TryFindReactionBySelectedReagents(selectedReagents, out currentReaction) && selectedReagents.Count >= 2)
            return false;

        if (currentReaction != lastEvaluationInput.reaction)
            return false;

        if (MapMediumFromDropdown(mediumDropdown != null ? mediumDropdown.value : 0) != lastEvaluationInput.medium)
            return false;

        if (!Mathf.Approximately(temperature, lastEvaluationInput.temperatureC))
            return false;

        if (!Mathf.Approximately(stirring, lastEvaluationInput.stirring01))
            return false;

        if (!Mathf.Approximately(grinding, lastEvaluationInput.grinding01))
            return false;

        return catalystEnabled == lastEvaluationInput.hasCatalyst;
    }

    private string BuildMeterBar(float normalized, string activeColor)
    {
        const int totalSegments = 10;
        int filledSegments = Mathf.Clamp(Mathf.RoundToInt(normalized * totalSegments), 0, totalSegments);
        var sb = new StringBuilder(totalSegments * 20 + 8);
        sb.Append(Clr(C.Slate, "["));
        for (int i = 0; i < totalSegments; i++)
            sb.Append(Clr(i < filledSegments ? activeColor : C.Dim, i < filledSegments ? "|" : "."));
        sb.Append(Clr(C.Slate, "]"));
        return sb.ToString();
    }

    private string BuildDashboardOutcomeSummary(ReactionEvaluationResult result, ReactionEvaluationInput input)
    {
        ReactionEntry reaction = input.reaction;
        if (reaction == null)
            return L("No product data available for this selection.", "");

        string products = string.Join(" + ", reaction.GetProductFormulas());
        if (string.IsNullOrWhiteSpace(products))
            products = L("products not listed", "");

        var observations = new List<string>();
        if (reaction.GetProducesGas() && DidReact(result))
            observations.Add(L("gas release", ""));
        if (reaction.visual_effects != null && reaction.visual_effects.precipitate && DidReact(result))
            observations.Add(L("precipitate formation", ""));
        if (reaction.visual_effects != null && !string.IsNullOrWhiteSpace(reaction.visual_effects.color_change) && DidReact(result))
            observations.Add(L($"color shift toward {DescribeVisualColor(reaction.visual_effects.color_change)}", ""));
        if (reaction.visual_effects != null && reaction.visual_effects.temperature_delta > 0f && DidReact(result))
            observations.Add(L($"thermal rise +{reaction.visual_effects.temperature_delta:F0}°C", ""));

        string observationText = observations.Count > 0
            ? string.Join(L(", ", ""), observations)
            : L("no dramatic visible change", "");

        return L(
            $"Products: {products}   |   Observations: {observationText}",
            "");
    }

    private void ApplyDropdownLocalization(TMP_Dropdown dropdown)
    {
        if (dropdown == null)
            return;

        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft;

        if (dropdown.captionText != null)
        {
            dropdown.captionText.isRightToLeftText = false;
            dropdown.captionText.alignment = alignment;
            dropdown.captionText.margin = new Vector4(14f, 0f, 8f, 0f);
        }

        if (dropdown.itemText != null)
        {
            dropdown.itemText.isRightToLeftText = false;
            dropdown.itemText.alignment = alignment;
            dropdown.itemText.margin = new Vector4(14f, 0f, 8f, 0f);
        }

        dropdown.RefreshShownValue();
    }

    private void ApplyContentLocalization()
    {
        TextAlignmentOptions resultAlignment = TextAlignmentOptions.TopLeft;

        if (resultText != null)
        {
            resultText.isRightToLeftText = false;
            resultText.alignment = resultAlignment;
        }

        if (historyText != null)
        {
            historyText.isRightToLeftText = false;
            historyText.alignment = resultAlignment;
        }
    }

    private void OnMix()
    {
        guidanceDismissed = true;

        if (AppManager.Instance == null && db == null)
        {
            hasLastEvaluation = false;
            SetResult(L("AppManager is missing.", ""));
            return;
        }

        if (db == null || db.reactions == null)
        {
            hasLastEvaluation = false;
            SetResult(L("Reaction database is unavailable.", ""));
            return;
        }

        List<string> selectedReagents = GetSelectedReagentSelections();
        int selectedMediumIndex = mediumDropdown != null ? mediumDropdown.value : 0;

        if (!TryValidateSelectionBeforeMix(out string validationMessage, out selectedReagents))
        {
            hasLastEvaluation = false;
            AddHistoryEntry(selectedReagents, selectedMediumIndex, "invalid");
            SetResult(validationMessage);
            return;
        }

        if (!TryBuildEvaluationInput(selectedReagents, out ReactionEvaluationInput input))
        {
            hasLastEvaluation = false;
            AddHistoryEntry(selectedReagents, selectedMediumIndex, "invalid");
            SetResult(BuildNoMatchSelectionMessage(selectedReagents));
            return;
        }

        ReactionEvaluationResult eval = ReactionEvaluator.Evaluate(input);
        lastEvaluationInput = input;
        lastEvaluationResult = eval;
        lastEvaluationScoreDelta = 0;
        hasLastEvaluation = true;
        AddHistoryEntry(selectedReagents, selectedMediumIndex, BuildHistoryOutcome(eval));

        if (!eval.IsValid)
        {
            Debug.LogWarning($"LabController: Invalid evaluation result. Summary='{eval.Summary}'");
            LogEvaluationDetails(eval);
            ApplyResultColor(eval);
            UpdateSessionProgress(eval, 0);
            SetResult(BuildResultMessage(eval, input));
            PlayReactionFx(eval, input);
            return;
        }

        DevLog($"Evaluation Status: {eval.Status}");
        DevLog($"Evaluation Summary: {eval.Summary}");
        LogEvaluationDetails(eval);

        int scoreDelta = CalculateScoreDelta(eval);
        lastEvaluationScoreDelta = scoreDelta;
        sessionScore += scoreDelta;
        UpdateSessionProgress(eval, scoreDelta);
        UpdateChallengeProgress(eval, input);
        if (challengeJustCompleted)
            sessionScore += lastChallengeReward;

        UpdateObjectiveProgress(eval, input);

        UpdateAchievements(eval, input);

        string message = BuildResultMessage(eval, input, scoreDelta);
        ApplyResultColor(eval);
        SetResult(message);

        SaveProgress();

        PlayReactionFx(eval, input);
    }

    private bool TryValidateSelectionBeforeMix(out string message, out List<string> selectedReagents)
    {
        message = string.Empty;
        selectedReagents = GetSelectedReagentSelections();

        if (reagentADropdown == null || reagentBDropdown == null || mediumDropdown == null)
        {
            message = L("Invalid selection: please complete all fields.", "");
            return false;
        }

        bool invalidReagentA = reagentADropdown.options == null ||
                               reagentADropdown.value < 0 ||
                               reagentADropdown.value >= reagentADropdown.options.Count;

        bool invalidReagentB = reagentBDropdown.options == null ||
                               reagentBDropdown.value < 0 ||
                               reagentBDropdown.value >= reagentBDropdown.options.Count;

        bool invalidReagentC = reagentCDropdown != null &&
                               (reagentCDropdown.options == null ||
                                reagentCDropdown.value < 0 ||
                                reagentCDropdown.value >= reagentCDropdown.options.Count);

        bool invalidReagentD = reagentDDropdown != null &&
                               (reagentDDropdown.options == null ||
                                reagentDDropdown.value < 0 ||
                                reagentDDropdown.value >= reagentDDropdown.options.Count);

        bool invalidMedium = mediumDropdown.options == null ||
                             mediumDropdown.value < 0 ||
                             mediumDropdown.value >= mediumDropdown.options.Count;

        if (invalidReagentA || invalidReagentB || invalidReagentC || invalidReagentD || invalidMedium)
        {
            message = L("Invalid selection: please complete all fields.", "");
            return false;
        }

        if (selectedReagents.Count < 2)
        {
            message = L("Choose at least two different reactants.", "");
            return false;
        }

        if (reagentCDropdown != null && reagentDDropdown != null)
        {
            string reagentC = GetSelectedDropdownText(reagentCDropdown);
            string reagentD = GetSelectedDropdownText(reagentDDropdown);
            if (IsOptionalReagentSelection(reagentC) && !IsOptionalReagentSelection(reagentD))
            {
                message = L("Fill reactant 3 before selecting reactant 4.", "");
                return false;
            }
        }

        if (selectedReagents.Distinct().Count() != selectedReagents.Count)
        {
            message = L("Invalid setup: each selected reactant must be different.", "");
            return false;
        }

        return true;
    }

    private bool TryBuildEvaluationInput(List<string> selectedReagents, out ReactionEvaluationInput input)
    {
        input = default;

        if (!TryReadUiValues(out List<string> reagentNames, out ReactionMedium selectedMedium,
                out float stirringValue, out float grindingValue, out float temperatureValue, out bool hasCatalyst))
        {
            return false;
        }

        if (selectedReagents == null || selectedReagents.Count == 0)
            selectedReagents = reagentNames;

        if (!TryFindReactionBySelectedReagents(selectedReagents, out ReactionEntry reaction))
            return false;

        if (reaction == null)
            return false;

        input = new ReactionEvaluationInput(
            reaction,
            stirringValue,
            grindingValue,
            temperatureValue,
            selectedMedium,
            hasCatalyst
        );

        return true;
    }

    private bool TryReadUiValues(
        out List<string> reagentNames,
        out ReactionMedium selectedMedium,
        out float stirringValue,
        out float grindingValue,
        out float temperatureValue,
        out bool hasCatalyst)
    {
        reagentNames = new List<string>();
        selectedMedium = ReactionMedium.Neutral;
        stirringValue = 0f;
        grindingValue = 0f;
        temperatureValue = 0f;
        hasCatalyst = false;

        if (reagentADropdown.options == null || reagentADropdown.options.Count == 0 ||
            reagentBDropdown.options == null || reagentBDropdown.options.Count == 0)
        {
            return false;
        }

        if (reagentADropdown.value < 0 || reagentADropdown.value >= reagentADropdown.options.Count ||
            reagentBDropdown.value < 0 || reagentBDropdown.value >= reagentBDropdown.options.Count)
        {
            return false;
        }

        reagentNames = GetSelectedReagentSelections();
        if (reagentNames.Count < 2)
            return false;

        selectedMedium = MapMediumFromDropdown(mediumDropdown.value);
        stirringValue = stirringSlider.value;
        grindingValue = grindingSlider.value;
        temperatureValue = temperatureSlider.value;
        hasCatalyst = catalystToggle.isOn;

        DevLog($"Selected Reagents: {string.Join(", ", reagentNames)}");
        DevLog($"Selected Medium: {selectedMedium}");

        return true;
    }

    private List<string> GetSelectedReagentSelections()
    {
        return GetReagentDropdowns()
            .Select(GetSelectedDropdownText)
            .Where(x => !string.IsNullOrWhiteSpace(x) && !IsOptionalReagentSelection(x))
            .ToList();
    }

    private IEnumerable<TMP_Dropdown> GetReagentDropdowns()
    {
        if (reagentADropdown != null) yield return reagentADropdown;
        if (reagentBDropdown != null) yield return reagentBDropdown;
        if (reagentCDropdown != null) yield return reagentCDropdown;
        if (reagentDDropdown != null) yield return reagentDDropdown;
    }

    private bool TryFindReactionBySelectedReagents(List<string> selectedReagents, out ReactionEntry reaction)
    {
        reaction = null;
        if (db == null || db.reactions == null || selectedReagents == null || selectedReagents.Count < 2)
            return false;

        string key = BuildSortedReagentKey(selectedReagents);
        reaction = db.reactions.FirstOrDefault(r => r != null && BuildSortedReagentKey(r.GetReactantFormulas()) == key);
        return reaction != null;
    }

    private static string BuildSortedReagentKey(IEnumerable<string> reagents)
    {
        return string.Join("|", reagents
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .OrderBy(x => x));
    }

    private string BuildNoMatchSelectionMessage(List<string> selectedReagents)
    {
        string selectedDisplay = FormatReagentList(selectedReagents);

        bool needsMoreReactants = db != null && db.reactions != null && db.reactions.Any(r =>
            r != null &&
            r.GetReactantFormulas().Count > selectedReagents.Count &&
            selectedReagents.All(sel => r.GetReactantFormulas().Contains(sel)));

        if (needsMoreReactants)
        {
            return L(
                $"The selected set ({selectedDisplay}) looks incomplete. Some reactions need 3 or 4 reactants.",
                "");
        }

        return L(
            $"No valid reaction matches the selected set ({selectedDisplay}).",
            "");
    }

    private string FormatReagentList(IEnumerable<string> reagents)
    {
        List<string> items = reagents?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
        if (items.Count == 0)
            return L("N/A", "");

        return string.Join(" + ", items);
    }

    private ReactionMedium MapMediumFromDropdown(int dropdownValue)
    {
        return dropdownValue switch
        {
            (int)MediumUi.Acidic => ReactionMedium.Acidic,
            (int)MediumUi.Basic => ReactionMedium.Basic,
            _ => ReactionMedium.Neutral
        };
    }

    // -- Guided Learning Mode --------------------------------------

    private void UpdateGuidanceMessage()
    {
        if (guidanceDismissed)
        {
            RefreshReactionDashboard();
            return;
        }

        SetResult(BuildGuidanceMessage());
        RefreshReactionDashboard();
    }

    private string BuildGuidanceMessage()
    {
        if (reagentADropdown == null || reagentBDropdown == null || mediumDropdown == null)
            return Clr(C.Gray, L("Ready for experiment.", ""));

        bool hasOptions = reagentADropdown.options != null && reagentADropdown.options.Count > 0
                       && reagentBDropdown.options != null && reagentBDropdown.options.Count > 0;

        if (!hasOptions)
            return Clr(C.Sky, B(L("Step 1:", ""))) + " " + Clr(C.White, L("Waiting for reagent data to load...", ""));

        List<string> selectedReagents = GetSelectedReagentSelections();
        if (selectedReagents.Count < 2)
            return Clr(C.Sky, B(L("Step 2:", ""))) + " " + Clr(C.White, L("Choose at least two different reactants to begin.", ""));

        if (selectedReagents.Distinct().Count() != selectedReagents.Count)
            return Clr(C.Sky, B(L("Step 2:", ""))) + " " + Clr(C.White, L("Each chosen reactant must be different from the others.", ""));

        string medium = GetSelectedDropdownText(mediumDropdown);

        float temp = temperatureSlider != null ? temperatureSlider.value : 25f;
        float stir = stirringSlider != null ? stirringSlider.value : 0.5f;
        float grind = grindingSlider != null ? grindingSlider.value : 0.5f;
        bool catalyst = catalystToggle != null && catalystToggle.isOn;
        bool mightNeedMoreReactants = db != null && db.reactions != null && db.reactions.Any(r =>
            r != null &&
            r.GetReactantFormulas().Count > selectedReagents.Count &&
            selectedReagents.All(sel => r.GetReactantFormulas().Contains(sel)));

        var sb = new StringBuilder();
        sb.AppendLine(Sz(115, Clr(C.Gold, B($"{L("Guided Mode", "")} -- {FormatReagentList(selectedReagents)}"))));
        sb.AppendLine(ThinDivider());
        sb.AppendLine($"  {Lbl(L("Medium", ""), Clr(C.White, medium))}  {Clr(C.Dim, "|")}  " +
                       $"{Lbl(L("Temp", ""), Clr(C.White, $"{temp:F0}°C"))}  {Clr(C.Dim, "|")}  " +
                       $"{Lbl(L("Stir", ""), Clr(C.White, $"{stir:P0}"))}  {Clr(C.Dim, "|")}  " +
                       $"{Lbl(L("Grind", ""), Clr(C.White, $"{grind:P0}"))}  {Clr(C.Dim, "|")}  " +
                       $"{Lbl(L("Catalyst", ""), catalyst ? Clr(C.Green, L("On", "")) : Clr(C.Gray, L("Off", "")))}");

        if (mightNeedMoreReactants)
            sb.AppendLine($"\n  {Clr(C.Orange, $"> {L("Hint:", "")}")} {Clr(C.White, L("This selection may belong to a reaction that needs an extra reactant in slot 3 or 4.", ""))}");

        if (stir < 0.3f && grind < 0.3f)
            sb.AppendLine($"\n  {Clr(C.Orange, $"> {L("Tip:", "")}")} {Clr(C.White, L("Very low stirring and grinding may reduce contact quality.", ""))}");
        else if (stir < 0.3f)
            sb.AppendLine($"\n  {Clr(C.Cyan, $"> {L("Tip:", "")}")} {Clr(C.White, L("Consider increasing stirring for better reagent contact.", ""))}");
        else if (grind < 0.3f)
            sb.AppendLine($"\n  {Clr(C.Cyan, $"> {L("Tip:", "")}")} {Clr(C.White, L("Consider increasing grinding for better reagent contact.", ""))}");

        if (temp < 20f)
            sb.AppendLine($"  {Clr(C.Cyan, $"> {L("Tip:", "")}")} {Clr(C.White, L("Low temperature may prevent some reactions from activating.", ""))}");

        sb.Append($"\n  {Clr(C.Green, B($"[OK] {L("Ready", "")}"))} {Clr(C.White, L("-- press Mix to evaluate the reaction.", ""))}");

        return sb.ToString();
    }

    // -- End Guided Learning Mode ----------------------------------

    private string BuildResultMessage(ReactionEvaluationResult r, ReactionEvaluationInput input = default, int scoreDelta = 0)
    {
        var sb = new StringBuilder();

        // -- Reaction Header --
        string identity = BuildReactionIdentity(input.reaction);
        if (!string.IsNullOrEmpty(identity))
        {
            sb.AppendLine(identity);
            sb.AppendLine(ThinDivider());
        }

        // -- Status Headline --
        sb.AppendLine(BuildResultHeadline(r));
        sb.AppendLine(Clr(C.Dim, $"{L("Contact", "")}: {r.ContactFactor:F2}    {L("Activation", "")}: {r.ActivationThresholdC:F1}°C    {L("Rate", "")}: {r.Rate01:P0}"));
        sb.AppendLine(SectionHeader(L("REACTOR SNAPSHOT", "")));
        sb.AppendLine(BuildReactorSnapshot(r, input));

        // -- Analysis Section --
        sb.AppendLine(SectionHeader(L("ANALYSIS", "")));
        sb.AppendLine(BuildFactorsSummary(r, input));
        sb.AppendLine(BuildInfluenceSummary(r, input));
        sb.AppendLine(BuildConditionsSummary(r, input));

        // -- Scientific Explanation --
        sb.AppendLine(SectionHeader(L("EXPLANATION", "")));
        sb.AppendLine(Clr(C.White, BuildCausalScientificExplanation(r, input)));

        // -- Lab Report --
        sb.AppendLine(SectionHeader(L("LAB REPORT", "")));
        sb.AppendLine(BuildMaterialStreamSummary(input));
        sb.AppendLine(BuildChemicalEquation(input));
        sb.AppendLine(BuildVisualOutcomeSummary(r, input));
        sb.AppendLine(BuildLabObservation(r, input));
        sb.AppendLine(BuildSafetyNote(r, input));

        // -- Quiz --
        sb.AppendLine(SectionHeader(L("QUIZ", "")));
        sb.AppendLine(BuildQuizQuestion(r, input));

        // -- Score & Progress --
        sb.AppendLine(SectionHeader(L("PROGRESS", "")));
        sb.AppendLine(BuildScoreMessage(scoreDelta));
        sb.AppendLine(BuildProgressMessage());
        sb.AppendLine(BuildLevelMessage());

        // -- Goals & Achievements --
        sb.AppendLine(SectionHeader(L("GOALS", "")));
        sb.AppendLine(BuildChallengeMessage());
        sb.AppendLine(BuildObjectiveMessage());
        sb.AppendLine(BuildAchievementMessage());

        return sb.ToString();
    }

    private string BuildFactorsSummary(ReactionEvaluationResult r, ReactionEvaluationInput input)
    {
        if (!r.IsValid)
            return Clr(C.Gray, $"  {L("Factors", "")}: {L("unable to evaluate (invalid setup)", "")}");

        bool medOk = !r.MediumMismatch;
        bool tempOk = !r.ActivationNotReached || DidReact(r);
        bool tempWarn = r.LowTemperature && tempOk;
        bool contactOk = !r.LowContactQuality;
        bool contactStrong = r.ContactFactor >= 1.2f;
        bool catOk = r.CatalystApplied;
        bool catAvail = input.reaction != null && input.reaction.catalystAllowed;

        string medIcon = medOk ? Indicator(true) : Indicator(false);
        string tempIcon = !tempOk ? Indicator(false) : (tempWarn ? IndicatorWarn() : Indicator(true));
        string contactIcon = contactStrong ? Clr(C.Green, "[+][+]") : (contactOk ? Indicator(true) : IndicatorWarn());
        string catIcon = catOk ? Indicator(true) : (catAvail ? Clr(C.Gray, "-") : Clr(C.Dim, "n/a"));

        return $"  {medIcon} {Clr(C.White, L("Medium", ""))}    {tempIcon} {Clr(C.White, L("Temp", ""))}    {contactIcon} {Clr(C.White, L("Contact", ""))}    {catIcon} {Clr(C.White, L("Catalyst", ""))}";
    }

    private string BuildInfluenceSummary(ReactionEvaluationResult r, ReactionEvaluationInput input)
    {
        string prefix = Clr(C.Cyan, B(L("Key influence:", "")));

        if (!r.IsValid)
            return $"  {prefix} {Clr(C.Gray, L("no valid reaction set was found for the selected reagents.", ""))}";

        if (r.MediumMismatch)
        {
            string required = input.reaction != null && !string.IsNullOrWhiteSpace(input.reaction.requiredMedium)
                ? LocalizeMediumName(input.reaction.requiredMedium)
                : L("required", "");
            string actual = LocalizeMediumName(ReactionEvaluator.MediumLabel(input.medium));
            return $"  {prefix} {Clr(C.White, L($"medium -- requires {required} but {actual} was selected.", ""))}";
        }

        if (r.ActivationNotReached && !DidReact(r))
        {
            float gap = r.ActivationThresholdC - input.temperatureC;
            return $"  {prefix} {Clr(C.White, L($"temperature -- {gap:F0}°C below the activation threshold.", ""))}";
        }

        if (r.Status == ReactionStatus.Partial)
        {
            if (r.LowTemperature)
                return $"  {prefix} {Clr(C.White, L("temperature -- slightly below threshold, limiting reaction yield.", ""))}";
            if (r.LowContactQuality)
                return $"  {prefix} {Clr(C.White, L("contact quality -- weak mixing reduced the reaction yield.", ""))}";
            return $"  {prefix} {Clr(C.White, L("borderline conditions limited the reaction.", ""))}";
        }

        if (r.CatalystApplied && r.ContactFactor >= 1.2f)
            return $"  {prefix} {Clr(C.White, L("catalyst + excellent contact drove the reaction to completion.", ""))}";
        if (r.CatalystApplied)
            return $"  {prefix} {Clr(C.White, L("catalyst -- lowered the energy barrier, enabling the reaction.", ""))}";
        if (r.ContactFactor >= 1.2f)
            return $"  {prefix} {Clr(C.White, L("excellent reagent contact contributed to a high reaction rate.", ""))}";
        if (r.Rate01 >= 0.8f)
            return $"  {prefix} {Clr(C.White, L("all factors balanced well, producing an optimal reaction.", ""))}";

        return $"  {prefix} {Clr(C.White, L("conditions were sufficient but not optimal.", ""))}";
    }

    private string BuildConditionsSummary(ReactionEvaluationResult r, ReactionEvaluationInput input)
    {
        if (!r.IsValid)
            return Clr(C.Gray, $"  {L("Summary", "")}: {L("invalid setup -- no factors evaluated.", "")}");

        string medium = r.MediumMismatch
            ? Clr(C.Red, L("medium mismatch", ""))
            : Clr(C.Green, L("medium suitable", ""));

        string activation = r.ActivationNotReached && !DidReact(r)
            ? Clr(C.Red, L("low activation", ""))
            : Clr(C.Green, L("activation reached", ""));

        string catalyst = input.hasCatalyst
            ? Clr(C.Teal, L("catalyst present", ""))
            : Clr(C.Gray, L("no catalyst", ""));

        string contact;
        if (r.ContactFactor >= 1.2f)
            contact = Clr(C.Green, L("strong contact", ""));
        else if (r.LowContactQuality)
            contact = Clr(C.Orange, L("weak contact", ""));
        else
            contact = Clr(C.White, L("moderate contact", ""));

        return $"  {Clr(C.Gray, $"{L("Summary", "")}:")} {medium} {Clr(C.Dim, "|")} {activation} {Clr(C.Dim, "|")} {catalyst} {Clr(C.Dim, "|")} {contact}";
    }

    private string BuildLabObservation(ReactionEvaluationResult r, ReactionEvaluationInput input)
    {
        string icon = Clr(C.Teal, ">");

        if (!r.IsValid || !DidReact(r))
            return $"  {icon} {Clr(C.Gray, L("No obvious visible change expected.", ""))}";

        ReactionEntry rx = input.reaction;

        if (rx != null && rx.GetProducesGas())
            return $"  {icon} {Clr(C.White, L("Gas evolution may be observed.", ""))}";

        if (rx != null && rx.visual_effects != null && rx.visual_effects.temperature_delta > 0f)
            return $"  {icon} {Clr(C.White, L("Temperature may rise slightly.", ""))}";

        if (r.CatalystApplied)
            return $"  {icon} {Clr(C.White, L("Accelerated reaction may be noticed.", ""))}";

        return $"  {icon} {Clr(C.Gray, L("No obvious visible change expected.", ""))}";
    }

    private string BuildChemicalEquation(ReactionEvaluationInput input)
    {
        ReactionEntry rx = input.reaction;
        string eqLabel = Clr(C.Sky, B($"{L("Equation", "")}:"));

        if (rx == null)
            return $"  {eqLabel} {Clr(C.Gray, L("not available", ""))}";

        var reactantParts = new List<string>();
        var productParts = new List<string>();

        if (rx.reactants != null)
        {
            foreach (var c in rx.reactants)
            {
                if (c == null || string.IsNullOrWhiteSpace(c.formula)) continue;
                string coeff = c.stoich > 1f ? $"{c.stoich:G0}" : "";
                string state = !string.IsNullOrWhiteSpace(c.state) ? $"({c.state})" : "";
                reactantParts.Add($"{coeff}{c.formula}{state}");
            }
        }
        if (rx.products != null)
        {
            foreach (var c in rx.products)
            {
                if (c == null || string.IsNullOrWhiteSpace(c.formula)) continue;
                string coeff = c.stoich > 1f ? $"{c.stoich:G0}" : "";
                string state = !string.IsNullOrWhiteSpace(c.state) ? $"({c.state})" : "";
                productParts.Add($"{coeff}{c.formula}{state}");
            }
        }

        if (reactantParts.Count == 0)
        {
            reactantParts.AddRange(rx.GetReactantFormulas());
        }
        if (productParts.Count == 0)
        {
            productParts.AddRange(rx.GetProductFormulas());
        }

        if (reactantParts.Count == 0)
            return $"  {eqLabel} {Clr(C.Gray, L("not available", ""))}";

        string lhs = string.Join(" + ", reactantParts);
        string rhs = productParts.Count > 0 ? string.Join(" + ", productParts) : "?";
        return $"  {eqLabel} {Clr(C.White, $"{lhs}  ->  {rhs}")}";
    }

    private string BuildVisualOutcomeSummary(ReactionEvaluationResult r, ReactionEvaluationInput input)
    {
        ReactionEntry rx = input.reaction;
        if (rx == null)
            return $"  {Clr(C.Gray, L("Visual outcome: unavailable.", ""))}";

        bool reacted = DidReact(r);
        bool hasGas = rx.GetProducesGas() && reacted;
        bool hasPrecipitate = rx.visual_effects != null && rx.visual_effects.precipitate && reacted;
        bool hasColorShift = rx.visual_effects != null && !string.IsNullOrWhiteSpace(rx.visual_effects.color_change) && reacted;
        float delta = rx.visual_effects != null ? rx.visual_effects.temperature_delta : 0f;

        string gas = hasGas
            ? Clr(C.Cyan, L("Active gas release", ""))
            : Clr(C.Dim, L("No gas release", ""));

        string precipitate = hasPrecipitate
            ? Clr(C.White, L("Solid precipitate detected", ""))
            : Clr(C.Dim, L("No precipitate detected", ""));

        string colorShift = hasColorShift
            ? Clr(C.Purple, L(
                $"Color drifting toward {DescribeVisualColor(rx.visual_effects.color_change)}",
                ""))
            : Clr(C.Dim, L("No visible color drift", ""));

        string thermal;
        if (delta >= 15f && reacted)
            thermal = Clr(C.Orange, L("Strong exothermic profile", ""));
        else if (delta > 0f && reacted)
            thermal = Clr(C.Orange, L("Mild thermal rise", ""));
        else
            thermal = Clr(C.Dim, L("Thermally stable", ""));

        var sb = new StringBuilder();
        sb.AppendLine($"  {Clr(C.Teal, B($"{L("Visual Simulation", "")}:"))}");
        sb.AppendLine($"  {Clr(C.Dim, "-")} {gas}");
        sb.AppendLine($"  {Clr(C.Dim, "-")} {precipitate}");
        sb.AppendLine($"  {Clr(C.Dim, "-")} {colorShift}");
        sb.Append($"  {Clr(C.Dim, "-")} {thermal}");
        return sb.ToString();
    }

    private string BuildMaterialStreamSummary(ReactionEvaluationInput input)
    {
        ReactionEntry rx = input.reaction;
        if (rx == null)
            return $"  {Clr(C.Gray, L("Material stream unavailable.", ""))}";

        string reactants = FormatReagentList(rx.GetReactantFormulas());
        string products = rx.GetProductFormulas().Count > 0
            ? string.Join(" + ", rx.GetProductFormulas())
            : L("Not listed", "");

        string medium = LocalizeMediumName(ReactionEvaluator.MediumLabel(input.medium));
        return $"  {Clr(C.Sky, B(L("Material Stream", "")))}: {Clr(C.White, reactants)}  {Clr(C.Dim, "->")}  {Clr(C.Green, products)}  {Clr(C.Dim, "|")}  {Lbl(L("Medium", ""), Clr(C.White, medium))}";
    }

    private string DescribeVisualColor(string colorValue)
    {
        if (string.IsNullOrWhiteSpace(colorValue))
            return L("a visible change", "");

        if (!ColorUtility.TryParseHtmlString(colorValue.Trim(), out Color parsed))
            return colorValue.Trim();

        Color.RGBToHSV(parsed, out float hue, out float saturation, out float value);

        if (value < 0.15f)
            return L("near black", "");
        if (saturation < 0.12f && value > 0.82f)
            return L("milky white", "");
        if (saturation < 0.12f)
            return L("pale gray", "");
        if (hue < 0.04f || hue >= 0.96f)
            return L("red", "");
        if (hue < 0.10f)
            return L("orange", "");
        if (hue < 0.18f)
            return L("yellow", "");
        if (hue < 0.42f)
            return L("green", "");
        if (hue < 0.60f)
            return L("blue", "");
        if (hue < 0.78f)
            return L("violet", "");

        return L("magenta", "");
    }

    private string BuildSafetyNote(ReactionEvaluationResult r, ReactionEvaluationInput input)
    {
        string icon = Clr(C.Orange, "[!]");
        ReactionEntry rx = input.reaction;

        // -- Use real safety data from reaction entry if available --
        if (rx != null && rx.safety != null)
        {
            var warningsEn = rx.safety.warnings_en;
            var warningsAr = rx.safety.warnings_ar;
            bool hasEn = warningsEn != null && warningsEn.Count > 0;
            bool hasAr = warningsAr != null && warningsAr.Count > 0;

            if (hasEn || hasAr)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"  {Clr(C.Orange, B($"{L("Safety", "")}:"))}");

                // GHS pictogram codes
                if (rx.safety.ghs_icons != null && rx.safety.ghs_icons.Count > 0)
                    sb.AppendLine($"  {Clr(C.Red, B(string.Join("  ", rx.safety.ghs_icons)))}");

                IReadOnlyList<string> primaryWarnings = hasEn ? warningsEn : warningsAr;
                IReadOnlyList<string> secondaryWarnings = null;

                for (int i = 0; i < primaryWarnings.Count; i++)
                {
                    string w = primaryWarnings[i];
                    if (string.IsNullOrWhiteSpace(w)) continue;
                    sb.AppendLine($"  {icon} {Clr(C.White, LocalizeSafetyWarning(w))}");
                }

                if (secondaryWarnings != null)
                {
                    for (int i = 0; i < secondaryWarnings.Count; i++)
                    {
                        string w = secondaryWarnings[i];
                        if (string.IsNullOrWhiteSpace(w)) continue;
                        sb.AppendLine($"  {Clr(C.Dim, ">")} {Clr(C.Gray, LocalizeSafetyWarning(w))}");
                    }
                }

                string result = sb.ToString().TrimEnd();
                if (result.Length > 0) return result;
            }
        }

        // -- Fallback: infer from reaction properties --
        if (rx != null && !string.IsNullOrWhiteSpace(rx.requiredMedium))
        {
            string med = rx.requiredMedium.Trim().ToLowerInvariant();
            if (med == "acidic" || med == "acid")
                return $"  {icon} {Clr(C.Orange, L("Handle acids carefully.", ""))} {Clr(C.White, L("Wear gloves and eye protection.", ""))}";
            if (med == "basic" || med == "base" || med == "alkaline")
                return $"  {icon} {Clr(C.Orange, L("Handle bases carefully.", ""))} {Clr(C.White, L("Wear gloves and eye protection.", ""))}";
        }

        if (rx != null && rx.GetProducesGas())
            return $"  {icon} {Clr(C.Orange, L("Avoid inhaling released gases.", ""))} {Clr(C.White, L("Work in a ventilated area.", ""))}";

        if (r.IsValid && DidReact(r))
            return $"  {icon} {Clr(C.White, L("Wear gloves and eye protection.", ""))}";

        return $"  {Clr(C.Gray, L("[OK] No special safety concern for this reaction.", ""))}";
    }

    private static string LocalizeSafetyWarning(string warning)
    {
        if (string.IsNullOrWhiteSpace(warning))
            return string.Empty;

        return warning.Trim();
    }

    private static bool ContainsArabicCharacters(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if ((c >= '\u0600' && c <= '\u06FF') || (c >= '\u0750' && c <= '\u077F') || (c >= '\u08A0' && c <= '\u08FF'))
                return true;
        }

        return false;
    }

    // -- Student Score System ----------------------------------

    private int CalculateScoreDelta(ReactionEvaluationResult r)
    {
        if (!r.IsValid)
            return 0;

        int delta = 0;

        if (DidReact(r))
            delta += 10;

        if (!r.MediumMismatch)
            delta += 5;

        if (!r.ActivationNotReached)
            delta += 5;

        if (r.CatalystApplied)
            delta += 5;

        if (r.ContactFactor >= 1.0f)
            delta += 5;

        return delta;
    }

    private string BuildScoreMessage(int delta)
    {
        string deltaText = delta > 0
            ? Clr(C.Green, B($"+{delta}"))
            : Clr(C.Gray, "+0");

        return $"  {Clr(C.Amber, B($"{L("Score", "")}:"))} {deltaText}  {Clr(C.Dim, "->")}  {Clr(C.White, B($"{sessionScore}"))}";
    }

    // -- End Student Score System ------------------------------

    // -- Progress Tracking -------------------------------------

    private void UpdateSessionProgress(ReactionEvaluationResult eval, int scoreDelta)
    {
        sessionTotalExperiments++;
        lastScoreDelta = scoreDelta;

        if (!eval.IsValid || eval.MediumMismatch || (eval.ActivationNotReached && !DidReact(eval)))
            sessionInvalidExperiments++;

        if (DidReact(eval))
        {
            sessionSuccessCount++;
            sessionStreak++;
        }
        else
        {
            sessionStreak = 0;
        }

        if (sessionScore > sessionBestScore)
            sessionBestScore = sessionScore;

        UpdateLevelProgress(eval);
    }

    private string BuildProgressMessage()
    {
        if (sessionTotalExperiments <= 0)
            return Clr(C.Gray, $"  {L("No experiments recorded yet.", "")}");

        string level = GetPerformanceLevel();
        string streak = sessionStreak >= 2
            ? $"  {Clr(C.Orange, B($"Streak: {sessionStreak}"))}"
            : string.Empty;

        return $"  {Lbl(L("Experiments", ""), Clr(C.White, $"{sessionTotalExperiments}"))}  {Clr(C.Dim, "|")}  " +
               $"{Lbl(L("Success", ""), Clr(C.Green, $"{sessionSuccessCount}"))}  {Clr(C.Dim, "|")}  " +
               $"{Lbl(L("Best", ""), Clr(C.Amber, $"{sessionBestScore}"))}{streak}\n" +
               $"  {level}";
    }

    private string GetPerformanceLevel()
    {
        if (sessionScore >= 150) return Clr(C.Gold, B(L("Expert Chemist", "")));
        if (sessionScore >= 75)  return Clr(C.Teal, B(L("Competent Chemist", "")));
        if (sessionScore >= 30)  return Clr(C.Sky, B(L("Apprentice Chemist", "")));
        return Clr(C.Gray, L("Novice Chemist", ""));
    }

    // -- End Progress Tracking ---------------------------------

    // -- Level / Lesson Progression ----------------------------

    private void UpdateLevelProgress(ReactionEvaluationResult eval)
    {
        lastLevelUpTitle = null;

        if (!DidReact(eval))
            return;

        successfulExperimentsInLevel++;

        if (successfulExperimentsInLevel >= NextLevelRequirement && currentLevel < LessonTitles.Length)
        {
            currentLevel++;
            successfulExperimentsInLevel = 0;
            currentLessonTitle = GetLessonTitleForLevel(currentLevel);
            lastLevelUpTitle = currentLessonTitle;

            // Reset challenge for the new level
            currentChallengeTitle = GetChallengeForCurrentLevel();
            challengeCompleted = false;

            // Reset objective for the new level
            currentObjectiveTitle = GetObjectiveForCurrentLevel();
            objectiveCompleted = false;
            objectiveJustCompleted = false;
        }
    }

    private string BuildLevelMessage()
    {
        var sb = new StringBuilder();
        sb.Append($"  {Clr(C.Sky, B($"{L("Level", "")}:"))} {Clr(C.White, B($"{currentLevel}"))} {Clr(C.Dim, "--")} {Clr(C.White, LocalizeLessonTitle(currentLessonTitle))}");

        if (!string.IsNullOrEmpty(lastLevelUpTitle))
            sb.Append($"\n  {Clr(C.Green, B($"{L("* Level Up! Unlocked:", "")} {LocalizeLessonTitle(lastLevelUpTitle)}"))}");

        return sb.ToString();
    }

    private string GetLessonTitleForLevel(int level)
    {
        int index = Mathf.Clamp(level - 1, 0, LessonTitles.Length - 1);
        return LessonTitles[index];
    }

    // -- End Level / Lesson Progression ------------------------

    // -- Challenge Mode ----------------------------------------

    private void UpdateChallengeProgress(ReactionEvaluationResult eval, ReactionEvaluationInput input)
    {
        challengeJustCompleted = false;
        lastChallengeReward = 0;

        if (challengeCompleted)
            return;

        if (!DidReact(eval))
            return;

        bool passed = false;

        switch (currentLevel)
        {
            case 1: // Success without catalyst
                passed = !eval.CatalystApplied;
                break;
            case 2: // Correct medium (no mismatch)
                passed = !eval.MediumMismatch;
                break;
            case 3: // Strong contact factor
                passed = eval.ContactFactor >= 1.2f;
                break;
            case 4: // Two in a row (streak >= 2)
                passed = sessionStreak >= 2;
                break;
            default:
                passed = true;
                break;
        }

        if (passed)
        {
            challengeCompleted = true;
            challengeJustCompleted = true;
            lastChallengeReward = ChallengeRewardPoints;
        }
    }

    private string BuildChallengeMessage()
    {
        if (string.IsNullOrEmpty(currentChallengeTitle))
            return Clr(C.Gray, $"  {L("Challenge", "")}: {L("none available.", "")}");

        var sb = new StringBuilder();
        string challengeTitle = LocalizeChallengeTitle(currentChallengeTitle);

        if (challengeCompleted)
        {
            sb.Append($"  {Clr(C.Green, "[OK]")} {Clr(C.White, challengeTitle)}");
            if (challengeJustCompleted)
                sb.Append($"\n  {Clr(C.Amber, B(L($"Challenge completed! +{lastChallengeReward} bonus", "")))}");
        }
        else
        {
            sb.Append($"  {Clr(C.Orange, "[-]")} {Clr(C.White, challengeTitle)}");
        }

        return sb.ToString();
    }

    private string GetChallengeForCurrentLevel()
    {
        for (int i = 0; i < ChallengeDefinitions.Length; i++)
        {
            if (ChallengeDefinitions[i].Level == currentLevel)
                return ChallengeDefinitions[i].Title;
        }

        // Fallback: last challenge
        return ChallengeDefinitions.Length > 0
            ? ChallengeDefinitions[ChallengeDefinitions.Length - 1].Title
            : "Complete a successful reaction";
    }

    // -- End Challenge Mode ------------------------------------

    // -- Lesson Objectives Logic --------------------------------

    private string GetObjectiveForCurrentLevel()
    {
        int index = Mathf.Clamp(currentLevel - 1, 0, ObjectiveTitles.Length - 1);
        return ObjectiveTitles[index];
    }

    private void UpdateObjectiveProgress(ReactionEvaluationResult eval, ReactionEvaluationInput input)
    {
        objectiveJustCompleted = false;

        if (objectiveCompleted)
            return;

        if (!DidReact(eval))
            return;

        bool met = false;

        switch (currentLevel)
        {
            case 1: // Any valid successful reaction
                met = true;
                break;
            case 2: // Correct medium
                met = !eval.MediumMismatch;
                break;
            case 3: // Strong contact or catalyst
                met = eval.ContactFactor >= 1.2f || eval.CatalystApplied;
                break;
            case 4: // Advanced success under correct conditions
                met = !eval.MediumMismatch && !eval.ActivationNotReached && eval.ContactFactor >= 1.0f;
                break;
            default:
                met = true;
                break;
        }

        if (met)
        {
            objectiveCompleted = true;
            objectiveJustCompleted = true;
        }
    }

    private string BuildObjectiveMessage()
    {
        if (string.IsNullOrEmpty(currentObjectiveTitle))
            return Clr(C.Gray, $"  {L("Objective", "")}: {L("none available.", "")}");

        var sb = new StringBuilder();
        string objectiveTitle = LocalizeObjectiveTitle(currentObjectiveTitle);

        if (objectiveCompleted)
        {
            sb.Append($"  {Clr(C.Green, "[OK]")} {Clr(C.White, objectiveTitle)}");
        }
        else
        {
            sb.Append($"  {Clr(C.Cyan, "[-]")} {Clr(C.White, objectiveTitle)}");
        }

        if (objectiveJustCompleted)
            sb.Append($"\n  {Clr(C.Green, B(L("Objective completed!", "")))}");

        return sb.ToString();
    }

    // -- End Lesson Objectives Logic ----------------------------

    // -- Save / Load Progress ----------------------------------

    private void SaveProgress()
    {
        PlayerPrefs.SetInt(SaveKeyPrefix + "Score", sessionScore);
        PlayerPrefs.SetInt(SaveKeyPrefix + "TotalExperiments", sessionTotalExperiments);
        PlayerPrefs.SetInt(SaveKeyPrefix + "SuccessCount", sessionSuccessCount);
        PlayerPrefs.SetInt(SaveKeyPrefix + "InvalidExperiments", sessionInvalidExperiments);
        PlayerPrefs.SetInt(SaveKeyPrefix + "BestScore", sessionBestScore);
        PlayerPrefs.SetInt(SaveKeyPrefix + "Level", currentLevel);
        PlayerPrefs.SetString(SaveKeyPrefix + "LessonTitle", currentLessonTitle ?? "Basic Reactions");
        PlayerPrefs.SetInt(SaveKeyPrefix + "SuccessInLevel", successfulExperimentsInLevel);
        PlayerPrefs.SetString(SaveKeyPrefix + "ChallengeTitle", currentChallengeTitle ?? "");
        PlayerPrefs.SetInt(SaveKeyPrefix + "ChallengeCompleted", challengeCompleted ? 1 : 0);
        PlayerPrefs.SetInt(SaveKeyPrefix + "ObjectiveCompleted", objectiveCompleted ? 1 : 0);
        PlayerPrefs.SetString(SaveKeyPrefix + "Achievements", string.Join(",", unlockedAchievements));
        PlayerPrefs.Save();
        DevLog("LabController: Progress saved.");
    }

    private void LoadProgress()
    {
        if (!PlayerPrefs.HasKey(SaveKeyPrefix + "Score"))
        {
            DevLog("LabController: No saved progress found. Starting fresh.");
            return;
        }

        sessionScore = PlayerPrefs.GetInt(SaveKeyPrefix + "Score", 0);
        sessionTotalExperiments = PlayerPrefs.GetInt(SaveKeyPrefix + "TotalExperiments", 0);
        sessionSuccessCount = PlayerPrefs.GetInt(SaveKeyPrefix + "SuccessCount", 0);
        sessionInvalidExperiments = PlayerPrefs.GetInt(SaveKeyPrefix + "InvalidExperiments", 0);
        sessionBestScore = PlayerPrefs.GetInt(SaveKeyPrefix + "BestScore", 0);

        int savedLevel = PlayerPrefs.GetInt(SaveKeyPrefix + "Level", 1);
        currentLevel = Mathf.Clamp(savedLevel, 1, LessonTitles.Length);
        currentLessonTitle = GetLessonTitleForLevel(currentLevel);

        successfulExperimentsInLevel = Mathf.Max(0, PlayerPrefs.GetInt(SaveKeyPrefix + "SuccessInLevel", 0));

        string savedChallenge = PlayerPrefs.GetString(SaveKeyPrefix + "ChallengeTitle", "");
        currentChallengeTitle = string.IsNullOrEmpty(savedChallenge)
            ? GetChallengeForCurrentLevel()
            : savedChallenge;
        challengeCompleted = PlayerPrefs.GetInt(SaveKeyPrefix + "ChallengeCompleted", 0) == 1;

        currentObjectiveTitle = GetObjectiveForCurrentLevel();
        objectiveCompleted = PlayerPrefs.GetInt(SaveKeyPrefix + "ObjectiveCompleted", 0) == 1;
        objectiveJustCompleted = false;

        string savedAchievements = PlayerPrefs.GetString(SaveKeyPrefix + "Achievements", "");
        unlockedAchievements.Clear();
        if (!string.IsNullOrEmpty(savedAchievements))
        {
            foreach (string a in savedAchievements.Split(','))
            {
                string trimmed = a.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    unlockedAchievements.Add(trimmed);
            }
        }

        if (sessionScore > sessionBestScore)
            sessionBestScore = sessionScore;

        DevLog($"LabController: Progress loaded. Level={currentLevel}, Score={sessionScore}, Experiments={sessionTotalExperiments}");
    }

    private void ResetProgress()
    {
        PlayerPrefs.DeleteKey(SaveKeyPrefix + "Score");
        PlayerPrefs.DeleteKey(SaveKeyPrefix + "TotalExperiments");
        PlayerPrefs.DeleteKey(SaveKeyPrefix + "SuccessCount");
        PlayerPrefs.DeleteKey(SaveKeyPrefix + "InvalidExperiments");
        PlayerPrefs.DeleteKey(SaveKeyPrefix + "BestScore");
        PlayerPrefs.DeleteKey(SaveKeyPrefix + "Level");
        PlayerPrefs.DeleteKey(SaveKeyPrefix + "LessonTitle");
        PlayerPrefs.DeleteKey(SaveKeyPrefix + "SuccessInLevel");
        PlayerPrefs.DeleteKey(SaveKeyPrefix + "ChallengeTitle");
        PlayerPrefs.DeleteKey(SaveKeyPrefix + "ChallengeCompleted");
        PlayerPrefs.DeleteKey(SaveKeyPrefix + "ObjectiveCompleted");
        PlayerPrefs.DeleteKey(SaveKeyPrefix + "Achievements");
        PlayerPrefs.Save();
        DevLog("LabController: Saved progress has been reset.");
    }

    // -- End Save / Load Progress ------------------------------

    // -- Achievements ------------------------------------------

    private void UpdateAchievements(ReactionEvaluationResult eval, ReactionEvaluationInput input)
    {
        achievementJustUnlocked = false;
        lastUnlockedAchievement = null;

        // First Successful Reaction
        if (DidReact(eval) && !HasAchievement(AchievFirstReaction))
        {
            TryUnlockAchievement(AchievFirstReaction);
            return;
        }

        // Reach Level 2
        if (currentLevel >= 2 && !HasAchievement(AchievReachLevel2))
        {
            TryUnlockAchievement(AchievReachLevel2);
            return;
        }

        // Complete 5 Experiments
        if (sessionTotalExperiments >= 5 && !HasAchievement(Achiev5Experiments))
        {
            TryUnlockAchievement(Achiev5Experiments);
            return;
        }

        // Complete First Challenge
        if (challengeCompleted && !HasAchievement(AchievFirstChallenge))
        {
            TryUnlockAchievement(AchievFirstChallenge);
            return;
        }

        // Reach 100 Score
        if (sessionScore >= 100 && !HasAchievement(AchievScore100))
        {
            TryUnlockAchievement(AchievScore100);
            return;
        }

        // Use Catalyst Correctly
        if (eval.CatalystApplied && DidReact(eval) && !HasAchievement(AchievUseCatalyst))
        {
            TryUnlockAchievement(AchievUseCatalyst);
            return;
        }
    }

    private void TryUnlockAchievement(string achievementName)
    {
        if (unlockedAchievements.Add(achievementName))
        {
            achievementJustUnlocked = true;
            lastUnlockedAchievement = achievementName;
            DevLog($"LabController: Achievement unlocked -- {achievementName}");
        }
    }

    private bool HasAchievement(string achievementName)
    {
        return unlockedAchievements.Contains(achievementName);
    }

    private string BuildAchievementMessage()
    {
        var sb = new StringBuilder();
        sb.Append($"  {Clr(C.Amber, B($"{L("Achievements", "")}:"))} {Clr(C.White, $"{unlockedAchievements.Count}")}{Clr(C.Dim, "/6")}");

        if (achievementJustUnlocked && !string.IsNullOrEmpty(lastUnlockedAchievement))
            sb.Append($"\n  {Clr(C.Gold, B($"{L("* Unlocked:", "")} {LocalizeAchievementTitle(lastUnlockedAchievement)}"))}");

        return sb.ToString();
    }

    private string BuildReactorSnapshot(ReactionEvaluationResult r, ReactionEvaluationInput input)
    {
        string stage;
        string stageColor;

        if (!r.IsValid)
        {
            stage = L("Unknown configuration", "");
            stageColor = C.Orange;
        }
        else if (r.MediumMismatch)
        {
            stage = L("Blocked by medium mismatch", "");
            stageColor = C.Red;
        }
        else if (r.ActivationNotReached && !DidReact(r))
        {
            stage = L("Idle: waiting for activation", "");
            stageColor = C.Red;
        }
        else if (r.Status == ReactionStatus.Partial)
        {
            stage = L("Intermediate conversion", "");
            stageColor = C.Orange;
        }
        else if (r.Status == ReactionStatus.Success)
        {
            stage = L("Complete reactive flow", "");
            stageColor = C.Green;
        }
        else
        {
            stage = L("No significant change", "");
            stageColor = C.Gray;
        }

        float activationNormalized = 0f;
        if (r.ActivationThresholdC > 0.01f)
            activationNormalized = Mathf.Clamp01(input.temperatureC / r.ActivationThresholdC);
        else if (input.temperatureC > 0f)
            activationNormalized = 1f;

        float contactNormalized = Mathf.Clamp01(r.ContactFactor / 1.35f);
        float conversionNormalized = EstimateConversionLevel(r);
        float stabilityNormalized = EstimateStabilityLevel(r);

        string mediumText = LocalizeMediumName(ReactionEvaluator.MediumLabel(input.medium));
        string catalystText = input.hasCatalyst
            ? Clr(C.Teal, L("Catalyst active", ""))
            : Clr(C.Gray, L("No catalyst", ""));

        var sb = new StringBuilder();
        sb.AppendLine($"  {Clr(stageColor, B(stage))}");
        sb.AppendLine($"  {Lbl(L("Reactants", ""), Clr(C.White, FormatReagentList(input.reaction != null ? input.reaction.GetReactantFormulas() : GetSelectedReagentSelections())))}");
        sb.AppendLine($"  {Lbl(L("Medium", ""), Clr(C.White, mediumText))}  {Clr(C.Dim, "|")}  {Lbl(L("Catalyst", ""), catalystText)}");
        sb.AppendLine($"  {Lbl(L("Activation", ""), BuildMeterBar(activationNormalized, C.Orange))}  {Clr(C.Dim, "|")}  {Clr(C.White, $"{input.temperatureC:F0}°C")}");
        sb.AppendLine($"  {Lbl(L("Contact", ""), BuildMeterBar(contactNormalized, C.Cyan))}  {Clr(C.Dim, "|")}  {Clr(C.White, $"{r.ContactFactor:F2}")}");
        sb.AppendLine($"  {Lbl(L("Conversion", ""), BuildMeterBar(conversionNormalized, C.Green))}  {Clr(C.Dim, "|")}  {Clr(C.White, BuildConversionDescriptor(r))}");
        sb.Append($"  {Lbl(L("Stability", ""), BuildMeterBar(stabilityNormalized, C.Sky))}  {Clr(C.Dim, "|")}  {Clr(C.White, BuildStabilityDescriptor(r))}");
        return sb.ToString();
    }

    private float EstimateConversionLevel(ReactionEvaluationResult r)
    {
        if (!r.IsValid || r.MediumMismatch)
            return 0f;

        if (r.Status == ReactionStatus.Success)
            return Mathf.Clamp01(0.82f + r.Rate01 * 0.18f);

        if (r.Status == ReactionStatus.Partial)
            return Mathf.Clamp01(0.38f + r.Rate01 * 0.35f);

        if (r.ActivationNotReached)
            return 0.08f;

        return Mathf.Clamp01(r.Rate01 * 0.2f);
    }

    private float EstimateStabilityLevel(ReactionEvaluationResult r)
    {
        if (!r.IsValid)
            return 0.15f;

        float value = 0.35f;
        if (!r.MediumMismatch) value += 0.2f;
        if (!r.ActivationNotReached || DidReact(r)) value += 0.2f;
        if (!r.LowContactQuality) value += 0.15f;
        if (r.Status == ReactionStatus.Success) value += 0.1f;
        return Mathf.Clamp01(value);
    }

    private string BuildConversionDescriptor(ReactionEvaluationResult r)
    {
        if (!r.IsValid)
            return L("Not resolved", "");
        if (r.MediumMismatch)
            return L("Suppressed", "");
        if (r.ActivationNotReached && !DidReact(r))
            return L("Dormant", "");
        if (r.Status == ReactionStatus.Partial)
            return L("Partial yield", "");
        if (r.Status == ReactionStatus.Success)
            return L("High yield", "");
        return L("Low yield", "");
    }

    private string BuildStabilityDescriptor(ReactionEvaluationResult r)
    {
        if (!r.IsValid)
            return L("Unverified", "");
        if (r.MediumMismatch)
            return L("Destabilized", "");
        if (r.LowContactQuality && r.Status != ReactionStatus.Success)
            return L("Needs better mixing", "");
        if (r.Status == ReactionStatus.Success)
            return L("Stable progression", "");
        return L("Monitor closely", "");
    }

    // -- End Achievements --------------------------------------

    private string BuildQuizQuestion(ReactionEvaluationResult r, ReactionEvaluationInput input)
    {
        string icon = Clr(C.Purple, B("?"));

        if (!r.IsValid)
            return $"  {icon} {Clr(C.White, L("Why does this selected reactant set not form a valid reaction?", ""))}";

        if (r.MediumMismatch)
            return $"  {icon} {Clr(C.White, L("Why was the selected medium unsuitable for this reaction?", ""))}";

        if (r.ActivationNotReached && !DidReact(r))
            return $"  {icon} {Clr(C.White, L("What condition must increase for this reaction to start?", ""))}";

        if (r.CatalystApplied && DidReact(r))
            return $"  {icon} {Clr(C.White, L("What role did the catalyst play in this reaction?", ""))}";

        if (r.LowContactQuality)
            return $"  {icon} {Clr(C.White, L("How would better grinding or stirring affect the yield?", ""))}";

        if (r.Status == ReactionStatus.Partial)
            return $"  {icon} {Clr(C.White, L("What single change would push this to a complete reaction?", ""))}";

        if (DidReact(r))
            return $"  {icon} {Clr(C.White, L("Which factors made this reaction succeed?", ""))}";

        return $"  {icon} {Clr(C.White, L("What conditions would help this reaction proceed?", ""))}";
    }

    private string BuildReactionIdentity(ReactionEntry reaction)
    {
        if (reaction == null)
            return string.Empty;

        List<string> reactants = reaction.GetReactantFormulas();
        List<string> products = reaction.GetProductFormulas();

        if (reactants.Count < 2)
            return string.Empty;

        string label = !string.IsNullOrWhiteSpace(reaction.name_en)
            ? reaction.name_en
            : "Reaction";

        string equation = products.Count == 0
            ? string.Join(" + ", reactants)
            : $"{string.Join(" + ", reactants)}  ->  {string.Join(" + ", products)}";

        string medium = !string.IsNullOrWhiteSpace(reaction.requiredMedium)
            ? LocalizeMediumName(reaction.requiredMedium)
            : L("Any", "");

        string gas = reaction.GetProducesGas() ? Clr(C.Orange, L("Yes", "")) : Clr(C.Gray, L("No", ""));

        var sb = new StringBuilder();
        sb.AppendLine(Sz(140, Clr(C.Gold, B($"Lab: {label}"))));
        sb.AppendLine(Sz(120, Clr(C.Sky, B(equation))));
        sb.Append($"  {Lbl(L("Medium", ""), Clr(C.White, medium))}  {Clr(C.Dim, "|")}  {Lbl(L("Act. Temp", ""), Clr(C.White, $"{reaction.activationTempC:F0}°C"))}  {Clr(C.Dim, "|")}  {Lbl(L("Gas", ""), gas)}");
        return sb.ToString();
    }

    private string BuildCausalScientificExplanation(ReactionEvaluationResult r, ReactionEvaluationInput input)
    {
        ReactionEntry rx = input.reaction;
        string mediumLabel = LocalizeMediumName(ReactionEvaluator.MediumLabel(input.medium));

        // --- Invalid reactant set ---
        if (!r.IsValid)
        {
            string reactantSet = rx != null
                ? FormatReagentList(rx.GetReactantFormulas())
                : L("the selected set", "");
            return L(
                $"The selected reactant set ({reactantSet}) does not match a valid reaction in the database.",
                "");
        }

        // --- Medium mismatch ---
        if (r.MediumMismatch)
        {
            string required = rx != null && !string.IsNullOrWhiteSpace(rx.requiredMedium)
                ? LocalizeMediumName(rx.requiredMedium)
                : L("unknown", "");
            return L($"The selected medium ({mediumLabel}) does not match the required medium ({required}) for this reaction.", "");
        }

        // --- Activation not reached (no reaction) ---
        if (r.ActivationNotReached && !DidReact(r))
        {
            var sb = new StringBuilder();
            sb.Append(L($"The set temperature ({input.temperatureC:F0}°C) did not reach the activation threshold ({r.ActivationThresholdC:F0}°C).", ""));
            if (r.CatalystApplied)
                sb.Append(L(" A catalyst was applied but was insufficient to overcome the energy barrier.", ""));
            else if (rx != null && rx.catalystAllowed)
                sb.Append(L(" Consider using a catalyst to lower the activation energy.", ""));
            if (r.LowContactQuality)
                sb.Append(L($" Contact quality is also low ({r.ContactFactor:F2}); increase stirring or grinding.", ""));
            return sb.ToString();
        }

        // --- Partial reaction ---
        if (r.Status == ReactionStatus.Partial)
            return BuildPartialCausalExplanation(r, input);

        // --- Success ---
        if (r.Status == ReactionStatus.Success)
            return BuildSuccessCausalExplanation(r, input);

        return L("The reaction did not proceed under the current conditions.", "");
    }

    private string BuildPartialCausalExplanation(ReactionEvaluationResult r, ReactionEvaluationInput input)
    {
        var sb = new StringBuilder(L("Partial reaction:", ""));

        if (r.ActivationNotReached && r.LowTemperature)
            sb.Append(L($" temperature ({input.temperatureC:F0}°C) is near but below the threshold ({r.ActivationThresholdC:F0}°C), allowing only a weak reaction.", ""));
        else if (r.LowContactQuality)
            sb.Append(L($" activation energy was met, but poor reagent contact ({r.ContactFactor:F2}) limited the yield (stirring: {input.stirring01:P0}, grinding: {input.grinding01:P0}).", ""));

        if (r.CatalystApplied)
            sb.Append(L(" The catalyst helped lower the energy barrier without being consumed.", ""));

        if (r.LowContactQuality && !r.LowTemperature)
            sb.Append(L(" Increasing stirring or grinding would improve reagent contact.", ""));

        if (r.LowTemperature)
            sb.Append(L(" Raising the temperature slightly should allow a full reaction.", ""));

        sb.Append(L($" Rate: {r.Rate01:P0}.", ""));
        return sb.ToString();
    }

    private string BuildSuccessCausalExplanation(ReactionEvaluationResult r, ReactionEvaluationInput input)
    {
        string mediumLabel = LocalizeMediumName(ReactionEvaluator.MediumLabel(input.medium));
        var sb = new StringBuilder();

        sb.Append(L($"All conditions were met: medium ({mediumLabel}), temperature ({input.temperatureC:F0}°C >= {r.ActivationThresholdC:F0}°C)", ""));
        sb.Append(r.ContactFactor >= 1.2f
            ? L($", and excellent reagent contact ({r.ContactFactor:F2}).", "")
            : L($", and adequate reagent contact ({r.ContactFactor:F2}).", ""));

        if (r.CatalystApplied)
        {
            float originalTemp = input.reaction != null ? input.reaction.activationTempC : r.ActivationThresholdC;
            sb.Append(L($" A catalyst lowered the activation energy from {originalTemp:F0}°C to {r.ActivationThresholdC:F0}°C without being consumed.", ""));
        }

        if (r.LowContactQuality)
            sb.Append(L($" However, contact quality ({r.ContactFactor:F2}) is not optimal; increasing stirring ({input.stirring01:P0}) or grinding ({input.grinding01:P0}) may improve yield.", ""));

        if (r.Rate01 >= 0.8f)
            sb.Append(L($" Reaction rate is high ({r.Rate01:P0}), indicating optimal conditions.", ""));
        else if (r.Rate01 >= 0.4f)
            sb.Append(L($" Reaction rate is moderate ({r.Rate01:P0}); fine-tuning stirring or grinding could improve it.", ""));
        else
            sb.Append(L($" Reaction rate is low ({r.Rate01:P0}); conditions are borderline.", ""));

        return sb.ToString();
    }

    private string BuildResultHeadline(ReactionEvaluationResult r)
    {
        if (!r.IsValid)
            return Sz(135, Clr(C.Orange, B(L("[!] Invalid reaction setup", ""))));

        if (r.MediumMismatch)
            return Sz(135, Clr(C.Red, B(L("[X] No reaction -- medium mismatch", ""))));

        if (r.ActivationNotReached)
            return Sz(135, Clr(C.Red, B(L("[X] No reaction -- activation not reached", ""))));

        if (r.Status == ReactionStatus.Success)
            return Sz(135, Clr(C.Green, B(L("[OK] Reaction Successful!", ""))));

        if (r.Status == ReactionStatus.Partial)
            return Sz(135, Clr(C.Orange, B(L("[~] Partial Reaction", ""))));

        return Sz(135, Clr(C.Red, B(L("[X] No reaction -- activation not reached", ""))));
    }

    private bool DidReact(ReactionEvaluationResult eval)
    {
        return eval.Status == ReactionStatus.Success || eval.Status == ReactionStatus.Partial;
    }

    private bool IsSuccessLike(ReactionEvaluationResult eval)
    {
        return eval.Status == ReactionStatus.Success || eval.Status == ReactionStatus.Partial;
    }

    private string BuildHistoryOutcome(ReactionEvaluationResult r)
    {
        if (!r.IsValid)
            return "invalid";

        if (r.MediumMismatch)
            return "medium_mismatch";

        if (r.ActivationNotReached)
            return "activation_not_reached";

        if (DidReact(r))
            return "success";

        return "invalid";
    }

    private void AddHistoryEntry(IEnumerable<string> selectedReagents, int mediumIndex, string outcomeKey)
    {
        string reagentSummary = FormatReagentList(selectedReagents);
        experimentHistory.Add(new ExperimentHistoryEntry
        {
            ReagentSummary = string.IsNullOrWhiteSpace(reagentSummary) ? L("N/A", "") : reagentSummary,
            MediumIndex = mediumIndex,
            OutcomeKey = string.IsNullOrWhiteSpace(outcomeKey) ? "invalid" : outcomeKey
        });

        while (experimentHistory.Count > MaxHistoryEntries)
        {
            experimentHistory.RemoveAt(0);
        }

        string history = BuildHistoryText();
        if (historyText != null)
        {
            historyText.richText = true;
            historyText.text = history;
        }
        else
        {
            DevLog($"LabController history (last {MaxHistoryEntries}):\n{history}");
        }
    }

    private string BuildHistoryText()
    {
        if (experimentHistory.Count == 0)
            return Clr(C.Gray, L("No experiments yet.", ""));

        var builder = new StringBuilder();
        builder.AppendLine(Clr(C.Gold, B(L("Recent Experiments", ""))));
        builder.AppendLine(ThinDivider());

        int lineNumber = 1;
        for (int i = experimentHistory.Count - 1; i >= 0; i--)
        {
            ExperimentHistoryEntry entry = experimentHistory[i];
            string localizedOutcome = LocalizeOutcome(entry.OutcomeKey);
            string outcomeColor = entry.OutcomeKey == "success" ? C.Green
                                : entry.OutcomeKey == "invalid" ? C.Orange
                                : C.Red;

            builder.Append(Clr(C.Dim, $"{lineNumber}) "))
                .Append(Clr(C.White, entry.ReagentSummary))
                .Append(Clr(C.Dim, " | "))
                .Append(Clr(C.Sky, GetMediumLabelForIndex(entry.MediumIndex)))
                .Append(Clr(C.Dim, " | "))
                .AppendLine(Clr(outcomeColor, localizedOutcome));
            lineNumber++;
        }

        return builder.ToString().TrimEnd();
    }

    private string GetSelectedDropdownText(TMP_Dropdown dropdown)
    {
        if (dropdown == null || dropdown.options == null || dropdown.value < 0 || dropdown.value >= dropdown.options.Count)
            return string.Empty;

        string text = dropdown.options[dropdown.value].text;
        return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
    }

    private string LocalizeOutcome(string outcomeKey)
    {
        return outcomeKey switch
        {
            "success" => L("Success", ""),
            "activation_not_reached" => L("Activation not reached", ""),
            "medium_mismatch" => L("Medium mismatch", ""),
            _ => L("Invalid", "")
        };
    }

    private string LocalizeLessonTitle(string title)
    {
        return title switch
        {
            "Basic Reactions" => L("Basic Reactions", ""),
            "Medium and Temperature" => L("Medium and Temperature", ""),
            "Catalyst and Contact" => L("Catalyst and Contact", ""),
            "Advanced Reaction Conditions" => L("Advanced Reaction Conditions", ""),
            _ => title
        };
    }

    private string LocalizeChallengeTitle(string title)
    {
        return title switch
        {
            "Complete a successful reaction without catalyst" => L(title, ""),
            "Use the correct medium in one attempt" => L(title, ""),
            "Reach a strong contact factor (>=1.2)" => L(title, ""),
            "Complete two successful reactions in a row" => L(title, ""),
            "Complete a successful reaction" => L(title, ""),
            _ => title
        };
    }

    private string LocalizeObjectiveTitle(string title)
    {
        return title switch
        {
            "Perform one valid successful reaction." => L(title, ""),
            "Complete a reaction using the correct medium." => L(title, ""),
            "Complete a reaction with strong contact or proper catalyst use." => L(title, ""),
            "Complete an advanced successful reaction under correct conditions." => L(title, ""),
            _ => title
        };
    }

    private string LocalizeAchievementTitle(string title)
    {
        return title switch
        {
            "First Successful Reaction" => L(title, ""),
            "Reach Level 2" => L(title, ""),
            "Complete 5 Experiments" => L(title, ""),
            "Complete First Challenge" => L(title, ""),
            "Reach 100 Score" => L(title, ""),
            "Use Catalyst Correctly" => L(title, ""),
            _ => title
        };
    }

    private string LocalizeMediumName(string medium)
    {
        if (string.IsNullOrWhiteSpace(medium))
            return L("Any", "");

        switch (medium.Trim().ToLowerInvariant())
        {
            case "neutral":
                return L("Neutral", "");
            case "acidic":
            case "acid":
                return L("Acidic", "");
            case "basic":
            case "base":
            case "alkaline":
                return L("Basic", "");
            default:
                return medium.Trim();
        }
    }

    private void ApplyResultColor(ReactionEvaluationResult r)
    {
        if (resultText == null)
        {
            Debug.LogError("LabController: resultText reference is missing.");
            return;
        }

        if (!r.IsValid)
        {
            resultText.color = Color.yellow;
            TriggerResultVisualFeedback(false);
            return;
        }

        if (r.MediumMismatch || r.ActivationNotReached)
        {
            resultText.color = Color.red;
            TriggerResultVisualFeedback(false);
            return;
        }

        if (DidReact(r))
        {
            resultText.color = Color.green;
            TriggerResultVisualFeedback(true);
            return;
        }

        TriggerResultVisualFeedback(false);
    }

    private void TriggerResultVisualFeedback(bool isSuccess)
    {
        if (resultText == null)
        {
            Debug.LogError("LabController: resultText reference is missing.");
            return;
        }

        if (resultFeedbackRoutine != null)
            StopCoroutine(resultFeedbackRoutine);

        resultFeedbackRoutine = StartCoroutine(PlayResultFeedback(isSuccess));
    }

    private System.Collections.IEnumerator PlayResultFeedback(bool isSuccess)
    {
        if (resultText == null)
        {
            resultFeedbackRoutine = null;
            yield break;
        }

        Color baseColor = resultText.color;
        float targetAlpha = isSuccess ? SuccessAlphaBoost : FailureAlphaBoost;
        Color highlighted = new Color(baseColor.r, baseColor.g, baseColor.b, targetAlpha);

        resultText.color = highlighted;
        yield return new WaitForSeconds(ResultFeedbackDuration);

        if (resultText != null)
            resultText.color = baseColor;

        resultFeedbackRoutine = null;
    }

    private void SetResult(string msg)
    {
        string finalMessage = string.IsNullOrWhiteSpace(msg)
            ? L("Result unavailable.", "")
            : msg;

        if (resultText != null)
        {
            resultText.text = finalMessage;
        }
        else
        {
            Debug.LogError("LabController: resultText reference is missing.");
        }

        // Scroll to top so new result is visible
        if (resultScrollRect != null)
            resultScrollRect.verticalNormalizedPosition = 1f;

        UpdateHudStatus();
        RefreshReactionDashboard();
        DevLog(finalMessage);
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    private void LogEvaluationDetails(ReactionEvaluationResult eval)
    {
        if (eval.DetailedReasons == null || eval.DetailedReasons.Count == 0)
        {
            Debug.Log("LabController: No detailed reasons provided by evaluator.");
            return;
        }

        for (int i = 0; i < eval.DetailedReasons.Count; i++)
        {
            string reason = eval.DetailedReasons[i];
            if (!string.IsNullOrWhiteSpace(reason))
            {
                Debug.Log($"Reason #{i + 1}: {reason}");
            }
        }
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    private void DevLog(string message)
    {
        Debug.Log(message);
    }

    // -- Dynamic Slider Value Labels --------------------------

    private void CreateSliderValueLabels()
    {
        DestroyExistingSliderLabel(temperatureSlider);
        DestroyExistingSliderLabel(stirringSlider);
        DestroyExistingSliderLabel(grindingSlider);

        tempValueLabel  = CreateValueLabel(temperatureSlider, FormatTempValue(temperatureSlider.value));
        stirValueLabel  = CreateValueLabel(stirringSlider,    FormatPercentValue(stirringSlider.value));
        grindValueLabel = CreateValueLabel(grindingSlider,    FormatPercentValue(grindingSlider.value));
    }

    private void DestroyExistingSliderLabel(Slider slider)
    {
        if (slider == null) return;
        Transform existing = slider.transform.Find(SliderLabelName);
        if (existing != null)
            Destroy(existing.gameObject);
    }

    private TextMeshProUGUI CreateValueLabel(Slider slider, string initialText)
    {
        if (slider == null) return null;

        var go = new GameObject(SliderLabelName, typeof(RectTransform));
        go.transform.SetParent(slider.transform, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = initialText;
        tmp.fontSize = 28;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.84f, 0.28f);
        tmp.raycastTarget = false;
        tmp.richText = true;
        tmp.enableAutoSizing = false;

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 5f);
        rect.sizeDelta = new Vector2(200f, 36f);

        return tmp;
    }

    private void UpdateSliderLabels()
    {
        if (tempValueLabel  != null && temperatureSlider != null)
            tempValueLabel.text  = FormatTempValue(temperatureSlider.value);
        if (stirValueLabel  != null && stirringSlider != null)
            stirValueLabel.text  = FormatPercentValue(stirringSlider.value);
        if (grindValueLabel != null && grindingSlider != null)
            grindValueLabel.text = FormatPercentValue(grindingSlider.value);
    }

    private static string FormatTempValue(float v)    => $"<color=#FFD54F>{v:F0}°C</color>";
    private static string FormatPercentValue(float v) => $"<color=#90CAF9>{v:P0}</color>";

    // -- End Dynamic Slider Value Labels -----------------------

    // -- Scrollable Result Panel -------------------------------

    private void SetupScrollableResult()
    {
        if (resultText == null) return;

        // Guard: don't wrap twice
        if (resultScrollRect != null) return;

        RectTransform textRect = resultText.rectTransform;
        Transform originalParent = textRect.parent;
        int siblingIndex = textRect.GetSiblingIndex();

        // Save the original rect state
        Vector2 origAnchorMin = textRect.anchorMin;
        Vector2 origAnchorMax = textRect.anchorMax;
        Vector2 origPivot     = textRect.pivot;
        Vector2 origPos       = textRect.anchoredPosition;
        Vector2 origSize      = textRect.sizeDelta;

        // Expand viewport to usable size (min 680×480)
        float vpW = Mathf.Max(origSize.x, 680f);
        float vpH = Mathf.Max(origSize.y, 480f);

        // -- Viewport: dark semi-transparent panel with rect mask --
        var vpGO   = new GameObject("_ResultScroll",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
        var vpRect = vpGO.GetComponent<RectTransform>();
        vpRect.SetParent(originalParent, false);
        vpRect.SetSiblingIndex(siblingIndex);
        vpRect.anchorMin        = origAnchorMin;
        vpRect.anchorMax        = origAnchorMax;
        vpRect.pivot            = origPivot;
        vpRect.anchoredPosition = origPos;
        vpRect.sizeDelta        = new Vector2(vpW, vpH);

        var vpImage = vpGO.GetComponent<Image>();
        vpImage.color         = new Color(0.03f, 0.05f, 0.09f, 0.92f);
        vpImage.raycastTarget = true;
        RegisterAnimatedGlow(vpImage, 0.04f, 0.6f, 0.012f);

        // -- Reparent text into viewport --
        textRect.SetParent(vpRect, false);
        textRect.anchorMin        = new Vector2(0f, 1f);
        textRect.anchorMax        = new Vector2(1f, 1f);
        textRect.pivot            = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta        = new Vector2(-28f, 0f);  // 14px side padding

        // -- ContentSizeFitter: text grows vertically with content --
        var fitter = resultText.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // -- ScrollRect: vertical scroll, elastic --
        resultScrollRect = vpGO.AddComponent<ScrollRect>();
        resultScrollRect.content            = textRect;
        resultScrollRect.viewport           = vpRect;
        resultScrollRect.horizontal         = false;
        resultScrollRect.vertical           = true;
        resultScrollRect.movementType       = ScrollRect.MovementType.Elastic;
        resultScrollRect.elasticity         = 0.1f;
        resultScrollRect.scrollSensitivity  = 35f;
        resultScrollRect.inertia            = true;
        resultScrollRect.decelerationRate   = 0.135f;

        // -- Text settings for structured output --
        resultText.alignment    = TextAlignmentOptions.TopLeft;
        resultText.overflowMode = TextOverflowModes.Overflow;
        resultText.margin       = new Vector4(14f, 14f, 14f, 14f);
        resultText.enableAutoSizing = false;
        resultText.fontSize     = Mathf.Max(resultText.fontSize, 25f);
    }

    // -- End Scrollable Result Panel ---------------------------

    // -- Runtime History Panel (created when historyText is not wired in Inspector) --

    private void CreateHistoryPanelIfNeeded()
    {
        if (historyText != null) return;
        if (resultText == null) return;

        // Place the history panel to the right of the result scroll panel
        Transform resultParent = resultText.rectTransform.parent;
        // If result was wrapped in _ResultScroll, use its parent (the Canvas)
        Transform canvasParent = resultScrollRect != null
            ? resultScrollRect.GetComponent<RectTransform>().parent
            : resultParent;

        if (canvasParent == null) return;

        // -- Container with dark background --
        var panelGO = new GameObject("_HistoryPanel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.SetParent(canvasParent, false);

        // Position: top-right area of the canvas
        panelRect.anchorMin        = new Vector2(1f, 1f);
        panelRect.anchorMax        = new Vector2(1f, 1f);
        panelRect.pivot            = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-20f, -20f);
        panelRect.sizeDelta        = new Vector2(420f, 300f);

        var panelImage = panelGO.GetComponent<Image>();
        panelImage.color         = new Color(0.04f, 0.06f, 0.11f, 0.88f);
        panelImage.raycastTarget = false;
        RegisterAnimatedGlow(panelImage, 0.04f, 0.65f, 0.015f);

        // -- TMP text child --
        var textGO = new GameObject("_HistoryText", typeof(RectTransform));
        textGO.transform.SetParent(panelRect, false);

        historyText = textGO.AddComponent<TextMeshProUGUI>();
        historyText.fontSize          = 22;
        historyText.alignment         = TextAlignmentOptions.TopLeft;
        historyText.color             = Color.white;
        historyText.richText          = true;
        historyText.enableAutoSizing  = false;
        historyText.overflowMode      = TextOverflowModes.Truncate;
        historyText.raycastTarget     = false;
        historyText.margin            = new Vector4(6f, 6f, 6f, 6f);

        var textRect = historyText.rectTransform;
        textRect.anchorMin        = Vector2.zero;
        textRect.anchorMax        = Vector2.one;
        textRect.offsetMin        = Vector2.zero;
        textRect.offsetMax        = Vector2.zero;

        historyText.text = Clr(C.Gray, L("No experiments yet.", ""));
    }

    // -- End Runtime History Panel ------------------------------

    // -- Visual Reaction FX ------------------------------------

    private void PlayReactionFx(ReactionEvaluationResult eval, ReactionEvaluationInput input)
    {
        StopAllReactionFx();

        if (!eval.IsValid)
        {
            if (failFx != null) failFx.Play();
            return;
        }

        bool reacted = DidReact(eval);

        if (reacted)
        {
            if (successFx != null) successFx.Play();
        }
        else
        {
            if (failFx != null) failFx.Play();
        }

        // Gas-producing reaction
        if (reacted && input.reaction != null && input.reaction.GetProducesGas())
        {
            if (gasFx != null) gasFx.Play();
        }

        // Catalyst applied
        if (eval.CatalystApplied && reacted)
        {
            if (catalystFx != null) catalystFx.Play();
        }

        // High temperature or activation reached
        if (!eval.ActivationNotReached && reacted)
        {
            if (heatFx != null) heatFx.Play();
        }

        if (reacted && input.reaction != null && input.reaction.visual_effects != null && input.reaction.visual_effects.precipitate)
        {
            if (precipitateFx != null) precipitateFx.Play();
        }
    }

    private void StopAllReactionFx()
    {
        if (gasFx != null && gasFx.isPlaying)         gasFx.Stop();
        if (successFx != null && successFx.isPlaying)  successFx.Stop();
        if (failFx != null && failFx.isPlaying)        failFx.Stop();
        if (catalystFx != null && catalystFx.isPlaying) catalystFx.Stop();
        if (heatFx != null && heatFx.isPlaying)        heatFx.Stop();
        if (precipitateFx != null && precipitateFx.isPlaying) precipitateFx.Stop();
    }

    // -- End Visual Reaction FX --------------------------------
}
