#!/usr/bin/env python3
"""Expand the reaction database with new reactions and fix incomplete ones."""

import json

with open('Assets/_Project/DataSrc/reactions.json') as f:
    data = json.load(f)

new_reactions = [
    {
        "id": "rxn_107",
        "name_en": "Electrolysis of Water",
        "reactionType": "Decomposition",
        "reactants": [{"formula": "H2O", "state": "l", "stoich": 2}],
        "products": [{"formula": "H2", "state": "g", "stoich": 2}, {"formula": "O2", "state": "g", "stoich": 1}],
        "activationTempC": 25, "requiredMedium": "Neutral",
        "catalystAllowed": False, "catalystDeltaTempC": 0,
        "visual_effects": {"color_change": "", "precipitate": False, "gas": True, "temperature_delta": 0, "sound_id": "bubble"},
        "safety": {"ghs_icons": [], "warnings_en": ["Hydrogen gas is flammable.", "Use proper ventilation."]},
        "observation_en": "Bubbles of hydrogen gas form at the cathode and oxygen at the anode.",
        "explanation_en": "Water is split into hydrogen and oxygen gas by an electric current. 2H2O -> 2H2 + O2.",
        "condition_notes": "Requires electrical current. Add electrolyte (e.g. H2SO4) to increase conductivity.",
        "requiresHeating": False, "requiresCatalyst": False,
        "safety_notes": "Hydrogen is flammable - keep away from open flames."
    },
    {
        "id": "rxn_108",
        "name_en": "Ozone Decomposition",
        "reactionType": "Decomposition",
        "reactants": [{"formula": "O3", "state": "g", "stoich": 2}],
        "products": [{"formula": "O2", "state": "g", "stoich": 3}],
        "activationTempC": 80, "requiredMedium": "Neutral",
        "catalystAllowed": True, "catalystDeltaTempC": 30,
        "visual_effects": {"color_change": "", "precipitate": False, "gas": True, "temperature_delta": 5, "sound_id": ""},
        "safety": {"ghs_icons": ["GHS03"], "warnings_en": ["Ozone is a respiratory irritant.", "Avoid inhalation."]},
        "observation_en": "Ozone gas slowly converts to oxygen gas. The pale blue color fades.",
        "explanation_en": "Ozone (O3) is unstable and decomposes into diatomic oxygen (O2). Chlorine atoms from CFCs catalyze this reaction in the stratosphere.",
        "condition_notes": "Accelerated by UV light and certain catalysts like chlorine.",
        "requiresHeating": False, "requiresCatalyst": False,
        "safety_notes": "Ozone is toxic - ensure proper fume extraction."
    },
    {
        "id": "rxn_109",
        "name_en": "Photosynthesis (Simplified)",
        "reactionType": "Synthesis",
        "reactants": [{"formula": "CO2", "state": "g", "stoich": 6}, {"formula": "H2O", "state": "l", "stoich": 6}],
        "products": [{"formula": "C6H12O6", "state": "aq", "stoich": 1}, {"formula": "O2", "state": "g", "stoich": 6}],
        "activationTempC": 15, "requiredMedium": "Neutral",
        "catalystAllowed": False, "catalystDeltaTempC": 0,
        "visual_effects": {"color_change": "#00AA00", "precipitate": False, "gas": True, "temperature_delta": 0, "sound_id": ""},
        "safety": {"ghs_icons": [], "warnings_en": ["Safe reaction. No hazards."]},
        "observation_en": "In the presence of light and chlorophyll, CO2 and water are converted into glucose and oxygen gas.",
        "explanation_en": "Plants convert carbon dioxide and water into glucose and oxygen using sunlight energy captured by chlorophyll. 6CO2 + 6H2O -> C6H12O6 + 6O2.",
        "condition_notes": "Requires light energy and chlorophyll. Occurs in chloroplasts.",
        "requiresHeating": False, "requiresCatalyst": True,
        "safety_notes": ""
    },
    {
        "id": "rxn_110",
        "name_en": "Thermal Decomposition of Copper Carbonate",
        "reactionType": "Decomposition",
        "reactants": [{"formula": "CuCO3", "state": "s", "stoich": 1}],
        "products": [{"formula": "CuO", "state": "s", "stoich": 1}, {"formula": "CO2", "state": "g", "stoich": 1}],
        "activationTempC": 200, "requiredMedium": "Neutral",
        "catalystAllowed": False, "catalystDeltaTempC": 0,
        "visual_effects": {"color_change": "#000000", "precipitate": False, "gas": True, "temperature_delta": 10, "sound_id": ""},
        "safety": {"ghs_icons": [], "warnings_en": ["Heat hazard. Use tongs and heat-proof mat."]},
        "observation_en": "The green powder turns black as copper oxide forms, and CO2 gas is released.",
        "explanation_en": "Copper carbonate decomposes when heated, forming black copper oxide and carbon dioxide gas. CuCO3 -> CuO + CO2.",
        "condition_notes": "Requires strong heating (~200C). Green to black color change is diagnostic.",
        "requiresHeating": True, "requiresCatalyst": False,
        "safety_notes": "Hot apparatus - allow to cool before handling."
    },
    {
        "id": "rxn_111",
        "name_en": "Displacement of Copper by Iron",
        "reactionType": "Single Displacement",
        "reactants": [{"formula": "Fe", "state": "s", "stoich": 1}, {"formula": "CuSO4", "state": "aq", "stoich": 1}],
        "products": [{"formula": "FeSO4", "state": "aq", "stoich": 1}, {"formula": "Cu", "state": "s", "stoich": 1}],
        "activationTempC": 15, "requiredMedium": "Neutral",
        "catalystAllowed": False, "catalystDeltaTempC": 0,
        "visual_effects": {"color_change": "#00AA55", "precipitate": True, "gas": False, "temperature_delta": 3, "sound_id": ""},
        "safety": {"ghs_icons": [], "warnings_en": ["Copper sulfate is an irritant.", "Wash hands after handling."]},
        "observation_en": "The blue copper sulfate solution turns greenish as iron displaces copper. Copper deposits on the iron.",
        "explanation_en": "Iron is more reactive than copper, so it displaces copper from copper sulfate. Fe + CuSO4 -> FeSO4 + Cu.",
        "condition_notes": "Occurs readily at room temperature. The blue color fades as Cu2+ is replaced by Fe2+.",
        "requiresHeating": False, "requiresCatalyst": False,
        "safety_notes": "Wear gloves when handling copper sulfate solution."
    },
    {
        "id": "rxn_112",
        "name_en": "Esterification: Ethyl Acetate",
        "reactionType": "Esterification",
        "reactants": [{"formula": "CH3COOH", "state": "l", "stoich": 1}, {"formula": "C2H5OH", "state": "l", "stoich": 1}],
        "products": [{"formula": "CH3COOC2H5", "state": "l", "stoich": 1}, {"formula": "H2O", "state": "l", "stoich": 1}],
        "activationTempC": 60, "requiredMedium": "Acidic",
        "catalystAllowed": True, "catalystDeltaTempC": 20,
        "visual_effects": {"color_change": "", "precipitate": False, "gas": False, "temperature_delta": 2, "sound_id": ""},
        "safety": {"ghs_icons": [], "warnings_en": ["Ethanol is flammable.", "Concentrated acid is corrosive."]},
        "observation_en": "A fruity, sweet smell of ethyl acetate (banana-like) is produced.",
        "explanation_en": "Acetic acid and ethanol react in the presence of an acid catalyst to form ethyl acetate (ester) and water. CH3COOH + C2H5OH -> CH3COOC2H5 + H2O.",
        "condition_notes": "Requires acid catalyst (usually H2SO4) and gentle heating. The ester has a characteristic fruity smell.",
        "requiresHeating": True, "requiresCatalyst": True,
        "safety_notes": "Flammable organic compounds. No open flames near ethanol."
    },
    {
        "id": "rxn_113",
        "name_en": "Iodine Clock Reaction",
        "reactionType": "Redox",
        "reactants": [{"formula": "H2O2", "state": "aq", "stoich": 1}, {"formula": "KI", "state": "aq", "stoich": 1}, {"formula": "H2SO4", "state": "aq", "stoich": 1}],
        "products": [{"formula": "I2", "state": "aq", "stoich": 1}, {"formula": "K2SO4", "state": "aq", "stoich": 1}, {"formula": "H2O", "state": "l", "stoich": 2}],
        "activationTempC": 20, "requiredMedium": "Acidic",
        "catalystAllowed": False, "catalystDeltaTempC": 0,
        "visual_effects": {"color_change": "#000066", "precipitate": False, "gas": False, "temperature_delta": 0, "sound_id": ""},
        "safety": {"ghs_icons": [], "warnings_en": ["Iodine stains skin and clothing.", "Handle with care."]},
        "observation_en": "After a short delay, the solution suddenly turns deep blue-black as iodine forms and reacts with starch.",
        "explanation_en": "A classic clock reaction. Hydrogen peroxide oxidizes iodide to iodine in acidic conditions. Iodine forms a blue complex with starch.",
        "condition_notes": "Famous demonstration of reaction kinetics. The delay depends on concentration and temperature.",
        "requiresHeating": False, "requiresCatalyst": False,
        "safety_notes": "Iodine stains - wear gloves and protective clothing."
    },
    {
        "id": "rxn_114",
        "name_en": "Burning Sugar (Caramelization)",
        "reactionType": "Decomposition",
        "reactants": [{"formula": "C12H22O11", "state": "s", "stoich": 1}],
        "products": [{"formula": "C", "state": "s", "stoich": 12}, {"formula": "H2O", "state": "g", "stoich": 11}],
        "activationTempC": 186, "requiredMedium": "Neutral",
        "catalystAllowed": True, "catalystDeltaTempC": 50,
        "visual_effects": {"color_change": "#8B4513", "precipitate": False, "gas": True, "temperature_delta": 30, "sound_id": "sizzle"},
        "safety": {"ghs_icons": [], "warnings_en": ["Very hot - severe burn hazard.", "Molten sugar sticks to skin."]},
        "observation_en": "White sugar turns golden brown then black as it decomposes. Water vapor and caramel aroma are released.",
        "explanation_en": "When heated above its melting point, sucrose decomposes through caramelization, breaking down into carbon and water. C12H22O11 -> 12C + 11H2O.",
        "condition_notes": "Concentrated H2SO4 catalyzes this reaction at room temperature (dehydration of sugar).",
        "requiresHeating": True, "requiresCatalyst": False,
        "safety_notes": "Extremely hot molten sugar causes severe burns - use tongs and heat shield."
    },
    {
        "id": "rxn_115",
        "name_en": "Phenolpthalein Color Change",
        "reactionType": "Indicator",
        "reactants": [{"formula": "C20H14O4", "state": "aq", "stoich": 1}, {"formula": "NaOH", "state": "aq", "stoich": 2}],
        "products": [{"formula": "C20H12Na2O4", "state": "aq", "stoich": 1}, {"formula": "H2O", "state": "l", "stoich": 1}],
        "activationTempC": 15, "requiredMedium": "Basic",
        "catalystAllowed": False, "catalystDeltaTempC": 0,
        "visual_effects": {"color_change": "#FF1493", "precipitate": False, "gas": False, "temperature_delta": 0, "sound_id": ""},
        "safety": {"ghs_icons": [], "warnings_en": ["NaOH is corrosive.", "Wear gloves."]},
        "observation_en": "The colorless solution instantly turns bright pink when base is added.",
        "explanation_en": "Phenolpthalein is a pH indicator that is colorless in acidic/neutral solutions and pink in basic solutions (pH > 8.3).",
        "condition_notes": "A classic acid-base indicator. Reversible - adding acid returns the colorless form.",
        "requiresHeating": False, "requiresCatalyst": False,
        "safety_notes": ""
    },
]

