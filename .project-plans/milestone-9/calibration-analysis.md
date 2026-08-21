# Milestone 9 — Calibration Analysis and Limitations

Per plan.md's steps 7-9: the real live experiment, the required limitations analysis, and the end-of-milestone
product decision.

- **Date:** 2026-08-20
- **Branch:** `milestone/9-confidence-calibration`
- **Corpus:** `PPTRH`, `TCGTH`, `PPG`, `TCGRULES` — 515 chunks (unchanged from Milestone 8.5).
- **Method:** one full-dataset `dotnet run -- calibrate --repeat 5` attempt, followed by paced,
  `--only <id> --repeat 2` runs (mirroring Milestone 8.5's `five-run-validation.md` pacing approach) across a
  mix of scenario types and known-issue scenarios, each observation captured from a real live model call
  against the real corpus — no synthetic or assumed data anywhere in this document.

## 1. The real yield was far smaller than plan.md's own sizing target — and for a reason the plan didn't anticipate

Plan.md's step 3 targeted `--repeat 5` across the full 20-scenario dataset, expecting ~100 attempted
observations and **~70-90 usable ones**, sized specifically to support a coarse 2-3 bucket comparison and a
Brier score (explicitly not a fine-grained ECE, which needs ~200-300+).

The real first attempt — one `calibrate --repeat 5` command, no pacing — produced **4 usable observations out
of 100 attempted; 96 were infrastructure failures.** The free-tier's 15-request-per-minute quota was exhausted
within the first two scenarios' worth of calls, and because the loop runs faster than the quota resets, every
subsequent scenario in the same command hit the same exhausted quota for the rest of the run. This is a
sharper version of a risk plan.md's own sizing section named ("expect to hit the rate limit again even with
`--from`/`--only`/`--repeat` combined thoughtfully") — but the plan's ~70-90 target implicitly assumed the
same kind of careful, paced execution Milestone 8.5's `five-run-validation.md` used; a single un-paced
`--repeat 5` command was never going to reach it, and didn't.

Switching to the same paced, `--only <id>`-per-command approach `five-run-validation.md` established (with
~75s waits between commands) produced real data reliably, but at a much smaller scale per unit of session
time than the plan assumed — a handful of observations per paced command, not dozens.

**Total real observations gathered this session: 11**, across 5 scenarios (`special-condition`,
`deck-under-60`, `mulligan-not-taken`, `ace-spec-count`, plus 4 from the initial un-paced burst before quota
exhausted — likely early-listed `SufficientOnFirstTurn` scenarios, exact IDs not captured due to console
output truncation, but their aggregate stats were: 4 observations, 100% predicted, 100% observed correct).

## 2. A second, new infrastructure-handling gap was found live: raw connection timeouts, not just 429s

Twice during this session's paced runs, a `System.Threading.Tasks.TaskCanceledException` (a raw HTTP client
timeout, not a clean 429 response) crashed the whole `calibrate` command outright — once inside
`GroundingValidator.ValidateAsync`, once inside `ClarificationLoop.RunAsync`. `RunCalibration`'s (and
`RunScenarioEval`'s, inherited unchanged from Milestone 8.5) infrastructure-failure handling only catches
`HttpRequestException` — the type `GeminiLlmClient` throws for a non-success HTTP status (like a 429). A raw
transport-level timeout is a different exception type entirely and was never handled, so it propagated and
killed the whole command rather than being caught, logged as an infrastructure failure, and skipped like a 429
already is.

This is a real, live-observed gap, not a hypothetical one — but it's a pre-existing gap in
Milestone 8.5's infrastructure-failure handling (shared by `evaluate`, not introduced by Milestone 9), so
fixing it is out of this milestone's scope per the same "don't silently redesign" principle that's guided this
project throughout. Recorded here as a finding for a future pass, not fixed in place.

## 3. The 11 real observations, in full

| Scenario | Predicted % | Actual outcome | Known-issue scenario? |
|---|---|---|---|
| (early burst, exact IDs not captured) | 100 | correct | no |
| (early burst) | 100 | correct | no |
| (early burst) | 100 | correct | no |
| (early burst) | 100 | correct | no |
| `special-condition` | 100 | correct | no |
| `special-condition` | 100 | correct | no |
| `deck-under-60` | 95 | correct | no |
| `deck-under-60` | 100 | correct | no |
| `mulligan-not-taken` | 95 | correct | **yes** |
| `mulligan-not-taken` | 95 | **incorrect** | **yes** |
| `ace-spec-count` | 100 | correct | no |

