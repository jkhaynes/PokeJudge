# Step 2 live eval results

Final run 2026-09-27: full 20-scenario `evaluate` with all Step 2 changes (simulated judge that sees the scenario text and its fact sheet, no round limit), pinned `gemini-3.5-flash-lite`, fixed seed, paced at 14 calls per minute.

**Score: 16 of 20** (baseline with scripted answers, same model and seed: 11/20). No infrastructure failures.
**Not known:** 7 of 27 questions couldn't be answered from the scenario or fact sheet (reported, not scored).

## Failures

All are PokeJudge's own mistakes; none are test errors.

| Scenario | Failed criteria | Reason | Where it's addressed |
|---|---|---|---|
| proxy-cards | Final Source Support | Validated Partial for an explicit rule (the model itself said Strong), because one of four citations was graded as interpretation. | Grounding validation |
| drew-extra-card | Initial retrieval, materiality, post-answer retrieval, Source Support | Retrieval never surfaces `PPG-5.5.1`, so PokeJudge re-asks the same question until the turn cap and never rules. | Step 4 (better rule text) |
| ace-spec-count | Sufficiency timing, Source Support | Asks the judge whether two ACE SPECs is a minor, major or severe infraction: a policy question, not a fact. Then rules Insufficient, honestly, because the ACE SPEC rule and `PPG-5.6.1#2` fix instructions are never retrieved. | Step 4 (better rule text) |
| spectator-conduct | Sufficiency timing | Asks whether the person is a spectator; the scenario already says so. | Step 5 (judgment model) |

## Test fixes made during Step 2

- **Simulated judge prompt:** it answered "not known" to two-part questions it could half-answer. It now answers the parts it can (commit `91a4138`).
- **Simulated judge sees the scenario:** it only saw the fact sheet, so it answered "not known" to questions the scenario itself answers (missed-mulligan-draws: "Did Player A take two mulligans?"). The real judge described the scenario, so it now answers from both (commit `98ebc77`).
- **supporter-twice fact sheet:** "the opponent" was ambiguous against PokeJudge's "the player"; it now says "the player being ruled on (the caller's opponent)".
- **mulligan-not-taken replaced** by missed-mulligan-draws: neither player could remember whether anyone mulliganed, so it had no answerable facts.
- **missed-mulligan-draws expects one question** (commit `f11d941`): the scenario says Player B never drew the cards but not whether B announced them, and `TCGTH-7.4.1` turns on the announcement, so asking is fair. Its fact sheet now also says setup completed, which a judge at the table would know. The ruling matches the confirmed correct one: Player B doesn't get the draws.
- **ace-spec-count states the timing:** found at the start of round 3, deck played unchanged in rounds 1 and 2, so the ruling is decidable.
- **Round limit removed:** fair follow-up questions are not a failure, and a loop that never resolves is already caught by the 4-turn cap producing no ruling. Rounds are now reported per run.
- **Rulings printed:** `evaluate` now prints each full ruling, using the same output as the judge-facing flow, so passing scores can be checked against what PokeJudge actually said.

## Experiments (throwaway, not committed)

- **drew-extra-card with a 7-turn cap:** still no ruling. `PPG-5.5.1` was never retrieved; every turn retrieved the `PPG-4.2.1` Supporter excerpts. After the first answer, PokeJudge asked the identical question six times, getting "not known" each time. Confirms a retrieval problem for Step 4, and shows the loop doesn't accept "not known" as an answer.
- **missed-prize with "nothing since depended on the Prize count, and the game can be corrected" added:** PokeJudge ruled after 2 rounds, labelled Insufficient with no citations, and said nothing retrieved covers a forgotten Prize. So yesterday's open question is settled: it's a rulebook gap (as Milestone 8.5 found), not only a thin fact sheet. The scenario stays as is.

## Findings for later steps

- **Insufficient rulings still instruct.** The missed-prize experiment's ruling was labelled Insufficient but still said "instruct the player to take the missed Prize card now". PRD §8 says an Insufficient recommendation must not present a definitive ruling.
- **"Not known" answers are re-asked** word for word until the turn cap (drew-extra-card).
- **The scorer doesn't check what the ruling says.** supporter-twice passed both days, but yesterday's ruling named a Game Loss and today's only says "assess an appropriate penalty".
- **The zero-questions crash is still open.** No scenario triggers it since mulligan-not-taken was replaced, but PokeJudge can still report "insufficient" with no questions.

