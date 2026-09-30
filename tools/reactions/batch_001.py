"""Batch 001 — common school / first-year university reactions.

Every equation is mass-balanced (checked by reactions_tool.py) and uses only
reagents already present in materials.json. requiredMedium is left empty: the
reagents themselves set the medium, so the student does not have to guess it.
"""
from reactions_tool import parse_equation

# formula -> (GHS icon, warning)
HAZ = {
    "HCl": ("GHS05", "Hydrochloric acid is corrosive."),
    "H2SO4": ("GHS05", "Sulfuric acid is highly corrosive."),
    "HNO3": ("GHS05", "Nitric acid is corrosive and oxidizing."),
    "HBr": ("GHS05", "Hydrobromic acid is corrosive."),
    "HI": ("GHS05", "Hydroiodic acid is corrosive."),
    "HF": ("GHS06", "Hydrofluoric acid is extremely toxic and penetrates skin."),
    "H3PO4": ("GHS05", "Phosphoric acid is corrosive."),
    "CH3COOH": ("GHS05", "Concentrated acetic acid is corrosive."),
    "NaOH": ("GHS05", "NaOH is corrosive."),
    "KOH": ("GHS05", "KOH is corrosive."),
    "LiOH": ("GHS05", "LiOH is corrosive."),
    "Ba(OH)2": ("GHS06", "Barium compounds are toxic."),
    "BaCl2": ("GHS06", "Barium compounds are toxic."),
    "BaO": ("GHS06", "Barium compounds are toxic."),
    "Ca(OH)2": ("GHS05", "Calcium hydroxide irritates skin and eyes."),
    "CaO": ("GHS05", "Quicklime is corrosive and reacts with moisture."),
    "NH3": ("GHS06", "Ammonia gas is toxic and irritating; use a fume hood."),
    "AgNO3": ("GHS05", "Silver nitrate stains and burns skin."),
    "Pb(NO3)2": ("GHS06", "Lead compounds are toxic."),
    "Pb": ("GHS06", "Lead and its compounds are toxic."),
    "CuSO4": ("GHS07", "Copper salts are harmful if swallowed."),
    "CuCl2": ("GHS07", "Copper salts are harmful if swallowed."),
    "FeCl3": ("GHS05", "Iron(III) chloride is corrosive."),
    "NiCl2": ("GHS08", "Nickel salts are sensitizers and suspected carcinogens."),
    "K2Cr2O7": ("GHS08", "Dichromate is toxic and carcinogenic."),
    "KMnO4": ("GHS03", "Potassium permanganate is a strong oxidizer."),
    "KNO3": ("GHS03", "Potassium nitrate is an oxidizer."),
    "NH4NO3": ("GHS03", "Ammonium nitrate is an oxidizer."),
    "H2O2": ("GHS03", "Hydrogen peroxide is an oxidizer and irritant."),
    "NaClO": ("GHS05", "Bleach is corrosive; never mix it with acids or ammonia."),
    "Cl2": ("GHS06", "Chlorine gas is toxic; use a fume hood."),
    "Br2": ("GHS06", "Bromine is toxic and corrosive; use a fume hood."),
    "I2": ("GHS07", "Iodine is harmful and stains skin."),
    "H2S": ("GHS06", "Hydrogen sulfide is very toxic; use a fume hood."),
    "SO2": ("GHS06", "Sulfur dioxide is toxic; use a fume hood."),
    "SO3": ("GHS05", "Sulfur trioxide is highly corrosive."),
    "NO2": ("GHS06", "Nitrogen dioxide is toxic; use a fume hood."),
    "NO": ("GHS06", "Nitrogen oxides are toxic; use a fume hood."),
    "CO": ("GHS06", "Carbon monoxide is toxic."),
    "P2O5": ("GHS05", "Phosphorus pentoxide is corrosive."),
    "P": ("GHS02", "Phosphorus is highly flammable and toxic."),
    "H2": ("GHS02", "Hydrogen gas is flammable; keep flames away."),
    "O2": ("GHS03", "Oxygen strongly supports combustion."),
    "Na": ("GHS02", "Sodium reacts violently with water."),
    "K": ("GHS02", "Potassium reacts violently with water."),
    "Li": ("GHS02", "Lithium reacts with water and burns fiercely."),
    "Ca": ("GHS02", "Calcium releases flammable hydrogen with water."),
    "Mg": ("GHS02", "Burning magnesium is blinding; do not look directly at it."),
    "Al": ("GHS02", "Aluminium powder is flammable."),
    "Zn": ("GHS02", "Zinc powder is flammable."),
    "S": ("GHS07", "Burning sulfur gives toxic SO2."),
    "C2H5OH": ("GHS02", "Ethanol is flammable."),
    "CH3OH": ("GHS06", "Methanol is toxic and flammable."),
    "CH3COCH3": ("GHS02", "Acetone is highly flammable."),
    "C6H6": ("GHS08", "Benzene is carcinogenic and flammable."),
    "C2H2": ("GHS02", "Acetylene is extremely flammable."),
    "C2H4": ("GHS02", "Ethene is extremely flammable."),
    "CH4": ("GHS02", "Methane is extremely flammable."),
    "C10H22": ("GHS02", "Decane is flammable."),
    "HCHO": ("GHS08", "Formaldehyde is toxic and carcinogenic."),
    "SiO2": ("GHS08", "Do not inhale fine silica dust."),
    "Cr2O3": ("GHS07", "Avoid inhaling chromium oxide dust."),
    "Na2O": ("GHS05", "Sodium oxide is corrosive."),
}

REACTIONS = []


def R(name, rtype, eq, obs, expl, temp=20, heat=False, cat=False, dT=0, color="", ppt="",
      eff="none", sound="", flags=(), ghs=None, warn=None, cond=""):
    reactants, products = parse_equation(eq)
    gas = eff != "none" or any(p["state"] == "g" for p in products)

    icons, warnings = [], []
    for c in reactants + products:
        if c["formula"] in HAZ:
            icon, text = HAZ[c["formula"]]
            if icon not in icons:
                icons.append(icon)
            if text not in warnings:
                warnings.append(text)
    if ghs is not None:
        icons = list(ghs)
    if warn:
        warnings = list(warn) + [w for w in warnings if w not in warn]
    if heat:
        warnings.append("Hot apparatus; handle with tongs.")
    if not warnings:
        warnings = ["Wear goggles and gloves."]
    warnings = warnings[:4]

    if not cond:
        cond = ("Requires heating to about %d°C." % temp) if heat else "Proceeds at room temperature."
        if cat:
            cond += " Needs a catalyst."

    vfx = {
        "color_change": color, "precipitate": bool(ppt), "gas": gas, "temperature_delta": dT,
        "sound_id": sound, "dissolution": "dissolution" in flags, "crystallization": "crystallization" in flags,
        "effervescence": eff, "vortex": False, "vapor": "vapor" in flags, "turbidity": bool(ppt) or "turbidity" in flags,
        "precipitate_color": ppt, "electrical": False,
    }
    for extra in ("glow", "sparks", "smoke", "foam", "frost"):
        if extra in flags:
            vfx[extra] = True

    REACTIONS.append({
        "name_en": name, "name_ar": "", "reactionType": rtype,
        "reactants": reactants, "products": products,
        "producesGas": gas, "activationTempC": temp, "requiredMedium": "",
        "catalystAllowed": cat, "catalystDeltaTempC": 0,
        "visual_effects": vfx,
        "safety": {"ghs_icons": icons, "warnings_en": warnings, "warnings_ar": []},
        "validation": {"require_acid_into_water_rule": False, "min_moles_threshold": 0.001},
        "observation_en": obs, "explanation_en": expl, "condition_notes": cond,
        "requiresHeating": heat, "requiresCatalyst": cat,
        "safety_notes": " ".join(warnings),
    })


# ── Category helpers ──────────────────────────────────────────────────────

def neut(name, eq, salt, weak=""):
    R(name, "Neutralization", eq,
      "No visible change: two colorless solutions give a colorless solution of %s. The mixture warms up." % salt,
      "H⁺ from the acid and OH⁻ from the base combine to form water (H⁺ + OH⁻ → H₂O). "
      "The remaining ions stay dissolved as %s. The reaction is exothermic.%s" % (salt, (" " + weak) if weak else ""),
      dT=12, sound="mix")


def oxide_acid(name, eq, salt, solution, color="", temp=25):
    R(name, "Neutralization", eq,
      "The solid oxide slowly dissolves in the acid, giving %s." % solution,
      "A basic metal oxide reacts with an acid to give a salt and water. O²⁻ from the oxide accepts "
      "protons from the acid, and the metal ion stays in solution as %s." % salt,
      temp=temp, dT=8, color=color, flags=("dissolution",), sound="mix")


def metal_acid(name, eq, metal, salt, eff="mild", color="", temp=20, note=""):
    R(name, "Gas Evolution", eq,
      "%s dissolves with %s bubbling of colorless hydrogen gas; the tube warms up.%s"
      % (metal, {"mild": "steady", "vigorous": "vigorous", "violent": "violent"}[eff], (" " + note) if note else ""),
      "%s is above hydrogen in the reactivity series, so it reduces H⁺ to H₂ gas and is oxidized to its "
      "ion, which stays in solution as %s." % (metal, salt),
      temp=temp, dT=15, eff=eff, color=color, sound="fizz")


def carb_acid(name, eq, salt, eff="vigorous", color=""):
    R(name, "Gas Evolution", eq,
      "Brisk fizzing: colorless CO₂ gas bubbles out and the solid disappears, leaving a solution of %s." % salt,
      "The acid protonates the carbonate to carbonic acid, which is unstable and breaks down at once into "
      "water and carbon dioxide gas (H₂CO₃ → H₂O + CO₂). The salt %s remains." % salt,
      dT=3, eff=eff, color=color, sound="fizz")


def precip(name, eq, solid, look, hexcolor, extra="", color=""):
    R(name, "Precipitation", eq,
      "A %s precipitate of %s forms immediately and the mixture turns cloudy.%s" % (look, solid, (" " + extra) if extra else ""),
      "A double displacement reaction: the ions swap partners. %s is insoluble in water, so it leaves the "
      "solution as a solid, while the other ions stay dissolved." % solid,
      ppt=hexcolor, color=color, sound="plop")


def displace(name, eq, active, less, deposit, solution, color="", hexppt="", dT=10):
    R(name, "Single Displacement", eq,
      "%s forms on the %s and %s" % (deposit, active.lower(), solution),
      "%s is more reactive than %s, so it gives electrons to the %s ions. %s atoms go into solution as ions "
      "and %s metal is deposited." % (active, less.lower(), less.lower(), active, less.lower()),
      dT=dT, color=color, ppt=hexppt, sound="mix")


def dissolve(name, eq, obs, expl, dT, color="", flags=("dissolution",)):
    R(name, "Thermal", eq, obs, expl + " This is a physical dissolution, not a chemical reaction, but it shows the energy change clearly.",
      dT=dT, color=color, flags=flags + (("frost",) if dT <= -10 else ()), sound="mix")


WHITE, BLUE, GREEN, BROWN, YELLOW, BLACK = "#FFFFFF", "#7EC8E3", "#6B8E23", "#8B4513", "#FFD700", "#1A1A1A"
CU_BLUE, CU_GREEN, FE_YELLOW, FE_GREEN, NI_GREEN = "#1E90FF", "#2E9E8F", "#C9A227", "#B8D8A8", "#4CAF50"

# ── 1. Acid + base ────────────────────────────────────────────────────────
neut("Hydrochloric Acid and Lithium Hydroxide", "HCl(aq) + LiOH(aq) -> LiCl(aq) + H2O(l)", "lithium chloride")
neut("Hydrochloric Acid and Barium Hydroxide", "2 HCl(aq) + Ba(OH)2(aq) -> BaCl2(aq) + 2 H2O(l)", "barium chloride")
neut("Nitric Acid and Potassium Hydroxide", "HNO3(aq) + KOH(aq) -> KNO3(aq) + H2O(l)", "potassium nitrate")
neut("Nitric Acid and Lithium Hydroxide", "HNO3(aq) + LiOH(aq) -> LiNO3(aq) + H2O(l)", "lithium nitrate")
neut("Nitric Acid and Calcium Hydroxide", "2 HNO3(aq) + Ca(OH)2(aq) -> Ca(NO3)2(aq) + 2 H2O(l)", "calcium nitrate")
neut("Sulfuric Acid and Lithium Hydroxide", "H2SO4(aq) + 2 LiOH(aq) -> Li2SO4(aq) + 2 H2O(l)", "lithium sulfate")
neut("Hydrobromic Acid and Sodium Hydroxide", "HBr(aq) + NaOH(aq) -> NaBr(aq) + H2O(l)", "sodium bromide")
neut("Hydroiodic Acid and Sodium Hydroxide", "HI(aq) + NaOH(aq) -> NaI(aq) + H2O(l)", "sodium iodide")
neut("Hydroiodic Acid and Potassium Hydroxide", "HI(aq) + KOH(aq) -> KI(aq) + H2O(l)", "potassium iodide")
neut("Hydrofluoric Acid and Potassium Hydroxide", "HF(aq) + KOH(aq) -> KF(aq) + H2O(l)", "potassium fluoride",
     "HF is a weak acid, so the salt solution is slightly basic.")
neut("Acetic Acid and Sodium Hydroxide", "CH3COOH(aq) + NaOH(aq) -> CH3COONa(aq) + H2O(l)", "sodium acetate",
     "Acetic acid is a weak acid, so the final solution is slightly basic (pH about 9).")
neut("Acetic Acid and Potassium Hydroxide", "CH3COOH(aq) + KOH(aq) -> CH3COOK(aq) + H2O(l)", "potassium acetate",
     "Acetic acid is a weak acid, so the final solution is slightly basic.")
