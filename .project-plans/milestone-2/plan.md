# Milestone 2 — Judge-Focused Prompting, Clarification, Structured Responses

Status: planned, not started
Source: PRD.md §7-8 (Functional/AI-Specific Requirements), §11 (architecture progression), §14 (Milestone Roadmap, mock-corpus note), "Learning Objectives → Milestone 2"

## 1. What We Will Build

The console app grows from "one raw LLM call" (Milestone 1) into a small multi-turn clarification loop, still a single console project, still in-memory, still no retrieval:

1. A **judge-focused system prompt** — separate from user input — establishing the operational, neutral persona (PRD §10) and instructing the model to reason only over supplied reference text, never pretrained Pokémon knowledge.
2. A small, **hand-authored mock corpus**: 3 scenarios (reusing/extending Milestone 1's scenario themes for continuity), each with 2-4 short policy snippets — including deliberately irrelevant or partial ones alongside the actually-material one(s), each with a stable ID. This stands in for retrieval, which doesn't exist until Milestone 6 (PRD §14's explicit Milestone 2 note).
3. **Structured output**, not parsed free text: the provider call is schema-constrained (Gemini's native `responseSchema` / JSON mode) and deserialized directly into C# records expressing:
   - Sufficiency (is the scenario ready to rule on, given only the mock corpus?)
   - Clarifying questions (each conceptually tied to a specific snippet's applicability)
   - A rough draft ruling shape (recommended action + which snippet IDs support it) — intentionally rough; this is not the final grounded ruling with citations/Source Support, which arrives at Milestones 6-7.
4. **Application-owned structured game-state**: facts tracked as **confirmed** (explicit statement or strict logical entailment), **unknown** (not yet supplied), or **possible interpretation/hypothesis** (plausible but unconfirmed) — per PRD §8. A hypothesis must never be silently promoted to confirmed or used to support sufficiency/ruling content.
5. A **multi-turn console loop**: judge enters a scenario, app calls the LLM for a sufficiency/clarification result against the mock corpus + known facts so far; if insufficient, the judge answers the clarifying question(s) in free text; the app classifies that answer into confirmed facts vs. hypotheses (a second structured LLM call), updates state, and re-asks against the *updated* known facts (never re-asking something already confirmed) until sufficient or a small turn cap.

Per PRD §11's architecture progression, this stays **one project** — light internal module organization (e.g., separate files/folders for the LLM abstraction, structured-state types, and clarification logic) is reasonable, but not separate `.csproj` projects.

## 2. AI Concepts Being Learned

- **System instructions vs. user input** — a distinct channel with distinct trust/behavior, not just a longer prompt string.
- **Prompt design for a domain persona** — judge-facing tone and scope (PRD §10), not a general chatbot.
- **Structured outputs**: schema-constrained generation deserialized straight into C# types, vs. Milestone 1's free-text + naive regex. This is the concrete payoff for the friction Milestone 1 deliberately produced.
- **Multi-turn state as application data**: the model has no memory between calls; every "known fact" must be re-supplied by the app each turn.
- **Text-derived materiality, not pretrained knowledge**: the sufficiency judgment must be answerable only from the mock corpus's supplied text — the same discipline real retrieval requires from Milestone 6 onward.
- **Confirmed fact vs. hypothesis**: deciding when a judge's statement *strictly entails* another fact (zero degrees of freedom) versus merely makes it *plausible* — PRD §8 calls this out explicitly, and the Learning Objectives section frames it as a transferable "what does an agent actually know vs. merely assume" skill.

## 3. Implementation Steps (in order)

1. **Design the mock corpus.** Hand-author 3 scenarios (can extend Milestone 1's missed-prize / ability-timing / 61-card-deck themes) with 2-4 short, ID-tagged policy snippets each — some material, some deliberately irrelevant or only partially relevant.
2. **Design the structured-output schemas** as C# records:
   - `ClarificationResult(bool IsSufficient, List<ClarifyingQuestion> Questions, DraftRuling? Draft)`
   - `ClarifyingQuestion(string Question, string RelatedSnippetId)`
   - `DraftRuling(string RecommendedAction, List<string> SupportingSnippetIds)`
   - A separate `FactExtractionResult(List<string> ConfirmedFacts, List<string> Hypotheses)` for classifying the judge's free-text answers.
3. **Extend the provider abstraction.** Add a system-instruction parameter (distinct from user content) and a schema-constrained completion path using Gemini's native `responseMimeType`/`responseSchema` support, deserialized directly to the record types above — no string parsing of the model's output.
4. **Write the judge-focused system prompt**: persona/tone per PRD §10; explicit instruction to reason only over the supplied mock-corpus snippets for materiality, never pretrained Pokémon knowledge; explicit instruction to output only via the structured schema.
5. **Build the in-memory structured game-state**: confirmed facts, unknown facts (implied by unresolved clarifying questions), hypotheses — owned by the app, independent of the model's own "memory" (which doesn't exist).
6. **Wire the multi-turn loop**:
   - Judge enters the initial scenario.
   - App calls the LLM (mock corpus + known facts so far) → `ClarificationResult`.
   - If insufficient: print clarifying question(s), read the judge's free-text answer(s).
   - Call the LLM again with a fact-extraction schema to split the answer into confirmed facts vs. hypotheses; update state; never auto-promote a hypothesis to confirmed.
   - Re-run sufficiency against updated known facts (skip re-asking anything already confirmed) until sufficient or a small turn cap is hit.
   - Once sufficient, print the draft ruling shape.
7. **Manual smoke test** across all 3 mock scenarios, including at least one deliberately ambiguous answer (one that only supports a plausible interpretation, not a strict entailment) to check the app correctly keeps it out of confirmed facts.
8. **Document observed failures/limitations** (see below) with concrete transcript examples, in the same evidence-based style as Milestone 1's `baseline-run-output.md`.

## 4. Expected Limitations / Failures to Intentionally Observe

- **Structured output constrains shape, not truthfulness.** The schema guarantees a `DraftRuling` object exists, not that `RecommendedAction` is actually supported by the cited snippets — the model can still fill fields with unsupported claims. This is expected and untouched until Milestone 7's grounding validation.
- **The mock corpus only knows what was hand-written into it.** A judge scenario that doesn't map onto one of the 3 authored scenarios will get a poor or hallucinated materiality judgment. This is the direct, intended motivation for Milestone 3 (real document ingestion).
- **Confirmed-vs-hypothesis is genuinely hard, even for the model.** Expect some misclassifications — a plausible reading treated as confirmed, or a real entailment left as only a hypothesis. Document concrete examples rather than asserting the distinction "works."
- **Materiality is bounded by the fixed mock corpus.** If no snippet's applicability depends on some real-world-important fact, the system will never ask about it — an accepted limitation until real retrieval broadens coverage in Milestone 6.

## 5. What I Should Understand by the End

- The practical difference between a system instruction and user input, and why separating them changes reliability.
- What schema-constrained structured output actually is, how it's implemented against a real provider (Gemini's `responseSchema`), and why it closes the exact gap Milestone 1's naive parser exposed.
- How to represent multi-turn state as application data the app owns, rather than relying on the model to "remember" anything.
- The confirmed / unknown / hypothesis distinction well enough to explain, with a real example from this milestone's own transcripts, why a plausible-sounding inference is not the same as a stated fact.
- Why materiality must stay text-derived even with a fake corpus — and why that same fake corpus's narrow coverage is itself the reason Milestone 3 exists.

## Out of Scope for This Milestone

- Real retrieval, embeddings, or a vector store (Milestones 3-6) — the mock corpus is a deliberate stand-in.
- Final, fully-grounded ruling with real citations and a Source Support (Strong/Partial/Insufficient) classification (Milestones 6-7) — this milestone's "draft ruling shape" is intentionally rough.
- ASP.NET Core API project (deferred; still a console app).
- Persistence beyond in-memory / a single process run (no session store yet).
- Formal evaluation harness (Milestone 8).
