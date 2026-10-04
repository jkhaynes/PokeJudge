# Jev Reranking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let retrieval fetch the top 30 excerpts by cosine similarity, have Jev score how well each one governs the situation, and pass the best 5 to the AI. Measure the result against today's plain top 5 before making it the default.

**Architecture:** `RerankingRetriever` is a new `IRetriever` decorator around `VectorStoreRetriever`. It calls an `IRelevanceScorer`, whose production implementation, `JevRelevanceScorer`, makes one `noul` question per passage in a single `POST /v1/systemone` call. Reranking is opt-in through `--rerank jev` on `search`, `eval` and `evaluate`. The clarification loop, ruling, grounding, scorer and interactive mode don't change. Two new diagnostics come first: `search --top <n>` and a `retrieval-depth` command. They tell us whether the missing rules are within reach of a top-30 reranker at all.

**Tech Stack:** .NET 10, C#, xUnit, System.Text.Json, TypeSafe Jev REST API, existing Gemini embeddings.

**Spec:** `docs/superpowers/specs/2026-10-04-jev-reranking-design.md`

**Baseline:** https://claude.ai/artifact/3aghYCpThnprBHQaDVbVpp (from the 2026-09-27 run).

> **Written without a compiler:** this plan was written in a cloud session whose network policy blocked the .NET SDK download. None of the code below has been compiled. Each task's "verify they fail" step is the first build, so fix compile errors there before moving on.

## Global Constraints

- Work on branch `claude/jev-reranking`. Build and test from the repo root with `dotnet build PokeJudge.slnx` and `dotnet test PokeJudge.slnx`, the same as CI.
- Don't change the pipeline's behavior when `--rerank` is absent. Leave these alone: `ClarificationLoop`, `RulingGenerator`, `GroundingValidator`, `SystemPrompts`, `PromptBuilder`, `ScenarioEvalScorer`, and interactive mode.
- No test touches the network. CI has no secrets.
- Test-first for every behavior change. Code in `Program.cs` top-level statements has no tests, as before; keep logic out of it.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Never print or read secret values (e.g. never run `dotnet user-secrets list`).
- Part 1 (Tasks 1–8) needs no keys. Part 2 (Tasks 9–12) needs the corpus, a Gemini key and a Jev key.

---

## Part 1: offline

### Task 1: `RerankOption`

**Files:**
- Create: `PokeJudge/Retrieval/RerankOption.cs`
- Test: `PokeJudge.Tests/Retrieval/RerankOptionTests.cs`

**Interfaces:**
- Produces: `public static class RerankOption` with `public const string Jev = "jev"` and `public static (IReadOnlyList<string> Remaining, string? Rerank, string? Error) Extract(IReadOnlyList<string> args)`.

- [ ] **Step 1: Write the failing tests**

```csharp
namespace PokeJudge.Tests.Retrieval;

using PokeJudge.Retrieval;

public class RerankOptionTests
{
    [Fact]
    public void Extract_NoFlag_ReturnsArgsUnchanged()
    {
        var (remaining, rerank, error) = RerankOption.Extract(new[] { "--only", "notes" });

        Assert.Null(error);
        Assert.Null(rerank);
        Assert.Equal(new[] { "--only", "notes" }, remaining);
    }

    [Fact]
    public void Extract_JevFlag_RemovesItAndReturnsJev()
    {
        var (remaining, rerank, error) = RerankOption.Extract(new[] { "--only", "notes", "--rerank", "jev", "--repeat", "3" });

        Assert.Null(error);
        Assert.Equal(RerankOption.Jev, rerank);
        Assert.Equal(new[] { "--only", "notes", "--repeat", "3" }, remaining);
    }

    [Fact]
    public void Extract_UnknownValue_ReturnsErrorNamingIt()
    {
        var (_, _, error) = RerankOption.Extract(new[] { "--rerank", "gemini" });

        Assert.NotNull(error);
        Assert.Contains("gemini", error);
    }

    [Fact]
    public void Extract_MissingValue_ReturnsError()
    {
        var (_, _, error) = RerankOption.Extract(new[] { "--rerank" });

        Assert.Equal("\"--rerank\" requires a value.", error);
    }

    [Fact]
    public void Extract_FlagGivenTwice_ReturnsError()
    {
        var (_, _, error) = RerankOption.Extract(new[] { "--rerank", "jev", "--rerank", "jev" });

        Assert.Equal("\"--rerank\" was given more than once.", error);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PokeJudge.slnx --filter RerankOptionTests`
Expected: build error, `RerankOption` is not defined.

- [ ] **Step 3: Write the implementation**

```csharp
namespace PokeJudge.Retrieval;

// Strips the optional `--rerank <name>` flag before a command's own argument parsing
// runs, so search, eval and evaluate share one definition and EvalScenarioSelector
// stays unchanged. Reranking is opt-in: no flag means today's plain cosine top-K.
public static class RerankOption
{
    public const string Jev = "jev";

    public static (IReadOnlyList<string> Remaining, string? Rerank, string? Error) Extract(IReadOnlyList<string> args)
    {
        var remaining = new List<string>();
        string? rerank = null;

        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] != "--rerank")
            {
                remaining.Add(args[i]);
                continue;
            }

            if (i + 1 >= args.Count)
            {
                return (remaining, null, "\"--rerank\" requires a value.");
            }

            if (rerank is not null)
            {
                return (remaining, null, "\"--rerank\" was given more than once.");
            }

            var value = args[++i];
            if (value != Jev)
            {
                return (remaining, null, $"Unknown reranker \"{value}\". Supported: {Jev}.");
            }

            rerank = value;
        }

        return (remaining, rerank, null);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PokeJudge.slnx --filter RerankOptionTests`
Expected: 5 passed.

- [ ] **Step 5: Commit**

```bash
git add PokeJudge/Retrieval/RerankOption.cs PokeJudge.Tests/Retrieval/RerankOptionTests.cs
git commit -m "Add a shared --rerank flag parser"
```

---

### Task 2: `SearchArgsParser`

**Files:**
- Create: `PokeJudge/Retrieval/SearchArgsParser.cs`
- Test: `PokeJudge.Tests/Retrieval/SearchArgsParserTests.cs`

**Interfaces:**
- Consumes: the args *after* `RerankOption.Extract` has removed `--rerank`.
- Produces: `public sealed record SearchArgs(string Query, int TopK)` and `public static (SearchArgs? Args, string? Error) Parse(IReadOnlyList<string> args)`, plus `public const int DefaultTopK = 5`.

- [ ] **Step 1: Write the failing tests**