neut("Acetic Acid and Calcium Hydroxide", "2 CH3COOH(aq) + Ca(OH)2(aq) -> Ca(CH3COO)2(aq) + 2 H2O(l)", "calcium acetate")
neut("Phosphoric Acid and Potassium Hydroxide", "H3PO4(aq) + 3 KOH(aq) -> K3PO4(aq) + 3 H2O(l)", "potassium phosphate",
     "H₃PO₄ is triprotic: it needs three OH⁻ per molecule for complete neutralization.")
neut("Nitric Acid and Magnesium Hydroxide", "2 HNO3(aq) + Mg(OH)2(s) -> Mg(NO3)2(aq) + 2 H2O(l)", "magnesium nitrate",
     "The white suspension of Mg(OH)₂ (milk of magnesia) clears as it dissolves.")
neut("Sulfuric Acid and Magnesium Hydroxide", "H2SO4(aq) + Mg(OH)2(s) -> MgSO4(aq) + 2 H2O(l)", "magnesium sulfate",
     "The white suspension of Mg(OH)₂ clears as it dissolves.")
neut("Sulfuric Acid and Aluminium Hydroxide", "3 H2SO4(aq) + 2 Al(OH)3(s) -> Al2(SO4)3(aq) + 6 H2O(l)", "aluminium sulfate",
     "Here amphoteric Al(OH)₃ behaves as a base.")
R("Sulfuric Acid and Calcium Hydroxide", "Neutralization", "H2SO4(aq) + Ca(OH)2(aq) -> CaSO4(s) + 2 H2O(l)",
  "The mixture warms and a fine white precipitate of calcium sulfate (gypsum) appears.",
  "A neutralization that also gives a sparingly soluble salt: H⁺ + OH⁻ form water, while Ca²⁺ and SO₄²⁻ "
  "combine into CaSO₄, which is only slightly soluble.", dT=12, ppt=WHITE, sound="mix")
R("Sulfuric Acid and Barium Hydroxide", "Neutralization", "H2SO4(aq) + Ba(OH)2(aq) -> BaSO4(s) + 2 H2O(l)",
  "A dense white precipitate of barium sulfate forms at once and the mixture warms. At the end point the liquid barely conducts electricity.",
  "Both products leave the solution: H⁺ + OH⁻ form water and Ba²⁺ + SO₄²⁻ form insoluble BaSO₄. "
  "With almost no ions left, conductivity drops to a minimum — a classic conductometric titration.",
  dT=12, ppt=WHITE, sound="plop")
R("Hydrogen Chloride and Ammonia", "Synthesis", "HCl(g) + NH3(g) -> NH4Cl(s)",
  "Dense white smoke appears where the two gases meet; it settles as a white powder.",
  "Ammonia (a base) accepts a proton from hydrogen chloride (an acid), forming solid ammonium chloride. "
  "The smoke ring forms nearer the HCl end because lighter NH₃ molecules diffuse faster.",
  dT=10, flags=("smoke",), ppt=WHITE, sound="whoosh")
R("Nitric Acid and Ammonia", "Neutralization", "HNO3(aq) + NH3(aq) -> NH4NO3(aq)",
  "No visible change; the solution warms and the sharp smell of ammonia disappears.",
  "Ammonia is a weak base that accepts H⁺ from nitric acid to form the ammonium ion. The product, ammonium nitrate, "
  "is an important fertilizer.", dT=10, sound="mix")
R("Sulfuric Acid and Ammonia", "Neutralization", "H2SO4(aq) + 2 NH3(aq) -> (NH4)2SO4(aq)",
  "No visible change; the solution warms and the smell of ammonia disappears.",
  "Each H₂SO₄ molecule donates two protons to two NH₃ molecules, forming ammonium sulfate, a common fertilizer.",
  dT=12, sound="mix")
R("Acetic Acid and Ammonia", "Neutralization", "CH3COOH(aq) + NH3(aq) -> CH3COONH4(aq)",
  "No visible change; the smells of vinegar and ammonia both fade and the solution warms slightly.",
  "A weak acid and a weak base neutralize each other to form ammonium acetate. Because both are weak, the solution "
  "ends up close to neutral (pH about 7).", dT=6, sound="mix")
R("Aluminium Hydroxide and Sodium Hydroxide", "Neutralization", "Al(OH)3(s) + NaOH(aq) -> NaAlO2(aq) + 2 H2O(l)",
  "The white gelatinous solid dissolves in excess alkali, giving a clear colorless solution.",
  "Al(OH)₃ is amphoteric: with a strong base it behaves as an acid and forms soluble sodium aluminate. "
  "The same solid also dissolves in acids.", dT=5, flags=("dissolution",), sound="mix")
R("Carbon Dioxide and Sodium Hydroxide", "Neutralization", "CO2(g) + 2 NaOH(aq) -> Na2CO3(aq) + H2O(l)",
  "The gas is absorbed by the solution with no visible change; the solution warms slightly.",
  "CO₂ is an acidic oxide. It reacts with the alkali to form sodium carbonate and water. "
  "This is how NaOH solutions absorb CO₂ from air.", dT=6, sound="mix")
R("Carbon Dioxide and Potassium Hydroxide", "Neutralization", "CO2(g) + 2 KOH(aq) -> K2CO3(aq) + H2O(l)",
  "The gas is absorbed by the solution with no visible change; the solution warms slightly.",
  "CO₂ is an acidic oxide and reacts with the alkali to form potassium carbonate and water.", dT=6, sound="mix")
R("Carbon Dioxide and Lithium Hydroxide", "Neutralization", "CO2(g) + 2 LiOH(aq) -> Li2CO3(aq) + H2O(l)",
  "The gas is absorbed with no visible change.",
  "LiOH absorbs the acidic oxide CO₂, forming lithium carbonate. Lithium hydroxide canisters are used in "
  "spacecraft and submarines to remove exhaled CO₂.", dT=6, sound="mix")
R("Carbon Dioxide and Limewater", "Precipitation", "CO2(g) + Ca(OH)2(aq) -> CaCO3(s) + H2O(l)",
  "Clear limewater turns milky as a white precipitate of calcium carbonate forms.",
  "CO₂ reacts with calcium hydroxide to give insoluble CaCO₃. This is the standard laboratory test for carbon dioxide.",
  ppt=WHITE, sound="bubble")
R("Carbon Dioxide and Barium Hydroxide", "Precipitation", "CO2(g) + Ba(OH)2(aq) -> BaCO3(s) + H2O(l)",
  "The clear solution turns milky as white barium carbonate precipitates.",
  "The acidic oxide CO₂ reacts with the base to give insoluble BaCO₃. Baryta water is an even more sensitive CO₂ test than limewater.",
  ppt=WHITE, sound="bubble")
R("Sulfur Dioxide and Water", "Synthesis", "SO2(g) + H2O(l) -> H2SO3(aq)",
  "The gas dissolves readily; the solution turns blue litmus red.",
  "SO₂ is an acidic oxide. It dissolves in water to form sulfurous acid, a weak acid. "
  "This reaction in clouds is one cause of acid rain.", dT=3, flags=("dissolution",), sound="mix")
R("Sulfur Dioxide and Sodium Hydroxide", "Neutralization", "SO2(g) + 2 NaOH(aq) -> Na2SO3(aq) + H2O(l)",
  "The choking gas is absorbed by the solution with no visible change.",
  "The acidic oxide SO₂ reacts with alkali to form sodium sulfite and water. Alkali scrubbers remove SO₂ from flue gases this way.",
  dT=6, sound="mix")
R("Sulfur Trioxide and Sodium Hydroxide", "Neutralization", "SO3(g) + 2 NaOH(aq) -> Na2SO4(aq) + H2O(l)",
  "The fumes are absorbed vigorously and the solution becomes hot.",
  "SO₃ is the anhydride of sulfuric acid. With alkali it forms sodium sulfate and water in a strongly exothermic reaction.",
  dT=25, sound="sizzle")
R("Nitrogen Dioxide and Water", "Redox", "3 NO2(g) + H2O(l) -> 2 HNO3(aq) + NO(g)",
  "The brown gas fades as it dissolves, leaving a colorless acidic solution.",
  "NO₂ disproportionates in water: part is oxidized to nitric acid and part is reduced to NO. "
  "This is the last step of the Ostwald process and a source of acid rain.", dT=5, sound="mix")
R("Nitrogen Dioxide and Sodium Hydroxide", "Redox", "2 NO2(g) + 2 NaOH(aq) -> NaNO2(aq) + NaNO3(aq) + H2O(l)",
  "The brown gas is absorbed and the solution stays colorless.",
  "NO₂ disproportionates in alkali into nitrite (N +3) and nitrate (N +5). Alkali is used to trap this toxic gas.",
  dT=6, sound="mix")
R("Phosphorus Pentoxide and Sodium Hydroxide", "Neutralization", "P2O5(s) + 6 NaOH(aq) -> 2 Na3PO4(aq) + 3 H2O(l)",
  "The white powder dissolves with hissing and the solution gets hot.",
  "P₂O₅ is the anhydride of phosphoric acid; with excess alkali it gives sodium phosphate and water.",
  dT=25, flags=("dissolution",), sound="sizzle")
R("Hydrogen Sulfide and Sodium Hydroxide", "Neutralization", "H2S(g) + 2 NaOH(aq) -> Na2S(aq) + 2 H2O(l)",
  "The rotten-egg smell disappears as the gas is absorbed; no visible change.",
  "H₂S is a weak diprotic acid. Excess alkali neutralizes it to sodium sulfide and water.", dT=6, sound="mix")
R("Dichromate to Chromate", "Neutralization", "K2Cr2O7(aq) + 2 KOH(aq) -> 2 K2CrO4(aq) + H2O(l)",
  "The orange solution turns bright yellow.",
  "Dichromate and chromate are in equilibrium: Cr₂O₇²⁻ + 2OH⁻ ⇌ 2CrO₄²⁻ + H₂O. Adding base shifts it "
  "to yellow chromate; adding acid brings the orange color back (Le Chatelier's principle).",
  color=YELLOW, sound="mix")

# ── 2. Metal oxide + acid ────────────────────────────────────────────────
oxide_acid("Magnesium Oxide and Sulfuric Acid", "MgO(s) + H2SO4(aq) -> MgSO4(aq) + H2O(l)", "magnesium sulfate", "a colorless solution")
oxide_acid("Magnesium Oxide and Nitric Acid", "MgO(s) + 2 HNO3(aq) -> Mg(NO3)2(aq) + H2O(l)", "magnesium nitrate", "a colorless solution")
oxide_acid("Calcium Oxide and Hydrochloric Acid", "CaO(s) + 2 HCl(aq) -> CaCl2(aq) + H2O(l)", "calcium chloride", "a colorless solution and noticeable heat")
oxide_acid("Barium Oxide and Hydrochloric Acid", "BaO(s) + 2 HCl(aq) -> BaCl2(aq) + H2O(l)", "barium chloride", "a colorless solution")
oxide_acid("Sodium Oxide and Hydrochloric Acid", "Na2O(s) + 2 HCl(aq) -> 2 NaCl(aq) + H2O(l)", "sodium chloride", "a colorless solution and a lot of heat")
oxide_acid("Copper(II) Oxide and Nitric Acid", "CuO(s) + 2 HNO3(aq) -> Cu(NO3)2(aq) + H2O(l)", "copper(II) nitrate",
           "a blue solution as the black powder disappears", color=CU_BLUE, temp=35)
oxide_acid("Copper(II) Oxide and Acetic Acid", "CuO(s) + 2 CH3COOH(aq) -> Cu(CH3COO)2(aq) + H2O(l)", "copper(II) acetate",
           "a blue-green solution on warming", color=CU_GREEN, temp=50)
oxide_acid("Iron(III) Oxide and Hydrochloric Acid", "Fe2O3(s) + 6 HCl(aq) -> 2 FeCl3(aq) + 3 H2O(l)", "iron(III) chloride",
           "a yellow-brown solution as the rust dissolves", color=FE_YELLOW, temp=40)
oxide_acid("Iron(III) Oxide and Sulfuric Acid", "Fe2O3(s) + 3 H2SO4(aq) -> Fe2(SO4)3(aq) + 3 H2O(l)", "iron(III) sulfate",
           "a pale yellow-brown solution as the rust dissolves", color=FE_YELLOW, temp=50)
oxide_acid("Zinc Oxide and Nitric Acid", "ZnO(s) + 2 HNO3(aq) -> Zn(NO3)2(aq) + H2O(l)", "zinc nitrate", "a colorless solution")
oxide_acid("Aluminium Oxide and Hydrochloric Acid", "Al2O3(s) + 6 HCl(aq) -> 2 AlCl3(aq) + 3 H2O(l)", "aluminium chloride",
           "a colorless solution on warming", temp=60)
R("Magnetite and Hydrochloric Acid", "Neutralization", "Fe3O4(s) + 8 HCl(aq) -> FeCl2(aq) + 2 FeCl3(aq) + 4 H2O(l)",
  "The black solid dissolves on warming, giving a yellow-green solution.",
  "Fe₃O₄ contains both Fe²⁺ and Fe³⁺ (FeO·Fe₂O₃), so the acid gives a mixture of iron(II) and iron(III) chlorides.",
  temp=50, dT=8, color="#A8A832", flags=("dissolution",), sound="mix")
R("Aluminium Oxide and Sodium Hydroxide", "Neutralization", "Al2O3(s) + 2 NaOH(aq) -> 2 NaAlO2(aq) + H2O(l)",
  "The white powder slowly dissolves in hot concentrated alkali, giving a colorless solution.",
  "Al₂O₃ is amphoteric: it dissolves in acids and also in strong bases, here forming sodium aluminate. "
  "The Bayer process uses this reaction to purify bauxite.", temp=90, heat=True, dT=5, flags=("dissolution",), sound="mix")
R("Glass Etching (Silica and HF)", "Neutralization", "SiO2(s) + 4 HF(aq) -> SiF4(g) + 2 H2O(l)",
  "The glass surface turns frosted and rough where the acid touches it.",
  "HF is the only common acid that attacks silica: the very strong Si–F bond drives the formation of SiF₄ gas. "
  "This is why HF is stored in plastic bottles, never glass.", dT=5, eff="mild", sound="fizz")

