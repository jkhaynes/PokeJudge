# Milestone 6 — Implementation Summary

**Milestone:** Milestone 6 — RAG
**Branch:** `milestone/6-rag`

## Milestone Implemented

Milestone 6 — RAG (PRD roadmap #6). Ruling generation is now conditioned on retrieved chunks +
accumulated game state, and the Milestone 2 clarification loop is rebuilt as the retrieve → assess →
clarify → re-retrieve loop from PRD §11, replacing mock-corpus-based materiality with retrieval-grounded
materiality. A first-pass Source Support classification (Strong/Partial/Insufficient) is introduced as an
output of ruling generation.

## What Changed

- **Retired `MockCorpus`.** Deleted `MockCorpus.cs`, `PolicySnippet`, `MockScenario`, and
  `MockCorpusTests.cs`. The judge now types a free-text scenario description (PRD FR1) instead of picking
  from three canned options.
- **`Retrieval/IRetriever.cs` + `Retrieval/VectorStoreRetriever.cs`** — a small seam over "embed the query,
  then search the vector store," letting `ClarificationLoop`'s new per-turn retrieval be unit tested via a
  `StubRetriever` (mirrors `ILlmClient`/`StubLlmClient`). Production implementation is a thin wrapper
  around Milestone 4's `IEmbeddingClient` and Milestone 5's `InMemoryVectorStore`, unchanged.
- **`Retrieval/RetrievalQueryBuilder.cs`** — pure function building the text embedded for each retrieval
  call: scenario description plus all confirmed facts so far, growing every turn.
- **`ClarificationLoop` rebuilt** to retrieve every turn (not just assess against a fixed corpus): builds
  the retrieval query, retrieves top-K (default 5) via `IRetriever`, then assesses sufficiency against
  those chunks. `onAssessment` now also exposes the turn's retrieved chunks for console visibility.
  `ClarificationOutcome` no longer carries a draft ruling — sufficiency is now a pure stop signal.
- **`ClarificationResult`/`ClarifyingQuestion` simplified**: dropped the embedded `Draft` field; renamed
  `RelatedSnippetId` → `RelatedChunkId` to match what's actually being cited (a specific retrieved chunk,
  not a mock-corpus snippet).
- **`StructuredState/RulingResult.cs`** (new) — `RulingResult` record + `SourceSupport` enum
  (Strong/Partial/Insufficient) + JSON schema. `StructuredResponseParser` gained a `JsonStringEnumConverter`
  to deserialize the enum from the schema-constrained string.
- **`Clarification/RulingGenerator.cs`** (new) — ruling generation as its own explicit LLM step, per PRD
  §11's architecture diagram, rather than something the sufficiency call also produces. Builds its prompt
  from the scenario, confirmed facts (never hypotheses), and a final retrieved chunk set.