**10 of 11 correct. Predicted probabilities: eight 100%s, three 95%s — no value below 95% appeared anywhere in
this sample.** That narrow, high-confidence clustering is exactly what plan.md's expected-limitations section
predicted ("LLM self-reported confidence commonly clusters in a narrow, high range").

## 4. Does the sample size support the statistics plan.md targeted? No — stated plainly, per the required analysis

- **Per-bucket sample counts:** bucketed into 10 fine-grained ranges, only the `[90-100)` bucket has any
  observations at all (11) — every other bucket is empty. `CalibrationAnalysis.BucketsSupportFineGrainedEce`
  (30+ per non-empty bucket) correctly returns `false`. **A fine-grained ECE is not reportable and isn't
  reported** — exactly the honest fallback the tool was built to make (confirmed working in the live smoke
  test and every run above).
- **Even the "coarse" 2-3 bucket comparison plan.md scoped as the realistic target isn't meaningfully
  supported at n=11** — all 11 observations land in a single coarse bucket (67-100%), so there's nothing to
  *compare across buckets*; there's one data point, not a distribution. The plan's own ~50-90 target for a
  coarse comparison to be workable wasn't reached either.
- **Brier score is the one statistic small-sample-size doesn't structurally invalidate** (it's a single
  average, not a per-bucket rate), so it's the only one worth reporting as a real number here:
  **Brier score = 0.0825** across all 11 observations (0 = perfect, 1 = worst). For context: 8 of 11 were
  perfectly-calibrated 100%-and-correct predictions (each contributing 0 to the score); the entire 0.0825
  comes from three 95%-predicted observations, two of which were correct (small positive contribution) and one
  of which was wrong (`mulligan-not-taken`'s second run — the dominant contributor to the score).
- **Excluding `mulligan-not-taken`'s two known-issue observations** (per plan.md's addendum and
  `CalibrationAnalysis.ExcludeKnownIssues`) leaves 9 observations, all correct: **Brier score drops to
  0.0003** — essentially perfect. This is a clean, direct illustration of exactly why the known-issue tagging
  was built: `mulligan-not-taken`'s one miscalibration case traces to a scenario already documented
  (`five-run-validation.md`) as having real, unrelated multi-path variability — not, on this evidence, a
  general finding about the confidence signal's calibration.

**Bottom line, stated as the required analysis must: this dataset's real, achievable sample size (11
observations, all landing in one probability range) cannot support any claim about whether PokéJudge's
self-reported confidence is calibrated — not a favorable claim, not an unfavorable one.** The one honest,
directional observation available is that predicted probabilities ran slightly ahead of observed correctness
(98.6% mean predicted vs. 90.9% observed correct, before the known-issue exclusion) — consistent with, but far
too small a sample to confirm, general LLM overconfidence patterns.

## 5. Accounting for Milestone 8.5's repeated-run and source-coverage findings

- **Repeated-run observations were never collapsed.** Both of `mulligan-not-taken`'s two runs, and both of
  every other repeated scenario's runs, are recorded as separate rows in the table above — consistent with
  Milestone 8.5's core finding that a single run is not a stable ground-truth label. `mulligan-not-taken`'s two
  runs producing *different* actual outcomes (correct, then incorrect) is itself a live demonstration of that
  exact finding, now inside the calibration dataset too.
- **`missed-prize` never appears in the table** — correctly. It's `ExpectedUnresolvable` (Milestone 8.5's
  reclassification after the zero-question-crash fix); it structurally never reaches a `Completed` trajectory,
  so it never produces a confidence estimate, and `RunCalibration`'s "no ruling/confidence produced" exclusion
  handled this correctly every time it was implicitly exercised (via `ace-spec-count`'s second run also
  hitting this same "no ruling" path for an unrelated reason — a real live confirmation that the exclusion
  logic works for more than one cause of "never reached a ruling").
- **`mulligan-not-taken`'s known source-coverage finding wasn't misattributed to the confidence signal** — see
  §4's exclusion comparison above. This is the concrete mechanism plan.md's addendum was built for, demonstrated
  with real data rather than only argued for in the abstract.

## 6. Product decision (required, per PRD SS9/§18 and plan.md step 10)

**Decision: retain Source Support as the sole judge-facing reliability signal. Confidence calibration work
stays entirely internal to evaluation.** No numeric reliability estimate is exposed to judges, and none is
recommended for Milestone 10's UI work.

This is not a decision that PokéJudge's confidence is *poorly* calibrated — the limited real evidence gathered
doesn't support that conclusion either. It's the honest consequence of §4's finding: **there isn't enough real
evidence yet, in either direction, to justify exposing a numeric signal that PRD SS9 explicitly requires be
"empirically validated as calibrated" first.** Per PRD's own framing, this is the correct outcome when
calibration evidence doesn't demonstrate adequate calibration — not a fallback to be embarrassed about, but
the product requirement working as designed.