# ── 3. Metal + acid ──────────────────────────────────────────────────────
metal_acid("Iron and Sulfuric Acid", "Fe(s) + H2SO4(aq) -> FeSO4(aq) + H2(g)", "Iron", "iron(II) sulfate",
           color=FE_GREEN, note="The solution turns pale green.")
metal_acid("Aluminium and Sulfuric Acid", "2 Al(s) + 3 H2SO4(aq) -> Al2(SO4)3(aq) + 3 H2(g)", "Aluminium", "aluminium sulfate",
           note="The reaction starts slowly until the oxide layer is removed.", temp=30)
metal_acid("Calcium and Hydrochloric Acid", "Ca(s) + 2 HCl(aq) -> CaCl2(aq) + H2(g)", "Calcium", "calcium chloride", eff="violent")
metal_acid("Magnesium and Acetic Acid", "Mg(s) + 2 CH3COOH(aq) -> Mg(CH3COO)2(aq) + H2(g)", "Magnesium", "magnesium acetate",
           note="It is clearly slower than with HCl of the same concentration, because acetic acid is a weak acid.")
metal_acid("Zinc and Acetic Acid", "Zn(s) + 2 CH3COOH(aq) -> Zn(CH3COO)2(aq) + H2(g)", "Zinc", "zinc acetate",
           note="The bubbling is slow because acetic acid is a weak acid.")
metal_acid("Magnesium and Hydrobromic Acid", "Mg(s) + 2 HBr(aq) -> MgBr2(aq) + H2(g)", "Magnesium", "magnesium bromide", eff="vigorous")
metal_acid("Zinc and Hydrobromic Acid", "Zn(s) + 2 HBr(aq) -> ZnBr2(aq) + H2(g)", "Zinc", "zinc bromide")
metal_acid("Magnesium and Phosphoric Acid", "3 Mg(s) + 2 H3PO4(aq) -> Mg3(PO4)2(s) + 3 H2(g)", "Magnesium", "magnesium phosphate",
           note="A white coating of magnesium phosphate gradually forms.")
metal_acid("Tin and Sulfuric Acid", "Sn(s) + H2SO4(aq) -> SnSO4(aq) + H2(g)", "Tin", "tin(II) sulfate",
           note="The reaction is slow and needs warming.", temp=50)
metal_acid("Nickel and Sulfuric Acid", "Ni(s) + H2SO4(aq) -> NiSO4(aq) + H2(g)", "Nickel", "nickel(II) sulfate",
           color=NI_GREEN, note="The solution slowly turns green.", temp=45)
metal_acid("Manganese and Hydrochloric Acid", "Mn(s) + 2 HCl(aq) -> MnCl2(aq) + H2(g)", "Manganese", "manganese(II) chloride",
           eff="vigorous", note="The solution becomes very pale pink.")
metal_acid("Chromium and Hydrochloric Acid", "Cr(s) + 2 HCl(aq) -> CrCl2(aq) + H2(g)", "Chromium", "chromium(II) chloride",
           color="#4FA3D1", note="The solution turns blue, then green in air as Cr²⁺ is oxidized to Cr³⁺.", temp=40)
R("Zinc and Sodium Hydroxide", "Gas Evolution", "Zn(s) + 2 NaOH(aq) -> Na2ZnO2(aq) + H2(g)",
  "On heating, zinc dissolves in the concentrated alkali with bubbles of hydrogen.",
  "Zinc is an amphoteric metal: it reacts with acids and also with strong bases, forming sodium zincate and hydrogen gas.",
  temp=80, heat=True, dT=8, eff="mild", sound="fizz")
R("Copper and Hot Concentrated Sulfuric Acid", "Redox", "Cu(s) + 2 H2SO4(aq) -> CuSO4(aq) + SO2(g) + 2 H2O(l)",
  "On strong heating, copper dissolves, choking SO₂ gas is given off, and the liquid turns blue when diluted.",
  "Copper is below hydrogen, so it does not release H₂ from acids. Hot concentrated H₂SO₄ acts as an oxidizing agent instead: "
  "it oxidizes Cu to Cu²⁺ and is itself reduced to SO₂.", temp=150, heat=True, dT=10, eff="mild", color=CU_BLUE,
  flags=("vapor",), sound="sizzle")
R("Carbon and Hot Concentrated Sulfuric Acid", "Redox", "C(s) + 2 H2SO4(aq) -> CO2(g) + 2 SO2(g) + 2 H2O(l)",
  "On heating, the carbon is slowly consumed and gases bubble out of the acid.",
  "Hot concentrated sulfuric acid is an oxidizing agent: it oxidizes carbon to CO₂ and is reduced to SO₂.",
  temp=200, heat=True, eff="mild", flags=("vapor",), sound="sizzle")
R("Sulfur and Hot Concentrated Sulfuric Acid", "Redox", "S(s) + 2 H2SO4(aq) -> 3 SO2(g) + 2 H2O(l)",
  "On heating, the yellow sulfur disappears and choking SO₂ gas is evolved.",
  "A comproportionation: sulfur (0) is oxidized and sulfuric acid (S +6) is reduced, both ending as SO₂ (S +4).",
  temp=200, heat=True, eff="mild", flags=("vapor",), sound="sizzle")

# ── 4. Carbonates / hydrogencarbonates + acid ───────────────────────────
carb_acid("Calcium Carbonate and Nitric Acid", "CaCO3(s) + 2 HNO3(aq) -> Ca(NO3)2(aq) + H2O(l) + CO2(g)", "calcium nitrate")
carb_acid("Calcium Carbonate and Acetic Acid", "CaCO3(s) + 2 CH3COOH(aq) -> Ca(CH3COO)2(aq) + H2O(l) + CO2(g)", "calcium acetate", eff="mild")
carb_acid("Sodium Carbonate and Sulfuric Acid", "Na2CO3(s) + H2SO4(aq) -> Na2SO4(aq) + H2O(l) + CO2(g)", "sodium sulfate")
carb_acid("Sodium Carbonate and Acetic Acid", "Na2CO3(s) + 2 CH3COOH(aq) -> 2 CH3COONa(aq) + H2O(l) + CO2(g)", "sodium acetate", eff="mild")
carb_acid("Sodium Bicarbonate and Hydrochloric Acid", "NaHCO3(s) + HCl(aq) -> NaCl(aq) + H2O(l) + CO2(g)", "sodium chloride")
carb_acid("Sodium Bicarbonate and Nitric Acid", "NaHCO3(s) + HNO3(aq) -> NaNO3(aq) + H2O(l) + CO2(g)", "sodium nitrate")
carb_acid("Sodium Bicarbonate and Phosphoric Acid", "3 NaHCO3(s) + H3PO4(aq) -> Na3PO4(aq) + 3 H2O(l) + 3 CO2(g)", "sodium phosphate", eff="mild")
carb_acid("Copper(II) Carbonate and Hydrochloric Acid", "CuCO3(s) + 2 HCl(aq) -> CuCl2(aq) + H2O(l) + CO2(g)",
          "copper(II) chloride (blue-green)", color=CU_GREEN)
carb_acid("Copper(II) Carbonate and Sulfuric Acid", "CuCO3(s) + H2SO4(aq) -> CuSO4(aq) + H2O(l) + CO2(g)",
          "copper(II) sulfate (blue)", color=CU_BLUE)
R("Sodium Bicarbonate and Sodium Hydroxide", "Neutralization", "NaHCO3(aq) + NaOH(aq) -> Na2CO3(aq) + H2O(l)",
  "No visible change; the solution warms slightly.",
  "The hydrogencarbonate ion is amphiprotic. With a strong base it acts as an acid and loses its proton: "
  "HCO₃⁻ + OH⁻ → CO₃²⁻ + H₂O.", dT=4, sound="mix")

# ── 5. Precipitation ─────────────────────────────────────────────────────
precip("Barium Chloride and Sulfuric Acid", "BaCl2(aq) + H2SO4(aq) -> BaSO4(s) + 2 HCl(aq)", "barium sulfate", "dense white", WHITE,
       "It does not dissolve in acid — this is the standard test for sulfate ions.")
precip("Barium Chloride and Copper(II) Sulfate", "BaCl2(aq) + CuSO4(aq) -> BaSO4(s) + CuCl2(aq)", "barium sulfate", "white", WHITE,
       "The liquid above it stays blue-green.", color=CU_GREEN)
precip("Barium Chloride and Sodium Carbonate", "BaCl2(aq) + Na2CO3(aq) -> BaCO3(s) + 2 NaCl(aq)", "barium carbonate", "white", WHITE,
       "Unlike BaSO₄, it dissolves with fizzing if acid is added.")
precip("Barium Chloride and Potassium Sulfate", "BaCl2(aq) + K2SO4(aq) -> BaSO4(s) + 2 KCl(aq)", "barium sulfate", "white", WHITE)
precip("Barium Chloride and Iron(II) Sulfate", "BaCl2(aq) + FeSO4(aq) -> BaSO4(s) + FeCl2(aq)", "barium sulfate", "white", WHITE,
       "The liquid stays pale green.", color=FE_GREEN)
precip("Barium Hydroxide and Sodium Sulfate", "Ba(OH)2(aq) + Na2SO4(aq) -> BaSO4(s) + 2 NaOH(aq)", "barium sulfate", "white", WHITE)
precip("Barium Hydroxide and Copper(II) Sulfate", "Ba(OH)2(aq) + CuSO4(aq) -> BaSO4(s) + Cu(OH)2(s)", "barium sulfate mixed with copper(II) hydroxide",
       "pale blue-white", "#BFE3F2", "Both products are insoluble, so the liquid is left almost free of ions.")
precip("Silver Nitrate and Hydrochloric Acid", "AgNO3(aq) + HCl(aq) -> AgCl(s) + HNO3(aq)", "silver chloride", "curdy white", WHITE,
       "It darkens slowly in light.")
precip("Silver Nitrate and Hydrobromic Acid", "AgNO3(aq) + HBr(aq) -> AgBr(s) + HNO3(aq)", "silver bromide", "cream", "#FFFDD0")
precip("Silver Nitrate and Hydroiodic Acid", "AgNO3(aq) + HI(aq) -> AgI(s) + HNO3(aq)", "silver iodide", "pale yellow", "#FFFF99")
precip("Silver Nitrate and Calcium Chloride", "2 AgNO3(aq) + CaCl2(aq) -> 2 AgCl(s) + Ca(NO3)2(aq)", "silver chloride", "curdy white", WHITE)
precip("Silver Nitrate and Barium Chloride", "2 AgNO3(aq) + BaCl2(aq) -> 2 AgCl(s) + Ba(NO3)2(aq)", "silver chloride", "curdy white", WHITE)
precip("Silver Nitrate and Copper(II) Chloride", "2 AgNO3(aq) + CuCl2(aq) -> 2 AgCl(s) + Cu(NO3)2(aq)", "silver chloride", "white", WHITE,
       "The liquid stays blue.", color=CU_BLUE)
precip("Silver Nitrate and Iron(III) Chloride", "3 AgNO3(aq) + FeCl3(aq) -> 3 AgCl(s) + Fe(NO3)3(aq)", "silver chloride", "white", WHITE,
       "The liquid stays pale yellow.", color=FE_YELLOW)
precip("Silver Nitrate and Magnesium Chloride", "2 AgNO3(aq) + MgCl2(aq) -> 2 AgCl(s) + Mg(NO3)2(aq)", "silver chloride", "white", WHITE)
precip("Silver Nitrate and Zinc Chloride", "2 AgNO3(aq) + ZnCl2(aq) -> 2 AgCl(s) + Zn(NO3)2(aq)", "silver chloride", "white", WHITE)
precip("Silver Nitrate and Ammonium Chloride", "AgNO3(aq) + NH4Cl(aq) -> AgCl(s) + NH4NO3(aq)", "silver chloride", "white", WHITE)
precip("Silver Nitrate and Sodium Carbonate", "2 AgNO3(aq) + Na2CO3(aq) -> Ag2CO3(s) + 2 NaNO3(aq)", "silver carbonate", "pale yellow", "#FFFACD")
precip("Silver Nitrate and Sodium Phosphate", "3 AgNO3(aq) + Na3PO4(aq) -> Ag3PO4(s) + 3 NaNO3(aq)", "silver phosphate", "yellow", YELLOW,
       "The yellow color distinguishes phosphate from chloride.")
precip("Silver Nitrate and Potassium Thiocyanate", "AgNO3(aq) + KSCN(aq) -> AgSCN(s) + KNO3(aq)", "silver thiocyanate", "white", WHITE)
R("Silver Nitrate and Sodium Hydroxide", "Precipitation", "2 AgNO3(aq) + 2 NaOH(aq) -> Ag2O(s) + 2 NaNO3(aq) + H2O(l)",
  "A dark brown precipitate of silver oxide forms immediately.",
  "Silver hydroxide is unstable: it loses water at once to give brown silver(I) oxide, Ag₂O.", ppt="#4E342E", sound="plop")
precip("Lead(II) Nitrate and Potassium Chloride", "Pb(NO3)2(aq) + 2 KCl(aq) -> PbCl2(s) + 2 KNO3(aq)", "lead(II) chloride", "white", WHITE,
       "It dissolves when the mixture is heated and reappears on cooling.")
precip("Lead(II) Nitrate and Hydrochloric Acid", "Pb(NO3)2(aq) + 2 HCl(aq) -> PbCl2(s) + 2 HNO3(aq)", "lead(II) chloride", "white", WHITE,
       "It dissolves in hot water.")
