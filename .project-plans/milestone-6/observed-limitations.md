# Milestone 6 — Observed Limitations & Failures (Real-Data Run)

Captured per the plan's step 10-12: concrete evidence from running the rebuilt retrieve → assess →
clarify → re-retrieve → generate loop against the real, already-embedded documents from Milestones 3-5.

- **Date:** 2026-08-17
- **Branch:** `milestone/6-rag`
- **Corpus:** the same 258-chunk corpus from Milestone 5 (`PPTRH`, `TCGTH`), unchanged.

## 1. Clean single-turn success — the full RAG pipeline working end to end

**Scenario:** *"Is a competitor allowed to keep written notes during their match?"* (the same query
Milestone 5 used to demonstrate semantic search).

Retrieved `TCGTH-7.4.6#0` at 0.8936 on the first turn, assessed immediately sufficient (no clarifying
questions needed), and `RulingGenerator` produced a recommendation directly grounded in the retrieved
text with **Source Support: Strong**, citing all five retrieved chunks and correctly summarizing the
note-taking conditions (blank at match start, timely, no messaging devices, no codes/ciphers). This is
the milestone's core learning objective working exactly as designed: real retrieved passages, not
pretrained knowledge, produced a specific and accurate recommendation.

A second well-covered scenario (*"Can a player use proxy cards printed at home during a sanctioned
tournament?"*) also resolved in one turn with Source Support: Strong, correctly citing `TCGTH-2.4#0`/`#1`
and accurately distinguishing judge-issued proxies (permitted, narrow circumstance) from player-made
proxies (not permitted) — evidence Strong isn't a rubber stamp; it tracked genuinely well-supported
retrieval.

## 2. Iterative retrieval demonstrably mattering, not just claimed

**Scenario:** *"A judge notices during a match that a player's Active Pokemon has a Special Condition
marker on it that doesn't seem right for what happened. The marker is Asleep, but the player says their
opponent's attack was supposed to cause Confused, not Asleep."*

- **Turn 1 retrieval:** `TCGTH-6.3.1#0` (0.7406), `TCGTH-2.4#0`, `TCGTH-2.1#0`, `TCGTH-2.1#1`, `TCGTH-8#0`.
- **Assessment:** Insufficient — one clarifying question, tied to `TCGTH-6.3.1#0`: *"What is the specific
  nature of the discrepancy regarding the condition marker on the Active Pokemon?"*
- **Judge's answer** was classified into two **confirmed facts** ("The condition marker currently on the
  Active Pokemon is Asleep." / "The player claims the opponent's attack was supposed to cause Confused
  instead of Asleep.") and three **hypotheses**, correctly kept separate — e.g. "An incorrect condition
  marker was placed on the Active Pokemon" is a plausible reading, not something the judge stated, and it
  was not promoted to a confirmed fact.
- **Turn 2 retrieval** (query now includes both confirmed facts): `TCGTH-6.3.1#0` (0.7297),
  `TCGTH-6.3.2#0`, `TCGTH-6.3#0`, `PPTRH-5.4#1`, `TCGTH-2.2#2` — **three of five results are genuinely
  new** compared to turn 1 (`TCGTH-6.3.2#0`, `TCGTH-6.3#0`, `PPTRH-5.4#1` replaced `TCGTH-2.4#0`,
  `TCGTH-2.1#0`, `TCGTH-2.1#1`, `TCGTH-8#0`). The confirmed facts genuinely changed what came back, exactly
  the mechanism PRD §8's "iterative retrieval" describes.
- **Final ruling: Source Support Partial**, with an honest rationale: the retrieved passages establish that
  condition markers are public information competitors can inspect, but don't prescribe an exact procedure
  for resolving a disputed marker — a legitimate, non-Strong classification the model reached on its own
  rubric, not a forced or hedged answer.

## 3. A real, reproducible low-confidence-retrieval failure (PRD §18's deferred question, now observed)

**Scenario:** *"During a League Challenge, a player just noticed they forgot to take a Prize card after
knocking out their opponent's Pokemon two turns ago."*

Retrieved (byte-for-byte identical across two separate runs, confirming Milestone 4's deterministic-embedding
property still holds): `PPTRH-7.2#0` (0.7516), `TCGTH-8.1#0` (0.7492), `PPTRH-4.6#3` (0.7399),
`TCGTH-7.4.3#2` (0.7374), `TCGTH-7.4.3#4` (0.7328) — all penalty/procedure-adjacent, none specifically
addressing missed-Prize discovery timing (there is no direct equivalent of Milestone 2's hand-authored
mock-corpus snippet in the real corpus, or it wasn't retrieved).

The model's sufficiency assessment then returned `isSufficient: false` with **zero clarifying questions** —
a genuinely malformed structured response the schema doesn't prevent (the schema only requires the
`questions` array to exist, not to be non-empty when insufficient). `ClarificationLoop` caught this with
the exact guard Milestone 2 wrote for this situation
(`RunAsync_InsufficientWithoutQuestions_ThrowsRatherThanLoopingSilently`) and the process crashed loudly
with a clear `InvalidOperationException`, satisfying PRD §9's "fail visibly" requirement — it did not
silently degrade into a guess.

This is a direct, concrete instance of the low-confidence-retrieval problem PRD §18 explicitly deferred to
this milestone: no similarity-threshold or minimum-passage-count gate exists, so a scenario sitting at the
edge of the corpus's real coverage produces borderline-relevant retrieval (~0.73-0.75, not dramatically
low) that the model still can't reliably reason about. It reproduced identically on a second run, so this
is a real, repeatable weak spot, not a one-off sampling fluke. **Left unfixed, as planned** — designing a
threshold or a "no good match found" fallback question before seeing this concrete failure mode would have
been premature; that design work is now informed by real evidence for whenever it's taken up.

## 4. What this suggests for Milestone 7

- The unresolved question from observation 3 is exactly what an explicit retrieval-confidence signal (or a
  guaranteed non-empty clarifying-question fallback when the model reports insufficient) would need to
  address — worth carrying into Milestone 7's criteria-based Source Support work, since "no relevant
  material retrieved" is one of PRD §8's own listed `Insufficient` conditions and this run shows the model
  didn't reliably reach that conclusion on its own.
- Source Support is still purely model-assigned here. Observation 2's Partial classification looked
  reasoned and well-justified, but nothing in this milestone independently checked it against retrieval
  scores, citation coverage, or fact sufficiency — it's the model's word alone, exactly the gap Milestone 7
  exists to close.
