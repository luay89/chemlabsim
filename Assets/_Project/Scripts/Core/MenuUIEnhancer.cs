using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to the Menu Canvas root to apply professional runtime styling.
/// Fixes CanvasScaler, creates title/subtitle, resizes and styles buttons.
/// Only modifies buttons whose labels match known keywords (start/quit).
/// </summary>
public class MenuUIEnhancer : MonoBehaviour
{
    [Header("Optional — auto-detected if left empty")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button quitButton;

    private TextMeshProUGUI subtitleText;
    private TextMeshProUGUI helperText;
    private TextMeshProUGUI versionText;
    private RectTransform centerPanel;
    private RectTransform iconRect;
    private float animationSeed;

    private static readonly Color32 PanelBg      = new Color32(20, 30, 50, 220);
    private static readonly Color32 BtnStartNorm = new Color32(30, 100, 70, 255);
    private static readonly Color32 BtnStartHov  = new Color32(40, 130, 90, 255);
    private static readonly Color32 BtnStartPrs  = new Color32(20, 75, 55, 255);
    private static readonly Color32 BtnQuitNorm  = new Color32(80, 40, 40, 255);
    private static readonly Color32 BtnQuitHov   = new Color32(110, 55, 55, 255);
    private static readonly Color32 BtnQuitPrs   = new Color32(60, 30, 30, 255);
    private static readonly Color32 TextWhite    = new Color32(230, 230, 230, 255);

    private bool initialized;

    private void Start()
    {
        if (initialized) return;
        initialized = true;
        animationSeed = Random.Range(0f, 10f);

        FixCanvasScaler();
        CreateBackground();
        CreateCenterPanel();
        ApplyLocalization();
    }

    private void Update()
    {
        if (centerPanel != null)
        {
            float panelFloat = Mathf.Sin((Time.unscaledTime + animationSeed) * 0.7f) * 8f;
            centerPanel.anchoredPosition = new Vector2(0f, panelFloat);
        }

        if (iconRect != null)
        {
            float pulse = 1f + Mathf.Sin((Time.unscaledTime + animationSeed) * 1.6f) * 0.035f;
            iconRect.localScale = new Vector3(pulse, pulse, 1f);
        }
    }

    private void OnDestroy()
    {
    }

    private void FixCanvasScaler()
    {
        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null) return;

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void CreateBackground()
    {
        // Gradient-like background overlay
        var bgGO = new GameObject("_MenuBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.SetParent(transform, false);
        bgRect.SetAsFirstSibling();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        var bgImg = bgGO.GetComponent<Image>();
        bgImg.color = new Color(0.08f, 0.12f, 0.22f, 0.95f);
        bgImg.raycastTarget = false;

        CreateAmbientStrip("_TopGlow", new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1400f, 280f),
            new Color(0.13f, 0.28f, 0.45f, 0.28f));
        CreateAmbientStrip("_MidGlow", new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(1550f, 520f),
            new Color(0.08f, 0.20f, 0.31f, 0.2f));
        CreateAmbientStrip("_BottomGlow", new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(1400f, 320f),
            new Color(0.16f, 0.12f, 0.05f, 0.18f));
    }

    private void CreateCenterPanel()
    {
        // Center container panel
        var panelGO = new GameObject("_MenuCenter", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var panelRect = panelGO.GetComponent<RectTransform>();
        centerPanel = panelRect;
        panelRect.SetParent(transform, false);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(760f, 760f);

        var panelImg = panelGO.GetComponent<Image>();
        panelImg.color = PanelBg;
        panelImg.raycastTarget = false;

        // -- Decorative icon (safe ASCII) --
        TextMeshProUGUI iconLabel = CreateLabel(panelRect, "_Icon", "<b>C</b>", 128, new Color(0.56f, 0.79f, 0.98f, 0.6f),
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(200f, 140f));
        iconLabel.richText = true;
        iconRect = iconLabel.rectTransform;

        // -- Title --
        titleText = CreateLabel(panelRect, "_Title", "", 0, Color.white,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(660f, 120f));
        titleText.richText = true;

        // -- Subtitle --
        subtitleText = CreateLabel(panelRect, "_Subtitle", string.Empty, 32,
            new Color(0.62f, 0.62f, 0.62f, 1f), TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(640f, 50f));

        // -- Helper copy --
        helperText = CreateLabel(panelRect, "_Helper", string.Empty, 25,
            new Color(0.78f, 0.86f, 0.92f, 0.92f), TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0f, -342f), new Vector2(610f, 84f));
        helperText.enableWordWrapping = true;
        helperText.overflowMode = TextOverflowModes.Ellipsis;

        // -- Divider --
        var divGO = new GameObject("_Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var divRect = divGO.GetComponent<RectTransform>();
        divRect.SetParent(panelRect, false);
        divRect.anchorMin = new Vector2(0.5f, 1f);
        divRect.anchorMax = new Vector2(0.5f, 1f);
        divRect.pivot = new Vector2(0.5f, 0.5f);
        divRect.anchoredPosition = new Vector2(0f, -438f);
        divRect.sizeDelta = new Vector2(440f, 2f);
        divGO.GetComponent<Image>().color = new Color(1f, 0.84f, 0.31f, 0.3f);

        // -- Find and restyle existing buttons --
        DetectButtons();

        // -- Start Button --
        if (startButton != null)
        {
            StyleButton(startButton, panelRect, new Vector2(0f, -520f), new Vector2(520f, 88f),
                BtnStartNorm, BtnStartHov, BtnStartPrs, string.Empty, 38);
        }

        // -- Quit Button --
        if (quitButton != null)
        {
            StyleButton(quitButton, panelRect, new Vector2(0f, -628f), new Vector2(520f, 72f),
                BtnQuitNorm, BtnQuitHov, BtnQuitPrs, string.Empty, 30);
        }

        // -- Version tag --
        versionText = CreateLabel(panelRect, "_Version", "v1.0.0", 22,
            new Color(0.4f, 0.4f, 0.4f, 0.7f), TextAlignmentOptions.Center,
            new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(200f, 30f));
    }

    private void CreateAmbientStrip(string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private void DetectButtons()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button btn in buttons)
        {
            if (btn == null) continue;

            TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label == null || string.IsNullOrWhiteSpace(label.text)) continue;

            string lower = label.text.Trim().ToLowerInvariant();

            if (startButton == null &&
                (lower.Contains("start") || lower.Contains("lab") || lower.Contains("play")))
            {
                startButton = btn;
            }
            else if (quitButton == null &&
                     (lower.Contains("quit") || lower.Contains("exit")))
            {
                quitButton = btn;
            }
        }
    }

