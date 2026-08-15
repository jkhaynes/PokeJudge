---

name: implement-milestone
description: Use this skill when the user wants to start, begin, implement, build, or work on the next/current approved PokéJudge milestone, including requests like "let's do the next milestone", "start the next milestone", "implement this milestone", or "let's build it".
------------------------------------------------------------

Read `docs/PRD.md` and the current milestone plan at `.project-plans/milestone-<N>/plan.md`.

Invoking this skill means the current milestone plan has been reviewed and approved by the user.

Implement the milestone according to that approved plan.

Do not expand the scope beyond the approved milestone.

## Branch setup

Before modifying any application files:

1. Check the current git status and branch.

2. If there are meaningful uncommitted changes that do not belong to the current milestone, stop and tell me before proceeding.

3. If currently on `main` or `master`, create and switch to a new branch for this milestone.

4. Derive a short, descriptive branch name from the milestone using:

   `milestone/<number>-<short-description>`

   Example:

   `milestone/1-first-llm-interaction`

5. If already on an appropriate branch for the current milestone, continue using it.

6. If already on a different feature or milestone branch that does not appear related to the current plan, stop and tell me rather than mixing work.

7. Do not delete, rename, merge, rebase, or force-push branches automatically.

8. Confirm the branch name before beginning implementation.

All milestone implementation work should occur on this branch so it can later be reviewed with `/review-pr` and submitted using `/create-pr`.

## Before implementation

Before making code changes, briefly summarize:

* Which milestone you are implementing
* What you are about to build
* The primary AI concept being learned
* Which files you expect to create or modify
* Any intentional limitation or experiment that must remain observable
* Which pieces of the planned work are deterministic, non-LLM logic (see "What counts as testable" below) — these are what you'll write tests for first

Then begin implementation.

Do not ask for another approval after this summary. Invoking this skill is the approval to implement the existing plan.

## Test-driven implementation flow

This project follows test-first development for anything that qualifies as deterministic logic. Follow red → green, then close with a deliberate coverage check:

### What counts as testable

The line: given the same input, does this specific piece of logic always produce the same output, or does its output depend on what a model returns at call time?

* **If deterministic** — it gets a unit test, including any behavior the plan expects to be flawed, partial, or incomplete at this stage. What the logic is *for* doesn't change this — only whether its own output varies with live model behavior.
* **If not deterministic** — its result depends on an actual model call, or on anything else that can vary between runs — it is not unit-testable. It belongs to manual experimentation or a later milestone's evaluation harness, not a unit test.

### 1. Red — write the tests first

Before writing the implementation:

1. From the milestone plan, list every piece of deterministic logic the plan calls for (see criteria above), including anything the plan expects to behave imperfectly or incorrectly at this stage.
2. Write unit tests for that logic based on what the plan says it should do — and, where the plan calls out expected imperfect behavior, what it should (incorrectly) do instead.
3. Confirm the tests fail (or fail to compile) because the implementation doesn't exist yet — this is expected at this point, not an error to fix.

Do not write tests for non-deterministic logic at this stage — there's nothing fixed to assert yet.

### 2. Green — implement to make the tests pass

Write the implementation following the rest of this document's requirements (scope, architecture, learning-first philosophy, etc. below), aiming to make the tests written in step 1 pass without weakening or rewriting them to fit whatever you happened to build. If a test describing an expected imperfection stops failing because the implementation turned out more capable than the plan called for, that's a scope signal — see "Do not silently redesign the milestone" below, not a reason to loosen the test.

Then implement everything else the milestone needs that isn't unit-testable.

### 3. Final test-coverage review

After the implementation otherwise works end-to-end, do one more pass across the actual diff — not just the original plan — and ask: did anything deterministic emerge during implementation that wasn't anticipated when the tests were written first? This can happen when real-world details only become visible once the implementation exists — an unanticipated edge case, an error-handling branch added while wiring things together, a helper that wasn't in the plan but turned out to be needed.

Add tests for anything found in this pass. This step is a deliberate gap-check on top of writing tests first, not a substitute for it — the goal is that test-writing happens twice: once from the plan before code exists, and once against the real diff before the milestone is called done.

## Implementation requirements

### 1. Follow the approved plan

Treat the milestone plan as the implementation scope.

Implement the planned work in the intended order where practical.

Do not:

* Add unrelated features
* Implement functionality belonging to later milestones
* Expand the architecture simply because future milestones may need it
* Quietly change the learning objective

### 2. Preserve the learning-first philosophy

This project exists to learn how AI applications are built, not merely to produce working code.

Prefer:

* Simple, understandable implementations
* Direct exposure to the AI concept being learned
* Small incremental changes
* Code that can be traced and explained
* Intentional experiments called out in the milestone plan

Avoid:

* Hiding the concept behind unnecessary frameworks
* Premature abstractions
* Excessive indirection
* Enterprise architecture that does not serve the current milestone
* Automatically solving limitations that are intentionally meant to remain observable until a later milestone

A temporary limitation is not automatically a defect if the milestone intentionally exists to demonstrate that limitation.

Use this learning loop:

