# Milestone 6 — Learning Checkpoint

## Q&A Transcript

### 1. Why does retrieval need to re-run on turn 2 rather than just adding the new confirmed fact to the sufficiency prompt against the same turn-1 chunks?

**Answer:** "Just adding the judge's new answer to the sufficiency prompt would give the LLM more information about the game state, but it would still be working with evidence that was retrieved before we knew that information. The new fact could make completely different rules relevant. By retrieving again first, turn 2 gets both the updated game state and evidence that matches it, and then the sufficiency assessment can decide if we actually have enough to make the ruling."

**Feedback:** Correct. Confirmed the mechanism directly — confirmed facts change what's true, which changes what's relevant, so the query must re-run, not just the reasoning. Tied to the real observed evidence in `observed-limitations.md` (Special Condition scenario: 3 of 5 turn-2 chunks genuinely changed after a fact was confirmed). Memory trick given: "New facts, new search."

### 2. Why does the sufficiency/ruling prompt bother showing the model hypotheses at all, instead of omitting them and only showing confirmed facts?

**Answer:** "We still show the hypotheses because they give the model context about the possible interpretations we've identified and can help it figure out what information is still missing. For example, if two possible hypotheses could lead to different rulings, that tells the model it may need to ask a clarifying question to determine which one is actually true. But since they haven't been confirmed by the judge, the model is specifically told not to use them as evidence for the ruling. So they're useful for identifying ambiguity and deciding what to ask next, but only confirmed facts can actually be relied on when making the ruling."

**Feedback:** Correct. Hypotheses stay visible so the model can use them to spot where ambiguity lives (often driving what to ask next), while being explicitly barred from treating them as load-bearing evidence — also keeps state fully inspectable to the model rather than silently dropping information the app already extracted.

### 3. Why is ruling generation split into its own LLM call instead of having the sufficiency call also produce the ruling when it says "sufficient"?

**Answer:** "We split them because deciding whether we have enough information and actually writing the ruling are two different jobs. The sufficiency call can stay focused on whether the confirmed facts and retrieved evidence are enough, without also trying to come up with the answer at the same time. Once sufficiency is confirmed, RulingGenerator gets a fresh call using the final game state and evidence to produce the ruling. It also makes the system easier to test and debug because we can separate a bad sufficiency decision from a bad final ruling."

**Feedback:** Correct. Single-responsibility per call and separate failure modes for debugging, exactly right. Added: this also matches PRD §11's architecture diagram directly (two separate boxes), and `RulingGenerator` deliberately has no idea a clarification loop even happened — keeping each prompt's job legible on its own. Memory trick given: "One call, one question."

### 4. What would have to exist for a `Strong` Source Support label to be independently verified rather than just trusted, and why doesn't it exist yet?

**Answer:** "Right now, if the model says the source support is Strong, we are basically trusting that label. Nothing else in the code checks that the ruling is actually fully supported by the retrieved sources. To independently verify it, we would need another validation step that checks the important claims in the ruling against the evidence and confirms whether Strong, Partial, or Insufficient is actually correct. Milestone 6 doesn't have that yet because the source support label is only a first pass while we're getting the full retrieval, clarification, and ruling flow working."

**Feedback:** Mostly correct — right that nothing checks it, but answer stayed generic. Asked a follow-up to get concrete: "What's the simplest, fully deterministic check you could add right now, using only data already available in `RulingResult` and `retrievedChunks`?"

**Follow-up answer:** "We could check that every CitedChunkId returned by the model actually exists in the retrievedChunks that were passed into that ruling call. If the model says the support is Strong but cites a chunk it never even received, we would know that result is invalid. It still wouldn't prove the cited chunks actually support the ruling, but it would catch the model inventing or citing evidence outside of what it was given."

**Follow-up feedback:** Correct. Exactly the deterministic check the review flags as a good Milestone 7 candidate — pure set membership, no model judgment required. The caveat (proves not-fabricated, not proves-supports) is the important nuance and maps directly to PRD §7's deterministic-vs-model-judgment split. Memory trick given: "Exists vs. supports."

### 5. Why is crashing with `InvalidOperationException` on `isSufficient: false` + zero clarifying questions the *correct* behavior per PRD §9, rather than a bug to silently handle?

**Answer:** "Crashing is intentional because isSufficient: false means we know we don't have enough information, but returning no clarifying questions means the loop has no safe way to continue. Just looping again would probably repeat the exact same state, and inventing a generic fallback question could make us ask something that isn't actually material to the ruling. Per the PRD, we should fail instead of guessing or continuing without a valid path forward."

**Feedback:** Correct. Directly matches PRD §9's "fail visibly rather than silently degrading into an unsupported answer," and a generic fallback question would violate the "materiality must be retrieval-grounded, not invented" principle from PRD §8.

### 6. What is context-window budgeting, and what would concretely start going wrong if `topK` were bumped to 200 with no other safeguard?

