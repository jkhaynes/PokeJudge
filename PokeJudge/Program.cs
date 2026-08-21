using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using PokeJudge.AI;
using PokeJudge.Chunking;
using PokeJudge.Clarification;
using PokeJudge.Evaluation;
using PokeJudge.Grounding;
using PokeJudge.Ingestion;
using PokeJudge.Reliability;
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
// label (Strong/Partial/Insufficient).
//
// Milestone 7 — Citations and Grounding
//
// RulingGenerator's Source Support label is no longer trusted as-is. GroundingValidator
// runs after it -- deterministic checks (retrieval non-empty, citation existence, fact
// sufficiency) plus one more LLM call classifying whether each cited passage actually
// supports its claim -- and SourceSupportAssigner combines both into a validated label.
// The console prints both: the model's own unvalidated opinion and the validated
// signal, so any divergence is visible rather than buried. See
// .project-plans/milestone-7/plan.md and grounding-analysis.md.
//
// Milestone 8 — Evaluation
//
// Adds `dotnet run -- evaluate`: runs the real, full pipeline (the same one the
// default flow below runs) against every hand-authored scenario in
// Evaluation/EvalDataset.cs, with a scripted judge instead of console input.
// ScenarioEvalScorer compares each captured trajectory against the scenario's
// expected criteria -- retrieval quality, sufficiency timing, clarifying-question
// materiality, and final Source Support -- per PRD SS15's trajectory-evaluation
// framing: a correct final ruling reached the wrong way should be distinguishable
// from one reached correctly. No new AI mechanism here; this measures Milestones
// 1-7's existing pipeline. See .project-plans/milestone-8/plan.md.
//
// Milestone 9 — Confidence Calibration and Reliability
//
// Adds ConfidenceEstimator: a new, deliberately separate LLM call producing a
// self-reported predicted-correctness probability for each ruling -- a distinct
// signal from Source Support (PRD SS9: "Confidence describes belief; Source
// Support describes evidence"), never shown the grounding/Source Support result
// so the two signals stay independently produced. Wired into both the default
// console flow below (printed as an internal, unvalidated signal only) and
// ScenarioEvalRunner. Adds `dotnet run -- calibrate`: reuses the same
// evaluate pipeline and scenario selection, but compares captured confidence
// estimates against each run's actual scored outcome via CalibrationAnalysis
// (bucketing, Brier score, and Expected Calibration Error only when the sample
// size actually supports it) instead of evaluate's pass/fail criteria. See
// .project-plans/milestone-9/plan.md.
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

if (args.Length > 0 && args[0] == "evaluate")
{
    return await RunScenarioEval(args, apiKey, modelId);
}

