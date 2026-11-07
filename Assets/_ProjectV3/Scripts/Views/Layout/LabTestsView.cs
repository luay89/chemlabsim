// ChemLabSim v3 — "Tests" tab: titration and flame test (Stage 6)
//
// Titration   beaker = reagent A (its volume from the amount row, in mL), burette = reagent B;
//             concentrations are the configured bench-stock bottles. The student adds titrant in
//             steps; every pH comes from TitrationModel, plotted on TitrationGraph. Unsupported
//             pairs show the model's reason and no numbers.
// Flame test  holds reagent A in the burner flame; colour and explanation from FlameTest.
// Changing reagent A/B or A's amount ends the titration, so no stale curve is ever shown.
// Display/presenter only — the chemistry lives in Engine/Chemistry.

using System.Collections.Generic;
using System.Globalization;
using ChemLabSimV3.Controllers;
using ChemLabSimV3.Data;
using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemLabSimV3.Views.Layout
{
    public class LabTestsView : MonoBehaviour
    {
        private static readonly double[] Steps = { 0.1, 1.0, 5.0 };

        private TextMeshProUGUI titrationSetup, titrationReadout, flameResult, flameNote;
        private TitrationGraph graph;
        private Button[] addButtons;
        private Image flameSwatch;
        private LabInputController input;

        private TitrationModel model;
        private int indicator = -1;                    // index in Indicators.All, −1 = none
        private TextMeshProUGUI indicatorLabel;
        private readonly List<TitrationPoint> taken = new List<TitrationPoint>();
        private double addedMl;
        private string setupSignature;

        public string TitrationText => titrationReadout != null ? titrationReadout.text : string.Empty;
        public string FlameText => flameResult != null ? flameResult.text : string.Empty;

        public static LabTestsView Create(RectTransform list)
        {
            var go = new GameObject("LabTests", typeof(RectTransform));
            go.layer = list.gameObject.layer;
            go.transform.SetParent(list, false);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var view = go.AddComponent<LabTestsView>();
            view.BuildTitration((RectTransform)go.transform);
            view.BuildFlameTest((RectTransform)go.transform);
            view.ClearTitration("Choose an acid as reagent A (with its volume in mL) and a base as reagent B, or the other way round, then press Set up.");
            view.flameResult.text = "Choose a salt as reagent A, then hold it in the flame.";
            return view;
        }

        // Awake/OnDestroy: the panel lives in a tab that is often hidden.
        private void Awake()
        {
            EventBus.Subscribe<InputChangedEvent>(OnInputChanged);
            EventBus.Subscribe<ReactionEvaluatedEvent>(OnMix);
            EventBus.Subscribe<ReactionNotFoundEvent>(OnMixNotFound);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<InputChangedEvent>(OnInputChanged);
            EventBus.Unsubscribe<ReactionEvaluatedEvent>(OnMix);
            EventBus.Unsubscribe<ReactionNotFoundEvent>(OnMixNotFound);
        }

        private void OnInputChanged(InputChangedEvent e)
        {
            // Also clears an "unsupported" message that was about the previous reagents.
            if (setupSignature != null && Signature() != setupSignature)
                ClearTitration("The reagents or the volume changed. Press Set up to start a new titration.");
        }

        // A mix replaces the beaker's contents: the titration is over.
        private void OnMix(ReactionEvaluatedEvent e) { if (setupSignature != null) ClearTitration("The beaker was used for a mix. Press Set up to titrate again."); }
        private void OnMixNotFound(ReactionNotFoundEvent e) { if (setupSignature != null) ClearTitration("The beaker was used for a mix. Press Set up to titrate again."); }

        // ── Titration ──────────────────────────────────────────────────

        private void BuildTitration(RectTransform parent)
        {
            var panel = Panel(parent, "TitrationPanel", "TITRATION  ·  STRONG ACID / STRONG BASE");
            titrationSetup = Text(panel, "Setup", 14f, LabTheme.TextColor);

            var row = Row(panel, "TitrationButtons");
            AddButton(row, "TitrationSetup", "Set up", LabTheme.Accent, SetUp);
            addButtons = new Button[Steps.Length];
            for (int i = 0; i < Steps.Length; i++)
            {
                double step = Steps[i];
                addButtons[i] = AddButton(row, "Add" + step.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', '_'),
                                          "+" + step.ToString("0.#", CultureInfo.InvariantCulture) + " mL", LabTheme.ControlColor, () => Add(step));
            }

            var indicatorRow = Row(panel, "IndicatorRow");
            var indicatorButton = AddButton(indicatorRow, "IndicatorButton", "Indicator: none", LabTheme.ControlColor, NextIndicator);
            indicatorLabel = indicatorButton.GetComponentInChildren<TextMeshProUGUI>();

            titrationReadout = Text(panel, "Readout", 15f, LabTheme.TextColor);

            var graphGo = new GameObject("TitrationGraph", typeof(RectTransform));
            graphGo.layer = panel.gameObject.layer;
            graphGo.transform.SetParent(panel, false);
            graphGo.AddComponent<LayoutElement>().preferredHeight = 170f;
            graph = graphGo.AddComponent<TitrationGraph>();
            graph.raycastTarget = false;

            var note = Text(panel, "Scope", 11.5f, LabTheme.MutedText);
            note.text = "Model: complete reaction at 25 °C, ideal solution, volumes additive, Kw = 1.0 × 10⁻¹⁴. " +
                        "y axis pH 0–14, dashed line = equivalence volume. Weak acids, buffers and H2SO4 are not modelled. " +
                        "Indicator ranges: " + Indicators.Source + " (colours illustrative).";
        }

        private void SetUp()
        {
            if (input == null) input = FindObjectOfType<LabInputController>();
            var s = new TitrationSetup { AnalyteMolar = double.NaN, AnalyteMl = double.NaN, TitrantMolar = double.NaN };
            if (input != null)
            {
                s.Analyte = input.CurrentInput.ReagentA?.Trim();
                s.Titrant = input.CurrentInput.ReagentB?.Trim();
                if (input.TryGetAmount(0, out double v, out string unit) && unit == "mL") s.AnalyteMl = v;
                if (input.TryGetStockConcentration(s.Analyte, out double ca)) s.AnalyteMolar = ca;
                if (input.TryGetStockConcentration(s.Titrant, out double ct)) s.TitrantMolar = ct;
            }

            var m = TitrationModel.Create(s);
            if (!m.Supported)
            {
                string reason = double.IsNaN(s.AnalyteMl) && m.Reason.Contains("volume")
                    ? "measure reagent A in mL so the volume in the beaker is known" : m.Reason;
                ClearTitration("<color=#FFBD47>Not supported: " + reason + "</color>");
                setupSignature = Signature();
                EventBus.Publish(new TitrationUpdatedEvent { Active = true, Supported = false, Message = reason, Indicator = -1 });
                return;
            }

            model = m;
            setupSignature = Signature();
            addedMl = 0;
            taken.Clear();
            double range = System.Math.Ceiling(2.0 * m.EquivalenceMl);
            graph.SetModel(m.Curve(range, range / 200.0), range, m.EquivalenceMl);
            titrationSetup.text =
                $"Beaker: <b>{ChemText.Formula(s.Analyte)}</b> {N(s.AnalyteMl)} mL of {N(s.AnalyteMolar)} M (bench stock)\n" +
                $"Burette: <b>{ChemText.Formula(s.Titrant)}</b> {N(s.TitrantMolar)} M (bench stock)\n" +
                $"Equivalence point at <b>{m.EquivalenceMl.ToString("0.00", CultureInfo.InvariantCulture)} mL</b>";
            foreach (var b in addButtons) b.interactable = true;
            Record();
        }

        private void NextIndicator()
        {
            indicator = indicator + 1 >= Indicators.All.Length ? -1 : indicator + 1;
            indicatorLabel.text = "Indicator: " + (indicator < 0 ? "none" : Indicators.All[indicator].Name);
            if (model != null && taken.Count > 0) Show(taken[taken.Count - 1]);
        }

        private void Add(double ml)
        {
            if (model == null) return;
            if (addedMl >= graph.MaxMl - 1e-9)
            {
                titrationReadout.text += "\n<color=#FFBD47>End of the plotted range.</color>";
                return;
            }
            addedMl = System.Math.Min(System.Math.Round(addedMl + ml, 6), graph.MaxMl);
            Record();
        }

        private void Record()
        {
            if (!model.TryPointAt(addedMl, out var p, out string error))
            {
                ClearTitration("<color=#FFBD47>" + error + "</color>");
                return;
            }
            taken.Add(p);
            graph.SetTaken(taken);
            Show(p);
        }

        private void Show(TitrationPoint p)
        {
            titrationReadout.text = Readout(p, model);
            if (indicator >= 0)
            {
                var ind = Indicators.All[indicator];
                titrationReadout.text += $"\n{char.ToUpperInvariant(ind.Name[0])}{ind.Name.Substring(1)}: <b>{Indicators.ColourNameAt(ind, p.Ph)}</b>" +
                                         $"  <size=85%>(changes at pH {N(ind.LowPh)}–{N(ind.HighPh)})</size>";
            }
            EventBus.Publish(new TitrationUpdatedEvent
            {
                Active = true, Supported = true, Point = p, EquivalenceMl = model.EquivalenceMl,
                Analyte = model.Setup.Analyte, Titrant = model.Setup.Titrant, AnalyteMl = model.Setup.AnalyteMl,
                Indicator = indicator
            });
        }

        public static string Readout(TitrationPoint p, TitrationModel m)
        {
            string region;
            switch (p.Region)
            {
                case TitrationRegion.Start:            region = "no titrant added yet"; break;
                case TitrationRegion.Equivalence:      region = "<color=#FFBD47>equivalence point</color>: all the " + (m.AcidInBeaker ? "acid" : "base") + " is neutralised"; break;
                case TitrationRegion.BeforeEquivalence: region = "before the equivalence point: excess " + (m.AcidInBeaker ? "acid" : "base"); break;
                default:                               region = "after the equivalence point: excess " + (m.AcidInBeaker ? "base" : "acid"); break;
            }
            return $"Added <b>{p.AddedMl.ToString("0.00", CultureInfo.InvariantCulture)} mL</b>   pH <b>{p.Ph.ToString("0.00", CultureInfo.InvariantCulture)}</b>" +
                   (p.Approximate ? "  <size=85%>(approx., concentrated)</size>" : "  <size=85%>(calculated)</size>") +
                   "\n" + region;
        }

        private void ClearTitration(string message)
        {
            bool wasActive = setupSignature != null;
            model = null;
            setupSignature = null;
            taken.Clear();
            graph.Clear();
            titrationSetup.text = message;
            titrationReadout.text = string.Empty;
            foreach (var b in addButtons) b.interactable = false;
            if (wasActive) EventBus.Publish(new TitrationUpdatedEvent { Active = false });
        }

        private string Signature()
        {
            if (input == null) input = FindObjectOfType<LabInputController>();
            if (input == null) return string.Empty;
            input.TryGetAmount(0, out double v, out string unit);
            return $"{input.CurrentInput.ReagentA}|{input.CurrentInput.ReagentB}|{v.ToString(CultureInfo.InvariantCulture)}|{unit}";
        }

        // ── Flame test ─────────────────────────────────────────────────

        private void BuildFlameTest(RectTransform parent)
        {
            var panel = Panel(parent, "FlameTestPanel", "FLAME TEST");
            var row = Row(panel, "FlameRow");
            AddButton(row, "FlameButton", "Hold reagent A in the flame", LabTheme.Accent, RunFlameTest);
            var swatch = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
            swatch.transform.SetParent(row, false);
            var se = swatch.AddComponent<LayoutElement>();
            se.preferredWidth = se.minWidth = 34f;
            flameSwatch = swatch.GetComponent<Image>();
            LabTheme.Round(flameSwatch, new Color(0f, 0f, 0f, 0f), 2f);

            flameResult = Text(panel, "Result", 15f, LabTheme.TextColor);
            flameNote = Text(panel, "Note", 11.5f, LabTheme.MutedText);
            flameNote.text = "A salt on a clean wire loop in a hot burner flame. Colours: " + FlameTest.Source +
                             ". On screen: " + FlameTest.Fidelity + ". Done by a teacher with eye protection.";
        }

        private void RunFlameTest()
        {
            if (input == null) input = FindObjectOfType<LabInputController>();
            string sample = input != null ? input.CurrentInput.ReagentA : null;
            var result = FlameTest.Test(sample);
            switch (result.Status)
            {
                case FlameTestStatus.Characteristic:
                    flameSwatch.color = new Color(result.Entry.R, result.Entry.G, result.Entry.B);
                    flameResult.text = $"<b>{ChemText.Formula(result.Formula)}</b>: <b>{result.Entry.ColourName}</b> flame ({result.Entry.Ion})\n{result.Explanation}";
                    break;
                case FlameTestStatus.NoColour:
                    flameSwatch.color = new Color(0.35f, 0.55f, 1f, 0.6f);
                    flameResult.text = $"<b>{ChemText.Formula(result.Formula)}</b>: no characteristic colour\n{result.Explanation}";
                    break;
                default:
                    flameSwatch.color = new Color(0f, 0f, 0f, 0f);
                    flameResult.text = "<color=#FFBD47>Not supported: " + result.Explanation + "</color>";
                    break;
            }
            EventBus.Publish(new FlameTestEvent { Result = result });
        }

        // ── Building blocks ────────────────────────────────────────────

        private static RectTransform Panel(RectTransform parent, string name, string title)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            LabTheme.Round(go.AddComponent<Image>(), LabTheme.PanelColor);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 10, 12);
            layout.spacing = 8f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var t = Text((RectTransform)go.transform, "PanelTitle", 13f, LabTheme.TitleColor);
            t.text = title;
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 4;
            return (RectTransform)go.transform;
        }

        private static RectTransform Row(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = 34f;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return (RectTransform)go.transform;
        }

        private static Button AddButton(RectTransform row, string name, string label, Color colour, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = row.gameObject.layer;
            go.transform.SetParent(row, false);
            var image = go.GetComponent<Image>();
            LabTheme.Round(image, colour, 2f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            var text = new GameObject("Label", typeof(RectTransform));
            var tr = (RectTransform)text.transform;
            tr.SetParent(go.transform, false);
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(4f, 0f);
            tr.offsetMax = new Vector2(-4f, 0f);
            var tmp = text.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 14f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 10f;
            tmp.fontSizeMax = 14f;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return button;
        }

        private static TextMeshProUGUI Text(RectTransform parent, string name, float size, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.color = colour;
            t.richText = true;
            t.enableWordWrapping = true;
            t.raycastTarget = false;
            return t;
        }

        private static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
