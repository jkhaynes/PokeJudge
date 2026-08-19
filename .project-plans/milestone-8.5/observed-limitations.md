# Milestone 8.5 — Observed Limitations & Findings (Real-Data Runs)

Captured per the plan's step 8: concrete evidence from running `dotnet run -- evaluate` against the real,
expanded (20-scenario) corpus during implementation.

- **Date:** 2026-08-19
- **Branch:** `milestone/8.5-eval-dataset-hardening`
- **Corpus:** `PPTRH`, `TCGTH`, `PPG`, `TCGRULES` -- 515 chunks (unchanged from Milestones 7-8).

## 1. `--repeat` immediately reproduced `deck-not-shuffled`'s known non-determinism, live

`dotnet run -- evaluate --only deck-not-shuffled --repeat 3` produced three different outcomes across three
back-to-back runs: run 1 failed loudly (the known insufficient-with-zero-questions crash), run 2 reached
sufficiency cleanly (validated Strong), run 3 failed loudly again. Result: `1/3 runs fully passed`. This is
the exact pattern Milestone 8's `observed-limitations.md` documented across three separate manual sessions --
here it reproduced within a single command, with each run separately visible rather than requiring three
distinct developer-initiated attempts. Directly confirms the feature does what it was built for: making
run-to-run variability observable instead of hidden behind whichever single run happened to occur.

## 2. The infrastructure-failure handling was validated for real, not simulated, within minutes of being built

A `--from weakness-not-applied` run (selecting the 12 newest scenarios) hit the free-tier 15-req/min quota
immediately -- residual quota consumption from the `--repeat 3` run and an earlier single-scenario run left
almost nothing available. All 12 scenarios in that run received `HttpRequestException` (429) responses. Every
one was caught, printed as `[INFRASTRUCTURE FAILURE -- not counted]`, and the command continued to the next
scenario rather than crashing outright -- the harness survived a *worse* rate-limit hit than any single
Milestone 8 run encountered, and correctly reported `Result: 0/0` plus `Infrastructure failures (not counted
above): 12` rather than either crashing or silently recording 12 scenario failures. This is a stronger,
accidental stress-test of the feature than a deliberately engineered one would have been.

## 3. Two new `RequiresOneClarification` scenarios needed more than their single scripted answer — ✅ FIXED

~~Both `weakness-not-applied` and `supporter-twice` -- run individually, with the rate limit given time to
reset between them -- exhausted the 4-turn cap after asking for more than the one scripted answer provided
(`Asked more questions than scripted: True` in both cases), and consequently produced no ruling to score
Final Source Support against. Both scenarios' initial retrieval and sufficiency timing were correct; the gap
is specifically in `Clarifying question materiality` and `Answer budget`. This is an honest finding about
these two new scenarios' scripting, not a scored pass being forced: procedural/rules-interaction questions
(damage-calculation correctness, Supporter-card timing) may prompt the model to ask for more granular
confirmation across multiple rounds than the simpler game-state fact-gathering the original 8 scenarios were
built around (e.g. `special-condition`'s single-fact clarification). Left as-is rather than loosened to force
a pass -- if this pattern holds when re-run, it's worth revisiting whether these two scenarios' scripted
answers should anticipate a second round, or whether `RequiresOneClarification`'s single-answer assumption is
itself too narrow for procedural-timing questions specifically. Not resolved here; recorded as a genuine open
question for whoever next touches this part of the dataset.~~

**Fixed, and the real diagnosis turned out different from the hypothesis above.** `EvalScenario.ScriptedAnswer`
was generalized to `ScriptedAnswers` (an ordered list), and `Program.cs`'s eval-mode output was extended to
print each turn's actual clarifying-question text (previously eval mode was silent about this; interactive
mode already printed it). Re-running both scenarios with that visibility showed the model was **not** asking
a sequence of different follow-up questions -- it asked the *same* question, reworded, on every turn:
`supporter-twice` asked "which specific Supporter cards were played" four times; `weakness-not-applied` asked
"was the Defending Pokemon Active or Benched" four times. Neither original scripted answer ever addressed the
fact actually being asked for, so the model never had anything new to work with. The fix was correcting each
scenario's *single* scripted answer to actually answer the real question (naming the Supporter card;
specifying the Defending Pokemon's position), not adding a second round -- the multi-answer capability itself
was still worth building (it's now unit-tested and available for a genuinely multi-fact scenario), but wasn't
what these two specific scenarios needed. Both also revealed their real first clarifying question ties to a
different section than originally guessed (`supporter-twice` → `PPG-4.2.1`, not just `TCGRULES-turn-actions`;
`weakness-not-applied` → `TCGRULES-turn-actions`, not just `TCGRULES-full-details-of-attacking`) -- both
sections were added to `ExpectedMaterialSectionIds` based on this direct observation, not a guess. Re-run
after the fix: both scenarios now reach sufficiency in 2 turns and pass all 6 applicable criteria. Regression
check on `drew-extra-card` (an already-working single-answer scenario) confirmed no behavior change -- it
still reproduces the known, unrelated zero-questions crash exactly as before.

## 4. `gx-attack-twice`, a new `SufficientOnFirstTurn` scenario, passed cleanly on its first live run

Confirms the newly-verified `TCGRULES-appendix-19-pok-mon-gx` section is both retrievable and sufficient on
its own for a straightforward, fully-explicit rule -- a useful contrast against finding 3 above, since it
shows the harness and new dataset entries work correctly end-to-end when a scenario's material fact set is
genuinely complete from turn one.

## 5. What this suggests going forward

- The `--repeat` and infrastructure-failure features are both now validated against real, not simulated,
  conditions -- including a rate-limit event significantly larger than anything Milestone 8 observed.
- Finding 3's *symptom* (turn-cap exhaustion, no ruling produced) looked like a new failure shape, but its
  real cause was mundane: an incomplete scripted answer, not a structural limit on how many rounds a scenario
  can need. Worth remembering when diagnosing a future eval failure -- printing the actual question text
  before theorizing about root cause would have shortened this investigation considerably.
- Source-coverage findings (`source-coverage-analysis.md`) show 3 of 4 investigated scenarios are Sufficient
  coverage -- reinforcing that this dataset's known failures are dominated by a reasoning/sufficiency-
  assessment weakness, not a retrieval or corpus problem, consistent with Milestone 8's own conclusion but now
  backed by explicit, scenario-by-scenario source verification rather than inference.
- `ExpectedMaterialSectionIds` for both fixed scenarios needed a second, real section added once the model's
  actual first-question target was observed directly -- a reminder that guessing which section a clarifying
  question will target, without seeing a live run first, is exactly the kind of assumption this dataset's own
  practice (verify via `dotnet run -- search`/`evaluate` before trusting an authored expectation) exists to
  catch.
