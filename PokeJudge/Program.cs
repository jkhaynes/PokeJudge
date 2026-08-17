using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using PokeJudge.AI;
using PokeJudge.Chunking;
using PokeJudge.Clarification;
using PokeJudge.Ingestion;
using PokeJudge.Retrieval;

// ---------------------------------------------------------------------------
// Milestone 2 — Judge-Focused Prompting, Clarification, Structured Responses
//
// Grows Milestone 1's single raw LLM call into a multi-turn clarification
// loop: assess sufficiency against a hand-authored mock corpus + known
// facts (structured output, not parsed free text), ask clarifying questions
// when insufficient, classify the judge's free-text answers into confirmed
// facts vs. hypotheses, and re-assess until sufficient or a turn cap is hit.
// See .project-plans for the plan this implements.
//
// Milestone 3 — Document Ingestion
//
// Adds a second, entirely deterministic mode with no LLM calls at all:
// `dotnet run -- ingest <path-to-pdf> <document-code>` extracts, normalizes,
// and sections a real policy PDF into citable IngestedSection records, prints
// a raw-vs-normalized comparison, and writes the result to a local,
// gitignored JSON file for Milestone 4 to consume.
//
// Milestone 4 — Chunking and Embeddings
//
// Adds `dotnet run -- chunk <document-code>`: loads a previously-ingested
// document, splits its sections into sentence-boundary-aware chunks, and
// embeds each chunk via Gemini's batch embedding API (skipping chunks
// already embedded in a prior run), writing the result to another local,
// gitignored JSON file for Milestone 5 to eventually index. This mode does
// need the Gemini API key (embedding is a real model call), unlike `ingest`.
//
// Milestone 5 — Vector Search
//
// Adds `dotnet run -- search <query text>` and `dotnet run -- eval`. Both
// load every already-chunked/embedded document into an in-process,
// brute-force cosine-similarity vector store (no vector database -- see
// .project-plans/milestone-5/plan.md for why that's the right choice at this
// scale). `search` embeds a raw query and prints the top-5 most similar
// chunks; `eval` runs a small hand-authored set of judge-scenario queries
// against known-correct sections and reports hit/miss -- a deterministic
// check involving the embedding call but zero chat/completion model calls,
// demonstrating retrieval quality is measurable independent of generation
// quality.
//
// Milestone 6 — RAG
//
// The default mode below is rebuilt: Milestone 2's fixed, hand-authored
// MockCorpus is gone. The judge now types a free-text scenario description,
// and ClarificationLoop retrieves real chunks from Milestone 5's vector store
// every turn (retrieve -> assess -> clarify -> re-retrieve, PRD SS11) instead
// of reasoning over a static mock corpus. Once sufficient, one more retrieval
// runs against the complete accumulated scenario and RulingGenerator produces
// a structured ruling -- recommendation, explanation, repair steps, penalty
// guidance, cited chunk IDs, and a first-pass, model-assigned Source Support
// label (Strong/Partial/Insufficient), not yet checked against deterministic
// criteria (that's Milestone 7). See .project-plans/milestone-6/plan.md.
// ---------------------------------------------------------------------------

if (args.Length > 0 && args[0] == "ingest")
{
    return RunIngestion(args);
}

var config = new ConfigurationBuilder()
    .AddUserSecrets<LlmClientMarker>()
    .Build();

var apiKey = config["Gemini:ApiKey"]?.Trim();
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine(
        "Missing Gemini API key.");
    return 1;
}

var modelId = config["Gemini:Model"] ?? "gemini-flash-lite-latest";

if (args.Length > 0 && args[0] == "chunk")
{
    return await RunChunking(args, apiKey);
}

if (args.Length > 0 && args[0] == "search")
{
    return await RunSearch(args, apiKey);
}

if (args.Length > 0 && args[0] == "eval")
{
    return await RunRetrievalEval(apiKey);
}

ILlmClient llmClient = new GeminiLlmClient(apiKey, modelId);

