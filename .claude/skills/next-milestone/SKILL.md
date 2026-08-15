---
name: next-milestone
# next-milestone
description: Use this skill when the user wants to plan, design, or prepare the next PokéJudge milestone before coding, including requests like "plan the next milestone", "what's next", or "let's plan the next milestone".
---

Read `PRD.md` and identify the next milestone in the roadmap.

Before writing or modifying any application code:

1. Create an implementation and learning plan for that milestone.
2. Include:
   - What we will build
   - AI concepts being learned
   - Steps in implementation order
   - Expected limitations or failures we should intentionally observe
   - What I should understand by the end of the milestone
3. Save the plan to `.project-plans/milestone-<N>/plan.md`, where `<N>` is the milestone number (e.g. `.project-plans/milestone-1/plan.md`). Create the `milestone-<N>` folder if it does not already exist. All files related to this milestone (the plan, and later the implementation summary and review) live together in this one folder.
4. `.project-plans/` is committed to source control, alongside `docs/` and the application code — these documents are part of the project's learning record, not scratch notes.
5. Do not implement anything yet.

Summarize the plan for me and stop for my approval before making any code changes.