# Step 2: Fix the eval with a simulated judge

Status: approved design, 2026-09-26. Step 2 of the PokeJudge improvement plan.

## Goal

The eval score should measure PokeJudge, not the eval's own mistakes. Today the eval answers PokeJudge's clarifying questions from a fixed, ordered script. When PokeJudge asks something the script didn't expect, it gets a canned or empty answer, keeps re-asking, and fails. That caused 7 of the 9 failures in the 2026-09-26 baseline (11/20, pinned model, fixed seed).

Two changes:

1. Replace the ordered script with a **simulated judge**: an AI call that answers each question from a per-scenario fact sheet, or says "not known".
2. Correct the scenario expectations the baseline showed were wrong.

PokeJudge's own pipeline (retrieval, clarification loop, ruling, grounding) does not change.

## Components

### `EvalScenario` (changed)

- `ScriptedAnswers` (ordered list) is replaced by `FactSheet` (string): a few plain sentences stating what is true at the table.
- Everything else stays: `ExpectedOutcome`, expected sections, post-answer sections, acceptable Source Support.

### `SimulatedJudge` (new, `PokeJudge/Evaluation/`)

- `Task<JudgeAnswer> AnswerAsync(string factSheet, string question)`, returning `JudgeAnswer(string Answer, bool Known)`.
- One structured call through the existing `ILlmClient`: same Gemini model, same fixed seed, schema `{ answer: string, known: bool }`.
- System prompt: you are the tournament judge at the table; answer only from the fact sheet; never invent facts; answer briefly; if the fact sheet does not cover the question, answer "Not known." with `known = false`.
- Used only by the eval. The judge-facing console flow still asks the real person.

### `ScenarioEvalRunner` (changed)

- Takes a `SimulatedJudge` in its constructor.
- The `askJudge` callback calls `SimulatedJudge.AnswerAsync(scenario.FactSheet, question.Question)` and records each question with its answer.

### `ScenarioTrajectory` (changed)

- `AskedMoreQuestionsThanScripted` is removed.
- Adds the recorded questions and answers, and the count of "not known" answers.
- Rounds asked comes from the existing turn records.

## Scoring

- **"Answer budget" is removed, and rounds are not scored.** A round is one turn in which PokeJudge judged the facts insufficient and asked questions. Fair follow-up questions are not a failure, and a loop that never resolves is already caught: the clarification loop's 4-turn cap ends it with no ruling, which fails Final Source Support. Each run reports its rounds of questions. (Amended 2026-09-26: the first version scored rounds against a per-scenario `MaxClarifyingRounds`; a limit of 1 failed fair follow-ups, so it was dropped.)
- All other criteria are unchanged: initial retrieval, sufficiency timing, question materiality, post-answer retrieval, final Source Support, expected failure, expected unresolvable.
- "Not known" answers are reported, not scored.

## Output

- Each question prints with the judge's answer, marked `(not known)` when applicable.
- Each run prints "N of M questions not answerable from the fact sheet"; the summary prints the total.
- Each run prints its rounds of questions (reported, not scored).

## Errors

- A failed judge call (HTTP error, timeout) is an infrastructure failure, handled like a failed PokeJudge call today.
- A malformed judge response throws the same parser error as PokeJudge's own structured calls. It is never scored as PokeJudge's fault.

## Cost

About one extra call per question: a full run goes from about 120 to about 150 calls, around 11 minutes paced at 14 per minute on the free tier. $0.

## Scenario changes (approved)

Rulings made from the baseline run and the rule text:

| Scenario | Change | Reason |
|---|---|---|
| deck-not-shuffled | `SufficientOnFirstTurn` → `RequiresOneClarification`. Add `TCGTH-6.2.2` to expected sections. No post-answer sections. | TCGTH-6.2.2 only says it "may carry a penalty"; when it was noticed is material. |
| spectator-badges | Reword the description to "Do spectators need to wear a badge at a Regional Championship?" Stays `SufficientOnFirstTurn`. | The rule says Regionals and above; "large tournaments" was ambiguous. |
| drew-extra-card | Fact sheet: noticed later the same turn; the card can't be identified. | The old script ("several turns later") contradicted the description. |
| weakness-not-applied | The fact sheet says nothing else modified damage. | Its follow-up question about damage modifiers is fair. |
| supporter-twice | The fact sheet says the cards drawn by the second Supporter can't be identified. | PokeJudge's question decides whether the penalty can be lowered. |
| too-many-prizes | The fact sheet says the extra Prize card was set aside face down and can be returned. | Tests the de-escalation path. |