if (args.Length > 0 && args[0] == "calibrate")
{
    return await RunCalibration(args, apiKey, modelId);
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
var groundingValidator = new GroundingValidator(llmClient);
var confidenceEstimator = new ConfidenceEstimator(llmClient);

Console.WriteLine("=== PokeJudge AI — Milestone 9 Confidence Calibration and Reliability ===\n");
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

// PRD SS11's Grounding Validation & Source Support Assignment step -- checks
// the ruling's own Source Support label against retrieval/citation/fact data,
// rather than trusting it as generated (Milestone 7).
var grounding = await groundingValidator.ValidateAsync(ruling, finalChunks, outcome.Sufficient);

// Milestone 9: a self-reported confidence signal, deliberately independent of the
// grounding result above (see ConfidenceEstimator/SystemPrompts.ConfidenceEstimation).
// Printed here labeled as internal/unvalidated for developer inspectability -- this
// console flow is a development tool, not the judge-facing product (Milestone 10
// builds that UI) -- and per PRD SS9, never displayed to judges until (if ever)
// this milestone's calibration analysis validates it.
var confidence = await confidenceEstimator.EstimateAsync(scenarioDescription, outcome.State, finalChunks, ruling);

Console.WriteLine($"\nRecommendation: {ruling.Recommendation}");
Console.WriteLine($"Model's own assessment (unvalidated): {ruling.SourceSupport} — {ruling.SourceSupportRationale}");
Console.WriteLine($"Validated Source Support: {grounding.ValidatedSourceSupport} — {grounding.ValidatedRationale}");
Console.WriteLine($"Self-reported confidence (internal, unvalidated, not for judge display): " +
    $"{confidence.PredictedCorrectnessProbability}% — {confidence.Rationale}");
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

Console.WriteLine("\nCitation grounding breakdown:");
if (grounding.Assessment.Citations.Count == 0)
{
    Console.WriteLine("  (no citations to assess)");
}
foreach (var citation in grounding.Assessment.Citations)
{
    Console.WriteLine($"  [{citation.ChunkId}] {citation.SupportLevel}");
}
if (grounding.Assessment.ConflictDetected)
{
    Console.WriteLine("  Conflict detected among cited passages.");
}
Console.WriteLine($"  Deterministic checks: retrieval non-empty={grounding.RetrievalNonEmpty}, " +
    $"all citations exist={grounding.AllCitationsExist}, facts were sufficient={grounding.FactsWereSufficient}");

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
    var knownDocuments = new Dictionary<string, (string Title, int TitlePageNumber, (int Start, int End) TocPageRange, bool NamedHeadings)>(StringComparer.OrdinalIgnoreCase)
    {
        ["PPTRH"] = ("Play! Pokemon Tournament Rules Handbook", 1, (2, 3), false),
        ["TCGTH"] = ("Pokemon TCG Tournament Handbook", 1, (2, 3), false),
        ["PPG"] = ("Play! Pokemon Penalty Guidelines", 1, (2, 3), false),
        // Different layout from the other three: single-page TOC (body starts page 3, not 4),
        // its own "LAST UPDATED: <Month> <Year>" title-page phrasing rather than
        // "LAST REVISION: <Month> <Day>, <Year>" (see DocumentMetadataParser), and unnumbered
        // headings ("Special Conditions", not "5.1 Procedural Error") -- routed through
        // IngestionPipeline.RunNamedHeadings instead of Run; see NamedHeadingSectionSplitter.
        ["TCGRULES"] = ("Pokemon Trading Card Game Rulebook", 1, (2, 2), true),
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

    var (documentTitle, titlePageNumber, tocPageRange, namedHeadings) = known;

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

    var pipeline = new IngestionPipeline();
    var document = namedHeadings
        ? pipeline.RunNamedHeadings(pageTexts, titlePageNumber, tocPageRange, bodyPageRange, documentTitle, documentCode)
        : pipeline.Run(pageTexts, titlePageNumber, tocPageRange, bodyPageRange, documentTitle, documentCode);

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

// ---------------------------------------------------------------------------
// Milestone 8 evaluation harness. Runs the real, full pipeline (retrieve -> assess
// -> clarify -> re-retrieve -> generate -> validate grounding) against every
// hand-authored scenario in EvalDataset, scoring each captured trajectory against
// its expected criteria -- the "simple run log" PRD SS15 asks for. Separate from
// `eval` (Milestone 5's retrieval-only check, unchanged): this is a materially more
// expensive check, real chat completions per scenario, not just embeddings.
//
// Supports `--from <scenario-id>` / `--only <scenario-id>` so a developer can resume
// past the free-tier chat-completion rate limit (15 req/min, tighter than Milestone
// 4's embedding-tier limit) without re-running already-passing scenarios -- this
// command is developer/CI-facing tooling, not the judge-facing product, which only
// ever makes one real request at a time.
//
// Milestone 8.5 adds `--repeat <n>` so run-to-run variability (observed directly:
// deck-not-shuffled produced three different outcomes across three identical live
// runs) is visible rather than hidden behind whichever single run happened to
// occur. Each repeat is its own independent trajectory/report -- never collapsed --
// with a per-scenario "N/repeat runs passed" line printed alongside the individual
// results. A rate-limit or other transport failure (HttpRequestException) is caught
// per run and reported as an infrastructure failure, excluded from the pass/fail
// totals, rather than crashing the whole command or silently counting as PokeJudge
// getting the scenario wrong.
// ---------------------------------------------------------------------------
static async Task<int> RunScenarioEval(string[] args, string apiKey, string modelId)
{
    var (scenarios, repeatCount, selectionError) = EvalScenarioSelector.Select(args.Skip(1).ToList(), EvalDataset.Scenarios);
    if (selectionError is not null)
    {
        Console.Error.WriteLine(selectionError);
        return 1;
    }

    var chunks = LoadAllEmbeddedChunks();
    if (chunks.Count == 0)
    {
        Console.Error.WriteLine("No chunked/embedded documents found. Run `ingest` and `chunk` first.");
        return 1;
    }

    ILlmClient llmClient = new GeminiLlmClient(apiKey, modelId);
    IEmbeddingClient embeddingClient = CreateEmbeddingClient(apiKey);
    var store = CreateVectorStore(chunks);
    IRetriever retriever = new VectorStoreRetriever(embeddingClient, store);

    Console.WriteLine("=== PokeJudge AI — Milestone 8 Scenario Evaluation ===\n");
    Console.WriteLine($"Searching across {chunks.Count} chunks. {scenarios!.Count} scenario(s), {repeatCount} run(s) each.\n");

    var categoryResults = new List<(string Category, bool Passed)>();
    var totalPassCount = 0;
    var totalRunCount = 0;
    var infrastructureFailureCount = 0;

    foreach (var scenario in scenarios)
    {
        var scenarioPassCount = 0;

        for (var run = 1; run <= repeatCount; run++)
        {
            // Fresh loop/generator/validator per run -- no mutable state should leak
            // between independent runs, scenario or repeat alike.
            var loop = new ClarificationLoop(llmClient, retriever);
            var rulingGenerator = new RulingGenerator(llmClient);
            var groundingValidator = new GroundingValidator(llmClient);
            var confidenceEstimator = new ConfidenceEstimator(llmClient);
            var runner = new ScenarioEvalRunner(loop, rulingGenerator, groundingValidator, confidenceEstimator);

            var runLabel = repeatCount > 1 ? $"[{scenario.Id}] {scenario.Category} (run {run}/{repeatCount})" : $"[{scenario.Id}] {scenario.Category}";

            ScenarioTrajectory trajectory;
            try
            {
                trajectory = await runner.RunAsync(scenario);
            }
            // TaskCanceledException added after Milestone 9's live calibration runs hit it
            // repeatedly (a raw HttpClient.Timeout connection stall, not a clean 429) --
            // previously uncaught, it crashed the whole command outright instead of being
            // reported as the same kind of infrastructure failure a 429 already is.
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                infrastructureFailureCount++;
                Console.WriteLine($"--- {runLabel} ---");
                Console.WriteLine($"  [INFRASTRUCTURE FAILURE -- not counted] {ex.Message}");
                Console.WriteLine();
                continue;
            }

            var report = ScenarioEvalScorer.Score(trajectory);

            var outcomeLabel = trajectory.ThrewExpectedFailure
                ? "failed loudly"
                : trajectory.ReachedSufficiency ? "reached sufficiency" : "turn cap exhausted";

            Console.WriteLine($"--- {runLabel} ---");
            Console.WriteLine(scenario.InitialDescription);
            Console.WriteLine($"Turns used: {trajectory.TurnsUsed} ({outcomeLabel})");
            Console.WriteLine($"Asked more questions than scripted: {trajectory.AskedMoreQuestionsThanScripted}");

            // Eval mode was otherwise silent about what the model actually asked --
            // only the scorer's pass/fail criteria were visible. Printing the real
            // question text (mirroring the interactive console flow, which already does
            // this) matters most when a scenario needed more clarification than
            // scripted: without seeing the real question, there's no way to tell
            // whether the scripted answers should have anticipated it.
            for (var turnIndex = 0; turnIndex < trajectory.Turns.Count; turnIndex++)
            {
                foreach (var question in trajectory.Turns[turnIndex].Questions)
                {
                    Console.WriteLine($"  [Turn {turnIndex + 1} question — re: {question.RelatedChunkId}] {question.Question}");
                }
            }

            foreach (var criterion in report.Criteria)
            {
                var marker = criterion.Result == CriterionResult.Pass ? "PASS" : "FAIL";
                Console.WriteLine($"  [{marker}] {criterion.Name}: {criterion.Detail}");
            }

            if (trajectory.Grounding is not null)
            {
                Console.WriteLine(
                    $"  Model's own assessment: {trajectory.Ruling!.SourceSupport} | Validated: {trajectory.Grounding.ValidatedSourceSupport}");
            }

            categoryResults.Add((scenario.Category, report.AllPassed));
            totalRunCount++;
            if (report.AllPassed)
            {
                scenarioPassCount++;
                totalPassCount++;
            }

            Console.WriteLine();
        }

        if (repeatCount > 1)
        {
            Console.WriteLine($"  [{scenario.Id}] {scenarioPassCount}/{repeatCount} runs fully passed.\n");
        }
    }

    if (categoryResults.Count > 0)
    {
        Console.WriteLine("--- By category ---");
        foreach (var outcome in CategorySummary.Summarize(categoryResults))
        {
            Console.WriteLine($"  {outcome.Category}: {outcome.Passed}/{outcome.Total} passed");
        }

        Console.WriteLine();
    }

    Console.WriteLine(repeatCount > 1
        ? $"Result: {totalPassCount}/{totalRunCount} scenario-runs fully passed all applicable criteria " +
          $"(across {scenarios.Count} scenario(s), {repeatCount} run(s) each)."
        : $"Result: {totalPassCount}/{totalRunCount} scenarios fully passed all applicable criteria.");

    if (infrastructureFailureCount > 0)
    {
        Console.WriteLine($"Infrastructure failures (not counted above): {infrastructureFailureCount}.");
    }

    return 0;
}

// ---------------------------------------------------------------------------
// Milestone 9's `calibrate` command. Reuses EvalScenarioSelector and the same
// ScenarioEvalRunner pipeline `evaluate` drives -- the only difference is what's
// done with the captured trajectory: instead of scoring it against the
// scenario's expected criteria, this pairs the run's self-reported confidence
// (trajectory.Confidence) with its actual, already-established correctness
// (ScenarioEvalScorer.Score(...).AllPassed -- ground truth this milestone
// consumes, not redefines) and feeds the pair into CalibrationAnalysis. A run
// that never produces a ruling/confidence (crashed, turn cap exhausted, or an
// ExpectedToFailLoudly/ExpectedUnresolvable scenario that's never supposed to
// reach one) is correctly excluded from the calibration set, not an error.
// ---------------------------------------------------------------------------
static async Task<int> RunCalibration(string[] args, string apiKey, string modelId)
{
    var (scenarios, repeatCount, selectionError) = EvalScenarioSelector.Select(args.Skip(1).ToList(), EvalDataset.Scenarios);
    if (selectionError is not null)
    {
        Console.Error.WriteLine(selectionError);
        return 1;
    }

    var chunks = LoadAllEmbeddedChunks();
    if (chunks.Count == 0)
    {
        Console.Error.WriteLine("No chunked/embedded documents found. Run `ingest` and `chunk` first.");
        return 1;
    }

    var countingClient = new CallCountingLlmClient(new GeminiLlmClient(apiKey, modelId));
    ILlmClient llmClient = countingClient;
    IEmbeddingClient embeddingClient = CreateEmbeddingClient(apiKey);
    var store = CreateVectorStore(chunks);
    IRetriever retriever = new VectorStoreRetriever(embeddingClient, store);

    Console.WriteLine("=== PokeJudge AI — Milestone 9 Confidence Calibration ===\n");
    Console.WriteLine($"Searching across {chunks.Count} chunks. {scenarios!.Count} scenario(s), {repeatCount} run(s) each.\n");

    var observations = new List<CalibrationObservation>();
    var infrastructureFailureCount = 0;

    // Self-paces against the free-tier's 15-request-per-minute quota on generateContent calls
    // (confirmed via GeminiLlmClient's endpoint -- retrieval's embedContent calls are billed
    // separately and aren't paced here) -- see .project-plans/milestone-9/calibration-analysis.md
    // SS7. Tracks real usage via countingClient rather than guessing a fixed delay per run, so
    // cheap SufficientOnFirstTurn runs aren't slowed down more than they need to be. Window length
    // is 62s, not 60, to stay safely past the ~54-60s retry delays actually observed live.
    const int perMinuteQuota = 15;
    const int windowSeconds = 62;
    var windowCallCount = 0;
    var windowStart = DateTime.UtcNow;

    foreach (var scenario in scenarios)
    {
        for (var run = 1; run <= repeatCount; run++)
        {
            var estimatedMaxCalls = scenario.ExpectedOutcome == ExpectedTrajectoryOutcome.SufficientOnFirstTurn ? 4 : 8;

            if (windowCallCount + estimatedMaxCalls > perMinuteQuota)
            {
                var elapsed = DateTime.UtcNow - windowStart;
                if (elapsed.TotalSeconds < windowSeconds)
                {
                    var waitSeconds = windowSeconds - elapsed.TotalSeconds;
                    Console.WriteLine(
                        $"  [pacing] Waiting {waitSeconds:F0}s to stay under the free-tier's {perMinuteQuota}-request-per-minute quota...");
                    await Task.Delay(TimeSpan.FromSeconds(waitSeconds));
                }

                windowCallCount = 0;
                windowStart = DateTime.UtcNow;
            }

            var loop = new ClarificationLoop(llmClient, retriever);
            var rulingGenerator = new RulingGenerator(llmClient);
            var groundingValidator = new GroundingValidator(llmClient);
            var confidenceEstimator = new ConfidenceEstimator(llmClient);
            var runner = new ScenarioEvalRunner(loop, rulingGenerator, groundingValidator, confidenceEstimator);

            var runLabel = repeatCount > 1 ? $"[{scenario.Id}] (run {run}/{repeatCount})" : $"[{scenario.Id}]";

            var callsBeforeRun = countingClient.CallCount;
            ScenarioTrajectory trajectory;
            try
            {
                trajectory = await runner.RunAsync(scenario);
            }
            // TaskCanceledException added after this milestone's live runs hit it repeatedly (a
            // raw HttpClient.Timeout connection stall, not a clean 429) -- previously uncaught,
            // it crashed the whole command outright instead of being reported as the same kind
            // of infrastructure failure a 429 already is. By the time this fires, ~100s (the
            // HttpClient timeout) has already elapsed, which is longer than the pacing window
            // itself -- the next iteration's pacing check will see that and correctly skip an
            // extra artificial wait.
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                windowCallCount += countingClient.CallCount - callsBeforeRun;
                infrastructureFailureCount++;
                Console.WriteLine($"--- {runLabel} ---");
                Console.WriteLine($"  [INFRASTRUCTURE FAILURE -- not counted] {ex.Message}");
                Console.WriteLine();
                continue;
            }

            windowCallCount += countingClient.CallCount - callsBeforeRun;

            Console.WriteLine($"--- {runLabel} ---");

            if (trajectory.Confidence is null)
            {
                Console.WriteLine("  No ruling/confidence produced this run -- excluded from the calibration set.");
                Console.WriteLine();
                continue;
            }

            var report = ScenarioEvalScorer.Score(trajectory);
            var actualCorrect = report.AllPassed;

            // trajectory.Grounding is always non-null alongside trajectory.Confidence (both are
            // only ever set together, on the Completed path -- see ScenarioTrajectory.Completed).
            var grounding = trajectory.Grounding!;
            var citations = grounding.Assessment.Citations;
            var explicitSupportCount = citations.Count(c => c.SupportLevel == CitationSupportLevel.ExplicitSupport);
            var interpretationCount = citations.Count(c => c.SupportLevel == CitationSupportLevel.Interpretation);
            var unsupportedCount = citations.Count(c => c.SupportLevel == CitationSupportLevel.Unsupported);

            observations.Add(new CalibrationObservation(
                scenario.Id, scenario.Category, trajectory.Confidence.PredictedCorrectnessProbability,
                actualCorrect, report.Criteria, grounding.ValidatedSourceSupport, grounding.AllCitationsExist,
                grounding.Assessment.ConflictDetected, explicitSupportCount, interpretationCount, unsupportedCount));

            Console.WriteLine($"  Predicted correctness probability: {trajectory.Confidence.PredictedCorrectnessProbability}%");
            Console.WriteLine($"  Actual outcome: {(actualCorrect ? "correct" : "incorrect")}");

            // Printed only for incorrect runs -- this is exactly the detail a human needs to
            // write plan.md step 6's "compare confidence against other reliability signals"
            // narrative (see calibration-analysis.md), without having to re-run evaluate
            // separately for every wrong prediction the way the drew-extra-card diagnosis did.
            if (!actualCorrect)
            {
                Console.WriteLine($"  Grounding: Source Support={grounding.ValidatedSourceSupport}, " +
                    $"citations={explicitSupportCount} explicit/{interpretationCount} interpretation/{unsupportedCount} unsupported, " +
                    $"all citations exist={grounding.AllCitationsExist}, conflict={grounding.Assessment.ConflictDetected}");
            }

            Console.WriteLine();
        }
    }

    if (observations.Count == 0)
    {
        Console.WriteLine("No usable observations were captured -- cannot run a calibration analysis.");
        if (infrastructureFailureCount > 0)
        {
            Console.WriteLine($"Infrastructure failures: {infrastructureFailureCount}.");
        }

        return 0;
    }

    PrintCalibrationReport("All scenarios", observations);

    var withoutKnownIssues = CalibrationAnalysis.ExcludeKnownIssues(observations);
    if (withoutKnownIssues.Count != observations.Count)
    {
        Console.WriteLine();
        if (withoutKnownIssues.Count == 0)
        {
            Console.WriteLine("--- Excluding known-issue scenarios (missed-prize, mulligan-not-taken) ---");
            Console.WriteLine("(0 observations remain after exclusion -- every captured observation this run came from a known-issue scenario.)");
        }
        else
        {
            PrintCalibrationReport(
                "Excluding known-issue scenarios (missed-prize, mulligan-not-taken -- see plan.md's addendum)",
                withoutKnownIssues);
        }
    }

    if (infrastructureFailureCount > 0)
    {
        Console.WriteLine($"\nInfrastructure failures (not counted above): {infrastructureFailureCount}.");
    }

    return 0;
}

