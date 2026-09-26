# Step 2 live eval results

Final run 2026-09-26: full 20-scenario `evaluate` with all Step 2 changes (simulated judge with the partial-answer prompt fix, reworded supporter-twice fact sheet, no round limit), pinned `gemini-3.5-flash-lite`, fixed seed, paced at 14 calls per minute.

**Score: 13 of the 16 scenarios that ran** (baseline with scripted answers, same model and seed: 11/20).
**Not run:** 4 scenarios hit the Gemini free tier's daily limit of 500 requests per model (prize-issue-vague, double-energy-attach, discard-shuffle-deescalate, spectator-conduct). In the earlier full run today, the first three passed and spectator-conduct failed (see below). They need a re-run after the quota resets.
**Not known:** 3 of 21 questions couldn't be answered from the fact sheets (reported, not scored).

## Failures

All are PokeJudge's own mistakes; none are test errors.

| Scenario | Failed criteria | Reason | Where it's addressed |
|---|---|---|---|
| proxy-cards | Final Source Support | Validated Partial for an explicit rule (the model itself said Strong). | Grounding validation |
| drew-extra-card | Initial retrieval, materiality, post-answer retrieval, Source Support | Retrieval never surfaces PPG-5.5.1, so PokeJudge re-asks an answered question until the turn cap and never rules. | Step 4 (better rule text) |
| mulligan-not-taken | Unexpected failure | After a correct answer, PokeJudge reports "insufficient" with no questions: the known Milestone 2 zero-questions crash. | Open known bug |
| spectator-conduct (earlier run) | Sufficiency timing | Asks whether the person is a spectator; the scenario already says so. | Step 5 (judgment model) |

## Test fixes made during Step 2

- **Simulated judge prompt:** it answered "not known" to two-part questions it could half-answer. It now answers the parts its fact sheet covers (commit `91a4138`).
- **supporter-twice fact sheet:** "the opponent" was ambiguous against PokeJudge's "the player"; it now says "the player being ruled on (the caller's opponent)".
- **Round limit removed:** fair follow-up questions are not a failure, and a loop that never resolves is already caught by the 4-turn cap producing no ruling. Rounds are now reported per run.
- **Rulings printed:** `evaluate` now prints each full ruling, using the same output as the judge-facing flow, so passing scores can be checked against what PokeJudge actually said.

## Open question

