# Milestone 5 Review

**Milestone reviewed:** Milestone 5 — Vector Search
**Plan:** `.project-plans/milestone-5/plan.md`
**Branch:** `milestone/5-vector-search`
**Build:** `dotnet build` — succeeded, 0 warnings, 0 errors.
**Tests:** `dotnet test` — 96/96 passed at time of review; 99/99 after the fix below (3 new tests).

**Update:** Consider-Improving item #1 (the zero-vector chunk failure) has since been fixed — see its
note below.

## ✅ Matches the Plan

- **All "What We Will Build" items delivered**: `VectorMath.CosineSimilarity`, `InMemoryVectorStore`
  (brute-force, in-process), the `search` console mode, and the hand-authored `RetrievalEvalSet` +
  `RetrievalEvaluator` + `eval` console mode.
- **The "evaluable independent of the LLM" requirement is genuinely satisfied, verified by reading the
  code, not just trusting the docs**: `RunRetrievalEval` calls `GeminiEmbeddingClient` once (for the
  eval queries) and never touches `ILlmClient`/`GeminiLlmClient` at all — `RetrievalEvaluator.Evaluate`
  itself is a pure function over already-computed results with no I/O whatsoever.
- **Semantic-vs-keyword search is demonstrated with real evidence, not just claimed.** The `search`
  smoke test used a deliberately non-keyword-matching query ("can I take written notes during my
  match") and returned the exact correct chunk at rank 1 — this was independently re-run during this
  review and reproduced the same result.
- **The eval "miss" was independently verified as a genuine retrieval limitation, not a mislabeled
  ground-truth case.** Reading the real content of `PPTRH-7.5` (the expected section) against the
  actually-retrieved `PPTRH-7.3`/`TCGTH-8.1`/`PPTRH-7.2` confirms `PPTRH-7.5` is precisely and only
  about tracking repeat-offender history, while the retrieved sections are topically-adjacent
  penalty-tier content — the eval case's ground truth is correct, and the miss is real.
- **In-process, brute-force vector store is the right, proportional choice at this scale** (258
  chunks) — no premature indexing infrastructure, matching PRD §12's explicit framing.
- **Reuses Milestone 4's abstractions correctly rather than duplicating them**: `IEmbeddingClient`,
  `GeminiEmbeddingClient`, and the batch-first design (one `EmbedBatchAsync` call for all 7 eval
  queries, not 7 separate calls) are reused as-is. This also means Milestone 4's empirically-verified
  batch-response-order guarantee and count-validation safeguard (`GeminiEmbeddingResponseParser`)
  transitively protect this milestone's `queryVectors[i]` ↔ `RetrievalEvalSet.Cases[i]` positional
  mapping without needing to re-verify it from scratch.
- **Tests are meaningful and mathematically correct** — independently re-derived the expected cosine
  similarity values for the orthogonal/opposite/magnitude-invariance test cases by hand; all check out.
- **A real, honest methodological finding was surfaced and documented, not glossed over**: the
  `TCGTH-6.2` case only "hit" at rank 3 because two legitimately-relevant sibling subsections
  (`TCGTH-6.2.1`, `TCGTH-6.2.2`) outranked the exact section marked as ground truth — correctly
  identified as a limitation of the single-expected-section eval design, not just search quality.

## 🚨 Must Fix

None.

## ⚠️ Consider Improving

- ~~**`InMemoryVectorStore.Search` aborts the entire search if any single chunk's embedding is a zero
  vector**, with an unhelpful, unnamed error.~~ **Fixed.** Validation moved to the constructor — fails
  immediately at load time, before any query is attempted, and names every offending `ChunkId` in the
  message (not just the first), with guidance to re-run `chunk <document-code>` to regenerate the
  corrupted output. `Program.cs`'s `RunSearch`/`RunRetrievalEval` now build the store through a shared
  `CreateVectorStore` helper that prints a friendly diagnostic line before re-throwing — matching the
  guidance pattern already used for `chunk`'s rate-limit handling — so the failure is both loud (still
  fails, still surfaces the full exception) and diagnosable (names what's wrong and how to fix it).
  Locked in with 3 new tests (single bad chunk, multiple bad chunks named together, all-valid case
  unaffected) and re-verified against the real 258-chunk corpus with no regression.
- **The single-expected-`SectionId` eval design under-represents cases with more than one legitimately
  correct answer**, as directly observed with `TCGTH-6.2`/`6.2.1`/`6.2.2`. Already documented
  thoughtfully in `observed-limitations.md` as a real, accepted limitation rather than something to
  silently work around — flagging here only to note it's a genuine candidate for `RetrievalEvalCase` to
  eventually accept a *set* of acceptable section IDs, whenever Milestone 8's more formal harness is
  built. Not appropriate to fix now; the plan explicitly scoped this eval set as small and
  not-statistically-rigorous.

## 🧪 Learning Observations

- **The "evaluable independent of the LLM" learning objective landed clearly and was independently
  confirmed**, not just asserted in the docs: tracing the actual call graph of `eval` shows zero calls
  to the chat/completion model, only one batched embedding call for all 7 queries followed by pure,
  local vector math.
- **The real miss is more instructive than a clean 7/7 would have been.** Verifying `PPTRH-7.5`'s
  actual content against what got retrieved instead makes the failure mode concrete and specific
  (topically-adjacent-but-wrong, not nonsense) — a good, first-hand example of exactly the kind of gap
  Milestone 6's iterative retrieval and Milestone 8's evaluation exist to close.
- **The `TCGTH-6.2` near-miss is a genuinely valuable lesson about evaluation design itself**, distinct
  from the system being evaluated: deciding what counts as "correct" is a real design decision that can
  be wrong in ways invisible until real data runs through it. Worth carrying into Milestone 8's
  thinking about how branching/trajectory evaluation ground truth gets defined.
- **Reuse of Milestone 4's already-hardened abstractions (batch-first embedding, verified response
  ordering, count validation) meant this milestone's implementation had noticeably fewer rough edges
  than Milestones 3-4's first passes** — a good demonstration of how earlier hard-won correctness work
  pays forward into later milestones that build on it.

