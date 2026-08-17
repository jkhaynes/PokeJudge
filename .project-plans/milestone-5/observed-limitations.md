# Milestone 5 — Observed Limitations & Failures (Real-Data Run)

Captured per the plan's step 8: concrete evidence from running vector search and the retrieval
evaluation set against the real, already-embedded documents from Milestones 3-4.

- **Date:** 2026-08-16
- **Branch:** `milestone/5-vector-search`
- **Corpus:** 258 chunks across both real documents (`PPTRH`, 162 chunks; `TCGTH`, 96 chunks).
- **Eval set:** 7 hand-authored judge-scenario queries (`RetrievalEvalSet`), each paired with a
  ground-truth `SectionId` grounded in the real ingested content (inspected directly before writing
  the queries, not guessed at).
- **Result:** `dotnet run --project PokeJudge -- eval` → **6/7 hit within top 5**, one real miss.

## Semantic search worked, demonstrated concretely, not just asserted

`search "can I take written notes during my match"` — deliberately phrased in plain judge language,
not the rulebook's own wording ("Competitors are permitted to take written notes...") — returned
`TCGTH-7.4.6#0` as the #1 result at 0.8220 cosine similarity, the exact correct chunk. None of the
query's words ("can I", "my match") appear verbatim in the source text; the match is semantic, not
keyword-based. This is the core "semantic vs. keyword search" distinction the milestone plan set out to
demonstrate, shown with a real query against real data rather than a constructed toy example.

Five of the seven eval cases hit at rank 1, with scores in the high 0.7s-0.8s range, across genuinely
varied phrasing (deck shuffling, card damage, spectator badges, streamed matches, mulligans) and both
real documents — a solid, repeated demonstration, not a single lucky case.

## The one real miss: a good example of why retrieval quality needs evaluating, not assumed

**Query:** *"How are penalties handled for a competitor with a history of repeat violations?"*
**Expected:** `PPTRH-7.5` ("Play! Pokémon tracks each competitor's penalty history to differentiate
intentional...")
**Actual top 3:** `PPTRH-7.3#3` (0.6973), `TCGTH-8.1#1` (0.6953), `PPTRH-7.2#1` (0.6920) — the expected
section never appeared in the top 5 at all.

All three retrieved chunks are genuinely penalty-related (base infractions, penalty tiers), so the
search isn't returning nonsense — it's returning topically-adjacent content that isn't the specific
section that actually addresses *repeat* violations. This is a real, concrete example of the milestone
plan's predicted limitation: no query rewriting or re-ranking exists yet, so a query sitting at the
boundary between several similar-topic sections can miss the one that's actually most relevant. This is
exactly the kind of gap Milestone 6's iterative retrieval and Milestone 8's formal evaluation are meant
to address — observed directly here, not just anticipated.

## A methodological nuance found in the process: ground truth isn't always a single section

The `TCGTH-6.2` case (deck shuffling) hit, but only at **rank 3** — ranks 1 and 2 were `TCGTH-6.2.2`
("Insufficient Randomization") and `TCGTH-6.2.1` ("Judge Intervention"), both real subsections of
`6.2` and both *legitimately relevant* to "a deck wasn't fully shuffled." The evaluation set's
ground truth only credited the exact parent section (`TCGTH-6.2`) as correct, so this was scored a hit
only because it happened to still land within the top-5 window — but a stricter "rank 1 only" check
would have called this a miss, despite the actual top two results being reasonable, on-topic answers.

This is worth being explicit about: hand-authoring a retrieval eval set means deciding what counts as
"correct," and for a real document with parent/subsection structure, more than one section can
legitimately answer the same judge question. `RetrievalEvaluator`'s current design (a single expected
`SectionId`, checked for presence anywhere in the top-K) is a reasonable simplification for a 7-case set
inspected by hand, but it under-represents this ambiguity — a real limitation of the evaluation
methodology itself, not just the search quality it's measuring. Formalizing this (e.g., allowing a set
of acceptable sections per case) is reasonable future work, not attempted here.

## In-process brute-force search performed instantly at this scale

All 7 eval queries plus the standalone `search` smoke test completed with no perceptible latency beyond
the embedding API round-trip itself — confirming the plan's expectation that a brute-force linear scan
over 258 chunks needs no indexing structure at this scale. Nothing here suggests urgency to introduce a
dedicated vector store; that remains correctly deferred.

## Milestone 4's oversized-chunk finding did not visibly hurt retrieval in this run

None of the 7 eval cases' expected or retrieved chunks were among Milestone 4's previously-documented
oversized chunks (e.g., `PPTRH-3.3#3` at 2043 characters). This eval set is too small to rule out an
effect entirely, but no evidence of it surfaced here — worth revisiting if a larger, more systematic
evaluation (Milestone 8) is built later.
