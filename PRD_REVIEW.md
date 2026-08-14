# PokéJudge AI — PRD Critical Review

Reviewer stance: senior AI engineer / software architect review prior to implementation. Findings only — `PRD.md` was not modified as part of this review.

## A. Critical Issues

**A1. Milestone 2 knowingly implements the architecture §8 forbids, then discards it (§7 FR3, §8, §11, §14 M2)**

§7 FR3 and §8 ("Retrieval-driven materiality") state flatly that materiality must derive from retrieved text, "not from the model's general/pretrained Pokémon knowledge." §14's Milestone 2 row does exactly the forbidden thing and says so ("Sufficiency/materiality here is pretrained-knowledge-based and explicitly a temporary placeholder"), then requires Milestone 6 to fully rebuild and replace that logic.

*Why it matters:* This is the exact "build the wrong thing, then discard it" pattern that milestone sequencing should avoid, and it happens because there's no simpler way to get real retrieval context into Milestone 2 — but there is.

*Recommendation:* Feed Milestone 2's sufficiency engine a small **hard-coded/mock policy-passage context** instead of relying on pretrained knowledge. This exercises the real architecture (context → sufficiency assessment → structured output → clarifying questions) with a fake-but-structurally-correct data source. Milestone 6 then just swaps the mock context provider for real retrieval behind the same seam — no logic is thrown away, and FR3 is never actually violated. This also removes the need for the "known, intentional gap" disclaimer entirely.

**A2. Contradiction in the confirmed-fact definition (§8 "Structured state" vs. "No material inference")**

§8 defines a confirmed fact as "explicitly stated by the judge **or unambiguously implied with no other possible reading**," then in the very next bullet forbids exactly this: "a possible interpretation/hypothesis must never be treated as a confirmed fact... it must either already have that fact or ask." Domain-plausibility inference (e.g., "an attack usually causes a KO") is precisely the kind of "unambiguous implication" a model will be tempted to treat as confirmed.

*Why it matters:* This is the single highest-risk ambiguity in the document — it directly gates whether the system can safely issue a `Strong` ruling. A generous reading of "unambiguous" silently converts inference into fact, which §8's own worked example (forgotten Prize ≠ confirmed KO) says must not happen.

*Recommendation:* Tighten "confirmed" to **strict literal statement or pure logical/definitional entailment with zero degrees of freedom** (e.g., "no other Pokémon in play" entails "no non-Active Pokémon," which is definitional, not domain-plausibility inference). Explicitly exclude "this is what normally happens" reasoning from ever qualifying, even when highly likely.

**A3. No fallback when initial retrieval can't identify materiality (§7 FR2–3, §11 core loop)**

The core loop (§11) assumes retrieval on an incomplete scenario reliably surfaces passages specific enough to determine which facts are material. Nothing addresses what happens when the scenario is too vague for retrieval to return anything confidently relevant (e.g., "something went wrong with prizes" returns a broad, undifferentiated set of prize-rule chunks with no clear signal about which fact matters).

*Why it matters:* Without this, the system either (a) fabricates materiality from noise, or (b) silently falls back to pretrained knowledge — the exact thing §8 forbids — with no PRD-sanctioned path to do otherwise.

*Recommendation:* Explicitly split clarification into two kinds: **scenario-disambiguation questions** (neutral, asked when retrieval confidence/relevance is below a threshold, before any policy is trusted) vs. **policy-driven clarification questions** (asked once retrieved policy identifies a material fact). Add a retrieval-confidence check as an explicit step in §11's loop:

```
Judge scenario
      ↓
Can meaningful retrieval be performed?
      ↓
No → ask minimal scenario-disambiguation question
      ↓
Initial retrieval
      ↓
Relevant policy identifies material facts
      ↓
Ask policy-driven clarification questions
      ↓
Re-retrieve as facts change
      ↓
Generate guidance
```

**A4. Grounding Validation is described as more independent/deterministic than it can realistically be (§11 "Grounding Validation & Source Support Assignment," §8)**

