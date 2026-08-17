# Milestone 6 — RAG

Status: planned, not started
Source: PRD.md §7-8 (Functional/AI-Specific Requirements), §9 (Reliability/Safety), §11 (architecture,
retrieve → assess → clarify → re-retrieve loop), §18 (open questions — low-confidence retrieval,
deferred here), "Learning Objectives → Milestone 6"

## 1. What We Will Build

Milestone 5 built a working vector store and proved retrieval quality is measurable on its own, but
nothing in the live application uses it yet — the console's default flow still runs Milestone 2's
`ClarificationLoop` against a fixed, hand-authored `MockCorpus` of three canned scenarios. Milestone 6
retires that mock corpus and rebuilds the loop as the real thing per PRD §11's diagram:

```
retrieve → assess → clarify → re-retrieve → ... → generate → (Source Support, first pass)
```

Concretely:

1. **Retire `MockCorpus`.** The judge now types a free-text scenario description (PRD FR1) instead of
   picking from three canned options. `MockCorpus.cs`, `PolicySnippet`, `MockScenario`, and
   `MockCorpusTests.cs` are deleted — nothing else will depend on them once this milestone lands.
2. **A small `IRetriever` abstraction** wrapping Milestone 5's `IEmbeddingClient` + `InMemoryVectorStore`
   behind one method (`RetrieveAsync(queryText, topK)`), for the same reason `ILlmClient` exists: so
   `ClarificationLoop`'s new retrieval-in-the-loop control flow can be unit tested deterministically via a
   stub, without going through real embeddings.
3. **`ClarificationLoop` rebuilt to retrieve every turn.** Each turn: build a retrieval query from the
   original scenario description plus all confirmed facts gathered so far, retrieve the current top-K
   chunks, and assess sufficiency against *those* — not a fixed mock corpus. New facts can surface
   previously-unretrieved passages (PRD §8 "Iterative retrieval"), so retrieval genuinely re-runs, not
   just sufficiency.
