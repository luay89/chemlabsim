// ChemLabSim v3 — "Learn" tab: guided experiments
// A list of experiments (Resources/Data/guided_experiments.json, in teaching order). Starting one
// shows its steps one at a time: what to do, a hint on request, and a live status from the real
// controls. When the lab shows what the step asks for, the step is done: the student reads what to
// look at and why it happens, then presses Next. Mix steps count only a mix made during the step
// and check the reaction the engine actually ran. Finishing shows the conclusion and ticks the
// experiment off (kept in PlayerPrefs on this device).
// Presenter only: the checks are GuidedExperimentRunner.Check, the chemistry is the engine's.

using System;
using System.Collections.Generic;
using System.Linq;
using ChemLabSimV3.Controllers;
using ChemLabSimV3.Domain.GuidedExperiments;
using ChemLabSimV3.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ChemLabSimV3.Views.Layout.UiKit;

namespace ChemLabSimV3.Views.Layout
{
    public class LearnView : MonoBehaviour
    {
        private const string CompletedKey = "chemlab.learn.completed";
        private static readonly Color DoneColor = new Color(0.30f, 0.85f, 0.50f);
        private static readonly Color WaitColor = new Color(1f, 0.74f, 0.28f);

        private GuidedExperimentRunner runner;
        private readonly HashSet<string> completed = new HashSet<string>();
        private LabInputController input;

        private RectTransform listRoot, runRoot;
        private readonly Dictionary<string, TextMeshProUGUI> cardStatus = new Dictionary<string, TextMeshProUGUI>();
        private TextMeshProUGUI runTitle, progress, instruction, status, hint, observe;
        private Image progressFill;
        private Button nextButton, hintButton, leaveButton;
        private TextMeshProUGUI nextLabel;

        private ExperimentSession session;
        private bool stepDone, mixedSinceStepStart;
        private string lastReactionId, lastStatus;

        /// <summary>Everything the run panel shows (screenshot checks read it).</summary>
        public string StepText => runRoot != null && runRoot.gameObject.activeSelf
            ? string.Join("\n", runTitle.text, progress.text, instruction.text, status.text, observe.text)
            : string.Empty;
        public string ListText => string.Join("\n", cardStatus.Select(kv => kv.Key + ": " + kv.Value.text));
        public bool Running => session != null;

        public static LearnView Create(RectTransform list)
        {
            var go = new GameObject("Learn", typeof(RectTransform));
            go.layer = list.gameObject.layer;
            go.transform.SetParent(list, false);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var view = go.AddComponent<LearnView>();
            view.Load();
            view.BuildList((RectTransform)go.transform);
            view.BuildRun((RectTransform)go.transform);
            view.ShowList();
            return view;
        }

        // Awake/OnDestroy: the panel lives in a tab that is often hidden.
        private void Awake()
        {
            EventBus.Subscribe<InputChangedEvent>(OnInput);
            EventBus.Subscribe<ReactionEvaluatedEvent>(OnEvaluated);
            EventBus.Subscribe<ReactionNotFoundEvent>(OnNotFound);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<InputChangedEvent>(OnInput);
            EventBus.Unsubscribe<ReactionEvaluatedEvent>(OnEvaluated);
            EventBus.Unsubscribe<ReactionNotFoundEvent>(OnNotFound);
        }

        // ── Data ───────────────────────────────────────────────────────

        private void Load()
        {
            var asset = Resources.Load<TextAsset>("Data/guided_experiments");
            var catalog = asset != null ? JsonUtility.FromJson<GuidedExperimentCatalog>(asset.text) : null;
            runner = new GuidedExperimentRunner(catalog?.experiments);
            try
            {
                foreach (var id in PlayerPrefs.GetString(CompletedKey, string.Empty).Split(','))
                    if (!string.IsNullOrEmpty(id)) completed.Add(id);
            }
            catch (Exception) { /* no saved progress: start fresh */ }
        }

