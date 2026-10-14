#!/usr/bin/env python3
"""Material properties 003 — densities of pure liquids and bottle concentrations.

Run from the repository root:  python3 tools/reactions/material_properties_003.py
Idempotent. Writes materials.json in its existing format (indent 2, UTF-8).

Two different kinds of value, kept in separate fields:

* densityGPerMl (+ densitySource): an intrinsic property of a PURE liquid.
  Source: CRC Handbook of Chemistry and Physics, 97th ed. (2016), "Physical Constants of
  Organic Compounds" / "Physical Constants of Inorganic Compounds"; temperature as noted.
  HClO and NaClO are not given one: they do not exist as pure liquids, only in solution.

* stockConcentrationM (+ stockLabel): a CONFIGURATION of the bottle on the lab shelf,
  not a property of the substance. Bench stocks of a school lab: 1.0 M acids and bases,
  0.1 M salt solutions, 0.02 M permanganate. Two are derived from their usual label:
  H2O2 "3 % w/v" = 30 g/L / 34.0147 g/mol = 0.882 M;
  Ca(OH)2 "saturated limewater" = solubility 1.73 g/L at 20 °C (CRC) / 74.093 g/mol = 0.0233 M.
  No bottle for H2CO3 (decomposes), FeSCN2+ (an ion) or HCHO (formalin is w/w, needs density).
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import reactions_tool as tool  # noqa: E402

DENSITY = {   # g/mL
    "H2O":      (0.997, "CRC Handbook 97th ed., 25 °C"),
    "C2H5OH":   (0.789, "CRC Handbook 97th ed., 20 °C"),
    "CH3OH":    (0.791, "CRC Handbook 97th ed., 20 °C"),
    "CH3COCH3": (0.785, "CRC Handbook 97th ed., 25 °C"),
    "C6H6":     (0.877, "CRC Handbook 97th ed., 20 °C"),
    "Br2":      (3.10,  "CRC Handbook 97th ed., 25 °C"),
}

ACIDS_BASES_1M = ["HCl", "H2SO4", "HNO3", "H3PO4", "HBr", "HI", "HF", "CH3COOH",
                  "NaOH", "KOH", "LiOH"]
STOCK = {f: (1.0, "1.0 M bench stock") for f in ACIDS_BASES_1M}
STOCK.update({
    "Ba(OH)2": (0.1, "0.1 M bench stock"),
    "Ca(OH)2": (0.0233, "saturated limewater (1.73 g/L, 20 °C)"),
    "H2O2":    (0.882, "3 % w/v hydrogen peroxide"),
    "KMnO4":   (0.02, "0.02 M bench stock"),
})
NO_BOTTLE = {"H2CO3", "FeSCN2+", "HCHO"}
SALT_STOCK = (0.1, "0.1 M bench stock")


def main():
    path = tool.MATERIALS
    with open(path, encoding="utf-8") as f:
        raw = f.read()
    materials = json.loads(raw)
    for m in materials:
        formula = m.get("formula", "")
        state = (m.get("state") or "").lower()
        if formula in DENSITY and state == "liquid":
            m["densityGPerMl"], m["densitySource"] = DENSITY[formula]
        if state in ("aqueous", "aq") and formula not in NO_BOTTLE:
            m["stockConcentrationM"], m["stockLabel"] = STOCK.get(formula, SALT_STOCK)
    out = json.dumps(materials, indent=2, ensure_ascii=False)
    if out == raw:
        print("nothing to change")
        return
    with open(path, "w", encoding="utf-8") as f:
        f.write(out)
    dens = sum(1 for m in materials if "densityGPerMl" in m)
    stock = sum(1 for m in materials if "stockConcentrationM" in m)
    print(f"materials.json: {dens} densities, {stock} bottle concentrations")


if __name__ == "__main__":
    main()
