#!/usr/bin/env python3
"""Reaction database helper for ChemLabSim (runs outside Unity).

Usage (from the repository root):
    python3 tools/reactions/reactions_tool.py check             # validate reactions.json
    python3 tools/reactions/reactions_tool.py add batch_001     # append a batch module, then pack
    python3 tools/reactions/reactions_tool.py pack              # regenerate reactions.bytes + manifest

`pack` mirrors Assets/_Project/Scripts/Security (KeyMaterial + CryptoUtil):
AES-256-CBC + HMAC-SHA256, file layout [iv(16)][cipher][hmac(32)]. It needs the
`openssl` command line tool.
"""
import datetime
import hashlib
import hmac
import importlib
import json
import os
import re
import subprocess
import sys
import textwrap
from collections import Counter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Assets/_Project/DataSrc/reactions.json")
MATERIALS = os.path.join(ROOT, "Assets/_Project/DataSrc/materials.json")
BYTES = os.path.join(ROOT, "Assets/_Project/DataSecure/reactions.bytes")
MANIFEST = os.path.join(ROOT, "Assets/_Project/DataSecure/reactions.manifest.json")

MAX_SLIDER_TEMP_C = 1000  # TemperatureSliderView.MaxTemperatureC
STATES = ("s", "l", "g", "aq")
EFFERVESCENCE = ("none", "mild", "vigorous", "violent")


# ── Formula / equation parsing ────────────────────────────────────────────

def atoms(formula):
    """Atom counts of a formula such as 'Ca3(PO4)2' or 'Cu(CH3COO)2'."""
    pos = 0

    def group():
        nonlocal pos
        total = Counter()
        while pos < len(formula) and formula[pos] != ")":
            if formula[pos] == "(":
                pos += 1
                inner = group()
                if pos >= len(formula) or formula[pos] != ")":
                    raise ValueError("unbalanced parentheses in " + formula)
                pos += 1
                n = number()
                for el, c in inner.items():
                    total[el] += c * n
            else:
                m = re.match(r"[A-Z][a-z]?", formula[pos:])
                if not m:
                    raise ValueError("cannot parse formula " + formula)
                pos += m.end()
                total[m.group()] += number()
        return total

    def number():
        nonlocal pos
        m = re.match(r"\d+", formula[pos:])
        if not m:
            return 1
        pos += m.end()
        return int(m.group())

    result = group()
    if pos != len(formula):
        raise ValueError("cannot parse formula " + formula)
    return result


def parse_side(text):
    """'2 HCl(aq) + Ba(OH)2(aq)' -> [{'formula','state','stoich'}, ...]"""
    out = []
    for term in text.split(" + "):
        m = re.match(r"^\s*(\d+)?\s*(\S+)\((s|l|g|aq)\)\s*$", term)
        if not m:
            raise ValueError("bad equation term: '%s'" % term)
        out.append({"formula": m.group(2), "state": m.group(3), "stoich": int(m.group(1) or 1)})
    return out


def parse_equation(eq):
    left, right = eq.split(" -> ")
    return parse_side(left), parse_side(right)


def side_atoms(chems):
    total = Counter()
    for c in chems:
        try:
            for el, n in atoms(c["formula"]).items():
                total[el] += n * c.get("stoich", 1)
        except ValueError:
            return None
    return total


def reactant_key(reaction):
    return "|".join(sorted(c["formula"] for c in reaction.get("reactants", [])))


# ── Validation ────────────────────────────────────────────────────────────

def load():
    with open(SRC, encoding="utf-8") as f:
        raw = f.read()
    return raw, json.loads(raw)["reactions"]


def save(reactions):
    """Write reactions.json in its stored layout (one indented block per reaction, ASCII escapes)."""
    blocks = ["  " + json.dumps(r, indent=2, ensure_ascii=True).replace("\n", "\n  ") for r in reactions]
    out = '{\n"reactions": [\n' + ",\n".join(blocks) + "\n]\n}\n"
    json.loads(out)
    with open(SRC, "w", encoding="utf-8") as f:
        f.write(out)


def check(reactions, strict_from=None):
    """Returns (errors, warnings). Entries at index >= strict_from must also balance."""
    errors, warnings = [], []
    with open(MATERIALS, encoding="utf-8") as f:
        mats = json.load(f)
    mats = mats if isinstance(mats, list) else mats["materials"]
    known = {m["formula"] for m in mats}
    # Formulas that older entries already use as reactants are valid dropdown reagents too.
    known |= {c["formula"] for r in reactions[:strict_from] for c in r.get("reactants", [])}

    seen_ids, seen_keys = {}, {}
    for i, r in enumerate(reactions):
        rid = r.get("id", "?")
        strict = strict_from is not None and i >= strict_from
        if rid in seen_ids:
            errors.append("%s: duplicate id" % rid)
        seen_ids[rid] = i
        if not r.get("reactants") or not r.get("products"):
            errors.append("%s: reactants/products missing" % rid)
            continue
        key = reactant_key(r)
        if key in seen_keys and not r.get("debug_only"):   # debug_only: reached by id, not by the UI
            (errors if strict else warnings).append(
                "%s: same reactant set as %s (unreachable, first entry wins)" % (rid, seen_keys[key]))
        else:
            seen_keys[key] = rid

        left, right = side_atoms(r["reactants"]), side_atoms(r["products"])
        # Water of an aqueous solution can take part without being a selectable reagent
        # ("solvent_water": n adds n H2O to the left side).
        if left is not None and r.get("solvent_water"):
            left = left + Counter({"H": 2 * r["solvent_water"], "O": r["solvent_water"]})
        if r.get("skip_balance"):
            pass   # documented exception, e.g. a polymer with no fixed formula
        elif left is None or right is None:
            (errors if strict else warnings).append("%s: formula could not be parsed" % rid)
        elif left != right:
            (errors if strict else warnings).append("%s: equation is not balanced" % rid)

        if strict:
            for c in r["reactants"] + r["products"]:
                if c.get("state") not in STATES:
                    errors.append("%s: bad state for %s" % (rid, c["formula"]))
            for c in r["reactants"]:
                if c["formula"] not in known:
                    errors.append("%s: reactant %s is not a known reagent" % (rid, c["formula"]))
            if not 0 <= r.get("activationTempC", -1) <= MAX_SLIDER_TEMP_C:
                errors.append("%s: activationTempC outside the slider range" % rid)
            if r.get("requiredMedium", "") not in ("", "Neutral", "Acidic", "Basic"):
                errors.append("%s: bad requiredMedium" % rid)
            if r["visual_effects"].get("effervescence") not in EFFERVESCENCE:
                errors.append("%s: bad effervescence" % rid)
            for field in ("name_en", "reactionType", "observation_en", "explanation_en"):
                if not r.get(field):
                    errors.append("%s: %s is empty" % (rid, field))
    return errors, warnings


