# Reaction data corrections, stage 2 (2026-09-27)

Source: Assets/_Project/DataSrc/reactions.json (147 -> 137 records). Backup of previous files: Backups/reactions-data-2026-09-27-before-stage2/

```
REMOVED duplicate rxn_107 (Electrolysis of Water) - older copy; first copy kept
REMOVED duplicate rxn_108 (Ozone Decomposition) - older copy; first copy kept
REMOVED duplicate rxn_109 (Photosynthesis (Simplified)) - older copy; first copy kept
REMOVED duplicate rxn_110 (Thermal Decomposition of Copper Carbonate) - older copy; first copy kept
REMOVED duplicate rxn_111 (Displacement of Copper by Iron) - older copy; first copy kept
REMOVED duplicate rxn_112 (Esterification: Ethyl Acetate) - older copy; first copy kept
REMOVED duplicate rxn_113 (Iodine Clock Reaction) - older copy; first copy kept
REMOVED duplicate rxn_114 (Burning Sugar (Caramelization)) - older copy; first copy kept
REMOVED duplicate rxn_115 (Phenolpthalein Color Change) - older copy; first copy kept
REMOVED rxn_126 (Disproportionation of H2O2) - same reaction and key as rxn_101, unreachable
rxn_007: 2H2O2 + 1MnO2 -> 2H2O + 1O2  ==>  2H2O2 + 1MnO2 -> 2H2O + 1O2 + 1MnO2   [catalyst MnO2 is regenerated]
rxn_014: 1CuSO4 + 2NaOH -> 1Na2SO4 + 1Cu  ==>  1CuSO4 + 2NaOH -> 1Na2SO4 + 1Cu(OH)2   [precipitate is Cu(OH)2, not Cu]
rxn_039: 1FeCl3 + 3NaOH -> 3NaCl + 1Fe2O3  ==>  1FeCl3 + 3NaOH -> 3NaCl + 1Fe(OH)3   [precipitate is Fe(OH)3]
rxn_040: 1NH4NO3 + 1H2O -> 1NH4NO3  ==>  1NH4NO3 + 1H2O -> 1NH4NO3 + 1H2O   [water is the solvent, unchanged]
rxn_041: 1AgNO3 + 1KBr -> 1AgCl + 1KNO3  ==>  1AgNO3 + 1KBr -> 1AgBr + 1KNO3   [precipitate is AgBr, not AgCl]
rxn_042: 1K2Cr2O7 + 1H2SO4 + 1C2H5OH -> 1Cr2O3 + 1K2SO4 + 1CH3COOH + 4H2O  ==>  2K2Cr2O7 + 8H2SO4 + 3C2H5OH -> 2Cr2(SO4)3 + 2K2SO4 + 3CH3COOH + 11H2O   [acidified dichromate gives Cr3+ sulfate (green)]
rxn_057: 1Cu + 2AgNO3 -> 2Ag  ==>  1Cu + 2AgNO3 -> 2Ag + 1Cu(NO3)2   [Cu(NO3)2 forms (solution turns blue)]
rxn_058: 1Fe + 1CuCl2 -> 1Cu + 1FeCl3  ==>  1Fe + 1CuCl2 -> 1Cu + 1FeCl2   [Fe + CuCl2 gives FeCl2]
rxn_068: 1C12H22O11 + 1H2SO4 -> 12C + 11H2O  ==>  1C12H22O11 + 1H2SO4 -> 12C + 11H2O + 1H2SO4   [H2SO4 is the dehydrating agent, not consumed]
rxn_069: 8HNO3 + 3Cu -> 1Cu(NO3)2 + 2NO2 + 2H2O  ==>  4HNO3 + 1Cu -> 1Cu(NO3)2 + 2NO2 + 2H2O   [concentrated HNO3 (brown NO2) stoichiometry]
rxn_080: 4NH3 + 5O2 -> 4NO2 + 6H2O  ==>  4NH3 + 7O2 -> 4NO2 + 6H2O   [overall NH3 -> NO2 (NO oxidized in air)]
rxn_094: 2KMnO4 -> 2MnO2 + 1O2  ==>  2KMnO4 -> 1MnO2 + 1O2 + 1K2MnO4   [K2MnO4 is also a product]
rxn_095: 2KNO3 -> 1O2  ==>  2KNO3 -> 1O2 + 2KNO2   [residue is KNO2]
rxn_100: 1AgNO3 + 1KI -> 1KNO3  ==>  1AgNO3 + 1KI -> 1KNO3 + 1AgI   [yellow AgI precipitate was missing]
rxn_113: 1H2O2 + 1KI + 1H2SO4 -> 1I2 + 1K2SO4 + 2H2O  ==>  1H2O2 + 2KI + 1H2SO4 -> 1I2 + 1K2SO4 + 2H2O   [2 KI per I2]
rxn_115: 1C20H14O4 + 2NaOH -> 1C20H12Na2O4 + 1H2O  ==>  1C20H14O4 + 2NaOH -> 1C20H12Na2O4 + 2H2O   [2 H2O; colour pink per its own observation]
rxn_115 visual: color_change #000066 -> #FF1493, crystallization True -> False, turbidity True -> False
```

## Deferred (need reactant changes, ion notation, or an electric-current condition)

```
10 rxn_011 Ferric Thiocyanate Complex Formation | 2FeSO4 + 1H2O2 + 1H2SO4 + 2KSCN -> 2FeSCN2+ + 1K2SO4 + 2H2O | L-R {'N': -2, 'O': 8, 'S': 2}
55 rxn_056 Iron(III) Chloride and Sodium Carbonate | 2FeCl3 + 3Na2CO3 -> 6NaCl + 3CO2 | L-R {'Fe': 2, 'O': 3}
86 rxn_087 Iron(III) Chloride and Potassium Thiocya | 1FeCl3 + 1KSCN -> 1FeSCN2+ + 3KCl | L-R {'K': -2, 'N': -1}
97 rxn_098 Sodium Hydroxide and Aluminum (Amphoteri | 2NaOH + 2Al -> 2NaAlO2 + 3H2 | L-R {'H': -4, 'O': -2}
120 rxn_121 Oxidation of Ethanol to Ethanoic Acid | 1C2H5OH + 1K2Cr2O7 -> 1CH3COOH + 1Cr2O3 + 1K2SO4 | L-R {'H': 2, 'O': -1, 'S': -1}
122 rxn_123 Copper Electroplating | 1CuSO4 -> 1Cu | L-R {'O': 4, 'S': 1}
126 rxn_128 Blue Bottle Experiment (Redox Indicator) | 1C6H12O6 + 1NaOH -> 1C6H12O7 | L-R {'H': 1, 'Na': 1}
unbalanced 7 of 137
rxn_137, rxn_138: ions written without charges (CH3COO, H, Ca, F)
8 reactant-key collisions remain (second reaction unreachable): rxn_134, rxn_111, rxn_125, rxn_132, rxn_106, rxn_131, rxn_127, rxn_109
```
