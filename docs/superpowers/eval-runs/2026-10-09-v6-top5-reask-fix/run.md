# Evaluate run: v6, top 5, with the re-asking fix (2026-10-09)

Day 1 of [the to-do for 2026-10-09](../../plans/2026-10-09-todo.md): the new top-5 baseline. The only change from
[the 2026-10-08 run](../2026-10-08-v6-top5/run.md) is the re-asking fix (`87aa9a9`), which shows the question loop the
questions it already asked. The top-10 run will be compared against this one.

## Settings

| | |
|---|---|
| Commit | `2efdc54` (master after PR #16: tuning v6 plus the re-asking fix) |
| Command | `dotnet run --project PokeJudge -- evaluate --repeat 3 --top 5` |
| Excerpts the AI reads per turn | 5 |
| Reranking | Jev, the v4 question wording, 100 candidates, at most 4 excerpts per section, retries on |
| Chat model | `gemini-3.5-flash-lite`, fixed seed, paced to 14 requests per minute |
| Started | 2026-10-09 14:20 UTC; took about 21 minutes |

Files: `evaluate.log` is the full console output (stdout and stderr). `summary.json` is produced from it by
`python -I tools/eval_summary.py summarize evaluate.log summary.json`.

An attempt at 03:01 UTC the same day failed on all 60 scenario-runs with `429 RESOURCE_EXHAUSTED`, because the chat
quota hadn't reset yet (see the to-do's quota note). It produced no results and wasn't kept.

## Headline

- **45/60 scenario-runs passed** (36/60 on 2026-10-08), with no infrastructure failures and no Jev retries.
- **No run hit the 4-turn cap.** Every run ended in 1 or 2 turns. The loop asked **38** clarifying questions in total,
  against 111 the day before.
- **The three repeats now match.** Each scenario took the same number of turns and got the same outcome in all three
  runs. With fewer turns, Jev's small score differences on later turns have less room to change what the AI sees.
- **Retrieval is unchanged.** Every needed section reached the AI in every run, except `PPG-4.1.1` for `ace-spec-count`
  (still scored about 0.06 by Jev). `Initial retrieval` passed in all 57 runs where it applies.

## Per scenario

| Scenario | Passed 10-08 | **Passed 10-09** | Turns 10-08 | Turns 10-09 | Why 10-09 runs failed |
|---|---|---|---|---|---|
| notes | 3/3 | **3/3** | 1, 1, 1 | 1, 1, 1 | |
| proxy-cards | 3/3 | **3/3** | 1, 1, 1 | 1, 1, 1 | |
| deck-not-shuffled | 0/3 | **0/3** | 2, 4, 2 | 2, 2, 2 | Asked about intent (`PPG-5.4`), not the shuffling rule (3); Source Support Insufficient (1) |
| spectator-badges | 3/3 | **3/3** | 1, 1, 1 | 1, 1, 1 | |
| repeat-violations | 0/3 | **3/3** | 4, 4, 4 | 1, 1, 1 | |
| special-condition | 1/3 | **0/3** | 2, 3, 2 | 2, 2, 2 | Asked about arbitrary adjustment (`PPG-5.4`), not the Special Conditions rule (3) |
| missed-prize | 2/3 | **0/3** | 4, 4, 2 | 2, 2, 2 | Ruled, although the scenario expects no ruling (3) |
| drew-extra-card | 0/3 | **3/3** | 4, 4, 4 | 2, 2, 2 | |
| weakness-not-applied | 1/3 | **3/3** | 2, 4, 4 | 2, 2, 2 | |
| supporter-twice | 3/3 | **3/3** | 3, 2, 2 | 2, 2, 2 | |
| gx-attack-twice | 3/3 | **3/3** | 1, 1, 1 | 1, 1, 1 | |
| missed-mulligan-draws | 0/3 | **3/3** | 4, 4, 2 | 2, 2, 2 | |
| late-to-round | 3/3 | **3/3** | 2, 2, 2 | 2, 2, 2 | |
| deck-under-60 | 3/3 | **0/3** | 2, 2, 2 | 1, 1, 1 | Ruled on turn 1; the scenario expects a question first (3) |
| ace-spec-count | 0/3 | **3/3** | 4, 3, 3 | 1, 1, 1 | |
| too-many-prizes | 2/3 | **0/3** | 4, 4, 4 | 1, 1, 1 | Ruled on turn 1; the scenario expects a question first (3) |
| prize-issue-vague | 3/3 | **3/3** | 2, 2, 2 | 2, 2, 2 | |
| double-energy-attach | 3/3 | **3/3** | 3, 3, 3 | 2, 2, 2 | |
| discard-shuffle-deescalate | 3/3 | **3/3** | 2, 2, 2 | 2, 2, 2 | |
| spectator-conduct | 0/3 | **3/3** | 2, 2, 2 | 1, 1, 1 | |
| **Total** | **36/60** | **45/60** | | | |

Criterion failures, 10-08 → 10-09: Final Source Support 13 → 1, Sufficiency timing 9 → 6, Clarifying question
materiality 6 → 12, Post-answer retrieval 0 → 6, Expected unresolvable 1 → 3.

## Reading it

- **The fix did what it was for.** All five scenarios the to-do expected to stop hitting the turn cap
  (`drew-extra-card`, `weakness-not-applied`, `missed-mulligan-draws`, `repeat-violations`, `too-many-prizes`) now end
  in 1 or 2 turns, and four of them pass 3/3. Source Support failures nearly vanished (13 → 1), because runs now reach
  a ruling instead of stopping at the cap with nothing to support.
- **It swung the loop toward ruling sooner.** Two scenarios that expect one question before the ruling now rule on
  turn 1: `deck-under-60` (3/3 → 0/3) and `too-many-prizes`. `too-many-prizes`' ruling tells the judge to "investigate
  to determine if" the extra Prize caused confusion, which is the question it should have asked.
- **`missed-prize`'s earlier passes weren't real.** Two of its three runs on 10-08 passed by hitting the turn cap
  without ruling, which satisfies "expected unresolvable". Now the loop asks once and rules, so all three fail. The
  harness suggests "the corpus may now cover this"; whether the scenario's expectation still fits is a scenario
  question, like `spectator-conduct`'s.
- **`spectator-conduct` passes 3/3 again.** It now rules at once instead of asking "spectator or competitor?", so the
  open to-do item about its "rule at once" expectation may no longer be needed.
- **The remaining failures are question choice, not retrieval.** `deck-not-shuffled` and `special-condition` ask a
  fair question tied to `PPG-5.4` (intent, arbitrary adjustment) instead of one tied to the rule the scenario names.
  Every needed section was in the AI's top 5 in those runs.