`Build → Observe → Understand → Improve`

### 3. Keep architecture proportional to the milestone

Follow the modular-monolith direction in `docs/PRD.md`.

Do not create:

* Microservices
* Separate deployment units
* Multiple class-library projects
* Provider factories
* Message buses
* Infrastructure layers
* Large abstractions

unless the approved milestone plan specifically requires them.

A conceptual responsibility does not automatically need its own project, interface, or service.

### 4. Do not silently redesign the milestone

If implementation reveals a meaningful problem with the approved design:

1. Stop before making the architectural change.
2. Explain:

   * What the issue is
   * Why the approved design causes it
   * What change you recommend
   * Whether the change affects the milestone's learning objective
3. Wait for user direction before making that architectural change.

Small implementation details that do not materially alter the approved design do not require another approval.

### 5. Keep AI behavior understandable

For AI-related code, make the important behavior easy to inspect.

Depending on the milestone, preserve visibility into things such as:

* System instructions
* User messages
* Context supplied to the model
* Structured outputs
* Model responses
* Token/context behavior
* Embeddings
* Retrieved chunks
* Similarity results
* Application-owned state versus model-generated output

Do not obscure these behind abstractions when seeing them directly is part of the milestone's learning objective.

### 6. Preserve intentional limitations

Read the milestone plan's expected limitations or failure modes before implementation.

Do not accidentally remove them.

Examples may include:

* Hallucination
* Reliance on pretrained knowledge
* Lack of grounding
* Missing citations
* Prompt sensitivity
* Poor retrieval behavior
* Chunking tradeoffs
* Model uncertainty

If a limitation is intentionally part of the milestone, keep it observable so it can be explored during `/learning-checkpoint`.

### 7. Tests

Tests for deterministic logic are written first, per "Test-driven implementation flow" above — not as a final checklist item. This section is just the reminder of the boundary: unit tests cover deterministic, non-LLM logic only; do not create misleading unit tests that imply probabilistic LLM behavior has been proven correct. Manual AI experiments, integration tests, and evaluation tests remain the right tool for anything whose output depends on live model behavior, based on what the current milestone has introduced.

### 8. Secrets and configuration

Do not commit credentials, API keys, secrets, or sensitive local configuration.

Use the project's intended local secret-management approach.

Before finishing, verify that no secret-bearing files have accidentally become tracked or staged.

### 9. Planning documents are part of the repository

`.project-plans/` is committed to source control, alongside the application code — these are the project's learning record, not scratch notes. Do not treat writes to `.project-plans/` as something to hide from git status; they are expected to eventually be staged and committed like any other project file. This skill itself does not stage or commit anything (see "Stop condition" below) — that remains a separate, explicit step.

## Validation

After implementation:

1. Build the relevant solution/project.
2. Run the automated tests and confirm the tests written in the Red step now pass (green), unchanged in intent from when they were written.
3. Fix implementation errors that are within the approved milestone scope.
4. Re-run validation after fixes.
5. Do the "Final test-coverage review" pass described above and add any tests it surfaces.
6. Inspect git status to confirm:

   * Expected implementation files changed
   * No secrets are staged
   * No unrelated files were modified unintentionally

Do not implement future features while fixing current milestone issues.

## Completion summary

When implementation and validation are complete, write the full completion summary to a document instead of printing it all to the console — it is too long to read comfortably as chat output.

1. Write the summary to `.project-plans/milestone-<N>/implementation-summary.md`, alongside that milestone's `plan.md`.
2. Write that file with the following sections:

### Milestone Implemented

State the milestone name and number.

### What Changed

Briefly summarize the meaningful implementation changes.

### Validation

Report:

* Build result
* Test result, noting which tests were written first (Red step) versus added during the final coverage-review pass
* Any other validation performed

Do not claim validation that was not actually run.

### Intentional Limitations

List the limitations or failure modes that remain intentionally present.

Explain briefly why each is being left for a later milestone.

### Learning Focus

Explain the key AI concepts demonstrated by this implementation.

Focus on what I should understand, not just what the code does.

### What I Should Try

Give me a small set of specific manual experiments or things to inspect before considering the milestone complete.

Prefer experiments that expose:

* Expected model behavior
* Important implementation details
* Intentional weaknesses
* The reason a later milestone will be needed

### Git Status

Report:

* Current branch
* Whether implementation changes remain uncommitted
* Whether any unexpected files are present

Do not commit or push automatically unless the approved milestone plan explicitly instructs you to do so.

3. This summary file lives in `.project-plans/`, which is committed to the repository — but this skill does not stage or commit it itself; that remains a separate, explicit step.
4. After writing the file, reply in the console with only a short pointer, not the full content: the milestone name, the file path, and 2-3 sentences on the single most important thing to know (e.g. whether build/tests passed, and the one finding most worth their attention). Tell me to open the file for the full report.

## Stop condition

After providing the completion summary, stop.

Do not:

* Begin the next milestone
* Run `/review-milestone` automatically
* Run `/learning-checkpoint` automatically
* Create a pull request
* Merge anything
* Implement future functionality

The user controls progression to the next workflow stage.

