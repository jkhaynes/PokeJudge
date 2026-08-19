# Milestone 8 — Observed Limitations & Failures (Real-Data Run)

Captured per the plan's step 6: concrete evidence from running `dotnet run -- evaluate` against the real,
expanded corpus. Three separate full-harness attempts were made; none completed all 8 scenarios in one run
(see §1) — the results below are compiled across all three, plus one targeted `--only` run that finally
reached the 8th scenario, since re-running the identical scenario multiple times turned out to be far more
informative than a single clean pass would have been.

**Update:** added `--from <scenario-id>` / `--only <scenario-id>` to `dotnet run -- evaluate` after the three
full-run attempts below, specifically to reach the 8th scenario without re-burning quota on the other seven.
`dotnet run -- evaluate --only drew-extra-card` reached it immediately (see §7) — direct confirmation the flag
solves the exact problem §1 documents. This is developer/CI-facing tooling only; the judge-facing product
never makes more than one request at a time, so no equivalent was needed (or added) to the shared LLM client.

- **Date:** 2026-08-18
- **Branch:** `milestone/8-evaluation`
- **Corpus:** `PPTRH`, `TCGTH`, `PPG`, `TCGRULES` -- 515 chunks (unchanged from Milestone 7).

## 1. The free-tier chat-completion rate limit is a real, hard constraint on running the full harness

`gemini-3.5-flash-lite`'s free tier allows 15 `generate_content` requests per minute. A full 8-scenario run
needs roughly 25-30+ chat calls (single-turn scenarios use ~3: sufficiency, ruling, grounding; branching
scenarios use ~5: two sufficiency assessments, fact extraction, ruling, grounding). Every one of three full
attempts hit a 429 partway through, even after waiting 65-90 seconds between attempts — once even after a
90-second wait, the very next scenario's first call still failed. Milestone 4's embedding-tier rate limit
(100/minute) was already known and designed around (resumable chunk/embed progress); this milestone's
chat-completion limit is a *tighter* constraint that the harness has no equivalent resumability for --
`RunScenarioEval` re-runs every scenario from the start each time, burning quota on already-passing scenarios
before reaching new ones. The 8th scenario (`drew-extra-card`) was never reached in any of the three attempts
as a direct result. This is a real, previously-unquantified cost of moving from "one manual scenario at a
time" (Milestones 6-7's validation style) to "run a dataset," worth carrying forward, not smoothed over.

## 2. Real non-determinism in the model's own sufficiency-assessment behavior, run to run

The most important finding this milestone surfaced: running the *identical* scenario multiple times does not
reliably produce the same trajectory.

