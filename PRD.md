# PokéJudge AI — Product Requirements Document

## 1. Product Overview

PokéJudge AI is an AI-powered decision-support tool for Pokémon Trading Card Game (TCG) judges working organized-play events (League Challenges, League Cups, prereleases, and similar). A judge describes a rules, game-state, or tournament-policy situation in natural language. The system determines whether it has enough information to rule on the situation; if not, it asks targeted clarifying questions. Once the material facts are established, it retrieves relevant passages from official rules and policy documents and produces a cited, judge-facing recommendation labeled with a **Source Support** classification (Strong / Partial / Insufficient) describing how well the retrieved authoritative material backs the recommendation — separating explicit policy from inference, and explicitly flagging when the source material is insufficient.

This is also an AI engineering learning project. The backend is built in C# / ASP.NET Core, and each major AI capability (LLM calls, structured output, RAG, evaluation, etc.) is introduced only when the product has a concrete need for it, in service of building real understanding rather than assembling a framework-driven stack.

## 2. Problem Statement

Judges at organized-play events must make fast, defensible rulings under time pressure, often on edge cases (missed prizes, misresolved attacks, illegal game states, deck/decklist issues) that aren't always obvious from memory. Existing resources — the TCG rulebook, the Play! Pokémon Tournament Rules Handbook, and penalty guidelines — are authoritative but large, cross-referenced, and not optimized for rapid lookup mid-event. A judge who guesses risks an incorrect or inconsistent ruling; a judge who stops to dig through PDFs loses time at the table. There is no tool that helps a judge quickly gather the *right* facts and then locates the *right* authoritative passages to support a ruling.

## 3. Target Users

- **Primary:** Active Pokémon TCG judges (Judge 1–3 certified or equivalent local judges) running or supporting League Challenges, League Cups, prereleases, and similar events.
- **Assumed knowledge:** Familiar with basic TCG terminology, turn structure, and common judge vocabulary (e.g., "prize," "knock out," "game state," "infraction"). The system does not need to explain basic game concepts.
- **Not the target user:** Players seeking rules explanations for their own games. This is explicitly not a general player-facing rules chatbot.

## 4. Primary Judge Use Cases

1. A judge encounters a game-state error (e.g., missed prize, skipped step, incorrect attack resolution) discovered mid-event, possibly turns after it occurred, and needs to know how to repair game state and whether a penalty applies.
2. A judge needs to confirm whether a specific action is legal (e.g., timing of an Ability, a rule interaction) and cite the correct rule.
3. A judge needs to determine the correct infraction category and penalty for a procedural or game-play error under tournament policy.
4. A judge has an incomplete picture of what happened and needs help identifying which additional facts matter before a ruling can be made.
5. A judge needs a tournament-procedure answer (e.g., deck/decklist issue, round-timing question) distinct from an in-game rules question.

## 5. Goals

- Help judges reach accurate, policy-grounded rulings faster than manual document lookup.
- Ensure the system investigates before it advises — gathering material facts before proposing a ruling.
- Ground every substantive recommendation in cited, retrievable source material.
- Make the boundary between "explicit policy," "reasonable inference," and "insufficient information" visible to the judge.
- Represent evidentiary strength honestly: describe how strongly retrieved authoritative material supports a recommendation (Source Support) rather than presenting an unvalidated model-generated confidence score as if it were a calibrated probability.
- Build the system incrementally as a vehicle for learning modern AI application development in a .NET context.

## 6. Non-Goals

- Not a general Pokémon rules chatbot for players.
- Not a replacement for official Pokémon policy, Player/Judge escalation paths, or Head Judge / Tournament Organizer discretion.
- Not an automated penalty-issuing system — it produces recommendations, not binding rulings.
- Not intended (at least through Milestone 10) to manage tournament operations (pairings, standings, deck registration, etc.).
- Not optimized for casual conversation, personality, or open-ended Pokémon Q&A.
- Not initially multi-language; English-language source material and responses only.

## 7. Functional Requirements