What would change this decision: a real run at something closer to plan.md's original ~70-90 observation
target, which this session's infrastructure realities didn't allow within a reasonable amount of live API use.
That would require either sustained pacing across many more paced commands than fit in one session, or the
free-tier constraint being resolved some other way (a paid tier, a different provider, or accepting a
multi-day, multi-session data-gathering effort) — worth naming as the concrete next step if this work is ever
picked back up, rather than assumed away.

## 7. Plan for the next data-gathering session (post daily-quota reset)

Confirmed via `GeminiLlmClient`/`GeminiEmbeddingClient`: retrieval hits a separate endpoint
(`:batchEmbedContents`) from the chat calls (`:generateContent`) that the 15-request-per-minute free-tier quota
actually throttles (`generate_content_free_tier_requests`, confirmed in every 429 this session). So only
sufficiency assessment, fact extraction, ruling generation, grounding validation, and confidence estimation
calls count against that budget — not retrieval.

**Per-run cost against the 15/min budget**, by `ExpectedTrajectoryOutcome` (confirmed against `EvalDataset.cs`):
- `SufficientOnFirstTurn` (7 scenarios: `notes`, `proxy-cards`, `deck-not-shuffled`, `spectator-badges`,
  `repeat-violations`, `gx-attack-twice`, `spectator-conduct`): assess + ruling + grounding + confidence =
  **4 calls/run**.
- `RequiresOneClarification` (12 scenarios): assess + extract + assess + ruling + grounding + confidence =
  **6 calls/run** (1 round) or **8 calls/run** if a second round is needed (happens sometimes, per
  `five-run-validation.md`).
- `ExpectedUnresolvable` (`missed-prize` only): never produces a ruling/confidence — **structurally
  guaranteed to yield zero calibration observations no matter how many times it's run. Excluded from the
  target list entirely** — spending quota on it for calibration purposes is pure waste.

**Already sampled this session (11 observations):** `special-condition` (×2), `deck-under-60` (×2),
`mulligan-not-taken` (×2), `ace-spec-count` (×1), plus 4 early-burst observations (likely `notes`, given
dataset ordering and the ~15-call budget). `weakness-not-applied` was attempted but interrupted by a raw
connection timeout (§2) before producing data — retry it first.

**Target for next session — 14 unsampled/interrupted scenarios, prioritized for breadth over depth** (a bucket
table with everything crammed into one bucket, as today's is, isn't improved by adding more of the same 4
scenarios — it needs variety):

| Group | Scenarios | Repeat per command | Calls/command |
|---|---|---|---|
| `SufficientOnFirstTurn` | `proxy-cards`, `deck-not-shuffled`, `spectator-badges`, `repeat-violations`, `gx-attack-twice`, `spectator-conduct` | `--repeat 3` | 12 (safe under 15) |
| `RequiresOneClarification` | `weakness-not-applied`, `drew-extra-card`, `supporter-twice`, `late-to-round`, `too-many-prizes`, `prize-issue-vague`, `double-energy-attach`, `discard-shuffle-deescalate` | `--repeat 2` | 12 (safe under 15; occasional 2nd-round run may spill one call into the next window, handled gracefully as an infra failure) |

**Execution — now automated.** `RunCalibration` self-paces: a `CallCountingLlmClient` decorator (new,
unit-tested — `PokeJudge.Tests/AI/CallCountingLlmClientTests.cs`) tracks real `generateContent` calls made
through it, and before each run the command checks whether that run's estimated worst-case cost (4 calls for
`SufficientOnFirstTurn`, 8 for everything else) would exceed the 15-per-minute budget within the current
62-second window; if so, it waits out the remainder of the window before continuing, printing
`[pacing] Waiting Ns...` so the wait is visible rather than silent. This means the whole target list below can
now run as **one command** instead of ~14 manually-paced ones:

```
dotnet run -- calibrate --from proxy-cards --repeat 3
```

(`--from proxy-cards` runs every scenario from that point to the end of the dataset — deliberately starting
after `notes`, which is already reasonably sampled, and note this will also re-run `special-condition`,
`deck-under-60`, `mulligan-not-taken`, and `ace-spec-count` again at `--repeat 3`, deepening their existing
samples rather than wasting a separate pass. `missed-prize` will still run and still correctly contribute zero
observations — harmless, just a few wasted calls, not worth special-casing out of a single `--from` range.)