precip("Lead(II) Nitrate and Sodium Sulfate", "Pb(NO3)2(aq) + Na2SO4(aq) -> PbSO4(s) + 2 NaNO3(aq)", "lead(II) sulfate", "white", WHITE)
precip("Lead(II) Nitrate and Sulfuric Acid", "Pb(NO3)2(aq) + H2SO4(aq) -> PbSO4(s) + 2 HNO3(aq)", "lead(II) sulfate", "white", WHITE)
precip("Lead(II) Nitrate and Copper(II) Sulfate", "Pb(NO3)2(aq) + CuSO4(aq) -> PbSO4(s) + Cu(NO3)2(aq)", "lead(II) sulfate", "white", WHITE,
       "The liquid stays blue.", color=CU_BLUE)
precip("Lead(II) Nitrate and Magnesium Sulfate", "Pb(NO3)2(aq) + MgSO4(aq) -> PbSO4(s) + Mg(NO3)2(aq)", "lead(II) sulfate", "white", WHITE)
precip("Lead(II) Nitrate and Potassium Sulfate", "Pb(NO3)2(aq) + K2SO4(aq) -> PbSO4(s) + 2 KNO3(aq)", "lead(II) sulfate", "white", WHITE)
precip("Lead(II) Nitrate and Sodium Carbonate", "Pb(NO3)2(aq) + Na2CO3(aq) -> PbCO3(s) + 2 NaNO3(aq)", "lead(II) carbonate", "white", WHITE)
precip("Lead(II) Nitrate and Potassium Bromide", "Pb(NO3)2(aq) + 2 KBr(aq) -> PbBr2(s) + 2 KNO3(aq)", "lead(II) bromide", "white", WHITE)
precip("Lead(II) Nitrate and Sodium Bromide", "Pb(NO3)2(aq) + 2 NaBr(aq) -> PbBr2(s) + 2 NaNO3(aq)", "lead(II) bromide", "white", WHITE)
precip("Lead(II) Nitrate and Sodium Hydroxide", "Pb(NO3)2(aq) + 2 NaOH(aq) -> Pb(OH)2(s) + 2 NaNO3(aq)", "lead(II) hydroxide", "white", WHITE,
       "It redissolves in excess NaOH because Pb(OH)₂ is amphoteric.")
precip("Copper(II) Sulfate and Potassium Hydroxide", "CuSO4(aq) + 2 KOH(aq) -> Cu(OH)2(s) + K2SO4(aq)", "copper(II) hydroxide", "pale blue gelatinous", BLUE)
precip("Copper(II) Chloride and Sodium Hydroxide", "CuCl2(aq) + 2 NaOH(aq) -> Cu(OH)2(s) + 2 NaCl(aq)", "copper(II) hydroxide", "pale blue gelatinous", BLUE,
       "On heating it turns black as it loses water to form CuO.")
precip("Copper(II) Nitrate and Sodium Hydroxide", "Cu(NO3)2(aq) + 2 NaOH(aq) -> Cu(OH)2(s) + 2 NaNO3(aq)", "copper(II) hydroxide", "pale blue gelatinous", BLUE)
precip("Copper(II) Sulfate and Calcium Hydroxide", "CuSO4(aq) + Ca(OH)2(aq) -> Cu(OH)2(s) + CaSO4(s)", "copper(II) hydroxide with calcium sulfate",
       "pale blue", BLUE, "This suspension is Bordeaux mixture, a traditional fungicide for vines.")
precip("Copper(II) Chloride and Sodium Carbonate", "CuCl2(aq) + Na2CO3(aq) -> CuCO3(s) + 2 NaCl(aq)", "copper(II) carbonate", "blue-green", "#33AA88")
precip("Iron(II) Sulfate and Sodium Hydroxide", "FeSO4(aq) + 2 NaOH(aq) -> Fe(OH)2(s) + Na2SO4(aq)", "iron(II) hydroxide", "dirty green", GREEN,
       "It turns brown at the surface as air oxidizes it to iron(III) hydroxide.")
precip("Iron(II) Chloride and Sodium Hydroxide", "FeCl2(aq) + 2 NaOH(aq) -> Fe(OH)2(s) + 2 NaCl(aq)", "iron(II) hydroxide", "dirty green", GREEN,
       "It slowly turns brown in air.")
precip("Iron(III) Chloride and Potassium Hydroxide", "FeCl3(aq) + 3 KOH(aq) -> Fe(OH)3(s) + 3 KCl(aq)", "iron(III) hydroxide", "rust-brown gelatinous", BROWN)
precip("Iron(III) Nitrate and Sodium Hydroxide", "Fe(NO3)3(aq) + 3 NaOH(aq) -> Fe(OH)3(s) + 3 NaNO3(aq)", "iron(III) hydroxide", "rust-brown gelatinous", BROWN)
precip("Magnesium Sulfate and Sodium Hydroxide", "MgSO4(aq) + 2 NaOH(aq) -> Mg(OH)2(s) + Na2SO4(aq)", "magnesium hydroxide", "white gelatinous", WHITE)
precip("Magnesium Chloride and Sodium Hydroxide", "MgCl2(aq) + 2 NaOH(aq) -> Mg(OH)2(s) + 2 NaCl(aq)", "magnesium hydroxide", "white gelatinous", WHITE)
precip("Calcium Chloride and Sodium Hydroxide", "CaCl2(aq) + 2 NaOH(aq) -> Ca(OH)2(s) + 2 NaCl(aq)", "calcium hydroxide", "faint white", WHITE,
       "It only appears with concentrated solutions, because Ca(OH)₂ is slightly soluble.")
precip("Zinc Chloride and Sodium Hydroxide", "ZnCl2(aq) + 2 NaOH(aq) -> Zn(OH)2(s) + 2 NaCl(aq)", "zinc hydroxide", "white gelatinous", WHITE,
       "It redissolves in excess NaOH because Zn(OH)₂ is amphoteric.")
precip("Zinc Sulfate and Sodium Hydroxide", "ZnSO4(aq) + 2 NaOH(aq) -> Zn(OH)2(s) + Na2SO4(aq)", "zinc hydroxide", "white gelatinous", WHITE,
       "It redissolves in excess NaOH because Zn(OH)₂ is amphoteric.")
precip("Aluminium Chloride and Sodium Hydroxide", "AlCl3(aq) + 3 NaOH(aq) -> Al(OH)3(s) + 3 NaCl(aq)", "aluminium hydroxide", "white gelatinous", WHITE,
       "It redissolves in excess NaOH because Al(OH)₃ is amphoteric.")
precip("Nickel(II) Chloride and Sodium Hydroxide", "NiCl2(aq) + 2 NaOH(aq) -> Ni(OH)2(s) + 2 NaCl(aq)", "nickel(II) hydroxide", "apple-green", "#8FBC5A")
precip("Manganese(II) Sulfate and Sodium Hydroxide", "MnSO4(aq) + 2 NaOH(aq) -> Mn(OH)2(s) + Na2SO4(aq)", "manganese(II) hydroxide", "off-white", "#F5F0E1",
       "It quickly darkens to brown in air as it is oxidized.")
precip("Magnesium Sulfate and Sodium Carbonate", "MgSO4(aq) + Na2CO3(aq) -> MgCO3(s) + Na2SO4(aq)", "magnesium carbonate", "white", WHITE)
precip("Magnesium Chloride and Sodium Carbonate", "MgCl2(aq) + Na2CO3(aq) -> MgCO3(s) + 2 NaCl(aq)", "magnesium carbonate", "white", WHITE)
precip("Zinc Chloride and Sodium Carbonate", "ZnCl2(aq) + Na2CO3(aq) -> ZnCO3(s) + 2 NaCl(aq)", "zinc carbonate", "white", WHITE)
precip("Nickel(II) Chloride and Sodium Carbonate", "NiCl2(aq) + Na2CO3(aq) -> NiCO3(s) + 2 NaCl(aq)", "nickel(II) carbonate", "light green", "#A8D5A2")
precip("Calcium Hydroxide and Sodium Carbonate", "Ca(OH)2(aq) + Na2CO3(aq) -> CaCO3(s) + 2 NaOH(aq)", "calcium carbonate", "white", WHITE,
       "The liquid left behind is sodium hydroxide solution — the old 'causticizing' way of making NaOH.")
precip("Calcium Chloride and Sodium Sulfate", "CaCl2(aq) + Na2SO4(aq) -> CaSO4(s) + 2 NaCl(aq)", "calcium sulfate", "fine white", WHITE,
       "It forms only from fairly concentrated solutions, because CaSO₄ is slightly soluble.")
precip("Calcium Chloride and Sulfuric Acid", "CaCl2(aq) + H2SO4(aq) -> CaSO4(s) + 2 HCl(aq)", "calcium sulfate", "fine white", WHITE)
precip("Calcium Chloride and Sodium Phosphate", "3 CaCl2(aq) + 2 Na3PO4(aq) -> Ca3(PO4)2(s) + 6 NaCl(aq)", "calcium phosphate", "white", WHITE,
       "Calcium phosphate is the main mineral of bones and teeth.")
precip("Barium Chloride and Sodium Phosphate", "3 BaCl2(aq) + 2 Na3PO4(aq) -> Ba3(PO4)2(s) + 6 NaCl(aq)", "barium phosphate", "white", WHITE)
precip("Copper(II) Sulfate and Sodium Phosphate", "3 CuSO4(aq) + 2 Na3PO4(aq) -> Cu3(PO4)2(s) + 3 Na2SO4(aq)", "copper(II) phosphate", "light blue-green", "#7FC8B5")
precip("Iron(III) Chloride and Sodium Phosphate", "FeCl3(aq) + Na3PO4(aq) -> FePO4(s) + 3 NaCl(aq)", "iron(III) phosphate", "pale yellowish-white", "#F3E9C6")
precip("Magnesium Chloride and Sodium Phosphate", "3 MgCl2(aq) + 2 Na3PO4(aq) -> Mg3(PO4)2(s) + 6 NaCl(aq)", "magnesium phosphate", "white", WHITE)
precip("Zinc Chloride and Sodium Phosphate", "3 ZnCl2(aq) + 2 Na3PO4(aq) -> Zn3(PO4)2(s) + 6 NaCl(aq)", "zinc phosphate", "white", WHITE)
precip("Lead(II) Nitrate and Sodium Phosphate", "3 Pb(NO3)2(aq) + 2 Na3PO4(aq) -> Pb3(PO4)2(s) + 6 NaNO3(aq)", "lead(II) phosphate", "white", WHITE)
precip("Calcium Hydroxide and Hydrofluoric Acid", "Ca(OH)2(aq) + 2 HF(aq) -> CaF2(s) + 2 H2O(l)", "calcium fluoride", "white", WHITE,
       "This is also why calcium gluconate gel is the first-aid treatment for HF burns.")
precip("Hydrogen Sulfide and Copper(II) Sulfate", "H2S(g) + CuSO4(aq) -> CuS(s) + H2SO4(aq)", "copper(II) sulfide", "black", BLACK,
       "The blue color of the solution fades.")
precip("Hydrogen Sulfide and Lead(II) Nitrate", "H2S(g) + Pb(NO3)2(aq) -> PbS(s) + 2 HNO3(aq)", "lead(II) sulfide", "black", BLACK,
       "Paper soaked in lead salt is used to detect H₂S this way.")
precip("Hydrogen Sulfide and Silver Nitrate", "H2S(g) + 2 AgNO3(aq) -> Ag2S(s) + 2 HNO3(aq)", "silver sulfide", "black", BLACK,
       "The same compound is the black tarnish on silverware.")
R("Copper(II) Sulfate and Potassium Iodide", "Redox", "2 CuSO4(aq) + 4 KI(aq) -> 2 CuI(s) + I2(aq) + 2 K2SO4(aq)",
  "The blue solution turns muddy brown: an off-white solid is suspended in brown iodine solution.",
  "Cu²⁺ oxidizes iodide to iodine and is reduced to copper(I), which precipitates as white CuI. "
  "The iodine released can be titrated with thiosulfate to measure copper.", color=BROWN, ppt="#E8DCC8", sound="plop")
R("Iron(III) Nitrate and Potassium Thiocyanate", "Complexation", "Fe(NO3)3(aq) + 3 KSCN(aq) -> Fe(SCN)3(aq) + 3 KNO3(aq)",
  "The pale yellow solution turns deep blood-red at once.",
  "Fe³⁺ ions bind thiocyanate ions to form intensely red iron(III) thiocyanate complexes. "
  "The color is visible even at very low concentrations, so it is a sensitive test for Fe³⁺.",
  color="#B11226", sound="mix")

# ── 6. Displacement (metals and halogens) ────────────────────────────────
SILVER = "Glittering grey-silver crystals of silver"
COPPER = "A red-brown coating of copper"
displace("Zinc and Silver Nitrate", "Zn(s) + 2 AgNO3(aq) -> Zn(NO3)2(aq) + 2 Ag(s)", "Zinc", "Silver", SILVER,
         "the solution stays colorless.", hexppt="#C0C0C0")
displace("Iron and Silver Nitrate", "Fe(s) + 2 AgNO3(aq) -> Fe(NO3)2(aq) + 2 Ag(s)", "Iron", "Silver", SILVER,
         "the solution turns pale green.", color=FE_GREEN, hexppt="#C0C0C0")
displace("Magnesium and Silver Nitrate", "Mg(s) + 2 AgNO3(aq) -> Mg(NO3)2(aq) + 2 Ag(s)", "Magnesium", "Silver", SILVER,
         "the solution stays colorless and warms up.", hexppt="#C0C0C0", dT=20)
displace("Aluminium and Copper(II) Chloride", "2 Al(s) + 3 CuCl2(aq) -> 2 AlCl3(aq) + 3 Cu(s)", "Aluminium", "Copper", COPPER,
         "the blue-green color fades; the mixture gets hot and may fizz.", hexppt="#B87333", dT=35)
displace("Zinc and Copper(II) Chloride", "Zn(s) + CuCl2(aq) -> ZnCl2(aq) + Cu(s)", "Zinc", "Copper", COPPER,
         "the blue-green color fades to colorless.", hexppt="#B87333", dT=15)
displace("Magnesium and Copper(II) Chloride", "Mg(s) + CuCl2(aq) -> MgCl2(aq) + Cu(s)", "Magnesium", "Copper", COPPER,
         "the blue-green color fades quickly and the mixture gets hot.", hexppt="#B87333", dT=30)
