using UnityEngine;
using UnityEngine.UI;

namespace ChemLabSimV3.Engine.SimulationV1
{
    /// <summary>
    /// Runtime debug overlay for ReactionSimulationEngine.
    /// Creates/uses a top-left UI text named "DebugOverlayText" and updates it every frame.
    /// Toggle visibility with D.
    /// </summary>
    public class SimulationDebugOverlay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ReactionSimulationEngine simulationEngine;
        [SerializeField] private Text debugOverlayText;

        [Header("Behavior")]
        [SerializeField] private KeyCode toggleKey = KeyCode.D;
        [SerializeField] private bool visibleOnStart = true;

        [Header("Rate Color")]
        [SerializeField] private Color lowRateColor = new Color(0.35f, 0.60f, 1.00f, 1.00f);
        [SerializeField] private Color highRateColor = new Color(1.00f, 0.28f, 0.25f, 1.00f);
        [SerializeField, Min(0.01f)] private float highRateReference = 1.25f;

        private bool _isVisible;

        private void Awake()
        {
            if (simulationEngine == null)
                simulationEngine = FindObjectOfType<ReactionSimulationEngine>();

            if (debugOverlayText == null)
                debugOverlayText = GetOrCreateDebugOverlayText();

            _isVisible = visibleOnStart;
            SetOverlayVisible(_isVisible);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                _isVisible = !_isVisible;
                SetOverlayVisible(_isVisible);
            }

            if (!_isVisible || debugOverlayText == null || simulationEngine == null)
                return;

            var state = simulationEngine.State;
            float reactionRate = state.reactionRate;
            float gasAmount = state.gasAmount;
            Debug.Log($"Rate: {reactionRate}, Gas: {gasAmount}");
            float progressPercent = state.progress * 100f;
            float foamLevel = simulationEngine.FoamLevel;

            debugOverlayText.text =
                "Progress: " + progressPercent.ToString("0.0") + "%" +
                "\nRate: " + reactionRate.ToString("0.000") +
                "\nGas: " + gasAmount.ToString("0.000") +
                "\nFoam: " + foamLevel.ToString("0.00") +
                "\nTemp: " + state.temperature.ToString("0.0") + " °C";

            float rateT = Mathf.Clamp01(reactionRate / highRateReference);
            debugOverlayText.color = Color.Lerp(lowRateColor, highRateColor, rateT);
        }

        private void SetOverlayVisible(bool visible)
        {
            if (debugOverlayText != null)
                debugOverlayText.gameObject.SetActive(visible);
        }

        private static Text GetOrCreateDebugOverlayText()
        {
            var existing = GameObject.Find("DebugOverlayText");
            if (existing != null)
            {
                var existingText = existing.GetComponent<Text>();
                if (existingText != null)
                    return existingText;
            }

            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("DebugOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            var textGo = new GameObject("DebugOverlayText", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(canvas.transform, false);

            var rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(18f, -18f);
            rect.sizeDelta = new Vector2(420f, 170f);

            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Color.white;
            text.raycastTarget = false;

            return text;
        }
    }
}