The architecture presents grounding validation as a distinct box that checks "does each claim trace to a retrieved passage," "are facts confirmed," "do sources conflict" — implying a clean, largely automatable gate. In practice, only some of these are deterministic; claim-to-passage semantic support is not.

*Why it matters:* If the same underlying model (even via a separate call) both generates the ruling and judges its own groundedness, that is not independent validation — correlated blind spots survive. The PRD should not imply this step produces a trustworthy independent audit by construction.

*Recommendation:* Split explicitly into (a) deterministic app-logic checks computable from structured data with no model call — citation-ID exists in the ingested corpus, all required facts are `confirmed` (not hypothesis), source currency/version flag, retrieval returned zero/low-confidence results — and (b) a narrowly scoped LLM check ("does this specific sentence/claim trace to this specific cited passage — yes/no"), explicitly labeled as a best-effort mitigation, not a grounding guarantee.

**A5. Milestone 9's statistical methods are likely oversized for the achievable dataset (§14 M9, §15, learning objectives M9)**

M9 proposes reliability diagrams, probability bucketing (e.g., 50–60%, 60–70%...), Expected Calibration Error, and Brier Score, built on the M8 hand-curated eval set. A single-developer portfolio eval set is realistically dozens of scenarios, not the hundreds-to-thousands typically needed for meaningful per-bucket calibration statistics.

*Why it matters:* Presenting ECE/reliability-diagram output from a sparse dataset risks being **statistically misleading** — exactly the false-precision problem the PRD elsewhere is careful to avoid for confidence scores, applied inconsistently to its own evaluation methodology.

*Recommendation:* Explicitly right-size M9: state a minimum N below which full calibration-curve/ECE analysis is not attempted, and default to simpler descriptive comparisons (e.g., raw correlation between stated confidence and correctness, 2–3 coarse buckets) when the dataset doesn't support finer analysis. Make "insufficient evidence → keep Source Support only" the explicit default outcome, not just an implied one.

---

## B. Important Improvements

**B1. Provider abstraction timing conflicts with the modular-monolith philosophy (§8, §11, §12, §18)**

§8 and §18 mandate a custom internal LLM provider interface from Milestone 1, unconditionally. §11's own stated principle — abstract when there's a demonstrated need, not because it might exist someday — argues against exactly this. §12 already hedges toward `Microsoft.Extensions.AI`. Recommend using `IChatClient` (or the raw provider SDK) directly at M1, and only hand-rolling a custom abstraction if/when a concrete need appears (e.g., structured-output support gaps at M2, or an actual second provider). Reconcile §8/§18 to match §12's softer framing rather than the other way around.

**B2. Milestone 6's "first-pass" Source Support precedes its own formalized criteria (§14 M6 vs. M7)**

M6 introduces a Strong/Partial/Insufficient output "as an output of ruling generation," but the citation-coverage/conflict-checking criteria aren't formalized until M7. Not wrong, but currently unstated — a reader could believe SS is criteria-based as of M6. Add a sentence noting M6's classification is necessarily provisional/heuristic until M7 formalizes it.

**B3. No document freshness/version status field required (§13)**

§13 requires title/version/section metadata and says versioning should be "tracked," but doesn't require a `current`/`superseded` status field, and §18 defers conflict-handling without committing to even a minimal mechanism. Given the failure mode where an older document wins on semantic similarity, recommend the simplest fix that fits project scope: ingest only the current version of each document, tag chunks with effective date, and when a document is superseded, retire its chunks from the active index rather than building version-aware re-ranking. This is scoped correctly for a solo project — avoid a general conflict-resolution ranking system.

**B4. No fact-correction/retraction path (§7 FR5–6, §10)**

FR6 only prevents re-asking already-supplied facts; nothing lets a judge correct a fact the system mis-recorded as confirmed. Given A2's risk that inference can slip into "confirmed," a correction mechanism is load-bearing, not optional polish.

**B5. No latency/turnaround expectation for a time-pressured judge (§10)**

