# PR Review — Milestone 2

## PR Review Summary

- **Milestone:** Milestone 2 — Judge-Focused Prompting, Clarification, Structured Responses
- **Current branch:** `milestone/2-judge-clarification-structured-responses`
- **Base branch:** `master`
- **What changed:** No commits exist yet on this branch (`git rev-list --count master..HEAD` = 0) — all Milestone 2 work is currently uncommitted working-tree changes. The effective diff against `master` is: `PokeJudge/Program.cs` rewritten from Milestone 1's raw-call demo into the interactive clarification-loop entry point; new `PokeJudge/AI/` (`ILlmClient`, `GeminiLlmClient`, `StructuredResponseParser`, `LlmClientMarker`), `PokeJudge/StructuredState/` (`ClarificationResult` + schema, `FactExtractionResult` + schema, `GameState`), and `PokeJudge/Clarification/` (`MockCorpus`, `SystemPrompts`, `PromptBuilder`, `ClarificationOutcome`, `ClarificationLoop`); matching new test folders under `PokeJudge.Tests/`; and `.project-plans/milestone-2/` planning docs.
- **Overall impression:** Clean, well-scoped implementation that matches the approved plan closely. No correctness or security defects found. A few small, non-blocking design notes below.
- **Build/test status:** `dotnet build` — succeeded, 0 warnings, 0 errors. `dotnet test` — 31/31 passed (29 from the original implementation + 2 added below for the `maxTurns` lower-bound fix).

## 🚫 Blockers

None.

## ⚠️ Major Issues

None.

## 🔎 Minor Issues

- **`PokeJudge/StructuredState/GameState.cs`** — `AddConfirmedFacts`/`AddHypotheses` append unconditionally with no dedup. Confirmed via live smoke-test transcripts (`.project-plans/milestone-2/observed-limitations.md`): when the sufficiency call re-asks a reworded version of an earlier question, near-identical facts accumulate verbatim in the printed list. Doesn't affect the confirmed/hypothesis boundary's correctness, just makes the surfaced list noisy. **Left out intentionally, not an oversight:** per the milestone's learning-checkpoint discussion, the underlying cause is that the loop currently has no way to represent a genuinely unknowable fact as a distinct third state (only confirmed/hypothesis), so the sufficiency call keeps re-asking. A string-similarity dedup here would be a fragile heuristic papering over that gap rather than fixing it, and the real fix (a schema/state redesign) is a deliberate decision for a later milestone, not something to slip in now. A plain `Distinct()` on add would still be a cheap cosmetic improvement if the noisy list becomes annoying in practice, independent of that larger fix.
- ~~**`PokeJudge/Program.cs:55-60`** — the `askJudge` lambda does `return await Task.FromResult(Console.ReadLine() ?? string.Empty);`. `Console.ReadLine()` is synchronous, so this is an unnecessary `async`/`await` round-trip through `Task.FromResult`.~~ **Fixed.** The lambda is now a plain (non-`async`) `Func<ClarifyingQuestion, Task<string>>` returning `Task.FromResult(...)` directly.
- ~~**`PokeJudge/Clarification/ClarificationLoop.cs`** constructor does not validate `maxTurns > 0`. Passing `maxTurns: 0` silently returns `TurnCapExhausted` with zero LLM calls made, rather than failing fast on an obviously-wrong configuration.~~ **Fixed.** The constructor now throws `ArgumentOutOfRangeException` for `maxTurns <= 0`, covered by a new test-first case: `ClarificationLoopTests.Constructor_MaxTurnsIsNotPositive_ThrowsArgumentOutOfRangeException` (`[InlineData(0)]`, `[InlineData(-1)]`) — this is deterministic constructor-argument validation, exactly the kind of lower-bound behavior that should be locked in with a unit test rather than left to only be caught by inspection.

## 💬 Review Notes

- **`ILlmClient` lost `CompleteAsync(string prompt)` rather than gaining `CompleteStructuredAsync<T>` alongside it.** The plan's wording ("**Extend** the provider abstraction... Add a system-instruction parameter... and a schema-constrained completion path") reads slightly more additive than what was built. The removal is justified — nothing calls the free-text method once the console flow moved to structured calls, and dead interface surface would itself be a smell — and it's disclosed in `Program.cs`'s comments and `implementation-summary.md`. Flagging only so it's a conscious, remembered decision rather than a silent one next time a plan says "extend."
- **`PromptBuilder.BuildFactExtractionPrompt` receives only the question and the judge's answer** — not the scenario, snippets, or accumulated `GameState`. This matches the plan's literal description ("split the answer into confirmed facts vs. hypotheses") and the smoke test didn't surface any misclassification traceable to missing context, but it's a real boundary: a judge answer that only makes sense with prior-turn context (e.g., "same as before") could be extracted without enough information to classify correctly. Not a defect against this milestone's scope; worth remembering if extraction quality becomes an issue later.
- **`SystemPrompts.Judge` doesn't explicitly state the output is a recommendation, not a binding ruling** (PRD §9/§10 language). Reasonable to defer since Milestone 2's `DraftRuling` is explicitly rough and the polished, cited final ruling arrives at Milestones 6–7 — but cheap to add now if it ever gets pulled forward.

## 🤖 AI-Specific Review

