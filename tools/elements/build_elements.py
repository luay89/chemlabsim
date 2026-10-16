#!/usr/bin/env python3
"""Build Assets/_ProjectV3/Resources/Data/elements.json — the single element dataset.

Run from the repository root:  python3 tools/elements/build_elements.py
Deterministic and idempotent. Inputs (both in the repository):

* tools/elements/pubchem_periodictable_2026-10-15.json
  PubChem Periodic Table (NIH, public domain), downloaded 2026-10-15 from
  https://pubchem.ncbi.nlm.nih.gov/rest/pug/periodictable/JSON .
  PubChem compiles these values from NIST, CRC and other references; it does not say
  for each value whether it was measured, so values are stored with status "listed".
* Assets/_ProjectV3/Scripts/Engine/Chemistry/AtomicWeights.cs
  IUPAC CIAAW 2021 abridged standard atomic weights — the project's single table of
  atomic weights (used by MolarMass). Copied here, never re-typed, so the two cannot drift.

Nothing is invented. Every property carries a status:
  standard            CIAAW standard atomic weight
  listed              value as listed by PubChem
  predicted           the source marks it predicted / expected (no bulk sample)
  estimated           a number is listed, but no macroscopic sample was ever measured
                      (astatine, francium, Z >= 100: see the note on the property)
  reference_dependent the value is not at the stated reference condition (note says why)
  unknown             the source lists nothing
  not_sourced         descriptive text not yet taken from a citable source
Elements without a standard atomic weight get the source's "atomic mass" as the mass of a
reference isotope (u) — a different quantity, labelled as such.
"""
import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SNAPSHOT = os.path.join(ROOT, "tools", "elements", "pubchem_periodictable_2026-10-15.json")
WEIGHTS = os.path.join(ROOT, "Assets", "_ProjectV3", "Scripts", "Engine", "Chemistry", "AtomicWeights.cs")
OUT = os.path.join(ROOT, "Assets", "_ProjectV3", "Resources", "Data", "elements.json")

SOURCES = [
    {"id": "pubchem_pt", "citation": "PubChem Periodic Table, National Library of Medicine (NIH); public domain",
     "url": "https://pubchem.ncbi.nlm.nih.gov/periodic-table/", "retrieved": "2026-10-15"},
    {"id": "ciaaw_2021", "citation": "IUPAC CIAAW, Standard atomic weights of the elements 2021 (abridged), "
                                     "Pure Appl. Chem. 2022, 94(5), 573-600",
     "url": "https://www.ciaaw.org/atomic-weights.htm", "retrieved": "2026-10-15"},
]

CATEGORY = {
    "Alkali metal": "alkali_metal", "Alkaline earth metal": "alkaline_earth_metal",
    "Transition metal": "transition_metal", "Post-transition metal": "post_transition_metal",
    "Metalloid": "metalloid", "Nonmetal": "other_nonmetal", "Halogen": "halogen",
    "Noble gas": "noble_gas", "Lanthanide": "lanthanide", "Actinide": "actinide",
}

# Elements of which no weighable (macroscopic) sample has been made or measured: numbers the
# source lists for their bulk properties are estimates.
NO_BULK_SAMPLE = {85, 87} | set(range(100, 119))

# Values the source lists that are not at the reference condition shown with them.
REFERENCE_NOTES = {
    (2, "meltingPoint"): "Helium does not solidify at 101.325 kPa at any temperature; this value needs about 2.5 MPa.",
    (6, "meltingPoint"): "Carbon (graphite) sublimes at 101.325 kPa instead of melting; the source does not state the pressure for this value.",
    (6, "boilingPoint"): "Carbon sublimes at 101.325 kPa; the source does not state the pressure for this value.",
    (33, "meltingPoint"): "Arsenic sublimes at 101.325 kPa; melting needs elevated pressure.",
    (33, "boilingPoint"): "This is arsenic's sublimation point at 101.325 kPa, not a boiling point.",
}
ALLOTROPE_NOTES = {
    6: "Values depend on the allotrope (graphite, diamond, …).",
    15: "Values depend on the allotrope (white, red, black phosphorus).",
    16: "Values depend on the allotrope (rhombic or monoclinic sulfur).",
    50: "Tin has two allotropes (white β-tin and grey α-tin).",
}


