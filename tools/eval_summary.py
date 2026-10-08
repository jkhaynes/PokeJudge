"""Turn `evaluate` output into a structured summary, and compare two summaries.

    python -I tools/eval_summary.py summarize <evaluate.log> <summary.json>
    python -I tools/eval_summary.py compare <before.json> <after.json>

`summarize` reads the console output of `dotnet run --project PokeJudge -- evaluate`
(stdout and stderr together, so Jev retry lines are kept) and records, per scenario
run: turns, every turn's retrieved chunks, the clarifying questions, each criterion's
PASS/FAIL, the model's and the validated Source Support, the cited chunks, the first
line of the recommendation, and any Jev retries or infrastructure failure.

`compare` prints a Markdown report of two summaries side by side: overall pass rate,
per-scenario passes, turns, Source Support, criterion failures, and which sections each
scenario received. It's how runs with different settings (for example `--top 5` against
`--top 10`) are compared, so both runs must be parsed by this same script.
"""
import json
import re
import sys
from collections import Counter
from pathlib import Path

RUN = re.compile(r"^--- \[(?P<id>[^\]]+)\] (?P<category>.+) \(run (?P<run>\d+)/(?P<of>\d+)\) ---$")
INFRA = re.compile(r"\[INFRASTRUCTURE FAILURE -- not counted\] (?P<msg>.*)$")
TURNS = re.compile(r"^Turns used: (?P<n>\d+) \((?P<why>[^)]+)\)")
RETRIEVED = re.compile(r"^\s+\[Turn (?P<turn>\d+) retrieved\] (?P<list>.*)$")
QUESTION = re.compile(r"^\s+\[Turn (?P<turn>\d+) question — re: (?P<re>[^\]]+)\] (?P<q>.*)$")
CRITERION = re.compile(r"^\s+\[(?P<result>PASS|FAIL)\] (?P<name>[^:]+): (?P<detail>.*)$")
ASSESSMENT = re.compile(r"^\s+Model's own assessment: (?P<model>\S+) \| Validated: (?P<validated>\S+)")
CITED = re.compile(r"^Cited chunk IDs: (?P<ids>.*)$")
RECOMMENDATION = re.compile(r"^Recommendation: (?P<text>.*)$")
RETRY = re.compile(r"^Jev .*retrying in")
SCENARIO_RESULT = re.compile(r"^\s+\[(?P<id>[^\]]+)\] (?P<passed>\d+)/(?P<of>\d+) runs fully passed\.")
RESULT = re.compile(r"^Result: (?P<passed>\d+)/(?P<of>\d+) scenario-runs fully passed")
INFRA_TOTAL = re.compile(r"^Infrastructure failures \(not counted above\): (?P<n>\d+)")
TOP_K = re.compile(r"^The AI reads the top (?P<k>\d+) excerpts per turn")
CHUNK = re.compile(r"(?P<id>\S+) \((?P<score>[0-9.]+)\)")


def summarize(log_path):
    runs, current, scenario_passes, totals = [], None, {}, {}
    for line in Path(log_path).read_text(encoding="utf-8", errors="replace").splitlines():
        if m := RUN.match(line):
            current = {
                "scenario": m["id"], "category": m["category"], "run": int(m["run"]),
                "infrastructureFailure": None, "turns": None, "stopReason": None,
                "retrieved": [], "questions": [], "criteria": {}, "modelSupport": None,
                "validatedSupport": None, "cited": [], "recommendation": None, "jevRetries": 0,
            }
            runs.append(current)
        elif m := SCENARIO_RESULT.match(line):
            scenario_passes[m["id"]] = int(m["passed"])
        elif m := RESULT.match(line):
            totals["passed"], totals["completedRuns"] = int(m["passed"]), int(m["of"])
        elif m := TOP_K.match(line):
            totals["topK"] = int(m["k"])
        elif m := INFRA_TOTAL.match(line):
            totals["infrastructureFailures"] = int(m["n"])
        elif current is None:
            continue
        elif m := INFRA.search(line):
            current["infrastructureFailure"] = m["msg"]
        elif RETRY.match(line):
            current["jevRetries"] += 1
        elif m := TURNS.match(line):
            current["turns"], current["stopReason"] = int(m["n"]), m["why"]
        elif m := RETRIEVED.match(line):
            current["retrieved"].append([c["id"] for c in CHUNK.finditer(m["list"])])
        elif m := QUESTION.match(line):
            current["questions"].append({"turn": int(m["turn"]), "about": m["re"], "question": m["q"]})
        elif m := CRITERION.match(line):
            current["criteria"][m["name"]] = m["result"]
        elif m := ASSESSMENT.match(line):
            current["modelSupport"], current["validatedSupport"] = m["model"], m["validated"]
        elif m := CITED.match(line):
            current["cited"] = [c.strip() for c in m["ids"].split(",") if c.strip()]
        elif m := RECOMMENDATION.match(line):
            current["recommendation"] = m["text"]

    totals.setdefault("topK", 5)  # logs from before `evaluate --top` existed always used 5
    for run in runs:
        completed = run["infrastructureFailure"] is None
        run["passed"] = completed and bool(run["criteria"]) and all(v == "PASS" for v in run["criteria"].values())
        run["sectionsRetrieved"] = sorted({c.split("#")[0] for turn in run["retrieved"] for c in turn})
    return {"log": Path(log_path).name, "totals": totals, "scenarioPasses": scenario_passes, "runs": runs}


