---

name: learning-checkpoint
description: Use this skill when the user wants to review what they learned, be quizzed on the completed milestone, or verify their understanding before moving on.
---------------------------------------------------------------------------------------------------

Run a learning checkpoint for the milestone that was just implemented and reviewed.

This skill exists to verify that I understand **how and why the implementation works**, not merely that the code runs.

Do not modify any files, except for the checkpoint transcript this skill itself produces (see "Recording the transcript" below) — that is this skill's own designated output artifact, not application code or documentation.

## Context

Read:

1. `docs/PRD.md`
2. The current milestone's plan, implementation summary, and review at `.project-plans/milestone-<N>/` (`plan.md`, `implementation-summary.md`, `review.md`)
3. The milestone implementation
4. The most relevant code for the AI concepts introduced in this milestone

Use those materials to determine what I should understand at this point in the project.

Do not quiz me on concepts intentionally deferred to future milestones.

## Quiz approach

Ask questions **one at a time**.

Wait for my answer before asking the next question.

Use approximately 5–8 questions, depending on the scope of the milestone.

Prioritize understanding over memorization.

Questions should focus on things such as:

* Why we implemented something this way
* What information is actually sent to the model
* What role the model plays versus application code
* What assumptions the current implementation makes
* What limitations we intentionally left in place
* What could go wrong
* Why a future milestone is needed
* How changing part of the implementation would affect behavior

Include concrete questions about the code when useful.

For example:

> What is the difference between the system instruction and the user message in our current implementation?

or:

> If the model gives a convincing but incorrect Pokémon ruling in this milestone, why can that happen?

Avoid trivia such as exact method names unless the method itself represents an important concept.

## Follow-up behavior

After each answer:

1. Tell me whether my understanding is:

   * Correct
   * Mostly correct
   * Needs clarification

2. Briefly explain anything important I missed.

3. Give a polished interview-style explanation when it would help me express the concept clearly.

4. When useful, provide a short memory trick.

Do not immediately give the answer before I attempt the question.

If my answer reveals a misconception, ask a short follow-up question when that would help confirm understanding.

## Recording the transcript

The quiz stays fully interactive in the console exactly as described above — ask one question at a time, wait for my real answer, give feedback before moving on. Do not batch questions or withhold them for the sake of the document.

As the quiz proceeds, keep track of each question asked, my raw answer, and the feedback given for that answer (including any follow-up question and answer, if one occurred). Once the quiz is complete and you've produced the Final Assessment, write the complete record to `.project-plans/milestone-<N>/learning-checkpoint.md`, alongside that milestone's `plan.md`, `implementation-summary.md`, and `review.md`. Structure it as:

1. A `## Q&A Transcript` section: for each question, in order, include the question text, my raw answer verbatim, and the feedback given (correct/mostly correct/needs clarification, what was missed, the polished explanation, and the memory trick if one was given). Include follow-up questions as part of the same numbered item they belong to, not as separate top-level entries.
2. The Final Assessment itself (all sections from "Final assessment" below), appended after the transcript.

This file lives in `.project-plans/`, which is committed to the repository — but this skill does not stage or commit it itself; that remains a separate, explicit step. If a checkpoint file already exists for this milestone (e.g. from a prior run), overwrite it with the current one rather than appending.

The Final Assessment is still also given directly in the console as normal — recording it to the file is in addition to that, not a replacement for it.

## Include implementation-specific understanding

At least some questions should require me to explain the actual PokéJudge implementation rather than generic AI definitions.

Examples:

* Trace a request through the current code.
* Explain why a particular prompt exists.
* Explain what data is application-owned versus model-generated.
* Identify where an intentional limitation exists in the current implementation.
* Predict what would happen if a piece of context or an instruction were removed.

## Include expected limitations

Use the milestone plan and implementation to ask at least one question about an intentional limitation or failure mode.

The goal is for me to understand:

> What does this version fail at, and why does that failure motivate a later milestone?

Do not treat an intentional limitation as something I should already know how to fix using concepts we have not learned yet.

## Final assessment

After the quiz, provide:

### Learning Checkpoint Result

Rate my understanding as one of:

* `Strong`
* `Developing`
* `Needs Review`

### Concepts I Understand

Briefly list the concepts I demonstrated clearly.

### Concepts to Reinforce

List anything I should review before moving on.

### Milestone Takeaway

Give me the 2–4 most important things I should remember from this milestone.

### Interview Readiness

Give me 1–3 questions an interviewer might ask about the concepts learned in this milestone and a concise description of what a strong answer should cover.

### Recommendation

Choose one:

* `Ready for PR Review`
* `Review These Concepts First`

Do not start another milestone.

Do not modify code or documentation.