def prop(status, value=0.0, text="", unit="", conditions="", source="", note=""):
    return {"status": status, "value": value, "text": text, "unit": unit,
            "conditions": conditions, "source": source, "note": note}


def unknown(unit="", why="Not listed by the source."):
    return prop("unknown", unit=unit, note=why)


def not_sourced():
    return prop("not_sourced", note="Not yet taken from a citable source.")


def read_weights():
    text = open(WEIGHTS, encoding="utf-8").read()
    return {s: float(v) for s, v in re.findall(r'\{\s*"([A-Z][a-z]?)",\s*([0-9.]+)\s*\}', text)}


def period_and_group(z):
    ends = [2, 10, 18, 36, 54, 86, 118]
    period = next(i + 1 for i, e in enumerate(ends) if z <= e)
    start = 1 if period == 1 else ends[period - 2] + 1
    pos = z - start + 1
    if period == 1:
        return period, (1 if z == 1 else 18), ("s")
    if period in (2, 3):
        group = pos if pos <= 2 else pos + 10
        return period, group, ("s" if pos <= 2 else "p")
    if period in (4, 5):
        block = "s" if pos <= 2 else "d" if pos <= 12 else "p"
        return period, pos, block
    # periods 6 and 7: positions 3..17 are the f-block row (La–Lu, Ac–Lr), no group number
    if 3 <= pos <= 17:
        return period, 0, "f"
    group = pos if pos <= 2 else pos - 14
    block = "s" if pos <= 2 else "d" if group <= 12 else "p"
    return period, group, block


def grid_position(z, period, group):
    """Row/column of the cell (1-based). f-block rows 9 and 10, columns 3..17."""
    if group == 0:
        first = 57 if period == 6 else 89
        return (9 if period == 6 else 10), 3 + (z - first)
    return period, group


def number(text):
    try:
        return float(text)
    except (TypeError, ValueError):
        return None


def physical(z, key, raw, unit, conditions):
    v = number(raw)
    if v is None:
        why = "No macroscopic sample has been measured." if z in NO_BULK_SAMPLE else "Not listed by the source."
        return unknown(unit, why)
    if (z, key) in REFERENCE_NOTES:
        return prop("reference_dependent", v, unit=unit, conditions=conditions, source="pubchem_pt",
                    note=REFERENCE_NOTES[(z, key)])
    if z in NO_BULK_SAMPLE:
        return prop("estimated", v, unit=unit, conditions=conditions, source="pubchem_pt",
                    note="No macroscopic sample has been measured; the listed number is an estimate.")
    return prop("listed", v, unit=unit, conditions=conditions, source="pubchem_pt",
                note=ALLOTROPE_NOTES.get(z, ""))


