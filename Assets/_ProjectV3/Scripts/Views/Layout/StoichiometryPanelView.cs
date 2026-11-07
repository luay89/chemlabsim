// ChemLabSim v3 — "Amounts & limiting reactant" panel (Result tab)
// Shows what the engine computed from the student's amounts: moles of each reagent and
// how they were obtained, which reactant limits (or that the proportions are exact), what is
// left over, the theoretical yield, and the reaction heat (ΔH° by Hess's law, the heat for these
// amounts and, for a reaction in water, the temperature change). When data is missing it says
// what is missing.
// Display only: every number comes from StoichiometryEvaluatedEvent.

using System;
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
            if (e.Evaluation != null) body.text = Describe(e.Evaluation, e.Proceeded, e.Reaction);
        }

        public static string Describe(AmountEvaluation evaluation, bool proceeded = true, ReactionEntry reaction = null)
        {
            var sb = new StringBuilder();
            var stoich = evaluation.Stoichiometry;
            if (!proceeded)
                sb.Append("<color=#FFBD47>The reaction did not proceed under these conditions. ")
                  .Append("The numbers below are what it would give if it did.</color>\n\n");

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
            if (reaction != null) AppendHeat(sb, reaction, evaluation);
            sb.Append("<size=85%><color=#9FB3C8>The 3D beaker animates the reaction in stoichiometric proportions; the numbers above use your amounts.</color></size>");
            return sb.ToString().TrimEnd();
        }

        /// <summary>Reaction heat: ΔH° (Hess's law), the heat for the extent ξ, and the temperature change in water.</summary>
        private static void AppendHeat(StringBuilder sb, ReactionEntry reaction, AmountEvaluation evaluation)
        {
            sb.Append("\n<b>Reaction heat</b>\n");
            if (!Calorimetry.HasSourcedEnthalpy(reaction))
            {
                sb.Append("<color=#FFBD47>ΔH° not available: ").Append(Calorimetry.MissingEnthalpyReason(reaction)).Append("</color>\n\n");
                return;
            }
            double dh = reaction.enthalpyKJPerMol;
            sb.Append("ΔH° = ").Append(Signed(dh)).Append(" kJ per mol of reaction  ")
              .Append(dh < 0 ? "<color=#FF9F6B>exothermic: gives out heat</color>" : "<color=#6BC5FF>endothermic: takes in heat</color>")
              .Append('\n');

            double extent = evaluation.Stoichiometry.Extent;
            double q = Calorimetry.HeatReleasedKJ(reaction, extent);
            sb.Append("For your amounts (ξ = ").Append(Moles(extent)).Append("): ")
              .Append(Number(Math.Abs(q))).Append(q >= 0 ? " kJ given out" : " kJ taken in").Append('\n');

            if (Calorimetry.TryReaction(reaction, evaluation, out double rise, out string why))
            {
                sb.Append("Temperature change: <b>").Append(Signed(rise)).Append(" °C</b> (calculated, no heat loss)");
                if (!Calorimetry.StaysLiquid(25.0, rise, out string limit)) sb.Append("\n<color=#FFBD47>From 25 °C ").Append(limit).Append(".</color>");
                sb.Append('\n');
            }
            else
            {
                sb.Append("<color=#9FB3C8>Temperature change not calculated: ").Append(why).Append(".</color>\n");
            }
            sb.Append("<size=85%><color=#9FB3C8>Hess's law: ΔH° = Σ ΔfH°(products) − Σ ΔfH°(reactants), standard values at 25 °C for dilute solutions. ")
              .Append(reaction.enthalpySource.Replace("Hess's law from standard enthalpies of formation at 25 °C: ", "Sources: "))
              .Append(".</color></size>\n\n");
        }

        private static string Signed(double v) => (v > 0 ? "+" : v < 0 ? "−" : "") + Number(Math.Abs(v));

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
