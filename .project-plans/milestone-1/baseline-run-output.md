# Milestone 1 — Baseline Run Output

Captured as a reference point for comparing naive free-text parsing (this milestone) against the structured-output approach introduced in Milestone 2.

- **Date:** 2026-08-14
- **Branch:** `milestone/1-first-llm-interaction`
- **Base commit:** `6c1c77a` (working tree had uncommitted `Program.cs`/`PokeJudge.csproj` changes at capture time — this is the Milestone 1 implementation as described in `milestone-01-first-llm-interaction-implementation-summary.md`)
- **Provider / model:** Gemini, `gemini-flash-lite-latest` (via `GeminiLlmClient`, raw `generateContent` REST call)
- **Command:** `dotnet run --project PokeJudge.csproj`
- **Exit code:** 0 (clean run, no exceptions)

## How to use this document

When Milestone 2 (or later formula changes) land, run the equivalent scenarios/extraction and compare against this baseline:
- Do the same 3 scenarios still produce plausible raw completions?
- Does the sufficiency-signal extraction get *more* consistent, or does it just move the failure mode elsewhere?
- Specifically re-check the "not sufficient" → misclassified-as-`SUFFICIENT` failure documented below — has structured output actually eliminated it?

## Naive-parser misclassification count (this run)

Out of 12 total naive-parser runs (3 scenarios × 4 repeats):
- **3 runs** were misclassified as `SUFFICIENT` despite the raw response explicitly saying "not sufficient" (Scenario 1 Run 4, Scenario 2 Run 4, Scenario 3 Run 4).
- **1 run** was `AMBIGUOUS` (both patterns matched) despite the response clearly saying "not sufficient" (Scenario 2 Run 1).
- Only **7 of 12** runs were correctly classified `INSUFFICIENT` — and every single raw response across all 12 runs was, in substance, saying "not sufficient" / "more details needed." The correct classification rate against ground truth was effectively **7/12 (58%)**, on a set of responses that were unanimously "insufficient" in meaning.

## Full Raw Output

