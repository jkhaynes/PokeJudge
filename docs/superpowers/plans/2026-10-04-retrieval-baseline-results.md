# Retrieval baseline (no reranking), 2026-10-04

Task 11 of [the Jev reranking plan](2026-10-04-jev-reranking.md). This is the "before" state for Step 4's reranking
experiment. Everything here ran on branch `claude/jev-reranking` without `--rerank`, when reranking was still opt-in.
Reranking is now on by default, so reproduce these numbers with `--rerank none`.

## Read this first: which corpus this is

The local corpus (`PokeJudge/Chunking/Output/*.chunks.json`, gitignored) was re-embedded on 2026-09-29 by the unmerged
`step-4-search-quality` branch. That branch's Fix 1a embeds excerpts as `RETRIEVAL_DOCUMENT`. Its results doc measured
that a search with no task type embeds the same as `RETRIEVAL_QUERY`. This branch is based on master, which sends no
task type, so its searches already run as Fix 1a.

So this baseline is **Fix 1a retrieval with master's pipeline**. It is not the corpus behind the published 2026-09-27
baseline (https://claude.ai/artifact/3aghYCpThnprBHQaDVbVpp). The before/after comparison is still fair, because the
reranked run uses the same corpus and the only difference is `--rerank jev`. The corpus wasn't re-embedded, because
that would overwrite the other branch's local data and cost 515 of the 1,000 daily embedding calls.

## Search-only test (`eval`)

**29/29** expected sections in the top 5. This is the 29-case set from Task 9.

| Rank of the expected section | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| Cases | 20 | 3 | 2 | 3 | 1 |

The adoption rule asks for this score to go up. From 29/29 it can't, so the rank-1 count is the only place a
change can show.

## Retrieval depth (`retrieval-depth`, turn-1 description, top 30)

| Scenario | Expected section | Rank | Reach | Chunk at rank 5 |
|---|---|---|---|---|
| notes | TCGTH-7.4.6 | 1 | InTopK | PPTRH-3.2#4 |
| proxy-cards | TCGTH-2.4 | 1 | InTopK | TCGTH-2.3.3#1 |
| deck-not-shuffled | TCGTH-6.2 | 3 | InTopK | PPG-5.5.1#3 |
| deck-not-shuffled | TCGTH-6.2.2 | 1 | InTopK | |
| spectator-badges | PPTRH-2.4 | 1 | InTopK | PPG-1.1#1 |
| repeat-violations | PPG-4.2.2 | 1 | InTopK | PPG-6#0 |
| special-condition | TCGRULES-special-conditions | 1 | InTopK | TCGRULES-special-conditions#1 |
| missed-prize | (none expected) | | | PPG-4.2.1#1 |
| **drew-extra-card** | PPG-5.5.1 | 2 | InTopK | TCGRULES-what-if-you-should-draw-more-cards-than-you-have#0 |
| weakness-not-applied | TCGRULES-full-details-of-attacking | 1 | InTopK | TCGRULES-turn-actions#8 |
| weakness-not-applied | TCGRULES-turn-actions | 2 | InTopK | |
| **supporter-twice** | TCGRULES-turn-actions | 6 | **Rerankable** | PPG-2.2#0 |
| **supporter-twice** | PPG-4.2.1 | 1 | InTopK | |
| gx-attack-twice | TCGRULES-appendix-19-pok-mon-gx | 1 | InTopK | TCGRULES-glossary#4 |
| missed-mulligan-draws | TCGTH-7.4.1 | 2 | InTopK | PPG-5.5.1#4 |
| missed-mulligan-draws | TCGRULES-full-details-of-taking-a-mulligan | 1 | InTopK | |
| late-to-round | PPG-5.2.1 | 1 | InTopK | PPTRH-5.2#3 |
| **deck-under-60** | TCGRULES-deck-building | not in top 30 | **OutOfReach** | PPG-5.6.1#7 |
| **deck-under-60** | PPG-5.6.1 | 1 | InTopK | |
| **ace-spec-count** | TCGRULES-appendix-3-ace-spec-cards | not in top 30 | **OutOfReach** | PPG-5.6.1#8 |
| **ace-spec-count** | PPG-5.6.1 | 1 | InTopK | |
| **ace-spec-count** | PPG-4.1.1 | not in top 30 | **OutOfReach** | |
| too-many-prizes | PPG-5.5.1 | 1 | InTopK | PPG-5.5.1#5 |
| prize-issue-vague | PPG-5.5.1 | 1 | InTopK | PPG-4.2.1#1 |
| double-energy-attach | PPG-5.5.1 | 1 | InTopK | PPG-5.6.1#6 |
| discard-shuffle-deescalate | PPG-5.5.1 | 1 | InTopK | PPG-5.5.1#5 |
| spectator-conduct | PPTRH-3.3 | 1 | InTopK | PPG-5.3#0 |

Totals: 22 in the top 5, 1 rerankable, 3 out of reach. Not all four targets are out of reach, so the plan continues.
On turn 1, a top-30 reranker can only help `supporter-twice`.

## Full evaluation (`evaluate --repeat 3`)

**39/60** scenario-runs fully passed, with no infrastructure failures. The run took 34 minutes, paced by
`Gemini:RequestsPerMinute`.

"Sections received" means every needed section (initial plus post-answer) appeared in at least one turn's top 5.

| Scenario | Sections received (runs) | Per section | Passed | Turns |
|---|---|---|---|---|
| notes | 3/3 | TCGTH-7.4.6 3/3 | 3/3 | 1 |
| proxy-cards | 3/3 | TCGTH-2.4 3/3 | 3/3 | 1 |
| deck-not-shuffled | 3/3 | TCGTH-6.2 3/3, TCGTH-6.2.2 3/3 | 0/3 | 4 (cap) |
| spectator-badges | 3/3 | PPTRH-2.4 3/3 | 3/3 | 1 |
| repeat-violations | 3/3 | PPG-4.2.2 3/3 | 0/3 | 2 |
| special-condition | 3/3 | TCGRULES-special-conditions 3/3 | 3/3 | 3 |
| missed-prize | n/a | (source gap) | 3/3 | 4 (cap) |
| **drew-extra-card** | **3/3** | PPG-5.5.1 3/3 | 0/3 | 4 (cap) |
| weakness-not-applied | 3/3 | TCGRULES-full-details-of-attacking 3/3, TCGRULES-turn-actions 3/3 | 3/3 | 2 |
| **supporter-twice** | **0/3** | TCGRULES-turn-actions 0/3, PPG-4.2.1 3/3 | 0/3 | 4 (cap) |
| gx-attack-twice | 3/3 | TCGRULES-appendix-19-pok-mon-gx 3/3 | 3/3 | 1 |
| missed-mulligan-draws | 3/3 | TCGTH-7.4.1 3/3, TCGRULES-full-details-of-taking-a-mulligan 3/3 | 0/3 | 4 (cap) |
| late-to-round | 3/3 | PPG-5.2.1 3/3 | 3/3 | 2 |
| **deck-under-60** | **0/3** | TCGRULES-deck-building 0/3, PPG-5.6.1 3/3 (`#2` 3/3) | 3/3 | 3 |
| **ace-spec-count** | **0/3** | TCGRULES-appendix-3-ace-spec-cards 0/3, PPG-5.6.1 3/3 (`#2` 0/3), PPG-4.1.1 0/3 | 0/3 | 4 (cap) |
| too-many-prizes | 3/3 | PPG-5.5.1 3/3 | 0/3 | 4 (cap) |
| prize-issue-vague | 3/3 | PPG-5.5.1 3/3 | 3/3 | 2 |
| double-energy-attach | 3/3 | PPG-5.5.1 3/3 | 3/3 | 2 |
| discard-shuffle-deescalate | 3/3 | PPG-5.5.1 3/3 | 3/3 | 2 |
| spectator-conduct | 3/3 | PPTRH-3.3 3/3 | 3/3 | 1 |

**The three repeats were identical.** Every scenario's per-turn retrieval was the same in all three runs, and so were
its turn count and pass/fail. At this seed, `--repeat 3` adds no information over one run.

### Failures that aren't about retrieval

Seven scenarios fail every run while receiving every needed section:

- **Turn-cap failures:** `deck-not-shuffled`, `drew-extra-card`, `missed-mulligan-draws` and `too-many-prizes` hit the
  4-turn cap with no ruling, and `missed-prize` does too. The question loop re-asks a question it already asked. For
  example, `drew-extra-card` asks "Did the competitor see the face of the extra card?" on turns 2, 3 and 4.
  `step-4-search-quality` fixed this in `114f9b9` ("Show the question loop what it already asked"), but the fix isn't
  on master or this branch.
- **Unnecessary questions:** `repeat-violations` and `ace-spec-count` ask a question when the scenario expects an
  immediate ruling.

Reranking can't change any of these, so pass counts will hide its effect. The adoption rule judges sections received,
which is the right measure here.

### Per-turn top 5 for the four targets (run 1; runs 2 and 3 identical)

**drew-extra-card**
```
T1: PPG-4.2.1#0 (0.7337), PPG-5.5.1#4 (0.7200), PPG-4.2.1#1 (0.7138), PPG-5.5.1#1 (0.7021), TCGRULES-what-if-you-should-draw-more-cards-than-you-have#0 (0.6954)
T2: PPG-5.5.1#4 (0.7483), PPG-4.2.1#0 (0.7442), PPG-4.2.1#1 (0.7380), PPG-5.5.1#1 (0.7361), PPG-5.6.1#3 (0.7304)
T3: PPG-5.5.1#4 (0.7493), PPG-4.2.1#0 (0.7437), PPG-4.2.1#1 (0.7391), TCGTH-6.3.3#0 (0.7378), PPG-5.5.1#1 (0.7338)
T4: PPG-5.5.1#4 (0.7519), PPG-4.2.1#0 (0.7459), PPG-4.2.1#1 (0.7426), TCGTH-6.3.3#0 (0.7423), PPG-5.5.1#1 (0.7372)
```

**supporter-twice**
```
T1: PPG-4.2.1#0 (0.7884), PPG-4.2.1#2 (0.7816), PPG-4.2.1#4 (0.7449), PPG-4.2.1#1 (0.7358), PPG-2.2#0 (0.7277)
T2: PPG-4.2.1#0 (0.7773), PPG-4.2.1#2 (0.7673), PPG-4.2.1#4 (0.7402), PPG-4.2.1#1 (0.7249), PPG-4.2.1#3 (0.7185)
T3: PPG-4.2.1#0 (0.7844), PPG-4.2.1#2 (0.7632), PPG-4.2.1#4 (0.7440), PPG-4.2.1#1 (0.7390), PPG-4.2.1#3 (0.7276)
T4: PPG-4.2.1#0 (0.7911), PPG-4.2.1#2 (0.7655), PPG-4.2.1#4 (0.7499), PPG-4.2.1#1 (0.7483), PPG-4.2.1#3 (0.7371)
```

**deck-under-60**
```
T1: PPG-5.6.1#2 (0.7803), PPG-5.6.1#6 (0.7580), PPG-5.6.1#3 (0.7412), TCGTH-3.2#1 (0.7361), PPG-5.6.1#7 (0.7347)
T2: PPG-5.6.1#2 (0.7643), PPG-5.6.1#6 (0.7628), PPG-5.6.1#0 (0.7583), TCGTH-3.2#1 (0.7567), PPG-5.6.1#8 (0.7534)
T3: PPG-5.6.1#8 (0.7834), PPG-5.6.1#3 (0.7763), PPG-5.6.1#6 (0.7729), PPG-5.6.1#2 (0.7718), PPG-5.6.1#4 (0.7668)
```

**ace-spec-count**
```
T1: PPG-5.6.1#7 (0.7642), PPG-5.6.1#4 (0.7582), PPG-5.6.1#3 (0.7539), PPG-5.6.1#0 (0.7523), PPG-5.6.1#8 (0.7467)
T2: PPG-5.6.1#7 (0.7671), PPG-5.6.1#6 (0.7573), PPG-5.6.1#4 (0.7505), PPG-5.5.1#3 (0.7469), PPG-5.6.1#3 (0.7441)
T3: PPG-5.6.1#7 (0.7717), PPG-5.6.1#6 (0.7625), PPG-5.6.1#4 (0.7526), PPG-5.5.1#3 (0.7502), PPG-5.6.1#5 (0.7464)
T4: PPG-5.6.1#7 (0.7604), PPG-5.6.1#6 (0.7587), PPG-5.6.1#4 (0.7511), PPG-5.5.1#3 (0.7498), PPG-5.6.1#3 (0.7440)
```

## Contradictions with the published baseline

| Published (2026-09-27) | Measured here | Likely cause |
|---|---|---|
| `drew-extra-card` never received `PPG-5.5.1`; every turn got `PPG-4.2.1` instead | `PPG-5.5.1#4` and `#1` are in the top 5 on all 4 turns, in 3/3 runs | Fix 1a vectors. `step-4-search-quality` measured `drew-extra-card` moving from TOP 10 to TOP 5 under Fix 1a. On this corpus it's no longer a retrieval target; it fails on the re-asking loop. |
| `supporter-twice`: turn-actions "unconfirmed" | Never retrieved: 0/3 runs, on any turn. Rank 6 on turn 1. | The per-turn top 5 is now printed, so this is now known. |
| `ace-spec-count`: ruling turn's 5 were `PPG-5.6.1#3, #4, #5, #6, #8` | `#7, #6, #4, #3` plus `#5` or `PPG-5.5.1#3`; `#2` (the fix) never retrieved | Different vectors. Same conclusion: neither the ACE SPEC rule nor `#2` reaches the AI. |
| `deck-under-60` turn 1: `5.6.1#2, #6, TCGTH-6.2.1#0, 5.6.1#3, #8` | `5.6.1#2, #6, #3, TCGTH-3.2#1, 5.6.1#7` | Different vectors. `TCGRULES-deck-building` is still out of reach (not in the top 30). |
| 16/20 scenarios passed | 13/20 pass all three runs (39/60) | The re-asking loop, which wasn't fixed on master. Retrieval isn't the cause: those scenarios receive every needed section. |
| Search-only test: 6/7 on 258 excerpts | 29/29 on the new 29-case set, 515 excerpts | New test set, and the repeat-violations case now expects `PPG-4.2.2`. |

The `step-4-search-quality` branch also dropped `PPG-4.1.1` from `ace-spec-count`'s requirements (`ccb79b5`). This
branch still expects it.