displace("Tin and Copper(II) Sulfate", "Sn(s) + CuSO4(aq) -> SnSO4(aq) + Cu(s)", "Tin", "Copper", COPPER,
         "the blue color slowly fades.", hexppt="#B87333", dT=5)
displace("Nickel and Copper(II) Sulfate", "Ni(s) + CuSO4(aq) -> NiSO4(aq) + Cu(s)", "Nickel", "Copper", COPPER,
         "the blue solution slowly turns green.", color=NI_GREEN, hexppt="#B87333", dT=5)
displace("Aluminium and Copper(II) Sulfate", "2 Al(s) + 3 CuSO4(aq) -> Al2(SO4)3(aq) + 3 Cu(s)", "Aluminium", "Copper", COPPER,
         "the blue color fades. The start is slow because of the oxide film; a pinch of salt speeds it up.", hexppt="#B87333", dT=20)
displace("Zinc and Iron(II) Sulfate", "Zn(s) + FeSO4(aq) -> ZnSO4(aq) + Fe(s)", "Zinc", "Iron", "A dark grey deposit of iron",
         "the pale green color fades to colorless.", hexppt="#555555", dT=5)
displace("Magnesium and Iron(II) Sulfate", "Mg(s) + FeSO4(aq) -> MgSO4(aq) + Fe(s)", "Magnesium", "Iron", "A dark grey deposit of iron",
         "the pale green color fades to colorless.", hexppt="#555555", dT=12)
displace("Magnesium and Zinc Sulfate", "Mg(s) + ZnSO4(aq) -> MgSO4(aq) + Zn(s)", "Magnesium", "Zinc", "A grey deposit of zinc",
         "the solution stays colorless.", hexppt="#9A9A9A", dT=10)
displace("Zinc and Nickel(II) Chloride", "Zn(s) + NiCl2(aq) -> ZnCl2(aq) + Ni(s)", "Zinc", "Nickel", "A dark grey deposit of nickel",
         "the green color slowly fades.", hexppt="#6E6E6E", dT=5)
displace("Iron and Lead(II) Nitrate", "Fe(s) + Pb(NO3)2(aq) -> Fe(NO3)2(aq) + Pb(s)", "Iron", "Lead", "Soft grey crystals of lead",
         "the solution turns pale green.", color=FE_GREEN, hexppt="#707070", dT=5)
displace("Magnesium and Lead(II) Nitrate", "Mg(s) + Pb(NO3)2(aq) -> Mg(NO3)2(aq) + Pb(s)", "Magnesium", "Lead", "Soft grey crystals of lead",
         "the solution stays colorless.", hexppt="#707070", dT=12)
R("Copper and Iron(III) Chloride", "Redox", "Cu(s) + 2 FeCl3(aq) -> CuCl2(aq) + 2 FeCl2(aq)",
  "The copper slowly dissolves and the yellow-brown solution turns green.",
  "Fe³⁺ is a strong enough oxidizing agent to oxidize copper metal to Cu²⁺, while it is reduced only to Fe²⁺ (no iron metal forms). "
  "This reaction is used to etch copper circuit boards.", dT=5, color="#3C8D5A", sound="mix")
R("Iron and Iron(III) Chloride", "Redox", "Fe(s) + 2 FeCl3(aq) -> 3 FeCl2(aq)",
  "The iron dissolves and the yellow-brown solution turns pale green.",
  "A comproportionation: iron metal (0) and Fe³⁺ (+3) meet at the intermediate state Fe²⁺ (+2).", dT=5, color=FE_GREEN, sound="mix")
R("Chlorine and Potassium Iodide", "Redox", "Cl2(g) + 2 KI(aq) -> 2 KCl(aq) + I2(aq)",
  "The colorless solution turns brown as iodine forms; with excess chlorine dark solid iodine separates.",
  "Chlorine is a stronger oxidizing agent than iodine, so it takes electrons from iodide ions. "
  "Reactivity of the halogens falls down the group: Cl₂ > Br₂ > I₂.", color=BROWN, sound="mix")
R("Chlorine and Sodium Bromide", "Redox", "Cl2(g) + 2 NaBr(aq) -> 2 NaCl(aq) + Br2(aq)",
  "The colorless solution turns yellow-orange as bromine forms.",
  "Chlorine is more reactive than bromine, so it oxidizes bromide ions to bromine and is reduced to chloride.",
  color="#E08A1E", sound="mix")
R("Chlorine and Water", "Redox", "Cl2(g) + H2O(l) -> HCl(aq) + HClO(aq)",
  "The greenish gas dissolves to give pale yellow-green chlorine water, which bleaches litmus paper.",
  "Chlorine disproportionates in water into hydrochloric acid (Cl −1) and hypochlorous acid (Cl +1). "
  "HClO is the bleaching and disinfecting agent in chlorinated water.", color="#E6F2A2", flags=("dissolution",), sound="mix")
R("Chlorine and Sodium Hydroxide (Making Bleach)", "Redox", "Cl2(g) + 2 NaOH(aq) -> NaCl(aq) + NaClO(aq) + H2O(l)",
  "The greenish gas is absorbed by the cold alkali, giving an almost colorless solution with a bleach smell.",
  "In cold dilute alkali chlorine disproportionates into chloride and hypochlorite. The product solution is household bleach.",
  dT=8, sound="mix")
R("Chlorine and Potassium Hydroxide", "Redox", "Cl2(g) + 2 KOH(aq) -> KCl(aq) + KClO(aq) + H2O(l)",
  "The greenish gas is absorbed by the cold alkali, giving an almost colorless solution.",
  "In cold dilute alkali chlorine disproportionates into chloride (Cl −1) and hypochlorite (Cl +1).", dT=8, sound="mix")

# ── 7. Direct combination with O2, Cl2, S, N2, halogens ─────────────────
def burn(name, eq, obs, expl, temp, dT=60, sound="burn", flags=("glow",), eff="none", rtype="Combustion"):
    R(name, rtype, eq, obs, expl, temp=temp, heat=True, dT=dT, sound=sound, flags=flags, eff=eff)


burn("Hydrogen Combustion", "2 H2(g) + O2(g) -> 2 H2O(g)",
     "A lighted splint gives a sharp squeaky 'pop'; droplets of water condense on the cool glass.",
     "Hydrogen burns in oxygen to form water only, releasing a lot of energy (ΔH = −286 kJ per mole of H₂O). "
     "The 'pop' is the standard test for hydrogen gas.", 570, dT=80, sound="explosion", flags=("glow", "vapor"))
burn("Aluminium Powder Burning", "4 Al(s) + 3 O2(g) -> 2 Al2O3(s)",
     "The powder burns with brilliant white sparks, leaving white aluminium oxide.",
     "Finely divided aluminium burns fiercely because of the very high lattice energy of Al₂O₃. "
     "Bulk aluminium is protected by a thin oxide film and does not burn. This is the reaction in sparklers.",
     660, dT=90, flags=("glow", "sparks"), sound="crackle")
burn("Zinc Burning", "2 Zn(s) + O2(g) -> 2 ZnO(s)",
     "Zinc burns with a blue-green flame, giving fluffy zinc oxide that is yellow while hot and white when cold.",
     "Zinc is oxidized to zinc oxide. The reversible yellow/white color change of ZnO on heating is caused by a small loss of oxygen from the crystal.",
     500, flags=("glow", "smoke"))
burn("Calcium Burning", "2 Ca(s) + O2(g) -> 2 CaO(s)",
     "Calcium burns with a bright brick-red (orange-red) flame, leaving white calcium oxide.",
     "Calcium is oxidized to the ionic oxide CaO (quicklime). The flame color comes from excited calcium atoms.", 400)
burn("Lithium Burning", "4 Li(s) + O2(g) -> 2 Li2O(s)",
     "Lithium burns with a crimson-red flame, leaving white lithium oxide.",
     "Lithium forms the normal oxide Li₂O. The small Li⁺ ion cannot stabilize larger peroxide ions, unlike sodium and potassium.", 180)
burn("Sodium Burning", "2 Na(s) + O2(g) -> Na2O2(s)",
     "Sodium melts and burns with an intense golden-yellow flame, leaving pale yellow sodium peroxide.",
     "In plenty of oxygen sodium forms mainly the peroxide Na₂O₂. The yellow light is the sodium D line at 589 nm.", 120)
burn("Potassium Burning", "K(s) + O2(g) -> KO2(s)",
     "Potassium melts and burns with a lilac flame, leaving orange-yellow potassium superoxide.",
     "The large K⁺ ion stabilizes the superoxide ion O₂⁻, so potassium forms KO₂ rather than a simple oxide.", 70)
burn("Benzene Combustion", "2 C6H6(l) + 15 O2(g) -> 12 CO2(g) + 6 H2O(g)",
     "Benzene burns with a very smoky, luminous yellow flame.",
     "Benzene has a high carbon-to-hydrogen ratio, so in air part of the carbon does not burn completely and appears as soot. "
     "A sooty flame is typical of aromatic compounds.", 500, flags=("glow", "smoke"))
burn("Ethene Combustion", "C2H4(g) + 3 O2(g) -> 2 CO2(g) + 2 H2O(g)",
     "Ethene burns with a bright, slightly smoky yellow flame.",
     "Complete combustion of an alkene gives carbon dioxide and water. The flame is more luminous than that of ethane because of the higher carbon content.",
     450)
burn("Acetone Combustion", "CH3COCH3(l) + 4 O2(g) -> 3 CO2(g) + 3 H2O(g)",
     "Acetone catches fire very easily and burns with a bright yellow flame.",
     "Acetone is volatile and its vapor forms flammable mixtures with air at room temperature. Complete combustion gives CO₂ and water.",
     465)
burn("Sucrose Combustion", "C12H22O11(s) + 12 O2(g) -> 12 CO2(g) + 11 H2O(g)",
     "The sugar melts, browns to caramel, then burns with a yellow flame, leaving some black char.",
     "Complete oxidation of sucrose gives CO₂ and water and releases the same energy the body obtains from it by respiration (about 5640 kJ/mol).",
     350, flags=("glow", "smoke"))
burn("Decane Combustion", "2 C10H22(l) + 31 O2(g) -> 20 CO2(g) + 22 H2O(g)",
     "Decane burns with a yellow, slightly sooty flame.",
     "Decane is a typical component of kerosene and diesel. Complete combustion of an alkane gives only CO₂ and water.", 210,
     flags=("glow", "smoke"))
burn("Hydrogen Sulfide Combustion", "2 H2S(g) + 3 O2(g) -> 2 SO2(g) + 2 H2O(g)",
     "The gas burns with a pale blue flame and a choking smell of sulfur dioxide.",
     "In excess oxygen hydrogen sulfide is oxidized completely to sulfur dioxide and water.", 260)
burn("Silicon Oxidation", "Si(s) + O2(g) -> SiO2(s)",
     "On strong heating the grey silicon becomes coated with a white glassy layer.",
     "Silicon is oxidized to silicon dioxide. The thin, tough SiO₂ layer grown this way is used as an insulator on silicon chips.",
     900, dT=30, rtype="Oxidation")
burn("Tin Oxidation", "Sn(s) + O2(g) -> SnO2(s)",
     "Molten tin becomes covered with a white-grey powdery skin.",
     "Tin is oxidized by air to tin(IV) oxide, a white solid used in ceramic glazes.", 300, dT=25, rtype="Oxidation")
burn("Nickel Oxidation", "2 Ni(s) + O2(g) -> 2 NiO(s)",
     "The hot metal darkens as a grey-green to black oxide layer forms.",
     "Nickel is slowly oxidized to nickel(II) oxide on strong heating.", 500, dT=20, rtype="Oxidation")
burn("Chromium Oxidation", "4 Cr(s) + 3 O2(g) -> 2 Cr2O3(s)",
     "Chromium powder glows and turns into a dull green powder.",
     "Chromium is oxidized to green chromium(III) oxide. A very thin invisible film of this oxide is what makes stainless steel stainless.",
     600, dT=30, rtype="Oxidation")
burn("Lead Oxidation", "2 Pb(s) + O2(g) -> 2 PbO(s)",
     "Molten lead becomes covered with a yellow powder.",
     "Lead is oxidized to yellow lead(II) oxide (litharge).", 400, dT=20, rtype="Oxidation")
burn("Manganese Oxidation", "3 Mn(s) + 2 O2(g) -> Mn3O4(s)",
     "Manganese powder burns with bright sparks, leaving a brown-black solid.",
     "On heating in air manganese forms the mixed oxide Mn₃O₄, which contains both Mn²⁺ and Mn³⁺.",
     450, flags=("glow", "sparks"), rtype="Oxidation")

burn("Hydrogen and Chlorine", "H2(g) + Cl2(g) -> 2 HCl(g)",
     "The mixture explodes in bright light or flame; the green color of chlorine disappears and steamy acid fumes form.",
     "Light splits Cl₂ into chlorine atoms, which start a fast radical chain reaction. The product hydrogen chloride dissolves in water to give hydrochloric acid.",
     250, dT=70, sound="explosion", rtype="Synthesis")
burn("Sodium and Chlorine", "2 Na(s) + Cl2(g) -> 2 NaCl(s)",
     "Hot sodium burns in chlorine with a bright yellow flame, giving clouds of white smoke that settle as table salt.",
     "Each sodium atom gives one electron to a chlorine atom, forming Na⁺ and Cl⁻ ions held together in an ionic lattice. "
     "Two dangerous elements combine into harmless salt.", 100, dT=80, flags=("glow", "smoke"), rtype="Synthesis")
burn("Potassium and Chlorine", "2 K(s) + Cl2(g) -> 2 KCl(s)",
     "Potassium burns in chlorine with a lilac flame, giving white smoke of potassium chloride.",
     "Potassium transfers its outer electron to chlorine, forming the ionic solid KCl. The reaction is even more vigorous than with sodium.",
     70, dT=85, flags=("glow", "smoke"), rtype="Synthesis")