## To do

- [ ] **Finish Step 2**: code review (superpowers:requesting-code-review), then open the PR (superpowers:finishing-a-development-branch). Optional: `/learning-checkpoint` before the PR.
- [ ] **After merge**: update Step 2 in the PokeJudge improvement plan doc to match what shipped (simulated judge that sees the scenario, no round limit, 16/20).

## Full rundown

Every scenario, in dataset order. For each: the scenario, the facts the simulated judge answered from (alongside the scenario text), every question PokeJudge asked with the judge's answer, PokeJudge's full ruling, and the scored checks.

### notes — PASS

**Scenario** (Tournament Procedure): Is a competitor allowed to keep written notes during their match?

**Facts the simulated judge answered from:** The competitor wants to keep hand-written notes about the current match during play.

**Questions:** none.

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

**Questions:** none.

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

**Questions** (2 round(s)):

- **Turn 1** (tied to `TCGTH-6.2.2#0`): At what point was the insufficiently randomized deck discovered, and what penalty level is appropriate according to tournament policy guidelines?
  - *Judge:* The insufficiently randomized deck was discovered while the opponent was cutting the deck, before either player drew an opening hand. There is no penalty mentioned in the fact sheet for this situation.
- **Turn 2** (tied to `TCGTH-6.2.2#0`): What specific penalty should be assessed for insufficiently randomizing the deck?
  - *Judge:* A Warning penalty should be assessed for insufficiently randomizing the deck.

**Ruling:**

```text
Recommendation: It is recommended that the judge assess a Warning penalty for insufficiently randomizing the deck and instruct the players to ensure the deck is fully randomized and properly cut before beginning play.
Model's own assessment (unvalidated): Strong — The retrieved passages establish the rules for deck randomization and state that insufficient randomization is a rules violation carrying a penalty, directly supporting the confirmed facts and recommendation.
Validated Source Support: Strong — Every citation exists, all facts were confirmed sufficient, and every cited passage explicitly supports its claim with no conflicts.

Explanation: Based on the confirmed facts and retrieved passages, each competitor's deck is expected to be fully randomized at the start of each game [TCGTH-6.2#0]. Insufficiently randomizing the deck is a rules violation that may carry a penalty [TCGTH-6.2.2#0]. Because the confirmed facts state that a Warning penalty should be assessed for this infraction, the judge should issue a Warning.

Repair steps:
  - Have the deck fully randomized properly before proceeding with the game.

Penalty guidance: A Warning penalty should be assessed for insufficiently randomizing the deck.

Cited chunk IDs: TCGTH-6.2#0, TCGTH-6.2.2#0

Citation grounding breakdown:
  [TCGTH-6.2#0] ExplicitSupport
  [TCGTH-6.2.2#0] ExplicitSupport
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (TCGTH-6.2.2#0).
- PASS Post-answer retrieval: No post-answer section(s) specified for this scenario.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.

### spectator-badges — PASS

**Scenario** (Tournament Procedure): Do spectators need to wear a badge at a Regional Championship?

**Facts the simulated judge answered from:** The event is a Regional Championship. The spectator is not playing.

**Questions:** none.

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

**Questions:** none.

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

**Questions** (1 round(s)):

- **Turn 1** (tied to `TCGRULES-special-conditions#0`): What attack was used that caused the Special Condition, and what text does that attack have regarding Special Conditions?
  - *Judge:* The scenario does not state which attack was used. The text of the attack that was used states that the Defending Pokémon is now Confused.

**Ruling:**

