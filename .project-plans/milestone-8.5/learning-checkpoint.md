# Milestone 8.5 — Learning Checkpoint

**Milestone:** Milestone 8.5 — Evaluation Dataset Hardening
**Date:** 2026-08-20

## Q&A Transcript

### Q1: Why 8.5?

**Question:** Milestone 8 built the eval harness; Milestone 8.5 didn't add any new evaluation *capability*. In your own words, what specifically was wrong with the Milestone 8 dataset that made 8.5 necessary, and why couldn't the harness itself have caught that problem on its own?

**Answer:** "It wasn't big enough to actually prove anything. Its very possible with such a small sample set that we were missing critical issues."

**Feedback:** Mostly correct. "Small dataset, might be missing coverage" is true and part of it, but not the concrete trigger cited in the plan. The real trigger: `deck-not-shuffled`, run identically three separate times, produced three different outcomes — not a coverage problem but a trust problem (a single run isn't a stable ground-truth label). The harness itself couldn't catch this because Milestone 8's harness ran each scenario exactly once, with no mechanism to ask "does this behave the same way twice?" — the non-determinism was found by a human manually re-running the same command by hand.

Memory trick given: *"Small dataset" is a coverage problem. "One run per scenario" is a trust problem.*

---

### Q2: Scorer/orchestration boundary

**Question:** In `EvalScenarioSelector`/`Program.cs`, `--repeat` is handled entirely in the orchestration loop — `ScenarioEvalScorer` was never touched. Why does that boundary matter? What would go wrong, conceptually, if you instead built the "did this scenario reliably pass?" logic *into* `ScenarioEvalScorer` itself?

**Answer:** "We keep the scope narrow for the method and this way we can also run as many calls on a scenario as we need to in milestone 9 to get a good dataset."

**Feedback:** Needs clarification. Captured the "narrow scope" intuition but missed the specific architectural argument: `ScenarioEvalScorer.Score()` is a pure function of one trajectory (same input, same output, no state) — that's exactly why it's trivially unit-testable. Pushing "reliability across repeats" into the scorer would force it to judge a *collection* of trajectories and invent a statistical threshold ("3/5 counts as reliable?") that doesn't currently exist — exactly the "model-based reasoning about variability" the plan says must stay out of the scorer.

Memory trick given: *The scorer answers "was this one run right?" — never "were enough runs right?"*

---

### Q3: Source-coverage classification

**Question:** Walk me through what `Evaluation/SourceCoverageFinding.cs`'s four levels (`Sufficient`, `RetrievalProblem`, `PossibleSourceGap`, `ConfirmedSourceGap`) are actually distinguishing between, using `spectator-badges` (classified `Sufficient`) and `deck-under-60` (classified `RetrievalProblem`) as your two examples — why are those two different classifications, when both scenarios *failed* in some way?

**Answer:** [Full four-level definition given correctly, plus:] "For spectator-badges, PokéJudge received enough evidence and still struggled to reason over it. For deck-under-60, the evidence existed in the knowledge base, but retrieval didn't reliably put the relevant rule in front of PokéJudge in the first place. And that distinction tells you where not to make the fix. Adding more source documents would make no sense for deck-under-60, because the exact rule already exists. Likewise, tuning retrieval would probably not fix spectator-badges, because retrieval already succeeded."

**Feedback:** Correct. All four definitions accurate, both examples correctly applied, and the actual point of the classification ("tells you where not to fix") stated unprompted. Added nuance: `Sufficient` isn't "the exact words are present" — it's "would a knowledgeable human judge find this enough," a slightly higher bar. `spectator-badges`'s retrieved passage says "attendance," not "spectators" — a human infers the connection; PokéJudge's sufficiency-assessment step wouldn't commit to that inference, which is why the gap is classified as reasoning, not retrieval or corpus.

Memory trick given: *Sufficient/RetrievalProblem/PossibleSourceGap/ConfirmedSourceGap answers "where does the fix belong: reasoning, retrieval, or the corpus?"*

---

### Q4: Tracing the call sequence

**Question:** When `dotnet run -- evaluate --only mulligan-not-taken --repeat 3` executes, roughly how many separate LLM calls does *one single repeat* make in the best case (resolves in one clarifying round), and what is each call for? Name the distinct calls, in order.

**Answer:** "In the best case, one repeat makes about 6 model calls: two embedding calls for retrieval, two LLM calls for sufficiency assessment before and after the clarification, one LLM call to generate the ruling, and one LLM call for grounding validation. ScenarioEvalScorer itself does not call the model; it only scores the completed trajectory."

**Feedback:** Mostly correct. Got retrieval embeddings, before/after sufficiency assessment, ruling generation, and grounding validation right, and correctly reaffirmed the scorer is model-free (ties back to Q2). Missed one real, distinct call: **fact extraction** — a separate LLM call between the judge's answer and the second retrieval, whose job is classifying the judge's free-text answer into `ConfirmedFacts` vs. `Hypotheses` (the confirmed/hypothesis discipline established since Milestone 6). Correct sequence: embed → assess (turn 1) → fact extraction → embed → assess (turn 2) → ruling → grounding = 7 calls, not 6. This explains why `five-run-validation.md` notes a single `RequiresOneClarification` scenario × 5 repeats can be 25+ calls, and why the rate limit got hit so hard once repeats were added.

Memory trick given: *Retrieve → Assess → Extract → Retrieve → Assess → Rule → Validate.*

---

### Q5: Why the Rationale field was necessary

**Question:** Before this milestone's follow-up work, `ClarificationLoop` would throw `InsufficientWithoutQuestionsException` whenever the model returned `isSufficient: false` with zero questions — with no information about *why*. What specific piece of information was missing from the `ClarificationResult` schema that made this crash undiagnosable, and why couldn't you just look at the retrieved chunks or the confirmed facts to figure out the cause yourself?

**Answer:** "Before, the model could say 'I can't answer' without saying why. The new Rationale field forced it to explain why, which finally let us diagnose the real problem."

**Feedback:** Mostly correct on the first half; didn't address the second half (why raw chunks/facts weren't enough on their own). Explanation given: chunks and confirmed facts tell you what the model *had*, not why it decided what it decided — a developer could only *guess* the cause from those. The `Rationale` field turned a guess into a direct, checkable statement from the model itself (e.g., "none of the retrieved passages address mulligans... instead discuss deck lists, Supporter cards, and infinite loops" — confirming a retrieval miss, not assumed).

