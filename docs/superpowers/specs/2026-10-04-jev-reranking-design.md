# Step 4: Rerank retrieved rules with Jev

Status: approved design, 2026-10-04. Step 4 ("Better rule text") of the PokeJudge improvement plan.

## Goal

The AI should see the rules a scenario actually turns on. Today it sees the 5 excerpts (of 515) whose embeddings are
closest to the search text. In the 2026-09-27 run (16/20), four scenarios never received a rule the corpus contains:

| Scenario | Rule that never reached the top 5 | Latest result |
|---|---|---|
| `drew-extra-card` | `PPG-5.5.1` | Fail |
| `ace-spec-count` | `TCGRULES-appendix-3-ace-spec-cards`, `PPG-4.1.1`, `PPG-5.6.1#2` | Fail |
| `deck-under-60` | `TCGRULES-deck-building` | Pass, only because `PPG-5.6.1` counts as a hit |
| `supporter-twice` | `TCGRULES-turn-actions` (no record either way) | Pass |

The full baseline is published at https://claude.ai/artifact/3aghYCpThnprBHQaDVbVpp.

The change: fetch the top 30 by cosine similarity, ask Jev (TypeSafe's classification model) whether each passage
governs the situation, and keep the 5 it rates highest. This is a variant of option A in the improvement plan
("send more excerpts"), except that the AI still reads only 5.

PokeJudge's clarification loop, ruling, grounding and scorer do not change. Reranking is opt-in behind a flag until
results justify making it the default.

## Why Jev

- Picking passages is a classification task, which is all Jev does. It returns a probability per question, with
  no generated text to parse.
- One request answers all 30 questions in a single parallel pass, with a claimed latency of 70 to 500 ms. The
  loop searches again every turn, so speed matters.
- It makes no Gemini calls, so it adds nothing to the free-tier rate limit.
- Embedding search scores word overlap ("deck", "legality"). A reranker reads the situation and the passage
  together, so it can tell "governs this case" from "uses the same words". That is the `deck-under-60` failure.

## What it won't fix

- **Source gaps.** `missed-prize` needs a rule that isn't in any rulebook.
- **Rules below rank 30.** Reranking can only promote what the wider search returns. The `retrieval-depth` command
  measures this before any Jev call is made.
- **Which of the 5 the AI cites.** Gemini still chooses its citations.

## Components

### `RerankingRetriever` (new, `PokeJudge/Retrieval/`)

An `IRetriever` decorator around `VectorStoreRetriever`. `ClarificationLoop` is unchanged.

- `RetrieveAsync(queryText, topK)` asks the inner retriever for `candidateCount` (default 30). It sends the query text
  and candidates to an `IRelevanceScorer`, stable-sorts by score (descending, ties keep cosine order), and returns
  the first `topK`.
- Each `ScoredChunk` keeps its cosine `Score`, so only the order changes. Nothing reads `Score` in logic; it appears
  in prompts and console output (`PromptBuilder.cs:100,120`). Keeping cosine means the prompts don't change meaning.
- An optional `Action<IReadOnlyList<RerankedCandidate>>` callback reports every candidate's Jev probability and
  original cosine rank, for logging. `search --rerank jev` uses it to print all 30.
- The constructor throws if `candidateCount < 1`. `RetrieveAsync` throws if `topK > candidateCount`, or if the scorer
  returns a different number of scores than candidates.

### `IRelevanceScorer` (new, `PokeJudge/Retrieval/`)

`Task<IReadOnlyList<double>> ScoreAsync(string situation, IReadOnlyList<ScoredChunk> candidates)` returns one score
in [0, 1] per candidate, in input order. It keeps HTTP out of the reranking logic, and tests use a stub.

### `JevRelevanceScorer` (new, `PokeJudge/AI/`)

The HTTP wrapper only, following `GeminiEmbeddingClient`'s pattern: a static `HttpClient` and an
`HttpRequestException` on non-2xx responses.

- `POST https://api.typesafe.ai/v1/systemone`, with header `Authorization: Bearer <key>`.
- Body: `model` (default `jev-latest`), plus a `state` object and a `questions` map:
  - `state` = `{ situation, passages: [{ id: "p0", source: "<ChunkId>", text }] }`
  - `questions` = `{ "p0": { type: "noul", instructions: "..." }, ... }`, one per passage.
- A `noul` question returns the probability that the answer is yes.
- The instructions ask whether the passage states a rule or policy a judge would apply to decide this situation,
  as opposed to merely mentioning related words.
- `internal static BuildRequestBody(...)` is unit-tested the same way as `GeminiLlmClient.BuildRequestBody`.

### `JevResponseParser` (new, `PokeJudge/AI/`)

A pure static: `Parse(string json, IReadOnlyList<string> keys) → IReadOnlyList<double>`.

- It reads each key's probability and returns them in key order.
- It throws `InvalidOperationException` naming the key if a key is missing, or if a value isn't a number in [0, 1].
- Jev's exact response field names were taken from third-party guides, not checked against the live API. They live
  only in this parser, and the first live task confirms them.

### Command-line changes (`Program.cs`)

- **`search [--top <n>] [--rerank jev] <query>`.** New pure parser `SearchArgsParser`, in the same style as
  `EvalScenarioSelector`. Default top is 5.
- **`retrieval-depth` (new).** It runs embeddings only, with no chat calls. For each `EvalDataset` scenario, it
  searches the turn-1 description with top 30. It prints the best rank of every expected section and the chunk at
  rank 5. Pure helper: `SectionRankFinder`.
- **`eval [--rerank jev]`.** It moves from `store.Search` to an `IRetriever`, so the search-only test can be reranked.
- **`evaluate ... [--rerank jev]`.** A shared helper, `RerankOption.Extract(args)`, removes `--rerank <value>`
  before the existing parsers run, so `EvalScenarioSelector` doesn't change. `jev` is the only accepted value.
  `evaluate` also prints each turn's top 5 (chunk ID and cosine score) from
  `TurnRecord.RetrievedChunks`.
- Interactive mode is unchanged.

### Configuration (user-secrets)

| Key | Required | Default |
|---|---|---|
| `Jev:ApiKey` | Only with `--rerank jev`. A missing key prints an error and exits 1. | none |
| `Jev:Model` | No | `jev-latest` |
| `Jev:CandidateCount` | No | `30` |

## Measurement

Run three things before and after, with `--rerank jev` the only difference:

1. `eval` (search-only test)
2. `retrieval-depth`
3. `evaluate --repeat 3`

Two prerequisites:

- The search-only test grows from 7 to about 30 cases first, as the improvement plan already requires. It adds
  PPG and TCGRULES targets, including the four missing sections.
- The repeat-violations case moves from `PPTRH-7.5` to `PPG-4.2.2`, matching `EvalDataset`.

**Adopt (as a separate change that makes it the default) only if all of these hold:**

- At least 2 of the 4 target scenarios receive every needed section in most repeats.
- No control scenario loses a needed section in most repeats.
- The search-only score goes up.
- Added latency per turn stays under about 2 seconds.

Otherwise, record why. Either the rules sit below rank 30 (then try options B or C), or Jev ranks them poorly.

## Error handling

- Jev transport failures throw `HttpRequestException`. `evaluate` already counts those as infrastructure failures,
  not scenario failures.
- Parser failures throw `InvalidOperationException` and stop the run. An unparseable answer is a bug to fix, not
  something to skip.

## Testing

- Unit tests cover:
  - `SearchArgsParser`, `SectionRankFinder`, `JevResponseParser`
  - `JevRelevanceScorer.BuildRequestBody`
  - `RerankingRetriever`, using `StubRetriever` and a new `StubRelevanceScorer`
  - `RerankOption.Extract` and `JevSettings.Read`
- No test touches the network, so CI stays green with no secrets.