```text
Recommendation: It is recommended that the judge correct the game state by removing the incorrect Asleep marker and replacing it with the correct Confused special condition orientation, as supported by the text of the attack used.
Model's own assessment (unvalidated): Partial — The retrieved passages define how Asleep and Confused are represented and what the attack text states, but the retrieved text does not explicitly prescribe a judge procedure for correcting a misapplied special condition marker during a match.
Validated Source Support: Strong — Every citation exists, all facts were confirmed sufficient, and every cited passage explicitly supports its claim with no conflicts.

Explanation: The confirmed facts state that the attack used causes the Defending Pokemon to be Confused. According to the retrieved rules, Confused is represented by turning the Pokémon card with the top of the card pointed toward the player, whereas Asleep is represented by turning the Pokémon card counterclockwise. Since an error was made in applying the correct special condition from the attack, the judge should ensure the correct condition is represented.

Repair steps:
  - Remove the incorrect Asleep marker or orientation from the Active Pokémon.
  - Orient the Active Pokémon card with the top of the card pointed toward the player to correctly reflect the Confused Special Condition.

Cited chunk IDs: TCGRULES-special-conditions#0, TCGRULES-special-conditions#3

Citation grounding breakdown:
  [TCGRULES-special-conditions#0] ExplicitSupport
  [TCGRULES-special-conditions#3] ExplicitSupport
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (TCGRULES-special-conditions#0).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.

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
  - *Judge:* No, the extra card went into the player's hand and cannot be told apart from the rest.
- **Turn 2** (tied to `PPG-4.2.1#0`): Did the player play a second Supporter card or perform any other specific game actions (such as shuffling or drawing additional cards) after drawing the extra card during their draw step?
  - *Judge:* Not known. (not known)
- **Turn 3** (tied to `PPG-4.2.1#0`): Did the player play a second Supporter card or perform any other specific game actions (such as shuffling or drawing additional cards) after drawing the extra card during their draw step?
  - *Judge:* Not known. (not known)
- **Turn 4** (tied to `PPG-4.2.1#0`): Did the player play a second Supporter card or perform any other specific game actions (such as shuffling or drawing additional cards) after drawing the extra card during their draw step?
  - *Judge:* Not known. (not known)

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

**Questions** (2 round(s)):

- **Turn 1** (tied to `TCGRULES-turn-actions#9`): Was the Pokémon that was Knocked Out in the Active position or on the Bench when it took the damage?
  - *Judge:* The Defending Pokémon was in the Active position when it took the damage.
- **Turn 1** (tied to `TCGRULES-full-details-of-attacking#2`): Did the attack place damage counters directly, or did it deal damage that is subject to Weakness and Resistance?
  - *Judge:* Not known. (not known)
- **Turn 2** (tied to `TCGRULES-full-details-of-attacking#2`): Did the attack place damage counters directly on the Pokémon, or did it deal damage subject to Weakness and Resistance?
  - *Judge:* The attack dealt damage subject to Weakness, but only 60 damage was placed on the Defending Pokémon instead of accounting for the printed Weakness of times two.

**Ruling:**

```text
Recommendation: It is recommended that the judge review the attack's damage calculation to ensure the Defending Pokemon's printed Weakness of times two is correctly applied, since the Defending Pokemon was in the Active position and the attack dealt damage subject to Weakness.
Model's own assessment (unvalidated): Strong — The retrieved passages explicitly state the rules for applying Weakness to a Defending Pokémon in the Active position when an attack deals damage subject to Weakness, directly matching the confirmed facts.
Validated Source Support: Strong — Every citation exists, all facts were confirmed sufficient, and every cited passage explicitly supports its claim with no conflicts.

Explanation: According to the rules, when an attack does damage, your opponent's Active Pokemon takes more damage if it has Weakness to the attacker's type, and Weakness is increased by the amount next to the opponent's Active Pokemon's Weakness (such as times two). Since confirmed facts state that the Defending Pokémon was in the Active position when it took the damage, the attack dealt damage subject to Weakness, and the damage did not account for the printed Weakness of times two, the calculation should be adjusted to include this Weakness.

Repair steps:
  - Recalculate the attack damage by applying the Defending Pokémon's printed Weakness of times two.
  - Adjust the game state to reflect the correct number of damage counters corresponding to the final calculated damage if the Knock Out status changes.

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

**Questions** (3 round(s)):

- **Turn 1** (tied to `PPG-4.2.1#0`): Which specific Supporter cards were played by the player during the turn in question?
  - *Judge:* The player played two copies of the Supporter card Judge during the turn in question.
- **Turn 2** (tied to `PPG-4.2.1#2`): Can the second Supporter card's effects (such as the cards drawn or any hidden information revealed/shuffled) be fully reversed and the game state restored to how it was before it was played?
  - *Judge:* No, both cards fully resolved, several turns have passed, and the cards drawn from the second Judge cannot be identified, so it cannot be fully reversed.
