# Milestone 2 — Learning Checkpoint

## Q&A Transcript

### 1. System instruction vs. user input

**Question:** In `GeminiLlmClient.CompleteStructuredAsync`, the request body sends `system_instruction` and `contents` (user content) as two separate fields to the Gemini API. Why does this milestone bother with that separation instead of just concatenating the judge persona text and the scenario/state data into one big prompt string? What would you actually lose if we went back to one combined string?

**Answer (verbatim):** "When we split the input out into two different variables like this, we are able to use system instructions to tell the llm HOW the ai should reason and then use the prompt to tell the ai WHAT it is currently reasoning. It also helps defend against prompt injection because system instructions are hard coded instructions I'm providing and not user provided content like the prompt itself."

**Feedback:** Correct. Captured the two things that matter most: role separation (persona/behavior vs. task data) and the trust boundary for injection defense. Added nuance: providers like Gemini also train the model to weight the system channel more heavily and resist being overridden by user-turn content — a real model behavior guarantee, not just semantic organization. Combining into one string would lose that weighting and force re-stating persona instructions every turn. Also noted: our "user content" bucket currently mixes mock-corpus snippets (standing in for retrieved document content) and judge-derived facts — both get the same trust treatment for now, which is fine at this milestone's scope but worth remembering as retrieval becomes real later. Memory trick given: *system = who you are, user = what you're looking at.*

### 2. Structured output's actual guarantee

**Question:** The review noted that Scenario 1's `DraftRuling` cited both snippet A1 and A2 as support, but only A1's condition had actually been confirmed through the clarification loop. The `responseSchema` on that call was respected perfectly — no malformed JSON, no parsing failure. So what exactly did the schema guarantee here, and what did it *not* guarantee? Why couldn't `StructuredResponseParser` or the JSON schema itself have caught this problem?

**Answer (verbatim):** "The schema guarentee's just that the response we receieved fits what we expect it terms offormatting. It doesn't guarentee that the response is correct in terms of logic."

**Feedback:** Correct. Added the "why": neither the schema nor `StructuredResponseParser` has any domain knowledge — they check syntax (types, required fields, shape) with zero awareness of what "A2 supports this ruling" means. Verifying that claim requires cross-checking `SupportingSnippetIds` against `GameState.ConfirmedFacts` and the snippets' actual text — a semantic grounding check, which is exactly what Milestone 7 exists to formalize (some checks deterministic, like citation-ID existence; some requiring model judgment, like semantic support). Memory trick given: *schema checks the envelope, not the letter inside it.*

### 3. Application-owned state

**Question:** Suppose, hypothetically, `PromptBuilder.BuildSufficiencyPrompt` stopped including `state.ConfirmedFacts` and `state.Hypotheses` in turn 2's prompt — it only sent the scenario and snippets, same as turn 1. Concretely, what would go wrong in the loop, and why? What does this tell you about where "memory" actually lives in this system?

**Answer (verbatim):** "Without memory from the previous turn's, the application may ask the same clarification question over and over and hence get stuck. It could also decide in one pass through that it all of a sudden does have enough clarification, and move to producing a ruling without any additional context. This shows us that gemini itself has no memory. The "memory" is inside our application and has to be provided during each call to the llm."

**Feedback:** Correct — thorough, including the subtler point about the model potentially flipping to "sufficient" with no justification, since these are live stochastic calls, not deterministic functions; identical input across turns doesn't guarantee identical output. Core lesson stated precisely: the model is stateless between calls, and `GameState` is the only thing with memory, persisting only because `PromptBuilder` re-serializes it into every prompt. Connected forward to the real smoke-test finding: even *with* facts correctly passed, Scenarios 2/3 still looped — because the confirmed content didn't resolve the underlying policy condition. Two distinct stall modes worth keeping separate: state never reaching the model at all (this hypothetical) vs. state reaching the model but not being useful (what actually happened). Memory trick given: *the LLM has amnesia between calls — `GameState` is the only thing with a memory in this system.*

### 4. Confirmed vs. hypothesis, applied

**Question:** Suppose the clarifying question was "Was a Pokémon Knocked Out this turn?" and the judge answers: "Yeah, I think so — the attack did enough damage to KO it." Should the fact-extraction call classify "a Pokémon was Knocked Out" as confirmed or as a hypothesis here? Walk through why, using the strict-entailment standard from `SystemPrompts.FactExtraction`.

