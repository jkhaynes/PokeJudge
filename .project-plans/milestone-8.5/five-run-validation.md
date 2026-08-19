# Milestone 8.5 — Five-Run Validation of the Full Dataset

Every scenario in the expanded, 20-scenario dataset run 5 times each (`dotnet run -- evaluate --only <id>
--repeat 5`), one scenario at a time, against the real corpus and live model — not a simulation, and not a
single spot-check. Purpose: directly exercise the `--repeat` feature this milestone added across the *entire*
dataset, and see whether the patterns observed in smaller, earlier samples (Milestone 8's `observed-limitations.md`,
this milestone's own earlier findings) hold up, sharpen, or change under more data.

- **Date:** 2026-08-19
- **Branch:** `milestone/8.5-eval-dataset-hardening`
- **Corpus:** `PPTRH`, `TCGTH`, `PPG`, `TCGRULES` — 515 chunks.
- **Method:** each scenario run individually via `--only <id> --repeat 5`, with a ~90s pause before each
  command to partially clear the free-tier 15-req/min quota. Even with that pacing, several commands still hit
  the rate limit partway through — those runs are reported honestly as infrastructure failures below, not
  padded out with re-runs to force exactly 5 real data points everywhere. **74 of 100 attempted runs
  completed for real; 26 hit the rate limit and were correctly excluded from scoring** (per this milestone's
  own infrastructure-failure handling) rather than miscounted as PokéJudge failures or crashing the run.

## Summary table

| Scenario | Category | Real runs | Fully passed | Infra failures | Pattern |
|---|---|---|---|---|---|
| `notes` | Tournament Procedure | 5/5 | 5/5 | 0 | Fully stable |
| `proxy-cards` | Deck/Decklist Issues | 5/5† | 5/5† | 0† | Fully stable (after one retry — see note) |
| `deck-not-shuffled` | Illegal Game State | 2/5 | 0/2 | 3 | **New**: both real runs asked an unexpected clarifying question, still reached sufficiency, failed "Sufficiency timing" only |
| `spectator-badges` | Tournament Procedure | 2/5 | 0/2 | 3 | Consistent with known gap — both exhausted turn cap |
| `repeat-violations` | Penalty Questions | 5/5 | 5/5 | 0 | Fully stable |
| `special-condition` | Discretion Required | 4/5 | 1/4 | 1 | **Major finding** — see below |
| `missed-prize` | Prize Errors | 5/5 | 5/5 | 0 | Fully stable (expected-failure regression check) |
| `drew-extra-card` | Gameplay Error | 5/5 | 0/5 | 0 | 4/5 known crash; 1/5 new retrieval miss |
| `weakness-not-applied` | Attack Resolution | 2/5 | 0/2 | 3 | Both reached sufficiency but needed a 2nd round |
| `supporter-twice` | Timing Questions | 3/5 | 2/3 | 2 | Process criteria stable; Source Support varied |
| `gx-attack-twice` | Timing Questions | 5/5 | 5/5 | 0 | Fully stable |
| `mulligan-not-taken` | Illegal Game State | 5/5 | 0/5 | 0 | **New**: known crash, 5/5 consistent |
| `late-to-round` | Tournament Procedure | 2/5 | 0/2 | 3 | **New**: both needed more than the scripted answer |
| `deck-under-60` | Deck/Decklist Issues | 3/5 | 0/3 | 2 | **New**: 2 retrieval misses + 1 known crash |
| `ace-spec-count` | Deck/Decklist Issues | 2/5 | 0/2 | 3 | **New**: both asked an unexpected question, never resolved |
| `too-many-prizes` | Prize Errors | 3/5 | 3/3 | 2 | Fully stable |
| `prize-issue-vague` | Prize Errors | 5/5 | 4/5 | 0 | Stable; 1/5 needed a 2nd round |
| `double-energy-attach` | Gameplay Error | 3/5 | 2/3 | 2 | Process stable; Source Support varied once |
| `discard-shuffle-deescalate` | Penalty Questions | 3/5 | 3/3 | 2 | Fully stable |
| `spectator-conduct` | Tournament Procedure | 5/5 | 3/5 | 0 | **New**: 1/5 known crash, 1/5 unexpected question |

† `proxy-cards`'s first attempt hit the rate limit on all 5 runs (residual quota from the prior scenario, no
wait beforehand) — an early, honest illustration of why pacing matters, not hidden from this report. The
retry after a 90s wait produced the clean 5/5 shown above.

## Key findings

### 1. The `special-condition` "divergence" is not rare — in this sample, it's the majority behavior