var defaultFlowChunks = LoadAllEmbeddedChunks();
if (defaultFlowChunks.Count == 0)
{
    Console.Error.WriteLine("No chunked/embedded documents found. Run `ingest` and `chunk` first.");
    return 1;
}

IEmbeddingClient defaultFlowEmbeddingClient = CreateEmbeddingClient(apiKey);
var defaultFlowVectorStore = CreateVectorStore(defaultFlowChunks);
IRetriever retriever = new VectorStoreRetriever(defaultFlowEmbeddingClient, defaultFlowVectorStore);

var loop = new ClarificationLoop(llmClient, retriever);
var rulingGenerator = new RulingGenerator(llmClient);

Console.WriteLine("=== PokeJudge AI — Milestone 6 RAG ===\n");
Console.Write("Describe the scenario: ");
var scenarioDescription = Console.ReadLine() ?? string.Empty;

if (string.IsNullOrWhiteSpace(scenarioDescription))
{
    Console.Error.WriteLine("A scenario description is required.");
    return 1;
}

// Captured from onAssessment so the turn that reaches sufficiency doesn't
// need a second, guaranteed-identical retrieval call before ruling generation
// -- the loop already retrieved against these exact confirmed facts moments
// earlier, and RetrievalQueryBuilder.Build is a pure function of them.
IReadOnlyList<ScoredChunk>? lastRetrievedChunks = null;

var outcome = await loop.RunAsync(
    scenarioDescription,
    askJudge: question =>
    {
        Console.WriteLine($"\n[Clarifying question — re: {question.RelatedChunkId}] {question.Question}");
        Console.Write("Your answer: ");
        return Task.FromResult(Console.ReadLine() ?? string.Empty);
    },
    onAssessment: (result, retrievedChunks) =>
    {
        lastRetrievedChunks = retrievedChunks;

        Console.WriteLine($"\n[Retrieved {retrievedChunks.Count} chunk(s) for this turn]");
        foreach (var chunk in retrievedChunks)
        {
            Console.WriteLine($"  [{chunk.Score:F4}] {chunk.Chunk.Chunk.ChunkId}");
        }

        Console.WriteLine(result.IsSufficient
            ? "[Assessment] Sufficient — generating ruling..."
            : $"[Assessment] Insufficient — {result.Questions.Count} clarifying question(s) needed.");
    });

Console.WriteLine("\n=== Result ===");
Console.WriteLine($"Turns used: {outcome.TurnsUsed}");

Console.WriteLine("\nConfirmed facts:");
if (outcome.State.ConfirmedFacts.Count == 0)
{
    Console.WriteLine("  (none)");
}
foreach (var fact in outcome.State.ConfirmedFacts)
{
    Console.WriteLine($"  - {fact}");
}

Console.WriteLine("\nHypotheses (unconfirmed):");
if (outcome.State.Hypotheses.Count == 0)
{
    Console.WriteLine("  (none)");
}
foreach (var hypothesis in outcome.State.Hypotheses)
{
    Console.WriteLine($"  - {hypothesis}");
}

if (!outcome.Sufficient)
{
    // PRD SS9: must not issue a ruling when material facts are still flagged missing.
    Console.WriteLine("\nTurn cap reached without sufficient facts — no ruling produced.");
    return 0;
}

// PRD FR7: finalize retrieval against the complete accumulated scenario before generating.
// The loop's last turn already retrieved against these exact confirmed facts
// (nothing adds facts between a turn reporting sufficient and RunAsync returning),
// so reuse that captured result instead of repeating an identical embedding + search call.
var finalChunks = lastRetrievedChunks!;

var ruling = await rulingGenerator.GenerateAsync(scenarioDescription, outcome.State, finalChunks);

Console.WriteLine($"\nRecommendation: {ruling.Recommendation}");
Console.WriteLine($"Source Support: {ruling.SourceSupport} — {ruling.SourceSupportRationale}");
Console.WriteLine($"\nExplanation: {ruling.Explanation}");

