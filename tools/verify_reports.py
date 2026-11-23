"""Checks result PDFs downloaded from 5-15.org against the norms and item mapping in this app.

Usage: python3 tools/verify_reports.py <folder-or-pdf> [...]

For each report it checks that
  * the norm columns (median, mean, SD, 98%, 90%) match one of the app's eight age/sex tables exactly, and
  * every result can be produced with the app's item counts: each subdomain score is k/n for a whole number k
    of points over its n items, and each domain score equals the item-weighted mean of its subdomains.
"""
import glob, os, re, sys
from fractions import Fraction
import pdfplumber

ROOT = os.path.join(os.path.dirname(__file__), "..")
KEYS = {
    "Motor skills": "motor", "Gross motor skills": "motor.gross", "Fine motor skills": "motor.fine",
    "Executive functions": "exec", "Attention and concentration": "exec.attention",
    "Overactivity and impulsivity": "exec.hyper", "Passivity and inactivity": "exec.passivity",
    "Planning and organizing": "exec.planning", "Perception": "perc",
    "Perception of space and directions": "perc.space", "Concepts of time": "perc.time",
    "Perception of own body": "perc.body", "Perception of visual forms and figures": "perc.visual",
    "Memory": "memory", "Language": "lang", "Comprehension of spoken language": "lang.comprehension",
    "Expressive language": "lang.expressive", "Verbal communication": "lang.communication",
    "Learning": "learn", "Reading and writing": "learn.reading", "Arithmetic": "learn.arithmetic",
    "General learning": "learn.general", "Coping with learning": "learn.coping",
    "Social skills": "social", "Emotional/behavioral difficulties": "emo",
    "Internalisation": "emo.internal", "Acting out": "emo.acting", "Obsessive actions or thoughts": "emo.obsessive",
}
ROW = re.compile(r"^(.*?)\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)$")


def load_norms():
    src = open(os.path.join(ROOT, "src/FimmFimmtan.Core/NormData.cs"), encoding="utf-8").read()
    tables = {}
    for m in re.finditer(r"\[\(AgeGroup\.(\w+), Sex\.(\w+)\)\] = new\(\)\s*\{(.*?)\n        \}", src, re.S):
        rows = {k: tuple(float(x) for x in v.split(",")) for k, v in re.findall(r'\["([\w.]+)"\] = new\(([^)]*)\)', m.group(3))}
        tables[f"{m.group(1).replace('Age', '').replace('_', '-')} {m.group(2)}"] = rows
    return tables


def load_item_counts():
    src = open(os.path.join(ROOT, "src/FimmFimmtan.Core/Scales.cs"), encoding="utf-8").read()
    unscored = [int(x) for x in re.search(r"UnscoredItems = \[([^\]]*)\]", src).group(1).split(",")]
    counts = {}
    for kind, key, a, b in re.findall(r'([DS])\("([\w.]+)", "[^"]*", "[^"]*", (\d+), (\d+)\)', src):
        counts[key] = len([i for i in range(int(a), int(b) + 1) if i not in unscored])
    return counts


def check(path, tables, counts):
    text = "\n".join(pg.extract_text() or "" for pg in pdfplumber.open(path).pages)
    result, norms, answered = {}, {}, {}
    for line in text.splitlines():
        m = ROW.match(line.strip())
        if m and m.group(1).strip() in KEYS:
            key = KEYS[m.group(1).strip()]
            result[key] = float(m.group(2))
            answered[key] = float(m.group(3))
            norms[key] = tuple(float(x) for x in m.groups()[3:])
    problems = []
    if len(norms) != len(KEYS):
        return [f"fann aðeins {len(norms)} af {len(KEYS)} línum í töflunni"], "?"

    group = next((g for g, t in tables.items() if all(t[k] == v for k, v in norms.items() if any(v))), None)
    if group is None:
        best = min(tables, key=lambda g: sum(tables[g][k] != v for k, v in norms.items()))
        diffs = [f"{k}: skjal {v} / forrit {tables[best][k]}" for k, v in norms.items() if tables[best][k] != v and any(v)]
        problems.append(f"viðmið passa ekki við neina töflu; næst {best}: " + "; ".join(diffs))
        group = best + " (?)"

    for key, n in counts.items():
        if "." not in key and any(k.startswith(key + ".") for k in counts):
            continue
        if answered[key] < 100:
            continue
        k = round(result[key] * n)
        if abs(k / n - result[key]) > 0.005:
            problems.append(f"{key} = {result[key]:.2f} er ekki heil stig deilt með {n} spurningum")
    for dom in [k for k in counts if "." not in k]:
        subs = [k for k in counts if k.startswith(dom + ".")]
        if not subs or any(answered[s] < 100 for s in subs + [dom]):
            continue
        total = sum(Fraction(round(result[s] * counts[s]), 1) for s in subs)
        if abs(float(total / counts[dom]) - result[dom]) > 0.005:
            problems.append(f"{dom} = {result[dom]:.2f} en undirþættir gefa {float(total / counts[dom]):.2f}")
    return problems, group


def main(args):
    files = []
    for a in args or ["."]:
        files += sorted(glob.glob(os.path.join(a, "*.pdf"))) if os.path.isdir(a) else [a]
    tables, counts = load_norms(), load_item_counts()
    bad = 0
    for f in files:
        problems, group = check(f, tables, counts)
        status = "Í LAGI" if not problems else "FRÁVIK"
        bad += bool(problems)
        print(f"{status:7} {os.path.basename(f)}  [{group}]")
        for p in problems:
            print(f"        - {p}")
    print(f"\n{len(files) - bad} af {len(files)} skýrslum passa við forritið.")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
