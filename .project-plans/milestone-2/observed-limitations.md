# Milestone 2 — Observed Limitations & Failures (Manual Smoke Test)

Captured per the plan's step 8: concrete transcript examples of the limitations Milestone 2 is
expected to intentionally exhibit, in the same evidence-based style as Milestone 1's
`baseline-run-output.md`.

- **Date:** 2026-08-15
- **Branch:** `milestone/2-judge-clarification-structured-responses`
- **Provider / model:** Gemini `gemini-flash-lite-latest`, via `GeminiLlmClient.CompleteStructuredAsync`
  (`responseMimeType: application/json` + `responseSchema`)
- **How this was run:** the sandbox this implementation was built in has no interactive stdin, so
  `Program.cs`'s real `Console.ReadLine()` prompts couldn't be driven directly. Instead, a scratch
  harness (outside the repo) called the real `ClarificationLoop` against the live Gemini API with
  pre-scripted judge answers per scenario — same production code path (`ClarificationLoop`,
  `GeminiLlmClient`, `PromptBuilder`, `SystemPrompts`, schemas), just a scripted `askJudge` delegate
  instead of a console one. **The interactive console flow itself (arrow-key-free menu selection,
  live typed answers) has not been manually driven and should be tried directly before considering
  this milestone fully exercised** — see the completion summary's "What I Should Try."

## Scenario 1 — Missed Prize Card: the clean/expected path

Reached sufficiency in 2 turns with a coherent, correctly-cited draft ruling:

```
[Q, re:A1] When was the missed Prize card error discovered relative to the player's next turn?
[A] The player already ended their next turn before noticing.
[Assessment] Sufficient

Confirmed facts:
  - The player ended their next turn.
  - The missed Prize card error was noticed after the player ended their next turn.
Hypotheses:
  - The opponent noticed the missed Prize card error.
  - The game has progressed significantly past the turn the error occurred.
Draft ruling: Do not award the missed Prize card retroactively. Correct the Prize count to reflect
  the correct number of remaining Prizes, and issue a Warning for a Procedural Error.
Supporting snippets: A1, A2
```

Note the model only asked about A1's discovery-timing condition, not A2's "is this a first offense"
condition, even though a real judge would need that too before finalizing a penalty tier. It drew a
plausible penalty conclusion anyway. This is exactly the plan's anticipated limitation:
**structured output constrains shape, not truthfulness** — the schema guarantees a `DraftRuling`
object exists, not that every cited snippet's conditions were actually confirmed. Untouched until
Milestone 7's grounding validation, as intended.

## Scenario 2 & 3 — repeated re-asking when the judge genuinely doesn't know

Both scenarios exhausted the 4-turn cap. In both cases, once the judge answered "I don't know" to a
question, the sufficiency call kept re-asking a reworded version of the *same* question every
subsequent turn, rather than treating "unknown and unknowable" as a distinct terminal state:

```
[Q, re:C4] Did the player's deck as played match the card counts on their registered decklist?
[A] This is the player's first Procedural Error this match.
[Assessment] Insufficient (1 question(s))
[Q, re:C4] Does the player's physical deck match the card counts on their registered decklist?
[A] The player doesn't have their registered decklist on hand right now.
[Assessment] Insufficient (1 question(s))
[Q, re:C4] Does the deck as played match the card counts on the player's registered decklist?
[A] I don't know.
[Assessment] Insufficient (1 question(s))
[Q, re:C4] Does the deck as played match the card counts on the player's registered decklist?
[A] I don't know.

Turn cap reached without sufficiency.
```

The fact-extraction step faithfully turned "I don't know" into confirmed facts like *"The judge does
not know whether the deck as played matches the card counts on the player's registered decklist"* —
which is, strictly, a correct entailment of what was said (no hypothesis was wrongly promoted to
confirmed; the confirmed/hypothesis separation held). But that confirmed fact is about the judge's
epistemic state, not the game state the policy snippet actually turns on, so it never resolves C4's
condition — the sufficiency call has no way to recognize "this fact is unknowable, proceed anyway or
flag Insufficient" and just re-asks. **This is the "never re-asking something already confirmed"
requirement holding at the letter (it wasn't the identical prior answer) while missing the point** —
a good concrete example of why sufficiency/fact-extraction being separate LLM judgments, not
deterministic app logic, is itself a limitation worth carrying into later milestones.

Scenario 3 also produced directly contradictory hypotheses in the same result — *"The player's deck
matched the card counts on their registered decklist"* **and** *"The player's physical deck does not
match the card counts on their registered decklist"* both listed as hypotheses simultaneously. This
is the plan's anticipated **"confirmed-vs-hypothesis is genuinely hard, even for the model"**
limitation, concretely observed: the model hedges by emitting both directions of a hypothesis rather
than picking one plausible interpretation.

## Turn cap as a safety valve — working as intended

Both Scenario 2 and Scenario 3 hit the turn cap and stopped **without ever fabricating a ruling** —
`ClarificationOutcome.TurnCapExhausted` correctly reported no draft. This is direct evidence for the
PRD's "must not issue a ruling when it has flagged material facts as missing" requirement holding at
the orchestration level, independent of whether the underlying LLM calls behave well.

## Errors fail visibly

Mid-run, the free-tier Gemini quota (15 requests/minute) was exceeded. `GeminiLlmClient` did not
swallow this or silently degrade — it surfaced a full `HttpRequestException` with the provider's
`429 RESOURCE_EXHAUSTED` body intact, which propagated up through `ClarificationLoop.RunAsync`
uncaught. This matches the PRD's "errors from the LLM provider... must fail visibly" requirement.

## Mock corpus's fixed coverage — not exercised this run

All three scripted runs used the corpus's own scenario text verbatim, so the "a real judge scenario
that doesn't map onto one of the 3 authored scenarios gets poor materiality judgment" limitation
wasn't directly triggered here. It remains the explicit, accepted motivation for Milestone 3 (real
document ingestion) rather than something this milestone needed to reproduce on its own scenarios.