**Answer (verbatim):** "\"I think so\" adds doubt to the input. Since the fact extraction prompt says that a hypothesis includes any inference, even a very resonable sounding one, i would think the ai would mark this sentence as a hypothesis."

**Feedback:** Correct. The hedge language is the clearest signal. Added a deeper layer: even stripping "I think so," the claim "the attack did enough damage to KO it" is *still* not a strict entailment — it's the judge's own inference from damage dealt to an outcome, not a directly observed fact, since it depends on the target's exact remaining HP. Two independent signals push this to hypothesis: the explicit hedge, and the fact that the claim itself is derived reasoning rather than direct observation. Contrasted with the system prompt's own example ("no other Pokemon were in play" → zero-degrees-of-freedom entailment). Memory trick given: *if the judge is doing math or guessing in their own head to get to the fact, it's a hypothesis — even if they sound confident.*

### 5. Tracing a turn through the code

**Question:** Walk me through what actually happens, in order, after `ClarificationLoop.RunAsync` gets back a `ClarificationResult` where `IsSufficient` is `false` and there are two `ClarifyingQuestion`s. Which methods get called, in what order, and how many total LLM calls does answering both questions cost before the next sufficiency check happens?

**Answer (verbatim):** "Once it gets the result back, it verifies that there are questions. Since there are, it will entire the for loop for question 1 where it calls askJudge(question) which asks the human user the question. Once the judge answers the question, it calls ExtractFactAsync(question, answer) which makes a call to the llm clients CompleteStructuredAsync so thats one call to the llm. It then stores the extracted information. Now question 1 is in the game state and it repeats this process for question 2 making a second llm call. Now the for loop advances to the next turn and makes a call to assessasync which is a call to the llm. If it is still not sufficient it will go through this whole process again until either 1. it is sufficient or 2. it has reached its max turns of 4."

**Feedback:** Correct — the call sequence is exactly right: `askJudge` (human I/O, no LLM call) → `ExtractFactsAsync` via `CompleteStructuredAsync` (1 LLM call) per question, twice for two questions, then the next loop iteration's `AssessAsync` (another LLM call). Total from "insufficient with 2 questions" to knowing the next result: 3 LLM calls (2 extract + 1 re-assess), repeating up to the turn cap of 4. Noted a practical consequence: cost/latency scale with the number of clarifying questions per turn, not just the number of turns — a 3-question turn costs 4 calls before the next verdict is even known. Not something this milestone needed to optimize, but a real product concern once this stops being a console toy. Memory trick given: *every question asked = one more LLM round-trip before the next verdict.*

### 6. Why the observed limitation isn't a bug to patch right now

**Question:** In the smoke test, once the judge answered "I don't know" to a clarifying question, the sufficiency call kept re-asking a reworded version of that same question every subsequent turn, until the turn cap stopped it. Why shouldn't we treat this as a bug to fix inside `ClarificationLoop` right now — for example, by adding logic that detects "this looks like the same question as last time" and forces a different outcome? What's actually missing here, and why is Milestone 2's toolset not the right toolset to solve it?

**Answer (verbatim):** "Right now the loop understands \"sufficient\" and \"insufficient\" but this introduces a new situation of insufficient and I can't get the information needed. Whats missing is a way to represent known unknowns instead of only ConfirmedFacts and Hypothesis. This is also why we have a turn cap on this version."

**Feedback:** Mostly correct. Identifying the missing third category — "known unknown / genuinely unknowable," distinct from confirmed and hypothesis — is a sharp insight, correctly tied to why the turn cap exists as a safety net. Missing piece: it's not that we lack the technical ability to patch this today (string-similarity detection on repeated questions is easy to write) — the reason not to is that doing so would either (a) bolt on a fragile heuristic that papers over the real cause without fixing it, since the model still has no principled way to represent "unknowable," or (b) require a genuine schema redesign to add a third state, which the approved plan never called for and shouldn't be slipped in mid-implementation ("don't silently redesign the milestone"). The turn cap isn't a workaround waiting for clever detection code — it's the honest stopgap for a real, currently-unsolved product question. Memory trick given: *a fragile heuristic hides the lesson; the turn cap preserves it.*

### 7. Predicting a change

