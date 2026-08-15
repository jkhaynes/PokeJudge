# Milestone 2 — Implementation Summary

**Milestone:** Milestone 2 — Judge-Focused Prompting, Clarification, Structured Responses
**Branch:** `milestone/2-judge-clarification-structured-responses`

## What Changed

Grew the console app from Milestone 1's single raw LLM call into a multi-turn clarification loop,
still one project, still in-memory:

- **`PokeJudge/AI/`** — `ILlmClient` (now a single `CompleteStructuredAsync<T>(systemInstruction,
  userContent, responseSchema)` method; Milestone 1's free-text `CompleteAsync` was retired since
  nothing in the new flow calls it), `GeminiLlmClient` (adds `system_instruction` and
  `generationConfig.responseSchema`/`responseMimeType: application/json` to the Milestone 1
  `generateContent` call), `StructuredResponseParser` (deserializes the model's JSON text into the
  requested type, case-insensitively, throwing rather than guessing on malformed input).
- **`PokeJudge/StructuredState/`** — `ClarificationResult` / `ClarifyingQuestion` / `DraftRuling`
  records plus their hand-written Gemini schema, `FactExtractionResult` plus its schema, and
  `GameState` (app-owned confirmed-facts / hypotheses lists with no promotion path between them).
- **`PokeJudge/Clarification/`** — `MockCorpus` (3 hand-authored scenarios reusing Milestone 1's
  missed-prize / ability-timing / 61-card-deck themes, each with 4 ID-tagged snippets, some
  material, some deliberately irrelevant), `SystemPrompts` (judge persona + fact-extraction
  persona), `PromptBuilder` (pure functions assembling the per-turn context), `ClarificationOutcome`,
  and `ClarificationLoop` (the multi-turn orchestration: assess → ask → extract → re-assess → stop
  on sufficiency or a turn cap).
- **`PokeJudge/Program.cs`** — rewritten as the interactive scenario-select + clarification loop.
  Milestone 1's `NaiveSufficiencyParser` class and its locked-in test file were left untouched as
  the historical evidence trail for why structured output was needed.

## Validation

- **Build:** `dotnet build` — succeeded, 0 errors.
- **Tests:** `dotnet test` — 29/29 passed.
  - **Written first (Red step), all deterministic:** `StructuredResponseParserTests` (valid/malformed
    JSON, camelCase↔PascalCase matching), `GameStateTests` (confirmed/hypothesis separation and
    accumulation), `MockCorpusTests` (3 scenarios, 2–4 uniquely-ID'd snippets each), `PromptBuilderTests`
    (prompt assembly), `ClarificationLoopTests` (stop-on-sufficient, turn-cap exhaustion, multi-question
    turns, fact accumulation carried into the next turn's prompt, and two "fail loudly" contract-violation
    cases — sufficient-without-draft and insufficient-without-questions — via a scripted `StubLlmClient`).
  - **Final coverage-review pass:** reviewed the full diff after implementation; no additional
    deterministic logic emerged beyond what the Red step already covered (`GeminiLlmClient` and
    `Program.cs`'s console I/O remain intentionally untested, matching Milestone 1's precedent —
    network-dependent and I/O, not deterministic).
  - One test bug was caught and fixed during the Green step: an assertion in
    `ClarificationLoopTests` checked the wrong element of the stub's recorded call list (it landed
    on the fact-extraction call instead of the second sufficiency call); fixed by indexing the
    correct position, not by loosening the assertion.
- **Manual smoke test:** this sandbox has no interactive stdin, so `Program.cs`'s real
  `Console.ReadLine()` prompts couldn't be driven directly. Ran a scratch harness (outside the repo)
  that calls the real `ClarificationLoop` + live Gemini API with pre-scripted judge answers across
  all 3 mock scenarios, including a deliberately ambiguous answer. Full transcripts and analysis are
  in `.project-plans/milestone-2/observed-limitations.md`. **The interactive console flow itself has
  not been manually driven — see "What I Should Try" below.**

## Intentional Limitations

- **Structured output constrains shape, not truthfulness.** Observed directly: Scenario 1's draft
  ruling cited A1 and A2 even though only A1's condition had actually been confirmed. Untouched
  until Milestone 7's grounding validation.
