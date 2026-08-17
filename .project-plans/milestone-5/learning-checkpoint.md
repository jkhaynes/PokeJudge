# Milestone 5 — Learning Checkpoint

**Milestone:** Milestone 5 — Vector Search
**Branch:** `milestone/5-vector-search`

## Q&A Transcript

### 1. Cosine similarity normalization

**Question:** `VectorMath.CosineSimilarity` divides the dot product by `Math.Sqrt(normA) * Math.Sqrt(normB)` -- it doesn't just return the raw dot product. Why does that normalization matter for comparing two embedding vectors, and what would go wrong (concretely, in terms of search results) if `InMemoryVectorStore.Search` used the raw dot product instead?

**Answer:** The normalization makes sure we're comparing the direction of the embeddings instead of their size. Without it, vectors with larger values could get higher dot product scores even if they aren't actually as semantically similar to the query. That could cause our search to rank a less relevant chunk above a better match just because its embedding has a larger magnitude.

**Feedback:** Correct. Captured the core mechanism exactly -- cosine similarity isolates direction while raw dot product conflates direction with magnitude, so a larger-magnitude but less-relevant chunk could out-rank a genuinely better match. Added nuance: embedding magnitude isn't guaranteed to be meaningless in all models, but for this system there's no evidence it carries a useful signal, and normalizing it away is the conservative default consistent with PRD Section 8's "Source Support, not confidence" discipline. Memory trick given: cosine similarity is the angle, not the arrow's length.

### 2. "Evaluable independent of the LLM"

**Question:** The plan and review both stress that `eval` demonstrates retrieval is "evaluable independent of the LLM." Trace through `RunRetrievalEval` in `Program.cs` -- what's the *one* model call it actually makes, and why doesn't that call count against the "independent of the LLM" claim? What would have to be true for that claim to be false?

**Answer:** RunRetrievalEval only makes a model call to create the embedding for the search query. The LLM isn't being asked to interpret the question, choose the best result, or generate an answer. Our retrieval code compares that embedding against the stored chunk embeddings and ranks the results itself. The claim would be false if we were using an LLM to judge, rerank, or otherwise decide which retrieved chunks were correct.

**Feedback:** Correct. Nailed the distinction between the embedding call (a fixed, non-judgmental transformation) and a chat/completion call (which would involve judgment). Correctly identified the exact failure condition: the claim would break the moment an LLM's judgment enters the loop, e.g. grading whether a retrieved chunk answers the query. Memory trick given: embedding = a ruler, not a referee.

### 3. Fail-fast validation on zero-vector embeddings

**Question:** `InMemoryVectorStore`'s constructor validates every chunk's embedding and throws immediately if any are zero-vectors, rather than letting `Search` fail (or silently skip that chunk) later. Why is "silently skip the bad chunk" specifically the wrong failure mode here, given what this system is actually for?

**Answer:** Exactly. I'd include that too: Silently skipping a bad chunk could cause us to miss important ruling information without knowing anything is wrong. Validating in the constructor makes the problem obvious right away, and it also avoids having to loop through all the chunks during every search just to discover an invalid embedding. Since the data should already be valid before searching, it makes more sense to fail early.

**Feedback:** Correct. Core point identified: a grounded-ruling system cannot silently drop evidence, or a judge could get a confidently wrong or unnecessarily "Insufficient" ruling with no visible signal anything broke -- directly matching PRD Section 9's "must fail visibly... rather than silently degrading" requirement, applied to the retrieval layer. Noted the efficiency point is reasonable but secondary; the code's own emphasis is on diagnosability -- failing once at a predictable load-time moment and naming every offending `ChunkId`, rather than surfacing a cryptic divide-by-zero on whichever chunk is scored first. Memory trick given: a missing chunk is a missing witness -- the system should say so, not just proceed without it.

### 4. Why the retrieval miss is expected, and what Milestone 6 changes

**Question:** The eval run had one genuine miss: the "repeat violations" query never retrieved `PPTRH-7.5` in the top 5, instead returning topically-adjacent penalty sections. Given that vector search has no query rewriting, re-ranking, or sufficiency reasoning at this stage -- why is this the *expected*, not surprising, outcome of this milestone's architecture, and what specifically does Milestone 6 add that would give this system a shot at fixing it?

