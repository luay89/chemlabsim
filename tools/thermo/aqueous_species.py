"""How each aqueous species of reactions.json is written in its standard state.

ΔfH° of a strong electrolyte in water (standard state: ideal 1 mol/kg solution, fully ionised,
the "ai" state of the NBS tables) is the sum of the ΔfH° of its ions. That is how the NBS tables
themselves build the "ai" values, so a salt does not need its own entry.

Weak electrolytes and molecular solutes stay as undissociated molecules (the NBS "ao" state):
at bench concentrations they are mostly un-ionised, and their ionisation enthalpies are small
next to a reaction enthalpy. These are looked up as (formula, "aq") in the data files.

Assumption, stated in the app: H2SO4 is taken as fully ionised (2 H⁺ + SO₄²⁻), as in the NBS "ai"
state; at bench concentrations its second proton is only partly released.

Every entry here is explicit so a reviewer can check it line by line.
"""

# formula in reactions.json -> {ion: count}
STRONG_ELECTROLYTES = {
    # acids
    "HCl": {"H+": 1, "Cl-": 1},
    "HBr": {"H+": 1, "Br-": 1},
    "HI": {"H+": 1, "I-": 1},
    "HNO3": {"H+": 1, "NO3-": 1},
    "H2SO4": {"H+": 2, "SO4-2": 1},
    "NaHSO4": {"Na+": 1, "HSO4-": 1},
    "KHSO4": {"K+": 1, "HSO4-": 1},
    # hydroxides
    "NaOH": {"Na+": 1, "OH-": 1},
    "KOH": {"K+": 1, "OH-": 1},
    "LiOH": {"Li+": 1, "OH-": 1},
    "Ca(OH)2": {"Ca+2": 1, "OH-": 2},
    "Ba(OH)2": {"Ba+2": 1, "OH-": 2},
    # chlorides
    "NaCl": {"Na+": 1, "Cl-": 1},
    "KCl": {"K+": 1, "Cl-": 1},
    "LiCl": {"Li+": 1, "Cl-": 1},
    "NH4Cl": {"NH4+": 1, "Cl-": 1},
    "CaCl2": {"Ca+2": 1, "Cl-": 2},
    "BaCl2": {"Ba+2": 1, "Cl-": 2},
    "MgCl2": {"Mg+2": 1, "Cl-": 2},
    "ZnCl2": {"Zn+2": 1, "Cl-": 2},
    "CuCl2": {"Cu+2": 1, "Cl-": 2},
    "FeCl2": {"Fe+2": 1, "Cl-": 2},
    "FeCl3": {"Fe+3": 1, "Cl-": 3},
    "AlCl3": {"Al+3": 1, "Cl-": 3},
    "MnCl2": {"Mn+2": 1, "Cl-": 2},
    "NiCl2": {"Ni+2": 1, "Cl-": 2},
    "SnCl2": {"Sn+2": 1, "Cl-": 2},
    "CrCl2": {"Cr+2": 1, "Cl-": 2},
    "CrCl3": {"Cr+3": 1, "Cl-": 3},
    # bromides, iodides, fluorides
    "NaBr": {"Na+": 1, "Br-": 1},
    "KBr": {"K+": 1, "Br-": 1},
    "MgBr2": {"Mg+2": 1, "Br-": 2},
    "ZnBr2": {"Zn+2": 1, "Br-": 2},
    "NaI": {"Na+": 1, "I-": 1},
    "KI": {"K+": 1, "I-": 1},
    "ZnI2": {"Zn+2": 1, "I-": 2},
    "NaF": {"Na+": 1, "F-": 1},
    "KF": {"K+": 1, "F-": 1},
    # nitrates
    "AgNO3": {"Ag+": 1, "NO3-": 1},
    "NaNO3": {"Na+": 1, "NO3-": 1},
    "KNO3": {"K+": 1, "NO3-": 1},
    "LiNO3": {"Li+": 1, "NO3-": 1},
    "NH4NO3": {"NH4+": 1, "NO3-": 1},
    "Pb(NO3)2": {"Pb+2": 1, "NO3-": 2},
    "Cu(NO3)2": {"Cu+2": 1, "NO3-": 2},
    "Mg(NO3)2": {"Mg+2": 1, "NO3-": 2},
    "Zn(NO3)2": {"Zn+2": 1, "NO3-": 2},
    "Ba(NO3)2": {"Ba+2": 1, "NO3-": 2},
    "Ca(NO3)2": {"Ca+2": 1, "NO3-": 2},
    "Fe(NO3)2": {"Fe+2": 1, "NO3-": 2},
    "Fe(NO3)3": {"Fe+3": 1, "NO3-": 3},
    "NaNO2": {"Na+": 1, "NO2-": 1},
    # sulfates
    "CuSO4": {"Cu+2": 1, "SO4-2": 1},
    "Na2SO4": {"Na+": 2, "SO4-2": 1},
    "K2SO4": {"K+": 2, "SO4-2": 1},
    "Li2SO4": {"Li+": 2, "SO4-2": 1},
    "(NH4)2SO4": {"NH4+": 2, "SO4-2": 1},
    "MgSO4": {"Mg+2": 1, "SO4-2": 1},
    "ZnSO4": {"Zn+2": 1, "SO4-2": 1},
    "FeSO4": {"Fe+2": 1, "SO4-2": 1},
    "MnSO4": {"Mn+2": 1, "SO4-2": 1},
    "NiSO4": {"Ni+2": 1, "SO4-2": 1},
    "SnSO4": {"Sn+2": 1, "SO4-2": 1},
    "Al2(SO4)3": {"Al+3": 2, "SO4-2": 3},
    "Fe2(SO4)3": {"Fe+3": 2, "SO4-2": 3},
    "Cr2(SO4)3": {"Cr+3": 2, "SO4-2": 3},
    # carbonates, phosphates, sulfites, sulfides, thiosulfate
    "Na2CO3": {"Na+": 2, "CO3-2": 1},
    "K2CO3": {"K+": 2, "CO3-2": 1},
    "Li2CO3": {"Li+": 2, "CO3-2": 1},
    "NaHCO3": {"Na+": 1, "HCO3-": 1},
    "Na3PO4": {"Na+": 3, "PO4-3": 1},
    "K3PO4": {"K+": 3, "PO4-3": 1},
    "Na2SO3": {"Na+": 2, "SO3-2": 1},
    "Na2S": {"Na+": 2, "S-2": 1},
    "Na2S2O3": {"Na+": 2, "S2O3-2": 1},
    "Na2S4O6": {"Na+": 2, "S4O6-2": 1},
    # acetates
    "CH3COONa": {"Na+": 1, "CH3COO-": 1},
    "CH3COOK": {"K+": 1, "CH3COO-": 1},
    "CH3COOLi": {"Li+": 1, "CH3COO-": 1},
    "CH3COONH4": {"NH4+": 1, "CH3COO-": 1},
    "Ca(CH3COO)2": {"Ca+2": 1, "CH3COO-": 2},
    "Mg(CH3COO)2": {"Mg+2": 1, "CH3COO-": 2},
    "Zn(CH3COO)2": {"Zn+2": 1, "CH3COO-": 2},
    "Cu(CH3COO)2": {"Cu+2": 1, "CH3COO-": 2},
    # oxo-anions of transition metals, hypochlorite, thiocyanate, aluminate, zincate, silicate
    "KMnO4": {"K+": 1, "MnO4-": 1},
    "K2Cr2O7": {"K+": 2, "Cr2O7-2": 1},
    "K2CrO4": {"K+": 2, "CrO4-2": 1},
    "NaClO": {"Na+": 1, "ClO-": 1},
    "KClO": {"K+": 1, "ClO-": 1},
    "KSCN": {"K+": 1, "SCN-": 1},
    "NaAlO2": {"Na+": 1, "AlO2-": 1},
    "Na2ZnO2": {"Na+": 2, "ZnO2-2": 1},
    "Na2SiO3": {"Na+": 2, "SiO3-2": 1},
}

# Ions written without their charge in net ionic equations (rxn_137, rxn_138).
BARE_IONS = {
    "H": "H+",
    "Ca": "Ca+2",
    "F": "F-",
    "CH3COO": "CH3COO-",
}

# NH4OH(aq) is aqueous ammonia: NH3(aq) + H2O(l), the NBS convention.
ALIASES_AQ = {
    "NH4OH": {"NH3": ("aq", 1), "H2O": ("l", 1)},
}
