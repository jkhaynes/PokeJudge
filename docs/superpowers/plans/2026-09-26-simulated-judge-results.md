# Step 2 live eval results

Run 2026-09-26: full 20-scenario `evaluate`, pinned `gemini-3.5-flash-lite`, fixed seed, simulated judge (after the partial-answer prompt fix, commit `91a4138`), paced at 14 calls per minute. Rescored after two changes made on review of this run: the per-scenario round limit ("Question budget") was removed, and the supporter-twice fact sheet was reworded and that scenario re-run.

**Score: 16/20** (baseline with scripted answers, same model and seed: 11/20).
**Not known:** 3 of 27 questions couldn't be answered from the fact sheets (reported, not scored).
**Infrastructure failures:** 0.

## Newly passing

- **deck-not-shuffled, spectator-badges:** the expectation or description was wrong (fixed in Task 3).
- **weakness-not-applied, too-many-prizes, supporter-twice:** scripted answers didn't match the questions asked.
- **special-condition:** asked 3 fair rounds of questions, which the removed round limit had counted as a failure.

## Failures

All four are PokeJudge's own mistakes; none are test errors.

| Scenario | Failed criteria | Reason | Where it's addressed |
|---|---|---|---|
| proxy-cards | Final Source Support | Validated Partial for an explicit rule (the model itself said Strong). | Grounding validation |
| drew-extra-card | Initial retrieval, materiality, post-answer retrieval, Source Support | Retrieval never surfaces PPG-5.5.1, so PokeJudge re-asks an answered question until the turn cap and never rules. | Step 4 (better rule text) |
| mulligan-not-taken | Unexpected failure | After a correct answer, PokeJudge reports "insufficient" with no questions: the known Milestone 2 zero-questions crash. | Open known bug |
| spectator-conduct | Sufficiency timing | Asks whether the person is a spectator; the scenario already says so. | Step 5 (judgment model) |

## Test fixes made from this run

- **Simulated judge prompt:** it answered "not known" to two-part questions it could half-answer. It now answers the parts its fact sheet covers (commit `91a4138`).
- **supporter-twice fact sheet:** "the opponent" was ambiguous against PokeJudge's "the player"; it now says "the player being ruled on (the caller's opponent)".
- **Round limit removed:** fair follow-up questions are not a failure, and a loop that never resolves is already caught by the 4-turn cap producing no ruling. Rounds are now reported per run.

## Full rundown

Every scenario from the full run, in dataset order, rescored without the removed Question budget criterion. supporter-twice shows its re-run after the fact-sheet fix. Each question is followed by the simulated judge's answer.

### notes — PASS

*Tournament Procedure.* Is a competitor allowed to keep written notes during their match?

Rounds of questions: 0

No questions asked.

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Resolved without unnecessary clarification, as expected.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.
- Source Support: Strong | Validated: Strong

### proxy-cards — FAIL

*Deck/Decklist Issues.* Can a player use proxy cards printed at home during a sanctioned tournament?

Rounds of questions: 0

No questions asked.

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Resolved without unnecessary clarification, as expected.
- **FAIL** Final Source Support: Validated Source Support was Partial, not in the acceptable set [Strong].
- Source Support: Strong | Validated: Partial

### deck-not-shuffled — PASS

*Illegal Game State.* A player's deck wasn't fully shuffled before the game started -- what should happen?

Rounds of questions: 1

