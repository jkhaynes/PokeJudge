---

name: implement-milestone
description: Implement the approved PokéJudge milestone plan
------------------------------------------------------------

Read `docs/PRD.md` and the current milestone plan in `.project-plans/`.

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

Then begin implementation.

Do not ask for another approval after this summary. Invoking this skill is the approval to implement the existing plan.

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

Add appropriate tests for deterministic application behavior where useful.

Do not create misleading unit tests that imply probabilistic LLM behavior has been proven correct.

Distinguish appropriately between:

* Unit tests
* Integration tests
* Manual AI experiments
* Evaluation tests

based on what the current milestone has introduced.

### 8. Secrets and configuration

Do not commit credentials, API keys, secrets, or sensitive local configuration.

Use the project's intended local secret-management approach.

Before finishing, verify that no secret-bearing files have accidentally become tracked or staged.

### 9. Keep local planning files out of source control

`.project-plans/` must remain local.

Do not stage or commit files from `.project-plans/`.

Do not add milestone plans to the repository.

## Validation

After implementation:

1. Build the relevant solution/project.
2. Run the relevant automated tests.
3. Fix implementation errors that are within the approved milestone scope.
4. Re-run validation after fixes.
5. Inspect git status to confirm:

   * Expected implementation files changed
   * No secrets are staged
   * `.project-plans/` remains untracked/excluded
   * No unrelated files were modified unintentionally

Do not implement future features while fixing current milestone issues.

## Completion summary

When implementation and validation are complete, report:

### Milestone Implemented

State the milestone name and number.

### What Changed

Briefly summarize the meaningful implementation changes.

### Validation

Report:

* Build result
* Test result
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

