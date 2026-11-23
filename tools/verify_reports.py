"""Checks result PDFs downloaded from 5-15.org against the norms and item mapping in this app.

Usage: python3 tools/verify_reports.py <folder-or-pdf> [...]

For each report it checks that
  * the norm columns (median, mean, SD, 98%, 90%) match one of the app's eight age/sex tables exactly, and
  * every result can be produced with the app's item counts: each subdomain score is k/n for a whole number k
    of points over its n items, and each domain score equals the item-weighted mean of its subdomains.
"""
import glob, os, re, sys
from collections import Counter, defaultdict
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
# Result, % Ans. and 2–5 norm columns: reports from 5-15.org often cut the table at the page margin so that only
# Median and Mid. v. remain.
ROW = re.compile(r"^(.*?)((?:\s+[\d.]+){4,7})$")


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


def same(app_norm, report_norm):
    """Compares the norm columns the report shows (it may only show Median and Mid. v.)."""
    return tuple(app_norm[:len(report_norm)]) == tuple(report_norm)


def informant(text):
    for word, label in (("Teacher", "kennari"), ("Self", "sjálfsmat"), ("Parent", "foreldri")):
        if re.search(r"\b%s\b" % word, text):
            return label
    return "óþekkt"


def check(path, tables, counts):
    """Returns (problems, group, info). Each problem is (kind, key, detail)."""
    text = "\n".join(pg.extract_text() or "" for pg in pdfplumber.open(path).pages)
    result, norms, answered = {}, {}, {}
    for line in text.splitlines():
        m = ROW.match(line.strip())
        if m and m.group(1).strip() in KEYS:
            key = KEYS[m.group(1).strip()]
            nums = [float(x) for x in m.group(2).split()]
            result[key], answered[key] = nums[0], nums[1]
            norms[key] = tuple(nums[2:])
    info = {"informant": informant(text), "norms": norms, "result": result, "answered": answered}
    if len(norms) != len(KEYS):
        missing = sorted(set(KEYS.values()) - set(norms))
        return [("parse", None, f"fann {len(norms)} af {len(KEYS)} línum; vantar {', '.join(missing)}")], "?", info

    problems = []
    group = next((g for g, t in tables.items() if all(same(t[k], v) for k, v in norms.items() if any(v))), None)
    if group is None:
        best = min(tables, key=lambda g: sum(not same(tables[g][k], v) for k, v in norms.items()))
        diffs = [k for k, v in norms.items() if not same(tables[best][k], v) and any(v)]
        problems.append(("norms", best, f"viðmið passa ekki við neina töflu; næst {best}, munur á: {', '.join(diffs)}"))
        group = best + " (?)"

    for key, n in counts.items():
        if "." not in key and any(k.startswith(key + ".") for k in counts):
            continue
        if answered[key] < 100:
            continue
        k = round(result[key] * n)
        if abs(k / n - result[key]) > 0.005:
            problems.append(("items", key, f"{key} = {result[key]:.2f} er ekki heil stig deilt með {n} spurningum"))
    for dom in [k for k in counts if "." not in k]:
        subs = [k for k in counts if k.startswith(dom + ".")]
        if not subs or any(answered[s] < 100 for s in subs + [dom]):
            continue
        total = sum(Fraction(round(result[s] * counts[s]), 1) for s in subs)
        if abs(float(total / counts[dom]) - result[dom]) > 0.005:
            problems.append(("domain", dom, f"{dom} = {result[dom]:.2f} en undirþættir gefa {float(total / counts[dom]):.2f}"))
    return problems, group, info


def possible_item_counts(values):
    """Item counts n (1..40) for which every observed score is a whole number of points divided by n."""
    return [n for n in range(1, 41) if all(abs(round(v * n) / n - v) <= 0.005 for v in values)]