All other scenarios keep their expectations. Failures judged to be PokeJudge's, not the test's: proxy-cards (labels an explicit rule Partial), mulligan-not-taken (ties a correct question to an unrelated section), spectator-conduct (asks whether a stated spectator is a spectator).

`missed-prize` stays `ExpectedUnresolvable`. Once the judge answers its questions, PokeJudge might produce a ruling; the existing criterion would then flag it, which is useful information either way.

## Fact sheets

| Scenario | Fact sheet |
|---|---|
| notes | The competitor wants to keep hand-written notes about the current match during play. |
| proxy-cards | The cards are home-printed copies of real cards, used in place of the originals in a sanctioned tournament. |
| deck-not-shuffled | The opponent noticed while cutting the deck, before either player drew an opening hand. There is no sign it was deliberate. |
| spectator-badges | The event is a Regional Championship. The spectator is not playing. |
| repeat-violations | The competitor has received penalties for the same kind of infraction earlier in this event. |
| special-condition | The opponent's attack text says the Defending Pokémon is now Confused. The card was turned to show Asleep by mistake. No other effects applied. |
| missed-prize | A League Challenge. The player Knocked Out the opponent's Pokémon two turns ago and did not take a Prize card. They noticed it now. |
| drew-extra-card | The player drew one extra card during their draw step. No card effect caused it. It was noticed later the same turn. The extra card went into their hand and can't be told apart from the rest. |
| weakness-not-applied | The Defending Pokémon was in the Active position when it took the damage. The attack's base damage was 60. The Defending Pokémon has a printed Weakness to that attack's type (×2), but only 60 damage was placed on it. No Abilities, Tools or other effects modified the damage. |
| supporter-twice | The player being ruled on (the caller's opponent) played two copies of the Supporter card Judge in the same turn. Both fully resolved before anyone noticed, and several turns have passed. The cards drawn from the second Judge can't be identified. |
| gx-attack-twice | The player already used a GX attack earlier in this game with a different Pokémon-GX. |
| mulligan-not-taken | Neither player can recall for certain whether either had a Basic Pokémon in their opening hand. There is no way to verify it now. |
| late-to-round | The competitor arrived exactly 7 minutes after the round officially started. |
| deck-under-60 | Both the decklist and the physical deck contain only 58 cards, two short of the required 60. |
| ace-spec-count | Both the decklist and the physical deck contain two different ACE SPEC cards, Prime Catcher and Master Ball, and they match each other. The judge found this while reviewing the decklist, before either player drew an opening hand. |
| too-many-prizes | The Knocked Out Pokémon was an ordinary Pokémon, worth one Prize card. The player took two. The extra Prize card was set aside face down, separate from the hand, and can be returned. |
| prize-issue-vague | A player took a Prize card after what they believed was a Knock Out, but the Defending Pokémon still had HP remaining. It was never Knocked Out. |
| double-energy-attach | The player attached two Basic Energy cards from hand in the same turn. No card effect allowed the second attachment. |
| discard-shuffle-deescalate | The competitor shuffled their discard pile into their deck without a card effect. The discard pile was small, the game hasn't progressed past the first few turns, and both competitors agree on exactly which cards were in it. |
| spectator-conduct | The person is a spectator, not playing in any event. They were standing next to the match and talking loudly about the game state. |

## Testing

Written test-first:

- `SimulatedJudge`: the fact sheet and question reach the prompt; `known = false` returns "not known". Uses the existing fake `ILlmClient`.
- `ScenarioEvalRunner`: judge answers flow into the loop; questions, answers and "not known" counts are recorded.
- `ScenarioEvalScorer`: several rounds of questions followed by an acceptable ruling pass.
- `EvalDataset`: every scenario has a non-empty fact sheet.

Live check: one full 20-scenario run. Label every failure "PokeJudge was wrong" or "the test was wrong". Success means no "test was wrong" failures remain.

## Out of scope

- Any change to PokeJudge's pipeline or prompts (Steps 3 to 5).
- Scoring "not known" answers.
- Renaming `RequiresOneClarification` (it already means "one or more").
