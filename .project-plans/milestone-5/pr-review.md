# Milestone 5 -- PR Review

## PR Review Summary

- **Milestone:** Milestone 5 -- Vector Search
- **Branch:** `milestone/5-vector-search`
- **Base branch:** `master`
- **What changed:** Adds `PokeJudge/Retrieval/` (`VectorMath` cosine similarity, `InMemoryVectorStore`
  brute-force search, `RetrievalEvaluator`, `RetrievalEvalSet`), two new console modes in `Program.cs`
  (`search <query text>`, `eval`), and their corresponding tests in `PokeJudge.Tests/Retrieval/`. All
  changes are currently uncommitted in the working tree, still on `milestone/5-vector-search`.
- **Overall impression:** A clean, well-scoped milestone. The implementation matches the plan closely,
  reuses Milestone 4's `IEmbeddingClient` abstraction without duplication, and the one real retrieval
  miss and one eval-methodology near-miss are documented honestly rather than glossed over. No
  blocking issues found.
- **Build/test status:** `dotnet build` -- succeeded, 0 warnings, 0 errors. `dotnet test` -- **99/99
  passed** (matches the implementation summary's claimed count).

## Blockers

None.

## Major Issues

None.

## Minor Issues

- ~~**`PokeJudge/Program.cs:288,359,391`** -- The embedding model id (`"gemini-embedding-001"`) and
  `outputDimensionality: 768` are duplicated as literals across three separate `GeminiEmbeddingClient`
  construction sites (`RunChunking`, `RunSearch`, `RunRetrievalEval`).~~ **Fixed.** Extracted into a
  single `CreateEmbeddingClient(string apiKey)` helper, now the sole construction site for
  `GeminiEmbeddingClient`; `RunChunking`, `RunSearch`, and `RunRetrievalEval` all call it instead of
  duplicating the model id and dimensionality inline. Chunk-time and query-time embeddings can no
  longer silently drift into incompatible vector spaces if the model or dimensionality changes at one
  call site but not the others, since there's only one call site now. Re-verified: `dotnet build`
  clean (0 warnings/errors), `dotnet test` still 99/99 passing, no behavior change.
- ~~**`.project-plans/milestone-5/plan.md:69` vs. `PokeJudge/Program.cs:342`** -- The plan specifies a
  `search <document-code(s)> <query>` mode (filterable by document), but the shipped `RunSearch` takes
  only `search <query text>` and always searches across every loaded document, with no code-filtering
  capability. This isn't necessarily wrong -- searching the whole corpus is arguably more realistic for
  a judge who doesn't know which document covers their scenario -- but it's an undocumented deviation
  from the plan; neither `implementation-summary.md` nor `review.md` calls it out as an intentional
  scope simplification. Worth a one-line note in the implementation summary for future reference, even
  though no code change is needed.~~ **Documented.** Added a note to `implementation-summary.md`'s
  "What Changed" section calling out the `search <query text>` vs. plan's `search
  <document-code(s)> <query>` deviation as an intentional scope simplification, with the rationale.
  No code change needed.

## Review Notes

- `LoadAllEmbeddedChunks` (`Program.cs:428`) silently skips a `.chunks.json` file if
  `JsonSerializer.Deserialize` returns `null` (`if (document is not null)`), rather than surfacing a
  warning. In practice this is very hard to trigger -- malformed JSON throws `JsonException` and fails
  loudly already, as intended; only a file whose content is the literal `null` would hit this silent
  path. Given this milestone's own `InMemoryVectorStore` constructor is deliberately strict about
  never silently dropping bad data, this is a very minor inconsistency in the same spirit, but the
  practical risk is low enough that it's a note, not a fix-now item.
- `CreateVectorStore`'s catch-log-rethrow pattern (`Program.cs:455`) intentionally lets an invalid-
  embedding failure crash the process with an unhandled-exception stack trace after printing a friendly
  diagnostic line first. This is the same precedent already established by `RunChunking`'s
  `HttpRequestException` handling (`Program.cs:309`) -- consistent with this project's "fail visibly,
  never silently degrade" discipline (PRD Section 9), not a new or inconsistent pattern introduced here.
- The `TCGTH-6.2` eval case's rank-3 hit (correctly identified in `observed-limitations.md` and the
  learning checkpoint) is a good, honest example of the single-`ExpectedSectionId` eval design's
  limitation -- confirmed by reading `RetrievalEvaluator.Evaluate`, which checks presence anywhere in
  top-K with no way to express multiple acceptable sections. Correctly scoped as a Milestone 8 concern,
  not something to fix here.

## AI-Specific Review

- **Model vs. application boundary is correctly drawn.** `RunRetrievalEval` makes exactly one model
  call category (batched query embedding via `IEmbeddingClient`); `InMemoryVectorStore.Search` and
  `RetrievalEvaluator.Evaluate` are both pure, deterministic application code with zero model
  involvement. Verified directly by reading the call graph, not just trusting the docs -- this genuinely
  supports the "evaluable independent of the LLM" claim.
- **No hidden reliance on unintended model behavior.** Cosine similarity's output is used consistently
  as a ranking signal only -- nowhere in `Program.cs` or `Retrieval/` is the raw score presented or
  treated as a confidence/correctness percentage, correctly respecting PRD Section 8's "Source Support,
  not confidence" principle one layer down at the retrieval-score level.
- **Batch-order dependency is real but already hardened.** `RunRetrievalEval` assumes
  `queryVectors[i]` corresponds positionally to `RetrievalEvalSet.Cases[i]`. This assumption is not
  re-verified in this milestone's own tests, but it doesn't need to be -- it transitively inherits
  Milestone 4's already-tested `GeminiEmbeddingResponseParser` batch-order guarantee, so re-testing it
  here would be redundant coverage rather than a gap.
- **Zero-vector validation is a legitimate AI-specific safety concern, correctly handled.** A corrupted
  or degenerate embedding (e.g., from a failed/partial embedding call) could otherwise silently vanish
  from search results without any signal, which is exactly the kind of ungrounded silent failure PRD
  Section 9 prohibits. The constructor-level fail-fast, chunk-naming validation directly addresses this.

## Test Review

- **Coverage is appropriately deterministic and meaningful, not testing implementation details.**
  `VectorMathTests` covers identical/orthogonal/opposite vectors, magnitude invariance, mismatched
  lengths, and the zero-vector guard -- independently re-derivable by hand, and each test would
  actually fail if the underlying math regressed. `InMemoryVectorStoreTests` covers ranking order,
  `topK` larger/smaller/zero, empty store, and all three zero-vector-validation scenarios (single bad
  chunk, multiple named together, all-valid unaffected). `RetrievalEvaluatorTests` covers hit at
  various ranks, miss, multiple-chunks-from-expected-section (earliest rank wins), and empty results.
  `RetrievalEvalSetTests` sanity-checks the hand-authored data itself (case count in range, non-empty
  fields, coverage of both real documents).
- **Correctly untested boundaries.** `GeminiEmbeddingClient`'s network calls and `Program.cs`'s console
  I/O (`RunSearch`, `RunRetrievalEval`) remain untested, consistent with prior milestones' precedent of
  not unit-testing network/IO boundaries or probabilistic model output.
- **Build/test results:** `dotnet build` clean (0/0 warnings/errors); `dotnet test` 99/99 passed,
  confirmed by direct re-run during this review, matching the implementation summary's reported count
  exactly (77 carried over + 19 from this milestone's Red-step tests + 3 from the zero-vector fix noted
  in `review.md`).
- **Manual AI experiments are appropriate here and were already done.** `search` and `eval` were both
  run against the real 258-chunk corpus per `observed-limitations.md`, producing a real semantic-match
  example and a real, documented miss -- the right kind of evidence for this milestone's learning
  objective, not something ordinary unit tests could substitute for.

## Scope Check

- **Does this branch correspond to the current milestone?** Yes -- matches `plan.md` closely (cosine
  similarity, in-process brute-force vector store, `search`/`eval` console modes, hand-authored eval
  set) with only the minor `search`-filtering deviation noted above.
- **Does the diff contain only work appropriate to this milestone?** Yes.
- **Did unrelated changes get mixed into the branch?** No -- `git status` shows exactly the expected
  changes: `PokeJudge/Program.cs` modified, new `PokeJudge/Retrieval/`, `PokeJudge.Tests/Retrieval/`,
  and `.project-plans/milestone-5/`.
- **Did it implement anything from future milestones?** No. Sufficiency/materiality reasoning,
  query rewriting/re-ranking, and a dedicated vector database are all correctly absent, matching the
  plan's explicit "Out of Scope" list.
- **Did it introduce unnecessary architecture or dependencies?** No new NuGet packages, no new
  projects -- stays within the single-project modular-monolith structure per PRD Section 11, adding
  only a new `Retrieval/` folder as the PRD's own illustrative later-milestone layout anticipated.

## Final Verdict

**Approve**

No correctness, safety, or scope issues. Both minor items originally found have since been addressed:
the duplicated embedding-model/dimensionality literals were fixed, and the undocumented `search`-mode
scope simplification vs. the plan is now documented in `implementation-summary.md`.