Memory trick given: *Chunks and facts are the ingredients. Rationale is the model telling you what it actually cooked.*

---

### Q6: Why the crash fix was out of scope

**Question:** The plan explicitly said `drew-extra-card`'s repeated crash was "evidence the known sufficiency-assessment bug persists, not evidence the expectation is wrong" — i.e., treat it as a thing to *measure*, not fix. You went ahead and fixed the underlying model behavior anyway (`SystemPrompts.Judge`), shared code with the interactive judge-facing console. Setting aside whether it was a *good* engineering decision — why was it *out of scope* for Milestone 8.5 specifically, according to the roadmap?

**Answer:** "It was out of scope because Milestone 8.5 was meant to make the tests and dataset better, not make the AI itself behave differently."

**Feedback:** Mostly correct — right boundary, but missing the roadmap-sequencing argument. The roadmap's dependency chain (8 measures → 8.5 hardens what 8 measured → 9 calibrates against what 8.5 hardened) assumes each stage's subject stays fixed once the next stage starts consuming it. Changing `SystemPrompts.Judge` mid-milestone moves the target after 8.5 aimed at it — the "known crash across six categories" that got documented is no longer the behavior Milestone 9 will calibrate against. Also worth naming: no milestone in the roadmap (8.5, 9, or later) is assigned the job of "fix known reasoning bugs found via eval" — this work wasn't just premature, it was unassigned.

Memory trick given: *8.5's job was to take a clean photograph of the pipeline's behavior. Fixing the pipeline mid-shoot means the photo Milestone 9 inherits isn't of the thing it thinks it's calibrating.*

---

### Q7: Small-dataset risk for calibration specifically

**Question:** The plan says 20-30 scenarios "does not, and cannot, establish a general system error rate or statistically representative judge behavior." Concretely — not just "small sample size is bad" — what specific kind of wrong conclusion could someone draw from this dataset if they forgot that limitation, and why would Milestone 9's calibration work be especially vulnerable to it?

**Answer:** "Milestone 8.5 gives us a better practice test, but it is still not the real exam. Milestone 9 has to avoid tuning confidence so closely to this small practice test that we mistake 'works on our examples' for 'works reliably in the real world.'"

**Feedback:** Mostly correct — good analogy, right spirit (overfitting), but missing the concrete mechanism. Calibration statistics (reliability diagrams, ECE, Brier score) bucket predictions into probability ranges and check observed correctness per bucket. With ~20 scenarios split across ~9 categories, some buckets could have 1-3 data points — a chart built from that looks statistically authoritative while actually being noise. The specific wrong conclusion: declaring PokéJudge's confidence "well-calibrated" (or not) from a chart that isn't statistically meaningful, then acting on it — specifically, showing a numeric confidence percentage to real judges, which the PRD explicitly gates on empirical validation. This is exactly why Milestone 9's plan requires an explicit limitations analysis (per-bucket sample counts, honest assessment of whether the chosen statistic is even supportable) as a required deliverable, not an afterthought.