- **`SystemPrompts.Judge`** updated for retrieval-grounded language (retrieved passages, not "supplied
  snippets"; retrieval is explicitly imperfect); the sufficiency step no longer produces a draft. New
  **`SystemPrompts.RulingGeneration`** restates PRD §8's Strong/Partial/Insufficient rubric directly in the
  prompt.
- **`Program.cs` default flow rewired**: free-text scenario entry; prints retrieved chunks + scores at
  every turn; on sufficiency, runs one more retrieval against the complete accumulated scenario (PRD FR7)
  and calls `RulingGenerator`; prints the full structured ruling. On turn-cap exhaustion, prints an
  explicit "no ruling produced" message rather than attempting generation (PRD §9).

No new NuGet packages, no new projects — everything lives in the existing `Retrieval/`, `Clarification/`,
and `StructuredState/` folders.

## Validation

- **Build:** `dotnet build` — succeeded, 0 warnings, 0 errors.
- **Tests:** `dotnet test` — 108/108 passed (99 carried over from Milestones 1-5, 9 new).
  - **Written first (Red step), all deterministic:** `RetrievalQueryBuilderTests` (empty facts, growing
    query), `VectorStoreRetrieverTests` (embeds then delegates to the store, respects topK), rewritten
    `ClarificationLoopTests` (retrieval happens every turn, query grows with confirmed facts, callback
    exposes retrieved chunks, turn cap / multi-question / insufficient-without-questions behavior
    preserved), rewritten `PromptBuilderTests` (chunk-shaped sufficiency prompt, new `BuildRulingPrompt`,
    hypotheses labeled "not confirmed"), new `RulingGeneratorTests` (returns what the LLM client produces,
    sends scenario/facts/chunks to the model), and `StructuredResponseParserTests` additions (enum
    deserialization, nullable `penaltyGuidance`).
  - **Final coverage-review pass:** no additional deterministic logic surfaced beyond what was anticipated
    when the tests were written first; `Program.cs` console I/O and `SystemPrompts` constants remain
    intentionally untested, consistent with prior milestones.
- **Real-data validation** (see `observed-limitations.md` for full detail): ran the rebuilt default flow
  against the real 258-chunk corpus for three scenario types —
  1. A well-covered scenario resolved in one turn with Source Support **Strong**, correctly grounded and
     cited.
  2. A deliberately vague scenario triggered a real clarifying round-trip; the **retrieved chunk set
     genuinely changed** between turn 1 and turn 2 after a fact was confirmed (iterative retrieval
     demonstrated, not just claimed), and the final ruling correctly self-assessed **Partial** support.
  3. A weakly-covered scenario (missed-Prize discovery timing) reproducibly retrieved only
     topically-adjacent content and caused the model to return a malformed `isSufficient: false` response
     with zero clarifying questions — correctly caught by Milestone 2's existing "fail loudly" guard
     (`InvalidOperationException`, not a silent guess), directly observing the low-confidence-retrieval
     problem PRD §18 deferred to this milestone.

## Intentional Limitations

- **Source Support is purely model-assigned, not criteria-validated.** Nothing checks the model's label
  against retrieval scores, citation coverage, or fact sufficiency yet — Milestone 7's explicit job.
- **No low-confidence-retrieval gate exists.** Observation 3 above shows a concrete failure mode (weak,
  topically-adjacent retrieval producing a malformed sufficiency response) with no similarity threshold or
  fallback in place — deliberately left unfixed so the design work that follows is based on real evidence.
- **Retrieval mistakes now propagate further than Milestone 5's standalone eval.** A wrong or borderline
  chunk can shape a clarifying question or a final ruling, not just show up as an eval miss.
- **More LLM/embedding calls per scenario than Milestone 2's static flow** — an embedding call happens
  every turn plus one more before ruling generation; not optimized here.
- **No query rewriting or re-ranking** — Milestone 5's brute-force cosine search is unchanged; this
  milestone only wires it into the loop.

## Learning Focus

- **Combining retrieval and generation end-to-end**: prompt construction now assembles real retrieved
  context at two distinct model steps (sufficiency/clarification vs. ruling generation) instead of a fixed
  mock corpus — the concrete difference between "the model knows this" and "the model was told this."
- **Iterative retrieval as a structural loop property**, demonstrated with a real before/after chunk-set
  comparison, not asserted.
- **A first-pass, unvalidated Source Support label**, observed producing both a well-justified Strong and a
  well-justified Partial classification on real data — useful evidence that the model's own judgment is
  often reasonable, and exactly why it still needs independent validation before being trusted as a
  reliability signal.
- **What "low-confidence retrieval" actually looks like** against this project's real corpus, observed
  directly rather than pre-designed around, per PRD §18's explicit framing.

## What I Should Try

1. Re-run the missed-Prize scenario from `observed-limitations.md` yourself and confirm it reproduces —
   then think about what should happen instead: a similarity threshold, a guaranteed fallback clarifying
   question, or something else. This is genuinely open; Milestone 6 intentionally didn't design an answer.
2. Try a scenario that needs *two* clarifying round-trips and inspect whether the retrieved chunk set keeps
   changing meaningfully on the third turn, or converges.
3. Read `RulingGenerator`'s prompt for the Special Condition scenario in `observed-limitations.md` and
   decide for yourself whether you'd have called that ruling Partial or Strong — a good exercise for
   Milestone 7's criteria-based rubric.
4. Try to get the model to leak pretrained Pokémon knowledge not present in the retrieved passages (e.g.,
   ask about a rule you know isn't in either document) and see whether the "ground only in retrieved
   passages" instruction actually holds.

## Git Status

- **Branch:** `milestone/6-rag`
- **Uncommitted:** yes — all implementation changes are in the working tree, nothing staged or committed
  yet (this skill does not commit automatically).
- **Unexpected files:** none. `git status` shows exactly the expected changes: modified
  `PokeJudge/Program.cs`, `AI/StructuredResponseParser.cs`, and the `Clarification/`/`StructuredState/`
  files touched by this milestone; new `Retrieval/IRetriever.cs`, `Retrieval/VectorStoreRetriever.cs`,
  `Retrieval/RetrievalQueryBuilder.cs`, `StructuredState/RulingResult.cs`, `Clarification/RulingGenerator.cs`,
  their tests, and `.project-plans/milestone-6/`; deleted `MockCorpus.cs` and `MockCorpusTests.cs`.
