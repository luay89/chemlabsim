// ChemLabSim v3 — reaction headline above the beaker
// After each mix: a status chip, the reaction name and its balanced equation in large type,
// centred over the 3D beaker (like an equation written on the board during a demo).

using ChemLabSimV3.Data;
using ChemLabSimV3.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemLabSimV3.Views.Layout
{
    public class ReactionHeadline : MonoBehaviour
    {
        private TextMeshProUGUI status, title, equation;
        private CanvasGroup group;
        private float targetAlpha;

        public static ReactionHeadline Create(RectTransform parent)
        {
            var go = new GameObject("ReactionHeadline", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(900f, 140f);

            var headline = go.AddComponent<ReactionHeadline>();
            headline.group = go.AddComponent<CanvasGroup>();
            headline.group.alpha = 0f;
            headline.group.blocksRaycasts = false;
            headline.group.interactable = false;

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 2f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            headline.status   = headline.AddText("Status", 14, FontStyles.Bold, LabTheme.Success);
            headline.status.characterSpacing = 5;
            headline.title    = headline.AddText("Title", 28, FontStyles.Bold, LabTheme.TextColor);
            headline.equation = headline.AddText("Equation", 26, FontStyles.Normal, LabTheme.TextColor);
            return headline;
        }

        private TextMeshProUGUI AddText(string name, float size, FontStyles style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            text.richText = true;
            // Readable over the 3D scene without a panel behind it.
            text.outlineWidth = 0.15f;
            text.outlineColor = new Color32(0, 0, 0, 160);
            return text;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ReactionEvaluatedEvent>(OnEvaluated);
            EventBus.Subscribe<ReactionNotFoundEvent>(OnNotFound);
            EventBus.Subscribe<MaterialPreviewChangedEvent>(OnNewReagent);
            EventBus.Subscribe<TitrationUpdatedEvent>(OnTitration);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ReactionEvaluatedEvent>(OnEvaluated);
            EventBus.Unsubscribe<ReactionNotFoundEvent>(OnNotFound);
            EventBus.Unsubscribe<MaterialPreviewChangedEvent>(OnNewReagent);
            EventBus.Unsubscribe<TitrationUpdatedEvent>(OnTitration);
        }

        // A new reagent A starts a new experiment: the last reaction's headline no longer applies.
        private void OnNewReagent(MaterialPreviewChangedEvent e) => targetAlpha = 0f;

        // Titration: the net ionic equation of every strong acid + strong base neutralisation.
        private void OnTitration(TitrationUpdatedEvent e)
        {
            if (!e.Active || !e.Supported) { targetAlpha = 0f; return; }
            if (e.Point.Region != Engine.Chemistry.TitrationRegion.Start) return;   // set up once, not on every addition
            Show("TITRATION", LabTheme.Accent,
                 $"{ChemText.Formula(e.Analyte)} titrated with {ChemText.Formula(e.Titrant)}",
                 "H<sup>+</sup>(aq) + OH<sup>−</sup>(aq) → H<sub>2</sub>O(l)");
        }

        private void Update()
        {
            group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.unscaledDeltaTime * 3f);   // also while paused
        }

        private void OnEvaluated(ReactionEvaluatedEvent e)
        {
            var reaction = e.Input.reaction;
            bool valid = e.Result.IsValid;
            var s = e.Result.Status;

            string label; Color color;
            if (!valid || s == ReactionStatus.Fail) { label = "NO REACTION UNDER THESE CONDITIONS"; color = LabTheme.Fail; }
            else if (s == ReactionStatus.Partial)   { label = "PARTIAL REACTION"; color = LabTheme.Partial; }
            else                                    { label = "REACTION"; color = LabTheme.Success; }

            Show(label, color,
                 reaction != null && !string.IsNullOrEmpty(reaction.name_en) ? reaction.name_en : string.Empty,
                 ChemText.Equation(reaction, withStates: true));
        }

        private void OnNotFound(ReactionNotFoundEvent e)
            => Show("NO REACTION", LabTheme.Fail, e.Message ?? string.Empty, string.Empty);

        private void Show(string label, Color color, string name, string eq)
        {
            status.text = label;
            status.color = color;
            title.text = name;
            title.fontSize = name.Length > 48 ? 20 : 28;
            equation.text = eq;
            equation.gameObject.SetActive(!string.IsNullOrEmpty(eq));
            group.alpha = 0f;
            targetAlpha = 1f;
        }
    }
}
