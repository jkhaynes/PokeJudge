# AI-Assisted Development Workflow

PokéJudge AI uses the [Superpowers](https://github.com/obra/superpowers) Claude Code skills, plus one custom skill, to create a structured, human-controlled AI development workflow.

The goal is **not** to have an AI agent autonomously build the project. Claude assists with design, planning, implementation, review, and documentation, while I remain responsible for approving designs and understanding the AI concepts being introduced.

The workflow is:

```text
superpowers:brainstorming            → design spec, approved by me
      ↓
superpowers:writing-plans            → step-by-step implementation plan
      ↓
superpowers:subagent-driven-development (or executing-plans), test-first
      ↓
superpowers:requesting-code-review
      ↓
/learning-checkpoint                 → custom skill, quiz + transcript
      ↓
superpowers:finishing-a-development-branch  → PR
```

Each stage ends at a checkpoint where I approve before Claude continues.

The project follows a learning-first loop:

```text
Build → Observe → Understand → Improve
```

Early milestones may intentionally expose limitations such as hallucination, reliance on pretrained knowledge, poor retrieval, or ungrounded answers. Later milestones introduce the AI techniques that address those problems.

---

## History

Milestones 1 to 8.5 used six custom skills (`/next-milestone`, `/implement-milestone`, `/review-milestone`, `/learning-checkpoint`, `/review-pr`, `/create-pr`). Their plans, implementation summaries, reviews, and learning-checkpoint transcripts remain in `.project-plans/milestone-<N>/` as a read-only archive. All but `/learning-checkpoint` were retired in favor of Superpowers, which covers the same ground: brainstorming and writing-plans replace planning, test-driven subagent development replaces implementation, and code review and branch finishing replace the review and PR skills.

---

# Workflow

## Brainstorming

Claude asks clarifying questions one at a time, proposes approaches with trade-offs, and presents a design section by section for my approval. The approved design is saved to `docs/superpowers/specs/YYYY-MM-DD-<topic>-design.md`.

**Purpose:** Prevent the coding agent from making design decisions and immediately implementing them without human review.

## Writing plans

Turns the approved spec into small, test-first tasks with exact files and verification steps, saved to `docs/superpowers/plans/YYYY-MM-DD-<topic>.md`.

## Implementation

Each task is implemented test-first (red, green, refactor), with a review between tasks. Scope stays within the approved plan, and intentional learning limitations are preserved.

**Purpose:** Use AI to accelerate implementation without allowing it to redesign the project or hide the concept being learned.

## Code review

A reviewer checks the finished work against the plan and for correctness, security, maintainability, tests, and unnecessary complexity before it merges.

**Purpose:** Answer:

> Is this change actually ready to merge?

## `/learning-checkpoint` (custom)

Runs an interactive quiz based on the work and its actual implementation. Claude asks questions one at a time about:

* What the model is doing
* What the application is doing
* Why the implementation works
* What assumptions exist
* What limitations remain
* Why the next AI technique is needed

The transcript and assessment are saved to `docs/superpowers/checkpoints/`.

**Purpose:** Answer:

> Can I explain what we just built and why it works?

## Finishing the branch

Verifies tests pass, then opens the pull request. Each PR documents what changed, what was learned, the intentional limitations, and the validation performed, so the project's evolution stays visible.

---

# Learning-First Philosophy

PokéJudge intentionally does not begin with its final RAG architecture.

Instead, each stage should expose a problem that motivates the next concept.

For example:

```text
Raw LLM
   ↓
Observe hallucination / unsupported knowledge
   ↓
Provide external context
   ↓
Learn why grounding matters
   ↓
Build retrieval
   ↓
Learn retrieval quality matters
   ↓
Build RAG
   ↓
Add evaluation and reliability work
```

Some temporary implementations are therefore intentional.

The distinction is:

**Useful rework:**
Build something simple, observe why it fails, then understand the technique that improves it.

**Wasteful rework:**
Build unnecessary architecture that is later replaced without teaching anything useful.

The workflow is designed to encourage the first and prevent the second.

---

# Repository Structure

```text
.claude/
└── skills/
    └── learning-checkpoint/

docs/
├── PRD.md
├── prompt-engineering.md
├── reviews/
└── superpowers/
    ├── specs/         (approved designs)
    ├── plans/         (implementation plans)
    └── checkpoints/   (learning-checkpoint transcripts)

.project-plans/        (archive: milestones 1 to 8.5)
```

Specs, plans, and checkpoint transcripts are all committed, so the full planning, implementation, review, and learning record is visible alongside the code.

---

## Goal

The overall principle behind the workflow is simple:

> **Use AI to accelerate development without outsourcing technical understanding.**
