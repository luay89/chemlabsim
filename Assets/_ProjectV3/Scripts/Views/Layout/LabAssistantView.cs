// ChemLabSim v3 — Lab assistant panel (Assistant tab)
// Question chips + an answer about the student's current experiment. After every mix it
// answers "What happened?" on its own. Answers come from an ILabAssistant (offline today).

using System.Collections.Generic;
using ChemLabSimV3.Controllers;
using ChemLabSimV3.Engine.Assistant;
using ChemLabSimV3.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemLabSimV3.Views.Layout
{
    public class LabAssistantView : MonoBehaviour
    {
        private readonly ILabAssistant assistant = new LocalLabAssistant();
        private readonly Dictionary<AssistantTopic, Image> chips = new Dictionary<AssistantTopic, Image>();
        private TextMeshProUGUI questionText, answerText;
        private LabContext context;

        public static LabAssistantView Create(RectTransform list)
        {
            var panel = NewRect("AssistantPanel", list);
            var view = panel.gameObject.AddComponent<LabAssistantView>();
            LabTheme.Round(panel.gameObject.AddComponent<Image>(), LabTheme.PanelColor);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 10, 14);
            layout.spacing = 8f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = view.AddText(panel, "PanelTitle", 13, LabTheme.TitleColor);
            title.text = "LAB ASSISTANT";
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 4;

            view.questionText = view.AddText(panel, "Question", 15, LabTheme.TitleColor);
            view.questionText.fontStyle = FontStyles.Bold;
            view.answerText = view.AddText(panel, "Answer", 16, LabTheme.TextColor);

            var grid = NewRect("Questions", panel);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(196f, 40f);
            g.spacing = new Vector2(8f, 8f);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 2;
            foreach (var pair in LocalLabAssistant.Questions)
                view.AddChip(grid, pair.Key, pair.Value);

            view.context = new LabContext();
            view.Ask(AssistantTopic.WhatHappened);
            return view;
        }

        /// <summary>Fits the two chip columns to the panel width.</summary>
        public void SetWidth(float width)
        {
            var grid = transform.Find("Questions")?.GetComponent<GridLayoutGroup>();
            if (grid == null) return;
            float cell = Mathf.Floor((width - 28f - grid.spacing.x) / 2f);
            grid.cellSize = new Vector2(Mathf.Max(120f, cell), grid.cellSize.y);
        }

        // Awake/OnDestroy, not OnEnable/OnDisable: the panel sits in a tab that is hidden most
        // of the time, and it must still follow every experiment.
        private void Awake()
        {
            EventBus.Subscribe<ReactionEvaluatedEvent>(OnEvaluated);
            EventBus.Subscribe<ReactionNotFoundEvent>(OnNotFound);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ReactionEvaluatedEvent>(OnEvaluated);
            EventBus.Unsubscribe<ReactionNotFoundEvent>(OnNotFound);
        }

        private void OnEvaluated(ReactionEvaluatedEvent e)
        {
            context = NewContext();
            context.Found = e.Input.reaction != null;
            context.Input = e.Input;
            context.Result = e.Result;
            Ask(AssistantTopic.WhatHappened);
        }

        private void OnNotFound(ReactionNotFoundEvent e)
        {
            context = NewContext();
            context.Found = false;
            context.NotFoundMessage = e.Message;
            Ask(AssistantTopic.WhatHappened);
        }

        private LabContext NewContext()
        {
            var reagents = new List<string>();
            bool current = false;
            var input = FindObjectOfType<LabInputController>();
            if (input != null)
            {
                var vm = input.CurrentInput;
                current = vm.HasElectricCurrent;
                foreach (var f in new[] { vm.ReagentA, vm.ReagentB, vm.ReagentC, vm.ReagentD })
                    if (!string.IsNullOrWhiteSpace(f)) reagents.Add(f.Trim());
            }
            return new LabContext
            {
                HasExperiment = true,
                HasElectricCurrent = current,
                Reagents = reagents,
                Database = AppManager.Instance != null ? AppManager.Instance.ReactionDatabase?.reactions : null
            };
        }

        private void Ask(AssistantTopic topic)
        {
            questionText.text = LocalLabAssistant.Questions[topic];
            answerText.text = assistant.Answer(topic, context);
            foreach (var pair in chips)
                pair.Value.color = pair.Key == topic ? LabTheme.Accent : LabTheme.ControlColor;
        }

        // ── Building blocks ────────────────────────────────────────────

        private void AddChip(RectTransform parent, AssistantTopic topic, string label)
        {
            var rect = NewRect(topic + "Chip", parent);
            var image = rect.gameObject.AddComponent<Image>();
            LabTheme.Round(image, LabTheme.ControlColor, 2.5f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => Ask(topic));
            chips[topic] = image;

            var text = AddText(rect, "Label", 14, Color.white);
            var tr = text.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(8f, 0f);
            tr.offsetMax = new Vector2(-8f, 0f);
            text.alignment = TextAlignmentOptions.Center;
            text.text = label;
            text.raycastTarget = false;
        }

        private TextMeshProUGUI AddText(RectTransform parent, string name, float size, Color color)
        {
            var rect = NewRect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = color;
            text.enableWordWrapping = true;
            text.richText = true;
            text.raycastTarget = false;
            return text;
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