- **The mock corpus only knows what was hand-written into it.** Not directly exercised this run
  (all scripted scenarios used the corpus's own text); remains the explicit motivation for
  Milestone 3.
- **Confirmed-vs-hypothesis is genuinely hard, even for the model.** Observed directly: Scenario 3
  produced two directly contradictory hypotheses in the same result ("deck matched" and "deck did
  not match" the decklist, simultaneously).
- **Materiality is bounded by the fixed mock corpus.** Observed as a sharper failure mode than
  anticipated: when the judge's honest answer is "I don't know" / "unknowable," the loop has no way
  to treat that as a terminal state — it re-asks a reworded version of the same question every turn
  until the turn cap stops it (Scenarios 2 and 3, both turn-cap-exhausted). The turn cap itself
  worked correctly as a safety valve — no ruling was ever fabricated.

## Learning Focus

- **System instructions vs. user input** as a genuinely separate channel: `SystemPrompts.Judge` and
  `SystemPrompts.FactExtraction` carry persona/hard constraints once, while `PromptBuilder` supplies
  only the per-turn scenario/state data — not a longer concatenated string.
- **Schema-constrained structured output** closing the exact gap Milestone 1's `NaiveSufficiencyParser`
  exposed: `StructuredResponseParser` either deserializes cleanly into a typed record or throws — no
  regex ambiguity, no silent misclassification of "sufficient" vs. "not sufficient."
- **Multi-turn state as application data**: `GameState` is the only thing that remembers anything
  between calls. Every `AssessAsync` call re-sends the full accumulated confirmed-facts and
  hypotheses lists via `PromptBuilder`, because the model itself retains nothing.
- **Confirmed vs. hypothesis, concretely**: the `observed-limitations.md` transcripts show the model
  correctly keeping "the judge doesn't know X" out of confirmed *game-state* facts in spirit, while
  also showing where the boundary gets genuinely fuzzy (epistemic facts about the judge vs. facts
  about the game).
- **Why materiality must stay text-derived**: every clarifying question in the transcripts ties back
  to a specific snippet ID (`RelatedSnippetId`), traceable to that snippet's actual wording — not to
  general Pokémon judging convention.

## What I Should Try

1. **Run the real console app interactively** (`dotnet run` from `PokeJudge/`) and drive it by hand
   for at least one scenario — this hasn't been done yet in this session; only the scripted-answer
   path has been exercised.
2. Pick Scenario 2 or 3 and, when asked the repeated question, give an answer that actually resolves
   it (e.g., "Yes, an independent witness saw it happen twice") instead of "I don't know" — confirm
   the loop stops re-asking and reaches sufficiency in that case, in contrast to the transcripts in
   `observed-limitations.md`.
3. Try a scenario where you give an answer that's a *plausible but not strictly entailed* reading
   (e.g., answer a Knocked-Out question with "the opponent's Pokémon had already taken a lot of
   damage" rather than a direct yes/no) and inspect whether it lands in `Hypotheses` or gets wrongly
   promoted to `ConfirmedFacts` — this is the milestone's core transferable skill.
4. Read `ClarificationResultSchema.Schema` and `FactExtractionResultSchema.Schema` side by side with
   the record types they deserialize into (`PokeJudge/StructuredState/`) — notice the schema is
   hand-maintained, not generated from the C# type; a mismatch between them would only surface at
   runtime, not compile time.
5. Compare this milestone's reliability against Milestone 1's `baseline-run-output.md`: does
   structured output actually eliminate the "not sufficient" → misclassified-as-`SUFFICIENT` failure
   from Milestone 1, or does the reliability gain just move up a level (the schema is always
   respected, but the model's *content* inside that schema can still be inconsistent, as seen in
   Scenario 1's under-supported draft ruling)?

## Git Status

- **Branch:** `milestone/2-judge-clarification-structured-responses`
- **Uncommitted:** yes — all implementation and planning-doc changes are in the working tree,
  nothing staged or committed yet (this skill does not commit automatically).
- **Unexpected files:** none. `git status` shows exactly the expected new/modified files:
  `PokeJudge/AI/`, `PokeJudge/StructuredState/`, `PokeJudge/Clarification/`, modified
  `PokeJudge/Program.cs`, the corresponding new folders under `PokeJudge.Tests/`, and
  `.project-plans/milestone-2/` (plan + this summary + the observed-limitations transcript doc).
