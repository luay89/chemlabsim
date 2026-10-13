#!/usr/bin/env python3
"""Curation pass 002 — chemistry corrections and colours that follow the products.

Run from the repository root:   python3 tools/reactions/curate_002.py
It rewrites reactions.json (same formatting), runs the checker and re-packs reactions.bytes.
Running it twice is harmless (every rule is idempotent).

1. Equation fixes (balanced, charge-neutral formulas).
2. Unreachable duplicates removed (same reactant set as an earlier entry).
3. Colours derived from the chemistry instead of hand-picked per reaction:
   - precipitate_color = colour of the solid product (AgCl white, PbI2 yellow, Cu(OH)2 blue ...)
   - a precipitate is a solid in the liquid, so the liquid itself does not take the
     precipitate's colour: color_change is set to the colour of the coloured ion left in
     solution (Cu2+ blue, Fe2+ pale green ...) or removed when the solution is colourless
   - a metal that plates out of a salt solution (Cu, Ag, Pb ...) is shown as a precipitate
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import reactions_tool as tool  # noqa: E402

# Solid products in water: colour of the precipitate.
SOLID_COLOURS = {
    "AgCl": "#F4F4F2", "AgBr": "#F1E7B6", "AgI": "#F3E27A", "AgSCN": "#F4F4F2",
    "Ag2O": "#4E342E", "Ag2CO3": "#F3EDC8", "Ag3PO4": "#F2D24B", "Ag2S": "#1A1A1A",
    "PbI2": "#F6C915", "PbCl2": "#F4F4F2", "PbBr2": "#F4F4F2", "PbSO4": "#F4F4F2",
    "PbCO3": "#F4F4F2", "Pb(OH)2": "#F4F4F2", "Pb3(PO4)2": "#F4F4F2", "PbS": "#1A1A1A",
    "BaSO4": "#F4F4F2", "BaCO3": "#F4F4F2", "Ba3(PO4)2": "#F4F4F2",
    "CaCO3": "#F4F4F2", "CaSO4": "#F4F4F2", "Ca3(PO4)2": "#F4F4F2", "CaF2": "#F4F4F2",
    "Ca(OH)2": "#F4F4F2", "MgCO3": "#F4F4F2", "Mg(OH)2": "#F4F4F2", "Mg3(PO4)2": "#F4F4F2",
    "Zn(OH)2": "#F4F4F2", "ZnCO3": "#F4F4F2", "Zn3(PO4)2": "#F4F4F2", "Al(OH)3": "#F4F4F2",
    "Cu(OH)2": "#4FA3D9", "CuCO3": "#3F9C8A", "Cu3(PO4)2": "#7FC8B5", "CuS": "#1A1A1A",
    "CuI": "#E8DCC8", "Fe(OH)3": "#8B4A1C", "Fe(OH)2": "#6E8B4E", "FePO4": "#F3E9C6",
    "Ni(OH)2": "#8FBC5A", "NiCO3": "#A8D5A2", "Mn(OH)2": "#F5F0E1", "MnO2": "#2B2B2B",
    "S": "#F2E14C", "I2": "#3B2F4A",
    # metals deposited from solution
    "Cu": "#B87333", "Ag": "#A8A8A8", "Pb": "#707070", "Fe": "#555555",
    "Zn": "#9A9A9A", "Ni": "#6E6E6E", "Sn": "#8C8C8C",
}

# Coloured ions left in solution (by the aqueous product that carries them).
SOLUTION_COLOURS = {
    "CuSO4": "#3D8FD6", "CuCl2": "#2E9E8F", "Cu(NO3)2": "#3D8FD6",
    "FeSO4": "#B8D8A8", "FeCl2": "#B8D8A8", "Fe(NO3)2": "#B8D8A8",
    "FeCl3": "#C9A227", "Fe(NO3)3": "#C9A227", "Fe2(SO4)3": "#C9A227",
    "NiCl2": "#4CAF50", "NiSO4": "#4CAF50", "Ni(NO3)2": "#4CAF50",
    "Cr2(SO4)3": "#2E7D32", "CrCl3": "#2E7D32", "CoCl2": "#E07A9B",
    "KMnO4": "#7B1FA2", "K2Cr2O7": "#F57C00", "K2CrO4": "#F2C200",
    "Fe(SCN)Cl2": "#8B0000", "Fe(SCN)SO4": "#8B0000",
}

METALS = {"Cu", "Ag", "Pb", "Fe", "Zn", "Ni", "Sn", "Hg"}


def term(formula, state, stoich):
    return {"formula": formula, "state": state, "stoich": stoich}


def fix_equations(by_id):
    def setr(rid, reactants=None, products=None, **extra):
        r = by_id.get(rid)
        if r is None:
            return
        if reactants is not None:
            r["reactants"] = reactants
        if products is not None:
            r["products"] = products
        r.update(extra)

    # Fe3+ + SCN- -> blood-red [Fe(SCN)]2+ ; written as its neutral salt.
    setr("rxn_087", products=[term("Fe(SCN)Cl2", "aq", 1), term("KCl", "aq", 1)])
    setr("rxn_011", products=[term("Fe(SCN)SO4", "aq", 2), term("K2SO4", "aq", 1), term("H2O", "l", 2)])
    # 2 FeCl3 + 3 Na2CO3 + 3 H2O -> 2 Fe(OH)3 + 6 NaCl + 3 CO2 (water is the solvent)
    setr("rxn_056", products=[term("Fe(OH)3", "s", 2), term("NaCl", "aq", 6), term("CO2", "g", 3)],
         solvent_water=3)
    # 2 NaOH + 2 Al + 2 H2O -> 2 NaAlO2 + 3 H2
    setr("rxn_098", solvent_water=2)
    # Electrolysis of copper(II) sulfate (inert electrodes): 2 CuSO4 + 2 H2O -> 2 Cu + O2 + 2 H2SO4
    setr("rxn_123", reactants=[term("CuSO4", "aq", 2)],
         products=[term("Cu", "s", 2), term("O2", "g", 1), term("H2SO4", "aq", 2)], solvent_water=2)
    # Blue bottle: glucose is oxidised by dissolved O2 in base.
    setr("rxn_128",
         reactants=[term("C6H12O6", "aq", 2), term("NaOH", "aq", 2), term("O2", "g", 1)],
         products=[term("C6H11NaO7", "aq", 2), term("H2O", "l", 2)])
    # Same reagents as rxn_069; kept only for StressTestTrigger, which looks it up by id.
    if "rxn_106" in by_id:
        by_id["rxn_106"]["debug_only"] = True
    # A polymer has no fixed formula; it cannot be atom-balanced.
    setr("rxn_122", skip_balance="polymer (C2H4)n")


# Same reactant set as an earlier entry; the earlier one always wins, these were never reachable.
DUPLICATES = ["rxn_109", "rxn_111", "rxn_125", "rxn_127", "rxn_131", "rxn_132", "rxn_134",
              # Ethanol + dichromate without acid, with a wrong Cr2O3 product; the correct
              # acidified version (+ H2SO4) is rxn_042, so the 2-reagent mix now asks for the acid.
              "rxn_121"]


def in_solution(r):
    return any(c.get("state") == "aq" for c in r["reactants"])


def recolour(r):
    v = r.setdefault("visual_effects", {})
    solids = [p["formula"] for p in r["products"] if p.get("state") == "s"]
    coloured_solid = next((s for s in solids if s in SOLID_COLOURS), None)
    if not in_solution(r) or coloured_solid is None:
        return

    # A metal plating out of a salt solution is a visible solid in the beaker.
    if coloured_solid in METALS and not v.get("precipitate"):
        v["precipitate"] = True
    if not v.get("precipitate"):
        return

    v["precipitate_color"] = SOLID_COLOURS[coloured_solid]

    solution = next((SOLUTION_COLOURS[p["formula"]] for p in r["products"]
                     if p.get("state") == "aq" and p["formula"] in SOLUTION_COLOURS), "")
    if solution:
        v["color_change"] = solution
    elif v.get("color_change") and r["id"] not in KEEP_LIQUID_COLOUR:
        v["color_change"] = ""   # colourless solution; the colour is in the precipitate


def electroplate(r):
    """Electrolysis of a solution: the metal plates onto the cathode (it is not a precipitate),
    and nothing glows. The solution keeps a paler colour of the ion being used up."""
    v = r.get("visual_effects") or {}
    liquid_at_room_temp = any(c.get("state") == "l" for c in r["reactants"]) and r.get("activationTempC", 0) < 300
    if not v.get("electrical") or not (in_solution(r) or liquid_at_room_temp):
        return   # e.g. molten NaCl at 801 °C really does glow
    v["glow"] = False
    metal = next((p["formula"] for p in r["products"] if p.get("state") == "s" and p["formula"] in METALS), None)
    if metal:
        v["precipitate"] = False
        v["precipitate_color"] = SOLID_COLOURS[metal]   # read as the plating colour by the bench
    ion = next((SOLUTION_COLOURS[c["formula"]] for c in r["reactants"] if c["formula"] in SOLUTION_COLOURS), "")
    v["color_change"] = ion


# Reactions whose liquid colour comes from something the products list does not carry
# (colloidal sulfur clouds the whole liquid, iodine-starch, ...).
KEEP_LIQUID_COLOUR = {"rxn_044", "rxn_381", "rxn_284", "rxn_122", "rxn_135"}


def write(reactions):
    blocks = ["  " + json.dumps(r, indent=2, ensure_ascii=True).replace("\n", "\n  ") for r in reactions]
    out = '{\n"reactions": [\n' + ",\n".join(blocks) + "\n]\n}\n"
    json.loads(out)
    with open(tool.SRC, "w", encoding="utf-8") as f:
        f.write(out)


def main():
    _, reactions = tool.load()
    before = json.dumps(reactions, sort_keys=True)
    by_id = {r["id"]: r for r in reactions}

    fix_equations(by_id)
    reactions = [r for r in reactions if r["id"] not in DUPLICATES]
    for r in reactions:
        recolour(r)
        electroplate(r)

    errors, warnings = tool.check(reactions)
    for w in warnings:
        print("warning:", w)
    if errors:
        print("\n".join(errors))
        raise SystemExit("curation rejected")
    if json.dumps(reactions, sort_keys=True) == before:
        print("nothing to change")
        return
    write(reactions)
    print("%d reactions written, %d warnings" % (len(reactions), len(warnings)))
    tool.pack()


if __name__ == "__main__":
    main()
