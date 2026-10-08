# Jev reranking results, 2026-10-07

Task 12 of [the Jev reranking plan](2026-10-04-jev-reranking.md). The "before" numbers come from
[the retrieval baseline](2026-10-04-retrieval-baseline-results.md). Both runs used the same local corpus and the same
branch, and the only difference was reranking: `--rerank none` for the baseline, Jev (the default since `045ffbe`)
here. Jev rescored the top 30 cosine results and kept 5.

## Verdict: don't adopt

| Adoption rule | Result | Holds? |
|---|---|---|
| At least 2 of the 4 targets receive every needed section in most repeats | 1 of 4 (`drew-extra-card`, which already did before reranking) | No |
| No control loses a needed section in most repeats | `deck-not-shuffled` lost `TCGTH-6.2.2` in both of its completed runs | No |
| The search-only score goes up | 29/29 before and after (it can't go up); rank-1 hits went from 20 to 24 | No (by the letter) |
| Added latency stays under about 2 s per turn | About 0.24 s per search | Yes |

The rules this step set out to reach are still below rank 30 (`TCGRULES-deck-building`, `ACE SPEC`, `PPG-4.1.1`) or
ranked out of the top 5 by Jev too (`TCGRULES-turn-actions`), so reranking the top 30 can't fix them. That points to
options B and C in the improvement plan, as the spec anticipated.

There are two more reasons not to make it the default:

- **Reliability.** 13 of 60 scenario-runs (22%) failed when Jev returned
  `503 {"error_type":"model_unavailable"}`. `evaluate` counted them as infrastructure failures, but in interactive
  mode each one is a failed question.
- **Determinism.** Before reranking, all three repeats retrieved the same sections on every turn. With Jev, the same
  turn-1 query returned a different top 5 (different chunks or a different order) across runs in 11 of the 16
  scenarios with more than one completed run.
  `--repeat 3` now measures Jev's variance as well as the model's.

## Search-only test (`eval`)

| Rank of the expected section | 1 | 2 | 3 | 4 | 5 | In top 5 |
|---|---|---|---|---|---|---|
| No rerank | 20 | 3 | 2 | 3 | 1 | 29/29 |
| Jev | **24** | 1 | 3 | 0 | 1 | 29/29 |

Cases whose rank changed:

| Expected section | No rerank | Jev |
|---|---|---|
| PPG-4.1.1 | 4 | 1 |
| PPG-5.4 | 4 | 1 |
| TCGTH-6.2 | 3 | 1 |
| PPG-5.2.1 | 2 | 1 |
| PPG-5.3.1 | 2 | 1 |
| TCGRULES-what-if-both-players-win-at-the-same-time | 4 | 2 |
| PPG-3.7 | 1 | **3** |
| TCGRULES-special-conditions | 2 | **3** |

Jev ranks better on the search-only test: 6 cases moved up and 2 moved down. These queries are short, single-rule
questions. The scenario runs below don't show the same gain.

## Full evaluation (`evaluate --repeat 3`)

**29/47** completed scenario-runs fully passed (62%), with 13 infrastructure failures, all from Jev 503s. The baseline
was 39/60 (65%) with none. The run took 30 minutes.

"Sections received" means every needed section appeared in at least one turn's top 5, counted over completed runs.

| Scenario | Completed runs | Sections received, before → after | Per section, after | Passed, before → after |
|---|---|---|---|---|
| notes | 3 | 3/3 → 3/3 | TCGTH-7.4.6 3/3 | 3/3 → 3/3 |
| proxy-cards | 3 | 3/3 → 3/3 | TCGTH-2.4 3/3 | 3/3 → 3/3 |
| **deck-not-shuffled** | 2 | 3/3 → **0/2** | TCGTH-6.2 2/2, **TCGTH-6.2.2 0/2** | 0/3 → 0/2 |
| spectator-badges | 3 | 3/3 → 3/3 | PPTRH-2.4 3/3 | 3/3 → 3/3 |
| repeat-violations | 3 | 3/3 → 3/3 | PPG-4.2.2 3/3 | 0/3 → 0/3 |
| special-condition | 3 | 3/3 → 3/3 | TCGRULES-special-conditions 3/3 | 3/3 → 2/3 |
| missed-prize | 3 | n/a (source gap) | | 3/3 → 0/3 |
| **drew-extra-card** | 3 | 3/3 → 3/3 | PPG-5.5.1 3/3 | 0/3 → 0/3 |
| weakness-not-applied | 3 | 3/3 → 3/3 | full-details-of-attacking 3/3, turn-actions 3/3 | 3/3 → 3/3 |
| **supporter-twice** | 3 | 0/3 → 0/3 | **TCGRULES-turn-actions 0/3**, PPG-4.2.1 3/3 | 0/3 → **3/3** |
| gx-attack-twice | 2 | 3/3 → 2/2 | appendix-19-pok-mon-gx 2/2 | 3/3 → 2/2 |
| missed-mulligan-draws | 1 | 3/3 → 1/1 | TCGTH-7.4.1 1/1, full-details-of-taking-a-mulligan 1/1 | 0/3 → 0/1 |
| late-to-round | 2 | 3/3 → 2/2 | PPG-5.2.1 2/2 | 3/3 → 2/2 |
| **deck-under-60** | 1 | 0/3 → 0/1 | **TCGRULES-deck-building 0/1**, PPG-5.6.1 1/1 (`#2` 1/1) | 3/3 → 1/1 |
| **ace-spec-count** | 3 | 0/3 → 0/3 | **appendix-3-ace-spec-cards 0/3**, PPG-5.6.1 3/3 (**`#2` 3/3**, was 0/3), **PPG-4.1.1 0/3** | 0/3 → 0/3 |
| too-many-prizes | 3 | 3/3 → 3/3 | PPG-5.5.1 3/3 | 0/3 → 2/3 |
| prize-issue-vague | 1 | 3/3 → 1/1 | PPG-5.5.1 1/1 | 3/3 → 1/1 |
| double-energy-attach | 1 | 3/3 → 1/1 | PPG-5.5.1 1/1 | 3/3 → 1/1 |
| discard-shuffle-deescalate | 2 | 3/3 → 2/2 | PPG-5.5.1 2/2 | 3/3 → 2/2 |
| spectator-conduct | 2 | 3/3 → 2/2 | PPTRH-3.3 2/2 | 3/3 → 1/2 |

### The four targets

- **drew-extra-card:** unchanged. It already received `PPG-5.5.1` on every turn before reranking, and it still fails
  because the question loop re-asks the same question until the 4-turn cap.
- **supporter-twice:** `TCGRULES-turn-actions` was rank 6 by cosine, the one target a top-30 reranker could have
  helped. Jev filled the top 5 with `PPG-4.2.1` chunks instead and never surfaced it. The scenario now passes 3/3,
  because with five `PPG-4.2.1` chunks the loop asks one question and rules on turn 2. The pass criteria accept any one
  expected section, so the pass doesn't mean the missing rule arrived.
- **deck-under-60:** `TCGRULES-deck-building` still never reaches the AI. It isn't in the top 30, so Jev never sees
  it. Two of the three runs failed on Jev 503s.
- **ace-spec-count:** a partial gain. Jev lifted `PPG-5.6.1#2` (the fix text) into turn 1 in 3/3 runs, where cosine
  never retrieved it. The ACE SPEC rule and `PPG-4.1.1` are below rank 30 and still missing, and the scenario still
  fails.

### The control that regressed

**deck-not-shuffled** needs `TCGTH-6.2.2`, which cosine ranked first on turn 1. Jev dropped it from the top 5 in both
completed runs and put `TCGTH-6.2.1` and two `PPG-2.3` chunks in its place:

```
Before T1: TCGTH-6.2.2 first (cosine rank 1), TCGTH-6.2 at rank 3
After  T1: TCGTH-6.2#0, TCGTH-6.2.1#0, PPG-2.3#0, PPG-2.3#1, TCGTH-6.2#1
```

`PPG-2.3` appears in the top 5 of most scenarios after reranking, but it wasn't in any of the four targets' top 5 in
the baseline. Jev rates this general excerpt as relevant to almost any situation, so it takes slots from specific
rules.

### Pass counts

Pass counts moved both ways (`supporter-twice` and `too-many-prizes` up, `missed-prize` and `special-condition` down).
Most of these scenarios received the same sections before and after, so the changes come from different chunk
orders reaching the model and from Jev's run-to-run variance, not from reaching new rules. As the baseline notes,
the adoption rule judges sections received for this reason.

## Latency

`eval` ran 29 searches in 14.7 s with Jev and 7.7 s without, so Jev adds about **0.24 s per search**. Each `evaluate`
turn runs one search, so the added latency per turn is about the same, well under the 2 s limit. Total `evaluate`
time isn't comparable (30 minutes against 34), because Gemini pacing sets it and 13 runs stopped early.

## Jev calls

One Jev request per search.

| Run | Jev requests |
|---|---|
| `eval` | 29 |
| `evaluate --repeat 3` | 119 completed turns, plus 13 that failed with 503 |
| **Total** | **at least 161** |

The output doesn't print turns of a run that later hits an infrastructure failure, so any successful calls before
each 503 aren't counted.