**Answer:** "Context-window budgeting is making sure we only send the model as much information as it actually needs. With topK: 5, this hasn't really mattered yet because the retrieved text is pretty small. If we bumped it to 200, every prompt would include a huge amount of source text, which could make calls slower and more expensive, eventually exceed the model's context limit, and make it harder for the model to focus on the most relevant evidence. So we would need some kind of limit or filtering instead of just continually increasing how much context we send."

**Feedback:** Correct, with a good extra insight (relevant-evidence dilution / "lost in the middle" effect), separate from the hard token-limit problem. Sharpened definition: context-window budgeting specifically concerns the model's fixed maximum tokens (input + output combined) per call — budgeting means deciding what to prioritize, truncate, or drop when the total *would not fit* at all, not just "send less to be efficient." Memory trick given: "Budget implies scarcity — nothing was scarce yet at topK: 5."

### 7. Why is it correct for `FactExtraction` to keep "an incorrect marker was placed" as a hypothesis rather than a confirmed fact, and what would go wrong downstream in `RulingGenerator` if it had been promoted?

**Answer:** "That part is still our interpretation of what probably happened. If we promoted it to a confirmed fact, `RulingGenerator` could rely on it when making the final ruling and treat an assumption like something we actually know is true. Keeping it as a hypothesis lets the model consider that possibility without using it as evidence."

**Feedback:** Correct. Names the exact downstream risk — `RulingGenerator` treating a plausible guess as load-bearing evidence for a recommendation/repair/penalty, which is precisely PRD §8's "no material inference" rule.

---

## Learning Checkpoint Result

**Strong**

## Concepts I Understand

- Iterative retrieval as a structural loop property: confirmed facts change relevance, so retrieval must re-run every turn, not just the reasoning step.
- Why hypotheses are shown to the model but explicitly excluded from establishing sufficiency or supporting a ruling.
- Why sufficiency assessment and ruling generation are separate LLM calls (single responsibility, separable failure modes, matches PRD §11's architecture).
- The unvalidated nature of the first-pass Source Support label, and a concrete, correctly-scoped deterministic check (citation-ID existence) that would catch one specific failure mode without requiring model judgment.
- Why "fail loudly" (an exception) is the correct response to a malformed insufficient-with-no-questions result, per PRD §9.
- Context-window budgeting as a concept, including a secondary effect (relevant-evidence dilution) beyond the hard token limit.
- The confirmed-fact vs. hypothesis boundary and its concrete downstream consequence for `RulingGenerator`.

## Concepts to Reinforce

- The precise definition of "context window" (a fixed token budget for the whole call, input + output) — the first answer was directionally right but framed budgeting more as "send less" than "decide what to do when it truly won't fit."
- The deterministic-vs-model-judgment split for grounding checks (existence-of-citation vs. semantic-support-of-claim) — understood correctly once prompted, worth keeping sharp heading into Milestone 7 since that's the milestone's central topic.

## Milestone Takeaway

1. Retrieval is not a one-shot step — it's structurally embedded in the loop and must re-run whenever confirmed facts change, because facts determine relevance, not just what the model reasons about.
2. Sufficiency assessment and ruling generation are deliberately separate LLM calls, matching PRD §11's architecture diagram — this isolates two different jobs and two different failure modes.
3. Source Support is currently a fully-trusted, unvalidated model self-label. Milestone 7 formalizes it by layering deterministic checks (e.g., citation-ID existence) with the checks that genuinely require model judgment (e.g., semantic support) — and by then asking why the same model checking its own output isn't full independent validation.
4. Failing loudly on malformed model output (insufficient + zero questions) is intentional, evidence-backed behavior per PRD §9 — not a bug, and not something to paper over with a generic fallback question.

## Interview Readiness

1. **"How does your RAG system handle multi-turn conversations where new information changes what's relevant?"** A strong answer covers: the retrieval query is rebuilt from the original scenario plus all confirmed facts at the top of every turn (not just appended to the prompt); retrieval genuinely re-runs, not just the reasoning step, because new facts can surface passages the earlier, incomplete query couldn't find; and ideally cites concrete evidence (e.g., a measurable before/after change in the retrieved chunk set) rather than just asserting the mechanism.

2. **"How do you avoid trusting an LLM's self-reported confidence or relevance labels blindly?"** A strong answer distinguishes deterministic checks (e.g., does a cited ID actually exist in what was supplied — pure lookup, no model needed) from checks that inherently require model judgment (e.g., does a cited passage actually semantically support the claim), and recognizes that having the *same* model both generate and self-check its own output is not independent validation — correlated blind spots can survive the check.

3. **"Why separate the 'do we have enough information' decision from the 'what's the answer' generation into two different LLM calls?"** A strong answer covers: single responsibility per call, independently debuggable failure modes (a bad sufficiency call vs. a bad ruling call), and matching an explicit architectural diagram/spec rather than letting one call quietly grow two jobs.

## Recommendation

**Ready for PR Review**