**Answer:** This is expected because right now the vector search only finds chunks that are semantically similar to the query. It doesn't understand which part of the question is most important or reconsider results when they aren't specific enough, so related penalty sections can outrank the exact one we want. Milestone 6 adds the LLM reasoning layer, which can use the retrieved context, judge whether it's sufficient, and improve the retrieval process when needed instead of relying only on the initial similarity ranking.

**Feedback:** Mostly correct. The "expected, not surprising" half was exactly right. The "what Milestone 6 adds" half was imprecise: Milestone 6 does not add re-ranking or query rewriting (the plan explicitly rules that out as still-future work). The actual mechanism is iterative re-retrieval -- when a clarifying answer supplies a new fact, that fact gets folded into an updated query and retrieval runs again with different text, not a re-ranking of the same results. For this specific miss, a clarifying answer containing language closer to "repeat"/"history of violations" could shift the query enough on a second pass to surface `PPTRH-7.5`. Also noted: Milestone 6 gives the system a *shot* at fixing cases like this, not a guarantee -- this single miss is exactly the kind of concrete evidence that motivates Milestone 8's more rigorous evaluation. Memory trick given: Milestone 6 doesn't make search smarter about ranking -- it makes the question smarter, then searches again.

### 5. Similarity score vs. confidence

**Question:** In `RunSearch` and `RunRetrievalEval`, the search results print a cosine similarity score like `[0.8220]` right next to each chunk. A judge (or a future teammate) glancing at that number might read it as "82% confidence this is the right answer." Why would that be a misreading, and what does the PRD's "Source Support, not confidence" principle (Section 8) suggest about how this score should -- and shouldn't -- be used or presented?

**Answer:** The 0.82 score only tells us how similar the query and chunk embeddings are, not that there is an 82% chance the chunk is correct. Following the PRD's "Source Support, not confidence" principle, we should use the score for ranking retrieval results internally, but not present it to users as a confidence or correctness percentage. The actual ruling should be supported by the retrieved source evidence.

**Feedback:** Correct. Drew the right line between geometric alignment and a validated probability of correctness -- nothing calibrates a 0.82 score to "82% correct." Added nuance: `search`/`eval` printing the raw score today is fine because those are developer-facing diagnostic tools, not the judge-facing product surface; the actual PRD constraint applies at the product boundary, where only the criteria-based Source Support label (Strong/Partial/Insufficient) should represent reliability to a judge. Memory trick given: a similarity score answers "how aligned," not "how likely right."

### 6. Why the TCGTH-6.2 near-miss is an eval design limitation, not a search quality limitation

**Question:** `RetrievalEvaluator.Evaluate` checks whether a chunk's `SectionId` matches the eval case's single `ExpectedSectionId` anywhere in the top-K. The `TCGTH-6.2` case actually hit at rank 3, behind two of its own sibling subsections (`6.2.1` and `6.2.2`), which are also legitimately relevant. Why does this count as a "hit" under the current evaluator, and why is that specifically a limitation of the *evaluation design* rather than a limitation of the *search quality* being measured?

**Answer:** It counts as a hit because the evaluator only checks whether the expected section appears anywhere in the top-K, and 6.2 was at rank 3. The limitation is that the eval only allows one expected section even though 6.2.1 and 6.2.2 are also valid results. So the search actually returned good, relevant information, but the eval design isn't able to give credit for those other valid sections.

**Feedback:** Correct. Identified both halves precisely: the "anywhere in top-K" check treats a rank-1 exact hit and a rank-3 hit behind other legitimate results identically, and `RetrievalEvalCase` structurally can't express "multiple sections are all acceptable." Reinforced the deeper framing: the search behaved reasonably (three genuinely on-topic sections), so this is a limitation of how the test was authored, not of the system under test -- a bad or misleading score can mean the test's definition of "correct" is wrong just as easily as it can mean the system is wrong. Memory trick given: a strict grader with the wrong answer key still grades wrong -- check the key before blaming the student.

### 7. Scaling limitations of brute-force search