burn("Iron and Chlorine", "2 Fe(s) + 3 Cl2(g) -> 2 FeCl3(s)",
     "Hot iron wool glows red and gives dense brown smoke of iron(III) chloride.",
     "Chlorine is a strong oxidizing agent and takes iron all the way to the +3 state (FeCl₃), unlike hydrochloric acid, which gives only FeCl₂.",
     250, flags=("glow", "smoke"), rtype="Synthesis")
burn("Copper and Chlorine", "Cu(s) + Cl2(g) -> CuCl2(s)",
     "Hot copper foil glows in chlorine and gives yellow-brown smoke; with a little water the solid turns blue-green.",
     "Copper is oxidized to copper(II) chloride. Anhydrous CuCl₂ is yellow-brown; its solution is blue-green.",
     300, flags=("glow", "smoke"), rtype="Synthesis")
burn("Magnesium and Chlorine", "Mg(s) + Cl2(g) -> MgCl2(s)",
     "Burning magnesium continues to burn in chlorine with a brilliant white flame, leaving white magnesium chloride.",
     "Magnesium gives two electrons to chlorine, forming the ionic solid MgCl₂.", 300, dT=80, rtype="Synthesis")
burn("Aluminium and Chlorine", "2 Al(s) + 3 Cl2(g) -> 2 AlCl3(s)",
     "Heated aluminium glows brightly in chlorine and white fumes of aluminium chloride sublime onto the cool glass.",
     "Aluminium is oxidized to AlCl₃, which is covalent enough to sublime at about 180°C.", 250, dT=70,
     flags=("glow", "smoke"), rtype="Synthesis")
burn("Zinc and Chlorine", "Zn(s) + Cl2(g) -> ZnCl2(s)",
     "Hot zinc burns in chlorine, leaving white zinc chloride.",
     "Zinc is oxidized to zinc chloride, a white solid that absorbs water from the air very readily.", 300, rtype="Synthesis")
burn("Calcium and Chlorine", "Ca(s) + Cl2(g) -> CaCl2(s)",
     "Hot calcium burns in chlorine with an orange-red flame, leaving white calcium chloride.",
     "Calcium gives two electrons to chlorine, forming ionic calcium chloride.", 300, dT=75, rtype="Synthesis")
burn("Phosphorus and Chlorine", "2 P(s) + 3 Cl2(g) -> 2 PCl3(l)",
     "Phosphorus catches fire in chlorine with a pale greenish flame and a fuming colorless liquid collects.",
     "Phosphorus combines directly with chlorine to give phosphorus trichloride; with excess chlorine solid PCl₅ forms.",
     60, dT=50, flags=("glow", "smoke"), rtype="Synthesis")
burn("Iron and Sulfur", "Fe(s) + S(s) -> FeS(s)",
     "Once started by heating, a red glow spreads through the mixture by itself, leaving a grey-black solid that is no longer magnetic.",
     "Iron and sulfur combine into iron(II) sulfide. The glow that continues without heating proves the reaction is exothermic, "
     "and the loss of magnetism shows a new compound has formed — the classic mixture-versus-compound experiment.",
     250, dT=50, rtype="Synthesis", sound="crackle")
burn("Zinc and Sulfur", "Zn(s) + S(s) -> ZnS(s)",
     "The mixture flares up violently with a blue-green flash and a cloud of white-yellow smoke.",
     "Zinc and sulfur powders combine very exothermically into zinc sulfide.", 300, dT=90, sound="whoosh",
     flags=("glow", "smoke", "sparks"), rtype="Synthesis")
burn("Copper and Sulfur", "2 Cu(s) + S(s) -> Cu2S(s)",
     "Hot copper glows in sulfur vapor and turns into a brittle blue-black solid.",
     "With sulfur, a mild oxidizing agent, copper is oxidized only to the +1 state, giving copper(I) sulfide.",
     250, dT=40, rtype="Synthesis")
burn("Magnesium and Sulfur", "Mg(s) + S(s) -> MgS(s)",
     "The mixture burns with a dazzling white flash, leaving a white-grey solid.",
     "Magnesium transfers two electrons to sulfur to form ionic magnesium sulfide.", 300, dT=90, sound="whoosh",
     flags=("glow", "sparks"), rtype="Synthesis")
burn("Aluminium and Sulfur", "2 Al(s) + 3 S(s) -> Al2S3(s)",
     "The mixture burns with an intense white glow, leaving a grey solid that smells of rotten eggs in moist air.",
     "Aluminium sulfide forms very exothermically. It is hydrolyzed by moisture to Al(OH)₃ and H₂S.",
     400, dT=90, flags=("glow", "sparks"), rtype="Synthesis")
burn("Magnesium and Nitrogen", "3 Mg(s) + N2(g) -> Mg3N2(s)",
     "Burning magnesium keeps burning in nitrogen, leaving a greenish-yellow powder.",
     "Magnesium is reactive enough to break the very strong N≡N bond and form magnesium nitride. "
     "This is why some nitride forms when magnesium burns in air.", 800, dT=60, rtype="Synthesis")
R("Lithium and Nitrogen", "Synthesis", "6 Li(s) + N2(g) -> 2 Li3N(s)",
  "The shiny metal slowly becomes covered with a dark red-black crust.",
  "Lithium is the only alkali metal that reacts with nitrogen at room temperature, forming lithium nitride. "
  "The small Li⁺ ion gives a very stable lattice with N³⁻.", dT=10)
burn("Aluminium and Bromine", "2 Al(s) + 3 Br2(l) -> 2 AlBr3(s)",
     "After a short delay the foil bursts into flame with bright sparks and clouds of orange-brown fumes.",
     "Once bromine gets through the oxide film, aluminium is oxidized violently to aluminium bromide.",
     25, dT=90, flags=("glow", "sparks", "smoke"), sound="crackle", rtype="Synthesis")
R("Aluminium and Iodine", "Synthesis", "2 Al(s) + 3 I2(s) -> 2 AlI3(s)",
  "A drop of water starts the reaction: the mixture flares up and clouds of purple iodine vapor billow out.",
  "Aluminium is oxidized by iodine to aluminium iodide. The water drop is the catalyst; the heat released "
  "sublimes excess iodine into a purple vapor.", dT=80, flags=("glow", "smoke", "vapor"), sound="whoosh",
  cond="Room temperature; started by one drop of water as catalyst.")
R("Zinc and Iodine", "Synthesis", "Zn(s) + I2(s) -> ZnI2(aq)",
  "In a little water the brown iodine color fades as the mixture warms, leaving a colorless solution.",
  "Zinc reduces iodine to iodide and is oxidized to Zn²⁺, giving soluble zinc iodide. "
  "Electrolysis of the solution reverses the reaction.", dT=25, sound="mix")
R("Magnesium and Bromine", "Synthesis", "Mg(s) + Br2(l) -> MgBr2(s)",
  "The orange-brown color of bromine fades as the magnesium reacts, and the mixture becomes hot.",
  "Magnesium gives two electrons to bromine, forming ionic magnesium bromide.", dT=40, sound="sizzle")

# ── 8. Reductions, steam and high-temperature reactions ──────────────────
burn("Magnesium in Carbon Dioxide", "2 Mg(s) + CO2(g) -> 2 MgO(s) + C(s)",
     "Burning magnesium keeps burning inside CO₂ with a crackling white flame, leaving white ash with black specks of carbon.",
     "Magnesium is such a strong reducing agent that it takes oxygen from CO₂, leaving carbon. "
     "This is why CO₂ extinguishers must not be used on metal fires.", 650, dT=80, flags=("glow", "sparks"),
     sound="crackle", rtype="Redox")
burn("Magnesium and Silica", "2 Mg(s) + SiO2(s) -> 2 MgO(s) + Si(s)",
     "On strong heating the mixture glows brightly, leaving a grey-white solid containing dark silicon.",
     "Magnesium removes oxygen from silicon dioxide, reducing it to silicon. It is a simple laboratory way to obtain silicon from sand.",
     650, dT=70, rtype="Reduction")
burn("Carbon and Carbon Dioxide", "C(s) + CO2(g) -> 2 CO(g)",
     "Red-hot carbon slowly disappears in the gas stream; the gas leaving burns with a blue flame.",
     "Hot carbon reduces CO₂ to carbon monoxide (the Boudouard reaction). In a blast furnace the CO formed is what actually reduces the iron ore.",
     700, dT=-10, rtype="Redox")
burn("Steam and Hot Carbon (Water Gas)", "C(s) + H2O(g) -> CO(g) + H2(g)",
     "Steam passed over white-hot coke gives a colorless gas that burns with a blue flame.",
     "Carbon reduces steam to hydrogen and is oxidized to carbon monoxide. The CO + H₂ mixture (water gas) is an industrial fuel and source of hydrogen. "
     "The reaction is endothermic.", 950, dT=-15, rtype="Redox")
burn("Iron and Steam", "3 Fe(s) + 4 H2O(g) -> Fe3O4(s) + 4 H2(g)",
     "Steam passed over red-hot iron leaves a black solid, and the gas collected burns with a pop.",
     "Iron does not react with cold water, but red-hot iron reduces steam to hydrogen and forms magnetite, Fe₃O₄. The reaction is reversible.",
     700, dT=10, eff="mild", rtype="Redox")
burn("Zinc and Steam", "Zn(s) + H2O(g) -> ZnO(s) + H2(g)",
     "Hot zinc glows in steam, forming zinc oxide (yellow while hot, white when cold) and hydrogen gas.",
     "Zinc is not reactive enough for cold water, but it reduces steam to hydrogen.", 450, dT=30, eff="mild", rtype="Redox")
burn("Zinc Oxide and Carbon", "ZnO(s) + C(s) -> Zn(g) + CO(g)",
     "On very strong heating the white powder is consumed and zinc distils off as vapor.",
     "Carbon reduces zinc oxide to the metal. Because the temperature is above zinc's boiling point (907°C), the zinc leaves as vapor and is condensed.",
     950, dT=-10, rtype="Reduction")
burn("Iron(III) Oxide and Hydrogen", "Fe2O3(s) + 3 H2(g) -> 2 Fe(s) + 3 H2O(g)",
     "The red-brown powder turns grey-black and becomes magnetic; water droplets condense in the cool part of the tube.",
     "Hydrogen removes oxygen from the oxide, reducing it to iron metal. 'Direct reduced iron' is made this way.",
     500, dT=5, flags=("vapor",), rtype="Reduction")
burn("Magnetite and Hydrogen", "Fe3O4(s) + 4 H2(g) -> 3 Fe(s) + 4 H2O(g)",
     "The black powder turns to grey iron and water vapor leaves the tube.",
     "The reverse of the iron-steam reaction: with excess hydrogen and steam removed, the equilibrium shifts toward iron.",
     600, dT=5, flags=("vapor",), rtype="Reduction")
burn("Chromium Thermite", "2 Al(s) + Cr2O3(s) -> Al2O3(s) + 2 Cr(s)",
     "Once ignited the mixture burns with a blinding white glow and showers of sparks, leaving a bead of chromium metal.",
     "Aluminium is more reactive than chromium and takes its oxygen (aluminothermic reduction). Chromium is produced industrially this way.",
     900, dT=95, flags=("glow", "sparks", "smoke"), sound="crackle", rtype="Redox")
burn("Copper Thermite", "2 Al(s) + 3 CuO(s) -> Al2O3(s) + 3 Cu(s)",
     "The mixture reacts almost explosively with a bright flash, leaving molten copper.",
     "Aluminium reduces copper(II) oxide to copper. Because copper holds oxygen weakly, this thermite is much more violent than the iron one.",
     650, dT=98, flags=("glow", "sparks", "smoke"), sound="explosion", rtype="Redox")
burn("Magnesium and Copper(II) Oxide", "Mg(s) + CuO(s) -> MgO(s) + Cu(s)",
     "A brilliant white flash; afterwards white magnesium oxide and red-brown copper remain.",
     "Magnesium is far more reactive than copper and removes the oxygen from copper(II) oxide very exothermically.",
     600, dT=95, flags=("glow", "sparks"), sound="whoosh", rtype="Redox")
burn("Ammonia and Copper(II) Oxide", "2 NH3(g) + 3 CuO(s) -> 3 Cu(s) + N2(g) + 3 H2O(g)",
     "The hot black oxide turns to pink-brown copper and water droplets collect further along the tube.",
     "Ammonia acts as a reducing agent: it takes oxygen from CuO and is oxidized to nitrogen gas.", 400, dT=10,
     flags=("vapor",), rtype="Reduction")
burn("Ethanol and Hot Copper(II) Oxide", "C2H5OH(l) + CuO(s) -> CH3CHO(g) + Cu(s) + H2O(l)",
     "A hot black copper coil turns shiny pink-red in ethanol vapor and a sharp, fruity smell of ethanal appears.",
     "Hot CuO oxidizes the primary alcohol ethanol to the aldehyde ethanal and is reduced to copper metal.",
     300, dT=10, rtype="Redox")
burn("Methanol and Hot Copper(II) Oxide", "CH3OH(l) + CuO(s) -> HCHO(g) + Cu(s) + H2O(l)",
     "A hot black copper coil turns shiny pink-red in methanol vapor and a pungent smell of formaldehyde appears.",
     "Hot CuO oxidizes methanol to methanal (formaldehyde) and is reduced to copper metal.", 300, dT=10, rtype="Redox")
burn("Quicklime and Carbon Dioxide", "CaO(s) + CO2(g) -> CaCO3(s)",
     "The white solid slowly gains mass as it absorbs the gas; no other visible change.",
     "The basic oxide CaO combines with the acidic oxide CO₂ to form calcium carbonate — the reverse of making quicklime in a kiln.",
     400, dT=20, rtype="Synthesis")
R("Sodium Oxide and Carbon Dioxide", "Synthesis", "Na2O(s) + CO2(g) -> Na2CO3(s)",
  "The white solid absorbs the gas and warms up.",
  "A basic oxide and an acidic oxide combine directly into a salt, sodium carbonate.", dT=20)
