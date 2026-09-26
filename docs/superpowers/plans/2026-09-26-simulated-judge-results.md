# Step 2 live eval results

Run 2026-09-26: full 20-scenario `evaluate`, pinned `gemini-3.5-flash-lite`, fixed seed, simulated judge (after the partial-answer prompt fix, commit `91a4138`), paced at 14 calls per minute.

**Score: 14/20** (baseline with scripted answers, same model and seed: 11/20).
**Not known:** 3 of 27 questions couldn't be answered from the fact sheets (reported, not scored).
**Infrastructure failures:** 0.

## Newly passing

deck-not-shuffled, spectator-badges, weakness-not-applied, too-many-prizes. All four had failed the baseline because of the old test: a wrong expectation, an ambiguous description, or scripted answers that didn't match the question.

## Failures

| Scenario | Failed criteria | Label | Reason |
|---|---|---|---|
| proxy-cards | Final Source Support | PokeJudge was wrong | Validated Partial for an explicit rule (the model itself said Strong). Grounding validation issue. |
| special-condition | Question budget (3 rounds, limit 1) | PokeJudge was wrong | The first answer settles it ("the attack text says Confused"); rounds 2 and 3 ask whether other effects applied. Over-asking. |
| drew-extra-card | Initial retrieval, materiality, post-answer retrieval, Question budget, Source Support | PokeJudge was wrong | Retrieval never surfaces PPG-5.5.1, so PokeJudge keeps asking the same question after it was answered ("No, it can't be identified"). Retrieval problem (Step 4). |
| supporter-twice | Question budget (4 rounds), Source Support | **The test was wrong** | PokeJudge asks "which Supporter cards were played by *the player*"; the fact sheet says "*the opponent* played two copies of Judge", and the judge answers literally that it doesn't know what "the player" played. Fact-sheet wording. PokeJudge also re-asks an answered question, but the test caused the loop. |
| mulligan-not-taken | Unexpected failure | PokeJudge was wrong | After a correct answer, PokeJudge reports "insufficient" with no questions: the known Milestone 2 zero-questions crash. |
| spectator-conduct | Sufficiency timing | PokeJudge was wrong | Asks whether the person is a spectator; the scenario already says so. |

## Follow-ups

- **supporter-twice fact sheet (the test was wrong):** name the player by role, e.g. "The player being ruled on (the caller's opponent) played two copies of Judge…", so "the player" in PokeJudge's questions is unambiguous.
- PokeJudge's failures above go to later steps: over-asking and re-asking answered questions (Step 5, the judgment model), retrieval misses (Step 4), and the zero-questions crash.