# ── Encryption (mirror of KeyMaterial.cs / CryptoUtil.cs) ────────────────

def _sha(text):
    return hashlib.sha256(text.encode("utf-8")).digest()


def _hkdf(ikm, salt, info, length=32):
    prk = hmac.new(salt, ikm, "sha256").digest()
    block, okm, counter = b"", b"", 1
    while len(okm) < length:
        block = hmac.new(prk, block + info + bytes([counter]), "sha256").digest()
        okm += block
        counter += 1
    return okm[:length]


def _keys():
    secret = "KX9q-" + "1pTz#" + "a_7!"
    master = _hkdf(_sha("com.chemlabsim.app|ChemLabSim|ChemLabSim|" + secret),
                   _sha("ChemLabSim|reaction-key-salt|v2"), _sha("reactions-db-v1"))
    aad = _sha("com.chemlabsim.app|reactions-db-v1")
    enc = _hkdf(master, _sha("enc_salt"), _sha("enc_info"))
    mac = _hkdf(master, _sha("mac_salt"), _sha("mac_info"))
    return enc, mac, aad


def _aes(data, key, iv, decrypt=False):
    cmd = ["openssl", "enc", "-aes-256-cbc"] + (["-d"] if decrypt else []) + ["-K", key.hex(), "-iv", iv.hex()]
    return subprocess.run(cmd, input=data, capture_output=True, check=True).stdout


def pack():
    enc, mac, aad = _keys()
    with open(SRC, "rb") as f:
        plain = f.read()
    iv = os.urandom(16)
    cipher = _aes(plain, enc, iv)
    blob = iv + cipher + hmac.new(mac, aad + iv + cipher, "sha256").digest()
    if _aes(blob[16:-32], enc, blob[:16], decrypt=True) != plain:
        raise SystemExit("pack: round-trip check failed")
    with open(BYTES, "wb") as f:
        f.write(blob)
    manifest = {
        "schemaVersion": 1,
        "dataVersion": "reactions-db-v1",
        "bytesFileName": "reactions.bytes",
        "bytesLength": len(blob),
        "sha256Hex": hashlib.sha256(blob).hexdigest(),
        "generatedAtUtc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%f0Z"),
    }
    with open(MANIFEST, "w", encoding="utf-8") as f:
        f.write(json.dumps(manifest, indent=4))
    print("packed %d bytes -> %s" % (len(blob), os.path.relpath(BYTES, ROOT)))


# ── Adding a batch ────────────────────────────────────────────────────────

def add(batch_name):
    sys.path.insert(0, os.path.dirname(__file__))
    batch = importlib.import_module(batch_name)
    raw, reactions = load()
    tail = "\n]\n}\n"
    if not raw.endswith(tail):
        raise SystemExit("reactions.json does not end as expected; refusing to edit it")

    existing_keys = {reactant_key(r) for r in reactions}
    next_id = max(int(r["id"].split("_")[1]) for r in reactions) + 1
    new, skipped = [], []
    for entry in batch.REACTIONS:
        key = reactant_key(entry)
        if key in existing_keys:
            skipped.append(entry["name_en"])
            continue
        existing_keys.add(key)
        entry = dict(entry)
        record = {"id": "rxn_%03d" % next_id}
        record.update(entry)
        new.append(record)
        next_id += 1

    errors, _ = check(reactions + new, strict_from=len(reactions))
    if errors:
        print("\n".join(errors))
        raise SystemExit("batch rejected: %d problem(s)" % len(errors))

    blocks = [textwrap.indent(json.dumps(r, indent=2, ensure_ascii=True), "  ") for r in new]
    out = raw[:-len(tail)] + "".join(",\n" + b for b in blocks) + tail
    if json.loads(out)["reactions"][:len(reactions)] != reactions:
        raise SystemExit("existing reactions changed; aborting")
    with open(SRC, "w", encoding="utf-8") as f:
        f.write(out)
    print("added %d reactions (%d skipped: reactant set already exists)" % (len(new), len(skipped)))
    for name in skipped:
        print("  skipped:", name)
    pack()


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else "check"
    if cmd == "check":
        _, reactions = load()
        errors, warnings = check(reactions)
        for w in warnings:
            print("warning:", w)
        for e in errors:
            print("ERROR:", e)
        print("%d reactions, %d errors, %d warnings" % (len(reactions), len(errors), len(warnings)))
        sys.exit(1 if errors else 0)
    elif cmd == "pack":
        pack()
    elif cmd == "add" and len(sys.argv) == 3:
        add(sys.argv[2])
    else:
        raise SystemExit(__doc__)


if __name__ == "__main__":
    main()
