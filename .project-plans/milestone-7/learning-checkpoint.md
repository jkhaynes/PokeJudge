# Milestone 7 — Learning Checkpoint

## Q&A Transcript

### 1. `RulingGenerator`'s prompt includes the scenario, confirmed facts, hypotheses, and the full retrieved chunk set. `GroundingValidator`'s prompt includes only the ruling's recommendation/explanation and the *cited* chunks' text. Why is that narrower scope correct?

**Answer:** "The grounding validator has a different job from RulingGenerator. It isn't trying to understand the whole scenario again, it's only checking whether the final ruling is actually supported by the chunks it cited. If we gave it all of the original context and retrieved evidence, it could use that extra information to justify the ruling even if the cited chunks themselves didn't support it. Keeping the prompt narrow makes the validation specifically about whether the cited source evidence backs up the recommendation and explanation."

**Feedback:** Correct. Named the real risk precisely: a validator with full context could reconstruct a justification from material the ruling never actually cited, weakening the check from "does the citation support the claim" to "is there *some* plausible justification somewhere." Memory trick given: "Check the receipt, not the whole store."

### 2. Why does `SourceSupportAssigner` assemble the final Strong/Partial/Insufficient label with plain, deterministic C# instead of just asking the model for the label directly?

**Answer:** "The LLM is still useful for judging whether the ruling is actually grounded in the cited evidence, but we don't want it to also make the final support label completely on its own. SourceSupportAssigner takes the validation results we already have and applies a consistent set of rules in normal C#. That makes the final Strong, Partial, or Insufficient label deterministic and easier to test and debug, instead of just trusting another model judgment."

**Feedback:** Mostly correct — right on mechanism (deterministic, testable, debuggable), but missed the sharper reason: asking a second LLM call for the label directly would just recreate Milestone 6's exact problem with an extra step, producing a label that looks more validated but still isn't checkable against PRD §8's actual criteria. The LLM is necessary for the sub-question it's suited for (does this citation support this claim); combining all evidence into a final label has to be inspectable code so a specific rule can be pointed to. Memory trick given: "The model reads evidence; code renders the verdict."

### 3. Walk through why the Special Condition scenario produced a validated **Strong** label while the model's own self-report said **Partial** — mechanically, what was each citation actually asked versus what the holistic self-assessment was actually judging?

**Answer:** "Mechanically, GroundingValidator marked all three citations as ExplicitSupport because each source really did support the individual claim it was cited for, like how Asleep or Confused is marked. With no conflicts and all the deterministic checks passing, SourceSupportAssigner then correctly followed its rules and produced Strong. The problem is that we're only validating each citation individually. Nothing checks whether those supported claims actually add up to full support for the overall recommendation to investigate and correct the marker. That step still requires some judge discretion that isn't directly prescribed by the cited text, which is what the model was considering when it originally said Partial. So this shows that even though the new validation is more structured, it can still confidently overrate source support if we're validating at the wrong level."

**Feedback:** Correct, and precisely put — this milestone's central finding, stated more cleanly than the written analysis itself. Per-citation grounding and whole-ruling grounding are genuinely different questions; only the first was ever built. Memory trick given: "All bricks sound, wall might not stand."

### 4. `GroundingValidator` and `RulingGenerator` share the same `ILlmClient` instance. Concretely, what's one specific way this could let a real grounding problem slip through uncaught?

**Answer:** "One specific risk is that the same model could make the same mistake twice. RulingGenerator might stretch the evidence and produce a recommendation that isn't fully supported, then GroundingValidator could read the citations the same way and agree that they support it. So even though validation is a separate call, using the same model means it may share the same blind spots instead of independently catching the problem."

**Feedback:** Correct. Core mechanism named: correlated blind spots, not independent checks. Added a concrete flavor: a systematic reading tendency (e.g., over-reading permissive language as prescriptive) would recur identically in both calls, since it's the same underlying capability making the same kind of misread twice. Memory trick given: "Don't ask the fox to guard henhouse #2."

### 5. `DeterministicGroundingChecks.AllCitationsExist` returns `false` for an empty citation list, even though "every ID in an empty list exists" is vacuously true. Why override that, and what would break in `SourceSupportAssigner` without the override?

**Answer:** "We deliberately return false for an empty citation list because in this system, no citations means there is no source support to validate. If we let the empty list pass just because there are technically no invalid IDs, SourceSupportAssigner could treat a ruling with no evidence as if it passed the citation check and give it a stronger label than it should."

**Feedback:** Correct, and I traced the exact consequence: without the override, `allCitationsExist` would be vacuously true, and the combinator's `levels` list (built from the empty `citedChunkIds`) would also be empty — so `levels.Any(Unsupported)` and `.Any(Interpretation)` both vacuously evaluate false, falling all the way through to **Strong** with zero actual evidence. Memory trick given: "Vacuous truth is still empty-handed."