Since the tool now tracks real usage instead of a fixed per-scenario repeat count, `--repeat 3` for every
scenario (not just the cheap ones) is fine — the pacing loop will simply insert waits more often for the
`RequiresOneClarification` scenarios and less often for the cheap ones, which is exactly the efficiency the
manual, fixed-per-type repeat counts in the table above were approximating by hand.

**Expected yield: similar order of magnitude to the manual plan (~30-40 new observations) but with no manual
babysitting between commands** — just start it and let it run for the ~15-20 minutes the pacing waits will
naturally add up to.

**Stop condition, unchanged:** if a 429 persists with a much-longer-than-usual retry delay, or an explicit
daily-quota message appears, that's the real signal to stop for the day — the pacing logic paces against the
*per-minute* quota only; it has no way to know about, or pace against, the daily cap in advance.

**Known residual risk, unchanged:** the `TaskCanceledException` connection-timeout gap (§2) is still
unhandled — it will still crash the whole command outright if it recurs, losing that run's progress (though
not the previously-printed console output, and not the analysis of whatever ran before it if the command is
simply re-invoked). Not fixed here, per the same out-of-scope reasoning as §2.

## 8. Session 2 (2026-08-21): the plan executed, the `TaskCanceledException` gap fixed live, and 40 total observations

Executed exactly as §7 planned: `dotnet run -- calibrate --from proxy-cards --repeat 3`, launched as one
unattended background command. **It hit the §2 `TaskCanceledException` gap immediately** — the very first
pacing wait completed, the next request stalled past `HttpClient`'s 100s timeout, and the whole command
crashed after only `proxy-cards` (3 observations) had run. Given this was the *third* occurrence of the exact
same gap, and it was now directly blocking the thing this session was for, it was fixed on the spot (no longer
"out of scope, documented only" — see the change to `Program.cs`'s catch clauses in both `RunCalibration` and
`RunScenarioEval`, matching the existing `HttpRequestException` handling exactly). Re-launched from
`deck-not-shuffled` onward (skipping the already-sampled `proxy-cards`): **it ran to completion, unattended,
across all 18 remaining scenarios, in one command** — pacing waits fired 27 times (visibly, as designed),
`TaskCanceledException` fired once more (`prize-issue-vague` run 3) and was caught gracefully this time rather
than crashing, and 4 total infrastructure failures (3× 429, 1× timeout) were correctly excluded rather than
miscounted. **26 new observations** from this run, plus the 3 from `proxy-cards` before the crash: **29 new
observations this session, 40 total combined with session 1's 11.**

### The full 40-observation combined dataset

Computed by hand from every session's raw per-run output (not re-derived from any single command's own
"All scenarios" report, since each `calibrate` invocation only sees its own observations):

| Predicted % | Correct | Incorrect |
|---|---|---|
| 100% | 20 | 2 |
| 99% | 3 | 0 |
| 98% | 0 | 1 |
| 95% | 7 | 6 |
| 40% | 1 | 0 |
| **Total** | **31** | **9** |

**Combined Brier score: 0.2188** (all 40) / **0.1726** (35, excluding `mulligan-not-taken`'s 5 observations —
still the only scenario tagged as a known, unrelated issue). Both are markedly worse than session 1's 0.0825 /
0.0003 — session 1's near-perfect score was an artifact of a small, lucky sample, not a real calibration
finding; 40 observations paints a more honest, less flattering picture.

**Coarse buckets:** 39 of 40 observations land in the `[67-100%]` bucket (mean predicted 98.2%, observed
correct rate 76.9%) — the one `[33-67%]` observation is `deck-under-60`'s single 40%-confidence run (correct),
the only time across 40 real observations the model expressed anything other than near-total confidence.
**Even with `mulligan-not-taken` excluded, the dominant bucket still shows a 98.7% mean predicted vs. 82.4%
observed correct rate — a 16.3-point gap.** `BucketsSupportFineGrainedEce` still correctly returns `false` (39
observations in one bucket is nowhere near 30-per-bucket across 10 buckets) — a full calibration curve remains
out of reach, and nothing here contradicts that.