4. **Ruling generation as its own explicit step**, matching PRD §11's diagram (a separate box from
   sufficiency/clarification) and FR7 ("once sufficient, finalize retrieval against the complete
   accumulated scenario and generate a structured response"). Once the loop reports sufficient, one more
   retrieval call runs against the full accumulated scenario, and a new `RulingGenerator` produces a
   structured `RulingResult`: recommendation, explanation, repair steps, penalty guidance, cited chunk
   IDs, and a **first-pass Source Support classification** (Strong/Partial/Insufficient) — the model's own
   judgment against the PRD §8 rubric supplied in the prompt, not yet independently validated against
   deterministic criteria (that check is explicitly Milestone 7's job).
5. **Console wiring**: free-text scenario entry, live per-turn visibility into what was retrieved (chunk
   IDs, section, score) alongside each sufficiency assessment, and a final structured print of the ruling
   result — or, if the turn cap is exhausted without sufficiency, an explicit refusal to rule (PRD §9: must
   not issue a ruling when facts are still flagged missing).

Still one project, no new folders beyond what Milestone 5 already introduced — `RulingResult` lives in
`StructuredState/` alongside `ClarificationResult`/`FactExtractionResult`; `RulingGenerator` lives in
`Clarification/` alongside `ClarificationLoop`, since it's the loop's natural terminal step, not a new
subsystem.

### Reusing, not duplicating, Milestones 4-5's building blocks

`IRetriever`'s production implementation (`VectorStoreRetriever`) is a thin wrapper: embed the query via
the existing `IEmbeddingClient`/`GeminiEmbeddingClient`, then call the existing `InMemoryVectorStore`
unchanged. No new embedding logic, no new similarity math, no new document loading.

## 2. AI Concepts Being Learned

- **Combining retrieval and generation end-to-end.** Prompt construction now assembles retrieved context
  at multiple points (sufficiency assessment, ruling generation) rather than a single static corpus —
  the actual mechanics of "the model was told this" instead of "the model knows this."
- **Iterative retrieval as a structural property of the loop, not an add-on.** Retrieval runs before the
  first clarifying question and again after every new confirmed fact, because materiality is now
  genuinely text-derived from real, changing retrieved content (PRD §8).
- **Ruling generation as a distinct step from sufficiency**, per PRD §11's architecture — not something
  the sufficiency call quietly also does. Separating them makes each prompt's job legible on its own.
- **Source Support as a first-pass, model-assigned label** — introduced here as an output of generation,
  deliberately *not yet* checked against observable criteria (retrieval success, citation coverage, fact
  sufficiency, source conflict). Seeing what an unvalidated model-assigned label looks like in practice is
  the direct, concrete setup for why Milestone 7 formalizes it.
- **What "low-confidence retrieval" actually looks like**, deferred from Milestone 5 to here per PRD §18:
  observing a genuinely out-of-corpus scenario surface uniformly weak similarity scores, and watching how
  (or whether) that gets reflected in the final Source Support label — a real problem to observe, not one
  to pre-design a fix for.

## 3. Implementation Steps (in order)

1. **Delete `MockCorpus.cs`, `PolicySnippet`, `MockScenario`, and `MockCorpusTests.cs`.** Confirm nothing
   else references them (only `ClarificationLoop`, `PromptBuilder`, and their tests do today).
2. **Add `IRetriever`** (`Retrieval/IRetriever.cs`): `Task<IReadOnlyList<ScoredChunk>> RetrieveAsync(string
   queryText, int topK)`. Add `VectorStoreRetriever` implementing it over an injected `IEmbeddingClient` +
   `InMemoryVectorStore`. Add `StubRetriever` test double (queue-based, mirroring `StubLlmClient`) in
   `PokeJudge.Tests/TestDoubles/`.
3. **Add a pure, testable retrieval-query builder** (`Retrieval/RetrievalQueryBuilder.cs`): given the
   original scenario description and the current list of confirmed facts, build the text to embed for the
   next retrieval call. Grows every turn as facts accumulate; no LLM or I/O involved.
4. **Rewrite `PromptBuilder.BuildSufficiencyPrompt`** to accept a scenario description string and a list
   of retrieved `ScoredChunk`s instead of `MockScenario.Snippets`, formatting each as
   `[ChunkId] (SectionId) text` — the model reasons only over these, exactly as before, just with real
   retrieved content instead of a fixed mock list.
5. **Simplify `ClarificationResult`/`ClarifyingQuestion`**: drop the embedded `Draft` field (ruling
   generation is now its own step); rename `RelatedSnippetId` → `RelatedChunkId` to match what's actually
   being cited. Update the JSON schema and `SystemPrompts.Judge` to match (retrieved passages, not
   supplied snippets; still "reason only over the supplied text, never pretrained knowledge").
6. **Rewrite `ClarificationLoop.RunAsync`**: accept a plain scenario description string. Each turn: build
   the retrieval query (step 3), retrieve top-K via `IRetriever` (default topK: 5, matching Milestone 5),
   assess sufficiency against those chunks + `GameState`. If insufficient, ask/extract exactly as
   Milestone 2 did. If sufficient, stop — return `ClarificationOutcome(Sufficient, State, TurnsUsed)` with
   no draft. Extend the `onAssessment` callback to also expose the turn's retrieved chunks, so the console
   can show what was actually retrieved at each step (grounding visibility, not just asserted).
7. **Add `RulingResult` + `SourceSupport` enum + JSON schema** (`StructuredState/RulingResult.cs`):
   recommendation, explanation, repair steps, penalty guidance (nullable), cited chunk IDs, Source
   Support (Strong/Partial/Insufficient), and a short rationale string for the label.
8. **Add `RulingGenerator`** (`Clarification/RulingGenerator.cs`) + `SystemPrompts.RulingGeneration` +
   `PromptBuilder.BuildRulingPrompt`: takes the scenario, confirmed facts (never hypotheses — PRD's "no
   material inference"), and a final set of retrieved chunks, produces a `RulingResult`. System prompt
   restates PRD §8's Strong/Partial/Insufficient definitions directly so the model's classification has an
   explicit rubric to work against, and repeats the "ground every claim in the supplied passages, never
   pretrained knowledge" instruction from `SystemPrompts.Judge`.
9. **Wire the console default flow** in `Program.cs`: prompt for a free-text scenario description; run the
   rebuilt loop, printing each turn's retrieved chunks and assessment; on sufficiency, run one final
   retrieval against the complete accumulated scenario (description + all confirmed facts) and call
   `RulingGenerator`; print the full structured ruling. On turn-cap exhaustion, print an explicit
   "insufficient — no ruling produced" message instead of attempting generation (PRD §9).
10. **Run the real end-to-end flow** against the real 258-chunk corpus for at least three scenarios: one
    well-covered by the ingested documents, one requiring at least one clarifying round-trip to observe
    re-retrieval actually changing the retrieved set, and one deliberately picked to sit outside what
    either document covers well, to directly observe the deferred low-confidence-retrieval question.
11. **Update tests**: new `RetrievalQueryBuilderTests`, updated `PromptBuilderTests` (chunk-shaped input),
    rewritten `ClarificationLoopTests` (string scenario, `StubRetriever`, no embedded draft, asserting
    retrieval happens each turn with the growing query), new `RulingGeneratorTests`/prompt-builder tests
    following the same stub-based pattern. Delete `MockCorpusTests.cs`.
12. **Document observed findings** (evidence-based, same style as Milestones 1-5) in
    `observed-limitations.md`: real re-retrieval changing results after a confirmed fact, the low-
    confidence out-of-corpus case, any instance of the model leaking pretrained knowledge despite
    instructions, and a candid assessment of the unvalidated, model-assigned Source Support label.

## 4. Expected Limitations / Failures to Intentionally Observe

- **Retrieval mistakes now propagate further than Milestone 5's standalone eval report** — a wrong or
  topically-adjacent retrieved chunk can shape a clarifying question or even a final ruling, not just show
  up as a miss in an eval score. Worth catching at least one concrete instance.
- **Low-confidence / out-of-corpus retrieval is a real, unresolved problem here, not pre-solved.** No
  similarity threshold or minimum-passage-count gate exists yet (PRD §18 explicitly defers designing that
  until this milestone surfaces it as a concrete problem) — expect to observe the system attempt
  sufficiency/generation against weak, barely-relevant retrieved passages, and to document what actually
  happens rather than assuming a graceful degradation.
- **Source Support is model-assigned and unvalidated in this milestone.** Nothing yet checks whether the
  label the model outputs actually matches observable retrieval/citation/fact-sufficiency conditions —
  worth deliberately checking whether the model over- or under-calls "Strong" at least once.
- **More LLM/embedding calls per scenario than Milestone 2's static flow.** An embedding call now happens
  every turn (query grows with confirmed facts) plus one more before ruling generation — real, observable
  latency/cost growth worth noting, not optimized away here.
- **No query rewriting or re-ranking still** — Milestone 5's brute-force cosine search is unchanged;
  Milestone 6 only wires it into the loop, it doesn't improve it.

## 5. What I Should Understand by the End

- How retrieved context actually gets assembled into a prompt at each of the loop's two distinct model
  steps (sufficiency/clarification vs. ruling generation), and why those are separate calls rather than
  one call doing both.
- A concrete, observed example of iterative retrieval mattering — a new confirmed fact changing which
  chunks come back on the next turn.
- What "low-confidence retrieval" looks like in practice against this project's real corpus, and why
  designing a fix for it before seeing a real instance would have been premature (PRD §18).
- Why a first-pass, model-assigned Source Support label is not yet a trustworthy reliability signal, and
  specifically what would need to be added (Milestone 7) to make it one.

## Out of Scope for This Milestone

- Formal, criteria-based Source Support validation (retrieval success / citation coverage / fact
  sufficiency / source conflict checks) — Milestone 7.
- Claim-level citation grounding checks (does each specific sentence trace to a cited passage) —
  Milestone 7.
- Designing or implementing a similarity-threshold / minimum-passage-count gate for low-confidence
  retrieval — observe the problem here; a considered fix is Milestone 7+ territory.
- A formal evaluation harness or branching/trajectory evaluation — Milestone 8.
- Query rewriting, re-ranking, or hybrid keyword+vector search.
- Any UI work (Milestone 10).
