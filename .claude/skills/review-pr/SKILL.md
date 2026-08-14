---
name: review-pr
description: Review the current PokéJudge branch or diff before creating a pull request
---

Review the current code changes as if you are a senior engineer performing a pull request review before merge.

This is a **review-only** step.

Do not modify code, tests, documentation, configuration, or project files.

## Context

Read:

1. `docs/PRD.md`
2. The current milestone plan in `.project-plans/`, when relevant
3. The current git branch and diff
4. Any tests associated with the changed code
5. Relevant surrounding code needed to understand the impact of the changes

Focus primarily on what changed in the current branch.

Do not perform a broad rewrite review of the entire repository unless a change directly affects existing code.

## Review priorities

Review in this order:

1. Correctness
2. Safety
3. Scope
4. Maintainability
5. Test coverage
6. AI-specific risks
7. Style

Do not prioritize stylistic preferences over functional issues.

## 1. Correctness

Look for:

- Bugs
- Incorrect assumptions
- Broken control flow
- Edge cases
- Null handling problems
- Incorrect async behavior
- Resource-management issues
- Incorrect library/API usage
- State-management bugs
- Error-handling gaps

Focus on issues that could cause incorrect behavior, not theoretical possibilities with little practical impact.

## 2. Security and secrets

Check for:

- API keys or credentials committed to source
- Secrets accidentally logged
- Sensitive configuration
- Unsafe handling of user-controlled content
- Prompt injection concerns where applicable
- Unsafe assumptions about retrieved AI context
- Excessive information in errors/logging

Only apply AI-security concerns that are relevant to the capabilities currently implemented.

Do not criticize the code for lacking defenses belonging to future milestones.

## 3. Scope and architecture

Check whether the changes:

- Stay within the approved milestone scope
- Introduce functionality belonging to future milestones
- Add unnecessary abstractions
- Add unnecessary dependencies
- Create architectural complexity without a concrete need
- Conflict with the modular-monolith approach in the PRD
- Quietly change an approved design decision

Do not penalize intentionally simple code for not resembling the final architecture.

## 4. AI-specific review

For AI-related changes, review only concepts currently introduced by the project.

Depending on the milestone, inspect things such as:

- System instructions vs. user content
- Prompt construction
- Context passed to the model
- Structured output schemas
- Model-generated vs. application-owned state
- Assumptions the model is allowed to make
- Retrieval logic
- Chunking
- Embeddings
- Grounding
- Citations
- Source Support
- Evaluation behavior

Ask:

> Could the implementation appear to work while actually relying on behavior we did not intend?

Call out hidden dependencies on model behavior where relevant.

## 5. Tests

Review whether tests:

- Cover important deterministic behavior introduced by the changes
- Test meaningful behavior rather than implementation details
- Include relevant failure/edge cases
- Would actually fail if the underlying feature broke

Do not require tests for every trivial line.

For probabilistic AI behavior, do not pretend ordinary unit tests can prove model correctness.

Distinguish between:

- deterministic unit/integration tests
- manual AI experiments
- evaluation tests

based on the current milestone.

## 6. Learning-first constraints

This project intentionally exposes some limitations before later milestones solve them.

Do not flag an intentional limitation as a defect solely because a future AI technique would improve it.

Instead, ask:

- Is the limitation intentional?
- Is it documented in the milestone plan?
- Is it safe within the current development scope?
- Does it preserve the learning objective?

If yes, classify it as an expected limitation rather than a PR defect.

## 7. Run validation

Where appropriate:

- Build the solution
- Run relevant tests
- Inspect failures

Do not fix failures.

Report them as review findings.

## Severity

Classify findings as:

### Blocker

Must be fixed before merge because it causes incorrect behavior, security risk, data loss, broken build/tests, or violates a core project requirement.

### Major

Should be fixed before merge because it creates a meaningful correctness, maintainability, architectural, or AI-behavior problem.

### Minor

Worth improving but not necessarily merge-blocking.

### Note

Observation, question, or optional improvement.

Avoid inflating severity.

## Output format

### PR Review Summary

Briefly state:

- What changed
- Overall impression
- Build/test status

### 🚫 Blockers

List merge-blocking issues.

For each include:

- File and location
- Problem
- Why it matters
- Recommended direction

If none, say `None`.

### ⚠️ Major Issues

Use the same format.

If none, say `None`.

### 🔎 Minor Issues

Keep these concise.

If none, say `None`.

### 💬 Review Notes

Optional observations, questions, or non-blocking suggestions.

### 🤖 AI-Specific Review

Summarize whether the AI-related implementation behaves consistently with the current milestone and PRD.

Explicitly mention any hidden reliance on model behavior, ungrounded assumptions, or AI-specific risks that matter now.

### 🧪 Test Review

Summarize:

- Existing coverage
- Missing important coverage
- Build/test results
- Whether additional manual AI experiments are appropriate

### 📦 Scope Check

Answer:

- Did the branch stay within milestone scope?
- Did it implement anything from future milestones?
- Did it introduce unnecessary architecture or dependencies?

### Final Verdict

Choose exactly one:

- `Approve`
- `Approve With Minor Comments`
- `Request Changes`

Give a concise explanation.

Do not modify files.

Do not create the pull request.

Do not implement review suggestions.

Do not begin another milestone.