**Question:** If we deleted the line in `SystemPrompts.Judge` that says "Do not use any pretrained knowledge you may have about Pokémon TCG rules, card text, or tournament policy" — but kept everything else the same, including the mock corpus and schemas — what's the most likely way the system's behavior would degrade? Why would this be hard to notice just by looking at whether the JSON still validates against the schema?

**Answer (verbatim):** "Without this line, the model is more likely to jump straight to a ruling thinking it knows the answers to its questions rather than asking clarifying questions. We may see some answers that look better because it happen to \"get it right\"."

**Feedback:** Mostly correct. The practical failure mode is right — the model reaching for pretrained Pokémon knowledge and sometimes "getting away with it" because that knowledge happens to be accurate — and that instinct about occasionally looking better is actually the most important part of the answer. Connected explicitly back to Question 2's theme: no field in `ClarificationResult`/`DraftRuling` can distinguish *why* the model reached a conclusion, so a `DraftRuling` citing A1 looks identical whether reasoned from A1's actual text or from real-world Pokémon judging knowledge that happened to line up. Invisible to schema validation and to `StructuredResponseParser` — only catchable by manually cross-checking a cited snippet's wording against the claim, which is what Milestone 7 formalizes. Bigger picture: that one sentence in `SystemPrompts.Judge` is currently the entire enforcement mechanism for "materiality must be text-derived" — there is no code checking it today, purely a prompt-level instruction resting on trust. Memory trick given: *a well-formed lie still validates against the schema.*

---

## Learning Checkpoint Result

**Strong**

## Concepts I Understand

- System instruction vs. user input as a genuine architectural/trust separation, not just prompt organization — including the injection-defense angle.
- Structured output's real guarantee (shape/format) vs. what it explicitly does not guarantee (logical/semantic correctness) — stated crisply and correctly twice, in two different framings (Q2 and Q7).
- Application-owned multi-turn state: the model is stateless between calls; `GameState` plus `PromptBuilder` re-serialization is the entire memory mechanism.
- The confirmed-vs-hypothesis strict-entailment standard, correctly applied to a concrete, non-obvious example (hedged language implying doubt).
- Precise, accurate tracing of the actual code path and LLM call count through `ClarificationLoop.RunAsync`.
- Recognizing that turn caps and other "unsatisfying" stopping behaviors can be intentional, honest engineering rather than bugs waiting for a patch.

## Concepts to Reinforce

- Why an observed limitation shouldn't be patched with ad hoc app-side heuristics mid-milestone, even when a fix is technically easy to write — the distinction between "we can't" and "we shouldn't yet" (Q6).
- The idea that a single prompt-level instruction can be the *entire* enforcement mechanism for a stated product requirement, with zero code-level backstop — worth sitting with, since it's a recurring shape of risk in this kind of system (Q7).

## Milestone Takeaway

1. Structured output solves *shape* reliability, not *truth* reliability — those are separate problems, and Milestone 2 only closed the first one.
2. All conversational memory in this system is application data (`GameState`), re-sent in full on every call — the model itself remembers nothing between requests.
3. The confirmed/hypothesis line is drawn at strict logical entailment (zero degrees of freedom), not plausibility — hedged or inferred judge statements belong in hypotheses even when they sound reasonable.
4. An intentional limitation (like the turn cap on "I don't know" loops) is more valuable left visible than quietly patched — it's evidence that should shape a *later*, deliberate design decision, not get papered over by a quick heuristic.

## Interview Readiness

1. **"Why does structured output matter for LLM applications, and what problem does it not solve?"** — A strong answer distinguishes format reliability (guaranteed by schema-constrained generation) from content/truthfulness reliability (not guaranteed at all), and can give a concrete example of a well-formed-but-unsupported response.
2. **"How do you give a stateless model the appearance of a multi-turn conversation?"** — A strong answer explains that the application owns and re-transmits accumulated state on every call, and can articulate the risk of forgetting to do so (repeated questions, or ungrounded verdicts, since the model has no way to know what happened before).
3. **"How do you distinguish something a system actually knows from something it's merely inferring?"** — A strong answer defines strict logical entailment (zero degrees of freedom) as the bar for "known," treats anything requiring an inferential leap — however reasonable — as unconfirmed, and can explain why this matters for downstream decisions that shouldn't be based on assumptions.

## Recommendation

**Ready for PR Review**