burn("Quicklime and Sulfur Dioxide", "CaO(s) + SO2(g) -> CaSO3(s)",
     "The hot white solid absorbs the choking gas.",
     "Basic CaO neutralizes acidic SO₂ to give calcium sulfite. Power stations use this reaction to remove SO₂ from flue gas.",
     400, dT=20, rtype="Synthesis")
burn("Quicklime and Silica (Slag Formation)", "CaO(s) + SiO2(s) -> CaSiO3(l)",
     "At bright red heat the two white solids fuse into a glassy molten slag.",
     "Basic calcium oxide reacts with acidic silica to form calcium silicate. In the blast furnace this removes sandy impurities as slag.",
     950, dT=15, rtype="Synthesis")

# ── 9. Redox in solution ─────────────────────────────────────────────────
R("Potassium Permanganate and Hydrochloric Acid", "Redox",
  "2 KMnO4(s) + 16 HCl(aq) -> 2 KCl(aq) + 2 MnCl2(aq) + 5 Cl2(g) + 8 H2O(l)",
  "The purple color disappears and yellow-green chlorine gas is given off.",
  "Permanganate (Mn +7) oxidizes chloride ions to chlorine and is reduced to almost colorless Mn²⁺. "
  "This is a convenient laboratory preparation of chlorine.", dT=10, eff="vigorous", color="#E6F2A2", sound="fizz")
R("Manganese Dioxide and Hydrochloric Acid", "Redox", "MnO2(s) + 4 HCl(aq) -> MnCl2(aq) + Cl2(g) + 2 H2O(l)",
  "On warming, the black powder dissolves and yellow-green chlorine gas is evolved.",
  "MnO₂ (Mn +4) oxidizes chloride to chlorine and is reduced to Mn²⁺. Scheele discovered chlorine with this reaction in 1774.",
  temp=60, heat=True, dT=5, eff="mild", sound="fizz")
R("Potassium Dichromate and Hydrochloric Acid", "Redox",
  "K2Cr2O7(s) + 14 HCl(aq) -> 2 KCl(aq) + 2 CrCl3(aq) + 3 Cl2(g) + 7 H2O(l)",
  "On warming with concentrated acid the orange color turns green and chlorine gas is evolved.",
  "Dichromate (Cr +6) oxidizes chloride ions to chlorine and is reduced to green Cr³⁺.", temp=60, heat=True,
  dT=5, eff="mild", color="#2E7D32", sound="fizz")
R("Permanganate and Iron(II) Titration", "Redox",
  "2 KMnO4(aq) + 10 FeSO4(aq) + 8 H2SO4(aq) -> K2SO4(aq) + 2 MnSO4(aq) + 5 Fe2(SO4)3(aq) + 8 H2O(l)",
  "Each drop of purple permanganate is decolorized at once; the first permanent pale pink marks the end point.",
  "In acid, MnO₄⁻ oxidizes Fe²⁺ to Fe³⁺ and is reduced to colorless Mn²⁺ (1 MnO₄⁻ : 5 Fe²⁺). "
  "Permanganate is its own indicator.", color="#F6E7A1", sound="mix")
R("Permanganate and Iodide", "Redox",
  "2 KMnO4(aq) + 10 KI(aq) + 8 H2SO4(aq) -> 6 K2SO4(aq) + 2 MnSO4(aq) + 5 I2(aq) + 8 H2O(l)",
  "The purple color vanishes and the solution turns deep brown as iodine is set free.",
  "Acidified permanganate oxidizes iodide ions to iodine and is reduced to Mn²⁺.", color=BROWN, sound="mix")
R("Sulfur Dioxide and Permanganate", "Redox",
  "5 SO2(g) + 2 KMnO4(aq) + 2 H2O(l) -> K2SO4(aq) + 2 MnSO4(aq) + 2 H2SO4(aq)",
  "The purple solution is decolorized as the gas bubbles through.",
  "SO₂ is a reducing agent: it is oxidized to sulfate while purple MnO₄⁻ is reduced to colorless Mn²⁺. "
  "This is a test for sulfur dioxide.", color="", sound="bubble")
R("Elephant Toothpaste (Peroxide and Iodide)", "Catalytic Decomposition", "2 H2O2(aq) + KI(aq) -> 2 H2O(l) + O2(g) + KI(aq)",
  "The liquid froths up violently and gets hot; with detergent a huge column of warm foam shoots out.",
  "Iodide ions catalyze the decomposition of hydrogen peroxide into water and oxygen. The catalyst is not used up. "
  "The reaction is strongly exothermic (−98 kJ per mole of H₂O₂).", dT=40, eff="violent", sound="foam_eruption",
  flags=("foam", "vapor"))
R("Hydrogen Peroxide and Bleach", "Redox", "H2O2(aq) + NaClO(aq) -> NaCl(aq) + H2O(l) + O2(g)",
  "Vigorous fizzing as oxygen gas is released; the mixture warms up.",
  "Hypochlorite oxidizes hydrogen peroxide to oxygen and is reduced to chloride. Here H₂O₂ acts as a reducing agent.",
  dT=20, eff="vigorous", sound="fizz")
R("Iron(II) Sulfate and Hydrogen Peroxide", "Redox",
  "2 FeSO4(aq) + H2O2(aq) + H2SO4(aq) -> Fe2(SO4)3(aq) + 2 H2O(l)",
  "The pale green solution turns yellow-brown.",
  "In acid, hydrogen peroxide oxidizes Fe²⁺ (pale green) to Fe³⁺ (yellow-brown) and is reduced to water.",
  dT=8, color=FE_YELLOW, sound="mix")
R("Sulfur Dioxide and Hydrogen Peroxide", "Redox", "SO2(g) + H2O2(aq) -> H2SO4(aq)",
  "The gas is absorbed with no visible change; the solution becomes strongly acidic and warm.",
  "Hydrogen peroxide oxidizes sulfur from +4 in SO₂ to +6 in sulfuric acid. The same reaction in cloud droplets produces acid rain.",
  dT=15, sound="mix")
R("Sulfur Dioxide and Hydrogen Sulfide", "Redox", "SO2(g) + 2 H2S(g) -> 3 S(s) + 2 H2O(l)",
  "Where the two gases meet, a yellow deposit of sulfur and droplets of water form on the glass.",
  "A comproportionation: sulfur at +4 (SO₂) and −2 (H₂S) meet at 0. This is the key step of the Claus process "
  "for recovering sulfur, and it also happens in volcanic vents.", ppt="#F2E14C", dT=10, flags=("smoke",))
R("Hydrogen Sulfide and Chlorine", "Redox", "H2S(g) + Cl2(g) -> S(s) + 2 HCl(g)",
  "A pale yellow deposit of sulfur appears and the green color of chlorine fades.",
  "Chlorine oxidizes sulfide (−2) to sulfur (0) and is reduced to hydrogen chloride.", ppt="#F2E14C", dT=15)
R("Hydrogen Sulfide and Iodine", "Redox", "H2S(g) + I2(aq) -> S(s) + 2 HI(aq)",
  "The brown iodine solution is decolorized and a milky yellow suspension of sulfur appears.",
  "Iodine oxidizes hydrogen sulfide to sulfur and is reduced to colorless iodide.", ppt="#F2E14C", sound="bubble")
R("Iodine and Thiosulfate", "Redox", "2 Na2S2O3(aq) + I2(aq) -> Na2S4O6(aq) + 2 NaI(aq)",
  "The brown iodine color fades to pale yellow and then to colorless.",
  "Thiosulfate reduces iodine to iodide and is oxidized to tetrathionate. This fast, exact reaction is the basis of iodometric titrations; "
  "starch is added near the end to sharpen the end point.", color="", sound="mix")
R("Thiosulfate and Sulfuric Acid", "Precipitation", "Na2S2O3(aq) + H2SO4(aq) -> Na2SO4(aq) + S(s) + SO2(g) + H2O(l)",
  "The clear solution slowly turns cloudy pale yellow until a cross under the flask can no longer be seen.",
  "Acid decomposes thiosulfate into solid sulfur and sulfur dioxide. Timing how long the cross takes to disappear is the classic experiment on reaction rate.",
  ppt="#FFFACD", sound="mix")
R("Bleach and Hydrochloric Acid (Never Mix!)", "Redox", "NaClO(aq) + 2 HCl(aq) -> NaCl(aq) + Cl2(g) + H2O(l)",
  "The mixture fizzes and gives off yellow-green, choking chlorine gas.",
  "Acid converts hypochlorite and chloride into chlorine gas. This is why bleach must never be mixed with acidic cleaners.",
  dT=10, eff="vigorous", color="#E6F2A2", sound="fizz",
  warn=["Never mix bleach with acids at home: toxic chlorine gas is released."])
R("Bleach and Ammonia (Never Mix!)", "Redox", "NaClO(aq) + NH3(aq) -> NH2Cl(g) + NaOH(aq)",
  "Irritating, toxic fumes of chloramine rise from the mixture.",
  "Hypochlorite chlorinates ammonia to form chloramine, a toxic gas. This is why bleach must never be mixed with ammonia-based cleaners.",
  dT=8, eff="mild", flags=("vapor",), sound="fizz", ghs=["GHS06", "GHS05"],
  warn=["Never mix bleach with ammonia: toxic chloramine gas is released."])
R("Ammonia and Chlorine", "Redox", "8 NH3(g) + 3 Cl2(g) -> 6 NH4Cl(s) + N2(g)",
  "The green color of chlorine disappears and dense white smoke of ammonium chloride forms.",
  "Chlorine oxidizes ammonia to nitrogen; the HCl formed reacts immediately with excess ammonia to give white NH₄Cl smoke. "
  "Ammonia vapor is used to find leaks in chlorine pipes this way.", dT=25, flags=("smoke",), ppt=WHITE, sound="whoosh")

# ── 10. Acids made / gases released from salts ───────────────────────────
R("Sodium Chloride and Concentrated Sulfuric Acid", "Gas Evolution", "NaCl(s) + H2SO4(aq) -> NaHSO4(aq) + HCl(g)",
  "The salt froths and steamy, sharp-smelling fumes of hydrogen chloride are given off.",
  "Non-volatile sulfuric acid displaces the volatile acid HCl from its salt. The fumes form white smoke with ammonia.",
  dT=5, eff="vigorous", flags=("vapor",), sound="fizz")
R("Potassium Nitrate and Concentrated Sulfuric Acid", "Gas Evolution", "KNO3(s) + H2SO4(aq) -> KHSO4(aq) + HNO3(g)",
  "On heating, pale yellow fumes of nitric acid distil over.",
  "Non-volatile sulfuric acid displaces the more volatile nitric acid from the nitrate. This was the historical way of making nitric acid.",
  temp=150, heat=True, eff="mild", flags=("vapor",), sound="sizzle")
R("Fluorite and Concentrated Sulfuric Acid", "Gas Evolution", "CaF2(s) + H2SO4(aq) -> CaSO4(s) + 2 HF(g)",
  "On warming, colorless fumes are given off that etch glass.",
  "Sulfuric acid displaces hydrogen fluoride from calcium fluoride. This is the industrial source of HF.",
  temp=100, heat=True, eff="mild", flags=("vapor",), sound="sizzle")
R("Sodium Acetate and Hydrochloric Acid", "Neutralization", "CH3COONa(aq) + HCl(aq) -> CH3COOH(aq) + NaCl(aq)",
  "No visible change, but a smell of vinegar appears.",
  "A strong acid displaces a weak acid from its salt: acetate ions accept H⁺ to form acetic acid molecules.", sound="mix")
R("Methane from Sodium Acetate (Soda Lime)", "Decomposition", "CH3COONa(s) + NaOH(s) -> CH4(g) + Na2CO3(s)",
  "On strong heating a colorless gas is given off that burns with a blue flame.",
  "Heating sodium acetate with solid alkali removes the carboxyl group as carbonate (decarboxylation), leaving methane. "
  "It is the classic laboratory preparation of methane.", temp=350, heat=True, eff="mild", sound="sizzle")
R("Ammonium Chloride and Calcium Hydroxide", "Gas Evolution", "2 NH4Cl(s) + Ca(OH)2(s) -> CaCl2(s) + 2 NH3(g) + 2 H2O(g)",
  "On heating, a pungent gas is given off that turns damp red litmus paper blue.",
  "A strong base displaces the weak base ammonia from its salt. This is the standard laboratory preparation of ammonia.",
  temp=80, heat=True, eff="mild", flags=("vapor",), sound="sizzle")
R("Ammonium Chloride and Potassium Hydroxide", "Gas Evolution", "NH4Cl(aq) + KOH(aq) -> KCl(aq) + NH3(g) + H2O(l)",
  "On warming, the sharp smell of ammonia is noticed and damp red litmus turns blue.",
  "OH⁻ removes a proton from NH₄⁺, releasing ammonia gas. Warming with alkali is the test for ammonium salts.",
  temp=40, eff="mild", sound="fizz")
R("Ammonium Nitrate and Sodium Hydroxide", "Gas Evolution", "NH4NO3(aq) + NaOH(aq) -> NaNO3(aq) + NH3(g) + H2O(l)",
  "On warming, the sharp smell of ammonia is noticed and damp red litmus turns blue.",
  "OH⁻ removes a proton from NH₄⁺, releasing ammonia gas.", temp=40, eff="mild", sound="fizz")
R("Barium Hydroxide and Ammonium Nitrate (Endothermic)", "Thermal",
  "Ba(OH)2(s) + 2 NH4NO3(s) -> Ba(NO3)2(aq) + 2 NH3(g) + 2 H2O(l)",
  "The two dry solids turn into a slush, smell of ammonia, and become so cold that the flask freezes to a wet board.",
  "A spontaneous endothermic reaction: it absorbs heat from the surroundings but still proceeds because entropy increases greatly "
  "(solids become a liquid and a gas). The temperature can fall below −20°C.", dT=-40, eff="mild", flags=("frost",),
  sound="frost_crackle")