        private void SaveCompleted()
        {
            try
            {
                PlayerPrefs.SetString(CompletedKey, string.Join(",", completed));
                PlayerPrefs.Save();
            }
            catch (Exception) { /* progress is a convenience; the lab works without it */ }
        }

        // ── List ───────────────────────────────────────────────────────

        private void BuildList(RectTransform parent)
        {
            listRoot = Panel(parent, "LearnList", "LEARN  ·  GUIDED EXPERIMENTS");
            Text(listRoot, "Intro", 15f, LabTheme.TextColor).text =
                "Short experiments that lead you step by step: do what the step says in the lab, " +
                "watch what happens, and find out why. Start at the top.";

            int n = 0;
            foreach (var e in runner.GetAll())
            {
                n++;
                var card = Panel(listRoot, "Card_" + e.id, string.Empty);
                card.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);
                var title = Text(card, "Title", 17f, Color.white);
                title.text = $"{n}. {e.title}";
                title.fontStyle = FontStyles.Bold;
                Text(card, "Summary", 14f, LabTheme.TextColor).text = e.summary;
                cardStatus[e.id] = Text(card, "Status", 13f, LabTheme.TitleColor);
                string id = e.id;
                AddButton(Row(card, "StartRow"), "Start_" + e.id, "Start", LabTheme.Accent, () => Begin(id));
            }
            if (n == 0) Text(listRoot, "Empty", 14f, WaitColor).text = "No guided experiments were found.";
            RefreshList();
        }

        private void RefreshList()
        {
            foreach (var e in runner.GetAll())
            {
                string stars = new string('●', Mathf.Clamp(e.difficultyLevel, 1, 5)) + new string('○', 5 - Mathf.Clamp(e.difficultyLevel, 1, 5));
                string line = $"{e.category}  ·  {stars}  ·  about {e.estimatedMinutes} min";
                if (completed.Contains(e.id)) line += "  ·  <color=#4DD980>✓ done</color>";
                else if (!runner.IsEligible(e.id, completed))
                    line += $"  ·  best after “{runner.Get(e.prerequisiteExperimentId)?.title}”";
                cardStatus[e.id].text = line;
            }
        }

        private void ShowList()
        {
            session = null;
            RefreshList();
            listRoot.gameObject.SetActive(true);
            runRoot.gameObject.SetActive(false);
        }

        // ── Running an experiment ──────────────────────────────────────