- **System vs. user content separation is real, not just organizational.** `GeminiLlmClient.CompleteStructuredAsync` sends `system_instruction` and `contents` as genuinely separate fields on the Gemini request (`GeminiLlmClient.cs:32-36`), verified working against the live API in the smoke test. Judge-supplied free text only ever enters the `contents`/user side, never the system-instruction channel — the correct, minimum-viable posture for PRD §16's "treat user input as data, not instructions" requirement at this milestone's scope.
- **Structured output is genuinely schema-constrained**, not a euphemism for still-parsed text: `responseMimeType: application/json` + hand-written `responseSchema` per call, deserialized via `StructuredResponseParser.Parse<T>` with no regex or substring matching anywhere in the new code. This was empirically confirmed against the real API, not just asserted.
- **Hidden reliance on model behavior worth naming explicitly:** the entire "reason only over supplied text, not pretrained knowledge" requirement (PRD §7 FR3, §8) is enforced by exactly one sentence in `SystemPrompts.Judge` and nothing else — no code-level check exists (or could easily exist) to verify the model actually complied. A `DraftRuling` reasoned from real-world Pokémon judging convention would be indistinguishable, at the schema level, from one reasoned strictly from the supplied snippets. This is an accurate reflection of where Milestone 2 is supposed to stop (Milestone 7 formalizes grounding checks) — not a defect — but it is the one place this implementation is most exposed to "appearing to work while relying on behavior we did not intend," and it's worth keeping in mind before this pattern is trusted at a larger scope.
- **Confirmed/hypothesis separation is structurally enforced, not just prompted.** `GameState` exposes only `AddConfirmedFacts`/`AddHypotheses` with no method that could promote a hypothesis to confirmed — PRD §8's "no material inference" requirement holds by construction in the app layer, independent of whether the model's own classification judgment is always correct (and the transcripts show it sometimes isn't — e.g., contradictory hypotheses coexisting in one result).
- **State re-supply on every turn is correct and complete.** `PromptBuilder.BuildSufficiencyPrompt` serializes the full `ConfirmedFacts`/`Hypotheses` lists into every sufficiency call; nothing relies on the model retaining anything between requests.

## 🧪 Test Review

- **Coverage of deterministic logic is thorough and appropriately scoped:** `StructuredResponseParserTests` (valid/malformed JSON, camelCase↔PascalCase matching, fail-loud-on-malformed-input), `GameStateTests` (confirmed/hypothesis separation and accumulation), `MockCorpusTests` (corpus shape constraints from the plan), `PromptBuilderTests` (prompt assembly), and `ClarificationLoopTests` (stop-on-sufficient, turn-cap exhaustion, multi-question turns, cross-turn fact propagation via a scripted `StubLlmClient`, and two "fail loudly on a malformed model contract" cases — sufficient-without-draft, insufficient-without-questions).
- **Tests exercise real behavior, not just implementation shape:** e.g., `RunAsync_InsufficientThenSufficient_AccumulatesFactsAndHypothesesAcrossTurns` asserts the turn-2 prompt actually contains the fact confirmed in turn 1 (`stub.UserContents[2]`), which would genuinely fail if `PromptBuilder`/`ClarificationLoop` stopped re-supplying state — this is a meaningful regression guard, not a tautology.
- **Correctly avoids unit-testing non-deterministic behavior.** `GeminiLlmClient` (network-dependent) and `Program.cs`'s console I/O are untested by design, matching Milestone 1's precedent; probabilistic model behavior is instead covered by the manual smoke test.
- **Manual AI validation was appropriate for this milestone and was actually performed against the live API**, not just described: `.project-plans/milestone-2/observed-limitations.md` documents real transcripts across all 3 mock scenarios, including a genuinely useful sharper-than-predicted failure mode (repeated re-asking after "I don't know" answers). One caveat carried over from the milestone review: the literal interactive `Console.ReadLine()` path in `Program.cs` was not driven by a human typing into it this session (sandbox has no stdin) — a scripted harness exercised the same production code path instead. Low risk, but worth a real interactive run before considering the console UX itself fully verified.
- **Build/test results:** `dotnet build` clean (0 warnings/errors); `dotnet test` 31/31 passed.

## 📦 Scope Check

- **Does this branch correspond to the current milestone?** Yes — branch name, file layout (`AI/`, `StructuredState/`, `Clarification/` matching the PRD's own §11 illustrative Milestone 2 structure), and content all match `plan.md`.
- **Does the diff contain only work appropriate to this milestone?** Yes.
- **Did unrelated changes get mixed into the branch?** No — `git status` shows exactly the expected new/modified files plus `.project-plans/milestone-2/` docs; nothing else.
- **Did it implement anything from future milestones?** No — no retrieval/embeddings/vector store, no citations/Source Support classification, no grounding validation, no ASP.NET Core API layer, no persistence.
- **Did it introduce unnecessary architecture or dependencies?** No new NuGet packages were added. No new projects were created; still a single console project with internal folder organization, consistent with the PRD's modular-monolith direction.

## Final Verdict

**Approve With Minor Comments**

No blockers or major issues. The implementation is correct, secure (no secrets in source, appropriate system/user separation), stays tightly within the approved scope, and its deterministic logic is well-tested. Of the three original minor items, two are now fixed (the cosmetic async no-op in `Program.cs`, and `ClarificationLoop`'s unvalidated `maxTurns` — now guarded and covered by a dedicated lower-bound unit test) and the third (`GameState` dedup) was confirmed as an intentional, documented deferral rather than an oversight. One process note: this branch has no commits yet — everything reviewed here is uncommitted working-tree state, so committing (and then opening the actual PR) is still an outstanding step before this can be merged.
