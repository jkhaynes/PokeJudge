---

name: implement-milestone
description: Implement the approved PokéJudge milestone plan
------------------------------------------------------------

Read `PRD.md` and the approved milestone plan in `.project-plans/`.

Implement the milestone according to the approved plan.

Requirements:

1. Treat the approved milestone plan as the implementation scope. Do not add unrelated features or move ahead to future milestones.
2. Preserve the project's learning-first philosophy:

   * Prefer simple, understandable implementations.
   * Do not hide the AI concept being learned behind unnecessary frameworks or abstractions.
   * Preserve intentional limitations or experiments called out in the plan.
3. Before making changes, briefly summarize:

   * What you are about to implement
   * Which files you expect to create or modify
4. Implement the milestone incrementally.
5. Add appropriate tests for deterministic application behavior where useful.
6. Do not silently change the approved design. If implementation reveals a meaningful design problem, stop and explain the issue before making that architectural change.
7. Do not implement functionality belonging to later milestones.
8. Keep `.project-plans/` out of source control.

After implementation:

* Run the relevant build/tests.
* Fix implementation errors that are within the approved scope.
* Summarize what changed.
* Identify any intentional limitations we should observe or experiment with.
* Explain the key AI concepts demonstrated by the implementation.
* Tell me what I should manually try or inspect before considering the milestone complete.

Do not begin the next milestone.