**`deck-not-shuffled`** ("A player's deck wasn't fully shuffled before the game started -- what should
happen?") behaved three different ways across three runs:
- Run 1: asked one clarifying question, then the second turn's assessment returned `isSufficient: false`
  with zero questions -- the exact malformed-response crash Milestone 2's guard exists for.
- Run 2: crashed identically to Run 1.
- Run 3: asked one clarifying question, received an empty scripted answer (this scenario has no
  `ScriptedAnswer` -- it was authored expecting immediate sufficiency), and *still* reached sufficiency after
  3 turns, landing on **Strong** (validated and self-assessed agreed).

**`special-condition`** (the Milestones 6-7 scenario, always observed needing exactly one clarifying round in
every prior manual run) resolved with **zero clarifying questions** on one live run here -- a materially
different trajectory than every previous observation of this exact scenario. See §3 for why this is actually
the most useful finding in this milestone, not just noise to explain away.

This means a single eval run's pass/fail result is not, on its own, a stable signal -- the same scenario can
look like a clean pass, a clean fail, or something in between depending on which run you happened to catch.
This is worth carrying directly into Milestone 9: if model behavior itself is this variable run-to-run,
that's a real complication for interpreting any calibration statistic built on top of a small, one-shot eval
dataset.

## 3. A real, unplanned demonstration of why trajectory evaluation matters

The `special-condition` run described above is a near-perfect natural example of PRD §15's core argument.
Scored on the final answer alone, this run looks fine: validated Source Support was **Strong**, which is
inside the scenario's accepted `{Strong, Partial}` set -- a pass. But `ScenarioEvalScorer` also scored the
*investigation*, and three criteria correctly failed:

- **Sufficiency timing: FAIL** -- the scenario expected a clarifying question on turn 1 (matching every prior
  real observation of this scenario); this run treated it as immediately sufficient instead.
- **Clarifying question materiality: FAIL** -- there was no clarifying question to check at all.
- **Post-answer retrieval: FAIL** -- there was no second turn to check retrieval on.

A destination-only eval would have called this run a success. Trajectory scoring correctly flagged that the
investigation this run actually took diverged from what the scenario expects a correct investigation to look
like -- exactly PRD §15's "a lucky correct ruling reached via a wrong investigation path...should not
automatically receive full credit" principle, observed for real rather than only designed for.

## 4. `spectator-badges` is the one stable, reproducible finding among the noise

Unlike `deck-not-shuffled` and `special-condition`, **`spectator-badges`** ("Do spectators need to wear a
badge at large tournaments?") failed the *same* way in both runs that reached it: it asked a clarifying
question, never reached sufficiency within the 4-turn cap, and no ruling was ever produced. This scenario was
reused from Milestone 5's `RetrievalEvalSet`, which only verified its *retrieval* hit rate (rank 1 against
`PPTRH-2.4`) -- this milestone is the first time its full-pipeline sufficiency behavior was actually observed,
and the `SufficientOnFirstTurn` expectation this scenario was authored with turned out to be wrong,
consistently. Retrieval finding the right section clearly isn't sufficient by itself for the model to treat a
seemingly-simple procedural question as resolvable -- worth investigating further (is the retrieved passage
actually specific enough, or does the model want detail the passage doesn't state?) rather than assumed to be
a quick fix.

## 5. A real scorer bug, found by running the harness rather than by unit testing

`ScenarioEvalScorer.Score`'s first version only special-cased `ExpectedToFailLoudly` scenarios for a crash;
a scenario expecting `SufficientOnFirstTurn` or `RequiresOneClarification` that crashed *unexpectedly* (as
`deck-not-shuffled` did in Run 1) fell through to the normal scoring path and produced a misleading report --
"Sufficiency timing: FAIL, a clarifying question was asked" -- when the real story was a crash, not merely an
unexpected question. This was not caught by `ScenarioEvalScorerTests` before the live run, because no test
had constructed exactly that combination (a non-`ExpectedToFailLoudly` scenario with `ThrewExpectedFailure:
true`). Fixed with an explicit "Unexpected failure" criterion checked before any of the outcome-specific
scoring, and a regression test added directly from this observation. A concrete instance of this project's
`Build → Observe → Understand → Improve` loop working as intended -- the bug was findable in principle by
more exhaustive interaction-testing of the scorer's inputs, but was actually found by running the real thing.

## 6. `drew-extra-card`, finally reached via `--only`, reproduces the same malformed-response crash a third time

After adding `--from`/`--only` specifically to reach the scenario the three full runs never got to,
`dotnet run -- evaluate --only drew-extra-card` reached it on the first attempt (a single-scenario run stays
well under the rate limit). Result: it crashed on turn 1 with the identical `isSufficient: false` /
zero-questions malformed response as `missed-prize` and (in two of three attempts) `deck-not-shuffled` --
despite `PPG-5.5.1` being confirmed, via `dotnet run -- search`, to directly cover "a competitor draws an
extra card" as a worked example before this scenario was authored. This is now the *third* distinct scenario
category (prize errors, illegal game state, gameplay error) to independently reproduce the same failure mode
first documented in Milestone 6 -- meaningfully stronger evidence that this is a systemic weakness in the
sufficiency-assessment step's ability to articulate a clarifying question from a specific missing fact, not an
artifact of any one scenario's phrasing or retrieval quality.

## 7. What this suggests for Milestone 9

- Model behavior's run-to-run variability (§2) is a real complication for calibration work: a "correct" or
  "incorrect" label attached to one observed run may not generalize to what the same scenario does on a
  different run. Milestone 9's required limitations analysis should account for this, not just dataset size.
- The `special-condition` divergence (§3) reinforces Milestone 7's own finding (`grounding-analysis.md`) that
  agreement between two signals (here: final-answer correctness and trajectory correctness) is not itself
  proof either signal is right -- worth remembering when combining Source Support with other reliability
  signals in Milestone 9's experiments.
- Three independent scenarios now reproducing the same sufficiency-assessment crash (§6) makes this a strong
  candidate for whichever future milestone next revisits that prompt -- no longer a single anecdote.
