# Milestone 7 — PR Review

## PR Review Summary

- **Milestone:** Milestone 7 — Citations and Grounding
- **Current branch:** `milestone/7-citations-grounding`
- **Base branch:** `master`
- **What changed:** Adds a new `Grounding/` module implementing PRD §11's "Grounding Validation & Source
  Support Assignment" architecture box. Three deterministic checks (retrieval non-empty, citation existence,
  fact sufficiency) plus one LLM call classifying each cited passage's support level
  (`ExplicitSupport`/`Interpretation`/`Unsupported`) feed a pure `SourceSupportAssigner` combinator that
  replaces Milestone 6's unvalidated, model-self-assigned `SourceSupport` label with a criteria-based,
  validated one. `Program.cs` prints both labels side by side plus a per-citation breakdown. All changes are
  currently uncommitted in the working tree (no commits yet ahead of `master`).
- **Overall impression:** Clean, well-scoped implementation that matches its approved plan closely and
  produced genuinely strong real-data evidence for its learning objective, including an important divergence
  case (documented in `observed-limitations.md`) where the validated label was arguably *less* accurate than
  the model's own self-report — a valuable, honestly-reported finding rather than a defect. One correctness
  gap found during this milestone's own internal review (`review.md`) — an unhandled exception on a
  duplicate-citation-ID model response — has already been fixed with a named exception and a regression test
  before this PR review; verified independently below.
- **Build/test status:** `dotnet build` — succeeded, 0 warnings, 0 errors (verified independently). `dotnet
  test` — 154/154 passed (verified independently, matches the implementation summary's reported count
  including the post-review fix's new test).

## 🚫 Blockers

None.

## ⚠️ Major Issues

None.

## 🔎 Minor Issues

- ~~**`PromptBuilder.cs` adds an explicit `using System.Linq;`**~~ **Fixed.** Removed — `PokeJudge.csproj` has
  `ImplicitUsings` enabled, so `System.Linq` was already globally available, matching
  `Grounding/DeterministicGroundingChecks.cs` and `Grounding/SourceSupportAssigner.cs`, which use the same
  LINQ methods with no explicit `using`. Re-verified: `dotnet build` clean (0 warnings/errors), `dotnet test`
  still 154/154 passing, confirming the `using` was genuinely redundant.

## 💬 Review Notes

- Independently re-verified the Must Fix item this milestone's own `review.md` already caught and fixed:
  `SourceSupportAssigner.Assign` now detects duplicate `ChunkId`s in the grounding-validation model's
  response via `GroupBy` before building the lookup dictionary, and throws a named `InvalidOperationException`
  listing the duplicate(s) rather than letting `ToDictionary` throw an unlabeled `ArgumentException`. Confirmed
  the fix is real (read the current file, not just the review's description) and confirmed the two silent
  alternatives considered and rejected (take-first, take-worst) were correctly reasoned against, since either
  would risk quietly resolving a malformed model response instead of surfacing it — consistent with this
  codebase's established "fail loudly, name the problem" convention for anomalous structured output (e.g.,
  Milestone 2's "insufficient with zero questions" guard). The new regression test
  (`Assign_ModelClassifiesTheSameChunkIdTwice_ThrowsNamingTheDuplicate`) correctly asserts both the exception
  type and that the message names the duplicated ID.
- `DeterministicGroundingChecks.RetrievalNonEmpty` and `FactsWereSufficient` are, in the live console flow,
  close to structurally-guaranteed-true rather than real gates — `GroundingValidator` is only ever invoked
  after `outcome.Sufficient` is already true, and `finalChunks` can only be empty if the entire corpus is
  empty (already gated earlier with its own error message). This isn't a defect — the plan explicitly frames
  `FactsWereSufficient` as a defensive, explicit criterion rather than one expected to fire in practice — but
  it's worth knowing that only `AllCitationsExist` and the per-citation LLM classification have realistic
  paths to actually catching something in today's real usage. Already noted by this milestone's own
  `review.md`; independently confirmed by re-reading the console wiring in `Program.cs`.
- Confirmed `BuildGroundingValidationPrompt` deliberately omits `ruling.SourceSupport`/`SourceSupportRationale`
  from the validation prompt (read the method directly) — the grounding-validation call has no way to see or
  anchor on the model's own self-reported label, which is exactly right for keeping the two assessments
  independent of each other's stated conclusion (though not independent in the deeper sense discussed in
  `grounding-analysis.md` §3, since both still share the same underlying model).

## 🤖 AI-Specific Review

- **Prompt scoping is deliberately narrow and correctly so.** `BuildGroundingValidationPrompt` sends only the
  ruling's recommendation/explanation and the text of *cited* chunks (not the full retrieved set, not the
  original scenario, not confirmed facts/hypotheses) — verified by reading the method. This prevents the
  validator from justifying a claim using context the ruling never actually cited, which would otherwise
  defeat the point of a citation-level check.
