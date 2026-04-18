// ChemLabSim v3 — LabV3 Scene Setup (Editor Tool)
// Creates Assets/_ProjectV3/Scenes/LabV3.unity with all v3 GameObjects pre-wired.
// Run from menu: ChemLabSim V3 → Create LabV3 Scene
//
// This is an editor-only tool. It generates:
// - V3Bootstrap (DontDestroyOnLoad)
// - All v3 controllers on "V3Controllers" GameObject (including LabInputController)
// - Canvas with output views (ReactionResult, Progress, Guidance, Challenge, Objective)
// - Lab Input Panel with real controls (4 reagent dropdowns, medium, temp, stirring, grinding, catalyst, mix button)
// - EventSystem

#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using ChemLabSimV3.Core;
using ChemLabSimV3.Controllers;
using ChemLabSimV3.Views;

namespace ChemLabSimV3.Editor
{
    public static class LabV3SceneSetup
    {
        [MenuItem("ChemLabSim V3/Create LabV3 Scene")]
        public static void CreateLabV3Scene()
        {
            // 1) Create new empty scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 2) V3Bootstrap
            var bootstrapGo = new GameObject("V3Bootstrap");
            bootstrapGo.AddComponent<V3Bootstrap>();

            // 3) Controllers container
            var controllersGo = new GameObject("V3Controllers");
            var reactionCtrl    = controllersGo.AddComponent<ReactionController>();
            var progressCtrl    = controllersGo.AddComponent<ProgressController>();
            var uiCtrl          = controllersGo.AddComponent<UIController>();
            var achievementCtrl = controllersGo.AddComponent<AchievementController>();
            var challengeCtrl   = controllersGo.AddComponent<ChallengeController>();
            var objectiveCtrl   = controllersGo.AddComponent<ObjectiveController>();
            var labInputCtrl    = controllersGo.AddComponent<LabInputController>();
            var notebookCtrl    = controllersGo.AddComponent<NotebookController>();
            controllersGo.AddComponent<QuizController>();
            controllersGo.AddComponent<FXController>();
            controllersGo.AddComponent<GuidanceController>();

            // 3b) ReactionFxView — non-UI particle view, lives outside Canvas
            var fxViewGo = new GameObject("ReactionFxView");
            fxViewGo.AddComponent<ReactionFxView>();

            // 4) Canvas
            var canvasGo = new GameObject("V3Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // -- Output Views ----------------------------------
            // ReactionResult: centered horizontally (clear of left input panel and right progress panel)
            var reactionResultView = CreateViewPanel<ReactionResultView>(canvasGo.transform, "ReactionResultPanel",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(520, 220),
                "headlineText", "explanationText", "reactantsText", "productsText");
            AddPanelTitle(reactionResultView.gameObject, "Reaction Result");

            // ReactionIdentity: below ReactionResult
            var reactionIdentityView = CreateViewPanel<ReactionIdentityView>(canvasGo.transform, "ReactionIdentityPanel",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -250), new Vector2(520, 100),
                "reactionNameText", "equationText", "conditionsText");
            AddPanelTitle(reactionIdentityView.gameObject, "Reaction Identity");
            reactionIdentityView.gameObject.SetActive(false);

            // ReactionDetails: below ReactionIdentity
            var reactionDetailsView = CreateViewPanel<ReactionDetailsView>(canvasGo.transform, "ReactionDetailsPanel",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -360), new Vector2(520, 160),
                "mediumStatusText", "temperatureStatusText", "contactStatusText", "catalystStatusText", "rateText");
            AddPanelTitle(reactionDetailsView.gameObject, "Reaction Details");
            reactionDetailsView.gameObject.SetActive(false);

            // ScientificExplanation: below ReactionDetails
            var scientificExplanationView = CreateViewPanel<ScientificExplanationView>(canvasGo.transform, "ScientificExplanationPanel",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -530), new Vector2(520, 90),
                "explanationText");
            AddPanelTitle(scientificExplanationView.gameObject, "Scientific Explanation");
            scientificExplanationView.gameObject.SetActive(false);

