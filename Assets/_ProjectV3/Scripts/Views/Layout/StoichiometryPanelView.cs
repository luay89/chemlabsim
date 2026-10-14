// ChemLabSim v3 — "Amounts & limiting reactant" panel (Result tab)
// Shows what the engine computed from the student's amounts: moles of each reagent and
// how they were obtained, which reactant limits (or that the proportions are exact), what is
// left over, and the theoretical yield. When data is missing it says what is missing.
// Display only: every number comes from StoichiometryEvaluatedEvent.

using System.Globalization;
using System.Text;
using ChemLabSimV3.Data;
using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemLabSimV3.Views.Layout
{
    public class StoichiometryPanelView : MonoBehaviour
    {
        private TextMeshProUGUI body;

        public static StoichiometryPanelView Create(RectTransform list)
        {
            var go = new GameObject("StoichiometryPanel", typeof(RectTransform));
            go.layer = list.gameObject.layer;
            go.transform.SetParent(list, false);
            LabTheme.Round(go.AddComponent<Image>(), LabTheme.PanelColor);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 10, 12);
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var view = go.AddComponent<StoichiometryPanelView>();
            var title = view.AddText("PanelTitle", 13f, LabTheme.TitleColor);
            title.text = "AMOUNTS & LIMITING REACTANT";
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 4;
            view.body = view.AddText("Body", 15f, LabTheme.TextColor);
            go.SetActive(false);
            return view;
        }

        // Awake/OnDestroy: the panel lives in a tab that is often hidden.
        private void Awake()
        {
            EventBus.Subscribe<StoichiometryEvaluatedEvent>(OnEvaluated);
            EventBus.Subscribe<ReactionNotFoundEvent>(OnNotFound);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<StoichiometryEvaluatedEvent>(OnEvaluated);
            EventBus.Unsubscribe<ReactionNotFoundEvent>(OnNotFound);
        }

        private void OnNotFound(ReactionNotFoundEvent e) => gameObject.SetActive(false);

        private void OnEvaluated(StoichiometryEvaluatedEvent e)
        {
            gameObject.SetActive(e.Evaluation != null);
            if (e.Evaluation != null) body.text = Describe(e.Evaluation);
        }

        public static string Describe(AmountEvaluation evaluation)
        {
            var sb = new StringBuilder();
            var stoich = evaluation.Stoichiometry;

            foreach (var line in evaluation.Lines)
            {
                sb.Append("<b>").Append(ChemText.Formula(line.Input.Formula)).Append("</b>  ")
                  .Append(Number(line.Input.Value)).Append(' ').Append(line.Input.Unit);
                if (line.Input.Unit == "mL" && line.Input.StockConcentrationM > 0)
                    sb.Append(" of ").Append(line.Input.StockLabel);
                if (line.Moles.Success)
                {
                    sb.Append("  = ").Append(Moles(line.Moles.Moles));
                    if (stoich.Success) sb.Append(Role(stoich, line.Input.Formula));
                }
                else
                {
                    sb.Append("  <color=#FFBD47>").Append(line.Moles.Message).Append("</color>");
                }
                sb.Append('\n');
            }

            if (!stoich.Success)
            {
                if (evaluation.Lines.TrueForAll(l => l.Moles.Success))
                    sb.Append("\n<color=#FFBD47>").Append(stoich.Message).Append("</color>");
                return sb.ToString().TrimEnd();
            }

            sb.Append('\n').Append(stoich.IsStoichiometric
                ? "<color=#4DD980>Exact proportions: every reactant is used up.</color>"
                : $"<color=#4DD980>Limiting reactant: <b>{string.Join(", ", stoich.LimitingFormulas.ConvertAll(ChemText.Formula))}</b></color>");

            sb.Append("\n\n<b>Theoretical yield</b> (if the reaction goes to completion)\n");
            foreach (var p in stoich.Products)
            {
                sb.Append(ChemText.Formula(p.Formula));
                if (!string.IsNullOrEmpty(p.State)) sb.Append('(').Append(p.State).Append(')');
                sb.Append("  ").Append(Moles(p.TheoreticalMol));
                sb.Append(double.IsNaN(p.TheoreticalGrams) ? "  (no molar mass)" : $"  = {Grams(p.TheoreticalGrams)}");
                sb.Append('\n');
            }
            sb.Append("<size=85%><color=#9FB3C8>The 3D beaker animates the reaction in stoichiometric proportions; the numbers above use your amounts.</color></size>");
            return sb.ToString().TrimEnd();
        }

        private static string Role(LimitingReactantResult s, string formula)
        {
            var share = s.Reactants.Find(r => r.Formula == formula);
            if (share.Formula == null) return string.Empty;
            switch (share.Role)
            {
                case ReactantRole.Limiting:    return "  <color=#FF9F6B>limiting</color>";
                case ReactantRole.ExactlyUsed: return "  <color=#9FB3C8>used up</color>";
                default:                       return $"  <color=#9FB3C8>excess, {Moles(share.LeftOverMol)} left</color>";
            }
        }

        private static string Moles(double n)
            => n < 0.1 ? $"{Number(n * 1000.0)} mmol" : $"{Number(n)} mol";

        private static string Grams(double g)
            => g < 0.1 ? $"{Number(g * 1000.0)} mg" : $"{Number(g)} g";

        private static string Number(double v)
            => double.IsNaN(v) ? "?" : v.ToString(v >= 100 ? "0.#" : v >= 10 ? "0.##" : "0.###", CultureInfo.InvariantCulture);

        private TextMeshProUGUI AddText(string name, float size, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = colour;
            text.richText = true;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }
    }
}
