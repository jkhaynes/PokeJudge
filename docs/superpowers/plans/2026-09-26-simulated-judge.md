# Simulated Judge Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the eval's ordered scripted answers with an AI "simulated judge" that answers PokeJudge's clarifying questions from a per-scenario fact sheet, and apply the approved scenario corrections.

**Architecture:** A new `SimulatedJudge` makes one structured `ILlmClient` call per question. `ScenarioEvalRunner` calls it from the `askJudge` callback `ClarificationLoop` already exposes, and records each question/answer. `EvalScenario.ScriptedAnswers` becomes `FactSheet` + `MaxClarifyingRounds`; the scorer's "Answer budget" becomes "Question budget". PokeJudge's own pipeline is untouched.

**Tech Stack:** .NET 10, C#, xUnit, Gemini REST API via the existing `GeminiLlmClient`.

**Spec:** `docs/superpowers/specs/2026-09-26-simulated-judge-design.md`

## Global Constraints

- Work on branch `step-2-simulated-judge`. Build/test with `dotnet test` from the repo root (PowerShell).
- No change to PokeJudge's pipeline or prompts: `ClarificationLoop`, `RulingGenerator`, `GroundingValidator`, `SystemPrompts`, `PromptBuilder`, retrieval.
- The judge uses the same `ILlmClient` instance as PokeJudge in `Program.cs` (same model, same fixed seed, same pacing).
- "Not known" answers are reported, never scored.
- Test-first for every behavior change. Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Never print or read secret values (e.g. never run `dotnet user-secrets list`).

---

### Task 1: `SimulatedJudge`

**Files:**
- Create: `PokeJudge/Evaluation/SimulatedJudge.cs`
- Test: `PokeJudge.Tests/Evaluation/SimulatedJudgeTests.cs`

**Interfaces:**
- Consumes: `ILlmClient.CompleteStructuredAsync<T>(string systemInstruction, string userContent, JsonElement responseSchema)`; test double `PokeJudge.Tests.TestDoubles.StubLlmClient` (`Enqueue(object)`, `UserContents`).
- Produces: `public sealed record JudgeAnswer(string Answer, bool Known)`; `public sealed class SimulatedJudge(ILlmClient)` with `Task<JudgeAnswer> AnswerAsync(string factSheet, string question)`; `public const string NotKnown = "Not known."`.

- [ ] **Step 1: Write the failing tests**

```csharp
namespace PokeJudge.Tests.Evaluation;

using PokeJudge.Evaluation;
using PokeJudge.Tests.TestDoubles;

public class SimulatedJudgeTests
{
    [Fact]
    public async Task AnswerAsync_SendsFactSheetAndQuestionToTheModel()
    {
        var llm = new StubLlmClient();
        llm.Enqueue(new JudgeAnswer("It was noticed on turn 3.", true));
        var judge = new SimulatedJudge(llm);

        await judge.AnswerAsync("The error was noticed on turn 3.", "When was it noticed?");

        Assert.Contains("The error was noticed on turn 3.", llm.UserContents[0]);
        Assert.Contains("When was it noticed?", llm.UserContents[0]);
    }

    [Fact]
    public async Task AnswerAsync_Known_ReturnsTheModelsAnswer()
    {
        var llm = new StubLlmClient();
        llm.Enqueue(new JudgeAnswer("It was noticed on turn 3.", true));

        var answer = await new SimulatedJudge(llm).AnswerAsync("facts", "question?");

        Assert.True(answer.Known);
        Assert.Equal("It was noticed on turn 3.", answer.Answer);
    }

    [Fact]
    public async Task AnswerAsync_NotKnown_ReturnsTheStandardNotKnownAnswer()
    {
        var llm = new StubLlmClient();
        llm.Enqueue(new JudgeAnswer("I'm not sure, maybe turn 2?", false));

        var answer = await new SimulatedJudge(llm).AnswerAsync("facts", "question?");

        Assert.False(answer.Known);
        Assert.Equal(SimulatedJudge.NotKnown, answer.Answer);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter SimulatedJudgeTests`
Expected: build error, `SimulatedJudge` / `JudgeAnswer` not defined.

- [ ] **Step 3: Write the implementation**