def summarize(results, tables, counts, out):
    def p(line=""):
        print(line)
        out.write(line + "\n")

    p("=" * 70)
    p("SAMANTEKT (engar persónuupplýsingar – má senda áfram)")
    p("=" * 70)
    p(f"Skýrslur: {len(results)}, í lagi: {sum(not r[1] for r in results)}")

    by_informant = Counter(r[3]["informant"] for r in results)
    ok_informant = Counter(r[3]["informant"] for r in results if not r[1])
    p("\nSvarandi: " + ", ".join(f"{k} {v} (í lagi {ok_informant[k]})" for k, v in by_informant.items()))

    groups = Counter(r[2] for r in results)
    p("\nAldurshópur/kyn (út frá viðmiðum): " + ", ".join(f"{g}: {c}" for g, c in sorted(groups.items())))

    kinds = Counter(kind for r in results for kind, _, _ in r[1])
    p("\nTegundir frávika (fjöldi skýrslna með hverja):")
    for kind, label in (("parse", "tafla fannst ekki/ólesanleg"), ("norms", "viðmið passa ekki"),
                        ("items", "svið passar ekki við fjölda spurninga"), ("domain", "aðalþáttur ≠ undirþættir")):
        p(f"  {label}: {sum(1 for r in results if any(k == kind for k, _, _ in r[1]))}")

    parse = Counter(d for r in results for k, _, d in r[1] if k == "parse")
    if parse:
        p("\nÓlesanlegar töflur:")
        for d, c in parse.most_common(10):
            p(f"  {c} ×  {d}")

    unknown = defaultdict(list)
    for r in results:
        if any(k == "norms" for k, _, _ in r[1]):
            unknown[tuple(sorted(r[3]["norms"].items()))].append(r)
    if unknown:
        p(f"\nÓþekkt viðmiðasett: {len(unknown)} ólík sett")
        for i, (fp, rs) in enumerate(sorted(unknown.items(), key=lambda x: -len(x[1])), 1):
            norms = dict(fp)
            best = min(tables, key=lambda g: sum(not same(tables[g][k], v) for k, v in norms.items()))
            inf = Counter(r[3]["informant"] for r in rs)
            p(f"\n  Sett {i}: {len(rs)} skýrslur ({', '.join(f'{k} {v}' for k, v in inf.items())}), næst töflu {best}")
            p(f"  {'svið':<22} {'Median':>6} {'Mean':>6} {'SD':>6} {'98%':>6} {'90%':>6}   forrit ({best})")
            for key in KEYS.values():
                v, t = norms[key], tables[best][key]
                mark = "" if same(t, v) else "   <-- " + " ".join(f"{x:.2f}" for x in t[:len(v)])
                p(f"  {key:<22} " + " ".join(f"{x:6.2f}" for x in v) + mark)

    item_vals = defaultdict(list)
    for r in results:
        for k, key, _ in r[1]:
            if k == "items":
                item_vals[key].append(r[3]["result"][key])
    if item_vals:
        p("\nSvið þar sem niðurstaða passar ekki við fjölda spurninga:")
        for key, vals in sorted(item_vals.items(), key=lambda x: -len(x[1])):
            all_vals = [r[3]["result"][key] for r in results if key in r[3]["result"] and r[3]["answered"].get(key) == 100]
            p(f"  {key}: {len(vals)} skýrslur; forritið notar {counts[key]} spurningar; "
              f"gildi sem passa ekki: {sorted(set(vals))[:12]}; mögulegur fjöldi spurninga: {possible_item_counts(all_vals)[:6]}")

    dom = Counter(key for r in results for k, key, _ in r[1] if k == "domain")
    if dom:
        p("\nAðalþættir sem passa ekki við undirþætti: " + ", ".join(f"{k} ({c})" for k, c in dom.most_common()))

    partial = Counter(key for r in results for key, a in r[3]["answered"].items() if 0 < a < 100)
    if partial:
        p("\nSvið með ófullkomnum svörum (ekki athuguð): " + ", ".join(f"{k} ({c})" for k, c in partial.most_common(8)))


def main(args):
    files = []
    for a in args or ["."]:
        files += sorted(glob.glob(os.path.join(a, "**", "*.pdf"), recursive=True)) if os.path.isdir(a) else [a]
    tables, counts = load_norms(), load_item_counts()
    results = []
    for f in files:
        try:
            problems, group, info = check(f, tables, counts)
        except Exception as ex:
            problems, group, info = [("parse", None, f"gat ekki opnað PDF ({type(ex).__name__})")], "?", {
                "informant": "óþekkt", "norms": {}, "result": {}, "answered": {}}
        results.append((f, problems, group, info))
        status = "Í LAGI" if not problems else "FRÁVIK"
        print(f"{status:7} {os.path.basename(f)}  [{group}]")
        for _, _, d in problems:
            print(f"        - {d}")
    print(f"\n{sum(not r[1] for r in results)} af {len(files)} skýrslum passa við forritið.\n")
    with open("5-15-samantekt.txt", "w", encoding="utf-8") as out:
        summarize(results, tables, counts, out)
    print(f"\nSamantektin var líka vistuð í {os.path.abspath('5-15-samantekt.txt')}")
    return 1 if any(r[1] for r in results) else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