if (ruling.RepairSteps.Count > 0)
{
    Console.WriteLine("\nRepair steps:");
    foreach (var step in ruling.RepairSteps)
    {
        Console.WriteLine($"  - {step}");
    }
}

if (ruling.PenaltyGuidance is not null)
{
    Console.WriteLine($"\nPenalty guidance: {ruling.PenaltyGuidance}");
}

Console.WriteLine($"\nCited chunk IDs: {string.Join(", ", ruling.CitedChunkIds)}");

return 0;

// ---------------------------------------------------------------------------
// Milestone 3 ingestion mode. Known layouts for the two real documents this
// milestone has been run against so far -- both title page 1, TOC pages 2-3,
// body starting immediately after the TOC. A newly-supplied document isn't
// auto-detected; its layout has to be inspected and added here (out of
// scope to detect automatically -- see .project-plans/milestone-3/plan.md).
// ---------------------------------------------------------------------------
static int RunIngestion(string[] args)
{
    var knownDocuments = new Dictionary<string, (string Title, int TitlePageNumber, (int Start, int End) TocPageRange)>(StringComparer.OrdinalIgnoreCase)
    {
        ["PPTRH"] = ("Play! Pokemon Tournament Rules Handbook", 1, (2, 3)),
        ["TCGTH"] = ("Pokemon TCG Tournament Handbook", 1, (2, 3)),
    };

    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: dotnet run -- ingest <path-to-pdf> <document-code>");
        Console.Error.WriteLine($"Known document codes: {string.Join(", ", knownDocuments.Keys)}");
        return 1;
    }

    var path = args[1];
    var documentCode = args[2].ToUpperInvariant();

    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"File not found: {path}");
        return 1;
    }

    if (!knownDocuments.TryGetValue(documentCode, out var known))
    {
        Console.Error.WriteLine($"Unknown document code \"{documentCode}\". Known codes: {string.Join(", ", knownDocuments.Keys)}");
        return 1;
    }

    var (documentTitle, titlePageNumber, tocPageRange) = known;

    Console.WriteLine("=== PokeJudge AI — Milestone 3 Document Ingestion ===\n");
    Console.WriteLine($"Extracting: {path}\n");

    var pageTexts = PdfTextExtractor.ExtractPageTexts(path);
    var bodyPageRange = (Start: tocPageRange.End + 1, End: pageTexts.Count);

    Console.WriteLine($"Extracted {pageTexts.Count} pages.\n");

    const int sampleBodyPage = 15;
    Console.WriteLine($"--- Raw extraction: page {sampleBodyPage} (before normalization) ---");
    Console.WriteLine(pageTexts[sampleBodyPage - 1]);
    Console.WriteLine();

    var normalizedSample = PageTextNormalizer.CollapseWhitespace(
        PageTextNormalizer.RejoinHyphenatedLineWraps(
            PageTextNormalizer.StripPageNumberFooter(pageTexts[sampleBodyPage - 1], sampleBodyPage)));

    Console.WriteLine($"--- Normalized: page {sampleBodyPage} (after normalization) ---");
    Console.WriteLine(normalizedSample);
    Console.WriteLine();

    var document = new IngestionPipeline().Run(
        pageTexts, titlePageNumber, tocPageRange, bodyPageRange, documentTitle, documentCode);

    Console.WriteLine($"--- Sectioned & cited: {document.Sections.Count} sections extracted ---");
    Console.WriteLine($"Document: {document.Metadata.Title} (revision: {document.Metadata.Version})\n");

    foreach (var section in document.Sections.Take(3))
    {
        Console.WriteLine($"[{section.SectionId}] {section.Heading}");
        Console.WriteLine(section.Text.Length > 300 ? section.Text[..300] + "..." : section.Text);
        Console.WriteLine();
    }

    var outputDirectory = Path.Combine(GetProjectDirectory(), "Ingestion", "Output");
    Directory.CreateDirectory(outputDirectory);
    var outputPath = Path.Combine(outputDirectory, $"{documentCode}.json");
    File.WriteAllText(outputPath, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine($"Wrote {document.Sections.Count} sections to {outputPath} (gitignored, not committed).");

    return 0;
}

