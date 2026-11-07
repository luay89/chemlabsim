using System;
using System.Collections.Generic;

[Serializable]
public class MaterialDB
{
    public List<ChemicalMaterial> materials = new List<ChemicalMaterial>();
}

[Serializable]
public class ChemicalMaterial
{
    // -- Legacy fields (preserved for backward compatibility) --
    public string formula          = string.Empty;
    public string name_ar          = string.Empty;
    public string name_en          = string.Empty;
    public string default_state    = "solid";       // solid, liquid, gas, aqueous
    public string color            = string.Empty;   // hex color or empty
    public List<string> roles              = new List<string>(); // reactant, product, gas, precipitate, catalyst, solvent
    public List<string> used_in_reactions  = new List<string>(); // reaction ids

    // -- New fields (v2 expansion) -----------------------------
    public string id               = string.Empty;
    public string displayNameAr    = string.Empty;
    public string displayNameEn    = string.Empty;
    public string category         = string.Empty;   // Element, Acid, Base, Salt, Oxide, Gas, Organic, Catalyst
    public string hazardLevel      = "low";          // low, medium, high
    public List<string> reactionTags = new List<string>(); // neutralization, redox, combustion, precipitation, etc.
    public bool requiresHeating;
    public bool supportsCatalyst   = true;
    public string state            = "solid";        // alias for default_state (new schema)

    // -- Amounts (Stage 5) — 0 means "not known", never a default ---------------
    // Intrinsic property of a pure liquid, with its literature source.
    public float densityGPerMl;
    public string densitySource    = string.Empty;
    // Configuration of the bottle on the lab shelf (a solution's concentration), not a property of the substance.
    public float stockConcentrationM;
    public string stockLabel       = string.Empty;

    // -- Heating lab — present only for substances whose data was checked against a source ----
    public MaterialThermalData thermal = new MaterialThermalData();

    /// <summary>Display name resolved by language index (0=EN, 1=AR).</summary>
    public string GetDisplayName(int languageIndex)
    {
        if (languageIndex == 1)
            return !string.IsNullOrEmpty(displayNameAr) ? displayNameAr
                 : !string.IsNullOrEmpty(name_ar) ? name_ar : formula;
        return !string.IsNullOrEmpty(displayNameEn) ? displayNameEn
             : !string.IsNullOrEmpty(name_en) ? name_en : formula;
    }

    /// <summary>Effective physical state (prefers new 'state' field, falls back to legacy).</summary>
    public string GetState()
    {
        return !string.IsNullOrEmpty(state) ? state
             : !string.IsNullOrEmpty(default_state) ? default_state : "solid";
    }
}

/// <summary>
/// Phase-change data of a pure substance at referencePressureKPa (written by
/// tools/reactions/material_properties_004.py). available = false means "no data": the heating
/// lab then explains the limitation instead of assuming values. 0 in a field = not given.
/// </summary>
[Serializable]
public class MaterialThermalData
{
    public bool available;
    public float referencePressureKPa;
    public float meltingPointC;
    public float boilingPointC;
    public float heatCapacityLiquidJPerGK;
    public float heatCapacitySolidJPerGK;
    public float enthalpyFusionJPerG;
    public float enthalpyVaporizationJPerG;
    public float solidDensityGPerMl;
    public string source = string.Empty;
    public string notes = string.Empty;
}
