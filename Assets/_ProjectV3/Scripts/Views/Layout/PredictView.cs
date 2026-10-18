// ChemLabSim v3 — optional "Predict" button next to Mix
// Mix works as always. Predict opens a card over the Lab Controls with three questions about the
// selected reagents (heat, new solid, gas). "Mix and check" presses Mix; once the reaction has had
// a few seconds to show itself, the card compares each answer with what happened (Prediction,
// from the engine's data) and says why. Close returns to the controls.
// Presenter only: the answers come from Prediction.Outcome; nothing here changes the chemistry.

using System.Collections;
using System.Linq;
using ChemLabSimV3.Controllers;
using ChemLabSimV3.Data;
using ChemLabSimV3.Engine.Assistant;
using ChemLabSimV3.Events;
using ChemLabSimV3.Views.Bench3D;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ChemLabSimV3.Views.Layout.UiKit;

namespace ChemLabSimV3.Views.Layout
{
    public class PredictView : MonoBehaviour
    {
        private const float RevealSeconds = 4f;   // let the pour and the first effects play first
        private static readonly Color Off = new Color(0.22f, 0.26f, 0.36f);
        private static readonly Color On = new Color(0.16f, 0.45f, 0.85f);
        private static readonly Color Right = new Color(0.30f, 0.85f, 0.50f);
        private static readonly Color Wrong = new Color(1f, 0.42f, 0.42f);

        private Button mixButton;
        private LabInputController input;
        private RectTransform card, questions;
        private TextMeshProUGUI intro, result;
        private Button checkButton;
        private Image[] heatButtons, solidButtons, gasButtons;

        private PredictionAnswers answers = new PredictionAnswers { Heat = HeatAnswer.None };
        private bool waiting;
        private ReactionEntry reaction;
        private bool proceeded, evaluated;
        private double riseK = double.NaN;

        /// <summary>The card's text (screenshot checks read it).</summary>
        public string CardText => card != null && card.gameObject.activeSelf ? intro.text + "\n" + result.text : string.Empty;
        public bool IsOpen => card != null && card.gameObject.activeSelf;

        /// <summary>Puts the Predict button next to Mix and builds the (hidden) card over the controls.</summary>
        public static PredictView Create(RectTransform controlsPanel)
        {
            var mixView = FindObjectOfType<MixButtonView>(true);
            var mix = mixView != null ? mixView.GetComponentInChildren<Button>(true) : null;
            if (controlsPanel == null || mix == null) return null;

            var view = controlsPanel.gameObject.AddComponent<PredictView>();
            view.mixButton = mix;
            view.input = FindObjectOfType<LabInputController>();

            // Mix keeps two thirds of its row; Predict takes the rest.
            var mixRect = (RectTransform)mix.transform;
            mixRect.anchorMax = new Vector2(0.64f, mixRect.anchorMax.y);
            var predict = new GameObject("PredictButton", typeof(RectTransform), typeof(Image));
            var pr = (RectTransform)predict.transform;
            pr.SetParent(mixRect.parent, false);
            pr.anchorMin = new Vector2(0.67f, mixRect.anchorMin.y);
            pr.anchorMax = new Vector2(1f, mixRect.anchorMax.y);
            pr.offsetMin = new Vector2(0f, mixRect.offsetMin.y);
            pr.offsetMax = new Vector2(mixRect.offsetMax.x, mixRect.offsetMax.y);
            LabTheme.Round(predict.GetComponent<Image>(), new Color(0.42f, 0.30f, 0.78f), 2f);
            var button = predict.AddComponent<Button>();
            button.targetGraphic = predict.GetComponent<Image>();
            button.onClick.AddListener(view.Open);
            var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            var lr = (RectTransform)label.transform;
            lr.SetParent(pr, false);
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(6f, 0f);
            lr.offsetMax = new Vector2(-6f, 0f);
            label.text = "Predict";
            label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 30f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;

            view.BuildCard(controlsPanel);
            return view;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ReactionEvaluatedEvent>(OnEvaluated);
            EventBus.Subscribe<ReactionNotFoundEvent>(OnNotFound);
            EventBus.Subscribe<StoichiometryEvaluatedEvent>(OnStoichiometry);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ReactionEvaluatedEvent>(OnEvaluated);
            EventBus.Unsubscribe<ReactionNotFoundEvent>(OnNotFound);
            EventBus.Unsubscribe<StoichiometryEvaluatedEvent>(OnStoichiometry);
        }