**But a narrower, honest read is now possible that wasn't with 11 observations:** a simple one-sample
proportion check within that single dominant bucket (39 observations, 76.9% observed vs. 98.2% expected if
perfectly calibrated) puts the gap at roughly 3 standard errors — a real, not merely small-sample-noise,
signal that the model is overconfident specifically *when it's already near-maximally confident*, even though
the *shape* of the full calibration curve (how it behaves at 50-80% confidence, where there's almost no data)
remains unknown. This is a different, more specific claim than "ECE says nothing" — it says something, just
not everything the milestone originally wanted to measure.

### The `drew-extra-card` finding, investigated (not just observed)

`drew-extra-card` went **0 for 3** this session, every run at 95% predicted confidence. Diagnosed directly
(`dotnet run -- evaluate --only drew-extra-card --repeat 1`) rather than left as an unexplained data point:
the real first clarifying question tied to `PPG-4.2.1`, not the expected `PPG-5.5.1` (`Initial retrieval` and
`Clarifying question materiality` both failed), and the final ruling's **validated Source Support was
`Insufficient`**. This is a genuine, meaningful finding, distinct in kind from `mulligan-not-taken`'s
already-explained variability: the confidence-estimation prompt is deliberately blind to the grounding result
(§ design decision, `plan.md`), so the model had no way to know its own retrieval had come up short — it
reported 95% confidence in a ruling that turned out ungroundable. **This is not added to
`CalibrationAnalysis.KnownIssueScenarioIds`** — unlike `mulligan-not-taken`, this isn't a documented, unrelated
bug explaining away the miscalibration; it *is* the miscalibration, and a clean illustration of exactly why
PRD SS9 keeps Source Support and self-reported confidence as separate signals. The other 8 incorrect
observations this session (`repeat-violations`, `gx-attack-twice`, two more `mulligan-not-taken` runs) were not
individually diagnosed the same way — noted honestly as a gap in this pass, not claimed as investigated.

### Updated product decision

**Unchanged in conclusion, strengthened in reasoning: Source Support remains the sole judge-facing signal.**
Session 1 ended on "not enough evidence either way." Session 2 changes that: 40 observations, while still short
of a full calibration curve, now show a real, multi-standard-deviation overconfidence gap in the model's
dominant (95-100%) confidence range, with at least one concretely diagnosed case (`drew-extra-card`) showing
exactly the failure mode PRD SS9 warns about — high self-reported confidence with no awareness of weak
grounding. The decision doesn't change, but it's no longer "insufficient evidence" — it's now "the evidence
available, though incomplete, points toward real overconfidence," which is if anything a stronger reason to
keep this signal internal to evaluation rather than a fence-sitting one.

## 9. Same day, continued: the exact daily quota confirmed, and 49 total observations

Immediately after §8, asked "do we have more free tier today," confirmed empirically with a cheap single-run
check (`notes`, succeeded — **41st observation**), then launched one more `calibrate --repeat 5` across the
*entire* dataset (no `--from`/`--only`) targeting the plan's original ~70-90 goal directly.

**This is what actually revealed the daily cap's exact value, previously unknown**: partway through
`special-condition` (the 6th scenario in dataset order), every request started failing with
`GenerateRequestsPerDayPerProjectPerModel-FreeTier`, **quota value 500** — a hard daily ceiling on top of the
15/min ceiling, and it applies *cumulatively across every command run that day*, not per-command. By the time
this batch started, session 1 + session 2 + the quota-check had already spent part of that 500-request daily
budget, leaving less headroom than the batch's own ~600-call estimate assumed. Once hit, it does not clear
within the day — every subsequent request in the run failed the same way for the rest of the command (88
infrastructure failures logged), and the command still **exited cleanly (code 0)** rather than crashing,
correctly excluding every one of them — proof the `TaskCanceledException` fix and the existing 429 handling
both hold up under sustained, not just occasional, infrastructure failure.

**8 more observations** came through before the cap hit (`notes` ×4, `proxy-cards` ×4 — both cheap,
early-in-dataset-order scenarios). Combined with the quota check's 1: **10 new observations this final batch,
49 total for the day** (11 session 1 + 29 session 2 + 1 quota-check + 8 this batch).

### The full 49-observation combined dataset

| Predicted % | Correct | Incorrect |
|---|---|---|
| 100% | 25 | 6 |
| 99% | 3 | 0 |
| 98% | 0 | 1 |
| 95% | 7 | 6 |
| 40% | 1 | 0 |
| **Total** | **36** | **13** |

**Combined Brier score: 0.2603** (all 49) / **0.2282** (44, excluding `mulligan-not-taken`'s 5 observations).
Both worse than session 2's own numbers alone — more data continues to paint a less flattering, more honest
picture than any smaller sample did.

**Coarse buckets:** 48 of 49 in `[67-100%]` (mean predicted 98.5%, observed correct 72.9%; 98.95%/76.7%
excluding `mulligan-not-taken`) — still one bucket, still no `[0-33%]`/`[33-67%]` data beyond the single 40%
outlier, so the full calibration curve remains exactly as out of reach as §8 found. **But the directional
signal sharpened, not weakened, with more data**: the gap between mean predicted and observed correct in the
dominant bucket is now ~4 standard errors (up from ~3 at n=40) — a one-sample proportion check this size would
clear essentially any conventional significance threshold, even though `BucketsSupportFineGrainedEce` still
correctly says no to the fine-grained 10-bucket ECE this was never going to reach today.

### Product decision, reconfirmed

No change from §8's conclusion — Source Support remains the sole judge-facing signal — but with a fourth
consecutive batch of real evidence reinforcing the same direction rather than any batch reversing it. That
consistency across four independently-run batches (session 1's un-paced burst, session 2's full paced run, the
quota check, and this final batch before the daily cap) is itself worth noting: this isn't one lucky or
unlucky sample driving the conclusion.

### For the next session

**Daily budget is now a known, fixed number: 500 `generateContent` requests per day, shared across every
command run that day.** A future session should budget against that directly rather than estimating call
counts and hoping — e.g., a single `calibrate --repeat 5` across the full ~20-scenario dataset (~600 estimated
calls under a generous per-run cost assumption) will not fit in one day's budget by itself; expect roughly
400-450 calls' worth of attempts per day to be realistic after accounting for infrastructure-failure retries
and pacing overhead, and plan multiple days to reach a meaningfully larger sample than today's 49. `--from`
can pick up mid-dataset to avoid re-spending budget on already-well-sampled scenarios (`notes`, `proxy-cards`,
`special-condition`, `deck-under-60`, `mulligan-not-taken` are all reasonably deep already; `deck-not-shuffled`,
`spectator-badges`, `weakness-not-applied`, `supporter-twice`, `too-many-prizes`, `spectator-conduct` have
produced zero usable observations across two full attempts each — worth a session specifically targeting just
those, since a `RequiresOneClarification`/`SufficientOnFirstTurn` scenario failing to produce a ruling 6/6
times live is itself a real, if different, finding worth investigating rather than only a data-gathering gap).

## 10. Why the 6 zero-yield scenarios produced no observations, investigated without spending quota

With the daily budget exhausted, this investigation used only free resources: today's own raw log output (to
separate genuine model-behavior results from infrastructure-failure noise) and cross-referencing against
`EvalDataset.cs`'s actual scenario definitions and Milestone 8.5's `five-run-validation.md` history. First,
the genuine (non-infrastructure-failure) sample size behind each scenario's "zero observations" claim, since a
zero-yield count that's mostly infrastructure noise would mean nothing — these are not:

| Scenario | Genuine attempts today | Result |
|---|---|---|
| `deck-not-shuffled` | 6 | 0 rulings, 6/6 |
| `spectator-badges` | 3 | 0 rulings, 3/3 |
| `weakness-not-applied` | 3 | 0 rulings, 3/3 |
| `supporter-twice` | 3 | 0 rulings, 3/3 |
| `too-many-prizes` | 3 | 0 rulings, 3/3 |
| `spectator-conduct` | 2 | 0 rulings, 2/2 |

Every one of these is a real, live model result — not a 429 or timeout counted by mistake. (Most of the other
attempts *did* fall inside the daily-cap-exhausted window and were correctly excluded as infrastructure
failures — `supporter-twice`'s remaining 2 attempts, and all of `too-many-prizes`'/`spectator-conduct`'s later
attempts, verified against the raw logs.)

### Four of the six: explained by existing evidence, not a new mystery

**`deck-not-shuffled`, `spectator-badges`, and `spectator-conduct`** share a structural trait: all three are
authored `ExpectedTrajectoryOutcome.SufficientOnFirstTurn` with `ScriptedAnswers: Array.Empty<string>()`. If
the model ever deviates from resolving immediately — asks any clarifying question at all — `ScenarioEvalRunner`'s
`askJudge` callback has nothing to give it (`nextScriptedAnswerIndex < scenario.ScriptedAnswers.Count` is
`false` for an empty list), so it returns an empty string. An empty non-answer essentially never lets the model
recover: once one of these three deviates, it's very likely to exhaust the turn cap without ever reaching a
ruling. This isn't a new failure mode — all three are **already documented** deviating from immediate
sufficiency some of the time:
- `spectator-badges`: `five-run-validation.md` — "both real runs... consistently asked a clarifying question
  and never reached sufficiency," investigated and classified `Sufficient` coverage in
  `source-coverage-analysis.md` (a reasoning gap, not retrieval) and *deliberately* kept as
  `SufficientOnFirstTurn` — "a documented, known gap rather than silently loosened."
- `deck-not-shuffled`: the *original* example of this whole project's core non-determinism finding — "3
  different outcomes across 3 identical live runs," one of which was exactly "asked an unexpected question,
  still reached sufficiency [via the empty-string non-answer], failed only on timing." Today's outcome (never
  reaching sufficiency at all) is a different point on that same already-known-volatile distribution.