```csharp
namespace PokeJudge.Evaluation;

using System.Text.Json;
using PokeJudge.AI;

public sealed record JudgeAnswer(string Answer, bool Known);

// Stands in for the human judge at the table during `evaluate`: answers each of
// PokeJudge's clarifying questions from the scenario's fact sheet, and only from it.
// Replaces the old ordered script, which handed out canned answers regardless of
// what was actually asked. Eval-only: the judge-facing flow still asks a real person.
public sealed class SimulatedJudge
{
    public const string NotKnown = "Not known.";

    private const string SystemPrompt =
        "You are a Pokémon TCG tournament judge standing at the table, answering questions " +
        "from an assistant about what happened. Answer ONLY from the fact sheet you are given. " +
        "Never invent, guess, or infer facts that are not stated in it. Answer briefly, in one " +
        "or two sentences. If the fact sheet does not answer the question, set known to false.";

    private static readonly JsonElement Schema = JsonDocument.Parse("""
        {
          "type": "OBJECT",
          "properties": {
            "answer": { "type": "STRING" },
            "known": { "type": "BOOLEAN" }
          },
          "required": ["answer", "known"]
        }
        """).RootElement;

    private readonly ILlmClient _llmClient;

    public SimulatedJudge(ILlmClient llmClient)
    {
        _llmClient = llmClient;
    }

    public async Task<JudgeAnswer> AnswerAsync(string factSheet, string question)
    {
        var userContent = $"Fact sheet:\n{factSheet}\n\nQuestion:\n{question}";
        var answer = await _llmClient.CompleteStructuredAsync<JudgeAnswer>(SystemPrompt, userContent, Schema);

        // A "not known" reply must not leak a guess into PokeJudge's facts.
        return answer.Known ? answer : answer with { Answer = NotKnown };
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter SimulatedJudgeTests`
Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add PokeJudge/Evaluation/SimulatedJudge.cs PokeJudge.Tests/Evaluation/SimulatedJudgeTests.cs
git commit -m "Add SimulatedJudge: answers eval questions from a fact sheet"
```

---

### Task 2: Switch the eval from scripted answers to the simulated judge

This task is one compile unit: removing `ScriptedAnswers` breaks the runner, scorer, dataset, `Program.cs` and their tests at once.

**Files:**
- Modify: `PokeJudge/Evaluation/EvalScenario.cs`
- Modify: `PokeJudge/Evaluation/ScenarioTrajectory.cs`
- Modify: `PokeJudge/Evaluation/ScenarioEvalRunner.cs`
- Modify: `PokeJudge/Evaluation/ScenarioEvalScorer.cs:65-70,169-174`
- Modify: `PokeJudge/Evaluation/EvalDataset.cs` (all 20 scenarios)
- Modify: `PokeJudge/Program.cs:633,660`
- Test: `PokeJudge.Tests/Evaluation/ScenarioEvalRunnerTests.cs`, `ScenarioEvalScorerTests.cs`, `EvalDatasetTests.cs`, `EvalScenarioSelectorTests.cs`

**Interfaces:**
- Consumes: `SimulatedJudge`, `JudgeAnswer`, `SimulatedJudge.NotKnown` (Task 1).
- Produces:
  - `EvalScenario(string Id, string Category, string InitialDescription, IReadOnlyList<string> ExpectedMaterialSectionIds, ExpectedTrajectoryOutcome ExpectedOutcome, string FactSheet, int MaxClarifyingRounds, IReadOnlyList<string> ExpectedMaterialSectionIdsAfterAnswer, IReadOnlySet<SourceSupport>? AcceptableFinalSourceSupport)`
  - `public sealed record JudgeExchange(string Question, string Answer, bool Known)` (in `ScenarioTrajectory.cs`)
  - `ScenarioTrajectory` positional parameter `bool AskedMoreQuestionsThanScripted` replaced by `IReadOnlyList<JudgeExchange> Exchanges`; computed `int ClarifyingRounds` and `int NotKnownCount`.
  - Factories: `Failed(scenario, turns, message, IReadOnlyList<JudgeExchange>? exchanges = null)`, `TurnCapExhausted(scenario, turns, turnsUsed, IReadOnlyList<JudgeExchange>? exchanges = null)`, `Completed(scenario, turns, turnsUsed, ruling, grounding, IReadOnlyList<JudgeExchange>? exchanges = null)`.
  - `ScenarioEvalRunner(ClarificationLoop loop, RulingGenerator rulingGenerator, GroundingValidator groundingValidator, SimulatedJudge judge)`.
  - Scorer criterion named `"Question budget"` (replaces `"Answer budget"`).

- [ ] **Step 1: Update `EvalScenario`**

In `PokeJudge/Evaluation/EvalScenario.cs`, change the `RequiresOneClarification` comment and the record:

```csharp
    // One or more clarifying rounds are expected before sufficiency, up to the
    // scenario's MaxClarifyingRounds; the simulated judge answers from FactSheet.
    RequiresOneClarification,
