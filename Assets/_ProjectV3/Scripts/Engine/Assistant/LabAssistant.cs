// ChemLabSim v3 — Lab assistant (offline)
// Answers a student's questions about the experiment they just ran, using only the
// evaluated reaction and conditions (no network, no API keys). The ILabAssistant
// interface is the seam for a future online assistant: it receives the same LabContext.
//
// Answers are TMP rich text (formulas with sub/superscripts).

using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Engine.Assistant
{
    public enum AssistantTopic { WhatHappened, WhyThisResult, WhatToChange, Safety, TryNext }

    /// <summary>Everything the assistant knows about the current experiment.</summary>
    public struct LabContext
    {
        public bool HasExperiment;
        public bool Found;                       // false: no reaction matches the selected reagents
        public string NotFoundMessage;
        public IReadOnlyList<string> Reagents;   // formulas the student selected
        public ReactionEvaluationInput Input;
        public ReactionEvaluationResult Result;
        public IReadOnlyList<ReactionEntry> Database;   // for suggestions; may be null
    }

    public interface ILabAssistant
    {
        string Answer(AssistantTopic topic, LabContext context);
    }

    public class LocalLabAssistant : ILabAssistant
    {
        public static readonly IReadOnlyDictionary<AssistantTopic, string> Questions = new Dictionary<AssistantTopic, string>
        {
            { AssistantTopic.WhatHappened,  "What happened?" },
            { AssistantTopic.WhyThisResult, "Why this result?" },
            { AssistantTopic.WhatToChange,  "What should I change?" },
            { AssistantTopic.Safety,        "Is it safe?" },
            { AssistantTopic.TryNext,       "What can I try next?" },
        };

        public string Answer(AssistantTopic topic, LabContext c)
        {
            if (c.Found && c.Input.reaction == null) c.Found = false;
            if (!c.HasExperiment)
                return "Pick two reagents, set the conditions and press <b>Mix</b>. Then ask me about what you see.";

            switch (topic)
            {
                case AssistantTopic.WhatHappened:  return WhatHappened(c);
                case AssistantTopic.WhyThisResult: return Why(c);
                case AssistantTopic.WhatToChange:  return WhatToChange(c);
                case AssistantTopic.Safety:        return Safety(c);
                default:                           return TryNext(c);
            }
        }

        // ── Topics ─────────────────────────────────────────────────────

        private static string WhatHappened(LabContext c)
        {
            if (!c.Found)
                return $"{Reagents(c)} do not react with each other in this lab. {c.NotFoundMessage}".Trim();

            var r = c.Input.reaction;
            var sb = new StringBuilder();
            bool reacted = c.Result.IsValid && c.Result.Status != ReactionStatus.Fail;
            if (!reacted)
            {
                sb.Append($"Nothing visible happened: <b>{r.name_en}</b> did not start under these conditions. ");
                sb.Append("Ask <i>Why this result?</i> to see which condition was missing.");
                return sb.ToString();
            }

            sb.Append($"<b>{r.name_en}</b>");
            if (!string.IsNullOrEmpty(r.reactionType)) sb.Append($" ({r.reactionType.ToLowerInvariant()})");
            sb.Append($"\n{ChemText.Equation(r)}\n\n");

            foreach (var line in Observations(r)) sb.Append("• ").Append(line).Append('\n');
            if (!string.IsNullOrEmpty(r.observation_en)) sb.Append('\n').Append(r.observation_en);
            if (c.Result.Status == ReactionStatus.Partial)
                sb.Append("\n\nIt only ran <b>partly</b>, so less product formed than possible.");
            return sb.ToString().TrimEnd();
        }

        private static string Why(LabContext c)
        {
            if (!c.Found)
                return "There is no reaction between these substances in the lab's database. Some reactions also need a third reagent " +
                       "(an acid, a catalyst or water) — try adding one in slot C.";

            var r = c.Input.reaction;
            var res = c.Result;
            var sb = new StringBuilder();
            sb.Append(Status(res)).Append("\n\n");
            sb.Append(Check(!res.MediumMismatch, "Medium",
                MediumRequired(r) ? $"needs {r.requiredMedium}, you used {c.Input.medium}" : "any medium works"));
            sb.Append(Check(!res.ActivationNotReached && !res.LowTemperature, "Temperature",
                $"{c.Input.temperatureC:0} °C, needs ≥ {res.ActivationThresholdC:0} °C" +
                (res.CatalystApplied ? " (lowered by the catalyst)" : string.Empty)));
            sb.Append(Check(!res.LowContactQuality, "Contact",
                $"factor {res.ContactFactor:0.00}" + (ContactModel.HasSolidReactant(r) ? " (a solid reacts only at its surface)" : " (solutions mix by themselves)")));

            if (!string.IsNullOrEmpty(r.explanation_en))
                sb.Append('\n').Append(r.explanation_en);
            return sb.ToString().TrimEnd();
        }

        private static string WhatToChange(LabContext c)
        {
            if (!c.Found)
                return "Change at least one reagent. Ask <i>What can I try next?</i> for reactions that use what you picked.";

            var r = c.Input.reaction;
            var res = c.Result;
            var tips = new List<string>();
            if (res.MediumMismatch && MediumRequired(r))
                tips.Add($"Set <b>Medium</b> to <b>{r.requiredMedium}</b>.");
            if (res.ActivationNotReached || res.LowTemperature)
            {
                string tip = $"Raise the <b>temperature</b> to at least <b>{res.ActivationThresholdC:0} °C</b>.";
                if (r.catalystAllowed && !c.Input.hasCatalyst)
                    tip += $" Or turn on the <b>catalyst</b>: it lowers the activation temperature by {r.catalystDeltaTempC:0} °C.";
                tips.Add(tip);
            }
            if (res.LowContactQuality)
                tips.Add(ContactModel.HasSolidReactant(r)
                    ? "Increase <b>grinding</b> (more surface on the solid) and <b>stirring</b>."
                    : "Increase <b>stirring</b>.");

            if (tips.Count == 0)
                return "Nothing — every condition is right. Now experiment: change <b>one</b> thing at a time " +
                       $"(for example the temperature below {res.ActivationThresholdC:0} °C) and watch what changes.";
            return string.Join("\n", tips.Select(t => "• " + t));
        }

        private static string Safety(LabContext c)
        {
            if (!c.Found) return "No reaction took place, but always handle every chemical with gloves and goggles.";
            var r = c.Input.reaction;
            var sb = new StringBuilder();
            var ghs = r.safety?.ghs_icons;
            if (ghs != null && ghs.Count > 0) sb.Append("Hazard symbols: <b>").Append(string.Join(", ", ghs)).Append("</b>\n");
            var warnings = r.safety?.warnings_en;
            if (warnings != null)
                foreach (var w in warnings) sb.Append("• ").Append(w).Append('\n');
            if (!string.IsNullOrEmpty(r.safety_notes)) sb.Append(r.safety_notes).Append('\n');
            if (sb.Length == 0) sb.Append("Low hazard. Still wear goggles and gloves.\n");
            if (r.GetProducesGas()) sb.Append("A gas is released — work in a well-ventilated place.");
            return sb.ToString().TrimEnd();
        }

        private static string TryNext(LabContext c)
        {
            if (c.Database == null || c.Database.Count == 0)
                return "Try another pair of reagents from the lists.";

            var mine = new HashSet<string>(c.Reagents ?? new List<string>());
            string currentId = c.Found ? c.Input.reaction?.id : null;
            string currentType = c.Found ? c.Input.reaction?.reactionType : null;

            var picks = c.Database
                .Where(r => r != null && r.id != currentId && r.reactants != null && r.reactants.Count == 2)
                .Where(r => r.reactants.Any(x => mine.Contains(x.formula)))
                .OrderBy(r => r.reactionType == currentType ? 1 : 0)       // prefer a new kind of reaction
                .ThenBy(r => r.activationTempC)
                .Take(3)
                .ToList();

            if (picks.Count == 0)
                return "Try a classic: <b>AgNO<sub>3</sub> + NaCl</b> (white precipitate) or <b>Zn + HCl</b> (hydrogen gas).";

            var sb = new StringBuilder("Reactions that use what you picked:\n");
            foreach (var r in picks)
            {
                sb.Append("• <b>").Append(string.Join(" + ", r.reactants.Select(x => ChemText.Formula(x.formula)))).Append("</b> — ")
                  .Append(r.name_en);
                var hints = new List<string>();
                if (MediumRequired(r) && r.requiredMedium != "Neutral") hints.Add(r.requiredMedium.ToLowerInvariant() + " medium");
                if (r.activationTempC > 30f) hints.Add($"≥ {r.activationTempC:0} °C");
                if (hints.Count > 0) sb.Append(" (").Append(string.Join(", ", hints)).Append(')');
                sb.Append('\n');
            }
            return sb.ToString().TrimEnd();
        }

        // ── Helpers ────────────────────────────────────────────────────

        private static IEnumerable<string> Observations(ReactionEntry r)
        {
            var v = r.visual_effects;
            if (v == null) yield break;
            if (v.precipitate)
            {
                var solid = r.products?.FirstOrDefault(p => p.state == "s");
                yield return solid != null
                    ? $"A solid forms: {ChemText.Formula(solid.formula)} (a precipitate) settles to the bottom."
                    : "A solid precipitate forms and settles.";
            }
            var gas = r.products?.FirstOrDefault(p => p.state == "g");
            if (gas != null) yield return $"Bubbles: {ChemText.Formula(gas.formula)} gas is released.";
            if (!string.IsNullOrEmpty(v.color_change) && !v.precipitate) yield return "The colour of the liquid changes.";
            if (v.temperature_delta >= 2f) yield return $"It gives out heat (exothermic): about +{v.temperature_delta:0} °C.";
            else if (v.temperature_delta <= -2f) yield return $"It takes in heat (endothermic): about {v.temperature_delta:0} °C.";
            if (v.glow || v.sparks) yield return "It glows brightly — energy is released as light.";
            if (v.smoke) yield return "Smoke rises from the vessel.";
        }

        private static string Status(ReactionEvaluationResult res)
        {
            if (!res.IsValid || res.Status == ReactionStatus.Fail) return "<color=#FF6B6B><b>The reaction did not start.</b></color>";
            if (res.Status == ReactionStatus.Partial) return "<color=#FFBD47><b>The reaction ran only partly.</b></color>";
            return "<color=#4DD980><b>The reaction ran fully.</b></color>";
        }

        private static string Check(bool ok, string what, string detail)
            => (ok ? "<color=#4DD980>✓</color> " : "<color=#FF6B6B>✗</color> ") + $"<b>{what}</b>: {detail}\n";

        private static bool MediumRequired(ReactionEntry r) => !string.IsNullOrEmpty(r?.requiredMedium);

        private static string Reagents(LabContext c)
            => c.Reagents == null || c.Reagents.Count == 0
                ? "These reagents"
                : string.Join(" + ", c.Reagents.Select(ChemText.Formula));
    }
}