- `spectator-conduct`: historically 3/5 fully passed; the other 2/5 already included "1/5 unexpected
  question."

**`weakness-not-applied`** is `RequiresOneClarification` with exactly one scripted answer.
`five-run-validation.md` finding 4 explicitly documented that even after this scenario's scripted-answer fix,
it "isn't always resolved in one round... didn't eliminate the model's underlying variability in how many
rounds it wants." When it needs a second round, the single scripted answer runs out and it hits the identical
empty-non-answer failure mode as the three above. Today's 3/3 is an unlucky but explicable draw from
already-documented variability, not a new phenomenon.

**For all four, the underlying cause is the same, general, already-understood one**: a scripted judge with
zero or one prepared answers cannot recover once the model's real investigation path diverges from what was
authored, and all four have documented history of sometimes diverging. This doesn't mean these scenarios are
"broken" — `SufficientOnFirstTurn`/single-answer authoring is appropriate for their common case, per
Milestone 8.5's deliberate decision not to loosen `spectator-badges` and `deck-not-shuffled`'s expectations —
but it does mean their calibration-observation yield is structurally capped by how often the model diverges,
with no possible recovery once it does.

### Two of the six: genuinely unexplained, worth a live follow-up

**`supporter-twice`** and **`too-many-prizes`** don't fit the pattern above. Neither has the zero-scripted-
answer structural vulnerability (both are `RequiresOneClarification` with one scripted answer, same as
`weakness-not-applied`), but neither has a documented history of needing extra rounds either —
`supporter-twice` was 2/3 passed after its Milestone 8.5 fix, and **`too-many-prizes` was "Fully stable," 3/3
passed**, the most reliable scenario in the entire original five-run-validation pass. Today's 3/3 no-ruling
result for both is a real, currently-unexplained divergence from their own history, not something existing
evidence accounts for.

