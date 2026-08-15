# Milestone 2 Review

**Milestone reviewed:** Milestone 2 — Judge-Focused Prompting, Clarification, Structured Responses
**Plan:** `.project-plans/milestone-2/plan.md`
**Branch:** `milestone/2-judge-clarification-structured-responses`
**Build:** `dotnet build` — succeeded, 0 warnings, 0 errors.
**Tests:** `dotnet test` — 29/29 passed.

## ✅ Matches the Plan

- **Schemas match the plan's spec exactly.** `ClarificationResult(bool IsSufficient, List<ClarifyingQuestion> Questions, DraftRuling? Draft)`, `ClarifyingQuestion(string Question, string RelatedSnippetId)`, `DraftRuling(string RecommendedAction, List<string> SupportingSnippetIds)`, `FactExtractionResult(List<string> ConfirmedFacts, List<string> Hypotheses)` — field-for-field as specified (`PokeJudge/StructuredState/`).
- **Mock corpus** (`PokeJudge/Clarification/MockCorpus.cs`): 3 scenarios reusing Milestone 1's themes (missed prize, ability timing, 61-card deck), 4 ID-tagged snippets each, deliberately mixing material and irrelevant/partial snippets (e.g., A3's deck-check procedure has nothing to do with the missed-prize scenario). `MockCorpusTests` locks in the 2–4-snippet/unique-ID constraint from the plan.
- **System vs. user instruction separation** is real, not cosmetic: `SystemPrompts.Judge`/`SystemPrompts.FactExtraction` go through Gemini's `system_instruction` field; `PromptBuilder` supplies only per-turn scenario/state data as user content (`GeminiLlmClient.cs:32-36`).
- **Structured output is genuinely schema-constrained**, not string-parsed: `responseMimeType: application/json` + `responseSchema` on every call, deserialized via `StructuredResponseParser.Parse<T>` — no regex, no substring matching anywhere in the new code.
- **Confirmed/hypothesis separation is structurally enforced**, not just documented: `GameState` has `AddConfirmedFacts`/`AddHypotheses` as the only mutators, with no method that could move an item from one list to the other. PRD §8's "no material inference" requirement holds by construction.
- **Multi-turn loop matches the plan's described flow** step-for-step: assess → (if insufficient) ask + extract per question → re-assess → stop on sufficiency or turn cap (`ClarificationLoop.RunAsync`).
- **Milestone 1's `NaiveSufficiencyParser` and its tests were left untouched**, exactly as intended — preserved as the historical evidence trail, not deleted or "fixed."
- **Real end-to-end validation happened**, not just unit tests: a scripted harness drove `ClarificationLoop` + `GeminiLlmClient` against the live Gemini API across all 3 scenarios, confirming the hand-written JSON schemas are actually accepted and respected by the real API (`observed-limitations.md`).
- **Errors fail visibly**: a live 429 rate-limit response during the smoke test propagated as an unhandled `HttpRequestException` with the provider's full error body intact — nothing was swallowed or silently degraded, matching PRD §9.
- **No scope creep**: no retrieval/embeddings/vector store, no citations/Source Support classification, no ASP.NET Core API layer, no persistence layer. Still one console project.

## 🚨 Must Fix

None. No correctness bugs, security issues, or scope violations were found that should block calling this milestone complete.

## ⚠️ Consider Improving

- **`ClarificationLoop`'s provider abstraction change was a narrowing, not a pure extension.** `ILlmClient.CompleteAsync(string prompt)` (Milestone 1's free-text method) was removed entirely rather than kept alongside the new `CompleteStructuredAsync<T>`. The plan's wording was "**Extend** the provider abstraction... Add a system-instruction parameter... and a schema-constrained completion path," which reads more naturally as additive. The removal is well-reasoned (nothing calls the old method once the console flow moved to structured calls, and keeping dead surface area would itself be a code-quality issue) and is disclosed in `Program.cs`'s comments and the implementation summary — but it's worth being aware this was an implementation judgment call, not something the plan explicitly authorized. Not worth reverting; just worth knowing next time a plan says "extend."
- **`GameState` doesn't dedupe.** Observed directly in the real smoke-test transcripts: when the model re-asks a rewording of the same question across turns, near-identical "confirmed facts" pile up verbatim (e.g., four slightly-reworded variants of "the judge does not know whether X" in Scenario 3). This doesn't corrupt the confirmed/hypothesis boundary, but it does make the printed known-facts list noisy. Not required by the plan; a simple `Distinct()` on add would be a cheap improvement if this becomes annoying in practice.
- **`Program.cs`'s `askJudge` lambda has an unnecessary async round-trip**: `return await Task.FromResult(Console.ReadLine() ?? string.Empty);` — `Console.ReadLine()` is synchronous; this could just be `Task.FromResult(...)` without `async`/`await`. Cosmetic only.
- **The fact-extraction call is isolated per question** — `BuildFactExtractionPrompt` supplies only the one question and its answer, not the scenario, snippets, or the confirmed-facts/hypotheses accumulated so far. For the "no other Pokemon were in play" style examples in the system prompt this is enough, but a judge's answer that only makes sense in light of previously-confirmed context (e.g., "the same thing happened" referring to an earlier turn) could be classified without the context needed to interpret it correctly. Not a bug against the plan (which only specifies "split the answer into confirmed facts vs. hypotheses"), but worth knowing as a boundary of the current design.
- **`SystemPrompts.Judge` doesn't explicitly frame output as a recommendation to a judge retaining their own authority** (PRD §9/§10's "recommendations... not final, binding decisions" language). Reasonable to defer given Milestone 2's draft ruling is explicitly rough and the final grounded/cited ruling arrives at Milestones 6–7, but it would cost nothing to add now.

## 🧪 Learning Observations