        private void BuildRun(RectTransform parent)
        {
            runRoot = Panel(parent, "LearnRun", "GUIDED EXPERIMENT");
            runTitle = Text(runRoot, "RunTitle", 19f, Color.white);
            runTitle.fontStyle = FontStyles.Bold;
            progress = Text(runRoot, "Progress", 13f, LabTheme.TitleColor);

            var bar = new GameObject("ProgressBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(runRoot, false);
            bar.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
            bar.AddComponent<LayoutElement>().preferredHeight = 6f;
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fr = (RectTransform)fill.transform;
            fr.SetParent(bar.transform, false);
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = new Vector2(0f, 1f);
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            progressFill = fill.GetComponent<Image>();
            progressFill.color = LabTheme.Accent;

            instruction = Text(runRoot, "Instruction", 18f, Color.white);
            status = Text(runRoot, "Status", 15f, WaitColor);
            hint = Text(runRoot, "Hint", 14f, LabTheme.TitleColor);
            observe = Text(runRoot, "Observe", 15f, LabTheme.TextColor);

            var row = Row(runRoot, "RunButtons");
            hintButton = AddButton(row, "LearnHint", "Hint", new Color(0.22f, 0.26f, 0.36f), () => hint.gameObject.SetActive(!hint.gameObject.activeSelf));
            nextButton = AddButton(row, "LearnNext", "Next", LabTheme.Accent, Next);
            nextLabel = nextButton.GetComponentInChildren<TextMeshProUGUI>();
            leaveButton = AddButton(row, "LearnLeave", "All experiments", new Color(0.22f, 0.26f, 0.36f), ShowList);
        }

        /// <summary>Starts an experiment (Start buttons; screenshot tour).</summary>
        public void Begin(string id)
        {
            session = runner.StartSession(id);
            if (session == null) return;
            listRoot.gameObject.SetActive(false);
            runRoot.gameObject.SetActive(true);
            runTitle.text = runner.Get(id).title;
            StartStep();
        }

        private void StartStep()
        {
            stepDone = false;
            mixedSinceStepStart = false;
            lastReactionId = lastStatus = null;
            hint.gameObject.SetActive(false);
            Refresh();
        }

        private void Next()
        {
            if (session == null) return;
            if (session.IsCompleted) { ShowList(); return; }
            var step = runner.GetCurrentStep(session);
            if (!stepDone && step.ActionType != StepActionType.Observe) return;
            if (runner.AdvanceStep(session)) { StartStep(); return; }

            completed.Add(session.ExperimentId);
            SaveCompleted();
            Refresh();
        }

        private void Refresh()
        {
            if (session == null) return;
            var def = runner.Get(session.ExperimentId);
            int total = def.steps.Count;

            if (session.IsCompleted)
            {
                progress.text = $"All {total} steps done";
                progressFill.rectTransform.anchorMax = Vector2.one;
                instruction.text = "<b>What you found out</b>";
                status.text = "✓ Experiment complete";
                status.color = DoneColor;
                observe.text = def.conclusion;
                hintButton.gameObject.SetActive(false);
                hint.gameObject.SetActive(false);
                nextLabel.text = "All experiments";
                nextButton.interactable = true;
                leaveButton.gameObject.SetActive(false);   // Next already leads back
                return;
            }

            leaveButton.gameObject.SetActive(true);
            var step = runner.GetCurrentStep(session);
            int index = session.CurrentStepIndex;
            progress.text = $"Step {index + 1} of {total}";
            progressFill.rectTransform.anchorMax = new Vector2((float)index / total, 1f);
            instruction.text = step.instruction;
            hint.text = "Hint: " + step.hint;
            hintButton.gameObject.SetActive(!string.IsNullOrEmpty(step.hint));

            bool observeStep = step.ActionType == StepActionType.Observe;
            if (!stepDone && !observeStep)
            {
                var check = GuidedExperimentRunner.Check(step, Snapshot());
                stepDone = check.Done;
                if (!stepDone)
                {
                    status.text = "Waiting: " + check.Waiting + ".";
                    status.color = WaitColor;
                }
            }
            if (stepDone || observeStep)
            {
                status.text = observeStep ? "Look and read, then press Next." : "✓ Done";
                status.color = DoneColor;
                observe.text = $"<b>Look:</b> {step.observe}\n<b>Why:</b> {step.explanation}";
            }
            else observe.text = string.Empty;

            nextLabel.text = index + 1 == total ? "Finish" : "Next";
            nextButton.interactable = stepDone || observeStep;
        }

        private LabSnapshot Snapshot()
        {
            if (input == null) input = FindObjectOfType<LabInputController>();
            var c = input != null ? input.CurrentInput : default;
            var reagents = new[] { c.ReagentA, c.ReagentB, c.ReagentC, c.ReagentD }
                .Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).ToArray();
            return new LabSnapshot
            {
                Reagents = reagents,
                TemperatureC = c.Temperature,
                Grinding = c.Grinding,
                Catalyst = c.HasCatalyst,
                Current = c.HasElectricCurrent,
                MixedSinceStepStart = mixedSinceStepStart,
                LastReactionId = lastReactionId,
                LastResultStatus = lastStatus,
            };
        }

        // ── Lab events ─────────────────────────────────────────────────

        private void OnInput(InputChangedEvent e) => Refresh();

        private void OnEvaluated(ReactionEvaluatedEvent e)
        {
            mixedSinceStepStart = true;
            lastReactionId = e.Input.reaction?.id;
            lastStatus = GuidedExperimentRunner.StatusOf(e.Result);
            Refresh();
        }

        private void OnNotFound(ReactionNotFoundEvent e)
        {
            mixedSinceStepStart = true;
            lastReactionId = null;
            lastStatus = "NotFound";
            Refresh();
        }
    }
}
