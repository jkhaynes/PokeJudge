# Milestone 1 — First LLM Interaction — Review

Reviewed against `.project-plans/milestone-1/plan.md`, `docs/PRD.md` (§7-8, §11, §14, "Learning Objectives → Milestone 1"), and the current diff of `Program.cs` / `PokeJudge.csproj` on branch `milestone/1-first-llm-interaction`. Build re-run and passes clean (0 warnings, 0 errors).

*Updated after the original review: two items below have since been addressed — see "✅ Addressed Since This Review."*

## ✅ Matches the Plan

- Single console app, one file, one project — exactly the `Console App → LLM` shape from PRD §11. No API layer, no session store, no multi-provider abstraction beyond the one interface.
- `ILlmClient` is a genuinely minimal one-method interface with one implementation (`GeminiLlmClient`) — no factory, no config layer, no retry policy.
- Secrets loaded via `dotnet user-secrets` only; confirmed via `git diff` that no secret-bearing file is staged/tracked.
- Naive extraction exercise targets exactly the signal the plan specified ("is this sufficient to rule on, yes/no"), run 4× across 3 scenarios, with both the raw response and the naive verdict logged side-by-side — this is precisely what step 6 asked for.
- The provider swap (OpenAI → Gemini, forced by OpenAI's lack of a free tier) happened entirely inside `GeminiLlmClient` — `Program.cs`'s calling code and the naive-parsing logic never changed. That's a live, unplanned demonstration of the exact interface-value lesson the plan calls out in §5 ("cheap now, expensive to retrofit later") — better evidence than the plan anticipated.
- Expected limitations (no memory, no grounding, no structured guarantee, naive-parsing brittleness) are all present and were captured with concrete transcript evidence in `.project-plans/milestone-1/baseline-run-output.md`, including a quantified failure rate (7/12 correct despite all 12 responses meaning "insufficient" in substance).

## 🚨 Must Fix

None. No correctness, security, or scope-violation issues block completion.

## ⚠️ Consider Improving

None remaining. The item originally listed here has been fixed — see "✅ Addressed Since This Review."

## ✅ Addressed Since This Review

- **`Program.cs` (`GeminiLlmClient.CompleteAsync`, response parsing).** Was: `GetProperty("candidates")[0]` threw an opaque `JsonException`/`IndexOutOfRangeException` if Gemini returned zero candidates (e.g., a safety-blocked prompt returns `promptFeedback.blockReason` with no `candidates` array). This technically satisfied PRD §9's "fail visibly, don't silently degrade," but the resulting stack trace didn't say *why*. Fixed: now checks for a missing/empty `candidates` array before indexing and throws a clear `HttpRequestException` including the raw response body. `dotnet test` (5/5) confirms no regression.
- **`NaiveSufficiencyParser` test coverage.** ~~This is the one piece of genuinely deterministic logic in the milestone (pure regex over fixed text) and currently has zero test coverage.~~ Fixed: added a new `PokeJudge.Tests` xUnit project (referenced from `PokeJudge.slnx`) with `NaiveSufficiencyParserTests.cs` — 5 tests, including two that deliberately assert the parser's *current, known-incorrect* behavior (`"...is not sufficient..."` → misclassified as `SUFFICIENT`; the same text combined with an insufficiency phrase → `AMBIGUOUS`). These lock in the observed flaw as a permanent regression guard rather than leaving it to disappear if someone "fixes" the regex before Milestone 2 introduces structured output. Required adding `AssemblyInfo.cs` with `[assembly: InternalsVisibleTo("PokeJudge.Tests")]` (the parser is a top-level, implicitly-internal type) and an explicit `<Compile Remove="PokeJudge.Tests/**" />` in `PokeJudge.csproj` to fix a globbing collision (the root-level `.csproj`'s default `**/*.cs` include was also sweeping up the sibling test project's files). `dotnet test PokeJudge.slnx` → 5/5 passing.
- **Reflection log entry requirement.** Removed as a project-wide requirement rather than fulfilled — `docs/PRD.md` §14 and §17 no longer call for a per-milestone reflection log, so this is no longer applicable to any milestone, not just this one.

## 🧪 Learning Observations

- **The interface abstraction earned its keep under real pressure, not hypothetically.** OpenAI's quota wall, then two rounds of Gemini model-ID deprecation, then a dead-end detour into a new "Interactions API" that turned out to 404 — all absorbed inside `GeminiLlmClient` with zero changes to the calling code. This is a more convincing demonstration of PRD §12's provider-swap goal than a planned exercise could have manufactured.
- **The naive-parser failure is concrete and quantified**, not asserted: 4 of 12 runs misclassified a response as `SUFFICIENT`/`AMBIGUOUS` purely because the substring `"sufficient"` appears inside `"not sufficient"`. This is the textbook motivating case for Milestone 2's structured output — worth re-reading `NaiveSufficiencyParser`'s regexes and predicting failures before looking at the transcript.
- **Grounding failure was more vivid than the plan predicted.** The plan anticipated "potentially fabricating rule specifics." What actually happened was the model spontaneously answering the "61-card deck" scenario using *Magic: The Gathering* rules terminology (Infraction Procedure Guide, "REL") layered under a Pokémon-framed question — a stronger, more surprising illustration of "no grounding" than mere rule-detail hallucination. Worth remembering specifically when Milestone 6/7 introduce retrieval grounding.
- **No memory, confirmed structurally, not just observed**: `CompleteAsync` takes only a `prompt` string and nothing else — there's no way for prior-call context to leak in even if you wanted it to. That's a stronger guarantee than "we didn't happen to pass history this time."

## 🎯 Learning Objective Check

1. **AI concept intended**: the raw LLM request/response cycle (auth, prompt/completion, no scaffolding) and, deliberately, the failure of naive text parsing as the motivator for Milestone 2's structured output.
2. **Does the implementation expose it clearly?** Yes — the raw HTTP request/response is fully visible (no SDK hiding the JSON shape), and the naive-parser failures are logged directly next to the raw text that caused them, so the mismatch is inspectable line-by-line rather than asserted.
3. **What the developer should be able to explain after this milestone**: what a prompt/completion/token exchange looks like at the wire level; why `ILlmClient` was worth writing even with one implementation (now with a real story, not a hypothetical); concretely why substring/regex parsing of free text breaks (negation blindness); why none of this required an API project, DI container, or multiple providers yet.
4. **Any abstraction hiding something it shouldn't?** No. `ILlmClient` is one method; `GeminiLlmClient` makes the raw REST call directly with no SDK in between — if anything, this implementation is *more* transparent about the wire format than the originally-planned Microsoft.Extensions.AI approach would have been.

## 📋 Plan Completion

| Step | Status |
|---|---|
| 1. Secrets setup | Complete |
| 2. Provider interface | Complete |
| 3. Concrete implementation | Complete (provider changed OpenAI→Gemini during implementation, documented, no plan violation — PRD §18 explicitly deferred exact provider choice) |
| 4. Wire up console flow | Complete |
| 5. Manual smoke test | Complete (captured in `baseline-run-output.md`) |
| 6. Naive extraction exercise | Complete |
| 7. Document the failure | Complete (concrete examples with counts in baseline doc) |

## Final Verdict

**Ready to Complete**

The implementation itself is correct, appropriately minimal, stays fully in scope, and produces unusually strong evidence for exactly the concepts this milestone exists to teach. No code-level issues block completion; the remaining "Consider Improving" items are optional polish, not gating.