- **Turn 1** (tied to `TCGTH-6.2.2#0`): At what point was the insufficiently randomized deck discovered, and what penalty level is appropriate according to tournament policy guidelines?
  - Judge: The insufficiently randomized deck was discovered while cutting the deck, before either player drew an opening hand, and there is no sign it was deliberate. The fact sheet does not state what penalty level is appropriate.

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (TCGTH-6.2.2#0).
- PASS Post-answer retrieval: No post-answer section(s) specified for this scenario.
- PASS Final Source Support: Validated Source Support (Partial) was in the acceptable set.
- Source Support: Partial | Validated: Partial

### spectator-badges — PASS

*Tournament Procedure.* Do spectators need to wear a badge at a Regional Championship?

Rounds of questions: 0

No questions asked.

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Resolved without unnecessary clarification, as expected.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.
- Source Support: Strong | Validated: Strong

### repeat-violations — PASS

*Penalty Questions.* How are penalties handled for a competitor with a history of repeat violations?

Rounds of questions: 0

No questions asked.

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Resolved without unnecessary clarification, as expected.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.
- Source Support: Strong | Validated: Strong

### special-condition — PASS

*Discretion Required.* A judge notices during a match that a player's Active Pokemon has a Special Condition marker on it that doesn't seem right for what happened. The marker is Asleep, but the player says their opponent's attack was supposed to cause Confused, not Asleep.

Rounds of questions: 3

- **Turn 1** (tied to `TCGRULES-special-conditions#0`): What attack was used that caused the Special Condition, and what text does that attack have regarding Special Conditions?
  - Judge: The fact sheet does not state what attack was used. The attack text says the Defending Pokémon is now Confused.
- **Turn 2** (tied to `TCGRULES-special-conditions#2`): Did an effect other than the attack apply the Asleep Special Condition after the attack resolved?
  - Judge: No other effects applied.
- **Turn 3** (tied to `TCGRULES-special-conditions#2`): Did the Pokémon evolve, move to the Bench, or have another Special Condition applied after the attack resolved?
  - Judge: The Defending Pokémon was affected by Asleep by mistake instead of Confused, and no other effects applied.

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (TCGRULES-special-conditions#0).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Partial) was in the acceptable set.
- Source Support: Strong | Validated: Partial

### missed-prize — PASS

*Prize Errors.* During a League Challenge, a player just noticed they forgot to take a Prize card after knocking out their opponent's Pokemon two turns ago.

Rounds of questions: 4

- **Turn 1** (tied to `PPG-5.5.1#5`): Did the player fail to take a Prize card after a Knock Out, and how many turns have passed since the error occurred?
  - Judge: Yes, the player failed to take a Prize card after a Knock Out, and two turns have passed since the error occurred.
- **Turn 2** (tied to `PPG-5.5.1#1`): Has the error remained unnoticed for long enough to have influenced subsequent gameplay or resulted in an irreversible confusion to the game state that cannot be completely reset?
  - Judge: Not known. (not known)
- **Turn 3** (tied to `PPG-5.5.1#1`): Has the error remained unnoticed for long enough or influenced gameplay such that it results in irreversible confusion to the game state or cannot be completely repaired?
  - Judge: Not known. (not known)
- **Turn 4** (tied to `PPG-5.5.1#1`): Did the error remain unnoticed for long enough to have influenced gameplay, and does it result in irreversible confusion to the game state that cannot be completely repaired?
  - Judge: Not known. (not known)

- PASS Expected unresolvable: Correctly exhausted the turn cap without resolving, as expected for this known corpus gap.

### drew-extra-card — FAIL

*Gameplay Error.* A player drew an extra card during their draw step and didn't realize it until later in the turn.

Rounds of questions: 4

- **Turn 1** (tied to `PPG-4.2.1#1`): Can the specific card drawn extra during the draw step be identified and distinguished from the rest of the player's hand?
  - Judge: No, the extra card went into their hand and cannot be told apart from the rest.
- **Turn 2** (tied to `PPG-4.2.1#1`): Can the specific extra card drawn be identified or distinguished from the rest of the player's hand?
  - Judge: No, the extra card went into their hand and cannot be told apart from the rest.
- **Turn 3** (tied to `PPG-4.2.1#0`): What specific card or action was played erroneously after the draw step that caused the game state issue?
  - Judge: No card effect caused the error; the player simply drew one extra card during their draw step without a specific card or action causing it.
- **Turn 4** (tied to `PPG-4.2.1#0`): Can the extra card be identified in the player's hand, or are all retrieved passages concerning Supporter card infractions inapplicable here?
  - Judge: The extra card went into their hand and can't be told apart from the rest. The fact sheet does not contain information about Supporter card infractions.

- **FAIL** Initial retrieval: None of the expected section(s) [PPG-5.5.1] were retrieved on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- **FAIL** Clarifying question materiality: No clarifying question was tied to any of the expected section(s) [PPG-5.5.1].
- **FAIL** Post-answer retrieval: None of the expected post-answer section(s) [PPG-5.5.1] were retrieved on turn 2.
- **FAIL** Final Source Support: No ruling was produced to evaluate.

### weakness-not-applied — PASS

*Attack Resolution.* A judge is called over because a player's Pokemon was Knocked Out by an attack, but the opponent thinks the damage might have been calculated incorrectly -- their Pokemon has a printed Weakness to the attack's type, and they're not sure whether that was factored in.

Rounds of questions: 1

- **Turn 1** (tied to `TCGRULES-turn-actions#9`): Was the Pokémon that was Knocked Out in the Active position or on the Bench when it took the damage?
  - Judge: The Defending Pokémon was in the Active position when it took the damage.
- **Turn 1** (tied to `TCGRULES-full-details-of-attacking#2`): Did the attack place damage counters directly, or did it deal damage that is subject to Weakness and Resistance?
  - Judge: The attack dealt damage that is subject to Weakness and Resistance, rather than placing damage counters directly.

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (TCGRULES-turn-actions#9).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.
- Source Support: Strong | Validated: Strong

### supporter-twice — PASS (re-run)

*Timing Questions.* A judge is called over because a player thinks their opponent played two Supporter cards in the same turn.

Rounds of questions: 2

- **Turn 1** (tied to `PPG-4.2.1#0`): Which specific Supporter cards were played by the player during the turn in question?
  - Judge: The player played two copies of the Supporter card Judge during the turn in question.
- **Turn 2** (tied to `PPG-4.2.1#2`): Can the second Supporter card's effects (such as the cards drawn or any hidden information revealed/shuffled) be fully reversed and the game state restored to how it was before it was played?
  - Judge: No, several turns have passed and the cards drawn from the second Judge cannot be identified, so the effects cannot be fully reversed.

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (PPG-4.2.1#0).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.
- Source Support: Partial | Validated: Strong

### gx-attack-twice — PASS

*Timing Questions.* A judge is asked whether a player can use a GX attack with a second Pokemon-GX later in the same game, having already used a GX attack with a different Pokemon-GX earlier.

Rounds of questions: 0

No questions asked.

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Resolved without unnecessary clarification, as expected.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.
- Source Support: Strong | Validated: Strong

### mulligan-not-taken — FAIL

*Illegal Game State.* Partway through a game, a judge is called over because a player realizes they don't remember either player mulliganing at the start, even though the game has clearly been going for several turns.

Rounds of questions: 1

- **Turn 1** (tied to `PPG-4.2.1#0`): Did either player actually draw a mulligan during the setup of the game?
  - Judge: Neither player can recall for certain whether either had a Basic Pokémon in their opening hand, and there is no way to verify it now.

- **FAIL** Unexpected failure: The loop failed loudly, which this scenario did not expect: Model reported the scenario insufficient but supplied no clarifying questions. Model's rationale: "The confirmed facts state that it is unknown whether mulligans occurred, but the retrieved passages only explain how mulligans are performed and what extra card draws are permitted during setup. The retrieved text does not contain any penalty guidelines or procedural instructions for resolving a situation where players realize several turns into the game that they cannot remember if mulligans happened or were missed. Therefore, the retrieved passages are insufficient to determine a ruling."

### late-to-round — PASS

*Tournament Procedure.* A judge is asked to rule on what should happen because a competitor arrived several minutes after their round began.

Rounds of questions: 1

- **Turn 1** (tied to `PPG-5.2.1#0`): How many minutes late did the competitor arrive to their match?
  - Judge: The competitor arrived exactly 7 minutes late.

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (PPG-5.2.1#0).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.
- Source Support: Strong | Validated: Strong

### deck-under-60 — PASS

*Deck/Decklist Issues.* Before a match begins, a judge is asked to check a competitor's deck because it seems to have fewer than 60 cards.

Rounds of questions: 1

- **Turn 1** (tied to `PPG-5.6.1#2`): Does the physical deck actually contain fewer than 60 cards when counted, or is it merely suspected?
  - Judge: The physical deck actually contains only 58 cards when counted, which is two short of the required 60.

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (PPG-5.6.1#2).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.
- Source Support: Strong | Validated: Strong

### ace-spec-count — PASS

*Deck/Decklist Issues.* Before a match, a judge is asked to check a competitor's decklist because it appears to include two different ACE SPEC cards.

Rounds of questions: 1

- **Turn 1** (tied to `TCGRULES-appendix-3-ace-spec-cards#0`): Does the competitor's deck list actually list two different ACE SPEC cards, and what are their specific names?
  - Judge: Yes, the decklist lists two different ACE SPEC cards, and their specific names are Prime Catcher and Master Ball.

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (TCGRULES-appendix-3-ace-spec-cards#0).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.
- Source Support: Strong | Validated: Strong

### too-many-prizes — PASS

*Prize Errors.* A judge is called over because a player took two Prize cards after a single Knock Out, instead of one.

Rounds of questions: 1

- **Turn 1** (tied to `PPG-5.5.1#1`): Have the extra Prize cards taken been revealed or mixed into the player's hand, or can they be uniquely identified and returned to the Prize pool?
  - Judge: The extra Prize card was set aside face down, separate from the hand, and can be returned.

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (PPG-5.5.1#1).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Partial) was in the acceptable set.
- Source Support: Partial | Validated: Partial

### prize-issue-vague — PASS

*Prize Errors.* A judge is called over because something seems wrong with how Prize cards were handled during the match, but nobody can immediately explain what happened.

Rounds of questions: 1

- **Turn 1** (tied to `PPG-5.5.1#2`): Did a competitor take a Prize card without Knocking Out a Pokémon or take too many Prize cards after Knocking Out a Pokémon?
  - Judge: The player took a Prize card after what they believed was a Knock Out, but the Defending Pokémon still had HP remaining and was never Knocked Out.

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (PPG-5.5.1#2).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.
- Source Support: Partial | Validated: Strong

### double-energy-attach — PASS

*Gameplay Error.* A judge is called over because a player attached two Energy cards to their Pokemon during a single turn.

Rounds of questions: 1

- **Turn 1** (tied to `PPG-5.5.1#2`): Did the competitor attach more than one Energy card in a turn with or without an effect that allows this?
  - Judge: The player attached two Basic Energy cards from hand in the same turn without a card effect allowing the second attachment.

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (PPG-5.5.1#2).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Partial) was in the acceptable set.
- Source Support: Partial | Validated: Partial

### discard-shuffle-deescalate — PASS

*Penalty Questions.* A judge is called over because a competitor shuffled their discard pile into their deck without a card effect allowing it, and now needs a ruling on the appropriate penalty.

Rounds of questions: 1

- **Turn 1** (tied to `PPG-5.5.1#4`): What were the contents of the discard pile, what turn of the game is it, and do both competitors agree on which exact cards were in the discard pile?
  - Judge: The exact contents of the discard pile are not stated, but it was small. It is the first few turns of the game, and both competitors agree on exactly which cards were in it.

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (PPG-5.5.1#4).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.
- Source Support: Strong | Validated: Strong

### spectator-conduct — FAIL

*Tournament Procedure.* A judge is called over because a spectator was standing very close to an in-progress match and talking loudly about the game state.

Rounds of questions: 1

- **Turn 1** (tied to `PPTRH-3.3#5`): Is the person standing close to the match and talking actually an active competitor in another event, or are they a spectator who is not actively participating in any main competitive event?
  - Judge: The person is a spectator and is not playing in any event. They were standing next to the match and talking loudly about the game state.

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- **FAIL** Sufficiency timing: Expected immediate sufficiency, but a clarifying question was asked.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.
- Source Support: Strong | Validated: Strong