// ---------------------------------------------------------------------------
// Milestone 4 chunking + embedding mode. Loads a document Milestone 3's
// `ingest` mode already produced, chunks and embeds it, and skips any chunk
// already present in a prior run's output (resumable -- see
// .project-plans/milestone-4/plan.md's free-tier rate-limit note).
// ---------------------------------------------------------------------------
static async Task<int> RunChunking(string[] args, string apiKey)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: dotnet run -- chunk <document-code>");
        return 1;
    }

    var documentCode = args[1].ToUpperInvariant();
    var ingestedPath = Path.Combine(GetProjectDirectory(), "Ingestion", "Output", $"{documentCode}.json");

    if (!File.Exists(ingestedPath))
    {
        Console.Error.WriteLine($"No ingested document found at {ingestedPath}. Run `ingest` first.");
        return 1;
    }

    var ingestedDocument = JsonSerializer.Deserialize<IngestedDocument>(File.ReadAllText(ingestedPath))
        ?? throw new InvalidOperationException("Ingested document deserialized to null.");

    var outputDirectory = Path.Combine(GetProjectDirectory(), "Chunking", "Output");
    Directory.CreateDirectory(outputDirectory);
    var outputPath = Path.Combine(outputDirectory, $"{documentCode}.chunks.json");

    var alreadyEmbedded = new Dictionary<string, float[]>();
    if (File.Exists(outputPath))
    {
        var existing = JsonSerializer.Deserialize<ChunkedDocument>(File.ReadAllText(outputPath));
        if (existing is not null)
        {
            foreach (var chunk in existing.Chunks)
            {
                alreadyEmbedded[chunk.Chunk.ChunkId] = chunk.Embedding;
            }
        }
    }

    Console.WriteLine("=== PokeJudge AI — Milestone 4 Chunking + Embeddings ===\n");
    Console.WriteLine($"Document: {ingestedDocument.Metadata.Title} ({ingestedDocument.Sections.Count} sections)");
    Console.WriteLine($"Already embedded: {alreadyEmbedded.Count} chunk(s)\n");

    IEmbeddingClient embeddingClient = CreateEmbeddingClient(apiKey);
    // batchSize kept well under the free tier's observed ~100-item-per-minute
    // embedding quota (a single 100-item batch was rejected outright) so at least
    // one batch reliably succeeds and gets saved before any 429.
    var pipeline = new ChunkingPipeline(embeddingClient, batchSize: 25);

    // Persist after every batch, not just at the end -- a rate-limit exception
    // partway through must not lose already-embedded chunks. Re-running this same
    // command afterward resumes from whatever was last saved here.
    void SaveProgress(List<EmbeddedChunk> chunksSoFar)
    {
        var snapshot = new ChunkedDocument(ingestedDocument.Metadata, chunksSoFar);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Progress: {chunksSoFar.Count} chunk(s) embedded, saved to {outputPath} (gitignored, not committed).");
    }

    List<EmbeddedChunk> embeddedChunks;
    try
    {
        embeddedChunks = await pipeline.RunAsync(ingestedDocument, alreadyEmbedded, onProgress: SaveProgress);
    }
    catch (HttpRequestException) when (File.Exists(outputPath))
    {
        Console.Error.WriteLine(
            "Embedding call failed (see exception below) -- progress up to the last completed batch " +
            $"was already saved to {outputPath}. Re-run this same command to resume.");
        throw;
    }

    var sampleSection = ingestedDocument.Sections[0];
    Console.WriteLine($"--- Sample section: [{sampleSection.SectionId}] {sampleSection.Heading} (full text, {sampleSection.Text.Length} chars) ---");
    Console.WriteLine(sampleSection.Text);
    Console.WriteLine();

    var sampleChunks = embeddedChunks.Where(c => c.Chunk.SectionId == sampleSection.SectionId).ToList();
    Console.WriteLine($"--- Split into {sampleChunks.Count} chunk(s) ---");
    foreach (var chunk in sampleChunks)
    {
        Console.WriteLine($"[{chunk.Chunk.ChunkId}] ({chunk.Chunk.Text.Length} chars, {chunk.Embedding.Length}-dim vector)");
        Console.WriteLine(chunk.Chunk.Text);
        Console.WriteLine();
    }

    SaveProgress(embeddedChunks);

    return 0;
}