Memory trick given: *A formula doesn't know how many data points is "enough" — it'll happily output a precise-looking number from 2 samples.*

---

### Q8: Causal effect of the Rationale field vs. the crash-prevention instruction

**Question:** Prediction question. Suppose someone deleted the `Rationale` field from `ClarificationResult` and the corresponding instruction from `SystemPrompts.Judge`, but left `ClarificationLoop`'s crash-detection logic exactly as it is. Would `mulligan-not-taken` still crash sometimes? Would you be able to diagnose *why* if it did? What's the actual causal relationship between the `Rationale` field and the crash rate itself?

**Answer:** "Yes, it would still crash sometimes. The change to prompt was made to try to prevent the crashing by adding additional logic. The rationale field tells us why the crash happened. So if you remove the rationale field it has no result on the crash itself happening but it would stop us from being able to tell why it happened."

**Feedback:** Correct. Precisely separated the two mechanisms bundled into the same prompt update: the "always ask a question, never report insufficient with zero questions" instruction is what actually changed the crash rate; the `Rationale` field is purely diagnostic and has no causal effect on whether the crash occurs. This is a subtle distinction — most people would treat "the prompt fix" as one undifferentiated thing — and it was correctly decomposed without prompting.

---

## Final Assessment

### Learning Checkpoint Result: Developing

### Concepts I Understand
- Source-coverage classification (Sufficient / RetrievalProblem / PossibleSourceGap / ConfirmedSourceGap), correctly defined and correctly applied to distinguish where a fix belongs.
- The causal decomposition of the crash fix — a diagnostic field (rationale) vs. a behavior-changing instruction (always ask a question) are separate levers with separate effects, even shipped in the same prompt update.
- General grasp of why 8.5 exists, the scorer/orchestration boundary, why the crash fix was a scope violation, and the calibration risk of small datasets — all directionally correct throughout.

### Concepts to Reinforce
- The concrete trigger for 8.5: `deck-not-shuffled`'s 3-different-outcomes-in-3-runs finding, not just "dataset too small."
- Why `ScenarioEvalScorer` being a pure function of one trajectory (not repeat-aware) is what keeps it simple and testable.
- Full request trace through `ClarificationLoop`, including the fact-extraction step's role in the confirmed/hypothesis discipline.
- Why the roadmap's sequencing (8 → 8.5 → 9, each depending on the prior stage's subject staying fixed) makes the crash fix more than just "wrong milestone."
- The concrete mechanism behind small-dataset calibration risk (per-bucket sample sizes) — worth reviewing before Milestone 9 specifically, since its required limitations analysis depends on this.

### Milestone Takeaway
1. A single observed run is not a stable ground-truth label — non-determinism must be measured, not assumed away.
2. Root-cause attribution (retrieval vs. corpus vs. reasoning) tells you which layer to fix — conflating them wastes effort on the wrong one.
3. Keep the deterministic scorer ignorant of "how many times has this run" — non-determinism handling belongs in orchestration, never inside the thing making pass/fail judgments.
4. A diagnostic field and a behavior-changing instruction are different tools with different effects — one explains, one prevents.

### Interview Readiness
1. "How do you evaluate a multi-turn, non-deterministic AI system when a single test run can't be trusted as ground truth?" — Strong answer: repeated runs per scenario, each scored independently and deterministically, distinguishing genuine model non-determinism from a wrong test expectation, and why the scoring logic itself must stay repeat-agnostic/pure.
2. "In a RAG pipeline, how do you decide whether a failure is a retrieval problem, a corpus/data problem, or a model-reasoning problem — and why does that distinction matter operationally?" — Strong answer: inspect what was actually retrieved against the real source material, ask whether a knowledgeable human would find it sufficient, and explain that each cause requires a different fix (query/ranking tuning, adding source documents, or prompt/architecture changes).
3. "Why can adding an explicit instruction like 'always explain your reasoning' change a model's behavior beyond just making its output more diagnosable?" — Strong answer: distinguish a field that only exposes reasoning (diagnostic-only) from an instruction that changes what the model is required to do, and recognize these as separable levers even when shipped together.

### Recommendation: Ready for PR Review

No fundamental misconceptions surfaced anywhere in the quiz — every answer was directionally correct; the gaps were about sharpening mechanism and specificity, not correcting wrong beliefs. Worth a quick review of the calibration-bucket-size mechanism specifically before starting Milestone 9, since that milestone's required limitations analysis depends directly on it.