# Fix incomplete reactions
fixes = {
    "rxn_047": [{"formula": "Ba(NO3)2", "state": "aq", "stoich": 1}, {"formula": "H2O", "state": "l", "stoich": 2}],
    "rxn_051": [{"formula": "AgBr", "state": "s", "stoich": 1}, {"formula": "NaNO3", "state": "aq", "stoich": 1}],
    "rxn_055": [{"formula": "CuCO3", "state": "s", "stoich": 1}, {"formula": "Na2SO4", "state": "aq", "stoich": 1}],
    "rxn_059": [{"formula": "Zn(NO3)2", "state": "aq", "stoich": 1}, {"formula": "Pb", "state": "s", "stoich": 1}],
    "rxn_061": [{"formula": "ZnSO4", "state": "aq", "stoich": 1}, {"formula": "H2", "state": "g", "stoich": 1}],
    "rxn_064": [{"formula": "AlCl3", "state": "aq", "stoich": 2}, {"formula": "H2", "state": "g", "stoich": 3}],
    "rxn_069": [{"formula": "Cu(NO3)2", "state": "aq", "stoich": 1}, {"formula": "NO2", "state": "g", "stoich": 2}, {"formula": "H2O", "state": "l", "stoich": 2}],
    "rxn_081": [{"formula": "ZnSO4", "state": "aq", "stoich": 1}, {"formula": "H2O", "state": "l", "stoich": 1}],
    "rxn_082": [{"formula": "CrCl3", "state": "aq", "stoich": 2}, {"formula": "H2O", "state": "l", "stoich": 3}],
    "rxn_086": [{"formula": "Cu(NH3)4SO4", "state": "aq", "stoich": 1}],
    "rxn_092": [{"formula": "NaF", "state": "aq", "stoich": 1}, {"formula": "H2O", "state": "l", "stoich": 1}],
    "rxn_096": [{"formula": "Na2ZnO2", "state": "aq", "stoich": 1}, {"formula": "H2O", "state": "l", "stoich": 1}],
    "rxn_097": [{"formula": "SnCl2", "state": "aq", "stoich": 1}, {"formula": "H2", "state": "g", "stoich": 1}],
    "rxn_099": [{"formula": "Na2SiO3", "state": "aq", "stoich": 1}, {"formula": "H2O", "state": "l", "stoich": 1}],
    "rxn_048": [{"formula": "CH3COOLi", "state": "aq", "stoich": 1}, {"formula": "H2O", "state": "l", "stoich": 1}],
}

for rxn_id, new_products in fixes.items():
    for rxn in data:
        if rxn["id"] == rxn_id:
            rxn["products"] = new_products
            break

# Add new reactions
data.extend(new_reactions)

with open("Assets/_Project/DataSrc/reactions.json", "w") as f:
    json.dump(data, f, indent=2)

print(f"Total reactions: {len(data)}")
print(f"Fixed {len(fixes)} reactions with incomplete product data")
