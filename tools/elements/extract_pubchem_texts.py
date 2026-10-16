#!/usr/bin/env python3
"""Extract the element texts used by the explorer from downloaded PubChem records.

Usage (from the repository root):
  python3 tools/elements/extract_pubchem_texts.py <element_dir>

<element_dir> holds, for Z = 1..118, the PubChem PUG-View records downloaded on 2026-10-16:
  <Z>.json      https://pubchem.ncbi.nlm.nih.gov/rest/pug_view/data/element/<Z>/JSON
  ghs_<Z>.json  https://pubchem.ncbi.nlm.nih.gov/rest/pug_view/data/compound/<CID>/JSON?heading=GHS+Classification
                (CID = the first "Element Forms" CID of the element record; none for Z = 112..118)
Writes tools/elements/pubchem_element_texts_2026-10-16.json (only the fields used, with their
source and link), which build_elements.py reads. The raw records are not stored (≈ 16 MB).

What is taken, verbatim (only markup turned into <sup>/<sub>):
  uses        "Uses"  — Jefferson Lab ("It's Elemental") if present, else Los Alamos National Laboratory
  occurrence  "Sources" — Los Alamos National Laboratory (or the first source PubChem lists)
  crustal / oceanic abundance — "Estimated Crustal/Oceanic Abundance" (Jefferson Lab)
  safety      GHS hazard statements of the EU harmonised classification only (Regulation (EC)
              No 1272/2008, Annex VI), one entry per classified form (e.g. "magnesium powder
              (pyrophoric)"). Industry notifications to ECHA are NOT used (they vary and carry
              percentages). Annex VI note markers (*, **, ***) are dropped from the codes.
              Only entries whose classified substance name is a form of the element itself are
              kept (e.g. "magnesium powder (pyrophoric)"); a compound that merely contains the
              element (PubChem lists one for Cl) is dropped.
"""
import json
import os
import re
import sys

OUT = os.path.join(os.path.dirname(__file__), "pubchem_element_texts_2026-10-16.json")
JLAB, LANL = "Jefferson Lab, U.S. Department of Energy", "Los Alamos National Laboratory, U.S. Department of Energy"
CLP = "Regulation (EC) No 1272/2008 of the European Parliament and of the Council"


def find(sections, heading):
    for s in sections:
        if s.get("TOCHeading") == heading:
            return s
        r = find(s.get("Section", []), heading)
        if r:
            return r
    return None


def marked(swm):
    """One StringWithMarkup entry as text, superscript/subscript kept as TMP tags."""
    text = swm.get("String", "")
    spans = sorted((m for m in swm.get("Markup", []) if m.get("Type") in ("Superscript", "Subscript")),
                   key=lambda m: m["Start"], reverse=True)
    for m in spans:
        tag = "sup" if m["Type"] == "Superscript" else "sub"
        a, b = m["Start"], m["Start"] + m["Length"]
        text = text[:a] + f"<{tag}>" + text[a:b] + f"</{tag}>" + text[b:]
    return text


def entries(record, heading):
    refs = {r["ReferenceNumber"]: r for r in record.get("Reference", [])}
    s = find(record["Section"], heading)
    out = []
    for inf in (s or {}).get("Information", []):
        r = refs.get(inf.get("ReferenceNumber"), {})
        v = inf.get("Value", {})
        text = "\n\n".join(marked(x) for x in v.get("StringWithMarkup", []) if x.get("String"))
        if text:
            out.append({"source": r.get("SourceName", ""), "url": r.get("URL", ""), "text": text.strip()})
    return out


def pick(items, preferred):
    for name in preferred:
        for it in items:
            if it["source"] == name:
                return it
    return items[0] if items else None


# Spelling variants used by the CLP Annex VI entry names.
NAME_VARIANTS = {"aluminum": ["aluminum", "aluminium"], "sulfur": ["sulfur", "sulphur"], "cesium": ["cesium", "caesium"]}


def is_element_form(form_name, element_name):
    """The classified entry is the element itself (any physical form), not a compound containing it."""
    name = form_name.lower()
    variants = NAME_VARIANTS.get(element_name.lower(), [element_name.lower()])
    return any(name.startswith(v) or name.startswith("red " + v) or name.startswith("white " + v) for v in variants)


def safety(path, element_name):
    if not os.path.exists(path):
        return []
    d = json.load(open(path, encoding="utf-8"))
    if "Fault" in d:
        return []
    rec = d["Record"]
    refs = {r["ReferenceNumber"]: r for r in rec.get("Reference", [])}
    forms = {}
    def walk(sections):
        for s in sections:
            for inf in s.get("Information", []):
                r = refs.get(inf.get("ReferenceNumber"), {})
                if r.get("SourceName") != CLP:
                    continue
                form = forms.setdefault(inf["ReferenceNumber"], {"form": r.get("Name", ""), "url": r.get("URL", ""),
                                                                 "signal": "", "hazards": []})
                vals = [x.get("String", "") for x in inf.get("Value", {}).get("StringWithMarkup", [])]
                if inf.get("Name") == "Signal":
                    form["signal"] = " ".join(vals).strip()
                elif inf.get("Name") == "GHS Hazard Statements":
                    for h in vals:
                        h = re.sub(r"\s*\[.*\]\s*$", "", h)               # drop the "[Danger Category]" tail
                        h = re.sub(r"^(H\d{3}[A-Za-z]*)\s*\**", r"\1", h)  # drop Annex VI note markers
                        form["hazards"].append(h.strip())
            walk(s.get("Section", []))
    walk(rec["Section"])
    return [f for f in forms.values() if f["hazards"] and is_element_form(f["form"], element_name)]


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    src = sys.argv[1]
    out = {"retrieved": "2026-10-16", "elements": {}}
    for z in range(1, 119):
        rec = json.load(open(os.path.join(src, f"{z}.json"), encoding="utf-8"))["Record"]
        e = {
            "uses": pick(entries(rec, "Uses"), [JLAB, LANL]),
            "occurrence": pick(entries(rec, "Sources"), [LANL, JLAB]),
            "crustal": pick(entries(rec, "Estimated Crustal Abundance"), [JLAB]),
            "oceanic": pick(entries(rec, "Estimated Oceanic Abundance"), [JLAB]),
            "safety": safety(os.path.join(src, f"ghs_{z}.json"), rec.get("RecordTitle", "")),
        }
        out["elements"][str(z)] = e
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
        f.write("\n")
    n = lambda k: sum(1 for e in out["elements"].values() if e[k])
    print(f"wrote {OUT}: uses {n('uses')}, occurrence {n('occurrence')}, crustal {n('crustal')}, "
          f"oceanic {n('oceanic')}, harmonised safety {n('safety')}")


if __name__ == "__main__":
    main()