```csharp
namespace PokeJudge.Tests.Retrieval;

using PokeJudge.Retrieval;

public class SearchArgsParserTests
{
    [Fact]
    public void Parse_QueryOnly_DefaultsToTopFive()
    {
        var (args, error) = SearchArgsParser.Parse(new[] { "deck", "under", "60" });

        Assert.Null(error);
        Assert.Equal("deck under 60", args!.Query);
        Assert.Equal(5, args.TopK);
    }

    [Fact]
    public void Parse_TopFlag_SetsTopKAndIsNotPartOfTheQuery()
    {
        var (args, error) = SearchArgsParser.Parse(new[] { "--top", "30", "deck", "under", "60" });

        Assert.Null(error);
        Assert.Equal("deck under 60", args!.Query);
        Assert.Equal(30, args.TopK);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("abc")]
    public void Parse_TopNotAPositiveInteger_ReturnsError(string value)
    {
        var (args, error) = SearchArgsParser.Parse(new[] { "--top", value, "query" });

        Assert.Null(args);
        Assert.Equal($"\"--top\" requires a positive integer, got \"{value}\".", error);
    }

    [Fact]
    public void Parse_TopWithoutValue_ReturnsError()
    {
        var (_, error) = SearchArgsParser.Parse(new[] { "query", "--top" });

        Assert.Equal("\"--top\" requires a value.", error);
    }

    [Fact]
    public void Parse_NoQueryText_ReturnsUsage()
    {
        var (_, error) = SearchArgsParser.Parse(new[] { "--top", "10" });

        Assert.Equal(SearchArgsParser.Usage, error);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PokeJudge.slnx --filter SearchArgsParserTests`
Expected: build error, `SearchArgsParser` is not defined.

- [ ] **Step 3: Write the implementation**

```csharp
namespace PokeJudge.Retrieval;

public sealed record SearchArgs(string Query, int TopK);

// Pure arg parsing for `dotnet run -- search [--top <n>] [--rerank jev] <query text>`,
// in the same error-string style as EvalScenarioSelector. `--rerank` is removed by
// RerankOption.Extract before this runs. Every word that isn't a flag or its value
// is part of the query, matching the old `string.Join(" ", args.Skip(1))`.
public static class SearchArgsParser
{
    public const int DefaultTopK = 5;

    public const string Usage = "Usage: dotnet run -- search [--top <n>] [--rerank jev] <query text>";

    public static (SearchArgs? Args, string? Error) Parse(IReadOnlyList<string> args)
    {
        var topK = DefaultTopK;
        var queryWords = new List<string>();

        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] != "--top")
            {
                queryWords.Add(args[i]);
                continue;
            }

            if (i + 1 >= args.Count)
            {
                return (null, "\"--top\" requires a value.");
            }

            var value = args[++i];
            if (!int.TryParse(value, out topK) || topK < 1)
            {
                return (null, $"\"--top\" requires a positive integer, got \"{value}\".");
            }
        }

        return queryWords.Count == 0
            ? (null, Usage)
            : (new SearchArgs(string.Join(" ", queryWords), topK), null);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PokeJudge.slnx --filter SearchArgsParserTests`
Expected: 7 passed.

- [ ] **Step 5: Commit**

```bash
git add PokeJudge/Retrieval/SearchArgsParser.cs PokeJudge.Tests/Retrieval/SearchArgsParserTests.cs
git commit -m "Parse search's --top flag"
```

---

### Task 3: `SectionRankFinder`

**Files:**
- Create: `PokeJudge/Retrieval/SectionRankFinder.cs`
- Test: `PokeJudge.Tests/Retrieval/SectionRankFinderTests.cs`

**Interfaces:**
- Consumes: `RetrievalEvaluator.Evaluate(RetrievalEvalCase, IReadOnlyList<ScoredChunk>)` for rank-by-`SectionId`.
- Produces: `public sealed record SectionRank(string SectionId, int? Rank, SectionReach Reach)`, `public enum SectionReach { InTopK, Rerankable, OutOfReach }`, and `public static IReadOnlyList<SectionRank> Find(IReadOnlyList<string> sectionIds, IReadOnlyList<ScoredChunk> results, int topK)`.

- [ ] **Step 1: Write the failing tests**

```csharp
namespace PokeJudge.Tests.Retrieval;

using PokeJudge.Chunking;
using PokeJudge.Ingestion;
using PokeJudge.Retrieval;

public class SectionRankFinderTests
{
    private static readonly SourceDocumentMetadata Source = new("Test Handbook", "May 21, 2026", null);

    private static ScoredChunk Chunk(string chunkId) =>
        new(new EmbeddedChunk(new TextChunk(chunkId, chunkId.Split('#')[0], $"Text for {chunkId}", Source), new[] { 1f }), 0.5);

    private static IReadOnlyList<ScoredChunk> Results(int count, params (int Rank, string ChunkId)[] placed)
    {
        var results = Enumerable.Range(1, count).Select(i => Chunk($"FILLER-{i}#0")).ToList();
        foreach (var (rank, chunkId) in placed)
        {
            results[rank - 1] = Chunk(chunkId);
        }

        return results;
    }

    [Fact]
    public void Find_SectionInTopK_IsInTopK()
    {
        var ranks = SectionRankFinder.Find(new[] { "PPG-5.5.1" }, Results(30, (3, "PPG-5.5.1#2")), topK: 5);

        Assert.Equal(new SectionRank("PPG-5.5.1", 3, SectionReach.InTopK), ranks.Single());
    }

    [Fact]
    public void Find_SectionBelowTopKButInResults_IsRerankable()
    {
        var ranks = SectionRankFinder.Find(new[] { "TCGRULES-deck-building" }, Results(30, (17, "TCGRULES-deck-building#0")), topK: 5);

        Assert.Equal(new SectionRank("TCGRULES-deck-building", 17, SectionReach.Rerankable), ranks.Single());
    }

    [Fact]
    public void Find_SectionAbsent_IsOutOfReach()
    {
        var ranks = SectionRankFinder.Find(new[] { "PPG-4.1.1" }, Results(30), topK: 5);

        Assert.Equal(new SectionRank("PPG-4.1.1", null, SectionReach.OutOfReach), ranks.Single());
    }

    [Fact]
    public void Find_SectionAppearsTwice_ReportsTheBestRank()
    {
        var ranks = SectionRankFinder.Find(new[] { "PPG-5.6.1" }, Results(30, (4, "PPG-5.6.1#3"), (9, "PPG-5.6.1#2")), topK: 5);

        Assert.Equal(4, ranks.Single().Rank);
    }

    [Fact]
    public void Find_KeepsTheOrderOfTheRequestedSections()
    {
        var ranks = SectionRankFinder.Find(new[] { "B", "A" }, Results(30, (1, "A#0"), (2, "B#0")), topK: 5);

        Assert.Equal(new[] { "B", "A" }, ranks.Select(r => r.SectionId));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PokeJudge.slnx --filter SectionRankFinderTests`
Expected: build error, `SectionRankFinder` is not defined.

