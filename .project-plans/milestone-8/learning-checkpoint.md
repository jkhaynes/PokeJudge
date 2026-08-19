# Milestone 8 — Learning Checkpoint

## Q&A Transcript

### 1. The `special-condition` divergence and destination vs. trajectory evaluation

**Question:** The `observed-limitations.md` §3 "special-condition divergence" is called out in the review as
the single best piece of evidence this milestone produced. In your own words: what happened in that run, and
why does it demonstrate the difference between "destination" evaluation and "trajectory" evaluation? What
would a destination-only eval harness have concluded about that run, and why would that conclusion have been
misleading?

**Answer (verbatim):** "In the special-condition run, PokéJudge unexpectedly treated the scenario as
sufficient immediately and asked no clarification, even though every prior observation and the eval scenario
expected one clarification round. It still produced a final ruling with Strong validated Source Support, so a
destination-only harness would have marked it as a pass. Trajectory evaluation exposed that the process was
still wrong: sufficiency timing, clarifying-question materiality, and post-answer retrieval all failed because
the expected investigation never happened. That makes destination-only scoring misleading because a correct
final answer can hide an unstable or incorrect investigation path."

**Feedback:** Correct. A clean, complete answer covering all three required pieces (what happened, what a
destination-only harness would conclude, why that conclusion is misleading). Added nuance: PRD §15's framing
is that a correct ruling via a *wrong* path shouldn't get full credit; this run is the sharper version —
*no* investigation at all where one was structurally expected — and it's notable because it emerged from
real run-to-run non-determinism, not a scripted demonstration. Memory trick given: "Right answer, wrong
reasons" — a trajectory eval catches the case a destination eval structurally cannot.

### 2. Why `ScenarioEvalRunner` catches `InvalidOperationException` specifically

**Question:** Look at `ScenarioEvalRunner.RunAsync` (`Evaluation/ScenarioEvalRunner.cs`). It wraps the call to
`_loop.RunAsync(...)` in a `try/catch (InvalidOperationException ex)`. Why does it catch that *specific*
exception type rather than a broader `catch (Exception ex)`? What would go wrong for this milestone's
purposes if it caught everything?

**Answer (verbatim):** "ScenarioEvalRunner catches InvalidOperationException because ClarificationLoop
deliberately throws that exception for a specific known AI workflow failure: the model returns IsSufficient =
false but also returns zero clarifying questions. At that point, the loop cannot continue because the model
says more information is needed but gives the system no way to obtain it. Milestone 8 wants to record and
score that exact failure as part of the scenario trajectory rather than crash the whole harness. It does not
catch Exception broadly because that would also swallow unrelated failures such as Gemini 429 rate-limit
errors, network problems, serialization errors, or programming bugs. Those are harness or infrastructure
failures, not evidence that PokéJudge handled the scenario incorrectly. Catching everything would contaminate
the evaluation results by turning 'the system could not execute normally' into 'this scenario failed.'"

**Feedback:** Correct, and a genuinely strong answer — both the primary reason (a known, named AI-workflow
failure, not an infrastructure error) and the consequence of over-catching (contaminating results by
conflating "harness broke" with "system got the scenario wrong") were exactly right. Added nuance: this catch
decision pairs directly with `ScenarioEvalScorer` — every trajectory that hits this catch gets
`ThrewExpectedFailure: true`, but the scorer treats that as a pass ("Expected failure") only for
`ExpectedToFailLoudly` scenarios and a fail ("Unexpected failure") for every other type — confirmed for real
when `deck-not-shuffled` (authored as `SufficientOnFirstTurn`) crashed unexpectedly and was correctly flagged
rather than silently folded into "Sufficiency timing: FAIL." The narrow catch type is what makes that
distinction possible at all. Memory trick given: "Catch the workflow, not the wreckage."

### 3. Determinism of the scorer vs. non-determinism of the system under test