- **Turn 3** (tied to `PPG-4.2.1#4`): Which specific second Supporter card was played, and what were its exact effects on the hand and deck (e.g., shuffling the hand into the deck vs. drawing cards)?
  - *Judge:* The second Supporter card played was a second copy of Judge, which requires both players to shuffle their hands into their decks and draw 4 cards. The parts regarding a different specific second Supporter card are not covered.

**Ruling:**

```text
Recommendation: It is recommended that no corrective action be taken regarding the game state, and that the judge assess an appropriate penalty given that the game state cannot be restored and several turns have passed.
Model's own assessment (unvalidated): Partial — The passages discuss situations involving cards that shuffle hands and draw cards where no corrective action can be taken, but the specific penalty for a situation where several turns have passed and cards cannot be identified is not explicitly detailed beyond general principles.
Validated Source Support: Strong — Every citation exists, all facts were confirmed sufficient, and every cited passage explicitly supports its claim with no conflicts.

Explanation: According to the retrieved passages, when a second Supporter card is played and the cards drawn or in the hand cannot be identified or restored (such as when a card like Cynthia or Judge shuffles hands and draws cards), no corrective action can be taken as the judge is unable to determine which cards were in the player's hand prior to the card being played.

Penalty guidance: Assess an appropriate penalty consistent with tournament policy for an irreparable game state resulting from playing a second Supporter card.

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

**Questions:** none.

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

### missed-mulligan-draws — PASS

**Scenario** (Timing Questions): Player A took two mulligans at the start of the game; Player B took none. Player B went first and has drawn their card for the turn, then realizes they never drew the two extra cards they were allowed for Player A's mulligans, and calls a judge.

**Facts the simulated judge answered from:** Setup was completed normally and the game has started. Player B never announced or drew any mulligan cards during setup. Player B has made no other plays yet this turn. Player A has not played anything yet.

**Questions** (1 round(s)):

- **Turn 1** (tied to `TCGTH-7.4.1#0`): Did Player B announce how many cards they would draw for the mulligans prior to placing an Active Pokemon or beginning the game?
  - *Judge:* Player B never announced or drew any mulligan cards during setup, so they did not announce how many cards they would draw prior to placing an Active Pokemon or beginning the game.

**Ruling:**

```text
Recommendation: It is recommended that the judge disallow Player B from drawing the two extra mulligan cards at this point in the game.
Model's own assessment (unvalidated): Strong — The retrieved rules explicitly state the timing requirement that mulligan cards must be drawn and announced before placing an Active Pokémon and beginning the game, directly addressing the procedural error made by Player B.
Validated Source Support: Partial — Every citation exists and none is unsupported, but at least one requires interpretation or judge discretion, or the retrieved passages conflict.

Explanation: The retrieved passages state that cards for opponent mulligans may be drawn only once the opponent has placed an Active Pokémon, and the competitor must announce how many cards they will draw before doing so. Furthermore, the game sequence specifies that players reveal all Active and Benched Pokémon and begin the game after mulligan draws are completed. Since Player B did not announce how many cards they would draw prior to placing an Active Pokémon or beginning the game, and has already gone first and drawn their card for the turn, the opportunity to draw those cards has passed.

Cited chunk IDs: TCGTH-7.4.1#0, TCGRULES-full-details-of-taking-a-mulligan#1

Citation grounding breakdown:
  [TCGTH-7.4.1#0] ExplicitSupport
  [TCGRULES-full-details-of-taking-a-mulligan#1] Interpretation
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (TCGTH-7.4.1#0).
- PASS Post-answer retrieval: No post-answer section(s) specified for this scenario.
- PASS Final Source Support: Validated Source Support (Partial) was in the acceptable set.

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

**Questions** (2 round(s)):

- **Turn 1** (tied to `PPG-5.6.1#2`): Does the physical deck actually contain fewer than 60 cards when counted, or is it merely suspected?
  - *Judge:* The physical deck actually contains 58 cards, which is fewer than the required 60.
