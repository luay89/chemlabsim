// ChemLabSim v3 — Real-Time Reaction Graph
// Draws a live line graph showing reaction progress over time.
// Tracks: Completion %, Reaction Rate, Temperature Delta.
// Uses Unity UI (RectTransform + CanvasRenderer) with custom line drawing.
// Subscribes to ChemFxTriggeredEvent for data; updates every frame while active.
//
// Style: minimal + scientific — dark background, thin colored lines,
//        small axis labels, no grid clutter.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChemLabSimV3.Views
{
    public class ReactionGraphView : MonoBehaviour
    {
        // ── Configuration ──────────────────────────────────────
        private const int MaxSamples = 120;       // ~2 seconds at 60 FPS
        private const float SampleInterval = 0.033f; // ~30 Hz
        private const float GraphWidth = 320f;
        private const float GraphHeight = 140f;

        // ── Line colors (scientific palette) ───────────────────
        private static readonly Color CompletionColor = new Color(0.15f, 0.75f, 0.40f, 0.95f);
        private static readonly Color RateColor       = new Color(0.30f, 0.55f, 1.0f, 0.90f);
        private static readonly Color TempColor       = new Color(0.95f, 0.45f, 0.15f, 0.85f);
        private static readonly Color BgColor         = new Color(0.08f, 0.09f, 0.12f, 0.92f);
        private static readonly Color GridColor       = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color AxisColor        = new Color(1f, 1f, 1f, 0.25f);

        // ── Data ───────────────────────────────────────────────
        private readonly List<float> _completionSamples = new List<float>(MaxSamples);
        private readonly List<float> _rateSamples       = new List<float>(MaxSamples);
        private readonly List<float> _tempSamples       = new List<float>(MaxSamples);
        private float _sampleTimer;
        private bool _recording;

        // Current values (set by ChemFxTriggeredEvent)
        private float _completion;
        private float _rate;
        private float _tempDelta;

        // ── UI objects ─────────────────────────────────────────
        private RectTransform _graphRect;
        private Image _background;
        private RawImage _lineCanvas;
        private Texture2D _lineTexture;
        private Text _labelCompletion;
        private Text _labelRate;
        private Text _labelTemp;
        private Text _titleLabel;
        private bool _built;

        // ── Legend labels ──────────────────────────────────────
        private Text _legendCompletion;
        private Text _legendRate;
        private Text _legendTemp;

        private void OnEnable()
        {
            Events.EventBus.Subscribe<Events.ChemFxTriggeredEvent>(OnChemFx);
        }

        private void OnDisable()
        {
            Events.EventBus.Unsubscribe<Events.ChemFxTriggeredEvent>(OnChemFx);
        }

        private void OnChemFx(Events.ChemFxTriggeredEvent evt)
        {
            var s = evt.State;

            if (!s.Found || s.IsFailure)
            {
                _recording = false;
                return;
            }

            // New reaction: clear history
            if (!_recording)
            {
                ClearSamples();
                _recording = true;
                if (!_built) BuildUI();
                ShowGraph(true);
            }

            _completion = s.CompletionPercent;
            _rate = s.ReactionRate;
            _tempDelta = s.TemperatureDelta;
        }

        private void Update()
        {
            if (!_recording || !_built) return;

            _sampleTimer += Time.deltaTime;
            if (_sampleTimer >= SampleInterval)
            {
                _sampleTimer -= SampleInterval;
                AddSample(_completion / 100f, _rate, Mathf.Clamp01(Mathf.Abs(_tempDelta) / 100f));
            }

            DrawGraph();
            UpdateLabels();
        }

        // ════════════════════════════════════════════════════════
        //  DATA
        // ════════════════════════════════════════════════════════

        private void AddSample(float completion, float rate, float temp)
        {
            AddCapped(_completionSamples, completion);
            AddCapped(_rateSamples, rate);
            AddCapped(_tempSamples, temp);
        }

        private static void AddCapped(List<float> list, float value)
        {
            if (list.Count >= MaxSamples) list.RemoveAt(0);
            list.Add(value);
        }

        private void ClearSamples()
        {
            _completionSamples.Clear();
            _rateSamples.Clear();
            _tempSamples.Clear();
            _sampleTimer = 0f;
        }

        // ════════════════════════════════════════════════════════
        //  DRAW
        // ════════════════════════════════════════════════════════

        private void DrawGraph()
        {
            if (_lineTexture == null) return;

            int w = _lineTexture.width;
            int h = _lineTexture.height;

            // Clear
            var clear = new Color[w * h];
            for (int i = 0; i < clear.Length; i++) clear[i] = Color.clear;
            _lineTexture.SetPixels(clear);

            // Grid lines (horizontal at 25%, 50%, 75%)
            for (int g = 1; g <= 3; g++)
            {
                int y = Mathf.RoundToInt(h * g / 4f);
                DrawHLine(y, GridColor, w, h);
            }

            // Axis line at bottom
            DrawHLine(0, AxisColor, w, h);

            // Draw data lines
            DrawLine(_completionSamples, CompletionColor, w, h);
            DrawLine(_rateSamples, RateColor, w, h);
            DrawLine(_tempSamples, TempColor, w, h);

            _lineTexture.Apply();
        }

        private void DrawLine(List<float> samples, Color color, int w, int h)
        {
            if (samples.Count < 2) return;

            float xStep = (float)w / MaxSamples;

            for (int i = 1; i < samples.Count; i++)
            {
                int x0 = Mathf.RoundToInt((i - 1) * xStep);
                int y0 = Mathf.RoundToInt(samples[i - 1] * (h - 1));
                int x1 = Mathf.RoundToInt(i * xStep);
                int y1 = Mathf.RoundToInt(samples[i] * (h - 1));

                DrawSegment(x0, y0, x1, y1, color, w, h);
            }
        }

        /// <summary>Bresenham line segment.</summary>
        private void DrawSegment(int x0, int y0, int x1, int y1, Color color, int w, int h)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                SetPixelSafe(x0, y0, color, w, h);
                // Thicken line (1px above and below)
                SetPixelSafe(x0, y0 + 1, color * 0.7f, w, h);
                SetPixelSafe(x0, y0 - 1, color * 0.7f, w, h);

                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx)  { err += dx; y0 += sy; }
            }
        }

        private void DrawHLine(int y, Color color, int w, int h)
        {
            if (y < 0 || y >= h) return;
            for (int x = 0; x < w; x++)
                _lineTexture.SetPixel(x, y, color);
        }

        private void SetPixelSafe(int x, int y, Color color, int w, int h)
        {
            if (x >= 0 && x < w && y >= 0 && y < h)
                _lineTexture.SetPixel(x, y, color);
        }

        // ════════════════════════════════════════════════════════
        //  UI CONSTRUCTION
        // ════════════════════════════════════════════════════════

        private void BuildUI()
        {
            _built = true;

            // Root container
            _graphRect = CreateRect("ReactionGraph", transform);
            _graphRect.anchorMin = new Vector2(1, 0); // bottom-right
            _graphRect.anchorMax = new Vector2(1, 0);
            _graphRect.pivot = new Vector2(1, 0);
            _graphRect.anchoredPosition = new Vector2(-20f, 20f);
            _graphRect.sizeDelta = new Vector2(GraphWidth + 20f, GraphHeight + 44f);

            // Background
            var bgGo = CreateRect("BG", _graphRect);
            bgGo.anchorMin = Vector2.zero;
            bgGo.anchorMax = Vector2.one;
            bgGo.offsetMin = Vector2.zero;
            bgGo.offsetMax = Vector2.zero;
            _background = bgGo.gameObject.AddComponent<Image>();
            _background.color = BgColor;

            // Title
            _titleLabel = CreateLabel("Title", _graphRect, "REACTION PROGRESS", 11,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), TextAnchor.UpperCenter);
            _titleLabel.color = new Color(1f, 1f, 1f, 0.6f);

            // Line canvas (texture-based)
            int texW = Mathf.RoundToInt(GraphWidth);
            int texH = Mathf.RoundToInt(GraphHeight);
            _lineTexture = new Texture2D(texW, texH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var canvasRect = CreateRect("LineCanvas", _graphRect);
            canvasRect.anchorMin = new Vector2(0, 0);
            canvasRect.anchorMax = new Vector2(1, 1);
            canvasRect.offsetMin = new Vector2(10f, 24f);
            canvasRect.offsetMax = new Vector2(-10f, -20f);
            _lineCanvas = canvasRect.gameObject.AddComponent<RawImage>();
            _lineCanvas.texture = _lineTexture;
            _lineCanvas.uvRect = new Rect(0, 0, 1, 1);

            // Legend at bottom
            float legendY = 6f;
            _legendCompletion = CreateLabel("LegCompletion", _graphRect, "■ Completion",
                9, new Vector2(0, 0), new Vector2(0, 0), new Vector2(12f, legendY), TextAnchor.LowerLeft);
            _legendCompletion.color = CompletionColor;

            _legendRate = CreateLabel("LegRate", _graphRect, "■ Rate",
                9, new Vector2(0.35f, 0), new Vector2(0.35f, 0), new Vector2(0f, legendY), TextAnchor.LowerLeft);
            _legendRate.color = RateColor;

            _legendTemp = CreateLabel("LegTemp", _graphRect, "■ Temp",
                9, new Vector2(0.6f, 0), new Vector2(0.6f, 0), new Vector2(0f, legendY), TextAnchor.LowerLeft);
            _legendTemp.color = TempColor;

            // Value labels (right side)
            _labelCompletion = CreateLabel("ValCompletion", _graphRect, "0%",
                10, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4f, -20f), TextAnchor.UpperRight);
            _labelCompletion.color = CompletionColor;

            _labelRate = CreateLabel("ValRate", _graphRect, "0.0",
                10, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4f, -33f), TextAnchor.UpperRight);
            _labelRate.color = RateColor;

            _labelTemp = CreateLabel("ValTemp", _graphRect, "0°C",
                10, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4f, -46f), TextAnchor.UpperRight);
            _labelTemp.color = TempColor;
        }

        private void UpdateLabels()
        {
            if (_labelCompletion != null) _labelCompletion.text = $"{_completion:F0}%";
            if (_labelRate != null)       _labelRate.text       = $"{_rate:F2}";
            if (_labelTemp != null)       _labelTemp.text       = $"{_tempDelta:F1}°C";
        }

        private void ShowGraph(bool visible)
        {
            if (_graphRect != null) _graphRect.gameObject.SetActive(visible);
        }

        // ── UI helpers ─────────────────────────────────────────

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        private static Text CreateLabel(string name, RectTransform parent, string text,
            int fontSize, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, TextAnchor alignment)
        {
            var rt = CreateRect(name, parent);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = anchorMin;
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(120f, 16f);

            var t = rt.gameObject.AddComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            t.alignment = alignment;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.color = Color.white;
            return t;
        }

        private void OnDestroy()
        {
            if (_lineTexture != null) Destroy(_lineTexture);
        }
    }
}