static void PrintCalibrationReport(string label, IReadOnlyList<CalibrationObservation> observations)
{
    Console.WriteLine($"--- {label} ({observations.Count} observation(s)) ---");

    var brier = CalibrationAnalysis.BrierScore(observations);
    Console.WriteLine($"Brier score: {brier:F4} (0 = perfect, 1 = worst)");

    var coarseBuckets = CalibrationAnalysis.Bucket(observations, bucketCount: 3);
    Console.WriteLine("Coarse buckets (low/medium/high confidence):");
    foreach (var bucket in coarseBuckets)
    {
        Console.WriteLine($"  [{bucket.LowerBound}-{bucket.UpperBound}%] n={bucket.Count}, " +
            $"mean predicted={bucket.MeanPredictedProbability:F1}%, observed correct rate={bucket.ObservedCorrectRate:P0}");
    }

    var fineBuckets = CalibrationAnalysis.Bucket(observations, bucketCount: 10);
    if (CalibrationAnalysis.BucketsSupportFineGrainedEce(fineBuckets))
    {
        var ece = CalibrationAnalysis.ExpectedCalibrationError(fineBuckets);
        Console.WriteLine($"Expected Calibration Error (10 buckets): {ece:F4}");
    }
    else
    {
        Console.WriteLine("Expected Calibration Error not reported: insufficient per-bucket sample size " +
            "(need 30+ observations in every non-empty bucket) for a fine-grained (10-bucket) estimate to be " +
            "meaningful at this dataset's size -- see plan.md's sizing analysis.");
    }

    Console.WriteLine("By category:");
    foreach (var category in CalibrationAnalysis.SummarizeByCategory(observations))
    {
        Console.WriteLine($"  {category.Category}: n={category.Count}, " +
            $"mean predicted={category.MeanPredictedProbability:F1}%, observed correct rate={category.ObservedCorrectRate:P0}");
    }

    var criterionFailures = CalibrationAnalysis.SummarizeCriterionFailures(observations);
    if (criterionFailures.Count > 0)
    {
        var incorrectCount = observations.Count(o => !o.ActualCorrect);
        Console.WriteLine($"Criterion failures among the {incorrectCount} incorrect observation(s):");
        foreach (var failure in criterionFailures)
        {
            Console.WriteLine($"  {failure.CriterionName}: {failure.FailureCount}");
        }
    }
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