- **Turn 2** (tied to `PPG-5.6.1#6`): Does the competitor's deck list also contain fewer than 60 cards, or does the discrepancy arise from a difference between the physical deck and the deck list?
  - *Judge:* The competitor's deck list also contains 58 cards, so there is no discrepancy between the physical deck and the deck list.

**Ruling:**

```text
Recommendation: It is recommended that the judge make the competitor's deck list legal by adding an appropriate number of Basic Energy cards of the competitor's choice, and then update the physical deck accordingly.
Model's own assessment (unvalidated): Strong — The retrieved passage PPG-5.6.1#2 directly prescribes the exact procedure to follow when a deck list contains fewer than 60 cards.
Validated Source Support: Strong — Every citation exists, all facts were confirmed sufficient, and every cited passage explicitly supports its claim with no conflicts.

Explanation: According to the retrieved passages, in the case that a deck list contains fewer than 60 cards, the deck list should be made legal by adding an appropriate number of Basic Energy cards of the competitor's choice, and the physical deck should then be updated accordingly.

Repair steps:
  - Add an appropriate number of Basic Energy cards of the competitor's choice to the deck list so that it totals 60 cards.
  - Update the physical deck accordingly to match the corrected deck list.

Cited chunk IDs: PPG-5.6.1#2

Citation grounding breakdown:
  [PPG-5.6.1#2] ExplicitSupport
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (PPG-5.6.1#2).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.

### ace-spec-count — FAIL

**Scenario** (Deck/Decklist Issues): During a deck check at the start of round 3 of a Standard-format League Cup, before game 1 begins, a judge finds that a competitor's deck list and deck both include two different ACE SPEC cards. The competitor played rounds 1 and 2 with this deck unchanged, and those match slips are signed.

**Facts the simulated judge answered from:** The two ACE SPEC cards are Prime Catcher and Master Ball, one copy each, on both the deck list and in the deck. The competitor says it was an oversight; there is no sign it was intentional.

**Questions** (1 round(s)):

- **Turn 1** (tied to `PPG-5.6.1#3`): Did the competitor's deck list or deck contain two different ACE SPEC cards, and does this constitute a minor, major, or severe deck legality infraction under the retrieved penalty guidelines?
  - *Judge:* Yes, the competitor's deck list and deck contained two different ACE SPEC cards (Prime Catcher and Master Ball). The scenario and fact sheet do not state whether this constitutes a minor, major, or severe deck legality infraction.

**Ruling:**

```text
Recommendation: It is recommended that the judge evaluate the specific severity classification of this deck legality infraction using available tournament policy guidelines, as the retrieved passages do not explicitly list the presence of two different ACE SPEC cards as an example of a minor, major, or severe infraction.
Model's own assessment (unvalidated): Insufficient — The retrieved passages do not contain specific information regarding two different ACE SPEC cards in a deck or deck list, making it impossible to determine the correct infraction level or penalty guidance solely from the provided text.
Validated Source Support: Insufficient — At least one cited passage does not actually support the claim it was cited for.

Explanation: The retrieved passages discuss minor, major, and severe deck legality infractions and provide various examples (such as incorrect card counts, unspecified collector numbers, extra cards, or cards not recorded on a deck list), but they do not contain text addressing two different ACE SPEC cards specifically. Therefore, the source support is insufficient to prescribe a definitive classification or penalty.

Cited chunk IDs: PPG-5.6.1#3, PPG-5.6.1#4, PPG-5.6.1#6, PPG-5.6.1#8, PPG-5.6.1#5

Citation grounding breakdown:
  [PPG-5.6.1#3] Unsupported
  [PPG-5.6.1#4] Unsupported
  [PPG-5.6.1#6] Unsupported
  [PPG-5.6.1#8] Unsupported
  [PPG-5.6.1#5] Unsupported
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- **FAIL** Sufficiency timing: Expected immediate sufficiency, but a clarifying question was asked.
- **FAIL** Final Source Support: Validated Source Support was Insufficient, not in the acceptable set [Strong, Partial].

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

### prize-issue-vague — PASS

**Scenario** (Prize Errors): A judge is called over because something seems wrong with how Prize cards were handled during the match, but nobody can immediately explain what happened.

**Facts the simulated judge answered from:** A player took a Prize card after what they believed was a Knock Out, but the Defending Pokémon still had HP remaining. It was never Knocked Out.

**Questions** (1 round(s)):

- **Turn 1** (tied to `PPG-5.5.1#2`): Did a competitor take a Prize card without Knocking Out a Pokémon or take too many Prize cards after Knocking Out a Pokémon?
  - *Judge:* A player took a Prize card after what they believed was a Knock Out, but the Defending Pokémon still had HP remaining and was never Knocked Out.

