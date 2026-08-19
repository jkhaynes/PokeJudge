# Milestone 8.5 — Source-Coverage Analysis

Per the milestone plan's step 5/7: for scenarios that repeatedly failed or behaved unexpectedly during
Milestone 8's real runs, this investigates whether the retrieved material would actually be sufficient for a
knowledgeable human judge, before assuming every failure is a PokéJudge reasoning problem. This is a manual
judgment call — inspecting each scenario's actually-retrieved chunks against the real corpus via
`dotnet run -- search` and reading the full source section text — not an automated or LLM-as-judge check, per
this project's standing position on self/model validation. The structured counterpart to this write-up is
`Evaluation/SourceCoverageFindings.cs`.

Four scenarios were investigated: the three that reproduced the known "insufficient with zero clarifying
questions" crash across Milestones 6-8 (`missed-prize`, `drew-extra-card`, and two of `deck-not-shuffled`'s
three runs), plus `spectator-badges`, the one scenario with a *repeatable* (not one-off) mismatch between its
authored expectation and observed behavior.

## 1. `spectator-badges` — Sufficient coverage

**Query:** "Do spectators need to wear a badge at large tournaments?"

**Top retrieved chunk (rank 1, score 0.7630):** `PPTRH-2.4#0` — "Our larger events (such as events at the
level of Regional Championships, Special Championships, and above) are badged events that require a
prepurchased pass to enter. You are required to wear your badge in a visible location during the entirety of
your participation in **or attendance at the event** and other badged locations."

Also retrieved: `PPTRH-3.3#6` ("Spectator Responsibilities"), which covers spectator conduct but says nothing
about badges.

**Conclusion:** `PPTRH-2.4#0` does answer the material question — "attendance at the event" is broad enough
to include spectators, not just competitors. A knowledgeable human judge reading this passage, combined with
knowing "large tournaments" means Regional-Championship-tier-or-above, would confidently say yes. The
retrieved material is there; both real runs that reached this scenario asked a clarifying question and never
resolved it anyway. This points at the sufficiency-assessment step being unwilling to commit to the
inferential step ("attendance" → "spectators are in attendance"), not at retrieval or the corpus. **Decision:**
kept the scenario's `SufficientOnFirstTurn` expectation as-authored — the recurring failure is now a
documented, known gap in the sufficiency-assessment step, worth carrying forward as a priority whenever a
future milestone next revisits that prompt (the same treatment this project already gives the zero-questions
crash below).

## 2. `drew-extra-card` — Sufficient coverage

**Query:** "A player drew an extra card during their draw step and didn't realize it until later in the turn."

**Full text of `PPG-5.5.1`** (read in its entirety, not just the top search hit) explicitly lists, as a Major
gameplay error worked example: "A competitor draws an extra card," and separately, as a de-escalation example:
"A competitor draws an extra card. This is noted while the correct card can still be identified and before the
competitor saw the face of the card."

**Conclusion:** unambiguous, explicit, worked-example-level coverage. This scenario's repeated
insufficient-with-zero-questions crash is the known sufficiency-assessment bug (first observed in Milestone 6,
now reproduced a third time), not a source gap. **Decision:** kept `RequiresOneClarification` as-authored.

## 3. `deck-not-shuffled` — Sufficient coverage

**Query:** "A player's deck wasn't fully shuffled before the game started."

**Top retrieved chunks:** `TCGTH-6.2#0` ("Each competitor's deck is expected to be fully randomized at the
start of each game... Randomization must be done in the presence of the competitor's opponent"),
`TCGTH-6.2.2#0` ("Insufficiently randomizing the deck is a rules violation that may carry a penalty"), and
`TCGTH-6.2.1#0` (the exact Judge-involvement procedure: "If either competitor does not feel that either deck
is sufficiently randomized... a Judge...").

**Conclusion:** about as clean and explicit as source coverage gets — the setup rule, the violation
consequence, and the Judge-involvement procedure are all directly retrieved. This scenario's 3-different-
outcomes non-determinism across identical live runs (two crashes, one multi-turn success) is a reasoning/
consistency problem, not a retrieval or corpus problem. **Decision:** kept `SufficientOnFirstTurn` as-authored.

## 4. `missed-prize` — Possible source gap

**Query:** "a player forgot to take a Prize card after knocking out their opponent's Pokemon," plus a
follow-up targeted query: "how to fix a missed or forgotten Prize card game state repair."

**What's covered:** `PPG-5.5.1` (read in full) explicitly names two *adjacent* Prize-related errors: "A
competitor takes a Prize card **without** Knocking Out a Pokémon" and "takes **too many** Prize cards after
Knocking Out a Pokémon." `TCGRULES-turn-actions#13` describes the normal take-a-Prize-after-a-Knock-Out
procedure. `PPG-3.4#0` (Quadruple Prize Card penalty) covers severe, hard-to-resolve mistakes generally.

**What's not covered:** no passage found, across multiple targeted searches, explicitly names the exact
inverse case this scenario describes — a competitor forgetting to take a Prize card they *were* entitled to,
discovered turns later.

**Conclusion:** this is a genuine, real gap, distinct in kind from the three Sufficient-coverage findings
above. It's also a plausible partial explanation for why this scenario's crash is so consistently
reproducible: with no passage that names this exact situation, the sufficiency-assessment step may have
nothing concrete to formulate a clarifying question about — a different mechanism than `spectator-badges`'
gap (material present, inference declined) or `drew-extra-card`'s (material present, crash anyway).
**Decision:** classified as a **possible source gap**, not confirmed — the corpus may still address this
somewhere not surfaced by the queries tried. Per the milestone plan, no corpus expansion is being made on the
strength of this alone; this is recorded as a candidate for a future, narrowly-scoped addition if the gap is
confirmed through further investigation, not acted on automatically.

## Summary

| Scenario | Classification | Root cause of the observed failure |
|---|---|---|
| `spectator-badges` | Sufficient coverage | Sufficiency-assessment declines an inferential step |
| `drew-extra-card` | Sufficient coverage | Known zero-questions crash (reasoning, not retrieval) |
| `deck-not-shuffled` | Sufficient coverage | Reasoning/consistency non-determinism (not retrieval) |
| `missed-prize` | Possible source gap | Corpus may lack an explicit worked example; genuinely unconfirmed |

Three of four investigated scenarios are Sufficient coverage — the dominant pattern in this dataset's known
failures is a reasoning/sufficiency-assessment weakness, not a retrieval or corpus problem. `missed-prize` is
the one exception, and is recorded as a possible (not confirmed) gap rather than acted on, consistent with
the plan's "keep source expansion deliberate, not automatic" requirement.