**missed-prize may pass for the wrong reason.** It is expected to be unresolvable (Milestone 8.5 judged the rulebooks don't cover a forgotten Prize). But PokeJudge's later questions ask whether the error influenced gameplay or left the game state unrepairable, tied to PPG-5.5.1, and the judge answers "not known" because the fact sheet doesn't say. It may be the thin fact sheet, not a rulebook gap, that keeps it from ruling.

## To do next session (after the Gemini daily quota resets, midnight Pacific)

The free tier allows 500 requests per model per day; a full run uses about 150, a single scenario about 5 to 25.

- [ ] **Re-run the 4 scenarios that hit the quota**: prize-issue-vague, double-energy-attach, discard-shuffle-deescalate, spectator-conduct (`evaluate --only <id>`). Add their rulings to the rundown below and update the score.
- [ ] **Run the rewritten mulligan scenario** (`evaluate --only missed-mulligan-draws`). It replaced mulligan-not-taken after this run, so the rundown below still shows the old one. Retrieval already checks out: `TCGTH-7.4.1` and both `TCGRULES-full-details-of-taking-a-mulligan` excerpts are the top 3 search results. Correct ruling, confirmed by the user: **Player B does not get the draws** (they're optional and belong to setup, which has passed). Check the ruling against that, and swap the rundown entry.
- [ ] **drew-extra-card experiment: does it resolve with more rounds?** Throwaway, not committed: temporarily raise the clarification loop's turn cap in `RunScenarioEval` from 4 to 7 (`new ClarificationLoop(llmClient, retriever, maxTurns: 7)`), run `evaluate --only drew-extra-card`, record whether it ever retrieves `PPG-5.5.1` and reaches a ruling, then revert. Expected: it keeps retrieving the `PPG-4.2.1` Supporter excerpts and repeats itself, which would confirm a retrieval problem for Step 4.
- [ ] **Decide on missed-prize**: try adding "nothing since depended on the Prize count, and the game can be corrected" to its fact sheet and see whether PokeJudge rules (see Open question above), or leave it as is.
- [ ] **Finish Step 2**: code review (superpowers:requesting-code-review), then open the PR (superpowers:finishing-a-development-branch). Optional: `/learning-checkpoint` before the PR.
- [ ] **After merge**: update Step 2 in the PokeJudge improvement plan doc to match what shipped (simulated judge, no round limit, 16/20 so far).

## Full rundown

Every scenario, in dataset order. For each: the scenario, the facts the simulated judge answered from, every question PokeJudge asked with the judge's answer, PokeJudge's full ruling, and the scored checks.

### notes — PASS

**Scenario** (Tournament Procedure): Is a competitor allowed to keep written notes during their match?

**Facts the simulated judge answered from:** The competitor wants to keep hand-written notes about the current match during play.

**Questions** (0 round(s)):

None asked.

**Ruling:**

```text
Recommendation: Recommend to the judge that competitors are permitted to take written notes during a match and refer to them at any time during that match, provided they follow all applicable guidelines (such as starting with a blank sheet that is free of text or prewritten information, being timely, not using a message-sending device, and not using codes or obscuring meanings).
Model's own assessment (unvalidated): Strong — The retrieved passages directly and comprehensively address note-taking rules, permissions, and restrictions during matches.
Validated Source Support: Strong — Every citation exists, all facts were confirmed sufficient, and every cited passage explicitly supports its claim with no conflicts.

Explanation: Competitors are explicitly permitted to take written notes during a match and refer to those notes at any time during that match. However, the note sheet must be completely free of text or prewritten information at the start of each match, competitors must be timely, and they may not use a device that can send or receive messages. Furthermore, notes cannot use codes, ciphers, or abbreviations to obscure meaning, and competitors may not refer to notes from previous rounds.

Cited chunk IDs: TCGTH-7.4.6#0, PPTRH-3.2#6, TCGTH-7.4.6#1, PPTRH-3.2#5

Citation grounding breakdown:
  [TCGTH-7.4.6#0] ExplicitSupport
  [PPTRH-3.2#6] ExplicitSupport
  [TCGTH-7.4.6#1] ExplicitSupport
  [PPTRH-3.2#5] ExplicitSupport
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Resolved without unnecessary clarification, as expected.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.

### proxy-cards — FAIL

**Scenario** (Deck/Decklist Issues): Can a player use proxy cards printed at home during a sanctioned tournament?

**Facts the simulated judge answered from:** The cards are home-printed copies of real cards, used in place of the originals in a sanctioned tournament.

**Questions** (0 round(s)):

None asked.

**Ruling:**

```text
Recommendation: It is recommended that the judge disallow home-printed proxy cards. Only genuine Pokémon Trading Card Game cards may be used at Play! Pokémon events, unless a judge or organizer issues an official proxy card, which is restricted solely to specific situations involving cards damaged during a tournament.
Model's own assessment (unvalidated): Strong — The retrieved passages explicitly state that only genuine cards may be used unless a proxy is issued by a judge/organizer, and that proxies may not be used for any other reason and must be treated as counterfeit if discovered.
Validated Source Support: Partial — Every citation exists and none is unsupported, but at least one requires interpretation or judge discretion, or the retrieved passages conflict.

Explanation: Only genuine Pokémon Trading Card Game cards may be used at Play! Pokémon events, unless issued a proxy card by a Judge or Organizer. Proxies may not be used at Play! Pokémon tournaments for any other reason and should be treated as any other counterfeit card if discovered. A judge may only create a proxy if a card becomes damaged during a tournament in a way that results in it becoming marked.

Repair steps:
  - Instruct the player to remove the home-printed proxy cards from their deck.
  - Require the player to replace any non-genuine or illegal cards with genuine, legal copies of the cards.

Penalty guidance: Refer to applicable deck legality infractions if non-genuine cards are found in the deck.

Cited chunk IDs: TCGTH-2.4#0, TCGTH-2.4#1, TCGTH-2#0, PPG-5.6.1#0

Citation grounding breakdown:
  [TCGTH-2.4#0] ExplicitSupport
  [TCGTH-2.4#1] ExplicitSupport
  [TCGTH-2#0] ExplicitSupport
  [PPG-5.6.1#0] Interpretation
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Resolved without unnecessary clarification, as expected.
- **FAIL** Final Source Support: Validated Source Support was Partial, not in the acceptable set [Strong].

### deck-not-shuffled — PASS

**Scenario** (Illegal Game State): A player's deck wasn't fully shuffled before the game started -- what should happen?

**Facts the simulated judge answered from:** The opponent noticed while cutting the deck, before either player drew an opening hand. There is no sign it was deliberate.

**Questions** (1 round(s)):

- **Turn 1** (tied to `TCGTH-6.2.2#0`): At what point was the insufficiently randomized deck discovered, and what penalty level is appropriate according to tournament policy guidelines?
  - *Judge:* The insufficiently randomized deck was discovered while cutting the deck, before either player drew an opening hand, and there is no sign it was deliberate. The fact sheet does not state what penalty level is appropriate.

**Ruling:**

```text
Recommendation: It is recommended that the judge require the deck to be fully randomized and allow the match to proceed, as the issue was discovered before either player drew an opening hand and without signs of deliberation.
Model's own assessment (unvalidated): Partial — The retrieved passages confirm that decks must be randomized at the start of a game and that insufficiently randomizing a deck is a violation, but the passages do not explicitly prescribe the exact repair steps or penalty level for this specific scenario.
Validated Source Support: Partial — Every citation exists and none is unsupported, but at least one requires interpretation or judge discretion, or the retrieved passages conflict.

Explanation: Insufficiently randomizing the deck is a rules violation. However, the retrieved passages state that each competitor's deck is expected to be fully randomized at the start of each game, and the issue was caught while cutting the deck and before either player drew an opening hand, with no sign that it was deliberate.

Repair steps:
  - Instruct the competitor to fully randomize their deck.

Cited chunk IDs: TCGTH-6.2.2#0, TCGTH-6.2#0

Citation grounding breakdown:
  [TCGTH-6.2.2#0] ExplicitSupport
  [TCGTH-6.2#0] Interpretation
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (TCGTH-6.2.2#0).
- PASS Post-answer retrieval: No post-answer section(s) specified for this scenario.
- PASS Final Source Support: Validated Source Support (Partial) was in the acceptable set.

### spectator-badges — PASS

**Scenario** (Tournament Procedure): Do spectators need to wear a badge at a Regional Championship?

**Facts the simulated judge answered from:** The event is a Regional Championship. The spectator is not playing.

**Questions** (0 round(s)):

None asked.

**Ruling:**

```text
Recommendation: It is recommended that the judge advise that spectators do need to wear a badge at a Regional Championship, as Regional Championships are badged events requiring a pass, and attendees are required to wear their badge in a visible location during their attendance at the event.
Model's own assessment (unvalidated): Strong — The retrieved text explicitly states that Regional Championships are badged events and that attendees must wear their badge in a visible location during the entirety of their attendance at the event.
Validated Source Support: Strong — Every citation exists, all facts were confirmed sufficient, and every cited passage explicitly supports its claim with no conflicts.

Explanation: According to the retrieved passages, events at the level of Regional Championships are badged events that require a prepurchased pass to enter. Furthermore, individuals are required to wear their badge in a visible location during the entirety of their participation in or attendance at the event and other badged locations.

Cited chunk IDs: PPTRH-2.4#0

Citation grounding breakdown:
  [PPTRH-2.4#0] ExplicitSupport
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Resolved without unnecessary clarification, as expected.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.

### repeat-violations — PASS

**Scenario** (Penalty Questions): How are penalties handled for a competitor with a history of repeat violations?

**Facts the simulated judge answered from:** The competitor has received penalties for the same kind of infraction earlier in this event.

**Questions** (0 round(s)):

None asked.

**Ruling:**

```text
Recommendation: It is recommended that the judge consider escalating the penalty for subsequent infractions if a competitor repeats violations, as one of the purposes of penalties is to educate and reinforce adherence to tournament rules.
Model's own assessment (unvalidated): Strong — The retrieved passage PPG-4.2.2 directly addresses how repeat violations are handled by stating that it may be appropriate to escalate the penalty for subsequent infractions.
Validated Source Support: Strong — Every citation exists, all facts were confirmed sufficient, and every cited passage explicitly supports its claim with no conflicts.

Explanation: According to PPG-4.2.2#0, if infractions are repeated, it may be appropriate to escalate the penalty for each subsequent infraction to further reinforce the necessity of adhering to Play! Pokemon tournament rules, with the purpose of educating the competitor and reminding them to take extra care.

Penalty guidance: Escalate the penalty for each subsequent infraction when infractions are repeated.

Cited chunk IDs: PPG-4.2.2#0

Citation grounding breakdown:
  [PPG-4.2.2#0] ExplicitSupport
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Resolved without unnecessary clarification, as expected.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.

### special-condition — PASS

**Scenario** (Discretion Required): A judge notices during a match that a player's Active Pokemon has a Special Condition marker on it that doesn't seem right for what happened. The marker is Asleep, but the player says their opponent's attack was supposed to cause Confused, not Asleep.

**Facts the simulated judge answered from:** The opponent's attack text says the Defending Pokémon is now Confused. The card was turned to show Asleep by mistake. No other effects applied.

**Questions** (3 round(s)):

- **Turn 1** (tied to `TCGRULES-special-conditions#0`): What attack was used that caused the Special Condition, and what text does that attack have regarding Special Conditions?
  - *Judge:* The fact sheet does not state what attack was used. The attack text says the Defending Pokémon is now Confused.
- **Turn 2** (tied to `TCGRULES-special-conditions#2`): Did an effect other than the attack apply the Asleep Special Condition after the attack resolved?
  - *Judge:* No other effects applied.
- **Turn 3** (tied to `TCGRULES-special-conditions#2`): Did the Pokémon evolve, move to the Bench, or have another Special Condition applied after the attack resolved?
  - *Judge:* The Defending Pokémon was affected by Asleep by mistake instead of Confused, and no other effects applied.

**Ruling:**

```text
Recommendation: I recommend that the judge correct the game state by replacing the Asleep marker with the Confused Special Condition, aligning the marker with the attack text which states the Defending Pokémon is now Confused.
Model's own assessment (unvalidated): Strong — The retrieved passages describe Special Conditions including Confused and Asleep, and the confirmed facts directly state that Confused was the intended effect of the attack that was mistakenly marked as Asleep with no subsequent effects intervening.
Validated Source Support: Partial — Every citation exists and none is unsupported, but at least one requires interpretation or judge discretion, or the retrieved passages conflict.

Explanation: The confirmed facts establish that the attack text says the Defending Pokémon is now Confused, that the Defending Pokémon was affected by Asleep by mistake instead of Confused, and that no other effects applied after the attack resolved. Therefore, the incorrect Asleep marker should be replaced to match the correct application of the Confused Special Condition as dictated by the attack.

Repair steps:
  - Remove the Asleep marker (or adjust the card orientation) from the affected Pokémon.
  - Apply the Confused Special Condition to the Pokémon (turning the card with the top pointed toward the player, as specified for Confused).

Cited chunk IDs: TCGRULES-special-conditions#0, TCGRULES-special-conditions#2, TCGRULES-special-conditions#3

Citation grounding breakdown:
  [TCGRULES-special-conditions#0] Interpretation
  [TCGRULES-special-conditions#2] Interpretation
  [TCGRULES-special-conditions#3] Interpretation
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (TCGRULES-special-conditions#0).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Partial) was in the acceptable set.

### missed-prize — PASS

**Scenario** (Prize Errors): During a League Challenge, a player just noticed they forgot to take a Prize card after knocking out their opponent's Pokemon two turns ago.

**Facts the simulated judge answered from:** A League Challenge. The player Knocked Out the opponent's Pokémon two turns ago and did not take a Prize card. They noticed it now.

**Questions** (4 round(s)):

- **Turn 1** (tied to `PPG-5.5.1#5`): Did the player fail to take a Prize card after a Knock Out, and how many turns have passed since the error occurred?
  - *Judge:* Yes, the player failed to take a Prize card after a Knock Out, and two turns have passed since the error occurred.
- **Turn 2** (tied to `PPG-5.5.1#1`): Has the error remained unnoticed for long enough to have influenced subsequent gameplay or resulted in an irreversible confusion to the game state that cannot be completely reset?
  - *Judge:* Not known. (not known)
- **Turn 3** (tied to `PPG-5.5.1#1`): Has the error remained unnoticed for long enough or influenced gameplay such that it results in irreversible confusion to the game state or cannot be completely repaired?
  - *Judge:* Not known. (not known)
- **Turn 4** (tied to `PPG-5.5.1#1`): Did the error remain unnoticed for long enough to have influenced gameplay, and does it result in irreversible confusion to the game state that cannot be completely repaired?
  - *Judge:* Not known. (not known)

**Ruling:**

No ruling produced (turn cap exhausted).

**Checks:**

- PASS Expected unresolvable: Correctly exhausted the turn cap without resolving, as expected for this known corpus gap.

### drew-extra-card — FAIL

**Scenario** (Gameplay Error): A player drew an extra card during their draw step and didn't realize it until later in the turn.

**Facts the simulated judge answered from:** The player drew one extra card during their draw step. No card effect caused it. It was noticed later the same turn. The extra card went into their hand and can't be told apart from the rest.

**Questions** (4 round(s)):

- **Turn 1** (tied to `PPG-4.2.1#1`): Can the specific card drawn extra during the draw step be identified and distinguished from the rest of the player's hand?
  - *Judge:* No, the extra card went into their hand and cannot be told apart from the rest.
- **Turn 2** (tied to `PPG-4.2.1#1`): Can the specific extra card drawn be identified or distinguished from the rest of the player's hand?
  - *Judge:* No, the extra card went into their hand and cannot be told apart from the rest.
- **Turn 3** (tied to `PPG-4.2.1#0`): What specific card or action was played erroneously after the draw step that caused the game state issue?
  - *Judge:* No card effect caused the error; the player simply drew one extra card during their draw step without a specific card or action causing it.
- **Turn 4** (tied to `PPG-4.2.1#0`): Can the extra card be identified in the player's hand, or are all retrieved passages concerning Supporter card infractions inapplicable here?
  - *Judge:* The extra card went into their hand and can't be told apart from the rest. The fact sheet does not contain information about Supporter card infractions.

**Ruling:**

No ruling produced (turn cap exhausted).

**Checks:**

- **FAIL** Initial retrieval: None of the expected section(s) [PPG-5.5.1] were retrieved on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- **FAIL** Clarifying question materiality: No clarifying question was tied to any of the expected section(s) [PPG-5.5.1].
- **FAIL** Post-answer retrieval: None of the expected post-answer section(s) [PPG-5.5.1] were retrieved on turn 2.
- **FAIL** Final Source Support: No ruling was produced to evaluate.

### weakness-not-applied — PASS

**Scenario** (Attack Resolution): A judge is called over because a player's Pokemon was Knocked Out by an attack, but the opponent thinks the damage might have been calculated incorrectly -- their Pokemon has a printed Weakness to the attack's type, and they're not sure whether that was factored in.

**Facts the simulated judge answered from:** The Defending Pokémon was in the Active position when it took the damage. The attack's base damage was 60. The Defending Pokémon has a printed Weakness to that attack's type (×2), but only 60 damage was placed on it. No Abilities, Tools or other effects modified the damage.

**Questions** (1 round(s)):

- **Turn 1** (tied to `TCGRULES-turn-actions#9`): Was the Pokémon that was Knocked Out in the Active position or on the Bench when it took the damage?
  - *Judge:* The Defending Pokémon was in the Active position when it took the damage.
- **Turn 1** (tied to `TCGRULES-full-details-of-attacking#2`): Did the attack place damage counters directly, or did it deal damage that is subject to Weakness and Resistance?
  - *Judge:* The attack dealt damage that is subject to Weakness and Resistance, rather than placing damage counters directly.

**Ruling:**

```text
Recommendation: It is recommended that the judge verify whether Weakness was correctly factored into the damage calculation for the Defending Pokémon, as the rules state that damage should be increased by the amount next to the opponent's Active Pokémon's Weakness when it has Weakness to the attacker's type while in the Active position.
Model's own assessment (unvalidated): Strong — The retrieved passages directly address how Weakness applies to Active Pokémon during damage calculation, matching the confirmed facts of the scenario.
Validated Source Support: Strong — Every citation exists, all facts were confirmed sufficient, and every cited passage explicitly supports its claim with no conflicts.

Explanation: According to the rules, when an attack does damage, the opponent's Active Pokémon takes more damage if it has Weakness to the attacker's type. Specifically, step 3 of figuring out attack damage requires increasing the damage by the amount next to the opponent's Active Pokémon's Weakness if it has Weakness to the attacking Pokémon's type.

Cited chunk IDs: TCGRULES-full-details-of-attacking#3, TCGRULES-turn-actions#9, TCGRULES-turn-actions#8

Citation grounding breakdown:
  [TCGRULES-full-details-of-attacking#3] ExplicitSupport
  [TCGRULES-turn-actions#9] ExplicitSupport
  [TCGRULES-turn-actions#8] ExplicitSupport
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (TCGRULES-turn-actions#9).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.

### supporter-twice — PASS

**Scenario** (Timing Questions): A judge is called over because a player thinks their opponent played two Supporter cards in the same turn.

**Facts the simulated judge answered from:** The player being ruled on (the caller's opponent) played two copies of the Supporter card Judge in the same turn. Both fully resolved before anyone noticed, and several turns have passed. The cards drawn from the second Judge can't be identified.

**Questions** (2 round(s)):

- **Turn 1** (tied to `PPG-4.2.1#0`): Which specific Supporter cards were played by the player during the turn in question?
  - *Judge:* The player played two copies of the Supporter card Judge during the turn in question.
- **Turn 2** (tied to `PPG-4.2.1#2`): Can the second Supporter card's effects (such as the cards drawn or any hidden information revealed/shuffled) be fully reversed and the game state restored to how it was before it was played?
  - *Judge:* No, several turns have passed and the cards drawn from the second Judge cannot be identified, so the effects cannot be fully reversed.

**Ruling:**

```text
Recommendation: I recommend that the judge assess a Game Loss for playing two Supporter cards in the same turn when the game state cannot be repaired and the error is caught several turns later.
Model's own assessment (unvalidated): Partial — The retrieved passages discuss examples of playing two Supporter cards in a single turn and the resulting penalties based on game state correction, but specific details matching the exact sequence of a second Judge card played several turns prior are not explicitly detailed beyond general guidance on irreparable game states.
Validated Source Support: Strong — Every citation exists, all facts were confirmed sufficient, and every cited passage explicitly supports its claim with no conflicts.

Explanation: According to the retrieved passages, when an erroneous action results in a game state that is irreparably broken (such as when cards drawn cannot be identified or hands are shuffled and cannot be determined), and no corrective action can be taken, the appropriate penalty is a Game Loss because no other penalty can satisfactorily mitigate the damage done to the game state.

Penalty guidance: Game Loss

Cited chunk IDs: PPG-4.2.1#4

Citation grounding breakdown:
  [PPG-4.2.1#4] ExplicitSupport
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (PPG-4.2.1#0).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.

### gx-attack-twice — PASS

**Scenario** (Timing Questions): A judge is asked whether a player can use a GX attack with a second Pokemon-GX later in the same game, having already used a GX attack with a different Pokemon-GX earlier.

**Facts the simulated judge answered from:** The player already used a GX attack earlier in this game with a different Pokémon-GX.

**Questions** (0 round(s)):

None asked.

**Ruling:**

```text
Recommendation: Recommend to the player that they cannot use a second GX attack later in the game.
Model's own assessment (unvalidated): Strong — The retrieved passage explicitly states that a player cannot use more than one GX attack during an entire game, directly answering the question with no conflicts.
Validated Source Support: Strong — Every citation exists, all facts were confirmed sufficient, and every cited passage explicitly supports its claim with no conflicts.

Explanation: According to the rules for Pokemon-GX, a player cannot use more than one GX attack during an entire game, regardless of how many Pokemon-GX they play.

Cited chunk IDs: TCGRULES-appendix-19-pok-mon-gx#0

Citation grounding breakdown:
  [TCGRULES-appendix-19-pok-mon-gx#0] ExplicitSupport
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Resolved without unnecessary clarification, as expected.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.

### mulligan-not-taken — FAIL

**Scenario** (Illegal Game State): Partway through a game, a judge is called over because a player realizes they don't remember either player mulliganing at the start, even though the game has clearly been going for several turns.

**Facts the simulated judge answered from:** Neither player can recall for certain whether either had a Basic Pokémon in their opening hand. There is no way to verify it now.

**Questions** (1 round(s)):

- **Turn 1** (tied to `PPG-4.2.1#0`): Did either player actually draw a mulligan during the setup of the game?
  - *Judge:* Neither player can recall for certain whether either had a Basic Pokémon in their opening hand, and there is no way to verify it now.

**Ruling:**

No ruling produced (failed loudly).

**Checks:**

- **FAIL** Unexpected failure: The loop failed loudly, which this scenario did not expect: Model reported the scenario insufficient but supplied no clarifying questions. Model's rationale: "The confirmed facts state that it is unknown whether mulligans occurred, but the retrieved passages only explain how mulligans are performed and what extra card draws are permitted during setup. The retrieved text does not contain any penalty guidelines or procedural instructions for resolving a situation where players realize several turns into the game that they cannot remember if mulligans happened or were missed. Therefore, the retrieved passages are insufficient to determine a ruling."

### late-to-round — PASS

**Scenario** (Tournament Procedure): A judge is asked to rule on what should happen because a competitor arrived several minutes after their round began.

**Facts the simulated judge answered from:** The competitor arrived exactly 7 minutes after the round officially started.

**Questions** (1 round(s)):

- **Turn 1** (tied to `PPG-5.2.1#0`): How many minutes late did the competitor arrive to their match?
  - *Judge:* The competitor arrived exactly 7 minutes late.

**Ruling:**

```text
Recommendation: It is recommended that the judge issue a penalty for major tardiness to the competitor who arrived 7 minutes late.
Model's own assessment (unvalidated): Strong — The retrieved passages explicitly define major tardiness as arriving between 5 to 10 minutes late and list the corresponding penalty as a Game Loss.
Validated Source Support: Strong — Every citation exists, all facts were confirmed sufficient, and every cited passage explicitly supports its claim with no conflicts.

Explanation: According to PPG-5.2.1, major tardiness includes when a competitor arrives between 5 to 10 minutes late for their match. Because the competitor arrived exactly 7 minutes late, this falls into the major tardiness category, which carries a Major penalty of a Game Loss as outlined in the same section.

Penalty guidance: Major: Game Loss

Cited chunk IDs: PPG-5.2.1#0

Citation grounding breakdown:
  [PPG-5.2.1#0] ExplicitSupport
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (PPG-5.2.1#0).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.

### deck-under-60 — PASS

**Scenario** (Deck/Decklist Issues): Before a match begins, a judge is asked to check a competitor's deck because it seems to have fewer than 60 cards.

**Facts the simulated judge answered from:** Both the decklist and the physical deck contain only 58 cards, two short of the required 60.

**Questions** (1 round(s)):

- **Turn 1** (tied to `PPG-5.6.1#2`): Does the physical deck actually contain fewer than 60 cards when counted, or is it merely suspected?
  - *Judge:* The physical deck actually contains only 58 cards when counted, which is two short of the required 60.

**Ruling:**

```text
Recommendation: It is recommended that the judge make the deck list legal by adding an appropriate number of Basic Energy cards of the competitor's choice, update the physical deck accordingly, and review any additional cards left in the competitor's deck box if applicable.
Model's own assessment (unvalidated): Strong — The retrieved passages directly address the situation of a deck not containing 60 cards and explicitly prescribe the steps to make the deck legal and update the physical deck.
Validated Source Support: Strong — Every citation exists, all facts were confirmed sufficient, and every cited passage explicitly supports its claim with no conflicts.

Explanation: According to the retrieved passages, in the case that a deck list or physical deck does not contain 60 cards (classified as a major deck legality infraction), the deck list should be made legal by adding an appropriate number of Basic Energy cards of the competitor's choice, and then the physical deck should be updated accordingly. Additionally, a review of any additional cards left in the competitor's deck box may be required to ensure no infractions exist.

Repair steps:
  - Add an appropriate number of Basic Energy cards of the competitor's choice to make the deck list legal.
  - Update the physical deck accordingly to match the corrected deck list.
  - Review any additional cards or a separate deck left in the competitor's deck box to ensure no infractions exist.

Penalty guidance: Major: Game Loss

Cited chunk IDs: PPG-5.6.1#6, PPG-5.6.1#2, PPG-5.6.1#3

Citation grounding breakdown:
  [PPG-5.6.1#6] ExplicitSupport
  [PPG-5.6.1#2] ExplicitSupport
  [PPG-5.6.1#3] ExplicitSupport
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (PPG-5.6.1#2).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.

### ace-spec-count — PASS

**Scenario** (Deck/Decklist Issues): Before a match, a judge is asked to check a competitor's decklist because it appears to include two different ACE SPEC cards.

**Facts the simulated judge answered from:** Both the decklist and the physical deck contain two different ACE SPEC cards, Prime Catcher and Master Ball, and they match each other. The judge found this while reviewing the decklist, before either player drew an opening hand.

**Questions** (1 round(s)):

- **Turn 1** (tied to `TCGRULES-appendix-3-ace-spec-cards#0`): Does the competitor's deck list actually list two different ACE SPEC cards, and what are their specific names?
  - *Judge:* Yes, the decklist lists two different ACE SPEC cards, and their specific names are Prime Catcher and Master Ball.

**Ruling:**

```text
Recommendation: It is recommended that the judge review the deck list with the competitor, as ACE SPEC cards are restricted such that a deck can include only one total ACE SPEC card, not one of each.
Model's own assessment (unvalidated): Strong — The retrieved passage directly and explicitly addresses the ACE SPEC card rule stating that a deck can include only one total ACE SPEC card, not one of each.
Validated Source Support: Strong — Every citation exists, all facts were confirmed sufficient, and every cited passage explicitly supports its claim with no conflicts.

Explanation: According to the retrieved passages, ACE SPEC cards are limited to only one total ACE SPEC card per deck, meaning a deck cannot include one of each (Prime Catcher and Master Ball in this scenario).

Cited chunk IDs: TCGRULES-appendix-3-ace-spec-cards#0

Citation grounding breakdown:
  [TCGRULES-appendix-3-ace-spec-cards#0] ExplicitSupport
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (TCGRULES-appendix-3-ace-spec-cards#0).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.

### too-many-prizes — PASS

**Scenario** (Prize Errors): A judge is called over because a player took two Prize cards after a single Knock Out, instead of one.

**Facts the simulated judge answered from:** The Knocked Out Pokémon was an ordinary Pokémon, worth one Prize card. The player took two. The extra Prize card was set aside face down, separate from the hand, and can be returned.

**Questions** (1 round(s)):

- **Turn 1** (tied to `PPG-5.5.1#1`): Have the extra Prize cards taken been revealed or mixed into the player's hand, or can they be uniquely identified and returned to the Prize pool?
  - *Judge:* The extra Prize card was set aside face down, separate from the hand, and can be returned.

**Ruling:**

```text
Recommendation: It is recommended that the judge have the player return the extra Prize card, as it is separate from the hand and can be returned.
Model's own assessment (unvalidated): Partial — The retrieved passages classify taking too many Prize cards as a major gameplay error, but the specific repair steps for returning the extra Prize card rely on the confirmed facts rather than explicit procedural instructions in the text.
Validated Source Support: Partial — Every citation exists and none is unsupported, but at least one requires interpretation or judge discretion, or the retrieved passages conflict.

Explanation: According to the retrieved passages, taking too many Prize cards after Knocking Out a Pokemon is classified as a major gameplay error (PPG-5.5.1#1). However, the confirmed facts state that the extra Prize card was set aside face down, is separate from the hand, and can be returned.

Repair steps:
  - Return the extra Prize card to the game state.

Cited chunk IDs: PPG-5.5.1#1

Citation grounding breakdown:
  [PPG-5.5.1#1] Interpretation
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (PPG-5.5.1#1).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Partial) was in the acceptable set.

### prize-issue-vague — NOT RUN

**Scenario** (Prize Errors): hit the Gemini free tier's daily limit (500 requests per model), so there is no result yet. Re-run after the quota resets.

**Facts the simulated judge answers from:** A player took a Prize card after what they believed was a Knock Out, but the Defending Pokémon still had HP remaining. It was never Knocked Out.

### double-energy-attach — NOT RUN

**Scenario** (Gameplay Error): hit the Gemini free tier's daily limit (500 requests per model), so there is no result yet. Re-run after the quota resets.

**Facts the simulated judge answers from:** The player attached two Basic Energy cards from hand in the same turn. No card effect allowed the second attachment.

### discard-shuffle-deescalate — NOT RUN

**Scenario** (Penalty Questions): hit the Gemini free tier's daily limit (500 requests per model), so there is no result yet. Re-run after the quota resets.

**Facts the simulated judge answers from:** The competitor shuffled their discard pile into their deck without a card effect. The discard pile was small, the game hasn't progressed past the first few turns, and both competitors agree on exactly which cards were in it.

### spectator-conduct — NOT RUN

**Scenario** (Tournament Procedure): hit the Gemini free tier's daily limit (500 requests per model), so there is no result yet. Re-run after the quota resets.

**Facts the simulated judge answers from:** The person is a spectator, not playing in any event. They were standing next to the match and talking loudly about the game state.