- [ ] **Step 3: Write the implementation**

```csharp
namespace PokeJudge.Retrieval;

// Where a reranker could help: InTopK is already sent to the AI; Rerankable sits in the
// wider candidate list (ranks topK+1..N), where reranking can promote it; OutOfReach
// isn't in the candidates at all, so no reranker over them can recover it.
public enum SectionReach
{
    InTopK,
    Rerankable,
    OutOfReach
}

public sealed record SectionRank(string SectionId, int? Rank, SectionReach Reach);

// Pure. Reuses RetrievalEvaluator's first-match-by-SectionId rank, so "rank" means the
// same thing here as in the search-only test.
public static class SectionRankFinder
{
    public static IReadOnlyList<SectionRank> Find(IReadOnlyList<string> sectionIds, IReadOnlyList<ScoredChunk> results, int topK) =>
        sectionIds
            .Select(sectionId =>
            {
                var rank = RetrievalEvaluator.Evaluate(new RetrievalEvalCase(string.Empty, sectionId), results).Rank;
                var reach = rank is null ? SectionReach.OutOfReach
                    : rank <= topK ? SectionReach.InTopK
                    : SectionReach.Rerankable;
                return new SectionRank(sectionId, rank, reach);
            })
            .ToList();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PokeJudge.slnx --filter SectionRankFinderTests`
Expected: 5 passed.

- [ ] **Step 5: Commit**

```bash
git add PokeJudge/Retrieval/SectionRankFinder.cs PokeJudge.Tests/Retrieval/SectionRankFinderTests.cs
git commit -m "Classify how far each expected section sits from the top 5"
```

---

### Task 4: `JevResponseParser`

**Files:**
- Create: `PokeJudge/AI/JevResponseParser.cs`
- Test: `PokeJudge.Tests/AI/JevResponseParserTests.cs`

**Interfaces:**
- Produces: `public static IReadOnlyList<double> Parse(string responseJson, IReadOnlyList<string> keys)`.

The response shape comes from third-party guides: each question key at the top level next to `model` and `usage`, holding a `noul` probability. The parser accepts the value either as a bare number or as `{ "probability": n }`. Task 10 confirms the real shape; if it differs, only this file and its tests change.

- [ ] **Step 1: Write the failing tests**

```csharp
namespace PokeJudge.Tests.AI;

using PokeJudge.AI;

public class JevResponseParserTests
{
    [Fact]
    public void Parse_ObjectAnswers_ReturnsProbabilitiesInKeyOrder()
    {
        const string json = """
            { "model": "jev-1.13.0",
              "p1": { "probability": 0.12 },
              "p0": { "probability": 0.91 },
              "usage": { "input_tokens": 900, "output_tokens": 0 } }
            """;

        var scores = JevResponseParser.Parse(json, new[] { "p0", "p1" });

        Assert.Equal(new[] { 0.91, 0.12 }, scores);
    }

    [Fact]
    public void Parse_BareNumberAnswers_AreAccepted()
    {
        const string json = """{ "model": "jev-1.13.0", "p0": 0.4 }""";

        Assert.Equal(new[] { 0.4 }, JevResponseParser.Parse(json, new[] { "p0" }));
    }

    [Fact]
    public void Parse_MissingKey_ThrowsNamingIt()
    {
        const string json = """{ "model": "jev-1.13.0", "p0": { "probability": 0.4 } }""";

        var ex = Assert.Throws<InvalidOperationException>(() => JevResponseParser.Parse(json, new[] { "p0", "p1" }));

        Assert.Contains("\"p1\"", ex.Message);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("-0.1")]
    public void Parse_ProbabilityOutsideZeroToOne_Throws(string value)
    {
        var json = $$"""{ "p0": { "probability": {{value}} } }""";

        var ex = Assert.Throws<InvalidOperationException>(() => JevResponseParser.Parse(json, new[] { "p0" }));

        Assert.Contains("p0", ex.Message);
    }

    [Fact]
    public void Parse_AnswerWithoutAProbability_Throws()
    {
        const string json = """{ "p0": { "choice": "yes" } }""";

        Assert.Throws<InvalidOperationException>(() => JevResponseParser.Parse(json, new[] { "p0" }));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PokeJudge.slnx --filter JevResponseParserTests`
Expected: build error, `JevResponseParser` is not defined.

- [ ] **Step 3: Write the implementation**

```csharp
namespace PokeJudge.AI;

using System.Text.Json;

// Pure parsing of a Jev /v1/systemone response for `noul` questions, kept apart from
// the HTTP call so it can be tested with canned JSON (same split as
// GeminiEmbeddingResponseParser). The field shape came from third-party guides, not
// the live API -- see the plan's Task 10 probe. Fails loudly: an answer we can't read
// is a bug to fix, never a passage to silently rank last.
public static class JevResponseParser
{
    public static IReadOnlyList<double> Parse(string responseJson, IReadOnlyList<string> keys)
    {
        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;
        var scores = new List<double>(keys.Count);

        foreach (var key in keys)
        {
            if (!root.TryGetProperty(key, out var answer))
            {
                throw new InvalidOperationException($"Jev response has no answer for question \"{key}\".");
            }

            double probability;
            if (answer.ValueKind == JsonValueKind.Number)
            {
                probability = answer.GetDouble();
            }
            else if (answer.ValueKind == JsonValueKind.Object
                && answer.TryGetProperty("probability", out var value)
                && value.ValueKind == JsonValueKind.Number)
            {
                probability = value.GetDouble();
            }
            else
            {
                throw new InvalidOperationException($"Jev answer for \"{key}\" has no probability: {answer.GetRawText()}");
            }

            if (probability is < 0 or > 1)
            {
                throw new InvalidOperationException($"Jev answer for \"{key}\" is outside 0 to 1: {probability}");
            }

            scores.Add(probability);
        }

        return scores;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PokeJudge.slnx --filter JevResponseParserTests`
Expected: 6 passed.

- [ ] **Step 5: Commit**

```bash
git add PokeJudge/AI/JevResponseParser.cs PokeJudge.Tests/AI/JevResponseParserTests.cs
git commit -m "Parse Jev noul probabilities"
```

---

### Task 5: `IRelevanceScorer` and `RerankingRetriever`

**Files:**
- Create: `PokeJudge/Retrieval/IRelevanceScorer.cs`, `PokeJudge/Retrieval/RerankingRetriever.cs`, `PokeJudge.Tests/TestDoubles/StubRelevanceScorer.cs`
- Test: `PokeJudge.Tests/Retrieval/RerankingRetrieverTests.cs`

