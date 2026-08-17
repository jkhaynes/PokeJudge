# Milestone 7 — Grounding Analysis

Required deliverable per the PRD's Milestone 7 learning objective: which grounding checks can be
deterministic versus which genuinely require model judgment, and why an LLM validating its own generation is
not fully independent validation.

## 1. What's actually deterministic

Three checks run with zero LLM involvement, over data already produced by earlier steps
(`Grounding/DeterministicGroundingChecks.cs`):

| Check | What it verifies | Why it's a pure lookup |
|---|---|---|
| `RetrievalNonEmpty` | Was anything retrieved at all for this ruling? | `retrievedChunks.Count > 0` -- no interpretation involved. |
| `AllCitationsExist` | Does every `CitedChunkId` the ruling names actually appear in the chunk set it was shown, and did it cite at least one? | Set membership (`citedChunkIds.All(retrievedIds.Contains)`), plus a non-empty requirement. A citation either exists in the supplied set or it doesn't. |
| `FactsWereSufficient` | Did the clarification loop actually report `Sufficient` before this ruling was generated? | A boolean pass-through of a fact the loop already computed. Trivial by design -- see below. |

And the combinator itself (`Grounding/SourceSupportAssigner.cs`) is deterministic: given the three flags above
plus a structured `GroundingAssessment`, it applies PRD §8's Strong/Partial/Insufficient rubric as ordinary
code -- no model call, no free-form judgment. This is the actual "criteria-based, not raw model opinion" piece
PRD §8 asks for. It is exhaustively table-driven-tested (`SourceSupportAssignerTests`) because it's the one
piece of this milestone that can be.

**Why `FactsWereSufficient` is worth stating even though it's trivial:** before this milestone, "don't
generate a ruling from an insufficient scenario" was enforced only by `Program.cs`'s control flow --
`RulingGenerator` is never called on a `TurnCapExhausted` outcome. That's real protection, but it's an
*implicit* guarantee about caller discipline, not something the Source Support assignment itself checks. Now
it's an explicit, testable input to the combinator. If some future caller (or a bug) ever did call
`GroundingValidator` on an insufficient outcome, the label would still correctly collapse to `Insufficient`
rather than depending on that caller having gotten it right.

## 2. What genuinely requires model judgment

One thing, and it cannot currently be made deterministic: **does a specific cited passage's text actually
support the specific claim it was cited for?**

`GroundingValidator` makes one LLM call (`SystemPrompts.GroundingValidation`) that classifies each citation as
`ExplicitSupport` / `Interpretation` / `Unsupported`, and flags cross-passage conflict. This is a
reading-comprehension judgment -- "does this sentence, when read against that passage, hold up" -- and no
amount of string matching or embedding-similarity thresholding substitutes for actually reading both texts
against each other. `DeterministicGroundingChecks.AllCitationsExist` can tell you the model didn't invent a
citation ID; it cannot tell you whether the model correctly read what that citation says.

## 3. Why this is not independent validation

`GroundingValidator` and `RulingGenerator` are both constructed with the same `ILlmClient` in `Program.cs` --
today, the same underlying Gemini model both generates the ruling and grades its own citations. This matters
concretely, not just as a stated caveat: if the model's `RulingGenerator` call misreads a passage and cites it
for a claim it doesn't actually support, the same model's `GroundingValidator` call is being asked to notice
its *own* misreading of the *same* text, using the *same* underlying capabilities and blind spots. A model
that is, say, systematically prone to over-reading permissive language in tournament-policy text would likely
make that error in both the generation call and the validation call -- the checks are correlated, not
independent.

What real independence would require, roughly in increasing order of cost:

1. **A different model or provider** for `GroundingValidator` specifically -- cheapest change, but still an
   LLM judging LLM-shaped text, just a different one. Reduces (doesn't eliminate) correlated blind spots.
2. **Ground-truth eval data** (Milestone 8) -- a curated set of scenarios with known-correct citations/rulings
   lets you measure whether grounding validation actually catches injected errors, rather than just trusting
   that it would.
3. **A human in the loop** -- the only fully independent check, and not something this product can rely on at
   scale for every ruling.

None of these are built in this milestone. `GroundingValidator` deliberately reuses `Program.cs`'s single
`ILlmClient` instance, the same one `RulingGenerator` uses -- this is not an oversight, it's the honest,
observable state of "self-validation" as implemented today, left in place so the limitation is visible rather
than hidden behind an interface that implies more independence than actually exists.

## 4. A real, concrete instance of this limitation -- not just the general argument

`observed-limitations.md`'s Special Condition scenario is a case where the *combined* system (deterministic
checks + per-citation LLM classification + the combinator) produced a **Strong** validated label, while the
ruling-generation model's own free-form self-assessment was **Partial** -- and on manual reading, the model's
original Partial judgment looks like the more defensible one.

This is not the direction the plan expected to find (it expected to catch the model *over*-calling `Strong`).
Instead, it surfaces a different, real gap: `GroundingValidator` only asks "does citation X support the
specific claim it's attached to," at the granularity Milestone 6 established (per citation, not per sentence).
Every individual citation in that ruling genuinely did explicitly support its own narrow claim (how Asleep and
Confused are physically marked on the card). What none of the three citations addressed -- and what the
combinator has no way to check, because nothing asks it -- is whether the *recommendation as a whole*
(synthesizing those citations into "investigate and correct the marker") is itself fully prescribed by the
retrieved text or requires judge discretion to arrive at. The model's own holistic self-assessment noticed
that gap; the citation-level pipeline structurally cannot, because it was never asked the question at that
level.

This is exactly the "per-sentence or per-claim grounding granularity" question the plan's Out-of-Scope section
named and deliberately deferred. This instance is the concrete evidence for *why* that's a real limitation
rather than a hypothetical one -- worth carrying into whichever future milestone revisits grounding
granularity, since it's now a demonstrated gap, not a guess.