def build():
    snap = json.load(open(SNAPSHOT, encoding="utf-8"))["Table"]
    cols = snap["Columns"]["Column"]
    weights = read_weights()
    elements = []
    for row in snap["Row"]:
        d = dict(zip(cols, row["Cell"]))
        z = int(d["AtomicNumber"])
        sym = d["Symbol"]
        period, group, block = period_and_group(z)
        grow, gcol = grid_position(z, period, group)

        if sym in weights:
            saw = prop("standard", weights[sym], source="ciaaw_2021",
                       note="CIAAW 2021 abridged standard atomic weight (dimensionless relative mass).")
            iso = prop("not_applicable", note="This element has a standard atomic weight.")
        else:
            saw = prop("not_applicable", note="No standard atomic weight: the element has no stable isotopes "
                                             "with a characteristic terrestrial isotopic composition.")
            m = number(d["AtomicMass"])
            iso = (prop("listed", m, unit="u", source="pubchem_pt",
                        note="Atomic mass of a reference isotope as listed by PubChem — not a standard atomic weight.")
                   if m is not None else unknown("u"))

        cfg_raw = (d["ElectronConfiguration"] or "").strip()
        cfg_pred = "predicted" in cfg_raw or "calculated" in cfg_raw or z >= 104
        cfg_text = re.sub(r"\s*\((predicted|calculated)\)", "", cfg_raw).strip()
        econf = (prop("predicted" if cfg_pred else "listed", text=cfg_text, source="pubchem_pt",
                      note="Theoretical ground-state configuration." if cfg_pred else "")
                 if cfg_text else unknown())

        st_raw = (d["StandardState"] or "").strip()
        st_pred = st_raw.lower().startswith("expected") or z >= 100
        st_text = st_raw.lower().replace("expected to be a ", "")
        state = (prop("predicted" if st_pred else "listed", text=st_text,
                      conditions="room temperature and pressure, as listed by the source", source="pubchem_pt",
                      note="No bulk sample; this state is a prediction." if st_pred else "")
                 if st_text else unknown())

        en = number(d["Electronegativity"])
        ie = number(d["IonizationEnergy"])
        ea = number(d["ElectronAffinity"])
        rad = number(d["AtomicRadius"])
        ox = (d["OxidationStates"] or "").strip()
        year = (d["YearDiscovered"] or "").strip()
        cat = CATEGORY[d["GroupBlock"]]
        elements.append({
            "id": f"el_{z:03d}", "z": z, "symbol": sym, "name": d["Name"], "nameKey": f"element.{sym.lower()}.name",
            "category": cat, "categorySource": "pubchem_pt",
            "categoryNote": "Placed by its group; its chemistry is largely unknown." if z >= 104 else "",
            "period": period, "group": group, "block": block, "row": grow, "col": gcol,
            "standardAtomicWeight": saw, "referenceIsotopeMass": iso,
            "electronConfiguration": econf, "stateAtRoomConditions": state,
            "density": physical(z, "density", d["Density"], "g/cm³", "conditions not stated by the source"),
            "meltingPoint": physical(z, "meltingPoint", d["MeltingPoint"], "K", "101.325 kPa unless noted"),
            "boilingPoint": physical(z, "boilingPoint", d["BoilingPoint"], "K", "101.325 kPa unless noted"),
            "electronegativity": prop("listed", en, unit="Pauling scale", source="pubchem_pt") if en is not None else unknown("Pauling scale"),
            "firstIonizationEnergy": prop("listed", ie, unit="eV", source="pubchem_pt") if ie is not None else unknown("eV"),
            "electronAffinity": prop("listed", ea, unit="eV", source="pubchem_pt") if ea is not None else unknown("eV"),
            "atomicRadius": prop("listed", rad, unit="pm", source="pubchem_pt",
                                 note="Radius definition not stated by the source.") if rad is not None else unknown("pm"),
            "oxidationStates": prop("predicted" if z >= 104 else "listed", text=ox, source="pubchem_pt")
                               if ox else unknown(),
            "discovery": prop("listed", text="Known since antiquity" if year == "Ancient" else year, source="pubchem_pt")
                         if year else unknown(),
            "occurrence": not_sourced(), "uses": not_sourced(), "safety": not_sourced(),
        })

    elements.sort(key=lambda e: e["z"])
    return {"schemaVersion": 1, "sources": SOURCES, "elements": elements}


def main():
    data = build()
    zs = [e["z"] for e in data["elements"]]
    assert zs == list(range(1, 119)), "expected Z = 1..118 exactly once"
    assert len({e["symbol"] for e in data["elements"]}) == 118
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)
        f.write("\n")
    counts = {}
    for e in data["elements"]:
        for k, v in e.items():
            if isinstance(v, dict):
                counts.setdefault(k, {}).setdefault(v["status"], 0)
                counts[k][v["status"]] += 1
    print(f"wrote {OUT} ({len(zs)} elements)")
    for k, c in counts.items():
        print(f"  {k}: {c}")


if __name__ == "__main__":
    sys.exit(main())