**Interfaces:**
- Consumes: `IRetriever.RetrieveAsync(string queryText, int topK)`, and the test double `StubRetriever` (`Enqueue`, `QueryTexts`, `TopKValues`).
- Produces:
  - `public interface IRelevanceScorer { Task<IReadOnlyList<double>> ScoreAsync(string situation, IReadOnlyList<ScoredChunk> candidates); }`
  - `public sealed record RerankedCandidate(ScoredChunk Chunk, double Relevance, int CosineRank)`
  - `public sealed class RerankingRetriever(IRetriever inner, IRelevanceScorer scorer, int candidateCount, Action<IReadOnlyList<RerankedCandidate>>? onReranked = null) : IRetriever`

- [ ] **Step 1: Write the test double and the failing tests**

```csharp
namespace PokeJudge.Tests.TestDoubles;

using PokeJudge.Retrieval;

// Deterministic test double: returns pre-scripted scores and records each call, so
// RerankingRetriever's ordering logic is tested without a Jev call.
public sealed class StubRelevanceScorer : IRelevanceScorer
{
    private readonly Queue<IReadOnlyList<double>> _scores = new();

    public List<string> Situations { get; } = new();
    public List<IReadOnlyList<ScoredChunk>> Candidates { get; } = new();

    public void Enqueue(params double[] scores) => _scores.Enqueue(scores);

    public Task<IReadOnlyList<double>> ScoreAsync(string situation, IReadOnlyList<ScoredChunk> candidates)
    {
        Situations.Add(situation);
        Candidates.Add(candidates);

        if (_scores.Count == 0)
        {
            throw new InvalidOperationException("StubRelevanceScorer has no more queued scores.");
        }

        return Task.FromResult(_scores.Dequeue());
    }
}
```

```csharp
namespace PokeJudge.Tests.Retrieval;

using PokeJudge.Chunking;
using PokeJudge.Ingestion;
using PokeJudge.Retrieval;
using PokeJudge.Tests.TestDoubles;

public class RerankingRetrieverTests
{
    private static readonly SourceDocumentMetadata Source = new("Test Handbook", "May 21, 2026", null);

    private static ScoredChunk Chunk(string id, double cosine) =>
        new(new EmbeddedChunk(new TextChunk(id, id, $"Text for {id}", Source), new[] { 1f }), cosine);

    private static readonly IReadOnlyList<ScoredChunk> FourCandidates = new[]
    {
        Chunk("a", 0.90), Chunk("b", 0.85), Chunk("c", 0.80), Chunk("d", 0.75)
    };

    [Fact]
    public async Task RetrieveAsync_AsksTheInnerRetrieverForCandidateCount()
    {
        var inner = new StubRetriever();
        inner.Enqueue(FourCandidates);
        var scorer = new StubRelevanceScorer();
        scorer.Enqueue(0.1, 0.2, 0.3, 0.4);

        await new RerankingRetriever(inner, scorer, candidateCount: 30).RetrieveAsync("query", topK: 2);

        Assert.Equal(new[] { 30 }, inner.TopKValues);
    }

    [Fact]
    public async Task RetrieveAsync_ScoresTheQueryTextAgainstEveryCandidate()
    {
        var inner = new StubRetriever();
        inner.Enqueue(FourCandidates);
        var scorer = new StubRelevanceScorer();
        scorer.Enqueue(0.1, 0.2, 0.3, 0.4);

        await new RerankingRetriever(inner, scorer, candidateCount: 30).RetrieveAsync("deck under 60", topK: 2);

        Assert.Equal("deck under 60", scorer.Situations.Single());
        Assert.Same(FourCandidates, scorer.Candidates.Single());
    }

    [Fact]
    public async Task RetrieveAsync_ReturnsTopKByRelevance()
    {
        var inner = new StubRetriever();
        inner.Enqueue(FourCandidates);
        var scorer = new StubRelevanceScorer();
        scorer.Enqueue(0.10, 0.70, 0.05, 0.95);

        var results = await new RerankingRetriever(inner, scorer, candidateCount: 30).RetrieveAsync("query", topK: 2);

        Assert.Equal(new[] { "d", "b" }, results.Select(r => r.Chunk.Chunk.ChunkId));
    }

    [Fact]
    public async Task RetrieveAsync_TiesKeepCosineOrder()
    {
        var inner = new StubRetriever();
        inner.Enqueue(FourCandidates);
        var scorer = new StubRelevanceScorer();
        scorer.Enqueue(0.5, 0.5, 0.5, 0.9);

        var results = await new RerankingRetriever(inner, scorer, candidateCount: 30).RetrieveAsync("query", topK: 3);

        Assert.Equal(new[] { "d", "a", "b" }, results.Select(r => r.Chunk.Chunk.ChunkId));
    }

    [Fact]
    public async Task RetrieveAsync_KeepsTheCosineScore()
    {
        var inner = new StubRetriever();
        inner.Enqueue(FourCandidates);
        var scorer = new StubRelevanceScorer();
        scorer.Enqueue(0.1, 0.2, 0.3, 0.99);

        var results = await new RerankingRetriever(inner, scorer, candidateCount: 30).RetrieveAsync("query", topK: 1);

        Assert.Equal(0.75, results.Single().Score);
    }

    [Fact]
    public async Task RetrieveAsync_ReportsEveryCandidateWithItsCosineRank()
    {
        var inner = new StubRetriever();
        inner.Enqueue(FourCandidates);
        var scorer = new StubRelevanceScorer();
        scorer.Enqueue(0.1, 0.2, 0.3, 0.99);
        IReadOnlyList<RerankedCandidate>? reported = null;

        await new RerankingRetriever(inner, scorer, candidateCount: 30, onReranked: r => reported = r).RetrieveAsync("query", topK: 1);

        Assert.Equal(4, reported!.Count);
        Assert.Equal(("d", 0.99, 4), (reported[0].Chunk.Chunk.Chunk.ChunkId, reported[0].Relevance, reported[0].CosineRank));
    }

    [Fact]
    public async Task RetrieveAsync_ScoreCountMismatch_Throws()
    {
        var inner = new StubRetriever();
        inner.Enqueue(FourCandidates);
        var scorer = new StubRelevanceScorer();
        scorer.Enqueue(0.1, 0.2);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new RerankingRetriever(inner, scorer, candidateCount: 30).RetrieveAsync("query", topK: 2));

        Assert.Contains("2 score(s) for 4 candidate(s)", ex.Message);
    }

    [Fact]
    public async Task RetrieveAsync_NoCandidates_SkipsTheScorer()
    {
        var inner = new StubRetriever();
        inner.Enqueue(Array.Empty<ScoredChunk>());
        var scorer = new StubRelevanceScorer();

        var results = await new RerankingRetriever(inner, scorer, candidateCount: 30).RetrieveAsync("query", topK: 5);

        Assert.Empty(results);
        Assert.Empty(scorer.Situations);
    }

    [Fact]
    public async Task RetrieveAsync_TopKAboveCandidateCount_Throws()
    {
        var retriever = new RerankingRetriever(new StubRetriever(), new StubRelevanceScorer(), candidateCount: 3);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => retriever.RetrieveAsync("query", topK: 5));
    }

    [Fact]
    public void Constructor_CandidateCountBelowOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RerankingRetriever(new StubRetriever(), new StubRelevanceScorer(), candidateCount: 0));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PokeJudge.slnx --filter RerankingRetrieverTests`