The loop can chain several sequential LLM calls (sufficiency → clarify → re-retrieve → re-assess → generate → validate) per scenario. §10 asserts "fast to enter a scenario" but sets no bound on round-trip time, making the table-side usability goal untestable. Add even a rough target (e.g., "clarification turnaround under N seconds") so it's a real requirement, not an aspiration.

**B6. No handling for ambiguous/non-responsive judge answers (§7 FR5)**

FR5 assumes an answer cleanly updates state. Real judges will sometimes answer off-topic or ambiguously. Without a defined fallback (re-ask, flag as still-unknown, etc.), the sufficiency loop has no guaranteed termination behavior.

**B7. Branching eval authoring burden isn't scoped (§15)**

The per-scenario branching template (initial scenario + material facts + questions + all answer branches + next questions per branch + source material per branch + outcome per branch) is combinatorially expensive to hand-author. Recommend explicitly stating a minimum viable M8 dataset — a small number of core scenarios (single digits) with at most 1–2 branch points each — as the M8 deliverable, with broader coverage treated as ongoing/incremental rather than a blocking requirement.

**B8. Terminology drift: "ruling" vs. "recommendation" (throughout)**

§6 is explicit that the system produces "recommendations, not binding rulings," but "ruling" is used for the system's output in multiple other places (§5, §17, M9 learning goals — "predicted probability that its ruling is correct"). Since the advisory-not-binding distinction is itself a stated safety property (§9), the terminology should reinforce it consistently. Recommend: system always emits a **recommendation**; **ruling** is reserved for what the judge ultimately decides.

**B9. "Reliability" is overloaded (§9 title, §9 body, M9 title)**

Used as a general safety-section heading, as the name of the judge-facing signal ("reliability signal is Source Support"), and as the subject of Milestone 9 ("Confidence Calibration and Reliability"). Pick one sense; suggest reserving "reliability" for §9's general safety usage and renaming M9 to something like "Confidence Calibration Study."

**B10. "Game-State / Session Store" conflates two concepts (§11 diagram)**

"Session" risks implying ASP.NET Core session middleware, which isn't what's being built (an in-memory per-conversation object through M2+). Rename to "Game-State Store" and note explicitly, near the diagram, that through the relevant milestones this is an in-memory object, not a persistence service.

---

## C. Acceptable Tradeoffs

- **Modular monolith discipline (§11–12):** The explicit warning against premature project-splitting, and the "logical separation ≠ physical separation" framing, is the right call for a solo project and shouldn't be second-guessed.
- **Deferring exact vector store / provider / deployment choices (§18):** Appropriately deferred; none of these decisions block earlier milestones.
- **No auth/multi-user support until M11 (§18):** Correct scope for a portfolio/demo project.
- **Simple hand-authored branching representation, no trajectory-eval framework (§15):** Right call in principle — the concern in B7 is about dataset *size*, not the representation approach itself.
- **Source Support as the default and possibly permanent judge-facing signal, with numeric confidence gated on evidence (§8–9, M9):** This is the PRD's strongest piece of discipline and should be preserved as-is.
- **Deferring tool/function calling until a concrete need arises (§18):** Correct — no speculative capability-building.

---

## D. Final Assessment

**Overall readiness: Not Ready** — the document is well-organized and unusually disciplined about scope for a solo learning project, but A1–A5 represent real design contradictions and feasibility risks that should be resolved before Milestone 1 code is written, since two of them (A1, A2) shape the data model and Milestone 2 deliverable directly.

**Five highest-value changes, in priority order:**
1. Replace Milestone 2's pretrained-knowledge placeholder with a hard-coded mock policy-passage context (A1) — removes a real architectural contradiction and an entire "build it wrong, then discard it" milestone.
2. Tighten the "confirmed fact" definition to exclude domain-plausibility inference (A2) — this is a correctness/safety property, not a style issue.
3. Add an explicit retrieval-confidence check and scenario-disambiguation vs. policy-driven clarification distinction (A3).
4. Right-size Milestone 9's statistical ambition to the realistic dataset size (A5).
5. Split "Grounding Validation" into deterministic checks vs. a narrow LLM claim-check, and stop implying it's independent validation (A4).