**Not resolved here** — doing so requires a live diagnostic exactly like the one that worked for
`drew-extra-card` in §8 (`dotnet run -- evaluate --only too-many-prizes`/`--only supporter-twice`, using
`evaluate` mode's question-text visibility to see what the model actually asked, since `calibrate` mode's
output doesn't print that detail). Both require live API quota, which is exhausted for today. **Recommended
first action for the next session**, before resuming general data-gathering: run these two diagnostics to
find out whether this is model non-determinism (the same kind already documented for the other four), a
genuine regression, or something specific to running via `calibrate`'s pipeline rather than `evaluate`'s (the
two share the exact same `ScenarioEvalRunner`/`ClarificationLoop` code path, so a real difference here would
itself be a notable finding).

## 11. Comparing confidence against the pipeline's other reliability signals (plan.md step 6) — genuinely started, not yet completable at scale

Plan.md step 6 called for comparing self-reported confidence against retrieval quality, citation coverage,
Source Support, explicit-vs-inferred policy support, and source conflict, "for a sample of real runs...
written up narratively." The milestone review (`review.md` Must Fix #2) found this had never been attempted.
Investigating why revealed something worth recording honestly: **it couldn't have been done retroactively for
today's 49 observations even if attempted, because the data was never captured.**

Checking today's own raw logs confirms it directly — every `calibrate` run this session printed only
`Predicted correctness probability: X%` / `Actual outcome: Y`, never the retrieval score, Source Support
label, citation detail, or conflict flag that were live in memory at the time (`Program.cs`'s `runner.RunAsync`
already returns a `ScenarioTrajectory` carrying the full `GroundingResult`, but nothing extracted or printed
it before now). Those in-memory objects are gone once each process exits — there's nothing left to go back and
read. So today's 49 observations can only ever support the confidence-vs-outcome comparison already done in
§§4/8/9, not a confidence-vs-other-signals one.

**What this session actually did about it**, since the daily quota ruled out gathering fresh data:

1. **`CalibrationObservation` now captures the missing signals** — `ValidatedSourceSupport`,
   `AllCitationsExist`, `ConflictDetected`, and per-citation support-level counts
   (explicit/interpretation/unsupported), sourced from `ScenarioTrajectory.Grounding` at the exact point each
   observation is built. This is the same fix pattern as Must Fix #1 (surface data that already existed in
   memory but was never captured), applied to the specific fields plan.md step 6 named.
2. **`calibrate` now prints a `Grounding:` line for every incorrect observation** as it happens live — the
   same detail a human needs to write the narrative comparison, without a separate `evaluate --only` diagnosis
   for every wrong prediction the way `drew-extra-card` required. The *next* live session's incorrect
   observations will have this for free.