**Question:** `ScenarioEvalScorer.Score` is described in the plan and review as "pure" and "fully
deterministic," and its tests never touch the network. But the thing it scores — a `ScenarioTrajectory` —
comes from a live model that behaves non-deterministically (as `deck-not-shuffled` demonstrated, producing
three different outcomes across three identical runs). How can `ScenarioEvalScorer` be fully deterministic
and thoroughly unit-testable, while the system it's evaluating is not? What's actually being tested by
`ScenarioEvalScorerTests`, versus what's being observed (not tested) by running `dotnet run -- evaluate` for
real?

**Answer (verbatim):** "ScenarioEvalScorer is deterministic because it scores an already-observed trajectory
using fixed rules. ScenarioEvalScorerTests test that mapping by constructing controlled trajectories and
checking that each criterion is scored correctly, without involving Gemini or the network. dotnet run --
evaluate, on the other hand, observes what trajectory the real AI system produces. That upstream behavior is
non-deterministic, as deck-not-shuffled demonstrated. Different live runs can therefore produce different
trajectories and different scores, while the scorer itself remains completely deterministic for any given
trajectory."

**Feedback:** Correct. The `ScenarioTrajectory` record is the boundary — once captured, it's frozen data, and
scoring frozen data against fixed rules is exactly what a unit test can verify exhaustively.
`ScenarioEvalScorerTests` tests the mapping (trajectory → report), never "does the model behave correctly."
Named explicitly as the same pattern `SourceSupportAssigner` used in Milestone 7: a pure, deterministic
combinator sitting downstream of genuinely non-deterministic model behavior — push the non-determinism as far
upstream as possible, capture its output as inert data, then apply deterministic logic to that data. This is
also why `ScenarioEvalRunner` and `ScenarioEvalScorer` are separate classes at all. Memory trick given:
"Freeze it, then judge it."

### 4. Why clarifying-question materiality is a structural proxy, and why it wasn't fixed with an LLM check

**Question:** The plan explicitly calls out that clarifying-question materiality is scored via
`RelatedChunkId`'s section matching an expected section ID — a *structural* proxy, not a semantic one.
Concretely: what could a clarifying question do that would make this check pass even though a human judge
would say the question was actually bad? And separately — why was this limitation accepted here rather than
fixed, given that Milestone 7 already built an LLM-based check (`GroundingValidator`) for a related problem?

**Answer (verbatim):** "The materiality check is kind of a shortcut. It basically checks, 'Did this question
point to the section we expected?' But that doesn't necessarily mean the question was actually useful. So for
drew-extra-card, PokéJudge could ask something like, 'Was the extra card a Basic Energy?' and still tie that
question to PPG-5.5.1. The scorer would go, 'Yep, right section, pass.' But a human judge might say, 'That
detail doesn't actually matter. I need to know something else to decide what happened.' So it can pass a
question that's on-topic but still bad. We didn't try to fully solve that in Milestone 8 because figuring out
whether a question is truly material is a semantic judgment. The scorer itself can't really know that just
from IDs and structure. We could use another LLM call to judge the question, similar to what GroundingValidator
does, but then the evaluator itself becomes non-deterministic. Now you have Gemini judging Gemini, plus more
API calls, which is especially annoying since we already hit the free-tier limits during these eval runs. So
for now, section matching is basically a good-enough proxy. It catches obviously wrong directions, but it
doesn't prove the question was actually a good one. That's a known limitation we can improve later."

**Feedback:** Mostly correct — the core reasoning is right, and the `drew-extra-card` example is exactly the
right shape of failure (a question topically tied to the right section but probing an irrelevant detail).
"Gemini judging Gemini" independently reached the exact framing the project's own docs use. Clarified: the
primary stated rationale in the plan/review isn't the rate-limit cost (a real, secondary cost, correctly
flagged, but not the stated reason) — it's that Milestone 7 already produced direct, empirical evidence (not
just a theoretical worry) that an LLM validating its own generation isn't independent verification, because
correlated blind spots can survive the check. Building an LLM-as-judge question-quality scorer here would
repeat that exact, already-observed failure mode rather than apply what was learned from it. Memory trick
given: "The judge and the judged can't be the same witness."

### 5. Scoping the "Answer budget" criterion to `RequiresOneClarification` only