Expected: build error, `IRelevanceScorer` and `RerankingRetriever` are not defined.

- [ ] **Step 3: Write the implementation**

```csharp
namespace PokeJudge.Retrieval;

// One relevance score in [0, 1] per candidate, in input order: how strongly the passage
// governs the situation. An interface so RerankingRetriever's ordering logic is tested
// without the network (JevRelevanceScorer is the production implementation).
public interface IRelevanceScorer
{
    Task<IReadOnlyList<double>> ScoreAsync(string situation, IReadOnlyList<ScoredChunk> candidates);
}
```

```csharp
namespace PokeJudge.Retrieval;

public sealed record RerankedCandidate(ScoredChunk Chunk, double Relevance, int CosineRank);

// Step 4 experiment: widen the cosine search to candidateCount, let an IRelevanceScorer
// judge which passages actually govern the situation, and pass on the best topK. A
// decorator, so ClarificationLoop and everything downstream is unchanged. Each
// ScoredChunk keeps its cosine Score -- only the order changes -- so the "score" shown
// in prompts (PromptBuilder) keeps its meaning. LINQ's OrderByDescending is stable, so
// equal relevance keeps cosine order.
public sealed class RerankingRetriever : IRetriever
{
    private readonly IRetriever _inner;
    private readonly IRelevanceScorer _scorer;
    private readonly int _candidateCount;
    private readonly Action<IReadOnlyList<RerankedCandidate>>? _onReranked;

    public RerankingRetriever(
        IRetriever inner,
        IRelevanceScorer scorer,
        int candidateCount,
        Action<IReadOnlyList<RerankedCandidate>>? onReranked = null)
    {
        if (candidateCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(candidateCount), candidateCount, "Candidate count must be at least 1.");
        }

        _inner = inner;
        _scorer = scorer;
        _candidateCount = candidateCount;
        _onReranked = onReranked;
    }

    public async Task<IReadOnlyList<ScoredChunk>> RetrieveAsync(string queryText, int topK)
    {
        if (topK > _candidateCount)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), topK, $"topK ({topK}) cannot exceed the candidate count ({_candidateCount}).");
        }

        var candidates = await _inner.RetrieveAsync(queryText, _candidateCount);
        if (candidates.Count == 0)
        {
            return candidates;
        }

        var scores = await _scorer.ScoreAsync(queryText, candidates);
        if (scores.Count != candidates.Count)
        {
            throw new InvalidOperationException(
                $"Relevance scorer returned {scores.Count} score(s) for {candidates.Count} candidate(s).");
        }

        var reranked = candidates
            .Select((chunk, i) => new RerankedCandidate(chunk, scores[i], CosineRank: i + 1))
            .OrderByDescending(c => c.Relevance)
            .ToList();

        _onReranked?.Invoke(reranked);

        return reranked.Take(topK).Select(c => c.Chunk).ToList();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PokeJudge.slnx --filter RerankingRetrieverTests`
Expected: 10 passed.

- [ ] **Step 5: Commit**

```bash
git add PokeJudge/Retrieval/IRelevanceScorer.cs PokeJudge/Retrieval/RerankingRetriever.cs PokeJudge.Tests/TestDoubles/StubRelevanceScorer.cs PokeJudge.Tests/Retrieval/RerankingRetrieverTests.cs
git commit -m "Add a reranking retriever decorator"
```

---

### Task 6: `JevRelevanceScorer`

**Files:**
- Create: `PokeJudge/AI/JevRelevanceScorer.cs`
- Test: `PokeJudge.Tests/AI/JevRelevanceScorerTests.cs`

**Interfaces:**
- Consumes: `JevResponseParser.Parse`, `IRelevanceScorer`.
- Produces:
  - `public sealed class JevRelevanceScorer(string apiKey, string modelId) : IRelevanceScorer`
  - `internal static object BuildRequestBody(string modelId, string situation, IReadOnlyList<ScoredChunk> candidates)`
  - `internal static IReadOnlyList<string> QuestionKeys(int count)`

- [ ] **Step 1: Write the failing tests**

```csharp
namespace PokeJudge.Tests.AI;

using System.Text.Json;
using PokeJudge.AI;
using PokeJudge.Chunking;
using PokeJudge.Ingestion;
using PokeJudge.Retrieval;

public class JevRelevanceScorerTests
{
    private static readonly SourceDocumentMetadata Source = new("Test Handbook", "May 21, 2026", null);

    private static ScoredChunk Chunk(string id, string text) =>
        new(new EmbeddedChunk(new TextChunk(id, id.Split('#')[0], text, Source), new[] { 1f }), 0.8);

    private static JsonElement Body() =>
        // Same options JsonContent.Create uses when the client sends the request.
        JsonSerializer.SerializeToElement(
            JevRelevanceScorer.BuildRequestBody(
                "jev-latest",
                "Player drew an extra card.",
                new[] { Chunk("PPG-5.5.1#0", "Drawing extra cards..."), Chunk("PPG-4.2.1#1", "Supporter cards...") }),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact]
    public void BuildRequestBody_SendsTheModel()
    {
        Assert.Equal("jev-latest", Body().GetProperty("model").GetString());
    }

    [Fact]
    public void BuildRequestBody_StateHoldsTheSituationAndEveryPassage()
    {
        var state = Body().GetProperty("state");

        Assert.Equal("Player drew an extra card.", state.GetProperty("situation").GetString());
        var passages = state.GetProperty("passages").EnumerateArray().ToList();
        Assert.Equal(new[] { "p0", "p1" }, passages.Select(p => p.GetProperty("id").GetString()));
        Assert.Equal("PPG-5.5.1#0", passages[0].GetProperty("source").GetString());
        Assert.Equal("Supporter cards...", passages[1].GetProperty("text").GetString());
    }

    [Fact]
    public void BuildRequestBody_AsksOneNoulQuestionPerPassage()
    {
        var questions = Body().GetProperty("questions").EnumerateObject().ToList();

        Assert.Equal(new[] { "p0", "p1" }, questions.Select(q => q.Name));
        Assert.All(questions, q => Assert.Equal("noul", q.Value.GetProperty("type").GetString()));
        Assert.Contains("p1", questions[1].Value.GetProperty("instructions").GetString());
    }

    [Fact]
    public void QuestionKeys_AreZeroBasedAndPrefixed()
    {
        Assert.Equal(new[] { "p0", "p1", "p2" }, JevRelevanceScorer.QuestionKeys(3));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PokeJudge.slnx --filter JevRelevanceScorerTests`