### 6. If the model's grounding-classification response drops one of the cited chunk IDs (never classifies it), what does the code do with it, and why is that the safe choice?

**Answer:** "If one of the cited chunks is missing from the model's grounding response, the code treats that as a validation failure rather than just ignoring it. That's safer because a missing classification means that citation was never actually checked. If we ignored it, we could accidentally give the ruling a stronger support label even though not all of its evidence was validated."

**Feedback:** Correct. Named the exact mechanism as a follow-up: the combinator builds a lookup from `assessment.Citations` and defaults any missing ID to `CitationSupportLevel.Unsupported`, which forces the same "Insufficient" path as an explicit bad classification — same fail-safe principle as question 5, applied at a different point in the pipeline. Memory trick given: "No answer isn't a good answer."

### 7. The missed-Prize scenario, re-run against much better retrieval (0.80 on-topic vs. ~0.75 topically-adjacent before), still crashed the same way. Why does this show the failure was never really about retrieval quality, and why is it invisible to everything built this milestone?

**Answer:** "Since we now retrieve genuinely relevant Prize-card content and the exact same failure still happens, it shows that retrieval quality was only part of the original problem. The content gap from Milestone 6 was real, but fixing it still left the sufficiency call returning isSufficient: false with no clarifying questions. That points to a separate problem with the sufficiency/clarification step itself. Everything we built this milestone happens after a ruling is generated, so none of the grounding validation can catch or fix this because the loop crashes before it ever reaches that point."

**Feedback:** Correct on both halves — the diagnostic separation (retrieval quality and sufficiency-assessment quality are distinct failure modes) and the architectural point (`GroundingValidator` only ever runs downstream of a successful `RulingGenerator` call, which only runs after the clarification loop reports `Sufficient` — a crash at the sufficiency step is entirely upstream and structurally invisible to this milestone's work). Memory trick given: "Downstream fixes can't see upstream crashes."

---

## Learning Checkpoint Result

**Strong**

## Concepts I Understand

- Narrow-scope prompting for a validation call, specifically to prevent context-assisted rationalization of an under-grounded claim.
- Why the final Source Support label must be assembled by deterministic, inspectable code rather than another free-form model call, and why the latter would just relocate Milestone 6's original problem.
- The per-citation vs. whole-ruling grounding gap, demonstrated with the real Special Condition case, including the counterintuitive implication that more structured validation can be confidently *wrong* if it checks the wrong granularity.
- Correlated blind spots from same-model self-validation, with a concrete failure mechanism, not just the general caveat.
- Two related "fail closed, not open" design choices (empty citation lists, dropped classifications) and why both default to the unfavorable outcome rather than a vacuous pass.
- The clean separation between retrieval quality and sufficiency-assessment quality as distinct failure modes, and why this milestone's grounding work has no visibility into failures upstream of ruling generation.

## Concepts to Reinforce

None identified this session — all seven answers were correct on the first pass, several with precision beyond what was asked.

## Milestone Takeaway

1. Grounding validation only works at the granularity it's built for — per-citation checks cannot catch whether a ruling's *synthesis* across citations requires judgment, even when every individual citation is genuinely well-supported. The Special Condition scenario is now real evidence of this, not a hypothetical.
2. A deterministic combinator function is what actually makes Source Support "criteria-based, not raw model opinion" — the LLM's role is answering one narrow, checkable sub-question (does this citation support this claim), not assembling the final verdict.
3. Same-model self-validation is not independent verification, concretely: a systematic misreading in generation is likely to recur identically when the same model is asked to check its own work.
4. Retrieval quality and sufficiency-assessment quality are separable problems — fixing one (as the corpus expansion did) does not automatically fix the other, and this milestone's validation work is architecturally blind to failures that occur upstream of ruling generation.

## Interview Readiness

1. **"How do you validate that an LLM-generated recommendation is actually grounded in its cited sources?"** A strong answer distinguishes deterministic checks (citation-ID existence, a lookup) from checks requiring model judgment (does the cited text actually support the specific claim), explains why the latter still needs a model, and is honest that using the same model for generation and validation is not independent verification — correlated blind spots can survive the check.
2. **"If your validation pipeline and your model's own self-report disagree, which do you trust?"** A strong answer resists a single blanket rule and instead explains that disagreement itself is diagnostic: it can mean the self-report was wrong, or it can mean the validation is checking the wrong thing at the wrong granularity (as demonstrated by the Special Condition case, where the more "structured" answer was arguably the less accurate one).
3. **"Why not just ask the model to output a calibrated confidence score alongside its answer?"** A strong answer explains the difference between a model's free-form self-report and a criteria-based signal assembled from checkable inputs, and connects this to why a reliability signal needs to be built from decomposable, verifiable sub-checks rather than trusted as a single emitted number.

## Recommendation

**Ready for PR Review**
