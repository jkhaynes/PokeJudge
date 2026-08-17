# Milestone 5 — Vector Search

Status: planned, not started
Source: PRD.md §7-8 (Functional/AI-Specific Requirements), §11 (architecture progression), §12 (tech stack), §15 (Testing/Evaluation), §18 (open questions), "Learning Objectives → Milestone 5"

## 1. What We Will Build

Milestone 4 produced `ChunkedDocument` files — real chunks, each with a 768-dimension embedding
vector and full citation metadata, sitting in local JSON. Nothing can search over them yet. Milestone 5
closes that gap:

1. A **similarity search step**: given a query embedding, compare it against every stored chunk's
   embedding via cosine similarity and return the top-K most similar chunks, each with its score.
2. An **in-process vector store**: loads the existing `ChunkedDocument` files into memory and does a
   brute-force linear scan for similarity search. Per PRD §12's explicit framing ("start with a
   simple/local option... revisit for a dedicated vector DB only when scale/features require it"), this
   is the right choice at the current scale (258 chunks total across both documents) — no vector
   database, no approximate-nearest-neighbor indexing.
3. A **console query mode**: given a raw text query, embed it (reusing Milestone 4's
   `IEmbeddingClient`), search, and print the top-K chunks with their similarity scores and citation
   info — so retrieval behavior is directly observable, not just asserted.
4. A **small, hand-authored retrieval evaluation set and runner**: per PRD §15/§18 and this milestone's
   explicit "evaluable independent of the LLM" framing, a handful of judge-scenario-style natural
   language questions, each paired with the section/chunk it *should* retrieve. The runner embeds each
   query, searches, and checks whether the expected chunk appears in the top-K — a deterministic
   hit/miss check involving the embedding call but **zero chat/completion model calls**. This is not
   Milestone 8's full evaluation harness; it's a small, concrete demonstration that retrieval quality
   is a measurable thing independent of generation quality, which Milestone 8 will later formalize.

Per PRD §11's architecture progression, this stays **one project** — a new `PokeJudge/Retrieval/`
folder, matching the PRD's own illustrative later-milestone structure (`AI`, `State`, `Retrieval`,
`Ingestion`, `Evaluation`). No new projects, no vector database dependency.

### Reusing, not duplicating, Milestone 4's embedding client

Query embedding goes through the same `IEmbeddingClient`/`GeminiEmbeddingClient` built in Milestone 4 —
no new embedding abstraction. A judge's query is a single string, not a batch, but `EmbedBatchAsync`
already handles a one-item list fine; no new method needed.

## 2. AI Concepts Being Learned

- **Semantic vs. keyword search, demonstrated not just asserted.** The evaluation set is designed to
  include at least one query phrased differently from how the source text phrases the same idea (e.g.,
  a judge's plain-language question vs. the rulebook's formal wording), specifically to show retrieval
  succeeding where literal keyword matching would fail.
- **Cosine similarity as the standard metric for comparing embeddings**, and what the resulting score
  actually represents geometrically (vector alignment, not a probability or confidence value — a
  distinction worth being precise about, echoing PRD §8's "Source Support, not confidence" discipline
  applied one level down, to retrieval scores instead of model confidence).
- **Evaluating retrieval quality independent of generation quality.** The hand-authored eval set proves
  this is measurable without invoking the chat model at all — directly setting up Milestone 6's
  retrieval-grounded materiality and Milestone 8's formal evaluation harness.
- **Why an in-process, brute-force vector store is the right architecture at this scale**, and what
  would actually change (indexing structure, approximate search, a dedicated vector database) if the
  corpus grew by orders of magnitude — a concrete instance of the modular-monolith "introduce
  infrastructure only when a real need exists" principle.

## 3. Implementation Steps (in order)

1. **Load Milestone 4's chunked/embedded output.** A small loader deserializing
   `PokeJudge/Chunking/Output/*.chunks.json` back into `ChunkedDocument` — the first real consumer of
   that structured output.
2. **Build cosine similarity** as a small, pure, testable function: given two `float[]` vectors of
   equal length, return their cosine similarity as a `double`.