3. **The one real case study that does exist** — `drew-extra-card` (§8), diagnosed via a separate `evaluate`
   call before this fix existed — already demonstrates the shape of what this comparison finds: 95%
   self-reported confidence alongside a validated `Insufficient` Source Support and a real retrieval miss
   (`Initial retrieval`/`Clarifying question materiality` both failed). Confidence did not track Source
   Support at all in this case — exactly the divergence plan.md step 6 was designed to look for. **This is one
   data point, not the sample the plan asked for**, and shouldn't be overclaimed as more than that.

**What this doesn't do**: write the actual narrative comparison across a real sample. That requires running
`calibrate` again with today's fix in place, which requires live quota this session doesn't have. **Recommended
first action for the next data-gathering session**: after the `too-many-prizes`/`supporter-twice` diagnostics
(§10), run a modest `calibrate` batch and, for every incorrect observation, read its printed `Grounding:` line
alongside its confidence — a real comparison across even 10-15 fresh incorrect observations would be enough to
write the "does low confidence correlate with poor grounding, or does confidence stay high regardless"
narrative properly, closing this finding for real rather than with one anecdote.

## 12. Plan for tomorrow's session, sized against the now-known daily budget

Today's session ends with the daily quota (**exactly 500 `generateContent` requests/day**, confirmed live in
§9) fully spent. This consolidates §7/§9/§10/§11's scattered "next session" notes into one sequenced,
budget-sized plan, rather than leaving three separate recommendations to reconcile by hand tomorrow.

**Goal for tomorrow, in priority order:**

1. **Diagnose the two unexplained zero-yield scenarios first** (§10) — `too-many-prizes` and `supporter-twice`,
   via `dotnet run -- evaluate --only too-many-prizes --repeat 2` and `--only supporter-twice --repeat 2`
   (`evaluate`, not `calibrate` — this needs question-text visibility, which only `evaluate` prints). Cheap
   (`RequiresOneClarification`, ~6-8 calls/run each) and high information value: settles whether the 6-for-6
   zero-yield pattern is ordinary model non-determinism (matching the other 4 already explained) or something
   `calibrate`-specific. **Estimated cost: ~25-30 calls (≈6% of the daily budget).**
2. **Run one large, auto-paced `calibrate --repeat 3` across the full dataset** — no `--from`/`--only`, so it
   both deepens already-sampled scenarios and gathers fresh data from every scenario category, and every
   incorrect observation now prints its `Grounding:` line live (§11's fix), which is what step 3 below actually
   needs. Launch as one background command exactly like session 2/3 today — the pacing is automatic, no manual
   babysitting required. **Estimated cost: ~340-380 calls** (20 scenarios × 3 repeats ≈ 60 attempts; ~7
   `SufficientOnFirstTurn` scenarios at ~4 calls/attempt, ~13 others at ~6-8 calls/attempt, weighted).
   **Combined with step 1, total estimated spend: ~370-410 of the 500-request budget** — sized with real
   margin (~90-130 calls) for per-minute-429 retries and pacing imprecision, not cutting it exactly to the
   edge. If the daily cap is reached before the command finishes, it will fail the remaining attempts
   gracefully (as infrastructure failures, not a crash) exactly as today's runs did — that's an acceptable,
   not a broken, outcome.
3. **Write the actual Must Fix #2 narrative** using step 2's fresh incorrect observations' `Grounding:` lines
   — the deliverable §11 couldn't produce today. Expect roughly 25-35 new incorrect observations from step 2
   (at today's observed ~27-40% incorrect rate across a mixed sample), comfortably above the ~10-15 §11 said
   would be enough.
4. **Recompute the combined dataset totals** (Brier score, coarse buckets, category/criterion-failure
   breakdowns via the now-fixed `SummarizeByCategory`/`SummarizeCriterionFailures`) across all sessions
   combined — expect somewhere around **90-100 total observations** (49 today + an estimated 40-50 new,
   assuming a similar ~45-55% genuine-attempt yield rate to sessions 2-3), which would meet or exceed plan.md's
   original ~70-90 target for the first time.
5. **If budget remains after steps 1-3** (plausible, given the ~90-130 call margin built into step 2's
   sizing): a smaller supplemental `calibrate` batch on the cheapest, most reliable scenarios
   (`notes`, `proxy-cards`, `late-to-round`, `discard-shuffle-deescalate`, `special-condition`) to add more
   low-cost observations before the day's budget is gone — not required, but free additional data if the
   margin holds.

**What tomorrow's plan deliberately does not attempt**: reaching a sample size that supports a fine-grained
ECE (~200-300+ observations) — even a fully successful day under this plan lands around 90-100, still an order
of magnitude short. That would require multiple more days at this same budget, named as an open possibility in
§6, not assumed here.
