# Evaluate run: v6, top 10, with the re-asking fix (2026-10-10)

Day 2 of [the to-do for 2026-10-09](../../plans/2026-10-09-todo.md): the AI reads the top 10 excerpts per turn instead
of 5. Everything else matches [the Day 1 top-5 baseline](../2026-10-09-v6-top5-reask-fix/run.md), including the cap
of 4 excerpts per section.

## Settings

| | |
|---|---|
| Commit | `25b319f` (code identical to `2efdc54`; only docs differ) |
| Command | `dotnet run --project PokeJudge -- evaluate --repeat 3 --top 10` |
| Excerpts the AI reads per turn | 10 |
| Reranking | Jev, the v4 question wording, 100 candidates, at most 4 excerpts per section, retries on |
| Chat model | `gemini-3.5-flash-lite`, fixed seed, paced to 14 requests per minute |
| Started | 2026-10-10 15:39 UTC; took about 23 minutes |

Files: `evaluate.log` is the full console output (stdout and stderr). `summary.json` is produced from it by
`python -I tools/eval_summary.py summarize evaluate.log summary.json`.

## Verdict: keep 5

Top 10 is no better on any of the three things the to-do said to judge it on, and it loses some of what the
re-asking fix gained:

| | Top 5 (10-09) | Top 10 (10-10) |
|---|---|---|
| Scenario-runs passed | **45/60** | 42/60 |
| Clarifying questions asked | **38** | 48 |
| Runs whose final Source Support was Insufficient | **1** | 4 |
| Runs that asked when the scenario expects an immediate ruling | 0 | 4 (`ace-spec-count` 2, `spectator-conduct` 2) |
| Scenarios whose three repeats differ | **0** | 4 |

The retrieval checks pass at least as often with 10 (more excerpts make "the needed section was shown" easier), so
they aren't counted in its favour here.

## Per scenario

| Scenario | Passed top 5 | **Passed top 10** | Turns top 10 | Why top-10 runs failed |
|---|---|---|---|---|
| notes | 3/3 | **3/3** | 1, 1, 1 | |
| proxy-cards | 3/3 | **3/3** | 1, 1, 1 | |
| deck-not-shuffled | 0/3 | **0/3** | 2, 2, 2 | Asked about intent (`PPG-5.4`) and who called the judge, not the shuffling rule (3) |
| spectator-badges | 3/3 | **3/3** | 1, 1, 1 | |
| repeat-violations | 3/3 | **3/3** | 1, 1, 1 | |
| special-condition | 0/3 | **0/3** | 2, 2, 2 | Asked about arbitrary adjustment (`PPG-5.4`) (2); asked about the right rule but Source Support Insufficient (1) |
| missed-prize | 0/3 | **0/3** | 2, 2, 2 | Ruled, although the scenario expects no ruling (3); two of those rulings say the passages don't cover the case |
| drew-extra-card | 3/3 | **2/3** | 2, 2, 2 | Source Support Insufficient (1) |
| weakness-not-applied | 3/3 | **3/3** | 2, 2, 2 | |
| supporter-twice | 3/3 | **3/3** | 2, 2, 2 | |
| gx-attack-twice | 3/3 | **3/3** | 1, 1, 1 | |
| missed-mulligan-draws | 3/3 | **2/3** | 2, 2, 1 | Ruled on turn 1 without the expected question (1) |
| late-to-round | 3/3 | **3/3** | 2, 2, 2 | |
| deck-under-60 | 0/3 | **3/3** | 2, 2, 2 | |
| ace-spec-count | 3/3 | **1/3** | 2, 1, 2 | Asked a question when it should rule at once (2) |
| too-many-prizes | 0/3 | **0/3** | 2, 2, 2 | Asked which kind of Pokémon was Knocked Out (glossary, Pokémon ex rules), not a question tied to `PPG-5.5.1` (3) |
| prize-issue-vague | 3/3 | **3/3** | 2, 2, 2 | |
| double-energy-attach | 3/3 | **3/3** | 2, 2, 2 | |
| discard-shuffle-deescalate | 3/3 | **3/3** | 2, 2, 2 | |
| spectator-conduct | 3/3 | **1/3** | 1, 2, 2 | Asked "spectator or competitor?" when it should rule at once (2) |
| **Total** | **45/60** | **42/60** | | |

Criterion failures, top 5 → top 10: Clarifying question materiality 12 → 9, Sufficiency timing 6 → 5, Post-answer
retrieval 6 → 0, Final Source Support 1 → 2, Expected unresolvable 3 → 3.

## Reading it

- **The extra excerpts mostly add noise.** Every scenario received sections it didn't get at top 5, but they are
  mostly general ones (`PPG-2.3`, `PPG-4.1`, `PPTRH-3.3`, `PPTRH-7.3`, the rulebook credits). The AI turns some of them
  into questions: `spectator-conduct` sees the competitor-conduct rules again and asks "spectator or competitor?", and
  `too-many-prizes` sees the Pokémon ex rules and asks what kind of Pokémon was Knocked Out.
- **Some rulings get weaker, not stronger.** Four runs ended with Insufficient Source Support (1 at top 5). In
  `missed-prize` and `drew-extra-card` the AI said outright that the passages didn't cover the case, despite reading
  twice as many.
- **The repeats differ again.** At top 5 all three repeats matched; at top 10, four scenarios
  (`ace-spec-count`, `spectator-conduct`, `drew-extra-card`, `missed-mulligan-draws`) split.
- **One clear gain:** `deck-under-60` now asks a question before ruling, as the scenario expects (0/3 → 3/3).
  `too-many-prizes` asks one too, but about the wrong thing. Post-answer retrieval failures went from 6 to 0 for the
  same reason.
- **`ace-spec-count`'s fix text (`PPG-5.6.1#2`) reached the AI in 2 of 3 runs, but only on turn 2.** On turn 1 the cap
  of 4 per section keeps it out of the top 10, as the to-do expected. `PPG-4.1.1` still never arrives.