## 🎯 Learning Objective Check

1. **What AI concept was this milestone intended to teach?** Semantic vs. keyword search; cosine
   similarity as the standard embedding-comparison metric; evaluating retrieval quality independent of
   generation quality; and why an in-process, brute-force vector store is the right architecture at
   this scale.
2. **Does the implementation expose that concept clearly?** Yes, and with real, independently-verified
   evidence rather than constructed examples — the `search` smoke test's non-keyword-matching query,
   the confirmed-genuine `PPTRH-7.5` miss, and the `TCGTH-6.2` ranking nuance are all real data, not
   illustrations.
3. **What should the developer be able to explain after completing this milestone?** Why cosine
   similarity is magnitude-invariant and what that buys you; why `eval`'s zero-LLM-call design is what
   "evaluable independent of the LLM" concretely means; a real example of retrieval succeeding on
   reworded language and a real example of it missing a specific-but-related section; and why deciding
   eval ground truth is itself a design decision with its own failure modes.
4. **Is any abstraction hiding something the developer should understand directly?** No. `search` and
   `eval` both print raw scores, chunk IDs, and text directly to the console; `RetrievalEvaluator`'s
   hit/miss/rank logic is a small, fully inspectable pure function with no framework indirection.

## 📋 Plan Completion

| Step | Status |
|---|---|
| 1. Load Milestone 4's chunked/embedded output | Complete |
| 2. Build cosine similarity | Complete, independently re-verified by hand |
| 3. Build the in-process vector store | Complete |
| 4. Wire the console `search` mode | Complete |
| 5. Design the hand-authored retrieval evaluation set | Complete, with a real, documented methodological limitation discovered along the way |
| 6. Build the evaluation runner | Complete |
| 7. Run the evaluation against real data | Complete — 6/7 hits, one independently-verified genuine miss |
| 8. Document observed retrieval-quality findings | Complete, and unusually strong — includes verification that the miss and the near-miss are both real, not artifacts of a flawed eval set |

## Final Verdict

**Ready to Complete**

No Must Fix items. This is a clean, well-scoped milestone that reused Milestone 4's already-hardened
abstractions correctly, produced real (not staged) evidence for its core learning objectives, and — most
notably — the developer's own observed-limitations write-up already caught and correctly characterized
the one subtlety (the `TCGTH-6.2` ranking nuance) that a less careful pass might have glossed over as a
clean hit. Of the two Consider-Improving items, the zero-vector-chunk failure mode is now fixed
(fail-fast at load time, named chunks, friendly console guidance); the eval-set methodology limitation
remains open by design, correctly deferred to Milestone 8.