- **The schema-constrained payoff over Milestone 1 is directly demonstrable, and was demonstrated**: Scenario 1's smoke-test run reached sufficiency in 2 turns with a clean, correctly-typed `DraftRuling` — zero parsing ambiguity, in direct contrast to Milestone 1's `baseline-run-output.md` showing "not sufficient" misclassified as `SUFFICIENT` 3/12 times. Comparing these two documents side by side is the single best artifact of what this milestone actually bought.
- **"Structured output constrains shape, not truthfulness" was observed, not just asserted.** Scenario 1's draft ruling cited both A1 and A2 as supporting snippets, but only A1's condition (discovery timing) had actually been confirmed through the clarification loop — A2's "is this a first offense" condition was never asked about. The schema guaranteed a well-formed `DraftRuling` object; it did not guarantee every cited snippet ID was actually earned. This is exactly the limitation Milestone 7's grounding validation exists to close — good, faithful reproduction of the intended gap.
- **A sharper-than-predicted limitation surfaced: "I don't know" isn't a terminal state for this design.** Scenarios 2 and 3 both hit the turn cap because the sufficiency call kept re-asking a reworded version of the same question after the judge said "I don't know" — the fact-extraction step correctly turned that into a confirmed fact about the *judge's epistemic state* ("the judge does not know whether...") without ever wrongly promoting it to a game-state fact, but that confirmed fact doesn't resolve the underlying policy condition, so the loop has no way to recognize "this is genuinely unknowable, stop asking." This is a good, concrete illustration of why sufficiency and fact-extraction being separate LLM judgments — not deterministic app logic — is itself a limitation to carry forward, and it's the kind of finding that should genuinely motivate how later milestones think about non-answers.
- **The turn cap worked as a real safety valve, not just a documented intention.** Both stalled scenarios stopped cleanly with `TurnCapExhausted` and no fabricated ruling — directly observable evidence for PRD §9's "must not issue a ruling when it has flagged material facts as missing," holding at the orchestration layer independent of how well the underlying LLM calls behaved.
- **Contradictory hypotheses in a single result were observed**: Scenario 3 produced both "the deck matched the decklist" and "the deck did not match the decklist" as hypotheses simultaneously — a concrete, first-hand example of "confirmed-vs-hypothesis is genuinely hard, even for the model," rather than a claim taken on faith.
- **One gap in the manual smoke test worth closing yourself**: the actual interactive `Console.ReadLine()` path in `Program.cs` has not been driven by a human typing into it in this session (the sandbox has no stdin; a scripted harness substituted for it, hitting the same `ClarificationLoop`/`GeminiLlmClient` production code, just not through the console's actual I/O wiring). That wiring is thin and low-risk, but running `dotnet run` yourself and typing real answers is the one thing this review can't vouch for that you can.

## 🎯 Learning Objective Check

1. **What AI concept was this milestone intended to teach?** System instructions vs. user input as a distinct channel; schema-constrained structured output vs. free-text parsing; representing multi-turn conversational state as application data; and the confirmed-fact-vs-hypothesis distinction as a transferable "what does an agent actually know vs. merely assume" skill.
2. **Does the implementation expose that concept clearly?** Yes. `SystemPrompts` and `PromptBuilder` are separate, inspectable, and directly traceable to what's sent over the wire; `StructuredResponseParser` is a small, readable counterpart to `NaiveSufficiencyParser` that makes the M1→M2 contrast legible by design; `GameState`'s two-list-no-promotion structure makes the confirmed/hypothesis rule a structural property of the code, not just a prompt instruction hoped to be followed.
3. **What should the developer be able to explain after completing this milestone?** Why a system instruction is architecturally different from a longer user prompt; how `responseSchema`/`responseMimeType` on the Gemini API turns free text into a typed deserialization target; why the app, not the model, has to re-supply all known facts on every call; and — using this milestone's own transcripts — a concrete example of a plausible-but-not-entailed judge statement that correctly stayed a hypothesis, plus a concrete example of the boundary getting genuinely fuzzy (the "judge doesn't know X" facts in Scenarios 2/3).
4. **Is any abstraction hiding something the developer should understand directly?** No. The `askJudge`/`onAssessment` delegates in `ClarificationLoop` exist solely to make the orchestration testable without a real console or network call — they don't obscure any AI-relevant behavior; the actual prompts, schemas, and model responses remain fully visible in `PromptBuilder`, the `*Schema` static fields, and the transcripts captured in `observed-limitations.md`.

## 📋 Plan Completion

| Step | Status |
|---|---|
| 1. Design the mock corpus | Complete |
| 2. Design the structured-output schemas as C# records | Complete |
| 3. Extend the provider abstraction (system instruction + schema-constrained path) | Complete (see note above: the old free-text method was removed rather than kept alongside the new one) |
| 4. Write the judge-focused system prompt | Complete |
| 5. Build the in-memory structured game-state | Complete |
| 6. Wire the multi-turn loop | Complete |
| 7. Manual smoke test across all 3 mock scenarios, including an ambiguous answer | Complete, via a scripted harness against the live API rather than literal interactive typing (sandbox has no stdin) — production code path exercised end-to-end; interactive console I/O itself not yet human-verified |
| 8. Document observed failures/limitations with concrete transcripts | Complete |

## Final Verdict

**Ready to Complete**

No correctness, security, or scope issues were found. The implementation matches the plan closely, structurally enforces the confirmed/hypothesis rule rather than just hoping the prompt is followed, and the milestone's intended limitations were not only preserved but demonstrated with real transcripts — including a limitation sharper than the plan predicted, which is exactly the kind of evidence this project's learning loop is designed to produce. The only open item — manually running the interactive console yourself — is a low-risk, non-blocking follow-up, not a defect.
