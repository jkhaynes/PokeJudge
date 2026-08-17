# Milestone 4 — Implementation Summary

**Milestone:** Milestone 4 — Chunking and Embeddings
**Branch:** `milestone/4-chunking-embeddings`

## What Changed

Added a chunking + embedding pipeline that consumes Milestone 3's ingested output and prepares it for
retrieval, still one console project:

- **`PokeJudge/Chunking/`**:
  - `TextChunk.cs` — `TextChunk`, `EmbeddedChunk`, and `ChunkedDocument` records. `ChunkId` extends
    `IngestedSection.SectionId`'s scheme (e.g. `PPTRH-3.3#0`) so a chunk is always traceable back to
    its section-level citation.
  - `TextChunker.cs` — a small, pure, sentence-boundary-aware chunker: greedily packs sentences into a
    chunk until the next sentence would exceed the target size, then starts a new chunk carrying
    forward the last N sentences of the previous one for overlap. A single sentence longer than the
    target size is not split further (a deliberate simplification).
  - `ChunkingPipeline.cs` — orchestrates chunking + embedding, decoupled from the real embedding
    client (mirrors Milestone 2/3's pattern of injecting the untestable network boundary). Resumable:
    skips any `ChunkId` already present in a supplied `alreadyEmbedded` dictionary, batches the rest,
    and reports cumulative progress via an `onProgress` callback after every batch.
- **`PokeJudge/AI/`**: `IEmbeddingClient` (a second, differently-shaped provider abstraction —
  text-in/vector-out — alongside `ILlmClient`), and `GeminiEmbeddingClient`, implemented against
  `gemini-embedding-001`'s real `batchEmbedContents` endpoint (confirmed directly against the live API
  before writing any code), requesting a reduced `outputDimensionality: 768` for more compact storage.
- **`PokeJudge/Program.cs`** — added a `chunk <document-code>` console mode: loads a previously-ingested
  document, loads any existing chunk output for resumability, runs the pipeline, prints a raw-section-
  vs-chunks sample, and saves progress to a local JSON file **after every batch**, not just at the end
  (see "A real gap, found and fixed" below).
- **`.gitignore`** — added `PokeJudge/Chunking/Output/`, same copyright-hygiene reasoning as
  Milestone 3's `Ingestion/Output/`.

### A real gap, found and fixed during implementation

The approved plan's step 6 explicitly called for output "written incrementally enough that a partial
run... leaves a valid, resumable file." The first implementation pass didn't actually do this —
`Program.cs` only wrote output once, after the whole pipeline run completed. Running the pipeline
against the real `PPTRH` document immediately hit a live free-tier rate limit, and because nothing had
been saved incrementally, the first interruption lost all progress. Fixed by adding an `onProgress`
callback to `ChunkingPipeline.RunAsync` (invoked with a cumulative snapshot after every successful
batch) and having `Program.cs` re-save the output file on each call — covered by a new test
(`RunAsync_OnProgressCallback_FiresAfterEachBatchWithCumulativeSnapshotSoFar`) and then validated for
real: a subsequent run picked up exactly where an interrupted one left off. See
`observed-limitations.md` for the full account.

## Validation

- **Build:** `dotnet build` — succeeded, 0 warnings, 0 errors.
- **Tests:** `dotnet test` — 71/71 passed (69 carried over from Milestones 1-3, unchanged, plus 15 new
  for chunking/embedding, 13 written first and 2 added mid-implementation for the progress-callback
  fix above).
  - **Written first (Red step), all deterministic:** `TextChunkerTests` (short-text single-chunk,
    sentence-boundary splitting, overlap behavior, no-overlap behavior, an oversized single sentence
    not being split further, empty input, and text with no trailing punctuation), `ChunkingPipelineTests`
    (no-already-embedded case, resumability skipping already-embedded chunks, preserved vector for
    already-embedded chunks, document-order preservation, and multi-batch splitting via a scripted
    `StubEmbeddingClient`), and `EmbeddedChunkSerializationTests` (JSON round-trip, comparing fields
    directly rather than whole records to avoid the array/List reference-equality pitfall).
  - **Added during implementation, once the real-run gap was found:**
    `RunAsync_OnProgressCallback_FiresAfterEachBatchWithCumulativeSnapshotSoFar` and
    `RunAsync_OnProgressCallback_NotProvided_StillWorksNormally`.
  - `GeminiEmbeddingClient` and `Program.cs`'s `RunChunking` console/file I/O remain intentionally
    untested, consistent with prior milestones' boundary-testing precedent.
- **Real-document validation, both documents, including a real interruption:** ran the pipeline against
  both of Milestone 3's ingested documents. `PPTRH` hit the free-tier rate limit on its first attempt
  (zero chunks saved, the gap above), was fixed, and a follow-up run correctly resumed from 100
  already-saved chunks to complete all 162. `TCGTH` similarly required one retry after a brief wait and
  completed all 96 chunks. Full transcript and analysis in `observed-limitations.md`.