- **The deterministic/model-judgment split is real, not just labeled that way.** `DeterministicGroundingChecks`
  contains zero LLM calls and is fully unit-testable; `GroundingValidator` makes exactly one LLM call, isolated
  to the one question (does this citation support this claim) that genuinely can't be automated with a lookup.
  `SourceSupportAssigner` — the actual label-assignment logic — is plain, deterministic C#, not a second
  "ask the model for the label" call, which is the milestone's central architectural point and is correctly
  realized in code, not just asserted in comments.
- **Same-model self-validation limitation is honestly represented, not hidden behind an abstraction.**
  `GroundingValidator`'s constructor takes a plain `ILlmClient`, and `Program.cs` constructs it with the exact
  same instance used for `RulingGenerator` — there's no interface-level pretense of independence. The
  divergence observed in the Special Condition scenario (validated `Strong` vs. the model's own `Partial`,
  with the model's original judgment looking more defensible on manual review) is real, documented evidence
  of this limitation actually mattering, not just a theoretical caveat.
- **No hidden reliance on undocumented model behavior.** The grounding-classification schema constrains
  output to exactly three enum values per citation plus a boolean conflict flag; `StructuredResponseParser`'s
  existing `JsonStringEnumConverter` (added in Milestone 6) handles `CitationSupportLevel` deserialization
  with no new parsing logic required, confirmed by the new `Parse_GroundingAssessmentJson_...` tests actually
  exercising real JSON through the real parser rather than only through `StubLlmClient`-provided objects.

## 🧪 Test Review

- **Coverage is meaningful and appropriately layered.** `DeterministicGroundingChecksTests` is straightforward
  and complete for three trivial-but-important pure functions. `SourceSupportAssignerTests` is table-driven
  across the full rubric (all three deterministic-failure short-circuits, the dropped-citation-defaults-to-
  Unsupported case, the Interpretation-only case, the conflict-without-interpretation case, the all-Strong
  case, and now the duplicate-citation-ID exception case) — this is the piece that most needed thorough
  testing since it's the actual PRD-mandated rubric, and it got it. `GroundingValidatorTests` mirrors
  `RulingGeneratorTests`' established pattern: asserts real orchestration and real prompt content via
  `StubLlmClient`, not just that *a* result comes back.
- **The final coverage-review pass caught a real gap, not a token one.** `StructuredResponseParserTests`
  gained two tests deserializing real `GroundingAssessment` JSON (including the enum) — this was genuinely
  untested before, since every other Grounding test uses `StubLlmClient`, which bypasses real JSON parsing
  entirely. Independently confirmed this gap was real by checking `GroundingValidatorTests` and
  `SourceSupportAssignerTests` never exercise `StructuredResponseParser.Parse<GroundingAssessment>` directly.
- **Build/test results:** `dotnet build` clean; `dotnet test` 154/154 passing — independently reproduced.
- **Manual AI experiments:** `observed-limitations.md` documents three real runs against the live,
  expanded corpus — a clean agreement case, the Special Condition divergence, and a re-run of Milestone 6's
  missed-Prize failure showing it's unrelated to retrieval quality. This is appropriate, sufficient manual
  validation for this milestone; a formal eval harness measuring how often grounding validation catches real
  errors remains correctly out of scope until Milestone 8.

## 📦 Scope Check

- **Does this branch correspond to the current milestone?** Yes — every changed/added file maps directly to
  Milestone 7's plan (the `Grounding/` module, prompt/system-prompt additions, `Program.cs` wiring, tests,
  and the three required `.project-plans/milestone-7/` documents).
- **Does the diff contain only work appropriate to this milestone?** Yes.
- **Did unrelated changes get mixed into the branch?** No unrelated files or changes found in the diff
  against `master`. (The corpus-expansion/ingestion-pipeline work is correctly on its own, already-merged
  branch, not mixed into this one.)
- **Did it implement anything from future milestones?** No. Correctly avoided per the plan's Out-of-Scope
  section: no eval harness, no numeric/calibrated confidence, no second model/provider for independent
  validation, no per-sentence/per-claim grounding granularity, no retrieval improvements, no UI work.
- **Did it introduce unnecessary architecture or dependencies?** No new NuGet packages. `Grounding/` as a new
  top-level module (rather than folded into `Clarification/`) is justified by PRD §11's diagram explicitly
  drawing it as a distinct third box, and mirrors the same reasoning already applied when `IRetriever` got its
  own `Retrieval/` folder in Milestone 5.

## Final Verdict

**Approve**

No blockers or major issues, and the one Minor issue (a redundant-but-harmless `using` statement) isn't worth
a follow-up on its own. The Must Fix item this milestone's own internal review caught was independently
re-verified as genuinely fixed, with a real regression test, before this PR review — not just claimed fixed.
The implementation stays tightly scoped to the approved plan and produced strong, honestly-reported real-data
evidence for its learning objective, including a divergence finding that's more interesting than what the
plan anticipated.
