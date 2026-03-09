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

    // Educational controls
    public bool producesGas;
    public float activationTempC = 25f;
    public string requiredMedium;
    public bool catalystAllowed = false;
    public float catalystDeltaTempC = 10f;

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
}