## Intentional Limitations

- **Chunking quality is inspected, not measured**, exactly as planned — there's no retrieval or
  evaluation yet to say whether the chosen chunk size/overlap actually helps or hurts retrieval.
- **A single oversized sentence is not split further — and this was directly observed to matter, not
  just a theoretical risk.** Bulleted/nested list content (`•`, `o` markers) frequently carries no
  terminal `.`/`!`/`?` punctuation, so an entire multi-item list gets treated as one giant "sentence."
  In the real output, this produced the single largest chunk in the dataset (`PPTRH-3.3#3`, 2043
  characters — 2.5x the 800-character target), 6 more `PPTRH` chunks over 1200 characters, and the
  same pattern in the second document (`TCGTH-4.1.1#0`, 1708 characters). See
  `observed-limitations.md`'s "Chunking quality: the oversized-single-sentence simplification was
  observed to matter" for the full evidence. Reasonably out of scope to fix this milestone (bullet-aware
  sentence splitting is real added complexity for a "start simple" first pass), but the citation
  metadata at the section level remains correct regardless — only the chunk's *size* is affected.
- **The free-tier embedding quota's real behavior (counting items within a batch, not just outer
  calls) means batching alone doesn't fully solve rate-limit exposure** — the resumable design is the
  actual mitigation, demonstrated working in practice, not just batching by itself.
- **No vector storage or similarity search yet.** `ChunkedDocument` output is structured data in local
  JSON files; nothing can search over it yet. Milestone 5's job.
- **Embeddings are static snapshots.** If Milestone 3's ingestion output changes in the future (e.g., a
  citation-granularity improvement), embeddings would need regenerating — no staleness detection exists.

## Learning Focus

- **What an embedding actually is**, confirmed directly against the real API: a fixed-size dense
  vector (768 floats, requested via `outputDimensionality` from a native 3072-dimensional model) with
  no relationship to anything the chat/completion model produces.
- **Chunk size and overlap as a real, consequential design decision** — implemented as a small, pure,
  independently testable function precisely so its behavior (sentence-boundary packing, overlap
  carry-forward, the oversized-sentence edge case) is easy to reason about and verify.
- **A second provider-abstraction shape**: `IEmbeddingClient` (text-in/vector-out) sitting alongside
  `ILlmClient` (prompt-in/structured-JSON-out) — same swappable-behind-an-interface pattern, genuinely
  different call shape.
- **Free-tier cost engineering as a real, not hypothetical, constraint**: the batch-size assumption
  made in planning turned out to be wrong in a specific, discoverable way once tested against the real
  API, and the resumable design (built in anticipation of exactly this) is what actually made the
  mistake recoverable rather than catastrophic.
- **The value of running real integration validation, not just unit tests with a stub.** The
  incremental-save gap was invisible to the test suite (which never exercises a failing embedding call)
  and only surfaced by actually running the pipeline against real data under real network conditions.

## What I Should Try

1. Open `PokeJudge/Chunking/Output/PPTRH.chunks.json` and find the chunks belonging to `PPTRH-3.3`
   (the section Milestone 3 found embeds 11 real subsections in one block of text) — see how the
   chunker split that unusually long section, and judge for yourself whether any chunk boundary lands
   somewhere awkward.
2. Re-run `dotnet run --project PokeJudge -- chunk PPTRH` again now that both documents are fully
   embedded — confirm it reports "Already embedded: 162 chunk(s)" and makes zero new embedding calls,
   demonstrating the resumability design costs nothing extra once a document is done.
3. Try deleting a single chunk entry from `TCGTH.chunks.json` by hand, then re-run `chunk TCGTH` — confirm
   only that one chunk gets re-embedded, not the whole document.
4. Compare a 768-dimensional embedding vector's raw values (open the JSON, look at one `Embedding`
   array) against another chunk's — they won't mean much to the eye, which is itself worth sitting
   with: an embedding's usefulness only shows up in comparison (cosine similarity, etc.), which is
   exactly what Milestone 5 introduces.

## Git Status

- **Branch:** `milestone/4-chunking-embeddings`
- **Uncommitted:** yes — all implementation changes are in the working tree, nothing staged or
  committed yet (this skill does not commit automatically).
- **Unexpected files:** none. `git status` shows exactly the expected changes: new `PokeJudge/Chunking/`,
  `PokeJudge/AI/IEmbeddingClient.cs`, `PokeJudge/AI/GeminiEmbeddingClient.cs`, and
  `PokeJudge.Tests/Chunking/` plus `PokeJudge.Tests/TestDoubles/StubEmbeddingClient.cs`, modified
  `PokeJudge/Program.cs` and `.gitignore`, plus `.project-plans/milestone-4/` (plan, this summary,
  observed-limitations). The real ingested/chunked JSON output stays correctly excluded by `.gitignore`.