**Requirements to delete entirely:** none — nothing in the PRD is unnecessary on its own terms. The one thing to remove is the *justification structure* around Milestone 2 (the "known, intentional gap" framing and the requirement that M6 "fully replace" M2's logic) once A1 is adopted — that language becomes unnecessary rather than the milestone itself.

**Unnecessarily complex architecture:** The mandatory custom LLM provider abstraction from Milestone 1 (B1) is the one place the PRD's own "abstract only when needed" principle is violated by its own requirements section. Everything else in the architecture is appropriately scoped.

**Milestones to reorder/redesign:** Milestone 2 (redesign per A1, not reorder — it should stay where it is, just built differently). No milestone needs to move in the sequence; the ordering (state → ingestion → chunking → search → RAG → citations → eval → calibration → UI → deployment) is sound once M2's internals change.

> **If I were implementing this tomorrow, I would stop and ask for clarification on:** (1) whether Milestone 2 is really meant to use pretrained knowledge as designed, or whether the mock-context approach is adopted instead — this changes what I build on day one; (2) the exact boundary for "unambiguously implied" facts, since as written I can't tell whether a given inference should be confirmed or must trigger a question — this affects every ruling's safety; and (3) what "meaningful retrieval" means operationally in the core loop (a similarity threshold? a minimum passage count?) — without that, I can't tell when the system is supposed to ask a scenario-disambiguation question versus proceed on weak retrieval results.

---

# Part II — Re-Review: Learning Purpose as a First-Class Constraint

The review above optimizes primarily for architectural soundness. This second pass treats the project's dual purpose — a real application *and* a hands-on AI engineering learning vehicle — as a coequal design constraint, and asks not only "is this the cleanest way to build the final product?" but "is this the best sequence for *learning why* the next concept is needed?" Where the two goals conflict, that conflict is called out explicitly rather than defaulting to architectural purity. Some findings from Part I are revised here, not just supplemented — those revisions are marked explicitly. `PRD.md` was not modified by this pass either.

## 1. Does each milestone generate a *felt* need for the next concept, or just introduce concepts in sequence?

The prose "Learning Objectives" section (§373–424) is good at *explaining* why the next milestone exists — e.g., M2's objectives explicitly say clarification-without-retrieval "is the exact failure mode Milestone 6 exists to fix." But explaining a need and manufacturing the experience of hitting that need are different pedagogical tools, and the PRD relies almost entirely on the former.

- **M1 → M2 is the weakest transition.** M1's deliverable is "takes a scenario string, returns a raw LLM response" — full stop. Nothing in M1 asks the developer to *do* anything with that response, so the pain that structured output solves (brittle parsing, inconsistent free-text shape, no reliable yes/no signal) is never actually felt — it has to be taken on faith going into M2. A cheap, high-value addition: have M1 include a small reflective step — try to programmatically extract one specific fact from the raw response (e.g., "does this response indicate the situation is clear enough to rule on?") and watch it be unreliable. That failure is the actual motivation for M2's structured output, and it costs almost nothing to add.
- **M3 → M4 → M5 → M6 are well-motivated experientially**, because each milestone's deliverable is something the *previous* milestone's limitation makes obviously necessary (raw extracted text isn't searchable → chunk it; chunks aren't rankable by relevance → embed them; embeddings alone aren't evaluated against real generation → wire retrieval into generation). This is the strongest-sequenced stretch of the roadmap. Notably, M5's requirement that retrieval be "evaluable independent of the LLM" (§14) is genuinely good pedagogy — it forces isolating the retrieval component's correctness before conflating it with generation quality, which is a real, transferable RAG-engineering habit.
- **M2's own transition to M3 depends on how M2 is resolved** — see the next section, which is the crux of this pass.

## 2. Where did Part I's optimization for architectural purity suppress a valuable learning opportunity?

**Revision of Part I, Finding A1 (Milestone 2 placeholder).** Part I recommended replacing Milestone 2's pretrained-knowledge placeholder with a single hard-coded mock policy passage, on the grounds that it avoids contradicting §7 FR3 and avoids throwaway logic. Re-reading the PRD's own framing, that under-weighted something important: **§14 already explicitly defends the pretrained-knowledge placeholder as intentional pedagogy** — "acceptable as a stepping stone for learning structured output and multi-turn state" — and M2's learning objectives explicitly want the developer to *feel* why materiality-from-memory is unreliable before M6 fixes it. Discarding that experience in favor of a clean single mock passage isn't a free win: a single always-relevant canned passage makes the sufficiency-reasoning exercise nearly trivial (there's only one possible source of truth, so "is this sufficient" degenerates into checking a fixed checklist) — a *weaker* exercise of the actual skill (judging whether retrieved-shaped context covers a material fact) than either the original pretrained-knowledge approach or a better middle path.

**Revised recommendation:** use a small **hand-authored mock corpus** — 2–4 short policy snippets per scenario, deliberately including irrelevant/partial ones alongside the material one — rather than either raw pretrained knowledge or a single canned passage. This:
- Preserves the real skill being exercised (deciding whether *retrieved-shaped* text actually settles a material fact), which single-passage mocking loses.
- Still avoids the literal contradiction with §7 FR3 (materiality now genuinely comes from supplied text, not memory) that Part I correctly flagged as a real inconsistency, not just an architectural nitpick.
- Still motivates M3 experientially — but the motivation shifts from "we broke our own rule" (a normative, not-very-felt reason) to "this hardcoded corpus only covers the handful of scenarios I wrote by hand; it can't handle what an actual judge will type" — a *better*, more organic motivation for ingestion than the current framing gives.
- Keeps the "context provider" interface stable across M2 → M6, so M6 is a swap of the context source, not a rewrite of the reasoning logic — no learning value is lost by removing the throwaway work; only the weakest part (an unmotivated rule violation) is removed.

This is a genuine revision, not a restatement: the mistake wasn't recommending against pretrained knowledge, it was defaulting to the simplest fix (one static passage) instead of the fix that preserves the pedagogical intent.

**Revision of Part I, Finding B1 (LLM provider abstraction).** Part I recommended using `Microsoft.Extensions.AI`'s `IChatClient` instead of a hand-rolled interface at M1, citing the modular-monolith "don't abstract prematurely" principle. But §420 — "What I am building specifically to learn" — explicitly lists **"a provider-abstraction layer"** as one of the things being built from first principles, on the same list as the hand-rolled RAG pipeline and the hand-rolled eval harness. That's a deliberate, stated learning goal, not an accidental premature abstraction. Building your own thin interface at M1 teaches *why* such abstractions look the way they do — a real, transferable lesson — in a way that consuming someone else's pre-built abstraction doesn't. **Part I's B1 recommendation is retracted** under this framing; the PRD's current position (custom interface from M1) is correct *given the project's own stated goals*, and Part I applied a general software-engineering heuristic without checking it against those goals first. Keep the custom abstraction, but keep it genuinely minimal at M1 (one method, enough for the single M1 call) so it doesn't balloon into speculative machinery before there's a second provider to swap to.

## 3. Where might learning-first thinking, taken too far, leave the product incoherent or the learning goals unverifiable?

- **Softened: Milestone 9's calibration ambition (Part I, Finding A5).** Discovering that a small hand-curated eval set can't support meaningful ECE/reliability-diagram statistics is itself a real, valuable, often hard-won lesson in applied ML — so the PRD should not pre-solve this by mandating simplified statistics up front, as Part I suggested. Instead, require the *discovery itself* as a deliverable: a short written limitations analysis ("here is why my N is too small for ECE to be meaningful, here's what I'd need for it to be trustworthy") as a first-class part of M9's output, not just an assumed epilogue. Same underlying concern, but the fix is now "make the limitation a graded learning output" instead of "restrict the method in advance."
- **Softened: Grounding validation (Part I, Finding A4).** Discovering that an LLM judging its own generation isn't independent validation is a legitimate, well-known eval-design lesson. Rather than architecting it away in advance, add it as an explicit learning objective at Milestone 7 ("recognize why same-model grounding validation has correlated blind spots; identify which checks should move to deterministic code as a result") and let the developer arrive at the deterministic/LLM split through their own analysis, with the PRD only requiring the *conclusion* — which checks became deterministic and why — be written down.
- **Not softened: the confirmed/hypothesis boundary (Part I, Finding A2).** This isn't a case for "let them discover it." An ambiguous safety-relevant definition (when is a fact "confirmed" enough to rule on) sitting unresolved risks the developer building inconsistent behavior across milestones without ever being forced to confront it, because nothing downstream will loudly break the way a bad retrieval-confidence choice or an uncalibrated confidence score will. Recommend resolving this now, but frame the resolution *as* a learning objective — "wrestling with the boundary between inference and confirmation is a core, transferable AI-agent skill (this shows up constantly in tool-using agents deciding what they actually know vs. what they're assuming)" — added to M2's learning objectives, not silently patched.
- **Downgraded: retrieval-confidence fallback / scenario-disambiguation split (Part I, Finding A3).** Under the learning-first lens this drops from "fix before M1" to "let it be a discovery point at M6," where hitting a scenario retrieval can't confidently handle is a natural, felt problem once real retrieval exists — pre-specifying the exact threshold/mechanism now would rob the developer of diagnosing the failure mode themselves. The PRD should simply flag it as an *expected* open question to resolve during M6, rather than requiring it be designed today.

## 4. Five most valuable changes to improve the learning experience

1. **Add a manufactured-friction exercise to Milestone 1** — attempt naive text extraction from the raw LLM response and observe it fail, so structured output at M2 solves a felt problem rather than an asserted one.
2. **Replace Milestone 2's single-source placeholder (pretrained knowledge) with a small hand-authored mock corpus** (multiple snippets of varying relevance per scenario) rather than either raw model memory or one canned passage — preserves the sufficiency-reasoning exercise, removes the FR3 contradiction, and gives M3 a more organic motivation ("this hardcoded corpus doesn't generalize").
3. **Keep the custom LLM provider abstraction at Milestone 1** (retracting Part I's recommendation to use `IChatClient`) — it's an explicitly stated first-principles learning goal (§420), not incidental complexity; just keep it genuinely minimal at M1.
4. **Turn the two "known-imperfect architecture" spots (M7 grounding validation, M9 calibration) into required written self-reflection deliverables** rather than pre-solved designs — require a short limitations write-up at each ("why isn't this fully independent/statistically rigorous, and what would fixing it require") so the discovery is captured and graded, not just implicitly experienced.
5. **Add a lightweight per-milestone reflection ritual** — a running log where each milestone closes with a few sentences explaining, in the developer's own words, the concept introduced and why the prior milestone's approach needed it. §17 already claims this as a success criterion ("able to explain, in their own words...") but nothing in the PRD operationalizes it; this costs almost nothing and doubles as free portfolio-write-up material for M11.

## 5. Is the PRD ready to begin Milestone 1 without further architecture work?

**Ready With Minor Changes.**

Under the learning-first framing, most of what Part I called "critical, fix before M1" (A3, A4, A5) is better handled as scoped learning objectives to resolve *at* their natural milestone rather than pre-architected now — pre-solving them would cost real learning value for no product benefit this early. Two things are still worth resolving before M1, because they're foundational enough that getting them wrong shapes every downstream milestone's data model and can't be meaningfully "discovered" later without rework: **the confirmed/hypothesis fact boundary** and **Milestone 2's design (mock corpus vs. pretrained knowledge)**. Both are small, scoped changes to the PRD text, not new architecture — hence "minor changes," not "not ready."

---

> **If the primary success criterion were "by the end of this project, the developer deeply understands how and why this class of AI application works," what would change about this PRD?**
>
> Stop treating "avoid rework" as a proxy for "good sequencing." A few milestones in this PRD are already structured as build-the-naive-version-then-discover-why-it's-wrong (M2's placeholder, implicitly M7's grounding validation, implicitly M9's calibration ambition) — that pattern is not a flaw to engineer away, it's one of the most effective ways to actually learn *why* RAG, formal grounding checks, and calibration validation exist, rather than being told why. Part I of this review treated that pattern as something to minimize; this pass reversed that judgment for two of the three instances. The remaining structural gap is that the PRD narrates the "why" in prose (the Learning Objectives section) but rarely manufactures the experience of needing it — Milestone 1 is the clearest example, but it's really a roadmap-wide pattern. Add small, cheap "hit the wall yourself" exercises at each transition point, and make the moments where the architecture is *known to be imperfect* (grounding validation, calibration on small N) into explicit deliverables where the developer has to articulate the limitation in writing — because being able to explain why an approach is insufficient is a stronger signal of understanding than never building the insufficient version at all.

---

## Status: Changes Applied to PRD.md

All changes recommended in Part II's "five most valuable changes" (plus the two supporting findings referenced there — the confirmed/hypothesis boundary and the retrieval-confidence open question) have been applied to `PRD.md`, one at a time with explicit confirmation. This section is the changelog.

| # | Change | Where applied in `PRD.md` | Status |
|---|---|---|---|
| 1 | Milestone 1 manufactured-friction exercise (naive text extraction from the raw response, observed to fail) | §14 roadmap table, Milestone 1 row; Learning Objectives — Milestone 1 | Applied |
| 2 | Milestone 2 mock corpus replaces the pretrained-knowledge placeholder (2–4 hand-authored snippets per scenario, varying relevance) | §14 "Known, intentional gap" note (retitled "Milestone 2 uses a mock corpus, not pretrained knowledge"); §14 roadmap table, Milestone 2 row; Learning Objectives — Milestone 2 | Applied |
| 3 | Confirmed-fact definition tightened to strict logical entailment (excludes domain-plausibility inference); boundary-wrestling added as an explicit M2 learning objective | §8 "Structured state" bullet; Learning Objectives — Milestone 2 | Applied |
| 4 | Retrieval-confidence / scenario-disambiguation fallback flagged as an open question to resolve at Milestone 6, not designed in advance | §18 Open Questions (new bullet) | Applied |
| 5 | Milestone 7 required written analysis: which grounding checks are deterministic vs. require model judgment, and why same-model grounding validation isn't fully independent | §14 roadmap table, Milestone 7 row; Learning Objectives — Milestone 7 | Applied |
| 6 | Milestone 9 required written limitations analysis: whether the eval dataset's size actually supports the chosen calibration statistics | §14 roadmap table, Milestone 9 row; Learning Objectives — Milestone 9 | Applied |
| 7 | Per-milestone reflection log ritual (a few sentences per milestone: concept introduced, why the prior milestone's approach made it necessary) | §14 roadmap intro paragraph; §17 "Learning/portfolio" bullet | Applied |
| — | Keep the custom LLM provider abstraction at Milestone 1 (retraction of Part I's `IChatClient` recommendation) | No edit needed — `PRD.md` already specified a custom interface from Milestone 1; this item only reversed Part I's review recommendation, it never proposed a PRD change | No change required |

Part I findings **not** carried into a PRD edit in this pass (per Part II's re-prioritization, these remain intentionally deferred to their natural milestone rather than pre-architected):
- Grounding-validation architecture split (Part I A4) — converted to a Milestone 7 learning deliverable (#5 above) rather than a pre-built architecture change.
- Milestone 9 statistical method restrictions (Part I A5) — converted to a Milestone 9 learning deliverable (#6 above) rather than a pre-restricted method.
- Terminology consistency items (Part I B8–B10: "ruling" vs. "recommendation," "reliability" overload, "Game-State / Session Store" naming) — not addressed in this pass; still open if a future pass wants to take them up.
- Source freshness/versioning metadata (Part I B3) and other Part I "Important Improvements" not listed above — not addressed in this pass.