3. **Build the in-process vector store**: given a collection of `EmbeddedChunk`s, a `Search(float[]
   queryVector, int topK)` method returning the top-K chunks ranked by cosine similarity
   (`ScoredChunk(EmbeddedChunk Chunk, double Score)`), implemented as a straightforward in-memory
   linear scan.
4. **Wire a console `search <document-code(s)> <query>` mode**: embed the query text via
   `GeminiEmbeddingClient`, run it through the vector store, print the top-K results (score, `ChunkId`,
   section heading, chunk text) for direct inspection.
5. **Design the hand-authored retrieval evaluation set**: 6-8 judge-scenario-style natural language
   questions covering both of Milestone 3-4's real documents, each paired with its expected
   `SectionId`(s) — deliberately including at least one query phrased in plain judge language rather
   than the rulebook's own wording, to exercise the semantic-vs-keyword distinction directly.
6. **Build the evaluation runner**: for each eval query, embed it, search the vector store, and check
   whether a chunk from the expected section appears within the top-K results — reporting hit/miss (and
   rank, when it hits) per query, with no chat/completion model involved.
7. **Run the evaluation against the real, already-embedded data** and observe the actual results —
   hits and misses alike, not just the hits.
8. **Document observed retrieval-quality findings** with concrete evidence (query text, expected
   section, actual top-K results and scores) in the same evidence-based style as Milestones 1-4's
   observation docs — including a candid account of any miss, not only the successes.

## 4. Expected Limitations / Failures to Intentionally Observe

- **Retrieval quality is not expected to be perfect.** A reduced 768-dimension embedding, brute-force
  cosine similarity, and no query rewriting or re-ranking mean some eval queries may retrieve a
  plausible-but-not-quite-right chunk, or miss the intended one if the phrasing diverges too far. This
  is the direct, concrete motivation for Milestone 6's iterative retrieval and Milestone 8's full
  evaluation harness — expected to observe at least one real miss, not just report a clean success rate.
- **Milestone 4's oversized-chunk finding may directly affect retrieval quality here.** A chunk that
  absorbed an entire bulleted list (e.g., `PPTRH-3.3#3` at 2043 characters) packs several real
  sub-topics into one embedding, which could dilute its focus and cause it to rank lower than expected
  for a query about one specific sub-topic buried inside it — worth checking directly against the eval
  results, connecting this milestone's findings back to Milestone 4's.
- **Brute-force linear scan doesn't scale**, though it's the right choice at 258 chunks. Explicitly
  accepted, not fixed — a real indexing structure or dedicated vector store is deferred until an actual
  scale need exists (PRD §12).
- **No sufficiency/materiality reasoning happens here.** Vector search returns "most similar chunks,"
  not "enough information to rule." The Milestone 1-2 mock-corpus-based clarification loop remains
  untouched and still the only source of context for actual judge interactions until Milestone 6 wires
  retrieval in as a replacement.
- **The hand-authored eval set is small (6-8 queries)** — a real, concrete signal, but not statistically
  rigorous. Formalizing this is explicitly Milestone 8's job.

## 5. What I Should Understand by the End

- Why cosine similarity is the standard tool for comparing embeddings, and what the resulting score
  represents (geometric alignment, not a probability or confidence value).
- A concrete example, from this milestone's own eval run, of retrieval succeeding on a query phrased
  differently from the source text — the semantic-vs-keyword distinction, demonstrated with real data.
- Why retrieval quality can and should be evaluated separately from generation quality, and what that
  evaluation actually looked like here (embedding + similarity search only, zero chat model calls).
- Why an in-process, brute-force vector store is the right architecture choice right now, and what
  would have to change for it not to be.

## Out of Scope for This Milestone

- Wiring retrieval into the clarification loop or replacing the Milestone 1-2 mock corpus (Milestone 6).
- A dedicated vector database or approximate-nearest-neighbor indexing (e.g., FAISS, HNSW, pgvector) —
  brute-force linear scan is sufficient and appropriate at this scale.
- A full, statistically rigorous evaluation harness or branching/trajectory evaluation (Milestone 8).
- Query rewriting, re-ranking, or hybrid keyword+vector search.
- Ingesting, chunking, or embedding additional source documents beyond what Milestones 3-4 already
  produced.
