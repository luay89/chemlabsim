using System;
using System.Collections.Generic;

[Serializable]
public class ReactionDB
{
    public List<ReactionEntry> reactions = new List<ReactionEntry>();
}

[Serializable]
public class ReactionEntry
{
    public string id;
    public string name_ar;
    public string name_en;

    // Flat schema (runtime-friendly)
    public string reactantA;
    public string reactantB;
    public string product;

    // Rich schema (source-friendly)
    public List<ReactionChemical> reactants = new List<ReactionChemical>();
    public List<ReactionChemical> products = new List<ReactionChemical>();
    public ReactionVisualEffects visual_effects;
    public ReactionSafety safety;
    public ReactionValidation validation;

    // Educational controls
    public bool producesGas;
    public float activationTempC = 25f;
    public string requiredMedium;
    public bool catalystAllowed = false;
    public float catalystDeltaTempC = 10f;

    // Scientific output enrichment
    public string reactionType;           // Neutralization, Precipitation, Redox, GasEvolution, Decomposition, Combustion, Catalytic, Thermal
    public string observation_en;         // Observable lab evidence in English
    public string explanation_en;         // Rich scientific explanation in English
    public string condition_notes;        // Required conditions summary

    // Optional enrichment flags (default false for backward compatibility)
    public bool requiresHeating;          // True if activationTempC > ambient is essential
    public bool requiresCatalyst;         // True if reaction will not proceed without a catalyst
    public string safety_notes;           // Extra safety note for UI display

    public string GetReactantA()
    {
        if (!string.IsNullOrWhiteSpace(reactantA)) return reactantA;
        if (reactants != null && reactants.Count > 0) return reactants[0]?.formula;
        return null;
    }

    public string GetReactantB()
    {
        if (!string.IsNullOrWhiteSpace(reactantB)) return reactantB;
        if (reactants != null && reactants.Count > 1) return reactants[1]?.formula;
        return null;
    }

    public string GetPrimaryProduct()
    {
        if (!string.IsNullOrWhiteSpace(product)) return product;
        if (products != null && products.Count > 0) return products[0]?.formula;
        return null;
    }

    public List<string> GetReactantFormulas()
    {
        var result = new List<string>();

        if (reactants != null && reactants.Count > 0)
        {
            for (int i = 0; i < reactants.Count; i++)
            {
                string formula = reactants[i]?.formula;
                if (!string.IsNullOrWhiteSpace(formula))
                    result.Add(formula.Trim());
            }
        }

        if (result.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(reactantA))
                result.Add(reactantA.Trim());
            if (!string.IsNullOrWhiteSpace(reactantB))
                result.Add(reactantB.Trim());
        }

        return result;
    }

    public List<string> GetProductFormulas()
    {
        var result = new List<string>();

        if (products != null && products.Count > 0)
        {
            for (int i = 0; i < products.Count; i++)
            {
                string formula = products[i]?.formula;
                if (!string.IsNullOrWhiteSpace(formula))
                    result.Add(formula.Trim());
            }
        }

        if (result.Count == 0 && !string.IsNullOrWhiteSpace(product))
            result.Add(product.Trim());

        return result;
    }

    public bool GetProducesGas()
    {
        return producesGas || (visual_effects != null && visual_effects.gas);
    }
}

[Serializable]
public class ReactionChemical
{
    public string formula;
    public string state;
    public float stoich = 1f;
}

[Serializable]
public class ReactionVisualEffects
{
    public string color_change;
    public bool precipitate;
    public bool gas;
    public float temperature_delta;
    public string sound_id;
    public string intensity;
    public bool glow;
    public bool sparks;
    public bool smoke;
    public bool foam;
    public bool frost;
}

[Serializable]
public class ReactionSafety
{
    public List<string> ghs_icons = new List<string>();
    public List<string> warnings_ar = new List<string>();
    public List<string> warnings_en = new List<string>();
}

[Serializable]
public class ReactionValidation
{
    public bool require_acid_into_water_rule;
    public float min_moles_threshold = 0.001f;
}
