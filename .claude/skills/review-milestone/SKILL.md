---

name: review-milestone
description: Use this skill when the user wants to review, validate, or check the milestone that was just implemented against its plan and learning objectives.
------------------------------------------------------------------------------------------------------

Review the milestone that was just implemented.

This is a **review only**. Do not modify application code, tests, documentation, or the milestone plan.

## Context

Read:

1. `docs/PRD.md` as the source of truth for the overall project and milestone roadmap.
2. The current milestone plan at `.project-plans/milestone-<N>/plan.md`.
3. The implementation associated with that milestone.
4. Relevant git changes so you understand what was actually added or modified.

## Review goals

### 1. Scope adherence

Compare the implementation against the approved milestone plan.

Check for:

* Missing planned work
* Unplanned features
* Functionality belonging to future milestones
* Unnecessary abstractions or complexity
* Architectural changes that were not part of the plan

Do not criticize an intentionally simplified implementation merely because a later milestone will improve it.

### 2. Correctness

Review the implementation for:

* Bugs
* Incorrect assumptions
* Error-handling problems
* Incorrect async behavior
* Misuse of libraries or APIs
* Security or secret-management problems
* Tests that do not actually validate the intended behavior

Run the relevant build and tests where appropriate.

Do not fix failures during this skill. Report them.

### 3. AI engineering correctness

Review any AI-related implementation for issues relevant to the current milestone, such as:

* Prompt construction
* System vs. user instructions
* Context supplied to the model
* Structured outputs
* Model assumptions
* Grounding
* Retrieval
* Embeddings
* State management
* Evaluation

Only evaluate concepts that have actually been introduced by this milestone.

Do not require capabilities intentionally deferred to future milestones.

### 4. Learning objective

This project exists to learn how AI applications are built, not merely to produce working code.

Determine whether the implementation actually exposes the AI concept this milestone was intended to teach.

Check:

* Is the important AI behavior visible and understandable?
* Has unnecessary abstraction hidden the concept being learned?
* Were intentional experiments or limitations from the plan preserved?
* Can the developer observe the failure modes the milestone was designed to demonstrate?
* Did Claude accidentally "solve" a limitation that was intentionally supposed to remain until a later milestone?

Treat intentional limitations as **learning experiments**, not defects.

### 5. Expected limitations

Compare the implementation with the milestone plan's expected limitations or failures.

For each expected limitation, determine whether it is:

* Present and observable
* Accidentally hidden or eliminated
* More severe than expected
* Different from what the plan predicted

These observations are important because later milestones should be motivated by limitations we actually experienced.

### 6. Code quality

Evaluate normal software-engineering quality at the level appropriate for this milestone:

* Readability
* Naming
* Separation of concerns
* Duplication
* Testability
* Appropriate use of .NET conventions

Prefer simple code.

Do not recommend abstractions, patterns, additional projects, or infrastructure merely for architectural cleanliness.

## Output

Write the review to a document instead of printing it all to the console — it is too long to read comfortably as chat output.

1. Write the review to `.project-plans/milestone-<N>/review.md`, alongside that milestone's `plan.md` and `implementation-summary.md`. This file is the one designated exception to "do not modify files" below — it is the skill's own output artifact, not application code, tests, documentation, or the milestone plan. It lives in `.project-plans/`, so it stays local and untracked like the plan itself; do not stage or commit it.
2. If a review file already exists for this milestone (e.g. from a prior run), overwrite it with the current review rather than appending — the file should always reflect the latest review, not a history of past ones.
3. Write that file with the following sections:

### Milestone Review

State which milestone and plan were reviewed.

### ✅ Matches the Plan

Briefly identify what was implemented correctly and as intended.

### 🚨 Must Fix

List issues that should be addressed before the milestone is considered complete.

For each include:

* File/location
* Problem
* Why it matters
* Recommended direction

Do not implement the fix.

### ⚠️ Consider Improving

List worthwhile improvements that are not required to complete the milestone.

Avoid speculative cleanup.

### 🧪 Learning Observations

Identify the important behaviors, limitations, or failures the developer should manually observe.

Explain why each matters to understanding the AI concept being learned.

### 🎯 Learning Objective Check

Answer:

1. What AI concept was this milestone intended to teach?
2. Does the implementation expose that concept clearly?
3. What should the developer be able to explain after completing this milestone?
4. Is any abstraction hiding something the developer should understand directly?

### 📋 Plan Completion

Classify each planned implementation step as:

* Complete
* Partially complete
* Missing
* Intentionally deferred

### Final Verdict

Choose exactly one:

* `Ready to Complete`
* `Ready After Minor Fixes`
* `Needs Revision`

Then give a concise reason.

4. After writing the file, reply in the console with only a short pointer, not the full content: the milestone name, the file path, and the Final Verdict with its one-line reason. Tell me to open the file for the full review.

Do not begin the next milestone.

Do not modify files other than the review document specified above.

Do not automatically fix issues discovered during the review.
