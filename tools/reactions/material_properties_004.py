#!/usr/bin/env python3
"""Material properties 004 — thermal data for the heating lab (physical change only).

Run from the repository root:  python3 tools/reactions/material_properties_004.py
Idempotent. Writes materials.json in its existing format (indent 2, UTF-8).

A "thermal" block is written only for substances whose data was checked against a source;
every other material keeps no block, so the heating lab says the data is missing instead
of assuming anything. All values at 101.325 kPa.

Water (H2O) — NIST Chemistry WebBook, SRD 69, fluid properties (IAPWS-95), retrieved 2026-10-15:
  boiling point 99.9743 °C at 1.01325 bar; enthalpy of vaporization = h(vapour) − h(liquid)
  at that point = 2675.53 − 419.058 = 2256.5 J/g; liquid cp 4.18–4.22 J/(g·K) from 1 to 99 °C,
  taken as constant 4.19 (within 1 %).
  Melting point 0.00 °C, enthalpy of fusion 6.01 kJ/mol = 333.6 J/g and density of ice
  0.9167 g/cm³ at 0 °C: CRC Handbook of Chemistry and Physics (97th ed.).
  No cp for ice: a frozen sample can only start at 0 °C (the lab's lowest temperature).
Ethanol (C2H5OH) — NIST Chemistry WebBook, retrieved 2026-10-15:
  Tboil 351.5 ± 0.2 K = 78.35 °C (average of 138 values); ΔvapH 38.56 kJ/mol at 351.5 K
  (Majer and Svoboda, 1985) / 46.069 g/mol = 837.0 J/g; liquid cp 112.4 J/(mol·K) at
  298.15 K (Petrov et al., 1989) = 2.440 J/(g·K); Tfus 159 ± 2 K = −114 °C. No solid data:
  it cannot be frozen in this lab.
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import reactions_tool as tool  # noqa: E402

THERMAL = {
    "H2O": {
        "available": True,
        "referencePressureKPa": 101.325,
        "meltingPointC": 0.0,
        "boilingPointC": 99.97,
        "heatCapacityLiquidJPerGK": 4.19,
        "heatCapacitySolidJPerGK": 0.0,
        "enthalpyFusionJPerG": 333.6,
        "enthalpyVaporizationJPerG": 2256.5,
        "solidDensityGPerMl": 0.9167,
        "source": "NIST Chemistry WebBook SRD 69 (IAPWS-95), retrieved 2026-10-15: boiling point, "
                  "enthalpy of vaporization, liquid heat capacity; CRC Handbook 97th ed.: melting point, "
                  "enthalpy of fusion, density of ice",
        "notes": "Liquid heat capacity taken as constant (4.18–4.22 J/(g·K) over 1–99 °C). "
                 "No heat capacity for ice: a frozen sample starts at 0 °C.",
    },
    "C2H5OH": {
        "available": True,
        "referencePressureKPa": 101.325,
        "meltingPointC": -114.0,
        "boilingPointC": 78.35,
        "heatCapacityLiquidJPerGK": 2.440,
        "heatCapacitySolidJPerGK": 0.0,
        "enthalpyFusionJPerG": 0.0,
        "enthalpyVaporizationJPerG": 837.0,
        "solidDensityGPerMl": 0.0,
        "source": "NIST Chemistry WebBook, retrieved 2026-10-15: Tboil 351.5 K, ΔvapH 38.56 kJ/mol "
                  "(Majer and Svoboda 1985), cp(liquid) 112.4 J/(mol·K) at 298 K (Petrov et al. 1989), Tfus 159 K",
        "notes": "Liquid heat capacity at 25 °C taken as constant. Flammable vapour: in a real lab ethanol "
                 "is warmed in a water bath, never over a flame.",
    },
}


def main():
    path = tool.MATERIALS
    with open(path, encoding="utf-8") as f:
        raw = f.read()
    materials = json.loads(raw)
    for m in materials:
        formula = m.get("formula", "")
        if formula in THERMAL and (m.get("state") or "").lower() == "liquid":
            m["thermal"] = THERMAL[formula]
    out = json.dumps(materials, indent=2, ensure_ascii=False)
    if out == raw:
        print("nothing to change")
        return
    with open(path, "w", encoding="utf-8") as f:
        f.write(out)
    print("materials.json: thermal data for " + ", ".join(sorted(m["formula"] for m in materials if "thermal" in m)))


if __name__ == "__main__":
    main()