    private void StyleButton(Button btn, RectTransform parent, Vector2 pos, Vector2 size,
        Color32 normal, Color32 hover, Color32 pressed, string labelText, int fontSize)
    {
        var btnRect = btn.GetComponent<RectTransform>();
        btnRect.SetParent(parent, false);
        btnRect.anchorMin = new Vector2(0.5f, 1f);
        btnRect.anchorMax = new Vector2(0.5f, 1f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = pos;
        btnRect.sizeDelta = size;

        ColorBlock cb = btn.colors;
        cb.normalColor = normal;
        cb.highlightedColor = hover;
        cb.pressedColor = pressed;
        cb.selectedColor = hover;
        cb.fadeDuration = 0.12f;
        btn.colors = cb;

        Image img = btn.GetComponent<Image>();
        if (img != null)
            img.color = Color.white;

        TextMeshProUGUI lbl = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (lbl != null)
        {
            lbl.text = labelText;
            lbl.fontSize = fontSize;
            lbl.color = TextWhite;
            lbl.fontStyle = FontStyles.Bold;
            lbl.alignment = TextAlignmentOptions.Center;
        }
    }

    private Button CreateButton(RectTransform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        var label = CreateLabel(rect, "_Label", string.Empty, 24, TextWhite,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }

    private TextMeshProUGUI CreateLabel(RectTransform parent, string name, string text, int fontSize,
        Color color, TextAlignmentOptions align, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.enableAutoSizing = false;
        tmp.overflowMode = TextOverflowModes.Overflow;

        var rect = tmp.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        return tmp;
    }

    private void ApplyLocalization()
    {
        if (titleText != null)
        {
            titleText.isRightToLeftText = false;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.text = "<color=#FFD54F><size=72><b>ChemLab</b></size></color><color=#90CAF9><size=72><b>Sim</b></size></color>";
        }

        if (subtitleText != null)
            subtitleText.text = "Interactive Chemistry Laboratory";

        if (helperText != null)
            helperText.text = "Explore realistic reactions, combine reagents, and observe scientific results in a safe virtual lab.";

        if (versionText != null)
            versionText.text = "v1.0.0";

        if (startButton != null)
        {
            var label = startButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = "Start Lab";
                label.fontStyle = FontStyles.Bold;
            }
        }

        if (quitButton != null)
        {
            var label = quitButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = "Quit";
                label.fontStyle = FontStyles.Bold;
            }
        }
    }
}