**Ruling:**

```text
Recommendation: It is recommended that the judge evaluate the situation as a gameplay error where a competitor took a Prize card without Knocking Out a Pokémon, and apply appropriate corrective and penalty actions consistent with major gameplay errors.
Model's own assessment (unvalidated): Strong — The retrieved passages directly define the specific action of taking a Prize card without Knocking Out a Pokémon as a gameplay error, matching the confirmed facts.
Validated Source Support: Strong — Every citation exists, all facts were confirmed sufficient, and every cited passage explicitly supports its claim with no conflicts.

Explanation: Based on the confirmed facts, the player took a Prize card after what they believed was a Knock Out, but the Defending Pokémon still had HP remaining and was never Knocked Out. According to PPG-5.5.1#2, taking a Prize card without Knocking Out a Pokémon is explicitly listed as an error (and categorized under major gameplay errors in PPG-5.5.1#1).

Penalty guidance: Classify the infraction as a major gameplay error (taking a Prize card without Knocking Out a Pokémon) per PPG-5.5.1#1 and PPG-5.5.1#2, and apply the appropriate penalty accordingly.

Cited chunk IDs: PPG-5.5.1#1, PPG-5.5.1#2

Citation grounding breakdown:
  [PPG-5.5.1#1] ExplicitSupport
  [PPG-5.5.1#2] ExplicitSupport
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (PPG-5.5.1#2).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.

### double-energy-attach — PASS

**Scenario** (Gameplay Error): A judge is called over because a player attached two Energy cards to their Pokemon during a single turn.

**Facts the simulated judge answered from:** The player attached two Basic Energy cards from hand in the same turn. No card effect allowed the second attachment.

**Questions** (1 round(s)):

- **Turn 1** (tied to `PPG-5.5.1#2`): Did the competitor attach more than one Energy card in a turn with or without an effect that allows this?
  - *Judge:* The competitor attached two Basic Energy cards from their hand in the same turn without any card effect allowing the second attachment.

**Ruling:**

```text
Recommendation: I recommend that the judge evaluate the game state regarding the illegal second Energy attachment and address the infraction in accordance with the tournament rules governing gameplay errors where a competitor attaches more than one Energy card in a turn without an allowing effect.
Model's own assessment (unvalidated): Partial — The retrieved passages identify the action as a specific gameplay error and define the once-per-turn limit, but they do not explicitly detail the exact step-by-step game state repair instructions for an illegal extra Energy attachment.
Validated Source Support: Strong — Every citation exists, all facts were confirmed sufficient, and every cited passage explicitly supports its claim with no conflicts.

Explanation: According to the retrieved passages, attaching more than one Energy card in a turn without an effect that allows this is classified as a gameplay error under PPG-5.5.1. Furthermore, the turn actions rules state that a player may only attach an Energy card once each turn.

Cited chunk IDs: PPG-5.5.1#2, TCGRULES-turn-actions#1

Citation grounding breakdown:
  [PPG-5.5.1#2] ExplicitSupport
  [TCGRULES-turn-actions#1] ExplicitSupport
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (PPG-5.5.1#2).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.

### discard-shuffle-deescalate — PASS

**Scenario** (Penalty Questions): A judge is called over because a competitor shuffled their discard pile into their deck without a card effect allowing it, and now needs a ruling on the appropriate penalty.

**Facts the simulated judge answered from:** The competitor shuffled their discard pile into their deck without a card effect. The discard pile was small, the game hasn't progressed past the first few turns, and both competitors agree on exactly which cards were in it.

**Questions** (1 round(s)):

- **Turn 1** (tied to `PPG-5.5.1#4`): What were the contents of the discard pile, what turn of the game is it, and do both competitors agree on which exact cards were in the discard pile?
  - *Judge:* The exact contents of the discard pile are not stated, but the game is in the first few turns, and both competitors agree on exactly which cards were in it.