```
=== Part 1: Raw LLM responses ===

--- Scenario ---
During a League Challenge, a player realizes on their next turn that they forgot to take a Prize card after knocking out their opponent's Pokemon two turns ago. What should the judge do?

--- Raw response ---
According to the official Pokémon TCG Penalty Guidelines and Tournament Rules, here is how a judge should handle this situation:

### 1. Do Not Award the Prize Card Now
The player **cannot** take the missed Prize card at this point. In the Pokémon TCG, game actions must happen in the correct sequence. Because two turns have passed, taking the Prize card now would disrupt the current game state and is no longer valid. 

### 2. Issue a Warning (Procedural Error)
The judge should issue a formal **Warning** to the player for a **Procedural Error**. 
* **Reasoning:** Keeping track of Prize cards is the responsibility of the player whose turn it is to take them. Failing to take a Prize card is considered a game state error.

### 3. Allow Play to Continue
The game should continue from the current game state without rolling back actions. The player simply loses out on that Prize card.

---

### Why this ruling?
Judges cannot "rewind" a game by multiple turns to fix missed triggers or forgotten game actions (like taking Prize cards, drawing cards for an Ability, etc.) once significant game actions have occurred. Rewinding would cause more unfairness and confusion than simply letting the game proceed with the penalty and the missed Prize. 

*Note for the Judge:* It is always good practice to remind players to be vigilant about their game states, especially in competitive events like League Challenges.

--- Scenario ---
A player used an Ability that says it can only be used once per turn, but the judge suspects it may have been used twice in the same turn. The player disagrees. How should this be resolved?

--- Raw response ---
Resolving this situation requires balancing competitive integrity with the reality that human memory is fallible, especially in high-concentration card games like Pokémon, Magic: The Gathering, or Yu-Gi-Oh! 

Because the burden of proof in tournament play generally falls on maintaining a clear and verifiable gamestate, here is the standard procedure a judge should follow to resolve this dispute:

### 1. Interview Both Players and Assess the Board State
The judge should first pause the game and calmly ask both players to explain the sequence of events that led to the dispute. 
* **Look for physical evidence:** Did the player move a marker, tap a card, place dice on it, or turn it sideways to indicate use? Many players use counters or specific card orientations to track "once-per-turn" abilities.
* **Review public information:** Are there resources in the discard pile, damage counters, or board state that correlate with the ability being used twice (e.g., two searches for items, two extra energy attachments, etc.)?

### 2. Determine if the Gamestate is Irrevocable
If the judge cannot definitively prove whether the ability was used once or twice, they must determine if the gamestate has progressed too far to be untangled. 
* **If it was just used:** If the opponent immediately called a judge the second time the ability was declared, the gamestate is fresh.
* **If several actions have passed:** If the opponent only realized it *retroactively* after several subsequent turns or plays, it is often impossible to rewind accurately without heavily penalizing the innocent player.

### 3. Apply Ruling Principles (The "Benefit of the Doubt" vs. "Player Responsibility")
Judges operate under specific policy guidelines depending on the game, but generally:
* **The Player is Responsible for Their Gamestate:** Players are expected to clearly communicate and track their own actions. If a player cannot remember if they used an ability, they are generally not permitted to "guess and check." 
* **Lack of Conclusive Evidence:** If the judge investigates and finds no definitive proof (no video recording, no witness, ambiguous board state) that a rule was definitively broken, judges typically **cannot issue a penalty** based purely on suspicion. However, the judge *can* prevent the player from using the ability a second time if there is reasonable belief it was already used, ruling in the interest of a legal gamestate going forward.

### 4. Check for Policy Infractions (Cheating vs. Procedural Error)
The judge must determine *intent*:
* **If it was an honest mistake:** If the player genuinely forgot they used it, this is typically handled as a **Procedural Error** or **Game Error**. The illegal action (the second use) is undone if possible, and play continues. If it cannot be undone safely, the judge applies the appropriate fix outlined in the game's infraction procedure guide.
* **If it was intentional:** If the judge has reason to believe the player knowingly attempted to use the ability twice to gain an unfair advantage (e.g., trying to sneak it by while the opponent was distracted), this elevates to **Cheating**, which carries a penalty of Disqualification (DQ).

### 5. Final Decision and Prevention
Once the judge makes their ruling, it is final:
* **If the judge rules the ability was already used:** The second use is nullified, any random elements generated by that second use (like drawing cards or searching a deck) are undone if they haven't been mixed into hidden information, and play resumes. 
* **To prevent future disputes:** The judge should instruct both players to use explicit physical markers (like a die showing the number of times used, or turning the card sideways) for the remainder of the match to ensure tracking is transparent.

--- Scenario ---
A player's opponent points out, mid-game, that the player's deck has 61 cards instead of 60. What should the judge do?

--- Raw response ---
The exact ruling for this situation depends on the game being played, but assuming this is a standard Competitive rules enforcement level for a major Trading Card Game (like *Magic: The Gathering* or *Pokémon*), here is how a judge should handle it. 

Here is the standard procedure based on **Magic: The Gathering (Judge Program / Infraction Procedure Guide)** rules, which is the industry standard for this type of infraction:

### 1. Verify the Claim
The judge should pause the game and have the player count their main deck to verify if it actually contains 61 cards. 

### 2. Identify the Infraction
This is classified as a **Deck Problem** (specifically, an illegal deck list or incorrect card count). 

### 3. Apply the Remedy (Fix)
* **Determine the intended size:** Standard formats require a minimum of 60 cards. Having 61 cards means the deck is technically illegal because it does not match the official deck registration sheet submitted at the start of the tournament.
* **Fix the deck:** The judge must help the player bring the deck back down to the legal size (60 cards) or to the exact number listed on their registration sheet. 
    * *How to choose which card to remove:* Usually, the player is asked to identify a card that is not on their registration sheet, or if the list is correct, a random card from the library may be removed, or the player chooses a card to bring it in line with their submitted list. (In casual play, the player is usually just allowed to remove any single card of their choice).

### 4. Apply the Penalty
* **Competitive/Professional REL:** A Deck Problem usually carries a penalty of a **Warning** (assuming it was an honest mistake and there is no suspicion of cheating/card manipulation). 
* **Regular/Casual REL:** No formal penalty is given; the judge simply helps correct the deck size and allows the game to continue.

### 5. Resume the Game
Once the illegal card is removed and set aside, the game continues from the exact state it was paused in. 

***

*Note: If you are asking about a different TCG (like Pokémon, Yu-Gi-Oh!, or Flesh and Blood), the philosophy is generally the same—verify the error, correct the deck to legal parameters, issue a minor penalty if at a competitive event, and let play resume.*

=== Part 2: Naive sufficiency-signal extraction ===

--- Scenario ---
During a League Challenge, a player realizes on their next turn that they forgot to take a Prize card after knocking out their opponent's Pokemon two turns ago. What should the judge do?

[Run 1] Naive parser verdict: INSUFFICIENT
[Run 1] Raw response: More details are needed to make a ruling, specifically regarding whether both players agree on the sequence of events and if any significant game actions (such as taking a subsequent Prize card for a later KO) occurred in the intervening turns.

[Run 2] Naive parser verdict: AMBIGUOUS (both patterns matched)
[Run 2] Raw response: The information given is **not sufficient** to make a ruling, as more details are needed regarding whether both players agree on the gamestate and whether any actions (like drawing cards or advancing the game state significantly) have occurred since the missed Prize. According to the Pokémon TCG Penalty Guidelines, missing a Prize is considered a procedural error, and the head judge must determine the appropriate remedy based on when the error is noticed and whether it can be safely corrected without disrupting the game.

[Run 3] Naive parser verdict: INSUFFICIENT
[Run 3] Raw response: More details are needed, as the judge must first investigate whether the player intentionally left the Prize card behind for a strategic advantage or if it was a genuine, unintentional oversight. Depending on that finding, the penalty ranges from a Warning for Procedural Error to Disqualification for Unsporting Conduct, and the missed Prize is usually taken immediately upon discovery.

[Run 4] Naive parser verdict: SUFFICIENT
[Run 4] Raw response: The information given is **not sufficient** to make a final ruling. A judge would need to know whether the opponent agrees that a KO occurred two turns ago and if any game actions (such as drawing cards or shuffling) have occurred since then that could make awarding the Prize card disruptive to the gamestate.

--- Scenario ---
A player used an Ability that says it can only be used once per turn, but the judge suspects it may have been used twice in the same turn. The player disagrees. How should this be resolved?

[Run 1] Naive parser verdict: AMBIGUOUS (both patterns matched)
[Run 1] Raw response: The information provided is not sufficient to make a ruling, as a judge cannot issue a penalty or reversal without corroborating evidence or witnesses when a player denies committing an error. More details regarding game state, logs, or independent witnesses are needed.

[Run 2] Naive parser verdict: INSUFFICIENT
[Run 2] Raw response: More details are needed to make a ruling, as the judge must first investigate the game state and any available evidence (such as player testimony or discard piles) to determine if the Ability was indeed used twice. Without verifiable proof of the infraction, a definitive penalty or game correction cannot be applied.

[Run 3] Naive parser verdict: INSUFFICIENT
[Run 3] Raw response: More details are needed, as the information provided only outlines a dispute of fact without any game logs, video evidence, or reliable witness testimony to prove whether the Ability was actually used twice. Without a way to verify the board state or game history, the judge cannot definitively rule on what occurred.

[Run 4] Naive parser verdict: SUFFICIENT
[Run 4] Raw response: The information given is not sufficient to make a ruling, as a judge cannot issue a penalty or force a game state correction without evidence or confirmation of whether the Ability was actually used twice. The judge must investigate further by gathering context from the board state, the opponent's input, or any spectators to determine what legally transpired before deciding how to proceed.

--- Scenario ---
A player's opponent points out, mid-game, that the player's deck has 61 cards instead of 60. What should the judge do?

[Run 1] Naive parser verdict: INSUFFICIENT
[Run 1] Raw response: More details are needed, as the judge must first investigate whether the illegal deck size was the result of a procedural error during preparation or if it involved intentional cheating (such as marked cards or stalling). The specific penalty depends heavily on this intent and whether the deck was successfully registered and checked during the pre-tournament deck list submission.

[Run 2] Naive parser verdict: INSUFFICIENT
[Run 2] Raw response: More details are needed, as the judge must first investigate whether this is an honest deck-building/shuffling error or intentional cheating. The appropriate penalty ranges from a simple warning with a deck fix to a disqualification, depending on the outcome of that investigation.

[Run 3] Naive parser verdict: INSUFFICIENT
[Run 3] Raw response: More details are needed, as the judge must first investigate whether the illegal deck size was the result of a procedural error or cheating. The appropriate penalty—ranging from a warning with a deck fix to a disqualification—depends entirely on the player's intent and whether any cards were found stuck together.

[Run 4] Naive parser verdict: SUFFICIENT
[Run 4] Raw response: The information given is **not sufficient** to make a final ruling, as the judge must first determine *how* the deck came to have 61 cards (e.g., an extra card stuck to a sleeve, a failure to de-sideboard from a previous match, or an intentional addition) to apply the correct penalty under the Penalty Guidelines.

Observe the runs above: does the naive parser's verdict stay consistent for semantically similar answers, or does phrasing variation cause it to misclassify sufficiency? Record concrete failing examples in the reflection log.
```
