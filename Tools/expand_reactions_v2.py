#!/usr/bin/env python3
"""Phase 2 reaction expansion: organic chemistry, electrochemistry, complex reactions."""

import json
import os

with open("Assets/_Project/DataSrc/reactions.json") as f:
    data = json.load(f)

new_reactions = [
    # ═══════════════════════════════════════════════════════════
    #  ORGANIC CHEMISTRY REACTIONS (rxn_116+)
    # ═══════════════════════════════════════════════════════════
    {
        "id": "rxn_116",
        "name_en": "Dehydration of Ethanol to Ethene",
        "reactionType": "Elimination",
        "reactants": [{"formula": "C2H5OH", "state": "l", "stoich": 1}],
        "products": [{"formula": "C2H4", "state": "g", "stoich": 1}, {"formula": "H2O", "state": "l", "stoich": 1}],
        "activationTempC": 170, "requiredMedium": "Acidic",
        "catalystAllowed": True, "catalystDeltaTempC": 30,
        "visual_effects": {"color_change": "", "precipitate": False, "gas": True, "temperature_delta": 10, "sound_id": "sizzle", "smoke": True},
        "safety": {"ghs_icons": ["GHS02"], "warnings_en": ["Ethanol is flammable.", "Concentrated H2SO4 is corrosive.", "Ethene is flammable."]},
        "observation_en": "A colorless gas (ethene) is evolved. The solution darkens as the acid dehydrates the alcohol.",
        "explanation_en": "Ethanol is dehydrated by concentrated sulfuric acid at 170°C to form ethene (ethylene), an alkene. C2H5OH -> C2H4 + H2O. This is an elimination reaction.",
        "condition_notes": "Requires concentrated H2SO4 catalyst and high temperature (170°C). At lower temperatures (140°C), diethyl ether forms instead.",
        "requiresHeating": True, "requiresCatalyst": True,
        "safety_notes": "Concentrated acid is extremely corrosive. Ethene is highly flammable."
    },
    {
        "id": "rxn_117",
        "name_en": "Hydration of Ethene to Ethanol",
        "reactionType": "Addition",
        "reactants": [{"formula": "C2H4", "state": "g", "stoich": 1}, {"formula": "H2O", "state": "l", "stoich": 1}],
        "products": [{"formula": "C2H5OH", "state": "l", "stoich": 1}],
        "activationTempC": 300, "requiredMedium": "Acidic",
        "catalystAllowed": True, "catalystDeltaTempC": 150,
        "visual_effects": {"color_change": "", "precipitate": False, "gas": False, "temperature_delta": 5, "sound_id": ""},
        "safety": {"ghs_icons": ["GHS02"], "warnings_en": ["Ethene is flammable.", "High pressure operation."]},
        "observation_en": "Ethene gas is absorbed into the acidic solution, producing ethanol.",
        "explanation_en": "Ethene gas reacts with steam over a phosphoric acid catalyst at 300°C and high pressure to form ethanol. C2H4 + H2O -> C2H5OH. Industrial method for ethanol production.",
        "condition_notes": "Industrial process: 300°C, 70 atm, H3PO4 catalyst on silica support.",
        "requiresHeating": True, "requiresCatalyst": True,
        "safety_notes": "High pressure and temperature. Industrial scale only."
    },
    {
        "id": "rxn_118",
        "name_en": "Bromination of Ethene",
        "reactionType": "Addition",
        "reactants": [{"formula": "C2H4", "state": "g", "stoich": 1}, {"formula": "Br2", "state": "l", "stoich": 1}],
        "products": [{"formula": "C2H4Br2", "state": "l", "stoich": 1}],
        "activationTempC": 15, "requiredMedium": "Neutral",
        "catalystAllowed": False, "catalystDeltaTempC": 0,
        "visual_effects": {"color_change": "#FF6600", "precipitate": False, "gas": False, "temperature_delta": 3, "sound_id": "", "glow": True},
        "safety": {"ghs_icons": ["GHS06", "GHS07"], "warnings_en": ["Bromine is highly toxic and corrosive.", "Work in fume hood.", "Causes severe burns."]},
        "observation_en": "The reddish-brown bromine water rapidly decolorizes as it adds across the double bond. A colorless dibromoethane liquid forms.",
        "explanation_en": "Bromine adds across the carbon-carbon double bond of ethene in an electrophilic addition reaction. The brown color of bromine disappears, confirming the presence of unsaturation. C2H4 + Br2 -> C2H4Br2 (1,2-dibromoethane).",
        "condition_notes": "Classic test for unsaturation. Rapid at room temperature. Bromine water decolorization confirms C=C double bond.",
        "requiresHeating": False, "requiresCatalyst": False,
        "safety_notes": "Bromine is HIGHLY TOXIC and corrosive. Must be handled in a fume hood with proper PPE."
    },
    {
        "id": "rxn_119",
        "name_en": "Free Radical Substitution: Methane + Chlorine",
        "reactionType": "Substitution",
        "reactants": [{"formula": "CH4", "state": "g", "stoich": 1}, {"formula": "Cl2", "state": "g", "stoich": 1}],
        "products": [{"formula": "CH3Cl", "state": "g", "stoich": 1}, {"formula": "HCl", "state": "g", "stoich": 1}],
        "activationTempC": 250, "requiredMedium": "Neutral",
        "catalystAllowed": True, "catalystDeltaTempC": 100,
        "visual_effects": {"color_change": "#AAFF00", "precipitate": False, "gas": True, "temperature_delta": 8, "sound_id": "", "glow": True, "smoke": True},
        "safety": {"ghs_icons": ["GHS06", "GHS02"], "warnings_en": ["Chlorine gas is toxic.", "Methane is flammable.", "UV radiation hazard."]},
        "observation_en": "The greenish-yellow chlorine color fades as the reaction proceeds. HCl gas (white fumes with ammonia) is detected.",
        "explanation_en": "Methane reacts with chlorine in the presence of UV light via a free radical substitution mechanism. A hydrogen atom is replaced by chlorine. CH4 + Cl2 -> CH3Cl + HCl. Further substitution can produce CH2Cl2, CHCl3, and CCl4.",
        "condition_notes": "Requires UV light or high temperature to initiate free radical chain reaction. Product is a mixture of chloromethanes.",
        "requiresHeating": True, "requiresCatalyst": True,
        "safety_notes": "Toxic gases — perform in fume hood only. UV light can damage eyes."
    },
    {
        "id": "rxn_120",
        "name_en": "Hydrolysis of Ethyl Acetate",
        "reactionType": "Hydrolysis",
        "reactants": [{"formula": "CH3COOC2H5", "state": "l", "stoich": 1}, {"formula": "NaOH", "state": "aq", "stoich": 1}],
        "products": [{"formula": "CH3COONa", "state": "aq", "stoich": 1}, {"formula": "C2H5OH", "state": "l", "stoich": 1}],
        "activationTempC": 40, "requiredMedium": "Basic",
        "catalystAllowed": False, "catalystDeltaTempC": 0,
        "visual_effects": {"color_change": "", "precipitate": False, "gas": False, "temperature_delta": 2, "sound_id": ""},
        "safety": {"ghs_icons": [], "warnings_en": ["Sodium hydroxide is corrosive.", "Ethanol is flammable."]},
        "observation_en": "The ester layer gradually disappears as it hydrolyzes. The fruity smell of ethyl acetate fades and ethanol odor appears.",
        "explanation_en": "Esters undergo alkaline hydrolysis (saponification) to form a carboxylate salt and an alcohol. The reverse of esterification. CH3COOC2H5 + NaOH -> CH3COONa + C2H5OH.",
        "condition_notes": "Proceeds faster with heating. NaOH provides OH- nucleophile that attacks the carbonyl carbon.",
        "requiresHeating": False, "requiresCatalyst": False,
        "safety_notes": "Base is corrosive. Avoid contact with eyes and skin."
    },
    {
        "id": "rxn_121",
        "name_en": "Oxidation of Ethanol to Ethanoic Acid",
        "reactionType": "Redox",
        "reactants": [{"formula": "C2H5OH", "state": "l", "stoich": 1}, {"formula": "K2Cr2O7", "state": "aq", "stoich": 1}],
        "products": [{"formula": "CH3COOH", "state": "aq", "stoich": 1}, {"formula": "Cr2O3", "state": "s", "stoich": 1}, {"formula": "K2SO4", "state": "aq", "stoich": 1}],
        "activationTempC": 50, "requiredMedium": "Acidic",
        "catalystAllowed": False, "catalystDeltaTempC": 0,
        "visual_effects": {"color_change": "#00AA44", "precipitate": False, "gas": False, "temperature_delta": 5, "sound_id": "", "glow": True},
        "safety": {"ghs_icons": ["GHS03", "GHS07"], "warnings_en": ["Potassium dichromate is toxic and carcinogenic.", "Strong oxidizer.", "Avoid skin contact."]},
        "observation_en": "The orange dichromate solution turns green as Cr(VI) is reduced to Cr(III). A pungent vinegar smell indicates ethanoic acid formation.",
        "explanation_en": "Primary alcohols can be oxidized to carboxylic acids using strong oxidizing agents like acidified K2Cr2O7. The orange to green color change confirms oxidation. Ethanol -> ethanal -> ethanoic acid.",
        "condition_notes": "Requires acidic conditions (H2SO4) and heat. The color change from orange (Cr2O7^2-) to green (Cr^3+) is diagnostic.",
        "requiresHeating": True, "requiresCatalyst": False,
        "safety_notes": "Dichromate is a known carcinogen — wear gloves. Strong oxidizer — keep away from organics."
    },
    {
        "id": "rxn_122",
        "name_en": "Polymerization of Ethene",
        "reactionType": "Polymerization",
        "reactants": [{"formula": "C2H4", "state": "g", "stoich": 100}],
        "products": [{"formula": "(C2H4)n", "state": "s", "stoich": 1}],
        "activationTempC": 150, "requiredMedium": "Neutral",
        "catalystAllowed": True, "catalystDeltaTempC": 80,
        "visual_effects": {"color_change": "#E0E0E0", "precipitate": True, "gas": False, "temperature_delta": 15, "sound_id": "", "foam": True},
        "safety": {"ghs_icons": ["GHS02"], "warnings_en": ["Ethene is flammable.", "High pressure equipment required."]},
        "observation_en": "A white waxy solid (polyethene) forms as ethene gas polymerizes. The reaction is exothermic and the mixture thickens.",
        "explanation_en": "Ethene monomers join together in a chain reaction to form polyethene (polyethylene), the most common plastic. n(CH2=CH2) -> -(CH2-CH2)n-. Requires initiator and high pressure or Ziegler-Natta catalyst.",
        "condition_notes": "Industrial process: high pressure (1000-3000 atm) or Ziegler-Natta catalyst at lower pressure.",
        "requiresHeating": True, "requiresCatalyst": True,
        "safety_notes": "High pressure operation hazard. Ethene is highly flammable."
    },
    # ═══════════════════════════════════════════════════════════
    #  ELECTROCHEMISTRY
    # ═══════════════════════════════════════════════════════════
    {
        "id": "rxn_123",
        "name_en": "Copper Electroplating",
        "reactionType": "Electrochemistry",
        "reactants": [{"formula": "CuSO4", "state": "aq", "stoich": 1}],
        "products": [{"formula": "Cu", "state": "s", "stoich": 1}],
        "activationTempC": 20, "requiredMedium": "Acidic",
        "catalystAllowed": False, "catalystDeltaTempC": 0,
        "visual_effects": {"color_change": "#B87333", "precipitate": True, "gas": False, "temperature_delta": 2, "sound_id": "", "glow": True},
        "safety": {"ghs_icons": [], "warnings_en": ["Copper sulfate is an irritant.", "Low voltage is safe but avoid short circuits."]},
        "observation_en": "A layer of copper metal deposits on the cathode (negative electrode). The blue solution gradually fades as Cu2+ ions are removed.",
        "explanation_en": "In electrolysis, Cu2+ ions in solution are reduced at the cathode: Cu2+ + 2e- -> Cu. Copper atoms plate onto the cathode surface. The anode (copper) dissolves to replenish Cu2+ ions.",
        "condition_notes": "Requires DC power supply (low voltage). Current density affects plating quality.",
        "requiresHeating": False, "requiresCatalyst": False,
        "safety_notes": "Low voltage safe. Copper sulfate is an environmental pollutant — dispose properly."
    },
    {
        "id": "rxn_124",
        "name_en": "Electrolysis of Molten NaCl",
        "reactionType": "Electrochemistry",
        "reactants": [{"formula": "NaCl", "state": "l", "stoich": 2}],
        "products": [{"formula": "Na", "state": "l", "stoich": 2}, {"formula": "Cl2", "state": "g", "stoich": 1}],
        "activationTempC": 801, "requiredMedium": "Neutral",
        "catalystAllowed": False, "catalystDeltaTempC": 0,
        "visual_effects": {"color_change": "", "precipitate": False, "gas": True, "temperature_delta": 30, "sound_id": "sizzle", "glow": True, "smoke": True},
        "safety": {"ghs_icons": ["GHS06", "GHS05"], "warnings_en": ["Chlorine gas is HIGHLY TOXIC.", "Molten salt causes severe burns.", "Sodium metal is explosive in water."]},
        "observation_en": "Silvery sodium metal forms at the cathode and floats on the molten salt. Greenish-yellow chlorine gas bubbles at the anode.",
        "explanation_en": "Molten sodium chloride is electrolyzed to produce sodium metal (at cathode: Na+ + e- -> Na) and chlorine gas (at anode: 2Cl- -> Cl2 + 2e-). Down's cell industrial process.",
        "condition_notes": "Requires molten NaCl (801°C melting point). CaCl2 is often added to lower melting point to ~600°C.",
        "requiresHeating": True, "requiresCatalyst": False,
        "safety_notes": "EXTREME HAZARD: Chlorine gas is toxic. Sodium metal reacts violently with water. Molten salt causes severe burns."
    },
    {
        "id": "rxn_125",
        "name_en": "Daniell Cell (Galvanic Cell)",
        "reactionType": "Electrochemistry",
        "reactants": [{"formula": "Zn", "state": "s", "stoich": 1}, {"formula": "CuSO4", "state": "aq", "stoich": 1}],
        "products": [{"formula": "ZnSO4", "state": "aq", "stoich": 1}, {"formula": "Cu", "state": "s", "stoich": 1}],
        "activationTempC": 15, "requiredMedium": "Neutral",
        "catalystAllowed": False, "catalystDeltaTempC": 0,
        "visual_effects": {"color_change": "#0066AA", "precipitate": True, "gas": False, "temperature_delta": 1, "sound_id": "", "glow": True},
        "safety": {"ghs_icons": [], "warnings_en": ["Copper sulfate is an irritant.", "Zinc sulfate is an irritant."]},
        "observation_en": "The zinc electrode slowly dissolves while copper deposits on the copper electrode. A voltage of ~1.1V is produced. The blue CuSO4 solution fades.",
        "explanation_en": "A classic galvanic/voltaic cell. Zinc oxidizes: Zn -> Zn2+ + 2e-. Copper reduces: Cu2+ + 2e- -> Cu. Electrons flow through the external circuit, producing electricity spontaneously.",
        "condition_notes": "Salt bridge (KCl/agar) or porous barrier needed to complete the circuit. Produces 1.1V spontaneously.",
        "requiresHeating": False, "requiresCatalyst": False,
        "safety_notes": "Safe demonstration. Wash hands after handling salt bridge materials."
    },
    # ═══════════════════════════════════════════════════════════
    #  MORE COMPLEX INORGANIC REACTIONS
    # ═══════════════════════════════════════════════════════════
    {
        "id": "rxn_126",
        "name_en": "Disproportionation of Hydrogen Peroxide",
        "reactionType": "Redox",
        "reactants": [{"formula": "H2O2", "state": "aq", "stoich": 2}],
        "products": [{"formula": "O2", "state": "g", "stoich": 1}, {"formula": "H2O", "state": "l", "stoich": 2}],
        "activationTempC": 30, "requiredMedium": "Neutral",
        "catalystAllowed": True, "catalystDeltaTempC": 25,
        "visual_effects": {"color_change": "", "precipitate": False, "gas": True, "temperature_delta": 5, "sound_id": "fizz", "foam": True},
        "safety": {"ghs_icons": ["GHS05"], "warnings_en": ["Hydrogen peroxide is corrosive.", "Keep away from organic materials."]},
        "observation_en": "Oxygen gas bubbles vigorously. The reaction can produce foam if soap is added (elephant's toothpaste).",
        "explanation_en": "Hydrogen peroxide decomposes into water and oxygen. This disproportionation reaction is catalyzed by MnO2, KI, or catalase enzyme. 2H2O2 -> 2H2O + O2.",
        "condition_notes": "Decomposes slowly at room temperature. Catalysts (MnO2, KI, catalase) accelerate dramatically.",
        "requiresHeating": False, "requiresCatalyst": True,
        "safety_notes": "H2O2 can cause chemical burns. Higher concentrations (>30%) are dangerous oxidizers."
    },
    {
        "id": "rxn_127",
        "name_en": "Aluminothermic Reaction (Thermite)",
        "reactionType": "Redox",
        "reactants": [{"formula": "Fe2O3", "state": "s", "stoich": 1}, {"formula": "Al", "state": "s", "stoich": 2}],
        "products": [{"formula": "Al2O3", "state": "s", "stoich": 1}, {"formula": "Fe", "state": "l", "stoich": 2}],
        "activationTempC": 1200, "requiredMedium": "Neutral",
        "catalystAllowed": False, "catalystDeltaTempC": 0,
        "visual_effects": {"color_change": "#FF6600", "precipitate": False, "gas": False, "temperature_delta": 200, "sound_id": "explosion", "glow": True, "sparks": True, "smoke": True},
        "safety": {"ghs_icons": ["GHS02"], "warnings_en": ["EXTREMELY HOT (2500°C).", "Severe burn hazard.", "Use fireproof surface.", "Keep at least 5m distance."]},
        "observation_en": "A brilliant white-hot reaction with sparks. Molten iron flows out of the crucible. The reaction is self-sustaining once ignited.",
        "explanation_en": "Aluminum reduces iron oxide in a highly exothermic reaction reaching 2500°C. The aluminum is oxidized (to Al2O3) and iron is reduced (to molten Fe). Fe2O3 + 2Al -> Al2O3 + 2Fe. Used in railway welding.",
        "condition_notes": "Requires magnesium ribbon fuse to initiate. Reaction is self-sustaining once started. Do NOT use with metal containers — use ceramic crucible.",
        "requiresHeating": True, "requiresCatalyst": False,
        "safety_notes": "DANGER: 2500°C reaction. Do not look directly at the reaction (severe eye damage). Have fire extinguisher ready. Use ONLY on fireproof surface."
    },
    {
        "id": "rxn_128",
        "name_en": "Blue Bottle Experiment (Redox Indicator)",
        "reactionType": "Redox",
        "reactants": [{"formula": "C6H12O6", "state": "aq", "stoich": 1}, {"formula": "NaOH", "state": "aq", "stoich": 1}],
        "products": [{"formula": "C6H12O7", "state": "aq", "stoich": 1}],
        "activationTempC": 20, "requiredMedium": "Basic",
        "catalystAllowed": True, "catalystDeltaTempC": 5,
        "visual_effects": {"color_change": "#0044CC", "precipitate": False, "gas": False, "temperature_delta": 0, "sound_id": ""},
        "safety": {"ghs_icons": [], "warnings_en": ["NaOH is corrosive.", "Methylene blue stains."]},
        "observation_en": "The solution turns blue when shaken (oxygen dissolves) and fades back to colorless when left to stand (oxygen consumed). This cycle can be repeated many times.",
        "explanation_en": "A fascinating oscillating redox demonstration. Glucose reduces methylene blue to its colorless form. Shaking introduces O2 which re-oxidizes the indicator, restoring blue color. Cycle repeats until glucose is consumed.",
        "condition_notes": "Contains glucose, NaOH, and methylene blue indicator. Shaking restores blue color. Standing (or glucose consumption) returns to colorless.",
        "requiresHeating": False, "requiresCatalyst": False,
        "safety_notes": "Methylene blue stains skin and clothing permanently."
    },
    {
        "id": "rxn_129",
        "name_en": "Nitration of Benzene",
        "reactionType": "Substitution",
        "reactants": [{"formula": "C6H6", "state": "l", "stoich": 1}, {"formula": "HNO3", "state": "l", "stoich": 1}],
        "products": [{"formula": "C6H5NO2", "state": "l", "stoich": 1}, {"formula": "H2O", "state": "l", "stoich": 1}],
        "activationTempC": 50, "requiredMedium": "Acidic",
        "catalystAllowed": True, "catalystDeltaTempC": 20,
        "visual_effects": {"color_change": "#FFD700", "precipitate": False, "gas": False, "temperature_delta": 8, "sound_id": "", "smoke": True},
        "safety": {"ghs_icons": ["GHS06", "GHS02"], "warnings_en": ["Benzene is carcinogenic.", "Concentrated acids are corrosive.", "Nitrobenzene is toxic.", "Fume hood mandatory."]},
        "observation_en": "The mixture turns yellow as nitrobenzene forms. A dense yellow oily layer separates from the acid mixture. Heat is released.",
        "explanation_en": "Benzene undergoes electrophilic aromatic substitution with concentrated nitric acid in the presence of concentrated sulfuric acid catalyst. The nitronium ion (NO2+) is the electrophile. C6H6 + HNO3 -> C6H5NO2 + H2O.",
        "condition_notes": "Requires concentrated H2SO4 as catalyst (generates NO2+ electrophile). Temperature must be kept below 55°C to avoid dinitration.",
        "requiresHeating": False, "requiresCatalyst": True,
        "safety_notes": "BENZENE IS A CARCINOGEN. Nitrobenzene is toxic and absorbed through skin. MUST be in fume hood with full PPE."
    },
    {
        "id": "rxn_130",
        "name_en": "Haber Process (Ammonia Synthesis)",
        "reactionType": "Synthesis",
        "reactants": [{"formula": "N2", "state": "g", "stoich": 1}, {"formula": "H2", "state": "g", "stoich": 3}],
        "products": [{"formula": "NH3", "state": "g", "stoich": 2}],
        "activationTempC": 450, "requiredMedium": "Neutral",
        "catalystAllowed": True, "catalystDeltaTempC": 200,
        "visual_effects": {"color_change": "", "precipitate": False, "gas": True, "temperature_delta": 30, "sound_id": "", "glow": True},
        "safety": {"ghs_icons": ["GHS06"], "warnings_en": ["Ammonia is toxic.", "High pressure (200 atm) hazard.", "Hydrogen is flammable."]},
        "observation_en": "The reaction produces ammonia gas (detected by its sharp odor and by turning damp red litmus blue).",
        "explanation_en": "Nitrogen and hydrogen combine over an iron catalyst at 450°C and 200 atm to form ammonia. N2 + 3H2 <-> 2NH3. This exothermic equilibrium reaction is the foundation of the fertilizer industry. Le Chatelier's principle: high pressure favors product.",
        "condition_notes": "Industrial: 450°C, 200 atm, Fe catalyst with K2O/Al2O3 promoters. Equilibrium yield ~15% per pass.",
        "requiresHeating": True, "requiresCatalyst": True,
        "safety_notes": "High pressure and temperature. Ammonia is toxic and irritating. Hydrogen is flammable."
    },
    {
        "id": "rxn_131",
        "name_en": "Contact Process (Sulfuric Acid Production)",
        "reactionType": "Synthesis",
        "reactants": [{"formula": "SO2", "state": "g", "stoich": 2}, {"formula": "O2", "state": "g", "stoich": 1}],
        "products": [{"formula": "SO3", "state": "g", "stoich": 2}],
        "activationTempC": 450, "requiredMedium": "Neutral",
        "catalystAllowed": True, "catalystDeltaTempC": 200,
        "visual_effects": {"color_change": "", "precipitate": False, "gas": True, "temperature_delta": 50, "sound_id": "", "glow": True},
        "safety": {"ghs_icons": ["GHS05", "GHS06"], "warnings_en": ["SO2 and SO3 are toxic and corrosive.", "Sulfuric acid causes severe burns.", "High temperature."]},
        "observation_en": "SO2 gas reacts with oxygen over the catalyst producing SO3 fumes, which dissolve in water to form a mist of sulfuric acid.",
        "explanation_en": "The Contact Process: Sulfur dioxide is oxidized to sulfur trioxide over a vanadium(V) oxide catalyst (V2O5) at 450°C. 2SO2 + O2 <-> 2SO3. The SO3 is then absorbed in H2SO4 to form oleum, which is diluted to produce concentrated H2SO4.",
        "condition_notes": "Industrial: 450°C, 1-2 atm, V2O5 catalyst. Exothermic equilibrium - lower temp favors SO3 but slows rate.",
        "requiresHeating": True, "requiresCatalyst": True,
        "safety_notes": "SO3 fumes are extremely corrosive. Concentrated H2SO4 causes severe burns."
    },
    {
        "id": "rxn_132",
        "name_en": "Combustion of Methane",
        "reactionType": "Combustion",
        "reactants": [{"formula": "CH4", "state": "g", "stoich": 1}, {"formula": "O2", "state": "g", "stoich": 2}],
        "products": [{"formula": "CO2", "state": "g", "stoich": 1}, {"formula": "H2O", "state": "g", "stoich": 2}],
        "activationTempC": 550, "requiredMedium": "Neutral",
        "catalystAllowed": False, "catalystDeltaTempC": 0,
        "visual_effects": {"color_change": "#FFAA00", "precipitate": False, "gas": True, "temperature_delta": 100, "sound_id": "burn", "glow": True, "sparks": True, "smoke": True},
        "safety": {"ghs_icons": ["GHS02"], "warnings_en": ["Methane is highly flammable.", "CO2 asphyxiation risk in enclosed spaces.", "Explosion risk with air mixtures (5-15%)."]},
        "observation_en": "A clean blue flame with intense heat. Water vapor condenses on cold surfaces. CO2 is odorless but detectable with limewater (turns milky).",
        "explanation_en": "Natural gas (methane) undergoes complete combustion in excess oxygen. The blue flame indicates complete combustion. CH4 + 2O2 -> CO2 + 2H2O. Incomplete combustion produces soot (C) and toxic CO.",
        "condition_notes": "Requires ignition source. Complete combustion requires sufficient O2 supply. Blue flame = complete, yellow/orange = incomplete.",
        "requiresHeating": True, "requiresCatalyst": False,
        "safety_notes": "Explosive in air between 5-15% concentration. Install gas detector. Ensure ventilation to prevent CO2 buildup."
    },
    {
        "id": "rxn_133",
        "name_en": "Cracking of Decane (Thermal Cracking)",
        "reactionType": "Decomposition",
        "reactants": [{"formula": "C10H22", "state": "l", "stoich": 1}],
        "products": [{"formula": "C8H18", "state": "l", "stoich": 1}, {"formula": "C2H4", "state": "g", "stoich": 1}],
        "activationTempC": 500, "requiredMedium": "Neutral",
        "catalystAllowed": True, "catalystDeltaTempC": 150,
        "visual_effects": {"color_change": "#8B4513", "precipitate": False, "gas": True, "temperature_delta": 20, "sound_id": "", "smoke": True},
        "safety": {"ghs_icons": ["GHS02"], "warnings_en": ["Hydrocarbons are flammable.", "High temperature hazard.", "Ethene is flammable."]},
        "observation_en": "The long-chain hydrocarbon breaks down into smaller molecules. A gas (ethene) is evolved and the remaining liquid has different properties.",
        "explanation_en": "Cracking breaks long hydrocarbon chains into more useful shorter ones. Thermal cracking: high temperature (500°C), high pressure. Catalytic cracking: 450°C, zeolite catalyst. Produces alkenes (for plastics) and shorter alkanes (for fuel).",
        "condition_notes": "Industrial process: 500°C (thermal) or 450°C with zeolite catalyst (catalytic cracking).",
        "requiresHeating": True, "requiresCatalyst": False,
        "safety_notes": "Flammable hydrocarbons. High temperature. Ensure no air ingress (explosion risk)."
    },
    {
        "id": "rxn_134",
        "name_en": "Sodium in Water (with phenolphthalein)",
        "reactionType": "Redox",
        "reactants": [{"formula": "Na", "state": "s", "stoich": 2}, {"formula": "H2O", "state": "l", "stoich": 2}],
        "products": [{"formula": "NaOH", "state": "aq", "stoich": 2}, {"formula": "H2", "state": "g", "stoich": 1}],
        "activationTempC": 15, "requiredMedium": "Neutral",
        "catalystAllowed": False, "catalystDeltaTempC": 0,
        "visual_effects": {"color_change": "#FF1493", "precipitate": False, "gas": True, "temperature_delta": 15, "sound_id": "fizz", "glow": True, "sparks": True},
        "safety": {"ghs_icons": ["GHS02", "GHS05"], "warnings_en": ["Sodium metal reacts explosively with water.", "Severe caustic burn hazard from NaOH.", "Hydrogen gas is flammable.", "Use only small pieces."]},
        "observation_en": "Sodium metal fizzes vigorously on water, skating across the surface. A lilac flame appears as hydrogen ignites. Phenolphthalein indicator turns bright pink showing base formation.",
        "explanation_en": "Sodium reacts violently with water producing sodium hydroxide and hydrogen gas. The heat of reaction ignites the hydrogen (lilac flame is characteristic of Na). 2Na + 2H2O -> 2NaOH + H2. The pink color with phenolphthalein confirms the basic product.",
        "condition_notes": "Always use a small piece (<0.5g). Use a safety screen. Water trough experiment with phenolphthalein indicator.",
        "requiresHeating": False, "requiresCatalyst": False,
        "safety_notes": "DANGER: Sodium explodes with water. Use ONLY small pieces (<0.5g). Wear face shield and safety screen. Keep dry sand (NOT water) for fire extinguishing."
    },
    {
        "id": "rxn_135",
        "name_en": "Saponification: Soap Making",
        "reactionType": "Hydrolysis",
        "reactants": [{"formula": "C3H5(C18H35O2)3", "state": "l", "stoich": 1}, {"formula": "NaOH", "state": "aq", "stoich": 3}],
        "products": [{"formula": "C3H5(OH)3", "state": "l", "stoich": 1}, {"formula": "C18H35O2Na", "state": "s", "stoich": 3}],
        "activationTempC": 60, "requiredMedium": "Basic",
        "catalystAllowed": False, "catalystDeltaTempC": 0,
        "visual_effects": {"color_change": "#FFF5E6", "precipitate": True, "gas": False, "temperature_delta": 5, "sound_id": "", "foam": True},
        "safety": {"ghs_icons": ["GHS05"], "warnings_en": ["NaOH is corrosive.", "Hot fat causes burns.", "Soap is slippery - clean up spills."]},
        "observation_en": "The oil/fat mixture thickens into a creamy paste as soap forms. Glycerol (a byproduct) separates. The final product is soap that lathers with water.",
        "explanation_en": "Triglycerides (fats/oils) react with sodium hydroxide in a saponification reaction. The ester bonds are hydrolyzed, producing glycerol and sodium salts of fatty acids (soap). Soap molecules have a hydrophilic head and hydrophobic tail, enabling emulsification of grease.",
        "condition_notes": "Requires gentle heating (60°C) and stirring. The reaction takes 30-60 minutes. Salt is added at the end to precipitate soap (salting out).",
        "requiresHeating": True, "requiresCatalyst": False,
        "safety_notes": "Caustic NaOH solution - wear gloves and goggles. Hot fat can cause burns. Do NOT use aluminum containers (reacts with NaOH)."
    },
    {
        "id": "rxn_136",
        "name_en": "Reaction of Magnesium with Steam",
        "reactionType": "Redox",
        "reactants": [{"formula": "Mg", "state": "s", "stoich": 1}, {"formula": "H2O", "state": "g", "stoich": 1}],
        "products": [{"formula": "MgO", "state": "s", "stoich": 1}, {"formula": "H2", "state": "g", "stoich": 1}],
        "activationTempC": 350, "requiredMedium": "Neutral",
        "catalystAllowed": False, "catalystDeltaTempC": 0,
        "visual_effects": {"color_change": "#FFFFFF", "precipitate": False, "gas": True, "temperature_delta": 40, "sound_id": "sizzle", "glow": True, "smoke": True},
        "safety": {"ghs_icons": ["GHS02"], "warnings_en": ["Hot apparatus.", "Burning magnesium - do not look directly.", "Hydrogen gas is flammable."]},
        "observation_en": "When heated magnesium reacts with steam producing a brilliant white flame. White magnesium oxide powder and hydrogen gas are produced.",
        "explanation_en": "Magnesium reacts with steam (gaseous water) more vigorously than with liquid water because the higher temperature overcomes the activation energy. Mg + H2O(g) -> MgO + H2. The hydrogen gas can be collected and tested with a burning splint (pop test).",
        "condition_notes": "Requires heating magnesium to ignition temperature in steam atmosphere. More vigorous than Mg + cold water.",
        "requiresHeating": True, "requiresCatalyst": False,
        "safety_notes": "Bright white light - use welding goggles or do not look directly. Hot apparatus."
    },
    {
        "id": "rxn_137",
        "name_en": "Ostwald's Dilution Law (Weak Acid)",
        "reactionType": "Dissociation",
        "reactants": [{"formula": "CH3COOH", "state": "aq", "stoich": 1}],
        "products": [{"formula": "CH3COO", "state": "aq", "stoich": 1}, {"formula": "H", "state": "aq", "stoich": 1}],
        "activationTempC": 15, "requiredMedium": "Neutral",
        "catalystAllowed": False, "catalystDeltaTempC": 0,
        "visual_effects": {"color_change": "", "precipitate": False, "gas": False, "temperature_delta": 0, "sound_id": ""},
        "safety": {"ghs_icons": [], "warnings_en": ["Dilute acetic acid is generally safe.", "Avoid contact with eyes."]},
        "observation_en": "Weak acid partially dissociates in water. The extent of dissociation increases with dilution according to Ostwald's dilution law.",
        "explanation_en": "Weak acids like acetic acid only partially dissociate in water. The equilibrium constant Ka = [H+][A-]/[HA]. As the solution is diluted, the degree of dissociation (alpha) increases. CH3COOH <-> CH3COO- + H+.",
        "condition_notes": "Equilibrium reaction. Ka for acetic acid = 1.8e-5 at 25°C. Conductivity measurements confirm degree of dissociation.",
        "requiresHeating": False, "requiresCatalyst": False,
        "safety_notes": ""
    },
    {
        "id": "rxn_138",
        "name_en": "Common Ion Effect: CaF2 Solubility",
        "reactionType": "Equilibrium",
        "reactants": [{"formula": "CaF2", "state": "s", "stoich": 1}],
        "products": [{"formula": "Ca", "state": "aq", "stoich": 1}, {"formula": "F", "state": "aq", "stoich": 2}],
        "activationTempC": 15, "requiredMedium": "Neutral",
        "catalystAllowed": False, "catalystDeltaTempC": 0,
        "visual_effects": {"color_change": "", "precipitate": True, "gas": False, "temperature_delta": 0, "sound_id": ""},
        "safety": {"ghs_icons": [], "warnings_en": ["Fluoride compounds are toxic in large amounts.", "Avoid ingestion."]},
        "observation_en": "Adding CaCl2 or NaF to a saturated CaF2 solution causes more CaF2 to precipitate (common ion effect reduces solubility).",
        "explanation_en": "The solubility equilibrium of CaF2 is: CaF2(s) <-> Ca2+ + 2F-. Adding a common ion (Ca2+ or F-) shifts the equilibrium toward the solid, reducing solubility (Le Chatelier's principle). Ksp = [Ca2+][F-]^2 = 3.9e-11.",
        "condition_notes": "Ksp = 3.9e-11. Adding common ions (from CaCl2 or NaF) reduces molar solubility significantly.",
        "requiresHeating": False, "requiresCatalyst": False,
        "safety_notes": ""
    },
]

# Add new reactions
existing_ids = {r["id"] for r in data}
added = 0
for rxn in new_reactions:
    if rxn["id"] not in existing_ids:
        data.append(rxn)
        added += 1
        existing_ids.add(rxn["id"])

with open("Assets/_Project/DataSrc/reactions.json", "w") as f:
    json.dump(data, f, indent=2)

print(f"Added {added} new reactions")
print(f"Total reactions: {len(data)}")

# Print categories
from collections import Counter
cats = Counter(r.get("reactionType", "Unknown") for r in data)
print("\nReaction type breakdown:")
for cat, count in sorted(cats.items()):
    print(f"  {cat}: {count}")
