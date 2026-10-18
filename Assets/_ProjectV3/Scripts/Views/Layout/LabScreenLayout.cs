// ChemLabSim v3 — LabV3 screen layout (phone + desktop)
// Keeps the 3D beaker in the middle of the screen:
//   • left   : Lab Controls (scaled down if taller than the screen)
//   • centre : free for the beaker
//   • right  : one column of tabs, each a vertical scroll list — "Learn" (guided
//              experiments), "Result" (result, identity, details, explanation, safety,
//              quiz), "Progress" (objective, challenge, progress, notebook), "Assistant"
//              and "Tests"
// Everything sits inside a SafeArea rect (notches, rounded corners, nav bars).
// It only re-parents the existing panels at runtime; views and controllers keep
// their references, so no scene or controller changes are needed.

using ChemLabSimV3.Events;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChemLabSimV3.Views.Layout
{
    [RequireComponent(typeof(Canvas))]
    public class LabScreenLayout : MonoBehaviour
    {
        private const string TargetScene = "LabV3";
        private const string CanvasName  = "V3Canvas";

        private static readonly string[] ResultPanels =
        {
            "ReactionResultPanel", "ReactionIdentityPanel", "ReactionDetailsPanel",
            "ScientificExplanationPanel", "SafetyNotePanel", "QuizHintPanel", "QuizPanel"
        };

        private static readonly string[] ProgressPanels =
        {
            "ObjectivePanel", "ChallengePanel", "ProgressPanel", "NotebookPanel"
        };

        // ── Sizes (canvas reference units) ──────────────────────────────
        private const float Margin    = 16f;
        private const float TabHeight = 44f;
        private const float Gap       = 8f;
        private const float SpeedBarHeight = 44f;
        private static readonly Vector2 DesktopReference = new Vector2(1920f, 1080f);
        private static readonly Vector2 PhoneReference   = new Vector2(1280f, 720f);
        private const float DesktopColumnWidth = 480f;
        private const float PhoneColumnWidth   = 400f;
        private const float PhoneMaxDiagonalInches = 7f;

        private static readonly Color TabOn  = new Color(0.16f, 0.45f, 0.85f, 1f);
        private static readonly Color TabOff = new Color(0.12f, 0.14f, 0.20f, 0.92f);

        private RectTransform safeArea;
        private RectTransform controlsPanel;
        private const int LearnTab = 0, ResultTab = 1, ProgressTab = 2, AssistantTab = 3, TestsTab = 4;
        private ScrollRect[] scrolls;
        private Image[] tabImages;
        private int activeTab;
        private LabAssistantView assistantView;
        private SimulationSpeedBar speedBar;
        private ReactionHeadline headline;
        private GameObject quizPanel, quizHintPanel;
        private Rect lastSafeArea;
        private Vector2 lastScreen;
        private float columnWidth;
        private int placeFrames;   // re-place floating panels for a few frames after the scaler changes

        // ── Bootstrap ───────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (scene.name != TargetScene || FindObjectOfType<LabScreenLayout>() != null) return;
            var canvasGo = GameObject.Find(CanvasName);
            if (canvasGo == null || canvasGo.GetComponent<Canvas>() == null)
            {
                Debug.LogWarning($"[LabScreenLayout] '{CanvasName}' not found; keeping the scene layout.");
                return;
            }
            canvasGo.AddComponent<LabScreenLayout>();
        }

        // ── Lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            bool phone = IsPhone();
            ApplyScaler(phone);

            safeArea = CreateSafeArea();
            columnWidth = phone ? PhoneColumnWidth : DesktopColumnWidth;

            controlsPanel = FindPanel("LabInputPanel");

            var column = NewRect("InfoColumn", safeArea);
            column.anchorMin = new Vector2(1f, 0f);
            column.anchorMax = new Vector2(1f, 1f);
            column.pivot = new Vector2(1f, 0.5f);
            column.sizeDelta = new Vector2(columnWidth, -2f * Margin);
            column.anchoredPosition = new Vector2(-Margin, 0f);

            var tabs = NewRect("Tabs", column);
            tabs.anchorMin = new Vector2(0f, 1f);
            tabs.anchorMax = new Vector2(1f, 1f);
            tabs.pivot = new Vector2(0.5f, 1f);
            tabs.sizeDelta = new Vector2(0f, TabHeight);
            var tabRow = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabRow.spacing = Gap;
            tabRow.childControlWidth = tabRow.childControlHeight = true;
            tabRow.childForceExpandWidth = tabRow.childForceExpandHeight = true;

            tabImages = new[]
            {
                CreateTab(tabs, "Learn",     () => ShowTab(LearnTab)),
                CreateTab(tabs, "Result",    () => ShowTab(ResultTab)),
                CreateTab(tabs, "Progress",  () => ShowTab(ProgressTab)),
                CreateTab(tabs, "Assistant", () => ShowTab(AssistantTab)),
                CreateTab(tabs, "Tests",     () => ShowTab(TestsTab)),
            };
            scrolls = new[]
            {
                CreateScrollList("LearnScroll", column),
                CreateScrollList("ResultsScroll", column),
                CreateScrollList("ProgressScroll", column),
                CreateScrollList("AssistantScroll", column),
                CreateScrollList("TestsScroll", column),
            };

            foreach (var name in ResultPanels)   Adopt(name, scrolls[ResultTab].content);
            foreach (var name in ProgressPanels) Adopt(name, scrolls[ProgressTab].content);
            var amounts = StoichiometryPanelView.Create(scrolls[ResultTab].content);
            amounts.transform.SetSiblingIndex(1);   // right below the reaction result
            assistantView = LabAssistantView.Create(scrolls[AssistantTab].content);
            assistantView.SetWidth(columnWidth);
            LabTestsView.Create(scrolls[TestsTab].content);
            LearnView.Create(scrolls[LearnTab].content);
            PredictView.Create(controlsPanel);
            CreatePeriodicTableButton();
            speedBar = SimulationSpeedBar.Create(safeArea);

            quizPanel = FindPanel("QuizPanel")?.gameObject;
            quizHintPanel = FindPanel("QuizHintPanel")?.gameObject;
            headline = ReactionHeadline.Create(safeArea);
            LabTheme.Apply(transform);

            ShowTab(ResultTab);
            ApplySafeArea();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ReactionEvaluatedEvent>(OnReactionEvaluated);
            EventBus.Subscribe<ReactionNotFoundEvent>(OnReactionNotFound);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ReactionEvaluatedEvent>(OnReactionEvaluated);
            EventBus.Unsubscribe<ReactionNotFoundEvent>(OnReactionNotFound);
        }

        private void LateUpdate()
        {
            if (safeArea == null) return;
            if (Screen.safeArea != lastSafeArea || new Vector2(Screen.width, Screen.height) != lastScreen)
                ApplySafeArea();
            // The scaler applies the new reference size on its next update, so sizes that
            // depend on the canvas width are set again once it has settled.
            if (placeFrames > 0)
            {
                placeFrames--;
                PlaceFloatingPanels();
            }
            FitControlsPanel();

            // "Think about it" repeats the quiz question; show it only when there is no quiz.
            if (quizHintPanel != null && quizPanel != null && quizPanel.activeSelf && quizHintPanel.activeSelf)
                quizHintPanel.SetActive(false);
        }

        // A new result should be visible right away, from the top (the assistant and learn
        // tabs answer the new experiment by themselves, so they stay open).
        private void OnReactionEvaluated(ReactionEvaluatedEvent e) => OnNewExperiment();
        private void OnReactionNotFound(ReactionNotFoundEvent e) => OnNewExperiment();
        private void OnNewExperiment() => ShowTab(activeTab == AssistantTab || activeTab == LearnTab ? activeTab : ResultTab);

        // ── Layout pieces ───────────────────────────────────────────────

        private static bool IsPhone()
        {
            // "-chemlab-phone" previews the phone layout on a desktop build (screenshot checks).
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-chemlab-phone") >= 0) return true;
            if (!UnityEngine.Application.isMobilePlatform) return false;
            if (Screen.dpi <= 0f) return true;
            float w = Screen.width / Screen.dpi, h = Screen.height / Screen.dpi;
            return Mathf.Sqrt(w * w + h * h) < PhoneMaxDiagonalInches;
        }

        private void ApplyScaler(bool phone)
        {
            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) return;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = phone ? PhoneReference : DesktopReference;
            scaler.matchWidthOrHeight = phone ? 1f : 0.5f;   // phones: size by height, width is free
        }

        private RectTransform CreateSafeArea()
        {
            var rect = NewRect("SafeArea", transform);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            // Move every existing UI child into the safe area (keeps their own anchors).
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child == rect) continue;
                child.SetParent(rect, false);
                child.SetSiblingIndex(0);
            }
            return rect;
        }

        private void ApplySafeArea()
        {
            lastSafeArea = Screen.safeArea;
            lastScreen = new Vector2(Screen.width, Screen.height);
            placeFrames = 2;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            Rect area = Screen.safeArea;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        }

        private void FitControlsPanel()
        {
            if (controlsPanel == null) return;
            float available = safeArea.rect.height - 3f * Margin - SpeedBarHeight;
            float needed = controlsPanel.rect.height;
            float scale = needed > 0f && available > 0f ? Mathf.Min(1f, available / needed) : 1f;
            if (!Mathf.Approximately(controlsPanel.localScale.x, scale))
                controlsPanel.localScale = new Vector3(scale, scale, 1f);
        }

        private void PlaceFloatingPanels()
        {
            if (controlsPanel != null)
            {
                controlsPanel.anchorMin = controlsPanel.anchorMax = controlsPanel.pivot = new Vector2(0f, 1f);
                controlsPanel.anchoredPosition = new Vector2(Margin, -Margin);
            }

            // Hint: bottom centre, never wider than the free space between the side columns.
            var hint = FindPanel("GuidancePanel");
            if (hint != null)
            {
                float leftWidth = controlsPanel != null ? controlsPanel.rect.width + Margin : 0f;
                hint.anchorMin = hint.anchorMax = new Vector2(0f, 0f);
                hint.pivot = new Vector2(0f, 0f);
                hint.anchoredPosition = new Vector2(leftWidth + 2f * Margin, Margin);
                hint.sizeDelta = new Vector2(
                    Mathf.Max(240f, safeArea.rect.width - leftWidth - columnWidth - 5f * Margin),
                    hint.sizeDelta.y);
            }

            if (speedBar != null)
            {
                var bar = (RectTransform)speedBar.transform;
                bar.anchoredPosition = new Vector2(Margin, Margin);
                if (controlsPanel != null)
                    bar.sizeDelta = new Vector2(controlsPanel.rect.width, SpeedBarHeight);
            }

            // Free space between the side columns: headline on top, toast above the hint.
            float leftEdge = controlsPanel != null ? controlsPanel.rect.width * controlsPanel.localScale.x + 2f * Margin : Margin;
            float rightEdge = safeArea.rect.width - columnWidth - 2f * Margin;
            float centreX = (leftEdge + rightEdge) * 0.5f - safeArea.rect.width * 0.5f;
            float freeWidth = Mathf.Max(240f, rightEdge - leftEdge - 2f * Margin);

            if (headline != null)
            {
                var rect = (RectTransform)headline.transform;
                rect.anchoredPosition = new Vector2(centreX, -Margin);
                rect.sizeDelta = new Vector2(freeWidth, rect.sizeDelta.y);
            }

            var toast = FindPanel("AchievementToastPanel");
            if (toast != null)
            {
                float hintHeight = hint != null ? hint.sizeDelta.y : 0f;
                toast.anchorMin = toast.anchorMax = toast.pivot = new Vector2(0.5f, 0f);
                toast.anchoredPosition = new Vector2(centreX, hintHeight + 2f * Margin);
            }
        }

        // Periodic Table & Element Explorer: a full-screen panel over the lab, opened from a small
        // button in the corner of Lab Controls (beside the panel title).
        private PeriodicTableView periodicTable;

        private void CreatePeriodicTableButton()
        {
            periodicTable = PeriodicTableView.Create(safeArea, null);
            var host = controlsPanel != null ? controlsPanel : safeArea;
            var rect = NewRect("PeriodicTableButton", host);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(124f, 24f);
            rect.anchoredPosition = new Vector2(-8f, -6f);
            var image = rect.gameObject.AddComponent<Image>();
            LabTheme.Round(image, LabTheme.Accent, 2f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => periodicTable.Open());
            var label = NewRect("Label", rect);
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = label.offsetMax = Vector2.zero;
            var text = label.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = "Periodic Table";
            text.fontSize = 13;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;   // the panel's own layout must not move it
        }

        private Image CreateTab(RectTransform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var rect = NewRect(label + "Tab", parent);
            var image = rect.gameObject.AddComponent<Image>();
            LabTheme.Round(image, TabOff, 2f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var textRect = NewRect("Label", rect);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            var text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 18;
            text.enableAutoSizing = true;   // five tabs share the phone column
            text.fontSizeMin = 11;
            text.fontSizeMax = 18;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return image;
        }

        private static ScrollRect CreateScrollList(string name, RectTransform column)
        {
            var root = NewRect(name, column);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = new Vector2(0f, -(TabHeight + Gap));
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);    // invisible, but catches drags between panels

            var viewport = NewRect("Viewport", root);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var list = content.gameObject.AddComponent<VerticalLayoutGroup>();
            list.spacing = Gap;
            list.childControlWidth = list.childControlHeight = true;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;
            return scroll;
        }

        /// <summary>Moves a panel into a scroll list and lets its height follow its text.</summary>
        private void Adopt(string panelName, RectTransform content)
        {
            var panel = FindPanel(panelName);
            if (panel == null)
            {
                Debug.LogWarning($"[LabScreenLayout] Panel '{panelName}' not found.");
                return;
            }

            panel.SetParent(content, false);
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.localScale = Vector3.one;

            // The list sizes the panel now; a fitter on the panel would fight it.
            var fitter = panel.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;

            if (panel.GetComponent<LayoutGroup>() == null)
            {
                var element = panel.GetComponent<LayoutElement>();
                if (element == null) element = panel.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = Mathf.Max(element.preferredHeight, panel.sizeDelta.y);
            }

            // Fixed 28-unit text rows cut long explanations; let each text take its wrapped height.
            foreach (var text in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                text.enableWordWrapping = true;
                var element = text.GetComponent<LayoutElement>();
                if (element != null && element.preferredHeight > 0f)
                {
                    element.minHeight = Mathf.Max(element.minHeight, 18f);
                    element.preferredHeight = -1f;
                }
            }
        }

        private void ShowTab(int tab)
        {
            if (scrolls == null) return;
            activeTab = tab;
            for (int i = 0; i < scrolls.Length; i++)
            {
                scrolls[i].gameObject.SetActive(i == tab);
                tabImages[i].color = i == tab ? TabOn : TabOff;
            }
            Canvas.ForceUpdateCanvases();
            scrolls[tab].verticalNormalizedPosition = 1f;
        }

        // ── Helpers ────────────────────────────────────────────────────

        private RectTransform FindPanel(string panelName)
        {
            var root = safeArea != null ? safeArea : transform;
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == panelName) return rect;
            return null;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
