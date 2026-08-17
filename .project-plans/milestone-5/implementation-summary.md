# Milestone 5 — Implementation Summary

**Milestone:** Milestone 5 — Vector Search
**Branch:** `milestone/5-vector-search`

## What Changed

Added vector similarity search and a small retrieval evaluation set on top of Milestone 4's
chunked/embedded output, still one console project:

- **`PokeJudge/Retrieval/`**:
  - `VectorMath.cs` — pure `CosineSimilarity(float[], float[])`, throwing clearly on mismatched
    vector lengths or a zero vector rather than silently dividing by zero.
  - `InMemoryVectorStore.cs` — brute-force linear-scan similarity search over chunks loaded into
    memory (`ScoredChunk(EmbeddedChunk, Score)`), no vector database, no ANN indexing.
  - `RetrievalEvaluator.cs` — deterministic hit/miss/rank checking against already-computed search
    results, with zero embedding or chat calls of its own.
  - `RetrievalEvalSet.cs` — 7 hand-authored judge-scenario queries paired with ground-truth section
    IDs, grounded in the real ingested documents (inspected directly before writing them).
- **`PokeJudge/Program.cs`** — added `search <query text>` (embed a query, print the top-5 most
  similar chunks) and `eval` (run `RetrievalEvalSet` against the same vector store, report hit/miss
  per case and an overall score) console modes. Both need the Gemini API key (embedding is a real
  model call) and reuse Milestone 4's `IEmbeddingClient`/`GeminiEmbeddingClient` unchanged — no new
  embedding abstraction. **Intentional deviation from the plan:** `plan.md` specified
  `search <document-code(s)> <query>` (filterable by document); the shipped `search <query text>`
  always searches across every loaded document instead, with no code-filtering. This was a deliberate
  scope simplification, not an oversight — searching the whole corpus is arguably more realistic for a
  judge who doesn't know in advance which document covers their scenario.

No new NuGet packages, no `.gitignore` changes needed (vector search output is printed to the console,
not written to a new file).

## Validation

- **Build:** `dotnet build` — succeeded, 0 warnings, 0 errors.
- **Tests:** `dotnet test` — 96/96 passed (77 carried over from Milestones 1-4, unchanged, plus 19 new).
  - **Written first (Red step), all deterministic:** `VectorMathTests` (identical/orthogonal/opposite
    vectors, magnitude invariance, mismatched-length and zero-vector failure cases),
    `InMemoryVectorStoreTests` (descending-similarity ranking, `topK` larger/smaller than the
    collection, empty store, `topK: 0`), `RetrievalEvaluatorTests` (hit at various ranks, miss,
    multiple chunks from the expected section reporting the earliest rank, empty results), and
    `RetrievalEvalSetTests` (case count, non-empty fields, coverage of both real documents).
  - `GeminiEmbeddingClient`'s network calls and `Program.cs`'s console I/O remain intentionally
    untested, consistent with prior milestones' boundary-testing precedent.
- **Real-data validation:** ran both `search` and `eval` against the real 258-chunk corpus.
  `search "can I take written notes during my match"` returned the exact correct chunk
  (`TCGTH-7.4.6#0`) at rank 1 with a deliberately non-keyword-matching query. `eval` scored **6/7**
  hits within top-5 against the hand-authored eval set — including one genuine miss and one
  methodologically-interesting near-miss (hit at rank 3 because two legitimately-relevant sibling
  subsections outranked the exact expected section). Full transcript and analysis in
  `observed-limitations.md`.

## Intentional Limitations

- **Retrieval quality is not perfect, and a real miss was observed, not just anticipated.** The
  "penalties for repeat violations" query never retrieved its expected section within the top 5,
  instead returning topically-adjacent-but-wrong penalty content — the direct, concrete motivation
  for Milestone 6's iterative retrieval and Milestone 8's formal evaluation.
- **The evaluation methodology itself has a real limitation, discovered during this milestone**: a
  single expected `SectionId` per eval case can't represent that more than one section may
  legitimately answer the same judge question (observed directly with `TCGTH-6.2` and its two
  sibling subsections). Worth revisiting if a larger evaluation set is built later; not attempted here.
- **Brute-force linear scan doesn't scale past a certain corpus size** — correct and fast at 258
  chunks, deliberately not replaced with indexing or a dedicated vector store until an actual scale
  need exists (PRD §12).
- **No sufficiency/materiality reasoning happens here.** Vector search returns "most similar chunks,"
  not "enough information to rule." The Milestone 1-2 mock-corpus-based clarification loop remains
  untouched and still the only source of context for actual judge interactions until Milestone 6.
- **The eval set is small (7 queries)** — a real, concrete signal, but not statistically rigorous.
  Formalizing this is explicitly Milestone 8's job.

## Learning Focus

- **Semantic vs. keyword search, demonstrated with a real, deliberately non-matching query** — not
  just asserted as a property embeddings have in theory.
- **Cosine similarity as a geometric alignment score**, confirmed via unit tests to be
  magnitude-invariant and to fail clearly (not silently) on a zero vector — echoing Milestone 4's
  "fail visibly" discipline one layer up.
- **Retrieval quality is measurable independent of generation quality**, demonstrated concretely:
  `eval` never calls the chat/completion model, only the embedding client, and still produces a real,
  interpretable pass/fail signal per query.
- **Why an in-process, brute-force vector store is the right choice right now** — validated, not just
  asserted: 258 chunks searched with no perceptible latency beyond the embedding API round-trip.
- **Evaluation methodology has its own failure modes**, distinct from the system being evaluated — a
  transferable lesson for Milestone 8's more formal harness: deciding what counts as "correct" is
  itself a design decision that can be wrong in ways that don't show up until real data is run through it.

## What I Should Try

1. Run `dotnet run --project PokeJudge -- search "<your own judge scenario question>"` with a question
   in your own words, phrased nothing like either document's actual text, and see whether the top
   result is the section you'd expect.
2. Look at the `TCGTH-6.2` eval case's full top-5 output and decide for yourself: should `TCGTH-6.2.1`
   or `TCGTH-6.2.2` have counted as a correct answer too? This is a genuine, unresolved methodology
   question worth forming your own opinion on before Milestone 8 formalizes evaluation.
3. Try writing one or two of your own eval cases (a query + expected `SectionId`, added to
   `RetrievalEvalSet`) targeting content Milestone 4 flagged as an oversized chunk (e.g., something
   inside `PPTRH-3.3`) — does a chunk that absorbed an entire bulleted list retrieve well, or does its
   diluted embedding actually hurt it? `observed-limitations.md` notes this wasn't tested directly.
4. Re-run `eval` a second time and compare the exact similarity scores against the first run — since
   embeddings are deterministic for identical input (verified in Milestone 4), the scores should be
   byte-for-byte identical; worth confirming that expectation yourself.

## Git Status

- **Branch:** `milestone/5-vector-search`
- **Uncommitted:** yes — all implementation changes are in the working tree, nothing staged or
  committed yet (this skill does not commit automatically).
- **Unexpected files:** none. `git status` shows exactly the expected changes: new `PokeJudge/Retrieval/`
  and `PokeJudge.Tests/Retrieval/` folders, modified `PokeJudge/Program.cs`, plus
  `.project-plans/milestone-5/` (plan, this summary, observed-limitations).