**Question:** You just added the "Answer budget" criterion to `ScenarioEvalScorer`, scoped so it only applies
to `RequiresOneClarification` scenarios — not `SufficientOnFirstTurn` or `ExpectedToFailLoudly`. Walk through
why: what would happen, concretely, if you scored "Answer budget" for a `SufficientOnFirstTurn` scenario too?
Would it ever produce new information beyond what "Sufficiency timing" already tells you?

**Answer (verbatim):** "We only score Answer budget for RequiresOneClarification because that's the only
scenario type where there's a meaningful 'you get one answer' budget to enforce. For SufficientOnFirstTurn,
any question at all already causes Sufficiency timing to fail, so Answer budget would just duplicate the same
failure instead of telling us something new."

**Feedback:** Correct. Added nuance: `ExpectedToFailLoudly` scenarios don't even reach that part of `Score()`
— the method returns early after scoring just "Expected failure" (or "Unexpected failure" if the trajectory
crashed unexpectedly), so for that outcome type "Answer budget" isn't redundant with something else, it's
structurally unreachable. Two different reasons land on the same scoping decision: redundant in one case,
out-of-scope in the other. Memory trick given: "New signal, not new noise" — a criterion only earns its place
if it can disagree with an existing one on some real trajectory.

### 6. Why non-determinism compounds the small-dataset limitation for Milestone 9

**Question:** The plan states plainly that this milestone's dataset is "a small, hand-authored dataset with
no statistical claim attached" — 8 scenarios that "catch gross regressions" but "say nothing about the
system's general error rate." Given what you now know about `deck-not-shuffled`'s run-to-run non-determinism
(§2 of `observed-limitations.md`), why does that non-determinism make this dataset-size limitation *worse*
for Milestone 9's purposes specifically, rather than just being a separate, unrelated problem?

**Answer (verbatim):** "The small dataset was already too small to make strong statistical claims, but the
model's non-determinism makes that worse because even those few datapoints aren't stable. deck-not-shuffled
failed twice and succeeded once across identical runs, so if Milestone 9 uses one run per scenario, whether
that scenario counts as 'correct' could basically depend on which run we happened to get. That's a big
problem for calibration, because we're trying to compare predicted confidence against actual correctness.
With only 8 scenarios, one randomly different outcome can heavily change the apparent accuracy or confidence
bucket results. So Milestone 9 needs to treat any calibration numbers as exploratory unless we add more
scenarios, repeated runs, or both."

**Feedback:** Correct — a sharp, well-connected answer. Correctly identified the compounding effect: the
noise doesn't just add a second, separate problem, it attacks the ground-truth labels themselves. Even the
handful of data points Milestone 9 would have aren't stable "correct"/"incorrect" labels — each is one
observed sample from a distribution of possible outcomes for that scenario. This matches
`observed-limitations.md` §7 exactly. Memory trick given: "Small and shaky" — a small dataset alone limits
precision; a small dataset with unstable ground truth limits whether any single number can be trusted at all.

### 7. What happens to a real judge who hits the same crash outside the eval harness

**Question:** `ScenarioEvalRunner` uses a scripted `askJudge` callback instead of reading real console input,
so it can run unattended. But imagine a real judge, live at a tournament, hit the exact same "insufficient
with zero questions" crash that `missed-prize` and `drew-extra-card` reproduce in the eval harness. Given PRD
§9's reliability requirements and what you know about how `ClarificationLoop` throws that exception, what
would actually happen to that judge in the real (non-eval) console flow? Is that the right behavior for a
production judge-facing tool, or is it just acceptable for now because this is still a console prototype?

**Answer (verbatim):** "Right now, that InvalidOperationException is basically a fail-fast guard. If the real
console flow hits the same IsSufficient = false plus zero-questions state, the scenario stops and the
exception will bubble up unless the outer console code catches it. That's better than silently inventing a
ruling, and it satisfies the PRD's requirement that AI/provider failures fail visibly. But it's not good
enough for a production judge-facing tool. A finished version should catch that failure at the application
boundary and turn it into a clear 'I can't safely resolve this' message with a next step, instead of exposing
an exception to the judge. For the current console prototype, the crash is acceptable because it makes the
failure obvious while we're still learning and evaluating the system."

