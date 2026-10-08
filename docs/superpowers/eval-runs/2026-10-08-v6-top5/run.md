# Evaluate run: v6, top 5 (2026-10-08)

The full-scenario check of the Jev tuning ([tracker](../../plans/2026-10-07-jev-tuning-tracker.html), idea J), and the
"top 5" side of the planned 5-versus-10 comparison.

## Settings

| | |
|---|---|
| Commit | `1139e19` (tuning v6) |
| Command | `dotnet run --project PokeJudge -- evaluate --repeat 3` |
| Excerpts the AI reads per turn | 5 (before `--top` existed; the default) |
| Reranking | Jev, the v4 question wording, 100 candidates, at most 4 excerpts per section, retries on |
| Chat model | `gemini-3.5-flash-lite`, fixed seed, paced to 14 requests per minute |
| Started | 2026-10-08 15:54 UTC; took 33 min 40 s |

Files: `evaluate.log` is the full console output (stdout and stderr). `summary.json` is produced from it by
`python -I tools/eval_summary.py summarize evaluate.log summary.json`.

## Headline

- **36/60 scenario-runs passed**, with no infrastructure failures and no Jev retries (Jev had no outage during the run).
- **Every needed section reached the AI in every run, except `PPG-4.1.1` for `ace-spec-count`.** That includes the
  four retrieval targets the reranking work started with: `drew-extra-card`, `supporter-twice` and `deck-under-60`
  get every needed section in 3/3 runs, and `ace-spec-count` gets 3 of its 4.
- **The repeats are no longer identical.** Without reranking, all three repeats matched. Jev's small score differences
  on later turns now change which excerpts the AI sees, so turns and outcomes vary between repeats.

## Per scenario, against earlier full runs

"Received" = every needed section was in at least one turn's top 5. Earlier columns: the cosine baseline
([2026-10-04](../../plans/2026-10-04-retrieval-baseline-results.md)) and Jev as first built
([2026-10-07](../../plans/2026-10-04-jev-reranking-results.md), 13 runs lost to Jev outages).

| Scenario | Received (v6) | Passed: cosine | Passed: Jev v0 | **Passed: v6** | Turns (v6) | Why v6 runs failed |
|---|---|---|---|---|---|---|
| notes | 3/3 | 3/3 | 3/3 | **3/3** | 1, 1, 1 | |
| proxy-cards | 3/3 | 3/3 | 3/3 | **3/3** | 1, 1, 1 | |
| deck-not-shuffled | 3/3 | 0/3 | 0/2 | **0/3** | 2, 4, 2 | Questions not tied to an expected section (3), Source Support (1) |
| spectator-badges | 3/3 | 3/3 | 3/3 | **3/3** | 1, 1, 1 | |
| repeat-violations | 3/3 | 0/3 | 0/3 | **0/3** | 4, 4, 4 | Asked when it should rule at once; hit the turn cap |
| special-condition | 3/3 | 3/3 | 2/3 | **1/3** | 2, 3, 2 | Questions not tied to an expected section (2) |
| missed-prize | n/a | 3/3 | 0/3 | **2/3** | 4, 4, 2 | Ruled although the scenario is unresolvable (1) |
| drew-extra-card | 3/3 | 0/3 | 0/3 | **0/3** | 4, 4, 4 | Turn cap, no ruling (the re-asking loop) |
| weakness-not-applied | 3/3 | 3/3 | 3/3 | **1/3** | 2, 4, 4 | Turn cap, no ruling (2) |
| supporter-twice | 3/3 | 0/3 | 3/3 | **3/3** | 3, 2, 2 | |
| gx-attack-twice | 3/3 | 3/3 | 2/2 | **3/3** | 1, 1, 1 | |
| missed-mulligan-draws | 3/3 | 0/3 | 0/1 | **0/3** | 4, 4, 2 | Turn cap (2), question materiality (1) |
| late-to-round | 3/3 | 3/3 | 2/2 | **3/3** | 2, 2, 2 | |
| deck-under-60 | 3/3 | 3/3 | 1/1 | **3/3** | 2, 2, 2 | |
| ace-spec-count | 0/3 (`PPG-4.1.1` never) | 0/3 | 0/3 | **0/3** | 4, 3, 3 | Asked when it should rule at once (3), Source Support (1) |
| too-many-prizes | 3/3 | 0/3 | 2/3 | **2/3** | 4, 4, 4 | Turn cap (1) |
| prize-issue-vague | 3/3 | 3/3 | 1/1 | **3/3** | 2, 2, 2 | |
| double-energy-attach | 3/3 | 3/3 | 1/1 | **3/3** | 3, 3, 3 | |
| discard-shuffle-deescalate | 3/3 | 3/3 | 2/2 | **3/3** | 2, 2, 2 | |
| spectator-conduct | 3/3 | 3/3 | 1/2 | **0/3** | 2, 2, 2 | Asked when it should rule at once (3) |
| **Total** | | **39/60** | **29/47** | **36/60** | | |

## Reading it

- **Retrieval did its job.** No scenario is missing a needed section except `ace-spec-count`'s `PPG-4.1.1`. Against the
  spec's adoption rule: 3 of the 4 targets receive every needed section in most repeats (rule: at least 2), no control
  loses a section (rule: none), and latency is about +0.33 s per search (rule: under about 2 s). The search-only clause
  can't be met (29/29 before and after), but rank-1 hits rose from 20 to 25.
- **The pass rate fell (39 to 36), and the failures aren't about missing rules.** They are the question loop: hitting
  the 4-turn cap without a ruling (the re-asking bug fixed on `step-4-search-quality` but not here), or asking a
  question when the scenario expects an immediate ruling.
- **`spectator-conduct` is the clearest case of reranking changing the conversation.** Its turn-1 top 5 now includes the
  competitor-conduct rules (`PPG-5.3.1`, `PPG-5.3`), so the AI asks "was this a spectator or a competitor?" in all 3
  runs. That's a fair question, but the scenario expects an immediate ruling.
- **Gains:** `supporter-twice` 0/3 → 3/3 and `too-many-prizes` 0/3 → 2/3. **Losses:** `spectator-conduct` 3 → 0,
  `weakness-not-applied` 3 → 1, `special-condition` 3 → 1, `missed-prize` 3 → 2. With repeats varying now, a difference
  of one run per scenario is within noise.

## Comparing with later runs

This run is *before* the re-asking fix (`87aa9a9`, cherry-picked from `114f9b9` after it). The top-10 run will be
compared against a new top-5 baseline that has the fix, not against this run; see
[the to-do for 2026-10-09](../../plans/2026-10-09-todo.md). This run stays useful for measuring what the re-asking fix
alone changed.