```

```csharp
public sealed record EvalScenario(
    string Id,
    string Category,
    string InitialDescription,
    IReadOnlyList<string> ExpectedMaterialSectionIds,
    ExpectedTrajectoryOutcome ExpectedOutcome,
    string FactSheet,
    int MaxClarifyingRounds,
    IReadOnlyList<string> ExpectedMaterialSectionIdsAfterAnswer,
    IReadOnlySet<SourceSupport>? AcceptableFinalSourceSupport);
```

- [ ] **Step 2: Update `ScenarioTrajectory`**

Replace the `ScenarioTrajectory` record and factories in `PokeJudge/Evaluation/ScenarioTrajectory.cs` (keep `TurnRecord` as is):

```csharp
// One clarifying question and the simulated judge's answer to it.
public sealed record JudgeExchange(string Question, string Answer, bool Known);

public sealed record ScenarioTrajectory(
    EvalScenario Scenario,
    IReadOnlyList<TurnRecord> Turns,
    bool ReachedSufficiency,
    int TurnsUsed,
    IReadOnlyList<JudgeExchange> Exchanges,
    bool ThrewExpectedFailure,
    string? FailureMessage,
    RulingResult? Ruling,
    GroundingResult? Grounding)
{
    // A round is a turn in which PokeJudge judged the facts insufficient and asked questions.
    public int ClarifyingRounds => Turns.Count(t => !t.IsSufficient && t.Questions.Count > 0);

    public int NotKnownCount => Exchanges.Count(e => !e.Known);

    public static ScenarioTrajectory Failed(
        EvalScenario scenario, IReadOnlyList<TurnRecord> turns, string message, IReadOnlyList<JudgeExchange>? exchanges = null) =>
        new(scenario, turns, ReachedSufficiency: false, TurnsUsed: turns.Count, exchanges ?? Array.Empty<JudgeExchange>(),
            ThrewExpectedFailure: true, FailureMessage: message, Ruling: null, Grounding: null);

    public static ScenarioTrajectory TurnCapExhausted(
        EvalScenario scenario, IReadOnlyList<TurnRecord> turns, int turnsUsed, IReadOnlyList<JudgeExchange>? exchanges = null) =>
        new(scenario, turns, ReachedSufficiency: false, turnsUsed, exchanges ?? Array.Empty<JudgeExchange>(),
            ThrewExpectedFailure: false, FailureMessage: null, Ruling: null, Grounding: null);

    public static ScenarioTrajectory Completed(
        EvalScenario scenario, IReadOnlyList<TurnRecord> turns, int turnsUsed,
        RulingResult ruling, GroundingResult grounding, IReadOnlyList<JudgeExchange>? exchanges = null) =>
        new(scenario, turns, ReachedSufficiency: true, turnsUsed, exchanges ?? Array.Empty<JudgeExchange>(),
            ThrewExpectedFailure: false, FailureMessage: null, ruling, grounding);
}
```

- [ ] **Step 3: Update the test fixtures to the new shapes (tests first)**

In `ScenarioEvalScorerTests.cs`, `ScenarioEvalRunnerTests.cs` and `EvalScenarioSelectorTests.cs`, every test `EvalScenario` replaces its `ScriptedAnswers: ...,` argument:
- helpers for `SufficientOnFirstTurn`, `ExpectedToFailLoudly` and `ExpectedUnresolvable` scenarios: `FactSheet: "Test facts.", MaxClarifyingRounds: 0,`
- `RequiresOneClarificationScenario()` helpers: `FactSheet: "The marker is Asleep, but it should be Confused.", MaxClarifyingRounds: 1,`
- `RequiresTwoClarificationsScenario()` (runner tests): `FactSheet: "Both Supporter cards resolved. Noticed three turns later.", MaxClarifyingRounds: 2,`

In `ScenarioEvalScorerTests.cs`, remove the `bool` argument from every factory call: `Completed(scenario, turns, N, true|false, SomeRuling(), ...)` becomes `Completed(scenario, turns, N, SomeRuling(), ...)`, and `TurnCapExhausted(scenario, turns, 4, true|false)` becomes `TurnCapExhausted(scenario, turns, 4)`. For the multi-line `Completed(` calls at lines ~134 and ~121, remove the same argument.

Replace the four `// --- Answer budget ---` tests (lines ~314-370) with:

```csharp
    // --- Question budget ---

    [Fact]
    public void Score_RequiresOneClarification_MoreRoundsThanAllowed_QuestionBudgetFails()
    {
        var scenario = RequiresOneClarificationScenario();
        var turns = new List<TurnRecord>
        {
            new(new[] { Chunk("A1") }, false, new List<ClarifyingQuestion> { new("Q?", "A1#0") }),
            new(new[] { Chunk("A1") }, false, new List<ClarifyingQuestion> { new("Q2?", "A1#0") }),
            new(new[] { Chunk("A1") }, true, new List<ClarifyingQuestion>()),
        };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 3, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.Contains(report.Criteria, c => c.Name == "Question budget" && c.Result == CriterionResult.Fail);
        Assert.False(report.AllPassed);
    }

    [Fact]
    public void Score_RequiresOneClarification_RoundsAtTheLimit_QuestionBudgetPasses()
    {
        var scenario = RequiresOneClarificationScenario();
        var turns = new List<TurnRecord>
        {
            new(new[] { Chunk("A1") }, false, new List<ClarifyingQuestion> { new("Q?", "A1#0"), new("Q1b?", "A1#0") }),
            new(new[] { Chunk("A1") }, true, new List<ClarifyingQuestion>()),
        };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 2, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.Contains(report.Criteria, c => c.Name == "Question budget" && c.Result == CriterionResult.Pass);
    }

    [Fact]
    public void Score_SufficientOnFirstTurn_QuestionBudgetCriterionOmitted()
    {
        var scenario = SufficientOnFirstTurnScenario(new[] { "A1" });
        var turns = new List<TurnRecord> { new(new[] { Chunk("A1") }, true, new List<ClarifyingQuestion>()) };
        var trajectory = ScenarioTrajectory.Completed(scenario, turns, 1, SomeRuling(), SomeGrounding(SourceSupport.Strong));

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.DoesNotContain(report.Criteria, c => c.Name == "Question budget");
    }

    [Fact]
    public void Score_ExpectedToFailLoudly_QuestionBudgetCriterionOmitted()
    {
        var trajectory = ScenarioTrajectory.Failed(ExpectedFailureScenario(), new List<TurnRecord>(), "Model reported insufficient with no questions.");

        var report = ScenarioEvalScorer.Score(trajectory);

        Assert.DoesNotContain(report.Criteria, c => c.Name == "Question budget");
    }
```

In `ScenarioEvalRunnerTests.cs`, give the runner a judge backed by its own stub, so judge answers never mix with the loop's queue. Replace `BuildRunner()`:

```csharp
    private static (ScenarioEvalRunner Runner, StubLlmClient Llm, StubRetriever Retriever, StubLlmClient JudgeLlm) BuildRunner()
    {
        var llm = new StubLlmClient();
        var judgeLlm = new StubLlmClient();
        var retriever = new StubRetriever();
        var loop = new ClarificationLoop(llm, retriever);
        var rulingGenerator = new RulingGenerator(llm);
        var groundingValidator = new GroundingValidator(llm);
        var judge = new SimulatedJudge(judgeLlm);
        return (new ScenarioEvalRunner(loop, rulingGenerator, groundingValidator, judge), llm, retriever, judgeLlm);
    }
```

Update every test's deconstruction to `var (runner, llm, retriever, judgeLlm) = BuildRunner();` (use `_` for `judgeLlm` where unused). Then:

- `RunAsync_RequiresOneClarification_UsesTheScriptedAnswerAndReachesSufficiency`: rename to `RunAsync_RequiresOneClarification_AnswersFromTheJudgeAndReachesSufficiency`; add `judgeLlm.Enqueue(new JudgeAnswer("The marker is Asleep.", true));` before running; replace `Assert.False(trajectory.AskedMoreQuestionsThanScripted);` with:

```csharp
        var exchange = Assert.Single(trajectory.Exchanges);
        Assert.Equal("What happened?", exchange.Question);
        Assert.Equal("The marker is Asleep.", exchange.Answer);
        Assert.Contains("The marker is Asleep.", llm.UserContents[1]);
```
  and delete the old `Assert.Contains("The marker is Asleep.", llm.UserContents[2]);` (the fact-extraction prompt is `UserContents[1]`: index 0 is the first assessment, 1 the extraction for the judge's answer).

- `RunAsync_RequiresTwoClarifications_...`: rename to `RunAsync_TwoRounds_RecordsBothExchangesInOrder`; enqueue `judgeLlm.Enqueue(new JudgeAnswer("Both Supporter cards resolved.", true));` and `judgeLlm.Enqueue(new JudgeAnswer("Noticed three turns later.", true));`; replace the `AskedMoreQuestionsThanScripted` and `UserContents` asserts with:

```csharp
        Assert.Equal(new[] { "Q1?", "Q2?" }, trajectory.Exchanges.Select(e => e.Question));
        Assert.Equal(2, trajectory.ClarifyingRounds);
        Assert.Contains("Both Supporter cards resolved.", llm.UserContents[1]);
        Assert.Contains("Noticed three turns later.", llm.UserContents[3]);
```

- Replace `RunAsync_LoopAsksMoreQuestionsThanScripted_RecordsItRatherThanCrashing` with:

```csharp
    [Fact]
    public async Task RunAsync_JudgeCannotAnswer_RecordsNotKnownAndPassesItToTheLoop()
    {
        var (runner, llm, retriever, judgeLlm) = BuildRunner();
        llm.Enqueue(new ClarificationResult(false, new List<ClarifyingQuestion> { new("Which turn?", "A1#0") }));
        llm.Enqueue(new FactExtractionResult(new List<string>(), new List<string>()));
        llm.Enqueue(new ClarificationResult(true, new List<ClarifyingQuestion>()));
        retriever.Enqueue(new[] { Chunk("A1") });
        retriever.Enqueue(new[] { Chunk("A1") });
        llm.Enqueue(new RulingResult("Rec.", "Expl.", new List<string>(), null, new List<string> { "A1#0" }, SourceSupport.Strong, "n/a"));
        llm.Enqueue(new GroundingAssessment(new List<CitationGroundingCheck> { new("A1#0", CitationSupportLevel.ExplicitSupport) }, false, "n/a"));
        judgeLlm.Enqueue(new JudgeAnswer("Maybe turn 2?", false));

        var trajectory = await runner.RunAsync(RequiresOneClarificationScenario());

        Assert.Equal(1, trajectory.NotKnownCount);
        Assert.Equal(SimulatedJudge.NotKnown, trajectory.Exchanges[0].Answer);
        Assert.Contains(SimulatedJudge.NotKnown, llm.UserContents[1]);
        Assert.DoesNotContain("Maybe turn 2?", llm.UserContents[1]);
    }
```

- `RunAsync_UnrelatedInvalidOperationException_...`: add `judgeLlm.Enqueue(new JudgeAnswer("An answer.", true));` so the missing `FactExtractionResult` is still what fails.
- `RunAsync_NeverReachesSufficiencyWithinTurnCap_...`: inside the loop add `judgeLlm.Enqueue(new JudgeAnswer("An answer.", true));`.

In `EvalDatasetTests.cs`, replace `Scenarios_RequiresOneClarification_AlwaysHasAtLeastOneScriptedAnswer` with:

```csharp
    [Fact]
    public void Scenarios_EveryScenarioHasAFactSheet()
    {
        Assert.All(EvalDataset.Scenarios, s => Assert.False(string.IsNullOrWhiteSpace(s.FactSheet), s.Id));
    }

    [Fact]
    public void Scenarios_RequiresOneClarification_AllowsAtLeastOneRound()
    {
        foreach (var scenario in EvalDataset.Scenarios.Where(
            s => s.ExpectedOutcome == ExpectedTrajectoryOutcome.RequiresOneClarification))
        {
            Assert.True(scenario.MaxClarifyingRounds >= 1, scenario.Id);
        }
    }
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test`
Expected: build errors in `ScenarioEvalRunner.cs`, `ScenarioEvalScorer.cs`, `EvalDataset.cs`, `Program.cs` (they still use `ScriptedAnswers` / `AskedMoreQuestionsThanScripted` / the old constructor).

- [ ] **Step 5: Update `ScenarioEvalRunner`**

Add a `SimulatedJudge _judge` field, set from a fourth constructor parameter `SimulatedJudge judge`. In `RunAsync`, replace `nextScriptedAnswerIndex` / `askedMoreQuestionsThanScripted` with an exchange list and the judge:

```csharp
        var turns = new List<TurnRecord>();
        var exchanges = new List<JudgeExchange>();
        IReadOnlyList<ScoredChunk>? lastRetrievedChunks = null;

        ClarificationOutcome outcome;
        try
        {
            outcome = await _loop.RunAsync(
                scenario.InitialDescription,
                askJudge: async question =>
                {
                    var reply = await _judge.AnswerAsync(scenario.FactSheet, question.Question);
                    exchanges.Add(new JudgeExchange(question.Question, reply.Answer, reply.Known));
                    return reply.Answer;
                },
                onAssessment: (result, chunks) =>
                {
                    lastRetrievedChunks = chunks;
                    turns.Add(new TurnRecord(chunks, result.IsSufficient, result.Questions));
                });
        }
```

Pass `exchanges` to all three factories: `ScenarioTrajectory.Failed(scenario, turns, ex.Message, exchanges)`, `ScenarioTrajectory.TurnCapExhausted(scenario, turns, outcome.TurnsUsed, exchanges)`, `ScenarioTrajectory.Completed(scenario, turns, outcome.TurnsUsed, ruling, grounding, exchanges)`. Update the class comment: "with a simulated judge instead of console input".

- [ ] **Step 6: Update the scorer**

In `ScenarioEvalScorer.Score`, change `criteria.Add(ScoreAnswerBudget(trajectory));` to `criteria.Add(ScoreQuestionBudget(scenario, trajectory));` and replace `ScoreAnswerBudget` with:

```csharp
    private static CriterionOutcome ScoreQuestionBudget(EvalScenario scenario, ScenarioTrajectory trajectory) =>
        trajectory.ClarifyingRounds <= scenario.MaxClarifyingRounds
            ? new CriterionOutcome("Question budget", CriterionResult.Pass,
                $"Asked questions in {trajectory.ClarifyingRounds} round(s), within the limit of {scenario.MaxClarifyingRounds}.")
            : new CriterionOutcome("Question budget", CriterionResult.Fail,
                $"Asked questions in {trajectory.ClarifyingRounds} round(s), over the limit of {scenario.MaxClarifyingRounds}.");
```

In `ScorePostAnswerRetrieval`, change the text "after the scripted answer" to "after the judge's answer" (both occurrences).

- [ ] **Step 7: Convert the dataset**

In `PokeJudge/Evaluation/EvalDataset.cs`, in each `new EvalScenario(...)`, replace the whole `ScriptedAnswers: ...,` argument (it may span several lines) with the two arguments below. Fact sheets are copied verbatim from the spec.

| Id | Replacement |
|---|---|
| notes | `FactSheet: "The competitor wants to keep hand-written notes about the current match during play.", MaxClarifyingRounds: 0,` |
| proxy-cards | `FactSheet: "The cards are home-printed copies of real cards, used in place of the originals in a sanctioned tournament.", MaxClarifyingRounds: 0,` |
| deck-not-shuffled | `FactSheet: "The opponent noticed while cutting the deck, before either player drew an opening hand. There is no sign it was deliberate.", MaxClarifyingRounds: 0,` (Task 3 changes the round limit) |
| spectator-badges | `FactSheet: "The event is a Regional Championship. The spectator is not playing.", MaxClarifyingRounds: 0,` |
| repeat-violations | `FactSheet: "The competitor has received penalties for the same kind of infraction earlier in this event.", MaxClarifyingRounds: 0,` |
| special-condition | `FactSheet: "The opponent's attack text says the Defending Pokémon is now Confused. The card was turned to show Asleep by mistake. No other effects applied.", MaxClarifyingRounds: 1,` |
| missed-prize | `FactSheet: "A League Challenge. The player Knocked Out the opponent's Pokémon two turns ago and did not take a Prize card. They noticed it now.", MaxClarifyingRounds: 0,` |
| drew-extra-card | `FactSheet: "The player drew one extra card during their draw step. No card effect caused it. It was noticed later the same turn. The extra card went into their hand and can't be told apart from the rest.", MaxClarifyingRounds: 1,` |
| weakness-not-applied | `FactSheet: "The Defending Pokémon was in the Active position when it took the damage. The attack's base damage was 60. The Defending Pokémon has a printed Weakness to that attack's type (×2), but only 60 damage was placed on it. No Abilities, Tools or other effects modified the damage.", MaxClarifyingRounds: 2,` |
| supporter-twice | `FactSheet: "The opponent played two copies of the Supporter card Judge in the same turn. Both fully resolved before anyone noticed, and several turns have passed. The cards drawn from the second Judge can't be identified.", MaxClarifyingRounds: 1,` |
| gx-attack-twice | `FactSheet: "The player already used a GX attack earlier in this game with a different Pokémon-GX.", MaxClarifyingRounds: 0,` |
| mulligan-not-taken | `FactSheet: "Neither player can recall for certain whether either had a Basic Pokémon in their opening hand. There is no way to verify it now.", MaxClarifyingRounds: 1,` |
| late-to-round | `FactSheet: "The competitor arrived exactly 7 minutes after the round officially started.", MaxClarifyingRounds: 1,` |
| deck-under-60 | `FactSheet: "Both the decklist and the physical deck contain only 58 cards, two short of the required 60.", MaxClarifyingRounds: 1,` |
| ace-spec-count | `FactSheet: "Both the decklist and the physical deck contain two different ACE SPEC cards, Prime Catcher and Master Ball, and they match each other. The judge found this while reviewing the decklist, before either player drew an opening hand.", MaxClarifyingRounds: 1,` |
| too-many-prizes | `FactSheet: "The Knocked Out Pokémon was an ordinary Pokémon, worth one Prize card. The player took two. The extra Prize card was set aside face down, separate from the hand, and can be returned.", MaxClarifyingRounds: 1,` |
| prize-issue-vague | `FactSheet: "A player took a Prize card after what they believed was a Knock Out, but the Defending Pokémon still had HP remaining. It was never Knocked Out.", MaxClarifyingRounds: 1,` |
| double-energy-attach | `FactSheet: "The player attached two Basic Energy cards from hand in the same turn. No card effect allowed the second attachment.", MaxClarifyingRounds: 1,` |
| discard-shuffle-deescalate | `FactSheet: "The competitor shuffled their discard pile into their deck without a card effect. The discard pile was small, the game hasn't progressed past the first few turns, and both competitors agree on exactly which cards were in it.", MaxClarifyingRounds: 1,` |
| spectator-conduct | `FactSheet: "The person is a spectator, not playing in any event. They were standing next to the match and talking loudly about the game state.", MaxClarifyingRounds: 0,` |

In the file's header comment, change "Section IDs and scripted answers are grounded in real runs" to "Section IDs are grounded in real runs", and add after the header paragraph:

```csharp
// Step 2 (2026-09-26): scripted answers were replaced by fact sheets that
// SimulatedJudge answers from (docs/superpowers/specs/2026-09-26-simulated-judge-design.md).
```

Leave the historical Milestone 8/8.5 notes below it unchanged.

- [ ] **Step 8: Wire the judge into `evaluate`**

In `PokeJudge/Program.cs` `RunScenarioEval`, after `IRetriever retriever = ...;` add:

```csharp
    // Same client as PokeJudge: same model, seed and pacing.
    var judge = new SimulatedJudge(llmClient);
```

Change `new ScenarioEvalRunner(loop, rulingGenerator, groundingValidator)` to `new ScenarioEvalRunner(loop, rulingGenerator, groundingValidator, judge)`, and delete the line `Console.WriteLine($"Asked more questions than scripted: {trajectory.AskedMoreQuestionsThanScripted}");` (Task 4 replaces it).

- [ ] **Step 9: Run all tests**

Run: `dotnet test`
Expected: all pass, 230 tests (226 before this plan, +3 from Task 1, +1 net from the dataset tests).

- [ ] **Step 10: Commit**

```bash
git add PokeJudge/Evaluation PokeJudge/Program.cs PokeJudge.Tests/Evaluation
git commit -m "Answer eval questions with the simulated judge instead of scripted answers"
```

---

### Task 3: Apply the approved scenario corrections

**Files:**
- Modify: `PokeJudge/Evaluation/EvalDataset.cs` (deck-not-shuffled, spectator-badges)
- Test: `PokeJudge.Tests/Evaluation/EvalDatasetTests.cs`

**Interfaces:**
- Consumes: `EvalScenario` shape from Task 2.
- Produces: nothing new.

- [ ] **Step 1: Write the failing tests**

Add to `EvalDatasetTests`:

```csharp
    private static EvalScenario ById(string id) => EvalDataset.Scenarios.Single(s => s.Id == id);

    [Fact]
    public void DeckNotShuffled_AllowsOneQuestionAboutWhenItWasNoticed()
    {
        var scenario = ById("deck-not-shuffled");

        Assert.Equal(ExpectedTrajectoryOutcome.RequiresOneClarification, scenario.ExpectedOutcome);
        Assert.Equal(1, scenario.MaxClarifyingRounds);
        Assert.Contains("TCGTH-6.2.2", scenario.ExpectedMaterialSectionIds);
        Assert.Empty(scenario.ExpectedMaterialSectionIdsAfterAnswer);
    }

    [Fact]
    public void SpectatorBadges_NamesTheEventLevelTheRuleUses()
    {
        Assert.Contains("Regional Championship", ById("spectator-badges").InitialDescription);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter EvalDatasetTests`
Expected: the two new tests fail.

- [ ] **Step 3: Update the two scenarios**

`deck-not-shuffled`: expected sections `new[] { "TCGTH-6.2", "TCGTH-6.2.2" }`, outcome `ExpectedTrajectoryOutcome.RequiresOneClarification`, `MaxClarifyingRounds: 1`, `ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>()`. Add above it:

```csharp
        // Step 2 (2026-09-26): changed from SufficientOnFirstTurn. TCGTH-6.2.2 only says
        // poor randomization "may carry a penalty", so when it was noticed is material.
```

`spectator-badges`: description `"Do spectators need to wear a badge at a Regional Championship?"`. Add above it:

```csharp
        // Step 2 (2026-09-26): reworded from "large tournaments" -- PPTRH-2.4 names
        // "Regional Championships ... and above", and "large" was ambiguous.
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add PokeJudge/Evaluation/EvalDataset.cs PokeJudge.Tests/Evaluation/EvalDatasetTests.cs
git commit -m "Correct deck-not-shuffled and spectator-badges expectations"
```

---

### Task 4: Show the judge's answers in `evaluate` output

**Files:**
- Modify: `PokeJudge/Program.cs` (`RunScenarioEval` printing, around the per-turn question loop and the final `Result:` line)

**Interfaces:**
- Consumes: `ScenarioTrajectory.Exchanges`, `NotKnownCount`, `SimulatedJudge.NotKnown`.
- Produces: console output only.

This is console formatting in `Program.cs`, which has no tests today; verify by running.

- [ ] **Step 1: Print each answer under its question**

Replace the per-turn question loop with:

```csharp
            // Exchanges are recorded in the order the questions were asked, turn by turn.
            var exchangeIndex = 0;
            for (var turnIndex = 0; turnIndex < trajectory.Turns.Count; turnIndex++)
            {
                foreach (var question in trajectory.Turns[turnIndex].Questions)
                {
                    Console.WriteLine($"  [Turn {turnIndex + 1} question — re: {question.RelatedChunkId}] {question.Question}");
                    if (exchangeIndex < trajectory.Exchanges.Count)
                    {
                        var exchange = trajectory.Exchanges[exchangeIndex++];
                        Console.WriteLine($"    Judge: {exchange.Answer}{(exchange.Known ? "" : " (not known)")}");
                    }
                }
            }

            if (trajectory.Exchanges.Count > 0)
            {
                Console.WriteLine($"  Not answerable from the fact sheet: {trajectory.NotKnownCount} of {trajectory.Exchanges.Count} question(s)");
            }
```

- [ ] **Step 2: Add totals**

Next to `var infrastructureFailureCount = 0;` add `var totalQuestions = 0;` and `var totalNotKnown = 0;`. Right after `var report = ScenarioEvalScorer.Score(trajectory);` add:

```csharp
            totalQuestions += trajectory.Exchanges.Count;
            totalNotKnown += trajectory.NotKnownCount;
```

After the final `Result:` `Console.WriteLine(...)`, add:

```csharp
    Console.WriteLine($"Questions the fact sheets couldn't answer: {totalNotKnown} of {totalQuestions} (reported, not scored).");
```

- [ ] **Step 3: Build and smoke-test on one scenario**

Run: `dotnet build` then `dotnet run --project PokeJudge -- evaluate --only special-condition`
Expected: the question line is followed by a `Judge:` line, a "Not answerable from the fact sheet" line, and the final totals line.

- [ ] **Step 4: Commit**

```bash
git add PokeJudge/Program.cs
git commit -m "Print the simulated judge's answers and not-known counts in evaluate"
```

---

### Task 5: Live validation run

**Files:**
- Create: `docs/superpowers/plans/2026-09-26-simulated-judge-results.md`

- [ ] **Step 1: Run the full eval**

Run (paced by the `Gemini:RequestsPerMinute` user secret; about 11 minutes):
`dotnet run --project PokeJudge -- evaluate > <scratchpad>/step2-full.txt`

- [ ] **Step 2: Label every failure**

For each failing scenario, read its questions, the judge's answers and failed criteria, and label it **PokeJudge was wrong** or **the test was wrong** (a wrong expectation, a fact-sheet gap that produced "not known" for a fair question, or a judge answer that contradicts its fact sheet).

- [ ] **Step 3: Record the results**

Write `docs/superpowers/plans/2026-09-26-simulated-judge-results.md` with: the overall score (baseline was 11/20), the not-known total, and one table row per failing scenario: id, failed criteria, label, one-line reason. List any "test was wrong" findings as follow-ups; do not fix them in this step.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/plans/2026-09-26-simulated-judge-results.md
git commit -m "Record Step 2 live eval results"
```

Success: no failures labeled "the test was wrong"; any that are found are reported to the user before the branch is finished.