Expected: build error, `JevRelevanceScorer` is not defined.

- [ ] **Step 3: Write the implementation**

```csharp
namespace PokeJudge.AI;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using PokeJudge.Retrieval;

// Scores retrieved passages with TypeSafe's Jev, a classification model: one request
// whose state holds the situation and every candidate passage, with one `noul`
// (probability-of-yes) question per passage, answered in a single parallel pass.
// Endpoint and body shape are from TypeSafe's published guides; the response shape is
// isolated in JevResponseParser and confirmed by the plan's Task 10 probe. Same
// fail-loudly pattern as GeminiEmbeddingClient.
public sealed class JevRelevanceScorer : IRelevanceScorer
{
    private static readonly HttpClient Http = new();

    internal const string Endpoint = "https://api.typesafe.ai/v1/systemone";

    // {0} is the passage id. Asks about governing the outcome, not topical overlap --
    // the distinction cosine similarity can't make (deck-under-60's failure).
    internal const string Instructions =
        "Does passage {0} state a rule or policy that a Pokémon TCG judge would apply to decide this situation? " +
        "Answer yes only if the passage governs the outcome or the correct remedy, not if it merely mentions related words.";

    private readonly string _apiKey;
    private readonly string _modelId;

    public JevRelevanceScorer(string apiKey, string modelId)
    {
        _apiKey = apiKey;
        _modelId = modelId;
    }

    public async Task<IReadOnlyList<double>> ScoreAsync(string situation, IReadOnlyList<ScoredChunk> candidates)
    {
        if (candidates.Count == 0)
        {
            return Array.Empty<double>();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(BuildRequestBody(_modelId, situation, candidates))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var httpResponse = await Http.SendAsync(request);
        if (!httpResponse.IsSuccessStatusCode)
        {
            var errorBody = await httpResponse.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Jev API request failed ({(int)httpResponse.StatusCode} {httpResponse.StatusCode}): {errorBody}");
        }

        var responseBody = await httpResponse.Content.ReadAsStringAsync();

        return JevResponseParser.Parse(responseBody, QuestionKeys(candidates.Count));
    }

    internal static IReadOnlyList<string> QuestionKeys(int count) =>
        Enumerable.Range(0, count).Select(i => $"p{i}").ToList();

    internal static object BuildRequestBody(string modelId, string situation, IReadOnlyList<ScoredChunk> candidates)
    {
        var keys = QuestionKeys(candidates.Count);

        return new
        {
            model = modelId,
            state = new
            {
                situation,
                passages = candidates.Select((c, i) => new
                {
                    id = keys[i],
                    source = c.Chunk.Chunk.ChunkId,
                    text = c.Chunk.Chunk.Text
                }).ToList()
            },
            questions = keys.ToDictionary(
                key => key,
                key => (object)new { type = "noul", instructions = string.Format(Instructions, key) })
        };
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PokeJudge.slnx --filter JevRelevanceScorerTests`
Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add PokeJudge/AI/JevRelevanceScorer.cs PokeJudge.Tests/AI/JevRelevanceScorerTests.cs
git commit -m "Score passages with Jev"
```

---

### Task 7: `JevSettings`

**Files:**
- Create: `PokeJudge/AI/JevSettings.cs`
- Test: `PokeJudge.Tests/AI/JevSettingsTests.cs`

**Interfaces:**
- Produces: `public sealed record JevSettings(string ApiKey, string Model, int CandidateCount)` and `public static (JevSettings? Settings, string? Error) Read(Func<string, string?> get)`. Pass `key => config[key]`.

- [ ] **Step 1: Write the failing tests**

```csharp
namespace PokeJudge.Tests.AI;

using PokeJudge.AI;

public class JevSettingsTests
{
    private static Func<string, string?> Config(params (string Key, string Value)[] values) =>
        key => values.FirstOrDefault(v => v.Key == key).Value;

    [Fact]
    public void Read_OnlyApiKey_UsesDefaults()
    {
        var (settings, error) = JevSettings.Read(Config(("Jev:ApiKey", " key ")));

        Assert.Null(error);
        Assert.Equal(new JevSettings("key", "jev-latest", 30), settings);
    }

    [Fact]
    public void Read_OverridesModelAndCandidateCount()
    {
        var (settings, _) = JevSettings.Read(Config(("Jev:ApiKey", "key"), ("Jev:Model", "jev-1.13.0"), ("Jev:CandidateCount", "20")));

        Assert.Equal(new JevSettings("key", "jev-1.13.0", 20), settings);
    }