Milestone 8's `observed-limitations.md` treated `special-condition` resolving with zero clarifying questions
as a single, notable divergence against "every prior manual observation." Across 4 real runs here, **3 of 4
resolved with zero clarification** (immediately "sufficient," no question asked, no second turn) — only 1 of
4 matched the originally authored expectation (one clarifying round, tied to `TCGRULES-special-conditions`).
Final Source Support still frequently validated inside the acceptable set even on the zero-clarification runs
(Partial, Strong, Strong across the three), which is exactly the PRD §15 trajectory-evaluation argument this
scenario already made once, now with much stronger support: a destination-only eval would have called 3 of
these 4 runs clean passes; trajectory scoring correctly failed 3 of 4 on process criteria (`Sufficiency
timing`, `Clarifying question materiality`, `Post-answer retrieval`) despite an acceptable final answer. This
changes the interpretation from "a documented edge case" to "the model's typical behavior for this scenario
doesn't match the scripted investigation path most of the time." Worth real consideration before Milestone 9:
is `RequiresOneClarification` still the right expectation for `special-condition`, or does the evidence now
point toward treating zero-clarification-but-correct-answer as the *normal* case?

### 2. Five, not three, scenario categories now reproduce the known "insufficient with zero questions" crash

Beyond the three Milestone 8 already documented (`missed-prize` as its expected-failure regression check,
plus `drew-extra-card` and `deck-not-shuffled` reproducing it unexpectedly), this run adds two more real,
independent reproductions: `mulligan-not-taken` (5/5 consistently) and `spectator-conduct` (1/5). That's now
gameplay error, illegal game state (two different scenarios), prize errors, tournament procedure, and setup
violations all hitting the identical failure mode. The "systemic weakness in the sufficiency-assessment step"
conclusion from Milestone 8's `observed-limitations.md` §6 is substantially reinforced, not just repeated.

### 3. Two more scenarios show the exact "answer didn't address what was asked" pattern already fixed once

`late-to-round` consistently (2/2 real runs) needed more than its single scripted answer — the same shape of
problem `weakness-not-applied` and `supporter-twice` had before being fixed earlier this session. This wasn't
fixed here (out of scope for "run and document," per the request), but it's the same diagnosable issue —
worth a follow-up pass with eval mode's question-text printing to see exactly what's being asked, the same way
the other two were resolved.

### 4. Even after the fix, `weakness-not-applied` isn't *always* resolved in one round

Both real runs here needed a second round despite the corrected scripted answer (which passed cleanly in the
single validation run done right after the fix). This is a useful, humbling data point about the fix itself:
it made the scenario *more likely* to resolve in one round and *able to* reach sufficiency even when it
doesn't, but it didn't eliminate the model's underlying variability in how many rounds it wants. Consistent
with this project's broader finding (`deck-not-shuffled`, `special-condition`) that single-run validation
understates real variability — exactly why this exercise (5x, not 1x) was worth doing.

### 5. `ace-spec-count`'s `SufficientOnFirstTurn` expectation looks wrong, based on real evidence — ✅ ADDRESSED

~~Authored as fully explicit and unconditional (the rule text has no ambiguity — a deck can include only one
total ACE SPEC card). Both real runs asked a clarifying question anyway and never reached sufficiency within
the turn cap. This weakens the "explicit rule text guarantees `SufficientOnFirstTurn` behavior" assumption
used to author it (and `gx-attack-twice`, which by contrast passed 5/5) — worth investigating via the same
source-coverage classification process used for `spectator-badges`, not assumed to be a simple authoring
mistake.~~

**Investigated with the question-text visibility added earlier this session, across three iterations.**
Attempt 1: reworded "appears to include" to a confirmed fact — the model then asked whether this was a
decklist violation or a decklist/physical-deck mismatch (the same distinction found for `deck-under-60`,
below). Attempt 2: stated explicitly that the decklist itself was illegal on its face — the model then asked
whether the decklist was reviewed before opening hands were drawn, tied to `TCGTH-3.3.1`'s review-timing
procedure, a genuinely different question neither prior wording had addressed. Two different, real follow-up
questions across two fixes is itself evidence: this scenario's topic is more procedurally entangled than a
clean zero-clarification case, not a wording problem worth a third guess. Reclassified to
`RequiresOneClarification` with a scripted answer addressing both points directly (decklist-level violation,
confirmed before opening hands were drawn); `gx-attack-twice` remains the dataset's clean `SufficientOnFirstTurn`
contrast case. **Not yet re-verified live** — the session's free-tier quota was exhausted at the *daily* limit
(`GenerateRequestsPerDayPerProjectPerModel-FreeTier`), not just the per-minute one, partway through
verification. Build and the full deterministic test suite (214/214) are unaffected; live confirmation is
pending quota reset.

### 6. `deck-under-60` shows a genuine retrieval miss, not just a reasoning problem — partially addressed

