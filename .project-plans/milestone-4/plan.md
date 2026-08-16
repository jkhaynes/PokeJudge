# Milestone 4 — Chunking and Embeddings

Status: planned, not started
Source: PRD.md §7-8 (Functional/AI-Specific Requirements), §11 (architecture progression), §12 (tech stack), "Learning Objectives → Milestone 4"

## 1. What We Will Build

Milestone 3 produced `IngestedDocument`/`IngestedSection` records — real, citable text, but at
section granularity (a few sentences to several thousand characters per section, per the milestone 3
finding that citation granularity is bounded by each document's own Table of Contents depth).
Milestone 4 takes that output and prepares it for retrieval:

1. A **chunking step**: split each `IngestedSection.Text` into smaller, fixed-size text chunks (with
   configurable overlap), since embedding models work best over focused spans of text, not
   multi-thousand-character blocks that mix several real subsections together.
2. An **embedding-generation step**: for each chunk, call a provider embedding API to get a dense
   vector representing its semantic content — batched where the provider API supports it, to keep
   request counts (and free-tier rate-limit exposure) down.
3. A **chunk + embedding storage step**: persist each `(chunk text, embedding vector, citation
   metadata)` tuple to a local, gitignored JSON file — still no vector database and no similarity
   search. That's explicitly Milestone 5's job (PRD §14: "Vector search... Introduced at Milestone 5").
   This milestone only produces the data Milestone 5 will index. The pipeline is resumable: re-running
   it skips chunks that already have a stored embedding.
4. A **console entry point** to run chunking + embedding against Milestone 3's already-ingested output
   and print a visible before/after (a section's full text vs. the chunks it was split into), so
   chunk-boundary quality is directly observable, not just asserted.

Per PRD §11's architecture progression, this stays **one project** — reuses `PokeJudge/AI/`'s existing
provider-abstraction pattern (`ILlmClient` → `GeminiLlmClient`) for a new, parallel embedding
abstraction, and adds a `PokeJudge/Chunking/` folder alongside `Ingestion/`, `AI/`, `StructuredState/`,
and `Clarification/`. No new projects, no vector-store dependency yet.

### Copyright / redistribution hygiene (carried forward from Milestone 3)

Chunks and embeddings are derived directly from the same copyrighted source documents Milestone 3
ingested. The output of this milestone (chunk text + embedding vectors) is **not committed** — same
`.gitignore` treatment as Milestone 3's `Ingestion/Output/`. Automated tests use small hand-crafted
fixtures, not the real ingested JSON.

### Free-tier cost/rate-limit awareness (per the product goal of eventually running on Gemini's free tier)

Embedding two ingested documents means well over a hundred embedding calls — the same class of
free-tier rate limit already hit with the chat model in Milestone 2. Two deliberate design choices
address this now, while it's cheap, rather than retrofitting later:

- **Batch embedding calls, if Gemini's embedding API supports batched input.** Sending multiple chunks
  per request instead of one chunk per request directly cuts the request count that a per-minute rate
  limit actually counts against. Confirmed at implementation start against the real API; if the model
  in use doesn't support batching, this falls back to one call per chunk.
- **Idempotent/resumable pipeline.** Before embedding a chunk, check whether it already has a stored
  embedding in the output file and skip it if so. This isn't just a production nicety — it directly
  protects development-time iteration (re-running the pipeline while tuning chunk size/overlap) from
  needlessly re-burning free-tier quota on unchanged chunks every run.

This is scoped narrowly to what Milestone 4 actually controls (batch shape, skip-if-already-done). The
larger, ongoing free-tier pressure — repeated chat/completion calls per live judge session — is a
Milestone 2/6+ concern, not something this milestone's embedding pipeline can address.

## 2. AI Concepts Being Learned

- **What an embedding actually is**: a fixed-size dense vector produced by a specific embedding model,
  representing a piece of text's semantic content in a form that can be compared numerically — distinct
  from anything the chat/completion model (`GeminiLlmClient`) produces.
- **Chunking strategy as a real design decision, not a technicality**: chunk size and overlap directly
  determine what a later similarity search can possibly retrieve. A chunk boundary that splits a
  material fact or a policy condition mid-sentence degrades retrieval before retrieval even exists to
  test it.
- **Provider abstraction extended to a second capability shape**: `ILlmClient` is a
  prompt-in/structured-JSON-out abstraction; embeddings are a text-in/vector-out abstraction — genuinely
  different shapes, both swappable behind their own small interface, following the same pattern (PRD
  §8's "provider abstraction" requirement, §12's "via same swappable interface" for embeddings).
- **Chunking can't be validated yet, only inspected.** Without retrieval (Milestone 5) or evaluation
  (Milestone 8), "is this a good chunk boundary?" can only be answered by eyeballing real output, not
  measured. That limitation is itself the direct motivation for Milestone 5's "evaluable independent of
  the LLM" framing.

## 3. Implementation Steps (in order)

1. **Load Milestone 3's ingested output.** A small loader deserializing `PokeJudge/Ingestion/Output/*.json`
   back into `IngestedDocument` — the first real consumer of that structured output, closing the loop
   PRD §11's Milestone 3→4 progression describes.
2. **Design the chunk data model** as a C# record, e.g.:
   - `TextChunk(string ChunkId, string SectionId, string Text, SourceDocumentMetadata Source)`
   - `EmbeddedChunk(TextChunk Chunk, float[] Embedding)`
   `ChunkId` extends `IngestedSection.SectionId`'s scheme (e.g. `PPTRH-3.3#0`, `PPTRH-3.3#1`) so a
   retrieved chunk can always be traced back to its section-level citation.
3. **Build the chunking function** as a small, pure, testable unit: given a section's text, a target
   chunk size, and an overlap amount, return an ordered list of chunks. Start simple (character-count-
   based splitting, breaking on the nearest sentence/paragraph boundary rather than mid-word) —
   deliberately not over-engineering a sophisticated semantic chunker for a first pass.
4. **Extend the provider abstraction**: add a small `IEmbeddingClient` interface parallel to
   `ILlmClient`, implemented via Gemini's embedding API (exact model confirmed at implementation start,
   per PRD §12's "decide exact package/model when needed"). Check whether the chosen model's API
   supports batched input; if so, shape the interface as `Task<IReadOnlyList<float[]>>
   EmbedBatchAsync(IReadOnlyList<string> texts)` so batching is the primary path rather than bolted on
   afterward. Fall back to one-chunk-per-call only if batching isn't available.
5. **Wire the chunk + embed pipeline** to be resumable: for each loaded `IngestedDocument`, chunk every
   section, then embed only the chunks not already present in that document's existing output file
   (matched by `ChunkId`) — re-running the pipeline after an interruption, or while iterating on chunk
   size/overlap for unchanged sections, shouldn't re-spend quota on chunks already embedded.
6. **Serialize chunks + embeddings** to a local, gitignored JSON file per document (mirroring
   Milestone 3's `Ingestion/Output/` pattern) — e.g. `Chunking/Output/PPTRH.chunks.json` — written
   incrementally enough that a partial run (e.g., stopped by a rate limit) leaves a valid, resumable
   file rather than losing all progress.
7. **Wire a console `chunk` mode** (or extend `ingest`) that runs this pipeline against a
   previously-ingested document and prints a visible sample: one section's full text, followed by the
   chunks it produced, so chunk-boundary quality is directly inspectable.
8. **Document observed chunking-quality issues** with concrete examples from the real ingested
   documents — where a fixed-size boundary split something that should have stayed together (e.g., a
   bulleted list item, a single numbered rule) — in the same evidence-based style as Milestones 1-3.

## 4. Expected Limitations / Failures to Intentionally Observe

- **Chunking quality is inspected, not measured.** There is no retrieval or evaluation yet to say
  whether a given chunk size/overlap actually produces better or worse retrieval — only Milestone 5
  (and formally, Milestone 8) can measure that. Any judgment made this milestone about "this chunking
  looks reasonable" is provisional.
- **Fixed-size chunking will still occasionally split a meaningful unit** (a bulleted list item, a
  single rule, a sentence) across a chunk boundary, even with sentence/paragraph-aware breaking and
  overlap — expect concrete examples from the real documents, not just a theoretical risk.
- **Embeddings are static snapshots tied to the ingested text at generation time.** If Milestone 3's
  ingestion output ever changes (e.g., a future improvement to citation granularity), embeddings would
  need regenerating — no staleness detection or versioning exists yet.
- **Real embedding-API call volume is nontrivial, even with batching and resumability.** Two ingested
  documents (101 sections total) will produce well over a hundred chunks. Batched requests and a
  resumable pipeline (see "Free-tier cost/rate-limit awareness" above) reduce how often this actually
  bites, but a full first-time embed of both documents may still surface the same kind of free-tier
  rate-limit behavior already observed with the chat model in Milestone 2 — expected and worth
  documenting if hit, not something this milestone builds full retry/backoff infrastructure to hide.

## 5. What I Should Understand by the End

- What an embedding vector concretely is, and how it differs from anything the chat/completion model
  produces.
- Why chunk size and overlap are real, consequential design decisions — with concrete examples of a
  real chunk boundary landing somewhere awkward in the actual ingested documents.
- How citation traceability is preserved from `IngestedSection` down through an individual chunk
  (`ChunkId` → `SectionId` → `SourceDocumentMetadata`), so a future retrieved chunk can always be traced
  back to a real, specific citation.
- Why this milestone's chunking strategy can't be validated yet, and what would need to exist
  (Milestone 5's retrieval, Milestone 8's evaluation) to actually measure it.

## Out of Scope for This Milestone

- Vector storage, indexing, or similarity search of any kind (Milestone 5).
- Retrieval-grounded ruling generation, or wiring retrieval into the clarification loop (Milestone 6).
- Comparing or evaluating chunking strategies against each other — requires retrieval-quality
  measurement that doesn't exist until Milestones 5/8.
- Ingesting additional source documents beyond Milestone 3's two — a separate, lower-priority activity
  already discussed and deferred, not part of this milestone's scope.
- A dedicated vector-store or embeddings project — stays inside the single PokeJudge console project
  per PRD §11's modular-monolith principle, unless a concrete need emerges.