        // ── Card ───────────────────────────────────────────────────────

        private void BuildCard(RectTransform controlsPanel)
        {
            card = Panel(controlsPanel, "PredictCard", "PREDICT FIRST");
            card.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;   // floats over the controls
            card.anchorMin = Vector2.zero;
            card.anchorMax = Vector2.one;
            card.offsetMin = card.offsetMax = Vector2.zero;
            card.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(22, 22, 18, 18);
            card.GetComponent<VerticalLayoutGroup>().spacing = 12f;

            intro = Text(card, "Intro", 20f, Color.white);
            questions = new GameObject("Questions", typeof(RectTransform)).GetComponent<RectTransform>();
            questions.SetParent(card, false);
            var ql = questions.gameObject.AddComponent<VerticalLayoutGroup>();
            ql.spacing = 10f;
            ql.childControlWidth = ql.childControlHeight = true;
            ql.childForceExpandWidth = true;
            ql.childForceExpandHeight = false;

            Text(questions, "QHeat", 18f, LabTheme.TitleColor).text = "1. Heat: the mixture will…";
            heatButtons = Choices(questions, "Heat", new[] { "Warm up", "Cool down", "Hardly change" },
                i => { answers.Heat = (HeatAnswer)i; Mark(heatButtons, i); });
            Text(questions, "QSolid", 18f, LabTheme.TitleColor).text = "2. Will a new solid form?";
            solidButtons = Choices(questions, "Solid", new[] { "Yes", "No" },
                i => { answers.Solid = i == 0; Mark(solidButtons, i); });
            Text(questions, "QGas", 18f, LabTheme.TitleColor).text = "3. Will a gas be given off?";
            gasButtons = Choices(questions, "Gas", new[] { "Yes", "No" },
                i => { answers.Gas = i == 0; Mark(gasButtons, i); });

            result = Text(card, "Result", 18f, LabTheme.TextColor);
            // The card fills the controls panel: the spare height goes to this spacer, not to the rows.
            new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement)).transform.SetParent(card, false);
            card.Find("Spacer").GetComponent<LayoutElement>().flexibleHeight = 1f;
            var row = Row(card, "PredictButtons");
            Fixed(row, 52f);
            checkButton = AddButton(row, "PredictMix", "Mix and check", LabTheme.Accent, MixAndCheck);
            AddButton(row, "PredictClose", "Close", Off, Close);
            card.gameObject.SetActive(false);
        }

        private Image[] Choices(RectTransform parent, string name, string[] labels, System.Action<int> pick)
        {
            var row = Row(parent, name + "Choices");
            Fixed(row, 48f);
            var images = new Image[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                var b = AddButton(row, name + "_" + i, labels[i], Off, () => pick(index));
                images[i] = b.GetComponent<Image>();
                var t = b.GetComponentInChildren<TextMeshProUGUI>();
                t.fontSizeMax = 20f;
            }
            return images;
        }

        // A row whose layout forces its children's height counts as flexible; keep it at its height.
        private static void Fixed(RectTransform row, float height)
        {
            var le = row.GetComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleHeight = 0f;
        }

        private void Mark(Image[] buttons, int chosen)
        {
            for (int i = 0; i < buttons.Length; i++) buttons[i].color = i == chosen ? On : Off;
            checkButton.interactable = answers.Complete;
        }

        // ── Flow ───────────────────────────────────────────────────────

        /// <summary>Opens the questions for the reagents now selected (Predict button; screenshot tour).</summary>
        public void Open()
        {
            answers = new PredictionAnswers { Heat = HeatAnswer.None };
            foreach (var set in new[] { heatButtons, solidButtons, gasButtons })
                foreach (var img in set) img.color = Off;
            waiting = false;
            var c = input != null ? input.CurrentInput : default;
            string reagents = string.Join(" + ", new[] { c.ReagentA, c.ReagentB, c.ReagentC, c.ReagentD }
                .Where(r => !string.IsNullOrWhiteSpace(r)).Select(ChemText.Formula));
            intro.text = $"Before you mix <b>{(reagents.Length > 0 ? reagents : "nothing")}</b>, what do you think will happen?";
            result.text = string.Empty;
            questions.gameObject.SetActive(true);
            checkButton.gameObject.SetActive(true);
            checkButton.interactable = false;
            // Opaque, so the controls underneath do not show through (LabTheme styles panels translucent).
            card.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.14f, 1f);
            card.gameObject.SetActive(true);
            card.SetAsLastSibling();
        }

        /// <summary>Answers the questions (screenshot tour): heat 0–2, solid/gas yes = true.</summary>
        public void Answer(int heat, bool solid, bool gas)
        {
            answers.Heat = (HeatAnswer)heat; Mark(heatButtons, heat);
            answers.Solid = solid; Mark(solidButtons, solid ? 0 : 1);
            answers.Gas = gas; Mark(gasButtons, gas ? 0 : 1);
        }

        private void MixAndCheck()
        {
            if (!answers.Complete || mixButton == null) return;
            evaluated = false;
            reaction = null;
            riseK = double.NaN;
            waiting = true;
            questions.gameObject.SetActive(false);
            checkButton.gameObject.SetActive(false);
            result.text = "Watch the beaker…";
            mixButton.onClick.Invoke();
            StartCoroutine(Reveal());
        }

        private IEnumerator Reveal()
        {
            yield return new WaitForSecondsRealtime(RevealSeconds);
            if (!waiting) yield break;
            waiting = false;
            if (!evaluated) { result.text = "The mix did not give a result. Close and try again."; yield break; }

            var o = Prediction.Outcome(reaction, proceeded, riseK);
            int score = Prediction.Score(answers, o);
            result.text =
                Line("Heat", answers.Heat == o.Heat, Prediction.Text(answers.Heat), Prediction.Text(o.Heat), o.HeatWhy) +
                Line("New solid", answers.Solid == o.Solid, YesNo(answers.Solid), YesNo(o.Solid), o.SolidWhy) +
                Line("Gas", answers.Gas == o.Gas, YesNo(answers.Gas), YesNo(o.Gas), o.GasWhy) +
                $"\n<b>{score} of 3 right.</b> " + (score == 3 ? "Excellent prediction!" : "Look at the Result tab to see why.");
        }

        private static string Line(string what, bool right, string said, string actual, string why)
        {
            string mark = right ? $"<color=#{ColorUtility.ToHtmlStringRGB(Right)}>✓</color>" : $"<color=#{ColorUtility.ToHtmlStringRGB(Wrong)}>✗</color>";
            return $"{mark} <b>{what}</b>: you said {said}; it was {actual}.\n<size=85%><color=#9FB3C8>{why}</color></size>\n";
        }

        private static string YesNo(bool? v) => v == true ? "yes" : v == false ? "no" : "no answer";

        private void Close()
        {
            waiting = false;
            card.gameObject.SetActive(false);
        }

        // ── Engine results ─────────────────────────────────────────────

        private void OnEvaluated(ReactionEvaluatedEvent e)
        {
            if (!waiting) return;
            evaluated = true;
            reaction = e.Input.reaction;
            proceeded = e.Result.IsValid && e.Result.Status != ReactionStatus.Fail;
        }

        private void OnNotFound(ReactionNotFoundEvent e)
        {
            if (!waiting) return;
            evaluated = true;
            reaction = null;
            proceeded = false;
        }

        private void OnStoichiometry(StoichiometryEvaluatedEvent e)
        {
            if (!waiting) return;
            float start = input != null ? input.CurrentInput.Temperature : 25f;
            riseK = LabInstruments.RiseAfterMix(e, start, out _);
        }
    }
}