**Question:** Zooming out: the plan explicitly says brute-force linear scan "doesn't scale past a certain corpus size," but is still the right choice *right now*. If the corpus grew from 258 chunks to, say, 5 million, what specifically would degrade (be concrete about what `InMemoryVectorStore.Search` actually does per query), and what category of change would that require -- not a specific product name, but the *kind* of infrastructure change?

**Answer:** At 5 million chunks, `InMemoryVectorStore.Search` would have to compare the query against all 5 million chunks every time someone searches. That would make searches slower and use a lot more memory and processing power. At that point, we would need a different way of storing and searching the embeddings that is designed to handle much larger amounts of data efficiently.

**Feedback:** Mostly correct. The mechanism was exactly right -- `Search`'s `O(n)` linear scan means every query computes similarity against every chunk, so latency grows linearly with corpus size. The second half stayed a level too generic: the specific category is approximate nearest-neighbor (ANN) indexing (e.g. structures like HNSW), which examines only a small "probably close" candidate subset per query instead of everything, trading a small amount of recall for a large speed gain -- that's what a dedicated vector database actually buys, not just "better storage." This is the point where a dedicated vector store becomes justified per the modular-monolith principle of introducing infrastructure only when a real scale need exists, not before. Memory trick given: brute force checks everyone; ANN indexing checks the right neighborhood first.

## Final Assessment

### Learning Checkpoint Result: Strong

### Concepts I Understand
- Cosine similarity as direction-only comparison, and why magnitude normalization matters for embedding comparison.
- The precise boundary of "evaluable independent of the LLM" -- one embedding call vs. zero chat/completion calls, and what would break that claim.
- Why fail-fast, specifically-diagnosable validation (naming corrupted chunks at load time) matters for a system whose entire value is grounded, cited output -- not silently degrading.
- The score-vs-confidence distinction (PRD's "Source Support, not confidence" principle) applied correctly at the retrieval layer, including the internal-tool vs. product-surface distinction.
- That an eval "hit" scored under lenient rules can mask a real limitation in the eval design itself, not the system being measured -- and why that distinction matters.
- The general shape of the brute-force scaling limitation (O(n) per query).

### Concepts to Reinforce
- Milestone 6's specific mechanism: it's iterative re-retrieval triggered by newly confirmed facts changing the query, not "the LLM reasons over results and reranks them." Worth re-reading PRD Section 11's retrieve -> assess -> clarify -> re-retrieve loop before starting Milestone 6.
- Naming the scale-fix category: "approximate nearest-neighbor (ANN) indexing" -- the specific term for what a dedicated vector store buys you, rather than "better storage."

### Milestone Takeaway
1. Cosine similarity measures alignment (direction), not magnitude -- and is explicitly not a confidence or probability value, echoing the same discipline PRD Section 8 applies to model confidence one layer up.
2. Retrieval quality is fully measurable with zero chat-model calls -- a hand-authored query/expected-section pair, an embedding call, and deterministic hit/miss logic is enough.
3. A grounded-answer system must fail loudly, not silently degrade -- a dropped or corrupted chunk is a missing witness, and the system should say so.
4. How you define "correct" in an eval set is itself a design decision with its own failure modes -- a real miss (`PPTRH-7.5`) and a real "too-strict-by-accident" hit (`TCGTH-6.2`) both surfaced from one small, honest eval run.

### Interview Readiness
1. **"Why do RAG systems use cosine similarity instead of raw dot product or Euclidean distance to compare embeddings?"** -- A strong answer covers: cosine similarity isolates direction and ignores magnitude, which matters because embedding vector length isn't a reliable semantic signal; it's the standard, magnitude-invariant metric for this reason.
2. **"How do you evaluate retrieval quality in a RAG pipeline without involving the LLM's generation step?"** -- A strong answer describes a hand-authored set of (query, expected passage) pairs, embedding the query, running vector search, and checking presence/rank of the expected result deterministically -- zero chat-model calls, so the signal measures retrieval alone.
3. **"When do you move from brute-force vector search to an approximate-nearest-neighbor index or dedicated vector database?"** -- A strong answer names the O(n)-per-query cost of linear scan, explains ANN indexing trades a small amount of recall for large speed gains at scale, and frames the decision as "introduce this infrastructure only when a concrete scale need exists," not preemptively.

### Recommendation: Ready for PR Review
