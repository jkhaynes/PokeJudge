# AI-Assisted Development Workflow

PokéJudge AI uses a set of custom Claude Code skills to create a structured, human-controlled AI development workflow.

The goal is **not** to have an AI agent autonomously build the project. Claude assists with planning, implementation, review, and documentation, while I remain responsible for approving designs and understanding the AI concepts being introduced.

The workflow is:

```text
/next-milestone
      ↓
Review the plan
      ↓
/implement-milestone
      ↓
/review-milestone
      ↓
/learning-checkpoint
      ↓
/review-pr
      ↓
/create-pr
```

Each command acts as a deliberate checkpoint. Claude cannot automatically progress through the entire development process.

The project follows a learning-first loop:

```text
Build → Observe → Understand → Improve
```

Early milestones may intentionally expose limitations such as hallucination, reliance on pretrained knowledge, poor retrieval, or ungrounded answers. Later milestones introduce the AI techniques that address those problems.

---

## Why Custom Skills?

The skills live under:

```text
.claude/skills/
```

and are committed to the repository so the AI-assisted development process is visible alongside the application code.

They are designed around a few core principles:

* Plan before coding
* Keep AI changes within an approved scope
* Separate implementation from review
* Preserve intentional learning experiments
* Verify that I understand the code before moving forward
* Document both what was built and what was learned

Generated milestone plans, along with their implementation summaries, reviews, and learning-checkpoint transcripts, are stored in `.project-plans/` and are committed to the repository — the full planning and learning record stays visible alongside the code, for learning and portfolio purposes.

---

# Workflow

## `/next-milestone`

Creates a plan for the next milestone based on `docs/PRD.md`.

The plan includes:

* What will be built
* AI concepts being introduced
* Implementation steps
* Expected limitations or failures
* What I should understand afterward

Claude creates the plan and stops. I review it before invoking the implementation skill.

**Purpose:** Prevent the coding agent from making design decisions and immediately implementing them without human review.

---

## `/implement-milestone`

Implements the reviewed milestone plan.

The skill is instructed to:

* Stay within milestone scope
* Avoid future features
* Prefer simple, understandable implementations
* Preserve intentional learning limitations
* Avoid unnecessary frameworks and abstractions
* Stop before making meaningful unapproved design changes

After implementation it builds/tests the project and identifies behaviors I should manually explore.

**Purpose:** Use AI to accelerate implementation without allowing it to redesign the project or hide the concept being learned.

---

## `/review-milestone`

Reviews the completed milestone against:

* The milestone plan
* The PRD
* The learning objectives
* The implementation

It checks correctness, scope, code quality, AI behavior, tests, and whether expected limitations remain observable.

It does not modify code.

**Purpose:** Answer:

> Did we build what this milestone was actually supposed to teach and accomplish?

---

## `/learning-checkpoint`

Runs an interactive quiz based on the milestone and its actual implementation.

Claude asks questions one at a time about topics such as:

* What the model is doing
* What the application is doing
* Why the implementation works
* What assumptions exist
* What limitations remain
* Why the next AI technique is needed

The goal is understanding rather than memorization.

**Purpose:** Answer:

> Can I explain what we just built and why it works?

---

## `/review-pr`

Performs a senior-engineer-style review of the current branch before a pull request is created.

It reviews:

* Correctness
* Security
* Scope
* Maintainability
* Tests
* AI-specific risks
* Unnecessary complexity

Findings are classified as Blocker, Major, Minor, or Note.

The skill reports issues but does not fix them.

**Purpose:** Answer:

> Is this change actually ready to merge?

---

## `/create-pr`

Creates the GitHub pull request after the implementation and reviews are accepted.

Each PR documents:

* What changed
* The milestone's learning objective
* What behavior was observed
* Intentional limitations
* Validation performed
* What comes next

This makes the project's evolution visible instead of showing only the final architecture.

**Purpose:** Create a technical record of both the engineering work and the learning progression.

---

# Human Approval Model

The workflow uses command invocation as the approval boundary.

```text
Claude creates plan
      ↓
STOP
      ↓
I review it
      ↓
I run /implement-milestone
      ↓
Claude implements
      ↓
STOP
      ↓
I choose when to continue through review and PR steps
```

Claude does not automatically move from planning to implementation or from implementation to merge.

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

The skills are designed to encourage the first and prevent the second.

---

# Repository Structure

```text
.claude/
└── skills/
    ├── next-milestone/
    ├── implement-milestone/
    ├── review-milestone/
    ├── learning-checkpoint/
    ├── review-pr/
    └── create-pr/

docs/
├── PRD.md
├── prompt-engineering.md
└── reviews/

.project-plans/
└── milestone-<N>/
    ├── plan.md
    ├── implementation-summary.md
    ├── review.md
    ├── pr-review.md
    ├── learning-checkpoint.md
    └── ... (ad hoc supporting docs, e.g. baseline-run-output.md)
```

The reusable Claude skills and the `.project-plans/` milestone documents are both committed to GitHub, so the full planning, implementation, review, and learning record is visible alongside the code.

---

## Goal

The overall principle behind the workflow is simple:

> **Use AI to accelerate development without outsourcing technical understanding.**