def compare(a, b, label_a, label_b):
    def by_scenario(summary):
        out = {}
        for r in summary["runs"]:
            out.setdefault(r["scenario"], []).append(r)
        return out

    sa, sb = by_scenario(a), by_scenario(b)
    lines = [f"# Evaluate comparison: {label_a} vs {label_b}", ""]
    for label, s in ((label_a, a), (label_b, b)):
        t = s["totals"]
        retries = sum(r["jevRetries"] for r in s["runs"])
        lines.append(f"- **{label}** (top {t.get('topK')}): {t.get('passed')}/{t.get('completedRuns')} completed scenario-runs passed; "
                     f"{t.get('infrastructureFailures', 0)} infrastructure failures; {retries} Jev retries")
    lines += ["", f"| Scenario | Passed {label_a} | Passed {label_b} | Avg turns {label_a} | Avg turns {label_b} "
                  f"| Support {label_a} | Support {label_b} |", "|---|---|---|---|---|---|---|"]

    def stats(runs):
        done = [r for r in runs if r["infrastructureFailure"] is None]
        passed = sum(r["passed"] for r in done)
        turns = sum(r["turns"] or 0 for r in done) / len(done) if done else 0
        support = ", ".join(f"{k} {v}" for k, v in Counter(r["validatedSupport"] for r in done if r["validatedSupport"]).items())
        return f"{passed}/{len(done)}", f"{turns:.1f}", support or "—"

    for scenario in sorted(set(sa) | set(sb), key=lambda s: list(sa).index(s) if s in sa else 999):
        pa, ta, ua = stats(sa.get(scenario, []))
        pb, tb, ub = stats(sb.get(scenario, []))
        flag = " **←**" if pa != pb else ""
        lines.append(f"| {scenario}{flag} | {pa} | {pb} | {ta} | {tb} | {ua} | {ub} |")

    lines += ["", "## Criterion failures", "", f"| Criterion | {label_a} | {label_b} |", "|---|---|---|"]
    fa = Counter(n for r in a["runs"] for n, v in r["criteria"].items() if v == "FAIL")
    fb = Counter(n for r in b["runs"] for n, v in r["criteria"].items() if v == "FAIL")
    for name in sorted(set(fa) | set(fb)):
        lines.append(f"| {name} | {fa[name]} | {fb[name]} |")

    lines += ["", "## Sections only one side received (any run, any turn)", ""]
    for scenario in sa:
        ra = {s for r in sa[scenario] for s in r["sectionsRetrieved"]}
        rb = {s for r in sb.get(scenario, []) for s in r["sectionsRetrieved"]}
        if ra != rb:
            lines.append(f"- **{scenario}:** only {label_a}: {', '.join(sorted(ra - rb)) or '—'}; "
                         f"only {label_b}: {', '.join(sorted(rb - ra)) or '—'}")
    return "\n".join(lines)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")  # Windows consoles default to cp1252
    if len(sys.argv) == 4 and sys.argv[1] == "summarize":
        summary = summarize(sys.argv[2])
        Path(sys.argv[3]).write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")
        t = summary["totals"]
        print(f"{len(summary['runs'])} runs parsed; {t.get('passed')}/{t.get('completedRuns')} passed; "
              f"{t.get('infrastructureFailures', 0)} infrastructure failures")
    elif len(sys.argv) == 4 and sys.argv[1] == "compare":
        a, b = (json.loads(Path(p).read_text(encoding="utf-8")) for p in sys.argv[2:4])
        print(compare(a, b, Path(sys.argv[2]).parent.name, Path(sys.argv[3]).parent.name))
    else:
        sys.exit(__doc__)
