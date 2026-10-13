// ChemLabSim v3 — Contact model (single source for the contact factor)
// Grinding raises the surface area of solids only. Solutions, liquids and gases mix by
// diffusion, so without a solid reactant contact is never weak; stirring still speeds it up.
//   with a solid reactant : Lerp(0.6, 1.6, stirring) * Lerp(0.6, 1.6, grinding)   (0.36 … 2.56)
//   fluids only           : Lerp(1.0, 1.6, stirring)                              (1.0 … 1.6)

using UnityEngine;

namespace ChemLabSimV3.Engine
{
    public static class ContactModel
    {
        public static float Compute(ReactionEntry reaction, float stirring, float grinding)
        {
            stirring = Mathf.Clamp01(stirring);
            grinding = Mathf.Clamp01(grinding);

            if (!HasSolidReactant(reaction))
                return Mathf.Lerp(1.0f, 1.6f, stirring);

            return Mathf.Lerp(0.6f, 1.6f, stirring) * Mathf.Lerp(0.6f, 1.6f, grinding);
        }

        /// <summary>True when a reactant is a solid, or when the states are unknown (old flat records).</summary>
        public static bool HasSolidReactant(ReactionEntry reaction)
        {
            if (reaction?.reactants == null || reaction.reactants.Count == 0) return true;
            foreach (var r in reaction.reactants)
                if (r == null || string.IsNullOrEmpty(r.state) || r.state == "s") return true;
            return false;
        }
    }
}
