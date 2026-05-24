// ChemLabSim v3 — Minimal Scientific Result View
// Clean, focused reaction result display driven by ChemistryProcessedEvent.
// Design: "minimal + scientific, no clutter, focus on reaction result"
//
// Layout (top to bottom):
//   1. Balanced equation (large, monospaced)
//   2. Status badge (completion %)
//   3. Key metrics: Limiting reagent, ΔH, Keq, Rate
//   4. Product chips (compact)
//   5. Condition indicators (small icons)
//
// All elements built at runtime on a Canvas overlay.

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Views
{
    public class ChemResultView : MonoBehaviour
    {
        // ── Colors ─────────────────────────────────────────────
        private static readonly Color PanelBg     = new Color(0.06f, 0.07f, 0.10f, 0.94f);
        private static readonly Color AccentGreen = new Color(0.20f, 0.80f, 0.45f);
        private static readonly Color AccentAmber = new Color(0.95f, 0.75f, 0.15f);
        private static readonly Color AccentRed   = new Color(0.90f, 0.25f, 0.20f);
        private static readonly Color TextPrimary = new Color(0.92f, 0.93f, 0.95f);
        private static readonly Color TextSecond  = new Color(0.60f, 0.62f, 0.68f);
        private static readonly Color DividerCol  = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color ChipBg      = new Color(0.15f, 0.16f, 0.22f, 0.9f);

        private const float PanelWidth = 400f;
        private const float Pad = 14f;

        // ── UI refs ────────────────────────────────────────────
        private RectTransform _panel;
        private Text _equationLabel;
        private Text _statusLabel;
        private Image _statusBadge;
        private Text _metricsLabel;
        private RectTransform _productContainer;
        private Text _conditionsLabel;
        private Text _summaryLabel;
        private bool _built;

        private readonly List<GameObject> _productChips = new List<GameObject>();

        private void OnEnable()
        {
            EventBus.Subscribe<ChemistryProcessedEvent>(OnResult);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChemistryProcessedEvent>(OnResult);
        }

        private void OnResult(ChemistryProcessedEvent evt)
        {
            if (!_built) BuildUI();
            Populate(evt.Output);
            _panel.gameObject.SetActive(true);
        }

        // ════════════════════════════════════════════════════════
        //  POPULATE
        // ════════════════════════════════════════════════════════

        private void Populate(ChemistryOutput output)
        {
            // Equation
            _equationLabel.text = !string.IsNullOrEmpty(output.BalancedEquation)
                ? output.BalancedEquation
                : "—";

            // Status badge
            float pct = output.CompletionPercent;
            Color badgeColor;
            string statusText;
            if (!output.Found)
            {
                badgeColor = AccentRed;
                statusText = "NO REACTION";
            }
            else if (pct >= 90f)
            {
                badgeColor = AccentGreen;
                statusText = $"COMPLETE {pct:F0}%";
            }
            else if (pct >= 30f)
            {
                badgeColor = AccentAmber;
                statusText = $"PARTIAL {pct:F0}%";
            }
            else
            {
                badgeColor = AccentRed;
                statusText = $"LOW YIELD {pct:F0}%";
            }
            _statusBadge.color = badgeColor;
            _statusLabel.text = statusText;

            // Metrics
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(output.LimitingReagent))
                sb.AppendLine($"Limiting: {output.LimitingReagent}");

            // Enthalpy
            string enthalpySign = output.EnthalpyKJ >= 0 ? "+" : "";
            string thermoType = output.IsExothermic ? "exothermic" : "endothermic";
            sb.AppendLine($"ΔH = {enthalpySign}{output.EnthalpyKJ:F1} kJ/mol ({thermoType})");

            // Equilibrium
            if (output.IsReversible)
            {
                sb.AppendLine($"Keq = {output.Keq:F2}  Extent = {output.EquilibriumExtent:F2}");
                if (!string.IsNullOrEmpty(output.EquilibriumShift))
                    sb.AppendLine($"Shift: {output.EquilibriumShift}");
            }

            // Rate
            sb.AppendLine($"Rate = {output.ConditionRate:F2} × {output.RateMultiplier:F2} (Arrhenius)");

            _metricsLabel.text = sb.ToString().TrimEnd();

            // Products
            ClearProductChips();
            if (output.Substances != null)
            {
                for (int i = 0; i < output.Substances.Count; i++)
                {
                    var s = output.Substances[i];
                    if (!s.IsProduct) continue;
                    CreateProductChip(s);
                }
            }

            // Conditions
            var csb = new StringBuilder();
            if (output.Conditions != null)
            {
                for (int i = 0; i < output.Conditions.Count; i++)
                {
                    var c = output.Conditions[i];
                    string icon = c.Passed ? "✓" : "✗";
                    csb.Append($" {icon} {c.Name}");
                    if (i < output.Conditions.Count - 1) csb.Append("  │");
                }
            }
            _conditionsLabel.text = csb.ToString();

            // Summary
            _summaryLabel.text = !string.IsNullOrEmpty(output.Summary) ? output.Summary : "";
        }

        // ════════════════════════════════════════════════════════
        //  BUILD UI
        // ════════════════════════════════════════════════════════

        private void BuildUI()
        {
            _built = true;

            // Panel (left side, vertically centered)
            _panel = MakeRect("ChemResultPanel", transform);
            _panel.anchorMin = new Vector2(0, 0.5f);
            _panel.anchorMax = new Vector2(0, 0.5f);
            _panel.pivot = new Vector2(0, 0.5f);
            _panel.anchoredPosition = new Vector2(16f, 0f);
            _panel.sizeDelta = new Vector2(PanelWidth, 380f);
            var panelImg = _panel.gameObject.AddComponent<Image>();
            panelImg.color = PanelBg;

            // Vertical layout group
            var vlg = _panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset((int)Pad, (int)Pad, (int)Pad, (int)Pad);
            vlg.spacing = 8f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            var fitter = _panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 1. Equation
            _equationLabel = MakeLabel("Equation", _panel, "—", 15, TextPrimary, FontStyle.Bold);

            // Divider
            MakeDivider("Div1", _panel);

            // 2. Status badge
            var badgeRect = MakeRect("StatusBadge", _panel);
            var le = badgeRect.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 28f;
            _statusBadge = badgeRect.gameObject.AddComponent<Image>();
            _statusBadge.color = AccentGreen;

            _statusLabel = MakeLabel("StatusText", badgeRect, "COMPLETE", 12, Color.white, FontStyle.Bold);
            _statusLabel.alignment = TextAnchor.MiddleCenter;
            var srt = _statusLabel.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;

            // 3. Metrics
            _metricsLabel = MakeLabel("Metrics", _panel, "", 12, TextSecond, FontStyle.Normal);
            _metricsLabel.lineSpacing = 1.2f;

            // Divider
            MakeDivider("Div2", _panel);

            // 4. Product container (horizontal flow)
            _productContainer = MakeRect("Products", _panel);
            var hlg = _productContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            var ple = _productContainer.gameObject.AddComponent<LayoutElement>();
            ple.preferredHeight = 26f;

            // Divider
            MakeDivider("Div3", _panel);

            // 5. Conditions
            _conditionsLabel = MakeLabel("Conditions", _panel, "", 10, TextSecond, FontStyle.Normal);

            // 6. Summary
            _summaryLabel = MakeLabel("Summary", _panel, "", 11, TextPrimary, FontStyle.Italic);

            _panel.gameObject.SetActive(false);
        }

        // ── Product chips ──────────────────────────────────────

        private void CreateProductChip(SubstanceState substance)
        {
            var rt = MakeRect("Chip_" + substance.Formula, _productContainer);
            var chipImg = rt.gameObject.AddComponent<Image>();
            chipImg.color = ChipBg;
            var cle = rt.gameObject.AddComponent<LayoutElement>();
            cle.preferredHeight = 22f;
            cle.minWidth = 50f;

            string phaseTag = substance.Phase switch
            {
                Phase.Gas     => "(g)",
                Phase.Liquid  => "(l)",
                Phase.Solid   => "(s)",
                Phase.Aqueous => "(aq)",
                _             => ""
            };

            string text = $" {substance.Formula}{phaseTag} {substance.MolesFinal:F2} mol ";
            var label = MakeLabel("ChipLabel", rt, text, 10, TextPrimary, FontStyle.Normal);
            label.alignment = TextAnchor.MiddleCenter;
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            _productChips.Add(rt.gameObject);
        }

        private void ClearProductChips()
        {
            for (int i = 0; i < _productChips.Count; i++)
                Destroy(_productChips[i]);
            _productChips.Clear();
        }

        // ── UI Helpers ─────────────────────────────────────────

        private static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        private static Text MakeLabel(string name, Transform parent, string text,
            int size, Color color, FontStyle style)
        {
            var rt = MakeRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.font = Font.CreateDynamicFontFromOSFont("Arial", size);
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.alignment = TextAnchor.UpperLeft;

            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;

            return t;
        }

        private static void MakeDivider(string name, Transform parent)
        {
            var rt = MakeRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = DividerCol;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 1f;
            le.flexibleWidth = 1f;
        }

        /// <summary>Hide the result panel.</summary>
        public void Hide()
        {
            if (_panel != null) _panel.gameObject.SetActive(false);
        }
    }
}