// ---------------------------------------------------------------------------
// Milestone 5 vector search mode. Loads every already-chunked/embedded
// document into an in-process vector store -- brute-force cosine similarity,
// no vector database (see .project-plans/milestone-5/plan.md for why that's
// the right choice at this scale).
// ---------------------------------------------------------------------------
static async Task<int> RunSearch(string[] args, string apiKey)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: dotnet run -- search <query text>");
        return 1;
    }

    var query = string.Join(" ", args.Skip(1));
    var chunks = LoadAllEmbeddedChunks();

    if (chunks.Count == 0)
    {
        Console.Error.WriteLine("No chunked/embedded documents found. Run `ingest` and `chunk` first.");
        return 1;
    }

    IEmbeddingClient embeddingClient = CreateEmbeddingClient(apiKey);
    var queryVectors = await embeddingClient.EmbedBatchAsync(new[] { query });
    var store = CreateVectorStore(chunks);
    var results = store.Search(queryVectors[0], topK: 5);

    Console.WriteLine("=== PokeJudge AI — Milestone 5 Vector Search ===\n");
    Console.WriteLine($"Query: {query}");
    Console.WriteLine($"Searched {chunks.Count} chunks across all embedded documents.\n");

    foreach (var result in results)
    {
        Console.WriteLine($"[{result.Score:F4}] {result.Chunk.Chunk.ChunkId}");
        Console.WriteLine(result.Chunk.Chunk.Text.Length > 200 ? result.Chunk.Chunk.Text[..200] + "..." : result.Chunk.Chunk.Text);
        Console.WriteLine();
    }

    return 0;
}

// Runs the hand-authored retrieval evaluation set (RetrievalEvalSet) against
// the same in-process vector store -- deterministic hit/miss checking with
// zero chat/completion model calls, demonstrating retrieval quality is
// measurable independent of generation quality.
static async Task<int> RunRetrievalEval(string apiKey)
{
    var chunks = LoadAllEmbeddedChunks();
    if (chunks.Count == 0)
    {
        Console.Error.WriteLine("No chunked/embedded documents found. Run `ingest` and `chunk` first.");
        return 1;
    }

    IEmbeddingClient embeddingClient = CreateEmbeddingClient(apiKey);
    var store = CreateVectorStore(chunks);

    var queries = RetrievalEvalSet.Cases.Select(c => c.Query).ToList();
    var queryVectors = await embeddingClient.EmbedBatchAsync(queries);

    Console.WriteLine("=== PokeJudge AI — Milestone 5 Retrieval Evaluation ===\n");
    Console.WriteLine($"Searching across {chunks.Count} chunks. {RetrievalEvalSet.Cases.Count} eval case(s).\n");

    var hits = 0;
    for (var i = 0; i < RetrievalEvalSet.Cases.Count; i++)
    {
        var evalCase = RetrievalEvalSet.Cases[i];
        var results = store.Search(queryVectors[i], topK: 5);
        var evalResult = RetrievalEvaluator.Evaluate(evalCase, results);

        if (evalResult.Hit)
        {
            hits++;
        }

        Console.WriteLine(evalResult.Hit
            ? $"[HIT rank {evalResult.Rank}] \"{evalCase.Query}\" (expected {evalCase.ExpectedSectionId})"
            : $"[MISS] \"{evalCase.Query}\" (expected {evalCase.ExpectedSectionId})");

        foreach (var result in results.Take(3))
        {
            Console.WriteLine($"    [{result.Score:F4}] {result.Chunk.Chunk.ChunkId}");
        }
        Console.WriteLine();
    }

    Console.WriteLine($"Result: {hits}/{RetrievalEvalSet.Cases.Count} hit within top 5.");

    return 0;
}