# ── 11. Organic reactions ────────────────────────────────────────────────
R("Esterification: Methyl Acetate", "Esterification", "CH3COOH(l) + CH3OH(l) -> CH3COOCH3(l) + H2O(l)",
  "After warming with a few drops of concentrated sulfuric acid, a sweet, glue-like fruity smell appears.",
  "A carboxylic acid and an alcohol condense to an ester and water. The reaction is reversible and needs an acid catalyst.",
  temp=60, heat=True, cat=True, sound="mix")
R("Acetylene and Bromine", "Addition", "C2H2(g) + 2 Br2(aq) -> C2H2Br4(l)",
  "Orange bromine water is decolorized as the gas bubbles through.",
  "The triple bond adds two molecules of bromine, giving 1,1,2,2-tetrabromoethane. Decolorizing bromine is the test for unsaturation.",
  color="", sound="bubble")
R("Hydrogenation of Ethene", "Addition", "C2H4(g) + H2(g) -> C2H6(g)",
  "No visible change; the gas volume falls as the two gases combine.",
  "Hydrogen adds across the C=C double bond on the surface of a nickel or platinum catalyst, turning the alkene into an alkane. "
  "Vegetable oils are hardened into margarine by the same reaction.", temp=150, heat=True, cat=True)
R("Hydrogenation of Acetylene", "Addition", "C2H2(g) + 2 H2(g) -> C2H6(g)",
  "No visible change; the gas volume falls as the gases combine.",
  "With a nickel or platinum catalyst the triple bond adds two molecules of hydrogen, passing through ethene to ethane.",
  temp=150, heat=True, cat=True)
R("Hydrogenation of Benzene", "Addition", "C6H6(l) + 3 H2(g) -> C6H12(l)",
  "No visible change; hydrogen is absorbed.",
  "Benzene's stable ring resists addition, so hydrogenation to cyclohexane needs a nickel catalyst, heat and pressure.",
  temp=200, heat=True, cat=True)
R("Ethene and Chlorine", "Addition", "C2H4(g) + Cl2(g) -> C2H4Cl2(l)",
  "The green color of chlorine fades and oily droplets form on the glass.",
  "Chlorine adds across the double bond to give 1,2-dichloroethane — the 'oil of the Dutch chemists' that gave alkenes the old name olefins.",
  dT=10)
R("Ethene and Hydrogen Chloride", "Addition", "C2H4(g) + HCl(g) -> C2H5Cl(g)",
  "No visible change; the two gases combine into one.",
  "HCl adds across the double bond (electrophilic addition), giving chloroethane.", temp=150, heat=True, cat=True)
R("Ethene and Hydrogen Bromide", "Addition", "C2H4(g) + HBr(g) -> C2H5Br(l)",
  "The gases combine and colorless oily droplets of bromoethane form.",
  "HBr adds across the double bond: H⁺ attacks the electron-rich C=C first, then Br⁻ joins the carbocation.", dT=8)
R("Acetylene and Hydrogen Chloride", "Addition", "C2H2(g) + HCl(g) -> C2H3Cl(g)",
  "No visible change; the two gases combine into one.",
  "One molecule of HCl adds to the triple bond, giving chloroethene (vinyl chloride), the monomer of PVC.",
  temp=150, heat=True, cat=True)
R("Bromination of Benzene", "Substitution", "C6H6(l) + Br2(l) -> C6H5Br(l) + HBr(g)",
  "With iron filings as catalyst the red-brown color slowly fades and steamy fumes of HBr are given off.",
  "Benzene does not add bromine like an alkene. With a Lewis acid catalyst (FeBr₃) one hydrogen is substituted instead, "
  "which keeps the stable aromatic ring intact.", temp=30, cat=True, eff="mild", flags=("vapor",))
R("Ethanol and Hydrobromic Acid", "Substitution", "C2H5OH(l) + HBr(aq) -> C2H5Br(l) + H2O(l)",
  "On heating, a dense colorless oily layer of bromoethane separates.",
  "The OH group of the alcohol is protonated and replaced by bromide (nucleophilic substitution).", temp=60, heat=True)
R("Sodium and Ethanol", "Redox", "2 Na(s) + 2 C2H5OH(l) -> 2 C2H5ONa(aq) + H2(g)",
  "The sodium sinks and gives a steady stream of hydrogen bubbles, much more gently than with water.",
  "Ethanol has an O–H group like water but is a weaker acid, so sodium reacts more slowly, forming sodium ethoxide and hydrogen.",
  dT=15, eff="mild", sound="fizz")
R("Sodium and Methanol", "Redox", "2 Na(s) + 2 CH3OH(l) -> 2 CH3ONa(aq) + H2(g)",
  "The sodium gives a brisk stream of hydrogen bubbles and dissolves.",
  "Sodium replaces the hydrogen of the O–H group, forming sodium methoxide and hydrogen. Methanol reacts faster than ethanol but slower than water.",
  dT=20, eff="vigorous", sound="fizz")

# ── 12. Dissolving in water ──────────────────────────────────────────────
dissolve("Potassium Hydroxide Dissolution (Exothermic)", "KOH(s) + H2O(l) -> KOH(aq) + H2O(l)",
         "The white pellets dissolve quickly and the beaker becomes hot. The solution is strongly basic and slippery.",
         "Dissolving KOH releases a lot of heat (ΔH ≈ −57.6 kJ/mol) because hydration of K⁺ and OH⁻ gives out more energy than breaking the lattice needs.", 28)
dissolve("Lithium Hydroxide Dissolution", "LiOH(s) + H2O(l) -> LiOH(aq) + H2O(l)",
         "The white solid dissolves and the solution warms; it is strongly basic.",
         "Dissolving LiOH is exothermic (ΔH ≈ −23.6 kJ/mol).", 12)
dissolve("Diluting Sulfuric Acid (Acid into Water!)", "H2SO4(l) + H2O(l) -> H2SO4(aq) + H2O(l)",
         "The mixture becomes very hot; if water is added to the acid it can boil and spit violently.",
         "Hydration of H₂SO₄ is extremely exothermic (about −95 kJ/mol). Always add the acid slowly to the water so the large volume of water absorbs the heat.",
         45, flags=("dissolution", "vapor"))
dissolve("Diluting Hydrochloric Acid", "HCl(aq) + H2O(l) -> HCl(aq) + H2O(l)",
         "The acid mixes completely with the water and warms slightly.",
         "Diluting an acid lowers the H⁺ concentration, so the pH rises toward 7 but never passes it.", 4)
dissolve("Diluting Nitric Acid", "HNO3(aq) + H2O(l) -> HNO3(aq) + H2O(l)",
         "The acid mixes completely with the water and warms up.",
         "Hydration of the ions releases heat. Diluting lowers the H⁺ concentration and raises the pH toward 7.", 8)
dissolve("Acetic Acid in Water", "CH3COOH(l) + H2O(l) -> CH3COOH(aq) + H2O(l)",
         "The liquids mix completely; the solution smells of vinegar.",
         "Acetic acid is a weak acid: only about 1% of its molecules ionize in water. Vinegar is roughly a 5% solution.", 1)
dissolve("Calcium Chloride Dissolution (Exothermic)", "CaCl2(s) + H2O(l) -> CaCl2(aq) + H2O(l)",
         "The white granules dissolve and the solution gets hot.",
         "Dissolving anhydrous CaCl₂ is strongly exothermic (ΔH ≈ −82 kJ/mol). It is used in self-heating cans and for melting ice on roads.", 30)
dissolve("Ammonium Chloride Dissolution (Endothermic)", "NH4Cl(s) + H2O(l) -> NH4Cl(aq) + H2O(l)",
         "The white solid dissolves and the beaker feels cold.",
         "Dissolving NH₄Cl absorbs heat (ΔH ≈ +14.8 kJ/mol): the lattice energy is larger than the hydration energy.", -12)
dissolve("Potassium Nitrate Dissolution (Endothermic)", "KNO3(s) + H2O(l) -> KNO3(aq) + H2O(l)",
         "The white crystals dissolve and the beaker becomes noticeably cold.",
         "Dissolving KNO₃ is endothermic (ΔH ≈ +34.9 kJ/mol). Its solubility rises steeply with temperature, which makes it ideal for recrystallization experiments.", -15)
dissolve("Table Salt in Water", "NaCl(s) + H2O(l) -> NaCl(aq) + H2O(l)",
         "The white crystals dissolve to a clear colorless solution with almost no temperature change.",
         "Water molecules pull Na⁺ and Cl⁻ ions out of the crystal and surround them. Lattice and hydration energies nearly cancel (ΔH ≈ +3.9 kJ/mol).", -1)
dissolve("Potassium Chloride in Water", "KCl(s) + H2O(l) -> KCl(aq) + H2O(l)",
         "The white crystals dissolve and the solution cools slightly.",
         "Dissolving KCl is endothermic (ΔH ≈ +17.2 kJ/mol).", -6)
dissolve("Sugar in Water", "C12H22O11(s) + H2O(l) -> C12H22O11(aq) + H2O(l)",
         "The crystals dissolve to a clear, colorless, sweet solution that does not conduct electricity.",
         "Sucrose dissolves as whole molecules held by hydrogen bonds to water. No ions form, so it is a non-electrolyte.", 0)
dissolve("Copper(II) Sulfate in Water", "CuSO4(s) + H2O(l) -> CuSO4(aq) + H2O(l)",
         "White anhydrous powder turns blue and gets hot, then dissolves to a clear blue solution.",
         "Water molecules bond to Cu²⁺ to form the blue hydrated ion [Cu(H₂O)₆]²⁺. The white-to-blue change is a chemical test for water.",
         18, color=CU_BLUE)
dissolve("Copper(II) Chloride in Water", "CuCl2(s) + H2O(l) -> CuCl2(aq) + H2O(l)",
         "The solid dissolves to a green solution that turns blue on further dilution.",
         "Concentrated solutions contain green chloro-complexes of copper; dilution replaces chloride by water, giving the blue hydrated ion.",
         8, color=CU_GREEN)
dissolve("Iron(III) Chloride in Water", "FeCl3(s) + H2O(l) -> FeCl3(aq) + H2O(l)",
         "The dark solid dissolves with heat to a yellow-brown solution that is acidic.",
         "The highly charged Fe³⁺ ion polarizes water molecules and releases H⁺ (hydrolysis), so the solution is acidic.", 20, color=FE_YELLOW)
dissolve("Potassium Permanganate in Water", "KMnO4(s) + H2O(l) -> KMnO4(aq) + H2O(l)",
         "Dark crystals send out intense purple streaks that spread until the whole liquid is purple.",
         "The color comes from the MnO₄⁻ ion. Watching it spread without stirring is the classic demonstration of diffusion.", -2, color="#800080")
dissolve("Potassium Dichromate in Water", "K2Cr2O7(s) + H2O(l) -> K2Cr2O7(aq) + H2O(l)",
         "Orange crystals dissolve to a bright orange solution.",
         "The orange color belongs to the dichromate ion Cr₂O₇²⁺ in which chromium is in the +6 state.".replace("²⁺ in which", "²⁻ in which"),
         -4, color="#FF7F00")
dissolve("Sodium Carbonate in Water", "Na2CO3(s) + H2O(l) -> Na2CO3(aq) + H2O(l)",
         "The white powder dissolves and the solution warms; it turns litmus blue.",
         "Carbonate ions accept protons from water (CO₃²⁻ + H₂O ⇌ HCO₃⁻ + OH⁻), so the solution is basic, about pH 11.", 8)
dissolve("Baking Soda in Water", "NaHCO3(s) + H2O(l) -> NaHCO3(aq) + H2O(l)",
         "The white powder dissolves slowly and the solution cools slightly; it is mildly basic.",
         "Hydrogencarbonate is a weak base, giving a pH of about 8.3. Dissolving is slightly endothermic.", -4)
dissolve("Magnesium Sulfate in Water", "MgSO4(s) + H2O(l) -> MgSO4(aq) + H2O(l)",
         "The white anhydrous powder dissolves and the solution gets hot.",
         "Hydration of the small, doubly charged Mg²⁺ ion releases a lot of energy (ΔH ≈ −91 kJ/mol for the anhydrous salt).", 25)
dissolve("Potassium Iodide in Water", "KI(s) + H2O(l) -> KI(aq) + H2O(l)",
         "The white crystals dissolve very easily and the solution cools.",
         "Dissolving KI is endothermic (ΔH ≈ +20.3 kJ/mol).", -8)
dissolve("Silver Nitrate in Water", "AgNO3(s) + H2O(l) -> AgNO3(aq) + H2O(l)",
         "The colorless crystals dissolve very easily and the solution cools.",
         "Silver nitrate is one of the few very soluble silver salts, which is why it is the usual source of Ag⁺ ions. Dissolving is endothermic (ΔH ≈ +22.6 kJ/mol).", -8)
dissolve("Ammonia in Water", "NH3(g) + H2O(l) -> NH4OH(aq)",
         "The gas dissolves extremely readily (the fountain experiment); the solution turns phenolphthalein pink.",
         "Ammonia forms hydrogen bonds with water and a small fraction reacts: NH₃ + H₂O ⇌ NH₄⁺ + OH⁻, making the solution a weak base.", 8)
dissolve("Ethanol and Water", "C2H5OH(l) + H2O(l) -> C2H5OH(aq) + H2O(l)",
         "The liquids mix in any proportion, the mixture warms slightly and its volume is a little less than the sum of the two.",
         "Ethanol and water form hydrogen bonds with each other and pack more tightly than either liquid alone, so 50 mL + 50 mL gives only about 96 mL.", 4)
dissolve("Hydrogen Peroxide Dilution", "H2O2(aq) + H2O(l) -> H2O2(aq) + H2O(l)",
         "The liquids mix completely with no visible change.",
         "Hydrogen peroxide is miscible with water in all proportions. Pharmacy peroxide is a 3% solution.", 0)