1. Accept a free-text scenario description from a judge.
2. Retrieve an initial set of candidate rule/policy passages relevant to the scenario as described so far, even if the scenario is incomplete.
3. Determine, based on what those retrieved passages actually require, whether the scenario contains sufficient material facts to apply them and produce a ruling. Materiality is derived from the retrieved text, not from the model's general/pretrained Pokémon knowledge.
4. If insufficient, generate a small number of targeted clarifying questions, each tied to a specific fact that a retrieved passage indicates would change the outcome.
5. Accept follow-up answers in subsequent turns, update an internal representation of confirmed facts, unknown facts, and any unconfirmed hypotheses, and re-retrieve and re-evaluate sufficiency after each answer (new facts may surface additional relevant passages that weren't retrievable from the original, incomplete scenario).
6. Avoid re-asking for facts already supplied in the conversation.
7. Once sufficient, finalize retrieval against the complete accumulated scenario and generate a structured response containing (at minimum): recommended ruling/action, explanation, game-state repair steps (if applicable), infraction/penalty guidance (if applicable), source citations, and a **Source Support classification** (Strong / Partial / Insufficient) reflecting how well retrieved authoritative material backs the recommendation — not a model-reported confidence percentage.
8. Persist conversation/game-state across multiple turns within a single scenario session.
9. Clearly indicate when the system cannot produce a confident ruling from available material (Source Support: Insufficient), rather than guessing.
10. (Milestone 10+) Present the above through a web UI organized around the scenario → clarification → known facts → recommendation → sources flow.

## 8. AI-Specific Requirements

- **Retrieval-driven materiality:** Which missing facts are "material" must be derived from retrieved rule/policy passages, not the model's pretrained Pokémon knowledge. A clarifying question should be traceable to a specific retrieved passage whose applicability depends on that fact — e.g., the system should learn that "when was the error discovered?" matters *because* a retrieved penalty-guideline passage conditions the remedy on discovery timing, not because the model already knows Pokémon judging conventions.
- **Iterative retrieval:** Retrieval is not a single terminal step. It runs on the initial (possibly incomplete) scenario to surface candidate rules, and again after each new fact the judge supplies, since new facts can make previously unretrieved passages relevant.
- **Sufficiency assessment:** The system must explicitly reason, against the currently retrieved passages, about whether it has enough information before generating a ruling — this is a distinct step, not an implicit side effect of the main prompt.
- **Structured state:** Game-state facts should be tracked in a structured form independent of the model's free-text response, so the application — not just the model's memory — owns conversation state. Facts are tracked as **confirmed** (explicitly and literally stated by the judge, or a strict logical/definitional entailment of what was stated with zero degrees of freedom — e.g., "no other Pokémon in play" entails "no non-Active Pokémon" — never a domain-plausibility inference about what *probably* happened), **unknown** (not yet supplied), or **possible interpretation / hypothesis** (a plausible but unconfirmed reading of what happened, including any inference beyond strict logical entailment) — deliberately not a two-way "known vs. unknown" split, since a plausible guess is not the same as a fact.
- **No material inference:** A possible interpretation/hypothesis must never be treated as a confirmed fact, and must never be used to support a ruling, repair step, or penalty determination. If a hypothesis could plausibly affect the outcome, the system must ask a clarifying question to confirm or rule it out rather than assume it. Example: from "Player A attacked and forgot a Prize," the system must not infer that the attack resulted in a Knock Out just because taking a Prize would normally follow one — it must either already have that fact or ask.
- **Retrieval grounding:** Rulings must be generated using retrieved source passages, not the model's general Pokémon knowledge. The prompt should make clear that unsupported claims are unacceptable.
- **Citations:** Every substantive ruling, repair step, or penalty recommendation should reference the specific document/section it is drawn from.
- **Source Support, not confidence:** The judge-facing reliability signal is **Source Support** — `Strong` / `Partial` / `Insufficient` — describing how strongly the retrieved authoritative material backs the recommendation. It is not the model's self-reported confidence. *Confidence describes belief; Source Support describes evidence.*
  - **Strong:** relevant authoritative material was retrieved and directly addresses the material issue; the recommended ruling, repair, or penalty traces to specific source passages; all required material facts are confirmed (not merely hypothesized); no unresolved conflict exists among the relevant current sources.
  - **Partial:** relevant authoritative material was retrieved and addresses at least part of the situation, but some interpretation or judge discretion is required, or the source does not explicitly prescribe every material part of the recommendation. The response must clearly identify which portion is directly supported and which portion involves interpretation.
  - **Insufficient:** applies when relevant authoritative policy cannot be found, retrieved sources do not answer the material question, required game-state facts remain unknown, available sources conflict in a way the system cannot resolve, or a recommendation would require unsupported extrapolation. In this state the system must not produce a definitive ruling.
- **Source Support must be criteria-based, not a raw model opinion:** The classification must be derived from testable, observable conditions — whether authoritative sources were retrieved, whether retrieved passages directly cover the material issue, whether citations actually support the generated claims, whether all facts the applicable policy requires are confirmed, and whether relevant sources conflict. An LLM may be used as one component in assessing these conditions, but Source Support itself is a defined product concept with checkable criteria, not simply whatever label the model chooses to output.
- **Source Support is tied to sufficiency:** If required material facts are still unknown, the final recommendation generally cannot be classified `Strong` — game-state sufficiency (section 7) and Source Support are linked judgments, not independent ones.
- **No unvalidated numeric confidence:** The system must not present arbitrary LLM-generated confidence percentages (e.g., "Confidence: 87%") to judges unless there is empirical evidence that the number is calibrated against actual system correctness (see Milestone 9, Confidence Calibration and Reliability). An LLM's ability to produce a percentage does not make that percentage a statistically meaningful probability. Model-generated confidence values collected experimentally are evaluation data, not validated probabilities, until proven otherwise. Avoid false precision.
- **Insufficiency signaling:** When Source Support is `Insufficient`, the response must say so plainly rather than extrapolating confidently, and must not present a definitive ruling.
- **Provider abstraction:** LLM calls should go through an internal abstraction so the underlying provider/model can be swapped without rewriting application logic.
- **Structured outputs:** Model responses that feed application logic (sufficiency flag, clarifying questions, final ruling object, Source Support classification) should use structured output (schema-constrained), not parsed free text, once Milestone 2 is reached.

## 9. Reliability and Safety Requirements

- The system must never present a fabricated rule, penalty, or citation as authoritative. If it cannot ground a claim, it must say so explicitly.
- The system must not issue a ruling when it has flagged material facts as missing.
- The system must not display an LLM-generated confidence percentage to judges unless it has been empirically validated as calibrated (Milestone 9, Confidence Calibration and Reliability). The qualitative Source Support classification (Strong / Partial / Insufficient) is the default and initial judge-facing reliability signal, and remains so unless calibration evidence justifies otherwise.
- All rulings/recommendations must be framed as *recommendations to a judge exercising their own authority and discretion*, not as final, binding decisions.
- Errors from the LLM provider, retrieval layer, or ingestion pipeline must fail visibly (clear error/log) rather than silently degrading into an unsupported answer.
- API keys and other secrets must never be committed to source control or logged; they are loaded via configuration/secret management appropriate to the environment (e.g., user-secrets locally, environment variables / secret store in deployment).
- Basic input handling should guard against prompt injection via ingested documents or user input affecting system instructions (e.g., treating document content strictly as retrieved data, not instructions).

## 10. Judge Experience / UX Requirements

- Fast to enter a scenario; minimal friction to get started (single text box for the initial description).
- Clarifying questions presented as a short, focused list — not a long form — with brief rationale when useful.
- A visible, evolving summary of "what we know so far" so the judge can confirm the system has understood the situation correctly.
- Final output organized for quick scanning at a table during an event: recommended action first, then its Source Support classification, then repair/penalty, then reasoning and sources for anyone who wants to verify.
- Display the Source Support classification (Strong / Partial / Insufficient) prominently alongside the recommendation — not as a numeric confidence percentage — with a brief note on which portion is directly supported vs. which involves interpretation when Partial.
- Sources should be specific enough to locate in the physical/PDF rulebook (document name + section/rule number), not just a vague reference.
- The tone should be operational and neutral — an assistant supporting a judge's own authority, not a persona.

## 11. High-Level Architecture

The core loop is **retrieve → assess → clarify → re-retrieve → generate → validate grounding → assign Source Support**, not "clarify first, retrieve later" and not "generate, then ask the model how confident it feels." The system must not decide which facts are material from the model's pretrained Pokémon knowledge — materiality is determined by what the *retrieved* rules/policy actually turn on. A missing fact only becomes a clarifying question because some retrieved passage's applicability depends on it. Likewise, the final Source Support label is not the model's self-reported confidence — it is assigned by checking the ruling against observable criteria (retrieval success, citation coverage, fact sufficiency, source conflict).

```
                        ┌─────────────────────────┐
                        │   Judge-facing client    │
                        │ (console → later: web UI)│
                        └────────────┬─────────────┘
                                     │
                        ┌────────────▼─────────────┐
                        │   ASP.NET Core API layer  │
                        │  (scenario/session mgmt)  │
                        └────────────┬─────────────┘
                                     │
                     ┌───────────────▼────────────────┐
                     │   Game-State / Session Store     │
                     │ (confirmed facts / unknown facts /│
                     │  unconfirmed hypotheses;           │
                     │  clarification history)           │
                     └───────────────┬────────────────┘
                                     │  current known facts
                                     │◄──────────────────────────────┐
                     ┌───────────────▼────────────────┐              │
                     │       Retrieval (RAG)             │              │
                     │  - embeddings / vector search      │              │
                     │  - candidate rule/policy passages   │              │
                     │    for the facts known so far        │              │
                     └───────────────┬────────────────┘              │
                                     │  retrieved passages                │
                     ┌───────────────▼────────────────┐              │
                     │  Sufficiency & Clarification       │              │
                     │  Engine (LLM + structured output)   │              │
                     │  "Do these retrieved rules turn on   │              │
                     │   a fact we don't have yet?"          │              │
                     └───────┬─────────────────┬────────┘              │
                             │                   │                      │
                      insufficient          sufficient                 │
                             │                   │                      │
                    ┌────────▼────────┐          │                      │
                    │ Ask clarifying   │          │                      │
                    │ question(s)      │          │                      │
                    └────────┬────────┘          │                      │
                             │ judge answers       │                      │
                             └─────────────────────┴──── update known facts,
                                                          loop back to Retrieval
                                                                          ▲
                                                          (repeat until sufficient
                                                           or exhausted)
                                                                          │
                                                                          │
                                     sufficient ───────────────────────────┘
                                     │
                     ┌───────────────▼────────────────┐
                     │        Ruling Generation (LLM)     │
                     │ scenario + full game state + final  │
                     │ retrieved passages → structured      │
                     │ ruling response                       │
                     └────────────────┬────────────────────┘
                                      │
                     ┌────────────────▼────────────────┐
                     │  Grounding Validation &            │
                     │  Source Support Assignment          │
                     │  - does each claim trace to a        │
                     │    retrieved passage?                 │
                     │  - are required facts confirmed        │
                     │    or only hypothesized?                │
                     │  - do sources conflict?                  │
                     │  → Strong / Partial / Insufficient        │
                     └────────────────┬────────────────────┘
                                      │
                          ┌───────────▼────────────┐
                          │   LLM Provider Adapter   │
                          │ (interface; swappable —   │
                          │  used by every LLM step     │
                          │  above, including grounding  │
                          │  validation)                   │
                          └───────────────────────────┘
```

Key implications:
- Retrieval is not a one-shot step that runs after clarification finishes — it runs at least once *before* the first clarifying question is generated, and again after each new fact the judge supplies (new facts can surface additional relevant rules that weren't retrievable from the original, incomplete scenario).
- Source Support assignment is not the last thing an LLM call "decides" unchecked — it is validated against the same observable criteria described in section 8 (retrieval success, citation coverage, fact sufficiency, source conflict). If required facts are still unknown at this point, the recommendation cannot be classified `Strong`.

Supporting, offline pipeline (Milestones 3–4): document ingestion → text extraction/normalization → chunking → embedding generation → vector store population.

### Development philosophy: modular monolith

> Logical separation first. Physical separation only when there is a demonstrated need.

The diagram above is the **conceptual architecture** — the responsibilities the finished system needs (LLM interaction, structured game-state, clarification, retrieval, document ingestion, ruling/guidance generation, evaluation). It is not a deployment diagram, and it does not imply each box should become a separate service, project, or deployment unit. PokéJudge should begin, and likely remain for a long time, a **modular monolith**: one .NET solution with clear internal module boundaries, not a collection of independently deployed services.

Separating responsibilities, abstracting behavior, splitting projects, and splitting deployment units are four different decisions. Adding AI to an application does not, by itself, justify collapsing them into one — that distinction is worth as much attention as any RAG concept in this project.

#### Initial implementation architecture

The actual code structure should grow with the milestones, not anticipate their end state. An early, illustrative structure:

```
PokeJudge
│
├── AI
├── Models
├── Retrieval
├── Ingestion
└── Evaluation
```

This is illustrative, not a required final layout. A likely progression:

**Milestone 1**
```
Console App
    ↓
LLM
```
No session store, retrieval layer, ingestion service, vector database, separate API project, multiple class-library projects, multiple provider implementations, message bus, or microservices — a single console app making one LLM call is the entire deliverable.

**Milestone 2**
```
Application
├── AI
├── Structured State
└── Clarification
```

**Later, as retrieval/ingestion/evaluation come online (roughly Milestones 5–8)**
```
Application
├── AI
├── State
├── Retrieval
├── Ingestion
├── Evaluation
└── API
```

The React frontend (Milestone 10) sits outside this backend application as its own client — that's the one boundary expected to be a genuinely separate deployable, since it runs in the browser rather than the .NET process, not because "frontend" and "backend" are different AI capabilities.

Do not create separate projects such as `PokeJudge.Api`, `PokeJudge.AI`, `PokeJudge.Retrieval`, `PokeJudge.State`, or `PokeJudge.Ingestion` preemptively. A small number of projects may eventually be warranted — e.g., splitting out tests, or an offline ingestion tool — but only when a concrete engineering reason emerges, not for architecture aesthetics.

## 12. Proposed Technology Stack

| Layer | Choice | Notes |
|---|---|---|
| Language/runtime | C# / .NET 10 | Matches existing project scaffold. |
| API | ASP.NET Core (minimal APIs or controllers) | Introduced starting Milestone 2/10 as needed; Milestone 1 can be a console app. |
| LLM access | Direct provider SDK or Microsoft.Extensions.AI abstraction | Avoid heavy orchestration frameworks; wrap behind a small internal interface for swappability. |
| Structured output | Provider-native structured/tool output, deserialized to C# models | Introduced at Milestone 2. |
| Document ingestion | Plain text/PDF extraction libraries as needed | Introduced at Milestone 3. |
| Embeddings | Provider embedding API (via same swappable interface) | Introduced at Milestone 4. |
| Vector store | Start with a simple/local option (e.g., an in-process or lightweight vector store); revisit for a dedicated vector DB only when scale/features require it | Introduced at Milestone 5. |
| Frontend | React + TypeScript | Deferred to Milestone 10; backend/API-first until then. |
| Testing | xUnit (or similar) for app logic; separate evaluation harness for AI quality | Introduced incrementally; formal eval harness at Milestone 8, reused (not replaced) by Milestone 9's calibration analysis. |
| Secrets/config | .NET user-secrets locally; environment variables / secret manager in deployment | Applies from Milestone 1 onward. |

Exact package/library choices (e.g., specific PDF library, specific vector store) are intentionally deferred until the milestone that needs them, per the project's "introduce when needed" philosophy. Milestone 9's calibration work is intentionally scoped to simple statistics (bucketing, calibration curves, ECE/Brier score) over the existing eval dataset — it does not require new observability or ML infrastructure. Consistent with the modular monolith principle in section 11, every row above is a logical module within a single .NET solution, not a separate project or service, until a concrete need justifies otherwise.

## 13. Data / Source Strategy

Authoritative source categories to ingest, in rough priority order:

1. **Pokémon TCG Rulebook** — core game rules.
2. **Play! Pokémon Tournament Rules Handbook** — tournament procedure, infractions, penalty philosophy.
3. **Pokémon TCG Tournament Rules** — format/event-specific rules.
4. **Penalty Guidelines** — infraction → penalty mapping.
5. Official rules clarifications or other authoritative rulings/policy documents, where clearly sourced.

Requirements for ingested content:
- Preserve enough structure/metadata (document title, version/date, section or rule number) to produce a specific citation.
- Track source document versioning, since Play! Pokémon policy documents are revised periodically; the system should be able to indicate which version of a document a citation refers to.
- Only ingest documents the developer has legitimate access to and can redistribute/process for this personal/portfolio use; do not scrape or include unauthorized copyrighted redistributions beyond fair-use excerpts needed for citation context.

## 14. Milestone Roadmap

The roadmap below follows the learning milestones from the originating brief, with product deliverables per milestone made explicit. Each milestone closes with a short reflection log entry (a few sentences, in the developer's own words): what AI concept was introduced, and why the previous milestone's approach made it necessary. This operationalizes the learning/portfolio success criteria in section 17 and doubles as source material for the Milestone 11 write-up.

**Milestone 2 uses a mock corpus, not pretrained knowledge:** at Milestone 2, no real retrieval exists yet. Rather than letting the sufficiency/clarification logic guess at materiality from the model's pretrained Pokémon knowledge (which section 8 says the final system must never rely on), Milestone 2 supplies a small, hand-authored mock corpus — 2–4 short policy snippets per scenario, including deliberately irrelevant or partial ones alongside the material one — as supplied context. The sufficiency engine reasons only over this supplied text, exactly as it will reason over real retrieved passages later. This keeps materiality genuinely text-derived from Milestone 2 onward (no contradiction with section 7 FR3), and it exercises the real skill (judging whether retrieved-shaped context settles a material fact) rather than trivializing it. Milestone 6 replaces the mock corpus with real retrieval behind the same interface — a swap of the context source, not a rewrite of the sufficiency/clarification logic.

| # | Milestone | Product deliverable |
|---|---|---|
| 1 | First LLM interaction | Console app: takes a scenario string, returns a raw LLM response. No structure, no RAG, no session store, no API project — see section 11's modular-monolith progression. Includes a brief exercise: attempt to programmatically extract one specific answer (e.g., a yes/no sufficiency signal) from the raw text response using naive string handling, and observe how unreliable it is — this friction motivates Milestone 2's structured output requirement. |
| 2 | Judge-focused prompting, clarification, structured responses | System prompt + structured output model expressing sufficiency, clarifying questions, and a draft ruling shape. Multi-turn state held in memory. **Sufficiency/materiality is reasoned over a small hand-authored mock corpus (not pretrained knowledge)** — see note above; Milestone 6 swaps in real retrieval behind the same interface. |
| 3 | Document ingestion | Pipeline that extracts and normalizes text from source documents with citation metadata retained. |
| 4 | Chunking and embeddings | Source text split into chunks; embeddings generated and stored alongside metadata. |
| 5 | Vector search | Semantic retrieval of relevant chunks for a given scenario, evaluable independent of the LLM. |
| 6 | RAG | Ruling generation now conditioned on retrieved chunks + accumulated game state, **and** the Milestone 2 clarification logic is rebuilt as the retrieve → assess → clarify → re-retrieve loop from section 11, replacing mock-corpus-based materiality with retrieval-grounded materiality. A first-pass Source Support classification (Strong/Partial/Insufficient) is introduced here as an output of ruling generation. |
| 7 | Citations and grounding | Responses explicitly cite sources and distinguish explicit policy / reasonable interpretation of policy / insufficient-information cases. (This is about interpreting retrieved *rules*, distinct from the confirmed/unknown/hypothesis split used for *game-state facts*.) Source Support is formalized here: tied to explicit, testable criteria (retrieval success, citation coverage, fact sufficiency, source conflict) rather than the model's free-form judgment. Includes a short written analysis of which grounding checks are deterministic (application logic) versus which genuinely require model judgment, and why an LLM validating its own generation is not fully independent validation. |
| 8 | Evaluation | Scenario dataset + harness measuring clarification behavior, retrieval quality, ruling quality, and citation/grounding correctness — including hand-authored branching scenarios that score the investigation path (clarifying questions, branch chosen, state updates), not just the final answer. |
| 9 | Confidence Calibration and Reliability | Exploratory milestone, built on the Milestone 8 eval dataset: investigate whether a model-reported correctness probability is empirically calibrated, and whether combining it with other signals (Source Support, retrieval quality, citation coverage) beats self-reported confidence alone. Product decision at the end: expose a numeric reliability estimate only if it demonstrates real calibration; otherwise Source Support remains the sole judge-facing signal. Not required for a working product. Includes a required written limitations analysis: whether the Milestone 8 dataset's size is actually sufficient to support the chosen calibration statistics (e.g., per-bucket sample counts for ECE/reliability diagrams), and what would be needed for the analysis to be statistically trustworthy. |
| 10 | Judge-focused application UI | Web UI (React/TS) surfacing scenario, clarifications, known facts, recommendation, Source Support, repair, penalty, reasoning, sources. |
| 11 | Deployment and portfolio polish | Deployed instance + architecture/design/evaluation write-up. |

## 15. Testing and Evaluation Strategy

- **Unit/integration tests** for deterministic application logic: session/game-state tracking, retrieval plumbing, API contracts. Standard .NET testing practice — not an AI-specific concern.
- **AI evaluation harness (Milestone 8)** using a curated scenario dataset covering: missed game actions, incorrect attack resolution, illegal game states, drawing too many cards, prize errors, deck/decklist issues, timing questions, tournament procedure, penalty questions, discretion-required scenarios, and intentionally incomplete prompts.
- Each eval scenario should specify, where applicable: expected relevant source material, and an expected ruling or set of acceptable outcomes.
- Metrics tracked, not just manual spot-checking:
  - Does the system correctly recognize when facts are missing?
  - Are the clarifying questions the *correct*, material ones (not excessive, not irrelevant)?
  - Does it avoid premature rulings?
  - Does it correctly incorporate follow-up answers?
  - Does it reach the appropriate final ruling once facts are sufficient?
  - Retrieval quality (are the correct source sections retrieved)?
  - Answer quality and citation correctness.
- Evaluation results should be reviewable over time (e.g., simple run logs/reports) so regressions are visible as prompts/retrieval change.

### Branching / trajectory evaluation

Testing only the final answer is not sufficient for a system whose core job is to investigate before it advises. A subset of the Milestone 8 dataset should be **branching scenarios**, where the correct next step depends on the judge's answer to a clarifying question — e.g., "Player A forgot to take a Prize card" branches on "Was a Pokémon Knocked Out?" into materially different relevant policy, follow-up questions, and expected outcomes per branch (Yes / No).

Each branching scenario should be able to represent:
- An initial (incomplete) scenario
- The expected material unknown(s)
- The expected or acceptable clarifying question(s)
- The possible judge responses
- The expected next question(s) for each response branch
- The expected relevant source material for each branch
- The expected final recommendation, or acceptable set of recommendations, per branch

For these scenarios, evaluate whether PokéJudge:
1. Correctly recognizes that the initial scenario is incomplete.
2. Identifies the material fact that must be established.
3. Asks an appropriate clarifying question.
4. Avoids asking irrelevant or unnecessary questions.
5. Does not prematurely make a ruling.
6. Correctly updates structured game-state after receiving the answer.
7. Chooses the appropriate next decision path based on that answer.
8. Retrieves the appropriate policy for that branch.
9. Asks additional questions when the new state still lacks required information.
10. Produces the correct or acceptable final judge guidance once enough information is known.

**Principle: score the investigation, not just the destination.** A correct final ruling reached via a wrong investigation path — skipped a necessary clarification, chose the wrong branch, retrieved the wrong policy, then landed on the right answer anyway — should not automatically receive full credit. It should be distinguishable from a correct ruling reached via a correct investigation. This is a lightweight form of **trajectory (process) evaluation**: testing intermediate decisions and state transitions, not just input → output.

A simple hand-authored representation of branching scenarios (e.g., a small tree per scenario) is sufficient initially. This does not require a specialized trajectory-evaluation framework or new infrastructure — it's an extension of the same Milestone 8 harness.

- This dataset also feeds the later Confidence Calibration and Reliability milestone (Milestone 9), which studies whether model-reported correctness probabilities are empirically calibrated. That analysis is exploratory and separate from this milestone's pass/fail quality metrics — Milestone 8 itself does not depend on it.

## 16. Security Considerations

- API keys/secrets never committed to source control; use `dotnet user-secrets` locally and environment variables or a secret manager in any deployed environment.
- Treat retrieved document content and user input as data, not instructions — guard system prompts against injection attempts embedded in either.
- Validate/sanitize any user input surfaced back into logs or UI to avoid injection into downstream tooling.
- Rate-limit or otherwise bound LLM API usage per session to control cost and abuse exposure once exposed beyond local use.
- If deployed publicly, apply standard ASP.NET Core hardening (HTTPS, minimal exposed surface, no verbose error leakage) and avoid storing any real personally identifiable tournament/player information — use synthetic scenario data for demos and evals.

## 17. Success Criteria

**Product:**
- Given a representative event scenario, the system asks materially correct clarifying questions before ruling (not zero, not excessive).
- Given sufficient facts, the system produces a ruling with accurate citations traceable to real rule/policy sections.
- The system visibly declines to guess when source material is insufficient, rather than fabricating an answer.
- A judge unfamiliar with the tool's internals can use it end-to-end (scenario → clarification → ruling) without confusion.
- The Source Support classification (Strong / Partial / Insufficient) reflects observable retrieval, citation, and fact-sufficiency criteria — not an unvalidated model confidence score — and correctly downgrades to `Insufficient` when it should.

**Learning/portfolio:**
- Each milestone leaves the developer able to explain, in their own words, the AI concept it introduced and the tradeoffs involved — captured as the short reflection log entry described in section 14.
- The final project demonstrates a working RAG pipeline, structured multi-turn interaction, grounded citation behavior, and a real evaluation methodology — documented clearly enough for a portfolio reviewer to follow the reasoning.
- The project can articulate, with evidence, why numerical confidence was postponed and what it would take to justify exposing one — demonstrating that reliability signals should be validated against measured behavior rather than assumed from model output.

## 18. Open Questions / Decisions We Can Intentionally Defer

- Exact vector store choice (in-process library vs. dedicated service) — defer to Milestone 5.
- Exact LLM provider/model — defer, but design the provider interface from Milestone 1 so switching is cheap.
- How to handle conflicting or superseded source document versions — defer to Milestone 3, revisit at Milestone 7 (grounding).
- Whether/how to support saving or exporting past rulings for a judge's own reference — defer past Milestone 10.
- Whether authentication/multi-user accounts are needed, or a single-user local/demo deployment is sufficient for the portfolio goal — defer to Milestone 11.
- Whether tool/function calling has a concrete use case here (e.g., looking up a specific card's text) — defer until a real need appears; do not add speculatively.
- Deployment target (cloud host, container, etc.) — defer to Milestone 11.
- Level of formality around penalty recommendations (e.g., should the system ever suggest a specific penalty tier, or only point to the applicable guideline section) — worth deciding before Milestone 7 grounding rules are finalized, but not before.
- Whether a validated numeric reliability estimate should ever be exposed to judges alongside or instead of Source Support — intentionally deferred; decided at the end of Milestone 9 based on calibration evidence, not assumed in advance.
- When (if ever) to split the single .NET solution into multiple projects — deferred until a concrete engineering reason emerges (e.g., an offline ingestion tool, an isolated test project); not planned proactively per section 11's modular-monolith principle.
- Exact representation for branching evaluation scenarios (flat list vs. a small tree/graph per scenario) — deferred to Milestone 8; a simple hand-authored representation is sufficient initially, no specialized trajectory-evaluation framework needed.
- How to detect low-confidence or insufficient retrieval results (e.g., a similarity threshold or minimum passage count), and whether the system should ask a neutral scenario-disambiguation question before trusting retrieved materiality in that case — deferred to Milestone 6, where real retrieval first exists. This is expected to surface as a concrete problem during that milestone rather than being pre-designed now.

## Learning Objectives

**Milestone 1 — First LLM interaction**
Understand the basic LLM request/response cycle: authentication, request construction, prompts vs. completions, tokens, and context windows. Understand what a "raw" LLM call looks like before any application scaffolding is added.

Also internalize a cross-cutting software-engineering lesson that applies to every later milestone: separating responsibilities, abstracting behavior, splitting projects, and splitting deployment units are four different decisions — adding AI capabilities to the system does not automatically justify any of the latter three. PokéJudge begins, and should remain for a long time, a modular monolith (see section 11).

Before moving to Milestone 2, deliberately attempt to extract one specific piece of information from the raw response — for example, whether it indicates the scenario is clear enough to rule on — using naive string parsing. Observe how inconsistent and brittle this is across repeated calls. This friction is the actual motivation for Milestone 2's structured-output requirement, not just an assertion to take on faith.

**Milestone 2 — Judge-focused prompting, clarification, structured responses**
Understand system instructions vs. user input, prompt design for a specific domain persona, structured outputs (schema-constrained generation vs. free-text parsing), and how to represent multi-turn conversational state as application data rather than relying on the model to "remember." Also understand, concretely, why materiality must be text-derived: the mock corpus makes "is this fact material?" answerable only from supplied text, not memory — the same discipline Milestone 6 continues with real retrieved passages. The mock corpus's fixed, hand-authored coverage (it only knows what you wrote into it) is also what motivates Milestone 3: real judges will describe situations this corpus has no content for.

Wrestling with the boundary between confirmed fact and hypothesis — deciding when a judge's statement strictly entails another fact versus merely makes it plausible — is itself a core, transferable AI-agent skill: the same distinction between what an agent actually knows and what it is merely assuming shows up in any system that reasons over incomplete information.

**Milestone 3 — Document ingestion**
Understand the practical mechanics of preparing source knowledge for retrieval: text extraction, normalization, and metadata design for later citation — the "garbage in, garbage out" reality of RAG.

**Milestone 4 — Chunking and embeddings**
Understand what an embedding represents (semantic vector), why chunking strategy materially affects retrieval quality, and the tradeoffs between chunk size/overlap and retrieval precision.

**Milestone 5 — Vector search**
Understand semantic/vector search as distinct from keyword search, similarity metrics, and how to evaluate retrieval quality independent of generation quality.

**Milestone 6 — RAG**
Understand how retrieval and generation are combined end-to-end: prompt construction with retrieved context, context window budgeting, and the difference between "the model knows this" and "the model was told this." This is also where clarification becomes retrieval-grounded — understand how to make materiality decisions depend on retrieved passages rather than pretrained knowledge, and how to run retrieval iteratively (before the first clarifying question, and again after each new fact) instead of as a single terminal step.

**Milestone 7 — Citations and grounding**
Understand grounding and hallucination mitigation techniques: forcing citation of sources, detecting/handling unsupported claims, and designing prompts that make "I don't know" a viable, expected output.

Also recognize a specific limitation of this architecture: if the same underlying model both generates a ruling and judges whether it is grounded, that is not truly independent validation — correlated blind spots can survive the check. As part of this milestone, write a short analysis identifying which grounding checks can be deterministic (e.g., citation-ID existence, fact-sufficiency status, source freshness) versus which genuinely require model judgment (e.g., whether a specific claim is semantically supported by a cited passage), and why.

**Milestone 8 — Evaluation**
Understand AI evaluation as a discipline distinct from manual testing: building eval datasets, defining measurable success criteria for both retrieval and generation, and tracking quality over time as prompts/data change. Also understand **trajectory (process) evaluation**: for a multi-turn, branching decision-support system, final-answer correctness alone isn't sufficient — intermediate decisions matter too (recognizing an incomplete scenario, identifying the material fact, asking the right clarifying question, avoiding unnecessary ones, updating structured state correctly, choosing the right next branch, retrieving the right policy for that branch), so a lucky correct ruling reached via a wrong investigation path is scored differently from one reached via a correct investigation. A simple hand-authored set of branching scenarios is enough — this doesn't require specialized trajectory-evaluation infrastructure.

**Milestone 9 — Confidence Calibration and Reliability**
Goal: investigate whether PokéJudge can produce a meaningful, empirically validated estimate of how likely a recommendation is to be correct, building on the Milestone 8 evaluation dataset. This is an advanced, exploratory topic, not a requirement for a working product — the qualitative Source Support model (section 8) remains the default judge-facing signal unless this milestone demonstrates a numerical estimate is trustworthy enough to justify showing it.

A required part of this milestone's deliverable is an explicit limitations analysis: state how many labeled outcomes fall into each probability bucket, and assess candidly whether that sample size is large enough to support the chosen statistic (reliability diagram, ECE, Brier score) or whether the dataset can only support a simpler, more qualitative comparison (e.g., raw correlation between stated confidence and correctness, or 2–3 coarse buckets). This limitations analysis is itself a deliverable, not an afterthought — recognizing when a dataset is too small for a statistic to be meaningful is a core, transferable evaluation skill.

*Learning goals:* the difference between model confidence and actual correctness; what calibration means and why it's a different property from accuracy; overconfidence and underconfidence in LLM systems; how evaluation data is used to validate uncertainty estimates; reliability diagrams / calibration curves; the difference between qualitative support labels and calibrated probabilities; calibration metrics such as Expected Calibration Error and/or Brier Score, scoped appropriately; why a confidence percentage should not be exposed to judges until it has demonstrated meaningful calibration.

*Experiments:* have the model emit a predicted probability that its ruling is correct; compare those predictions against the curated Milestone 8 eval dataset's actual outcomes; bucket predictions into ranges (e.g., 50–60%, 60–70%, ... 90–100%) and check whether observed correctness in each bucket matches the stated range; compare model self-reported confidence against other reliability signals — retrieval quality, citation coverage, Source Support classification, presence of explicit vs. inferred policy, source conflict, and evaluation performance on similar past scenarios; investigate whether combining these signals produces a better reliability estimate than self-reported confidence alone.

*Product decision (end of milestone):* if the resulting reliability score demonstrates adequate calibration, consider exposing it to judges; if it does not, retain the qualitative Source Support model as the sole judge-facing signal and keep numeric confidence work internal to evaluation. Do not assume in advance that the product must eventually display a percentage.

**Milestone 10 — Judge-focused application UI**
Understand how AI application state (facts, questions, rulings, citations, Source Support) is exposed through an API to a frontend, and UX patterns for surfacing evidentiary strength — as a qualitative label, not a bare number — to a real user.

**Milestone 11 — Deployment and portfolio polish**
Understand what it takes to operate an AI application beyond a local demo: secrets/config in deployment, basic observability for LLM calls (latency, token usage, failure modes), and how to communicate AI-specific architecture and tradeoffs to a technical audience.

### What the product needs
Accurate, well-cited rulings; correct clarification behavior; traceable sources; an honest, criteria-based Source Support signal; graceful handling of insufficient information; a usable interface for judges under time pressure.

### What I am building specifically to learn
The full RAG pipeline built from first principles (ingestion → chunking → embeddings → vector search → generation) rather than an off-the-shelf framework; a hand-rolled evaluation harness, including branching/trajectory scenarios that score the investigation path, not just the final answer; explicit structured-state management for multi-turn interactions; a provider-abstraction layer; a calibration study distinguishing model confidence from validated correctness; disciplined use of a modular monolith rather than defaulting to services/microservices because the project involves AI.

### What we are intentionally postponing
Vector database selection beyond what Milestone 5 requires; the React/TypeScript frontend (until Milestone 10); numeric confidence/reliability scores shown to judges (until Milestone 9 demonstrates real calibration — Source Support is the interim and possibly permanent judge-facing signal); multi-user/auth concerns (until Milestone 11); formal deployment/observability infrastructure (until Milestone 11); tool/function calling (until a concrete need arises); support for non-English sources or responses.
