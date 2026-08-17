# Milestone 7 — Observed Limitations & Failures (Real-Data Run)

Captured per the plan's step 7/10: concrete evidence from running the rebuilt
retrieve → assess → clarify → re-retrieve → generate → **validate grounding** loop against the real,
expanded corpus (4 documents, 515 chunks -- see the separate `ingest/penalty-guidelines-and-core-rulebook`
branch/PR).

- **Date:** 2026-08-18
- **Branch:** `milestone/7-citations-grounding`
- **Corpus:** `PPTRH`, `TCGTH`, `PPG`, `TCGRULES` -- 515 chunks.

## 1. Clean agreement -- validated label matches the model's own assessment

**Scenario:** *"Is a competitor allowed to keep written notes during their match?"* (the same well-covered
query used in Milestones 5 and 6).

Resolved in one turn. Model's own self-assessment: **Strong**. `GroundingValidator`'s per-citation check
classified both cited chunks (`TCGTH-7.4.6#0`, `TCGTH-7.4.6#1`) as `ExplicitSupport`, no conflict detected, all
three deterministic checks passed -- validated label: **Strong**, matching the model exactly. This is the
"nothing to catch" case: a well-supported ruling with citations that hold up under independent, structured
scrutiny.

## 2. A real, important divergence -- the validated label was arguably *worse* than the model's own judgment

**Scenario:** *"A judge notices during a match that a player's Active Pokemon has a Special Condition marker on
it that doesn't seem right for what happened. The marker is Asleep, but the player says their opponent's
attack was supposed to cause Confused, not Asleep."* (the same scenario from Milestone 6's observed-limitations,
now with `TCGRULES`'s own "Special Conditions" section in the corpus.)

- Two turns: one clarifying question asked and answered, confirming the marker discrepancy.
- Final ruling cited `TCGRULES-special-conditions#0`, `#2`, `#3`.
- **Model's own self-assessment: Partial** -- "resolving a discrepancy between what an attack was supposed to
  cause versus what state is currently marked requires judge discretion and fact-finding not fully prescribed
  by the text alone."
- **Grounding validation classified all three citations as `ExplicitSupport`**, no conflict detected, all
  deterministic checks passed → **validated label: Strong**.

This is a direct contradiction between the model's own holistic read and the citation-level validation
pipeline, and on manual review the model's original **Partial** looks like the more defensible label. Every
individual citation genuinely does explicitly support its own narrow claim (how Asleep/Confused are physically
marked on a card) -- but nothing checks whether the *recommendation as a whole* (synthesizing those citations
into "investigate and correct the marker") is itself fully prescribed by policy or requires judge discretion to
reach. See `grounding-analysis.md` §4 for the full discussion: this is concrete evidence for the
per-citation-granularity limitation the plan's Out-of-Scope section named but didn't yet have a real instance
of.

**This is the opposite of what the plan anticipated observing** (it expected to catch the model *over*-calling
`Strong`; instead the automated validation over-called it relative to the model's own more cautious read). That
makes it, if anything, a more useful finding: it shows the risk isn't only "the model's self-report can't be
trusted upward" -- a validation scheme with the wrong granularity can *also* be wrong, confidently, in a
direction that looks more rigorous than it is precisely because it's now backed by structured, itemized
checks. Worth remembering when Milestone 8's eval harness scores agreement between the two labels: agreement
is not automatically evidence the validated label is correct.

## 3. The missed-Prize scenario still fails the same way, even with dramatically better retrieval

**Scenario:** *"During a League Challenge, a player just noticed they forgot to take a Prize card after
knocking out their opponent's Pokemon two turns ago."* (Milestone 6's reproducible low-confidence-retrieval
failure.)

Retrieval is now genuinely good: top result `PPG-5.5.1#2` at **0.8016** -- directly on-topic (Prize-card
gameplay-error categorization), versus Milestone 6's best of ~0.75 on topically-adjacent
tournament-*procedure* content. Despite that, the sufficiency assessment **still returned `isSufficient: false`
with zero clarifying questions**, and `ClarificationLoop`'s Milestone 2 guard caught it with the same
`InvalidOperationException` as before -- the process never reached `RulingGenerator` or `GroundingValidator` at
all for this scenario.

This is worth stating plainly: **better retrieval alone did not fix this failure mode.** Milestone 6 diagnosed
this as a retrieval-coverage gap ("no direct equivalent of the mock corpus's discovery-timing snippet in the
real corpus"). That diagnosis was only half right -- the content gap really did exist and is now closed (the
retrieved passage is squarely on-topic), but the model's sufficiency-assessment step still could not articulate
what specific fact was missing, given genuinely relevant material to reason over. This points at a distinct,
unresolved weakness in the sufficiency/clarification prompt or model behavior itself, separate from retrieval
quality -- not something this milestone's grounding-validation work touches, since grounding validation never
even runs when the loop fails this early. Left unfixed and undesigned-around here, consistent with the
project's "observe before designing a fix" discipline; worth flagging for whoever next revisits the
sufficiency-assessment prompt.

## 4. Source-conflict detection never fired

Across all three real runs, `GroundingAssessment.ConflictDetected` was `false` every time -- including the
Special Condition scenario, which cites three chunks from the same section and could plausibly have surfaced a
edge-case conflict if one existed. No instance in this corpus, at this scale, produced genuinely conflicting
retrieved passages for the model to flag. Consistent with the plan's anticipated limitation ("may rarely or
never fire against this project's current corpus at this scale") -- worth noting honestly rather than claiming
this check is validated, since it has literally never been exercised by a real positive case.

## 5. What this suggests going forward

- The Special Condition divergence (§2) is the strongest concrete argument yet for reconsidering grounding
  granularity -- not urgent, but no longer hypothetical.
- The missed-Prize scenario (§3) suggests the sufficiency-assessment prompt itself may need attention
  independent of retrieval/grounding work -- a different milestone's concern, flagged here because this is
  where it was re-observed.
- Conflict detection (§4) remains implemented-but-unvalidated by any real positive case; Milestone 8's eval
  dataset would be a natural place to deliberately construct one.
