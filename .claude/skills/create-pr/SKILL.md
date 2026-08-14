---
name: create-pr
description: Create a polished pull request for the completed PokéJudge milestone
---

Create a pull request for the current completed and reviewed PokéJudge milestone.

Invoking this skill means the implementation and PR review have been accepted by the user.

Do not modify application code, tests, documentation, or the milestone plan.

Do not merge the pull request.

## Context

Read:

1. `docs/PRD.md`
2. The current milestone plan in `.project-plans/`
3. The current git branch and commit history
4. The diff between the current branch and its target branch
5. Relevant build/test results when available

Use the actual implementation and approved milestone plan as the source of truth.

Do not claim work was completed unless it exists in the branch.

## Pre-flight checks

Before creating the PR:

1. Confirm the current branch is not `main` or `master`.
2. Check for uncommitted changes.
3. If meaningful uncommitted changes exist, stop and tell me what remains uncommitted. Do not commit them automatically.
4. Confirm the branch contains commits not present on the target branch.
5. Determine the appropriate target branch, normally `main`.
6. Check whether the branch has been pushed to the remote.

If the branch only needs to be pushed before creating the PR, you may push it.

Do not rewrite history, force push, rebase, or squash commits automatically.

## Pull request title

Create a concise title that describes the milestone outcome.

Prefer titles such as:

```text
Milestone 1: Add initial LLM interaction
```

or:

```text
Add structured judge clarification workflow
```

Avoid vague titles such as:

```text
Updates
Changes
AI work
Milestone stuff
```

## Pull request description

Write the PR description so another engineer — or a recruiter reviewing the repository — can quickly understand both:

1. What was built
2. What was learned

Use this structure:

### Summary

Briefly explain the purpose of this milestone and what the PR introduces.

### What Changed

Use concise bullets describing the actual implementation.

Do not list every file.

Focus on meaningful behavior or architecture.

### Learning Objective

Explain the AI engineering concept this milestone was designed to teach.

Include why this implementation exists at this point in the project.

### What I Observed

Summarize important behaviors, limitations, or failure modes intentionally observed during the milestone.

Examples may include:

- Hallucination
- Reliance on pretrained knowledge
- Prompt sensitivity
- Structured-output behavior
- Poor retrieval
- Chunking tradeoffs
- Grounding limitations
- Model uncertainty

Only include observations actually relevant to this milestone.

### Intentional Limitations

Document limitations intentionally left unresolved because later milestones are designed to address them.

Do not present these as accidental defects.

When useful, briefly identify which future concept is expected to address them.

### Validation

Include the relevant validation performed, such as:

- Build status
- Automated tests
- Manual AI experiments
- Evaluation runs

Do not claim validation that was not actually performed.

### Next Step

Briefly identify the next milestone or AI concept from `docs/PRD.md`.

Do not describe future implementation in unnecessary detail.

## Learning-first framing

The PR should make the project's incremental learning progression visible.

Where appropriate, communicate:

```text
What we built
      ↓
What limitation we observed
      ↓
Why that limitation matters
      ↓
What future AI concept will address it
```

Avoid making an intentionally early-stage milestone sound more production-ready than it is.

For example, if the current implementation uses an ungrounded LLM intentionally, say so.

That is part of the learning story, not something to hide.

## Keep the PR professional

The PR should:

- Be concise
- Be technically accurate
- Avoid marketing language
- Avoid exaggerated claims about AI capabilities
- Avoid unnecessary implementation detail
- Clearly distinguish implemented behavior from future plans
- Use terminology consistent with `docs/PRD.md`

Write it as an engineer documenting their work for other engineers.

## Create the PR

If GitHub CLI is installed and authenticated:

1. Push the current branch if necessary.
2. Create the pull request using the generated title and description.
3. Target the appropriate base branch.
4. Do not merge it.

If GitHub CLI is unavailable or authentication prevents PR creation:

1. Do not attempt alternate unsafe workarounds.
2. Provide the finalized PR title and description.
3. Tell me what prevented automatic creation.

## After creation

Report:

- PR title
- Source branch
- Target branch
- PR URL, if successfully created
- Whether the branch was pushed
- Any issues encountered

Do not begin another milestone.

Do not merge the PR.

Do not modify files after creating the PR.