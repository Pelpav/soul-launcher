# Génère packs.zip (ressource intégrée à l'exe) : packs/<Cat>/<Nom>.file0|.ini + packs_meta.tsv
# packs_meta.tsv : cat \t nom \t nom anglais \t source \t fichier d'origine
import os, re, sys, zipfile

ROOT = os.path.dirname(os.path.abspath(__file__))
PACKS = os.path.join(ROOT, "packs")
AUTHORS = {
    "RPG": "FLegenda / playground.ru", "SRC": "fixylol (speedrun.com)", "NARRY": "Narry (speedrun.com)",
    "IW": "Ironwolf306", "TC": "TheCheese42", "PZ": "pizzuhh", "ZT": "ZheltyTsypa",
    "PX": "pixingdaiyue051", "NB": "NICKISBAD",
}

def english(orig, cat):
    base = orig.replace("\\", "/").rstrip("/").split("/")[-1]
    base = re.sub(r"^\d+[a-z]?[.)]\s*", "", base)
    m = re.match(r"^(Pacifist|Neutral|Genocide)\s*\d+\s*\((.+)\)$", base)
    if m: base = m.group(2) + " (" + m.group(1).lower() + ")"
    base = base.replace(" - ", " – ").strip()
    if base.lower() in ("file0", "undertale", "save", ""): return ""
    return base

meta, cat = [], None
for line in open(os.path.join(PACKS, "SOURCES.md"), encoding="utf-8"):
    h = re.match(r"^## (\S+) \(\d+\)", line)
    if h: cat = h.group(1); continue
    if cat and line.startswith("| ") and not line.startswith("| Preset") and not line.startswith("|---"):
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) < 6: continue
        name, code, orig = cells[0], cells[4], cells[5].strip("`")
        meta.append((cat, name, english(orig, cat), AUTHORS.get(code, code), orig))

out = os.path.join(ROOT, "packs.zip")
n = 0
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    names = set()
    for c in sorted(os.listdir(PACKS)):
        d = os.path.join(PACKS, c)
        if not os.path.isdir(d): continue
        for f in sorted(os.listdir(d)):
            if f.endswith(".file0") or f.endswith(".ini"):
                z.write(os.path.join(d, f), c + "/" + f)
                if f.endswith(".file0"): n += 1; names.add((c, f[:-6]))
    missing = [m for m in meta if (m[0], m[1]) not in names]
    z.writestr("packs_meta.tsv", "\n".join("\t".join(m) for m in meta))
print(n, "presets,", len(meta), "métadonnées,", len(missing), "sans fichier", file=sys.stderr)
for m in missing[:10]: print("  manquant:", m[0], m[1], file=sys.stderr)
for m in meta[:6] + meta[70:74]: print("  ", m[1], "->", m[2], file=sys.stderr)