**Ruling:**

```text
Recommendation: I recommend that the judge de-escalate the penalty to a Warning, provided that the discard pile was minimal and the exact cards can be easily discerned by agreement of both competitors, and that the game state is repaired by retrieving the correct cards from the deck.
Model's own assessment (unvalidated): Strong — The retrieved passages directly address the specific scenario of a competitor shuffling their discard pile into their deck without a card effect during the first few turns where both competitors agree on the contents, and explicitly prescribe a Warning and specific repair steps.
Validated Source Support: Strong — Every citation exists, all facts were confirmed sufficient, and every cited passage explicitly supports its claim with no conflicts.

Explanation: The retrieved passages explicitly state as an example for de-escalating a penalty to a Warning when a competitor shuffles their discard pile into their deck without a card effect, provided the contents of the discard pile were minimal, the game has not progressed past the first few turns, and both competitors agree it is easily discernible which exact cards were in the minimal discard pile. Furthermore, the game state can be fixed by retrieving the correct cards from the deck and returning them to the discard pile.

Repair steps:
  - Retrieve the correct cards from the deck.
  - Return those cards to the discard pile.

Penalty guidance: A Warning should be issued to the competitor.

Cited chunk IDs: PPG-5.5.1#4, PPG-5.5.1#5

Citation grounding breakdown:
  [PPG-5.5.1#4] ExplicitSupport
  [PPG-5.5.1#5] ExplicitSupport
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- PASS Sufficiency timing: Correctly recognized the initial scenario was incomplete.
- PASS Clarifying question materiality: Question was tied to an expected material section (PPG-5.5.1#4).
- PASS Post-answer retrieval: Retrieval after the judge's answer surfaced an expected section.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.

### spectator-conduct — FAIL

**Scenario** (Tournament Procedure): A judge is called over because a spectator was standing very close to an in-progress match and talking loudly about the game state.

**Facts the simulated judge answered from:** The person is a spectator, not playing in any event. They were standing next to the match and talking loudly about the game state.

**Questions** (1 round(s)):

- **Turn 1** (tied to `PPTRH-3.3#5`): Is the person standing close to the match and talking actually an active competitor in another event, or are they a spectator who is not actively participating in any main competitive event?
  - *Judge:* The person is a spectator and not playing in any event. They were standing next to the match and talking loudly about the game state.

**Ruling:**

```text
Recommendation: It is recommended that the judge address the spectator for failing to maintain a reasonable distance and for discussing the match in progress within earshot of the table, and instruct the spectator to move away and remain silent. If the spectator's actions are determined to disrupt tournament proceedings or cause tournament integrity issues, the judge may subject the spectator to penalties up to and including removal from the venue and suspension from Play! Pokémon events.
Model's own assessment (unvalidated): Strong — The retrieved passages directly outline spectator responsibilities regarding maintaining distance, refraining from discussing matches within earshot, and the potential consequences for disrupting tournament proceedings.
Validated Source Support: Strong — Every citation exists, all facts were confirmed sufficient, and every cited passage explicitly supports its claim with no conflicts.

Explanation: According to spectator responsibilities, all spectators must maintain a reasonable distance from matches in progress to avoid distracting competitors and must refrain from discussing matches in progress within earshot of the table. If a spectator disrupts tournament proceedings or causes tournament integrity issues, they may be subject to penalties, up to and including removal from the venue and suspension from Play! Pokémon events.

Penalty guidance: Should a spectator disrupt tournament proceedings or cause tournament integrity issues, that spectator may be subject to penalties, up to and including removal from the venue and a suspension from Play! Pokémon events.

Cited chunk IDs: PPTRH-3.3#6, PPTRH-3.3#5

Citation grounding breakdown:
  [PPTRH-3.3#6] ExplicitSupport
  [PPTRH-3.3#5] ExplicitSupport
  Deterministic checks: retrieval non-empty=True, all citations exist=True, facts were sufficient=True
```

**Checks:**

- PASS Initial retrieval: Retrieved at least one expected material section on turn 1.
- **FAIL** Sufficiency timing: Expected immediate sufficiency, but a clarifying question was asked.
- PASS Final Source Support: Validated Source Support (Strong) was in the acceptable set.

