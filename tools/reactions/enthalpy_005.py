#!/usr/bin/env python3
"""Enthalpy 005 — standard reaction enthalpy of every reaction by Hess's law.

Run from the repository root:
    python3 tools/reactions/enthalpy_005.py           # report only
    python3 tools/reactions/enthalpy_005.py --write   # write reactions.json, then run `reactions_tool.py pack`

ΔH°rxn(25 °C) = Σ ν·ΔfH°(products) − Σ ν·ΔfH°(reactants), in kJ per mol of reaction as written,
with each species in the state the equation gives it. solvent_water adds that many H2O(l) to
the reactants (as in reactions_tool.check).

ΔfH° sources, first match wins (tools/thermo/):
  1. codata_key_values_1989.json  CODATA Key Values (Cox, Wagman & Medvedev 1989)
  2. nbs_1982_verified.json       NBS Tables (Wagman et al. 1982), each value read from the page image
  3. janaf_1998_selected.json     NIST-JANAF (Chase 1998), liquids at 25 °C only
  4. nist_webbook_organics_2026-10-18.json  NIST Chemistry WebBook, organic compounds above C2
  Elements in their reference state are 0 by definition. Aqueous species follow
  tools/thermo/aqueous_species.py (strong electrolytes as the sum of their ions).

Written per reaction:
  enthalpyKJPerMol  ΔH° (kJ/mol), or 0 when a ΔfH° is missing
  enthalpySource    the tables used, or "" when not sourced
  enthalpyMissing   the species without a sourced ΔfH°, or "" when sourced
Nothing is estimated: a reaction with one missing species gets no value.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "thermo"))
import reactions_tool as tool  # noqa: E402
import aqueous_species as aq  # noqa: E402

THERMO = os.path.join(HERE, "..", "thermo")

SOURCE_NAMES = {
    "CODATA": "CODATA Key Values 1989",
    "NBS": "NBS Tables 1982",
    "JANAF": "NIST-JANAF 1998",
    "WebBook": "NIST Chemistry WebBook",
    "definition": "element in its reference state",
}

# Reference-state elements that CODATA does not list (ΔfH° = 0 by definition; NBS lists them as 0).
REFERENCE_ELEMENTS = {"Fe", "Mn", "Cr", "Ni", "Ba"}

H2O_L = -285.830  # CODATA, used for the derived hydrated-ion value below

# Room-temperature reactions in solution whose stored observed ΔT is a small rise although the
# sourced ΔH° is endothermic (|ΔH°| > 5 kJ/mol). Their sign is turned to a fall of the same size;
# heated reactions, electrolysis and concentrated acids keep theirs (the heater, the current or the
# heat of mixing dominates there). The size stays the record's estimate.
TEMPERATURE_SIGN_FIX = {
    "rxn_002", "rxn_029", "rxn_044", "rxn_053", "rxn_056",
    "rxn_072", "rxn_073", "rxn_211", "rxn_212", "rxn_213",
}


def _state(printed):
    head = printed.split(",")[0].strip()
    return {"cr": "s", "l": "l", "g": "g", "aq": "aq"}.get(head, head)


def load_table():
    table = {}

    def put(species, state, value, source, note=""):
        table.setdefault((species, state), (value, source, note))

    for row in json.load(open(os.path.join(THERMO, "codata_key_values_1989.json")))["values"]:
        put(row["substance"], _state(row["state"]), row["dfH_kJ_per_mol"], "CODATA")
    for row in json.load(open(os.path.join(THERMO, "nbs_1982_verified.json")))["values"]:
        put(row["species"], row["state"], row["dfH_kJ_per_mol"], "NBS", "p. " + row["page"])
    for row in json.load(open(os.path.join(THERMO, "janaf_1998_selected.json")))["values"]:
        put(row["species"], row["state"], row["dfH_kJ_per_mol"], "JANAF", row["table"])
    for row in json.load(open(os.path.join(THERMO, "nist_webbook_organics_2026-10-18.json")))["values"]:
        put(row["species"], row["state"], row["dfH_kJ_per_mol"], "WebBook", row["reference"])
    for el in REFERENCE_ELEMENTS:
        put(el, "s", 0.0, "definition")

    # Derived values (each written out so it can be checked):
    # Cr³⁺(aq): NBS lists only the hexaaqua ion; its six waters are solvent.
    hexaaqua = table[("[Cr(H2O)6]+3", "aq")]
    put("Cr+3", "aq", hexaaqua[0] - 6 * H2O_L, "NBS", "from [Cr(H2O)6]3+ (p. 2-198) minus 6 H2O(l)")
    # P2O5 is the empirical formula of P4O10.
    p4o10 = table[("P4O10", "s")]
    put("P2O5", "s", p4o10[0] / 2.0, "NBS", "half of P4O10 (p. 2-73)")
    return table


def terms(formula, state):
    """The (species, state, count) terms whose ΔfH° make up one formula unit."""
    if state == "aq":
        if formula in aq.STRONG_ELECTROLYTES:
            return [(ion, "aq", n) for ion, n in aq.STRONG_ELECTROLYTES[formula].items()]
        if formula in aq.BARE_IONS:
            return [(aq.BARE_IONS[formula], "aq", 1)]
        if formula in aq.ALIASES_AQ:
            return [(sp, st, n) for sp, (st, n) in aq.ALIASES_AQ[formula].items()]
    return [(formula, state, 1)]


def reaction_enthalpy(reaction, table):
    """(ΔH° kJ/mol or None, sources used, missing species)."""
    total, used, missing = 0.0, set(), []
    sides = [(reaction.get("reactants") or [], -1.0), (reaction.get("products") or [], 1.0)]
    for chems, sign in sides:
        for c in chems:
            for species, state, n in terms(c["formula"], c.get("state", "")):
                hit = table.get((species, state))
                if hit is None:
                    label = "%s(%s)" % (c["formula"], c.get("state", ""))
                    if label not in missing:
                        missing.append(label)
                    continue
                total += sign * float(c.get("stoich", 1)) * n * hit[0]
                used.add(hit[1])
    water = reaction.get("solvent_water") or 0
    if water:
        total -= water * table[("H2O", "l")][0]
        used.add("CODATA")
    if missing:
        return None, used, missing
    return total, used, missing


def source_text(used):
    names = [SOURCE_NAMES[s] for s in ("CODATA", "NBS", "JANAF", "WebBook") if s in used]
    return "Hess's law from standard enthalpies of formation at 25 °C: " + "; ".join(names)


def main():
    write = "--write" in sys.argv
    table = load_table()
    with open(tool.SRC, encoding="utf-8") as f:
        raw = f.read()
    db = json.loads(raw)
    reactions = db["reactions"]

    sourced, blocked, sign_mismatch = 0, {}, []
    for r in reactions:
        dh, used, missing = reaction_enthalpy(r, table)
        if dh is None:
            r["enthalpyKJPerMol"] = 0
            r["enthalpySource"] = ""
            r["enthalpyMissing"] = "no sourced standard enthalpy of formation for " + ", ".join(missing)
            for m in missing:
                blocked.setdefault(m, []).append(r["id"])
            continue
        sourced += 1
        r["enthalpyKJPerMol"] = round(dh, 2)
        r["enthalpySource"] = source_text(used)
        r["enthalpyMissing"] = ""
        ve = r.get("visual_effects") or {}
        if r["id"] in TEMPERATURE_SIGN_FIX and dh > 5 and ve.get("temperature_delta", 0) > 0:
            ve["temperature_delta"] = -ve["temperature_delta"]
        dt = ve.get("temperature_delta", 0) or 0
        if dt and (dt > 0) == (dh > 0) and abs(dh) > 5:
            sign_mismatch.append((r["id"], r.get("name_en"), round(dh, 1), dt))

    print("reactions: %d, with sourced ΔH°: %d, without: %d" % (len(reactions), sourced, len(reactions) - sourced))
    print("\nmissing ΔfH° (species: reactions blocked):")
    for m, ids in sorted(blocked.items(), key=lambda kv: -len(kv[1])):
        print("  %-24s %3d  %s" % (m, len(ids), " ".join(ids[:6])))
    print("\nobserved ΔT has the opposite sign to ΔH° (|ΔH°| > 5 kJ/mol): %d" % len(sign_mismatch))
    for rid, name, dh, dt in sign_mismatch:
        print("  %s %-45s ΔH° %8.1f  observed ΔT %+g" % (rid, name, dh, dt))

    if write:
        if json.dumps(db["reactions"]) == json.dumps(json.loads(raw)["reactions"]):
            print("\nnothing to change")
        else:
            tool.save(db["reactions"])
            print("\nwrote %s; now run reactions_tool.py pack" % os.path.relpath(tool.SRC))

if __name__ == "__main__":
    main()