Two of three real runs failed `Initial retrieval` — `TCGRULES-deck-building` was not among the top results on
turn 1, despite being the section that directly and explicitly states the 60-card requirement. This is
different in kind from every other new finding here (which are reasoning/sufficiency-assessment issues, not
retrieval issues) and remains a genuine Retrieval-problem candidate for the source-coverage classification
process, not yet added to `source-coverage-analysis.md`'s four investigated scenarios — the underlying
retrieval-quality question (why `TCGRULES-deck-building` sometimes doesn't surface for this query) is not
addressed by anything below and stays open.

**Scripting fix applied, not yet re-verified live**, independent of the retrieval question above: the same
diagnostic pass showed the real first clarifying question consistently ties to `PPG-5.6.1` (the penalty
section), not `TCGRULES-deck-building` alone — the identical "real question targets the penalty section"
pattern already found for `weakness-not-applied`/`supporter-twice` — and the original scripted answer never
specified whether the shortfall was in the decklist or the physical deck, which a follow-up question asked
directly. Fixed by adding `PPG-5.6.1` to `ExpectedMaterialSectionIds` and specifying both decklist and
physical deck explicitly in the scripted answer. This also makes `Initial retrieval` more robust going
forward (either section now counts as a hit), without fixing why `TCGRULES-deck-building` itself sometimes
doesn't retrieve. Live re-verification is pending the same daily-quota reset as finding 5.

### 7. Self-reported Source Support consistently undershoots the validated label in several scenarios

`supporter-twice` self-reported "Insufficient" in all 3 real runs, while the validated label was Insufficient,
Strong, and Partial respectively — the model's own confidence was pessimistic relative to what
`GroundingValidator`'s deterministic checks actually supported, in 2 of 3 cases. A similar, smaller pattern
appears in `double-energy-attach` and `discard-shuffle-deescalate`. Not a new concept (Milestone 7 already
established Source Support isn't the model's self-reported confidence), but this is a concrete, repeated
illustration of exactly why that distinction matters in practice — worth keeping in mind for Milestone 9's
work comparing self-reported confidence against other reliability signals.

### 8. Rate-limit pressure is worse at this dataset's new size than Milestone 8 ever observed

26 of 100 attempted runs (about a quarter) hit the free-tier quota despite a 90-second pause before every
command — several commands alone (a `RequiresOneClarification` scenario × 5 repeats can be 25+ calls) exceed
the per-minute ceiling even with a fully reset quota. Milestone 8's `--from`/`--only` was sufficient for its
8-scenario dataset; at 20 scenarios with `--repeat`, sustained multi-scenario validation runs will need either
significantly longer pacing or acceptance that a meaningful fraction of any large run's data will be
infrastructure failures, not scenario results. Worth carrying into any future work that runs this dataset at
scale.

### 9. Even the free-tier's *daily* quota, not just the per-minute one, is now a real constraint

Follow-up fix verification for `late-to-round`, `ace-spec-count`, and `deck-under-60` (see findings 3, 5, 6)
hit `GenerateRequestsPerDayPerProjectPerModel-FreeTier`, a harder limit than the per-minute quota Milestone 8
originally documented — no amount of short waiting clears it within the same day. This session alone (the
five-run pass plus follow-up diagnosis and fixes) was enough real API usage to exhaust it. Worth naming
alongside the per-minute finding above as a second, distinct constraint on how much live validation this
dataset's size can absorb in one day of development.

## What this suggests for next steps (not acted on here, per the scope of this exercise)

- `special-condition`'s expected outcome remains the strongest candidate for re-review, given real, repeated
  evidence against its current authored expectation (finding 1) — not addressed in this follow-up pass, which
  focused on the three scripting/wording issues (findings 3, 5, 6).
- ~~`late-to-round` is a known, fixable issue of the same shape already solved twice this session.~~ **Fixed
  and verified live** (3/3 clean) — see finding 3's updated note.
- ~~`ace-spec-count`'s expected outcome...~~ **Investigated and reclassified** to `RequiresOneClarification`
  based on two further rounds of real diagnosis — see finding 5's updated note. Live re-verification pending
  daily quota reset.
- ~~`deck-under-60` deserves a source-coverage investigation entry (retrieval problem)...~~ **The scripting
  half fixed** (expected section, scripted answer); **the retrieval-quality half remains open** — still worth
  a source-coverage investigation entry extending `source-coverage-analysis.md` beyond its original four
  scenarios. Live re-verification of the scripting fix also pending daily quota reset.
- `mulligan-not-taken` and `spectator-conduct` extend the known-crash finding to a fifth and sixth scenario
  category — worth citing alongside the existing three in any future summary of that systemic issue. Not
  addressed here — this is the model's known sufficiency-assessment bug, not an eval-authoring issue.