            // SafetyNote: below ScientificExplanation
            var safetyNoteView = CreateViewPanel<SafetyNoteView>(canvasGo.transform, "SafetyNotePanel",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -630), new Vector2(520, 80),
                "ghsCodesText", "warningsText");
            AddPanelTitle(safetyNoteView.gameObject, "Safety Note");
            safetyNoteView.gameObject.SetActive(false);

            // QuizHint: below SafetyNote
            var quizHintView = CreateViewPanel<QuizHintView>(canvasGo.transform, "QuizHintPanel",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -720), new Vector2(520, 64),
                "questionText");
            AddPanelTitle(quizHintView.gameObject, "Think About It");
            quizHintView.gameObject.SetActive(false);

            // Progress: top-right
            var progressView = CreateViewPanel<ProgressView>(canvasGo.transform, "ProgressPanel",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20, -20), new Vector2(360, 240),
                "scoreText", "scoreDeltaText", "levelText", "lessonTitleText", "experimentsText", "levelUpText");
            AddPanelTitle(progressView.gameObject, "Progress");

            // Guidance: bottom-center
            var guidanceView = CreateViewPanel<GuidanceView>(canvasGo.transform, "GuidancePanel",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(620, 64),
                "hintText");
            AddPanelTitle(guidanceView.gameObject, "Hint");

            // Challenge: bottom-left (above guidance)
            var challengeView = CreateViewPanel<ChallengeView>(canvasGo.transform, "ChallengePanel",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20, 96), new Vector2(420, 90),
                "challengeTitleText", "challengeStatusText");
            AddPanelTitle(challengeView.gameObject, "Challenge");

            // Objective: bottom-right (above guidance)
            var objectiveView = CreateViewPanel<ObjectiveView>(canvasGo.transform, "ObjectivePanel",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20, 96), new Vector2(420, 90),
                "objectiveTitleText", "objectiveStatusText");
            AddPanelTitle(objectiveView.gameObject, "Objective");

            // AchievementToast: top-center, below QuizHint — starts hidden
            var achievementToastView = CreateViewPanel<AchievementToastView>(canvasGo.transform, "AchievementToastPanel",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -794), new Vector2(400, 70),
                "titleText", "nameText");
            achievementToastView.gameObject.SetActive(false);

            // QuizPanel: interactive quiz with answer buttons — below AchievementToast
            var quizPanelView = CreateQuizPanel(canvasGo.transform);
            quizPanelView.gameObject.SetActive(false);

            // -- Wire UIController → Output Views --------------
            WireUIControllerViews(uiCtrl, reactionResultView, progressView, guidanceView, challengeView, objectiveView, achievementToastView,
                reactionIdentityView, reactionDetailsView, scientificExplanationView, safetyNoteView, quizHintView, quizPanelView);

            // Notebook: right side, below Progress panel
            var notebookView = CreateNotebookPanel(canvasGo.transform);
            WireNotebookController(notebookCtrl, notebookView);

            // -- Lab Input Panel (left side) -------------------
            var inputPanelGo = CreateInputPanel(canvasGo.transform);
            AddPanelTitle(inputPanelGo, "Lab Controls");

            // Section: Reagents
            CreateSectionHeader(inputPanelGo.transform, "Reagents");
            var reagentA = CreateDropdownControl<ReagentDropdownView>(inputPanelGo.transform, "ReagentA", "Reagent A");
            var reagentB = CreateDropdownControl<ReagentDropdownView>(inputPanelGo.transform, "ReagentB", "Reagent B");
            var reagentC = CreateDropdownControl<ReagentDropdownView>(inputPanelGo.transform, "ReagentC", "Reagent C (opt.)");
            var reagentD = CreateDropdownControl<ReagentDropdownView>(inputPanelGo.transform, "ReagentD", "Reagent D (opt.)");

            // Section: Conditions
            CreateDivider(inputPanelGo.transform);
            CreateSectionHeader(inputPanelGo.transform, "Conditions");
            var medium   = CreateDropdownControl<MediumDropdownView>(inputPanelGo.transform, "Medium", "Medium");
            var tempSlider     = CreateSliderControl<TemperatureSliderView>(inputPanelGo.transform, "Temperature", "Temperature \u00b0C");
            var stirringSlider = CreateSliderControl<StirringSliderView>(inputPanelGo.transform, "Stirring", "Stirring");
            var grindingSlider = CreateSliderControl<GrindingSliderView>(inputPanelGo.transform, "Grinding", "Grinding");
            var catalystToggle = CreateToggleControl(inputPanelGo.transform, "Catalyst", "Use Catalyst");

            // Section: Action
            CreateDivider(inputPanelGo.transform);
            var mixButton = CreateMixButton(inputPanelGo.transform);

            // -- Wire LabInputController → Input Views + ReactionController --
            WireLabInputController(labInputCtrl, reactionCtrl,
                reagentA, reagentB, reagentC, reagentD,
                medium, tempSlider, stirringSlider, grindingSlider,
                catalystToggle, mixButton);

            // 8) EventSystem
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventSystemGo = new GameObject("EventSystem");
                eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // 9) Save scene
            string scenePath = "Assets/_ProjectV3/Scenes/LabV3.unity";
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(
                System.IO.Path.Combine(Application.dataPath, "../", scenePath)));
            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log($"[LabV3SceneSetup] Scene created and saved: {scenePath}");
            EditorUtility.DisplayDialog("LabV3 Scene Created",
                $"Scene saved to:\n{scenePath}\n\nHierarchy:\n" +
                "- V3Bootstrap\n- V3Controllers (11 controllers)\n" +
                "- ReactionFxView (particle FX)\n" +
                "- V3Canvas (11 output panels incl. interactive QuizPanel + Lab Input Panel with 10 controls)\n- EventSystem",
                "OK");
        }

        // -- Helpers -------------------------------------------

        private static T CreateViewPanel<T>(Transform parent, string panelName,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta,
            params string[] textFieldNames) where T : V3ViewBase
        {
            var panelGo = new GameObject(panelName);
            panelGo.transform.SetParent(parent, false);

            var rect = panelGo.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            // Background
            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);

            // Subtle outline (consistent with input panel)
            var outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.5f, 0.7f, 0.25f);
            outline.effectDistance = new Vector2(1, -1);

            // Add view component
            var view = panelGo.AddComponent<T>();

            // Add vertical layout
            var layout = panelGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 10);
            layout.spacing = 4;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // Create TMP text children and wire them
            var so = new SerializedObject(view);
            foreach (string fieldName in textFieldNames)
            {
                var textGo = new GameObject(fieldName);
                textGo.transform.SetParent(panelGo.transform, false);

                var textRect = textGo.AddComponent<RectTransform>();
                textRect.sizeDelta = new Vector2(0, 28);

                var textElem = textGo.AddComponent<LayoutElement>();
                textElem.preferredHeight = 28;

                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = 16;
                tmp.color = Color.white;
                tmp.text = "\u2014"; // em-dash placeholder (replaced at runtime by controller)

                // Wire the SerializedField
                var prop = so.FindProperty(fieldName);
                if (prop != null)
                {
                    prop.objectReferenceValue = tmp;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static void WireUIControllerViews(UIController uiCtrl,
            ReactionResultView reactionView,
            ProgressView progressView,
            GuidanceView guidanceView,
            ChallengeView challengeView,
            ObjectiveView objectiveView,
            AchievementToastView achievementToastView,
            ReactionIdentityView reactionIdentityView,
            ReactionDetailsView reactionDetailsView,
            ScientificExplanationView scientificExplanationView,
            SafetyNoteView safetyNoteView,
            QuizHintView quizHintView,
            QuizPanelView quizPanelView)
        {
            var so = new SerializedObject(uiCtrl);
            SetRef(so, "reactionResultView", reactionView);
            SetRef(so, "progressView", progressView);
            SetRef(so, "guidanceView", guidanceView);
            SetRef(so, "challengeView", challengeView);
            SetRef(so, "objectiveView", objectiveView);
            SetRef(so, "achievementToastView", achievementToastView);
            SetRef(so, "reactionIdentityView", reactionIdentityView);
            SetRef(so, "reactionDetailsView", reactionDetailsView);
            SetRef(so, "scientificExplanationView", scientificExplanationView);
            SetRef(so, "safetyNoteView", safetyNoteView);
            SetRef(so, "quizHintView", quizHintView);
            SetRef(so, "quizPanelView", quizPanelView);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRef(SerializedObject so, string propName, Object target)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
                prop.objectReferenceValue = target;
        }

        // -- Notebook Panel -----------------------------------

        private static NotebookView CreateNotebookPanel(Transform parent)
        {
            var panelGo = new GameObject("NotebookPanel");
            panelGo.transform.SetParent(parent, false);

            var rect = panelGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-20, -270);
            rect.sizeDelta = new Vector2(360, 350);

            // Background
            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);

            var outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.5f, 0.7f, 0.25f);
            outline.effectDistance = new Vector2(1, -1);

            var view = panelGo.AddComponent<NotebookView>();

            var layout = panelGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 10);
            layout.spacing = 4;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // Single scrollable text field for entries
            var textGo = new GameObject("entriesText");
            textGo.transform.SetParent(panelGo.transform, false);
            textGo.AddComponent<RectTransform>();

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 13;
            tmp.color = Color.white;
            tmp.richText = true;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Truncate;
            tmp.text = "\u2014";

            // Wire SerializeField
            var so = new SerializedObject(view);
            SetRef(so, "entriesText", tmp);
            so.ApplyModifiedPropertiesWithoutUndo();

            AddPanelTitle(panelGo, "Notebook");

            return view;
        }

        private static void WireNotebookController(NotebookController ctrl, NotebookView view)
        {
            var so = new SerializedObject(ctrl);
            SetRef(so, "notebookView", view);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // -- Interactive Quiz Panel ----------------------------

        private static QuizPanelView CreateQuizPanel(Transform parent)
        {
            // Root panel
            var panelGo = new GameObject("QuizPanel");
            panelGo.transform.SetParent(parent, false);

            var rect = panelGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -874);
            rect.sizeDelta = new Vector2(520, 0);

            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);

            var outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.5f, 0.7f, 0.25f);
            outline.effectDistance = new Vector2(1, -1);

            var layout = panelGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 10);
            layout.spacing = 6;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var fitter = panelGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var view = panelGo.AddComponent<QuizPanelView>();

            // Title
            AddPanelTitle(panelGo, "Quiz");

            // Question label
            var questionGo = new GameObject("questionLabel");
            questionGo.transform.SetParent(panelGo.transform, false);
            questionGo.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 36);
            questionGo.AddComponent<LayoutElement>().preferredHeight = 36;
            var questionTmp = questionGo.AddComponent<TextMeshProUGUI>();
            questionTmp.fontSize = 16;
            questionTmp.color = Color.white;
            questionTmp.enableWordWrapping = true;
            questionTmp.text = "\u2014";

            // Answer buttons (A, B, C)
            var btnA = CreateAnswerButton(panelGo.transform, "AnswerButtonA", "A");
            var btnB = CreateAnswerButton(panelGo.transform, "AnswerButtonB", "B");
            var btnC = CreateAnswerButton(panelGo.transform, "AnswerButtonC", "C");

            // Feedback panel
            var feedbackGo = new GameObject("FeedbackPanel");
            feedbackGo.transform.SetParent(panelGo.transform, false);
            feedbackGo.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 28);
            feedbackGo.AddComponent<LayoutElement>().preferredHeight = 28;
            feedbackGo.SetActive(false);

            var feedbackTmp = new GameObject("feedbackLabel");
            feedbackTmp.transform.SetParent(feedbackGo.transform, false);
            var fbRect = feedbackTmp.AddComponent<RectTransform>();
            fbRect.anchorMin = Vector2.zero;
            fbRect.anchorMax = Vector2.one;
            fbRect.sizeDelta = Vector2.zero;
            var fbText = feedbackTmp.AddComponent<TextMeshProUGUI>();
            fbText.fontSize = 15;
            fbText.color = new Color(0.9f, 0.9f, 0.5f);
            fbText.alignment = TextAlignmentOptions.Center;
            fbText.text = string.Empty;

            // Wire SerializeFields
            var so = new SerializedObject(view);
            SetRef(so, "questionLabel", questionTmp);
            SetRef(so, "answerButtonA", btnA.button);
            SetRef(so, "answerLabelA", btnA.label);
            SetRef(so, "answerButtonB", btnB.button);
            SetRef(so, "answerLabelB", btnB.label);
            SetRef(so, "answerButtonC", btnC.button);
            SetRef(so, "answerLabelC", btnC.label);
            SetRef(so, "feedbackLabel", fbText);
            SetRef(so, "feedbackPanel", feedbackGo);
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static (Button button, TextMeshProUGUI label) CreateAnswerButton(
            Transform parent, string name, string prefix)
        {
            var rowGo = new GameObject(name);
            rowGo.transform.SetParent(parent, false);
            rowGo.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 36);
            rowGo.AddComponent<LayoutElement>().preferredHeight = 36;

            var btnImg = rowGo.AddComponent<Image>();
            btnImg.color = new Color(0.18f, 0.22f, 0.30f);

            var btnOutline = rowGo.AddComponent<Outline>();
            btnOutline.effectColor = new Color(0.35f, 0.55f, 0.75f, 0.3f);
            btnOutline.effectDistance = new Vector2(1, -1);

            var button = rowGo.AddComponent<Button>();
            button.targetGraphic = btnImg;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(rowGo.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10, 0);
            labelRect.offsetMax = new Vector2(-10, 0);
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.fontSize = 15;
            labelTmp.color = Color.white;
            labelTmp.text = prefix;
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;

            return (button, labelTmp);
        }

        // -- Layout / Decoration Helpers ----------------------

        private static void AddPanelTitle(GameObject panelGo, string title)
        {
            // Insert as first child so VerticalLayoutGroup renders it at the top
            var titleGo = new GameObject("PanelTitle");
            titleGo.transform.SetParent(panelGo.transform, false);
            titleGo.transform.SetAsFirstSibling();

            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.sizeDelta = new Vector2(0, 22);
            var elem = titleGo.AddComponent<LayoutElement>();
            elem.preferredHeight = 22;

            var tmp = titleGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 12;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.55f, 0.7f, 0.9f);
            tmp.text = title.ToUpper();
            tmp.characterSpacing = 3;
        }

        private static void CreateSectionHeader(Transform parent, string title)
        {
            var headerGo = new GameObject(title + "Header");
            headerGo.transform.SetParent(parent, false);

            var rect = headerGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 22);
            var elem = headerGo.AddComponent<LayoutElement>();
            elem.preferredHeight = 22;

            var tmp = headerGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 13;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.6f, 0.75f, 0.95f);
            tmp.text = title;
        }

        private static void CreateDivider(Transform parent)
        {
            var dividerGo = new GameObject("Divider");
            dividerGo.transform.SetParent(parent, false);

            var rect = dividerGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 1);
            var elem = dividerGo.AddComponent<LayoutElement>();
            elem.preferredHeight = 1;
            elem.flexibleWidth = 1;

            var img = dividerGo.AddComponent<Image>();
            img.color = new Color(0.4f, 0.5f, 0.6f, 0.4f);

            // Add top margin via spacing element
            var spacerGo = new GameObject("Spacer");
            spacerGo.transform.SetParent(parent, false);
            var spacerElem = spacerGo.AddComponent<LayoutElement>();
            spacerElem.preferredHeight = 2;
        }

        // -- Input Panel Helpers -------------------------------

        private static GameObject CreateInputPanel(Transform canvasParent)
        {
            var panelGo = new GameObject("LabInputPanel");
            panelGo.transform.SetParent(canvasParent, false);

            var rect = panelGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20, -20);
            rect.sizeDelta = new Vector2(350, 0);

            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.10f, 0.16f, 0.92f);

            // Subtle outline for panel edge
            var outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.5f, 0.7f, 0.35f);
            outline.effectDistance = new Vector2(1, -1);

            var layout = panelGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 12);
            layout.spacing = 5;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var fitter = panelGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return panelGo;
        }

        private static T CreateDropdownControl<T>(Transform parent, string name, string labelText) where T : V3ViewBase
        {
            var rowGo = new GameObject(name + "Row");
            rowGo.transform.SetParent(parent, false);
            var rowRect = rowGo.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, 46);

            var rowElem = rowGo.AddComponent<LayoutElement>();
            rowElem.preferredHeight = 46;

            var rowLayout = rowGo.AddComponent<VerticalLayoutGroup>();
            rowLayout.spacing = 2;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;

            // Label
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(rowGo.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(0, 18);
            var labelLayoutElem = labelGo.AddComponent<LayoutElement>();
            labelLayoutElem.preferredHeight = 18;
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.fontSize = 13;
            labelTmp.color = new Color(0.7f, 0.7f, 0.7f);
            labelTmp.text = labelText;

            // Dropdown
            var dropdownGo = CreateTMPDropdown(rowGo.transform, name + "Dropdown");

            // Add the view component
            var view = rowGo.AddComponent<T>();
            var so = new SerializedObject(view);
            SetRef(so, "dropdown", dropdownGo.GetComponent<TMP_Dropdown>());
            SetRef(so, "label", labelTmp);
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static T CreateSliderControl<T>(Transform parent, string name, string labelText) where T : V3ViewBase
        {
            var rowGo = new GameObject(name + "Row");
            rowGo.transform.SetParent(parent, false);
            var rowRect = rowGo.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, 32);

            var rowElem = rowGo.AddComponent<LayoutElement>();
            rowElem.preferredHeight = 32;

            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;

            // Label
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(rowGo.transform, false);
            var labelElem = labelGo.AddComponent<LayoutElement>();
            labelElem.preferredWidth = 110;
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.fontSize = 13;
            labelTmp.color = new Color(0.78f, 0.78f, 0.78f);
            labelTmp.text = labelText;

            // Slider
            var sliderGo = CreateUnitySlider(rowGo.transform, name + "Slider");
            var sliderElem = sliderGo.AddComponent<LayoutElement>();
            sliderElem.flexibleWidth = 1;
            sliderElem.minWidth = 80;

            // Value label
            var valGo = new GameObject("ValueLabel");
            valGo.transform.SetParent(rowGo.transform, false);
            var valElem = valGo.AddComponent<LayoutElement>();
            valElem.preferredWidth = 44;
            var valTmp = valGo.AddComponent<TextMeshProUGUI>();
            valTmp.fontSize = 14;
            valTmp.color = Color.white;
            valTmp.text = "0";
            valTmp.alignment = TextAlignmentOptions.MidlineRight;

            // Add the view component
            var view = rowGo.AddComponent<T>();
            var so = new SerializedObject(view);
            SetRef(so, "slider", sliderGo.GetComponent<Slider>());
            SetRef(so, "valueLabel", valTmp);
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static CatalystToggleView CreateToggleControl(Transform parent, string name, string labelText)
        {
            var rowGo = new GameObject(name + "Row");
            rowGo.transform.SetParent(parent, false);
            var rowRect = rowGo.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, 30);
            var rowElem = rowGo.AddComponent<LayoutElement>();
            rowElem.preferredHeight = 30;

            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;

            // Toggle
            var toggleGo = new GameObject("Toggle");
            toggleGo.transform.SetParent(rowGo.transform, false);
            var toggleElem = toggleGo.AddComponent<LayoutElement>();
            toggleElem.preferredWidth = 30;
            toggleElem.preferredHeight = 30;

            var toggleBg = new GameObject("Background");
            toggleBg.transform.SetParent(toggleGo.transform, false);
            var bgImg = toggleBg.AddComponent<Image>();
            bgImg.color = new Color(0.3f, 0.3f, 0.3f);
            var bgRect = toggleBg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            var checkGo = new GameObject("Checkmark");
            checkGo.transform.SetParent(toggleBg.transform, false);
            var checkImg = checkGo.AddComponent<Image>();
            checkImg.color = new Color(0.3f, 0.8f, 0.3f);
            var checkRect = checkGo.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.1f, 0.1f);
            checkRect.anchorMax = new Vector2(0.9f, 0.9f);
            checkRect.sizeDelta = Vector2.zero;

            var toggle = toggleGo.AddComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.isOn = false;

            // Label
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(rowGo.transform, false);
            var labelElem = labelGo.AddComponent<LayoutElement>();
            labelElem.flexibleWidth = 1;
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.fontSize = 13;
            labelTmp.color = new Color(0.78f, 0.78f, 0.78f);
            labelTmp.text = labelText;

            // Add the view component
            var view = rowGo.AddComponent<CatalystToggleView>();
            var so = new SerializedObject(view);
            SetRef(so, "toggle", toggle);
            SetRef(so, "label", labelTmp);
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static MixButtonView CreateMixButton(Transform parent)
        {
            var btnRowGo = new GameObject("MixButtonRow");
            btnRowGo.transform.SetParent(parent, false);
            var btnRowRect = btnRowGo.AddComponent<RectTransform>();
            btnRowRect.sizeDelta = new Vector2(0, 48);
            var btnRowElem = btnRowGo.AddComponent<LayoutElement>();
            btnRowElem.preferredHeight = 48;
            btnRowElem.minHeight = 44;

            var btnGo = new GameObject("MixButton");
            btnGo.transform.SetParent(btnRowGo.transform, false);
            var btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.anchorMin = Vector2.zero;
            btnRect.anchorMax = Vector2.one;
            btnRect.sizeDelta = Vector2.zero;

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.15f, 0.55f, 0.85f);

            var btnOutline = btnGo.AddComponent<Outline>();
            btnOutline.effectColor = new Color(0.3f, 0.7f, 1f, 0.5f);
            btnOutline.effectDistance = new Vector2(1, -1);

            var button = btnGo.AddComponent<Button>();
            button.targetGraphic = btnImg;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(btnGo.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.fontSize = 20;
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.color = Color.white;
            labelTmp.text = "\u25b6  Mix";
            labelTmp.alignment = TextAlignmentOptions.Center;

            var view = btnRowGo.AddComponent<MixButtonView>();
            var so = new SerializedObject(view);
            SetRef(so, "button", button);
            SetRef(so, "buttonLabel", labelTmp);
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static void WireLabInputController(LabInputController ctrl,
            ReactionController reactionCtrl,
            ReagentDropdownView reagentA, ReagentDropdownView reagentB,
            ReagentDropdownView reagentC, ReagentDropdownView reagentD,
            MediumDropdownView medium,
            TemperatureSliderView temperature, StirringSliderView stirring, GrindingSliderView grinding,
            CatalystToggleView catalyst, MixButtonView mixBtn)
        {
            var so = new SerializedObject(ctrl);
            SetRef(so, "reagentADropdown", reagentA);
            SetRef(so, "reagentBDropdown", reagentB);
            SetRef(so, "reagentCDropdown", reagentC);
            SetRef(so, "reagentDDropdown", reagentD);
            SetRef(so, "mediumDropdown", medium);
            SetRef(so, "temperatureSlider", temperature);
            SetRef(so, "stirringSlider", stirring);
            SetRef(so, "grindingSlider", grinding);
            SetRef(so, "catalystToggle", catalyst);
            SetRef(so, "mixButton", mixBtn);
            SetRef(so, "reactionController", reactionCtrl);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // -- Primitive UI Builders -----------------------------

        private static GameObject CreateTMPDropdown(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var elem = go.AddComponent<LayoutElement>();
            elem.preferredHeight = 30;

            // Dropdown background
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.25f);

            // Caption text
            var captionGo = new GameObject("CaptionText");
            captionGo.transform.SetParent(go.transform, false);
            var captionRect = captionGo.AddComponent<RectTransform>();
            captionRect.anchorMin = Vector2.zero;
            captionRect.anchorMax = Vector2.one;
            captionRect.offsetMin = new Vector2(8, 0);
            captionRect.offsetMax = new Vector2(-24, 0);
            var captionTmp = captionGo.AddComponent<TextMeshProUGUI>();
            captionTmp.fontSize = 14;
            captionTmp.color = Color.white;

            // Arrow indicator
            var arrowGo = new GameObject("Arrow");
            arrowGo.transform.SetParent(go.transform, false);
            var arrowRect = arrowGo.AddComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1, 0);
            arrowRect.anchorMax = new Vector2(1, 1);
            arrowRect.pivot = new Vector2(1, 0.5f);
            arrowRect.anchoredPosition = new Vector2(-4, 0);
            arrowRect.sizeDelta = new Vector2(20, 0);
            var arrowTmp = arrowGo.AddComponent<TextMeshProUGUI>();
            arrowTmp.fontSize = 12;
            arrowTmp.color = new Color(0.6f, 0.7f, 0.8f);
            arrowTmp.text = "\u25bc";
            arrowTmp.alignment = TextAlignmentOptions.Center;

            // Template (required by TMP_Dropdown)
            var templateGo = new GameObject("Template");
            templateGo.transform.SetParent(go.transform, false);
            var templateRect = templateGo.AddComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = Vector2.zero;
            templateRect.sizeDelta = new Vector2(0, 150);
            templateGo.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f);
            var scrollRect = templateGo.AddComponent<ScrollRect>();

            // Viewport inside template
            var viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(templateGo.transform, false);
            var vpRect = viewportGo.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.sizeDelta = Vector2.zero;
            viewportGo.AddComponent<Image>().color = Color.white;
            viewportGo.AddComponent<Mask>().showMaskGraphic = false;

            // Content inside viewport
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRect = contentGo.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 28);

            scrollRect.viewport = vpRect;
            scrollRect.content = contentRect;

            // Item inside content
            var itemGo = new GameObject("Item");
            itemGo.transform.SetParent(contentGo.transform, false);
            var itemRect = itemGo.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 0.5f);
            itemRect.anchorMax = new Vector2(1, 0.5f);
            itemRect.sizeDelta = new Vector2(0, 28);
            var itemToggle = itemGo.AddComponent<Toggle>();

            var itemBgGo = new GameObject("Item Background");
            itemBgGo.transform.SetParent(itemGo.transform, false);
            var itemBgRect = itemBgGo.AddComponent<RectTransform>();
            itemBgRect.anchorMin = Vector2.zero;
            itemBgRect.anchorMax = Vector2.one;
            itemBgRect.sizeDelta = Vector2.zero;
            itemBgGo.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);

            var itemLabelGo = new GameObject("Item Label");
            itemLabelGo.transform.SetParent(itemGo.transform, false);
            var itemLabelRect = itemLabelGo.AddComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(8, 0);
            itemLabelRect.offsetMax = new Vector2(-8, 0);
            var itemLabelTmp = itemLabelGo.AddComponent<TextMeshProUGUI>();
            itemLabelTmp.fontSize = 14;
            itemLabelTmp.color = Color.white;

            itemToggle.targetGraphic = itemBgGo.GetComponent<Image>();

            templateGo.SetActive(false);

            // TMP_Dropdown component
            var dropdown = go.AddComponent<TMP_Dropdown>();
            dropdown.captionText = captionTmp;
            dropdown.itemText = itemLabelTmp;
            dropdown.template = templateRect;

            return go;
        }

        private static GameObject CreateUnitySlider(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            // Background
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(go.transform, false);
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.4f);
            bgRect.anchorMax = new Vector2(1, 0.6f);
            bgRect.sizeDelta = Vector2.zero;
            var bgImg2 = bgGo.AddComponent<Image>();
            bgImg2.color = new Color(0.25f, 0.25f, 0.30f);

            // Fill Area
            var fillAreaGo = new GameObject("Fill Area");
            fillAreaGo.transform.SetParent(go.transform, false);
            var fillAreaRect = fillAreaGo.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.4f);
            fillAreaRect.anchorMax = new Vector2(1, 0.6f);
            fillAreaRect.offsetMin = new Vector2(5, 0);
            fillAreaRect.offsetMax = new Vector2(-5, 0);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.sizeDelta = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.3f, 0.6f, 0.9f);

            // Handle slide area
            var handleAreaGo = new GameObject("Handle Slide Area");
            handleAreaGo.transform.SetParent(go.transform, false);
            var handleAreaRect = handleAreaGo.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10, 0);
            handleAreaRect.offsetMax = new Vector2(-10, 0);

            var handleGo = new GameObject("Handle");
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            var handleRect = handleGo.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 0);
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = new Color(0.85f, 0.9f, 1f);

            // Slider component
            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;

            return go;
        }
    }
}
#endif