    [Fact]
    public void Read_MissingApiKey_ReturnsErrorWithTheSetupCommand()
    {
        var (settings, error) = JevSettings.Read(Config());

        Assert.Null(settings);
        Assert.Contains("dotnet user-secrets set \"Jev:ApiKey\"", error);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("abc")]
    public void Read_InvalidCandidateCount_ReturnsError(string value)
    {
        var (_, error) = JevSettings.Read(Config(("Jev:ApiKey", "key"), ("Jev:CandidateCount", value)));

        Assert.Equal($"Jev:CandidateCount must be a positive integer, got \"{value}\".", error);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PokeJudge.slnx --filter JevSettingsTests`
Expected: build error, `JevSettings` is not defined.

- [ ] **Step 3: Write the implementation**

```csharp
namespace PokeJudge.AI;

// Reads the Jev user-secrets only when a command asks for `--rerank jev`, so runs
// without reranking never need a Jev key. Never echoes the key itself.
public sealed record JevSettings(string ApiKey, string Model, int CandidateCount)
{
    public const string DefaultModel = "jev-latest";
    public const int DefaultCandidateCount = 30;

    public static (JevSettings? Settings, string? Error) Read(Func<string, string?> get)
    {
        var apiKey = get("Jev:ApiKey")?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (null, "Missing Jev API key. Set it with: dotnet user-secrets set \"Jev:ApiKey\" \"<your-key>\" --project PokeJudge");
        }

        var candidateCount = DefaultCandidateCount;
        var candidateSetting = get("Jev:CandidateCount");
        if (!string.IsNullOrWhiteSpace(candidateSetting)
            && (!int.TryParse(candidateSetting, out candidateCount) || candidateCount < 1))
        {
            return (null, $"Jev:CandidateCount must be a positive integer, got \"{candidateSetting}\".");
        }

        var model = get("Jev:Model");
        return (new JevSettings(apiKey, string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim(), candidateCount), null);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PokeJudge.slnx --filter JevSettingsTests`
Expected: 5 passed.

- [ ] **Step 5: Commit**

```bash
git add PokeJudge/AI/JevSettings.cs PokeJudge.Tests/AI/JevSettingsTests.cs
git commit -m "Read Jev settings from user secrets"
```

---

### Task 8: Wire the commands in `Program.cs`

**Files:**
- Modify: `PokeJudge/Program.cs`. This covers the dispatch (lines 113–142), `RunSearch` (440–474), `RunRetrievalEval` (480–524), `RunScenarioEval` (550 onward, retriever at 574, per-turn output near 646), and a new `RunRetrievalDepth`.
- Modify: `README.md` command list.

There are no unit tests for this task (`Program.cs` top-level code). The logic lives in Tasks 1–7.

- [ ] **Step 1: Add one retriever factory** next to `CreateEmbeddingClient` (line ~803). Every reranked command builds its retriever through it:

```csharp
// Single place a reranker is attached, so search, eval and evaluate rerank identically.
// Returns an error (never throws) for a missing Jev key, so the command can exit 1.
static (IRetriever? Retriever, string? Error) CreateRetriever(
    IEmbeddingClient embeddingClient,
    InMemoryVectorStore store,
    string? rerank,
    IConfiguration config,
    Action<IReadOnlyList<RerankedCandidate>>? onReranked = null)
{
    IRetriever retriever = new VectorStoreRetriever(embeddingClient, store);
    if (rerank != RerankOption.Jev)
    {
        return (retriever, null);
    }

    var (jev, error) = JevSettings.Read(key => config[key]);
    if (error is not null)
    {
        return (null, error);
    }

    return (new RerankingRetriever(retriever, new JevRelevanceScorer(jev!.ApiKey, jev.Model), jev.CandidateCount, onReranked), null);
}
```

- [ ] **Step 2: Pass `config` into the three commands**, and route `retrieval-depth`:

```csharp
if (args.Length > 0 && args[0] == "search")
{
    return await RunSearch(args, apiKey, config);
}

if (args.Length > 0 && args[0] == "eval")
{
    return await RunRetrievalEval(args, apiKey, config);
}

if (args.Length > 0 && args[0] == "retrieval-depth")
{
    return await RunRetrievalDepth(apiKey);
}
```

In the `evaluate` branch, call `RunScenarioEval(args, apiKey, modelId, requestsPerMinute, config)`.

- [ ] **Step 3: `RunSearch`.** Parse with `RerankOption.Extract`, then `SearchArgsParser.Parse`. Build the retriever with `CreateRetriever`. When reranking, use an `onReranked` callback to print every candidate:

```csharp
var (remaining, rerank, rerankError) = RerankOption.Extract(args.Skip(1).ToList());
if (rerankError is not null)
{
    Console.Error.WriteLine(rerankError);
    return 1;
}

var (searchArgs, parseError) = SearchArgsParser.Parse(remaining);
if (parseError is not null)
{
    Console.Error.WriteLine(parseError);
    return 1;
}
// ... load chunks and the store as today ...
var (retriever, retrieverError) = CreateRetriever(embeddingClient, store, rerank, config, onReranked: candidates =>
{
    Console.WriteLine($"Jev scored {candidates.Count} candidate(s):");
    foreach (var c in candidates)
    {
        Console.WriteLine($"  [{c.Relevance:F3}] {c.Chunk.Chunk.Chunk.ChunkId} (cosine rank {c.CosineRank}, {c.Chunk.Score:F4})");
    }
    Console.WriteLine();
});
if (retrieverError is not null)
{
    Console.Error.WriteLine(retrieverError);
    return 1;
}
var results = await retriever!.RetrieveAsync(searchArgs!.Query, searchArgs.TopK);
```

Keep the existing per-result output. Add `Reranked by: Jev ({candidateCount} candidates)` to the header when `rerank` is set.

- [ ] **Step 4: `RunRetrievalEval`.** Accept only an optional `--rerank jev`:

```csharp
var (remaining, rerank, rerankError) = RerankOption.Extract(args.Skip(1).ToList());
if (rerankError is not null || remaining.Count > 0)
{
    Console.Error.WriteLine(rerankError ?? "Usage: dotnet run -- eval [--rerank jev]");
    return 1;
}
```

Replace the batch embed plus `store.Search(queryVectors[i], topK: 5)` with `await retriever.RetrieveAsync(evalCase.Query, SearchArgsParser.DefaultTopK)`, from `CreateRetriever`. This is one embedding call per case, which is fine at 7–30 cases. Print `Reranked by: Jev` in the header when set. Leave the HIT/MISS lines and the total unchanged.

- [ ] **Step 5: `RunRetrievalDepth` (new)**, with embeddings only and no chat calls:

```csharp
// Step 4 diagnostic: for each scenario, where do its expected sections rank in the
// top 30 for the turn-1 description? Rerankable (ranks 6-30) means a top-30 reranker
// can promote it; OutOfReach means no reranker over 30 candidates can.
static async Task<int> RunRetrievalDepth(string apiKey)
{
    const int depth = JevSettings.DefaultCandidateCount;
    var topK = SearchArgsParser.DefaultTopK;
    var chunks = LoadAllEmbeddedChunks();
    if (chunks.Count == 0)
    {
        Console.Error.WriteLine("No chunked/embedded documents found. Run `ingest` and `chunk` first.");
        return 1;
    }

    IEmbeddingClient embeddingClient = CreateEmbeddingClient(apiKey);
    var store = CreateVectorStore(chunks);
    var scenarios = EvalDataset.Scenarios;
    var vectors = await embeddingClient.EmbedBatchAsync(scenarios.Select(s => s.InitialDescription).ToList());

    Console.WriteLine($"=== PokeJudge AI — Retrieval Depth (top {depth}, AI sees top {topK}) ===\n");
    var counts = new Dictionary<SectionReach, int>();

    for (var i = 0; i < scenarios.Count; i++)
    {
        var results = store.Search(vectors[i], depth);
        Console.WriteLine($"[{scenarios[i].Id}] rank {topK} cutoff: {results.ElementAtOrDefault(topK - 1)?.Chunk.Chunk.ChunkId ?? "(none)"}");

        if (scenarios[i].ExpectedMaterialSectionIds.Count == 0)
        {
            Console.WriteLine("    (no expected sections)");
        }

        foreach (var rank in SectionRankFinder.Find(scenarios[i].ExpectedMaterialSectionIds, results, topK))
        {
            counts[rank.Reach] = counts.GetValueOrDefault(rank.Reach) + 1;
            Console.WriteLine($"    {rank.SectionId}: {(rank.Rank is { } r ? $"rank {r}" : $"not in top {depth}")} ({rank.Reach})");
        }
        Console.WriteLine();
    }

    Console.WriteLine($"Sections in top {topK}: {counts.GetValueOrDefault(SectionReach.InTopK)}, " +
        $"rerankable: {counts.GetValueOrDefault(SectionReach.Rerankable)}, " +
        $"out of reach: {counts.GetValueOrDefault(SectionReach.OutOfReach)}");
    return 0;
}
```

- [ ] **Step 6: `RunScenarioEval`**
  - Before `EvalScenarioSelector.Select`, run `RerankOption.Extract(args.Skip(1).ToList())`, and pass `remaining` to the selector.
  - Replace line 574 with `CreateRetriever(embeddingClient, store, rerank, config)`. Exit 1 on error.
  - Print `Reranked by: Jev` in the header when set.
  - Inside the `turnIndex` loop, before the questions, print each turn's top 5:

```csharp
var retrieved = trajectory.Turns[turnIndex].RetrievedChunks;
Console.WriteLine($"  [Turn {turnIndex + 1} retrieved] {string.Join(", ", retrieved.Select(c => $"{c.Chunk.Chunk.ChunkId} ({c.Score:F4})"))}");
```

  - Update the selector's usage text in the error path to mention `[--rerank jev]`. Print it from `Program.cs` only, so `EvalScenarioSelector`'s tests don't change.

- [ ] **Step 7: Build, test, and smoke-check the arguments without a key**

Run: `dotnet build PokeJudge.slnx` then `dotnet test PokeJudge.slnx`
Expected: build succeeds; every test passes, including the ones from Tasks 1–7.

Run: `dotnet run --project PokeJudge -- search --top 0 x`
Expected: `"--top" requires a positive integer, got "0".` and exit code 1. This needs the Gemini key, because config loads first; skip it if no key is set.

- [ ] **Step 8: Document the commands** in the README's command list:
  - `search [--top <n>] [--rerank jev] <query>`
  - `eval [--rerank jev]`
  - `retrieval-depth`
  - `evaluate ... [--rerank jev]`
  - the `Jev:*` user-secrets

- [ ] **Step 9: Commit**

```bash
git add PokeJudge/Program.cs README.md
git commit -m "Wire opt-in Jev reranking and the retrieval-depth command"
```

---

## Part 2: local runs (needs the corpus and keys)

### Task 9: Grow the search-only test to about 30 cases

**Files:**
- Modify: `PokeJudge/Retrieval/RetrievalEvalSet.cs`
- Modify: `PokeJudge.Tests/Retrieval/RetrievalEvalSetTests.cs`

This needs the ingested corpus, so that every expected section ID can be checked against `PokeJudge/Ingestion/Output/*.json`. The original set was written the same way: "inspected directly before writing these, not guessed at".

- [ ] **Step 1: Write the failing test.** Rename `HasBetweenSixAndEightCases` to `HasBetweenTwentyFiveAndThirtyFiveCases`, with `Assert.InRange(RetrievalEvalSet.Cases.Count, 25, 35)`.
- [ ] **Step 2: Run** `dotnet test PokeJudge.slnx --filter RetrievalEvalSetTests`. Expected: the renamed test fails, because there are 7 cases.
- [ ] **Step 3: Add cases.** Phrase each query in plain judge language, not copied from the source text.
  - **Coverage:** at least one case per PPG and TCGRULES section that `EvalDataset` expects.
  - **Required:** `PPG-5.5.1`, `TCGRULES-deck-building`, `TCGRULES-appendix-3-ace-spec-cards`, `PPG-4.1.1` and `TCGRULES-turn-actions`.
  - **Fix the stale case:** re-point repeat violations from `PPTRH-7.5` to `PPG-4.2.2`. Note why in the file header: `EvalDataset` retargeted that scenario, and the old expectation predates the PPG ingestion.
  - Update the header comment, which today says the cases cover PPTRH and TCGTH only.
- [ ] **Step 4: Run** `dotnet test PokeJudge.slnx`. Expected: all pass.
- [ ] **Step 5: Commit** with the message "Grow the search-only test to cover PPG and TCGRULES".

---

### Task 10: Jev probe

The goal is to confirm the API before trusting it. No code changes unless the probe shows the request or response shape is wrong.

- [ ] **Step 1:** Run:
  ```
  dotnet user-secrets set "Jev:ApiKey" "<key>" --project PokeJudge
  dotnet run --project PokeJudge -- search --top 5 --rerank jev "A player drew an extra card during their draw step"
  ```
- [ ] **Step 2: Check the response.** If it fails with an `HttpRequestException` (a 4xx naming a field) or an `InvalidOperationException` from the parser, fix the code:
  - Fix `BuildRequestBody` or `JevResponseParser` to match the real API.
  - Update their tests, with canned JSON copied from a real response, keys removed.
  - Commit with "Match Jev's actual request/response shape".
- [ ] **Step 3: Sanity-check the scores.** `PPG-5.5.1` excerpts, if present among the 30, should score well above `PPG-4.2.1` (Supporter rules).
  - Record the latency: time the command with and without `--rerank`.
  - If 30 passages exceed the state size limit, change `JevRelevanceScorer` to one request per passage. Keep `IRelevanceScorer` unchanged and update its tests.

---

### Task 11: Baseline capture (no reranking)

- [ ] **Step 1:** Run these, sending output to scratch files outside the repo:
  ```
  dotnet run --project PokeJudge -- eval
  dotnet run --project PokeJudge -- retrieval-depth
  dotnet run --project PokeJudge -- evaluate --repeat 3
  ```
- [ ] **Step 2:** Write `docs/superpowers/plans/2026-10-04-retrieval-baseline-results.md`:
  - **Search-only test:** the score.
  - **Retrieval depth:** a per-scenario table of expected section, rank, reach, and the rank-5 chunk.
  - **Evaluate:** pass rate per scenario out of 3, with the per-turn top 5 for the four targets.
  - **Contradictions:** anything that contradicts the published baseline. Compare the four targets against the artifact.
- [ ] **Step 3:** Commit with "Record the retrieval baseline".

---

### Task 12: Reranked run and decision

- [ ] **Step 1:** Run the same three commands with `--rerank jev` (`retrieval-depth` doesn't take the flag; skip it).
- [ ] **Step 2:** Write `docs/superpowers/plans/2026-10-04-jev-reranking-results.md`, with a before/after table per scenario covering:
  - each needed section received (yes/no, per repeat)
  - pass count out of 3
  - added latency per turn
  - Jev cost for the run

  Then apply the spec's adoption rule, decided before running:
  - at least 2 of the 4 targets complete in most repeats
  - no control loses a needed section in most repeats
  - the search-only score goes up
  - latency stays under about 2 s per turn
- [ ] **Step 3:** Update Step 4 in `docs/superpowers/plans/2026-09-26-improvement-plan.md` with the outcome. Either propose making reranking the default as a separate change, or record why not: rules out of reach, so try options B and C; or Jev ranks poorly.
- [ ] **Step 4:** Commit with "Record the Jev reranking results".

**Success:** both results documents are committed, the adoption verdict is stated with its evidence, and CI is green.