static List<EmbeddedChunk> LoadAllEmbeddedChunks()
{
    var outputDirectory = Path.Combine(GetProjectDirectory(), "Chunking", "Output");
    var chunks = new List<EmbeddedChunk>();

    if (!Directory.Exists(outputDirectory))
    {
        return chunks;
    }

    foreach (var file in Directory.GetFiles(outputDirectory, "*.chunks.json"))
    {
        var document = JsonSerializer.Deserialize<ChunkedDocument>(File.ReadAllText(file));
        if (document is not null)
        {
            chunks.AddRange(document.Chunks);
        }
    }

    return chunks;
}

// InMemoryVectorStore's constructor fails loudly (by design) if any chunk has
// a corrupted/invalid embedding, naming the chunk. This wraps that with a
// friendly console message before re-throwing, matching the guidance pattern
// already used for chunk's rate-limit handling -- the full exception still
// surfaces for deeper debugging, it's just not the *only* thing shown.
static InMemoryVectorStore CreateVectorStore(List<EmbeddedChunk> chunks)
{
    try
    {
        return new InMemoryVectorStore(chunks);
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine($"Could not build the vector store: {ex.Message}");
        throw;
    }
}

// Single source of truth for the embedding model/dimensionality used across
// every embedding call site (RunChunking, RunSearch, RunRetrievalEval) -- keeping
// this in one place means chunk-time and query-time embeddings can't silently
// drift into incompatible vector spaces if the model or dimensionality changes
// at one call site but not the others.
static IEmbeddingClient CreateEmbeddingClient(string apiKey) =>
    new GeminiEmbeddingClient(apiKey, "gemini-embedding-001", outputDimensionality: 768);

// Resolves to this .cs file's own directory (PokeJudge/) at compile time, so
// output paths (ingestion, chunking) are anchored to the project regardless of
// the process's working directory -- `dotnet run` from inside PokeJudge/ vs.
// `dotnet run --project PokeJudge` from the repo root previously produced different,
// inconsistent output locations, one of which wasn't covered by .gitignore.
static string GetProjectDirectory([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "") =>
    Path.GetDirectoryName(sourceFilePath)!;

// ---------------------------------------------------------------------------
// Milestone 1 artifact, intentionally preserved: naive substring/regex
// checks against free text, locked in by PokeJudge.Tests/NaiveSufficiencyParserTests.cs
// as evidence for why Milestone 2 introduces schema-constrained structured
// output instead. Not called from the flow above — do not "improve" it.
// ---------------------------------------------------------------------------
static class NaiveSufficiencyParser
{
    private static readonly Regex InsufficientPattern = new(
        @"\b(not enough|insufficient|more (details|information|info)|need(s)? (more|additional)|unclear|cannot determine|can't determine|further (details|information))\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SufficientPattern = new(
        @"\b(sufficient|enough information|enough details|clear enough|yes\b)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Parse(string rawResponse)
    {
        var isInsufficient = InsufficientPattern.IsMatch(rawResponse);
        var isSufficient = SufficientPattern.IsMatch(rawResponse);

        if (isInsufficient && isSufficient)
        {
            return "AMBIGUOUS (both patterns matched)";
        }

        if (isInsufficient)
        {
            return "INSUFFICIENT";
        }

        if (isSufficient)
        {
            return "SUFFICIENT";
        }

        return "UNKNOWN (neither pattern matched)";
    }
}