**Feedback:** Mostly correct — the reasoning about the two layers (fail-fast guard vs. product boundary) and
the "acceptable for prototype, not production" judgment were exactly right. Verified against the real code:
`Program.cs`'s default interactive console flow (line 159, `await loop.RunAsync(...)`) has no `try`/`catch`
around it at all — so today a real judge hitting this crash would see a raw, unhandled
`InvalidOperationException` stack trace, and the app would terminate. This still technically satisfies PRD §9
("errors... must fail visibly... rather than silently degrading into an unsupported answer") — a crash is
about as visible as failure gets — but it's a rougher edge than a caught-and-logged error would be, and
nothing in the roadmap has explicitly promised to close this gap yet. The instinct that a finished product
needs a designed error boundary here is exactly right; it just hasn't been built. Memory trick given: "Loud is
not the same as graceful" — failing visibly (what PRD §9 requires) and failing gracefully (what a finished
product needs) are two different bars, and this system currently only clears the first one.

## Final Assessment

### Learning Checkpoint Result

**Strong**

### Concepts I Understand

- **Trajectory vs. destination evaluation** — clearly explained the `special-condition` divergence and why
  it's the sharpest real evidence for PRD §15's core argument.
- **Determinism boundary** — precisely located where non-determinism (live model) ends and determinism (the
  scorer) begins, and why that split is what makes `ScenarioEvalScorer` fully unit-testable.
- **Exception design as a scoring mechanism** — connected the narrow `InvalidOperationException` catch to the
  "expected vs. unexpected failure" distinction the scorer relies on, and correctly reasoned about
  over-catching's cost.
- **Why LLM-as-judge was rejected** — correctly reasoned about self-validation blind spots, independently
  reaching "Gemini judging Gemini" before being told the term.
- **Criterion scoping discipline** — reasoned correctly about the newly-added "Answer budget" criterion's
  scope, including the redundancy case.
- **Compounding limitations** — connected small-dataset-size and run-to-run non-determinism into a single,
  sharper problem for Milestone 9 rather than treating them as separate issues.
- **Product-vs-prototype boundary** — correctly distinguished "fails visibly" (satisfied) from "fails
  gracefully" (not yet built).

### Concepts to Reinforce

- Nothing structural. The one gap was a factual detail, not a conceptual one: the assumption that the real
  console flow already catches the sufficiency-crash exception somewhere. It doesn't yet — worth remembering
  that as a concrete, still-open rough edge if error handling is revisited later, rather than assuming PRD §9
  compliance means a graceful message already exists.

### Milestone Takeaway

1. A correct final answer does not certify a correct investigation — the `special-condition` run is the
   concrete, no-longer-hypothetical proof of that for this project.
2. Determinism in an eval harness lives in the *scorer*, not the *system under test* — capture the
   trajectory, then judge the frozen data.
3. Rejecting LLM-as-judge here wasn't caution for its own sake — it was applying a specific, already-observed
   finding from Milestone 7 rather than re-litigating the question from scratch.
4. Small dataset + non-deterministic ground truth is a compounding problem for Milestone 9, not two separate
   footnotes.

### Interview Readiness

1. **"How do you evaluate a multi-turn AI system, not just its final output?"** — A strong answer covers:
   capturing intermediate decisions (trajectory) as structured data, scoring that captured data with
   deterministic rules, and a concrete example where destination and trajectory scoring disagreed.
2. **"Why not use an LLM to grade your own LLM's output?"** — A strong answer names the specific risk
   (correlated blind spots / non-independent validation), ideally with a concrete prior finding (Milestone
   7's grounding validation) rather than a generic "it might be biased."
3. **"How do you unit test something whose real behavior is non-deterministic?"** — A strong answer separates
   "the thing that produces the non-deterministic output" from "the thing that judges it," and explains that
   only the judging logic gets exhaustively unit tested — the producing logic gets validated empirically, not
   asserted.

### Recommendation

**Ready for PR Review**
