# Milestone 1 — First LLM Interaction

Status: planned, not started
Source: PRD.md §14 (Milestone Roadmap), §11 (modular monolith progression), "Learning Objectives → Milestone 1"

## 1. What We Will Build

The smallest possible working slice: a single C# console app (the existing `PokeJudge` project) that:

1. Reads a scenario description as a string (hardcoded or simple `Console.ReadLine()` input — no multi-turn, no menu).
2. Sends it as a prompt to an LLM provider via a **minimal internal abstraction** (an interface such as `ILlmClient` with one method, e.g. `Task<string> CompleteAsync(string prompt)`), with a single concrete implementation behind it.
3. Prints the raw, unstructured text response to the console.
4. As a deliberate exercise, attempts to programmatically extract one specific signal from that raw text (e.g., a yes/no "is this scenario clear enough to rule on?") using naive string handling (`Contains`, `StartsWith`, simple regex) — then observes and documents how unreliable that extraction is across repeated runs with varied phrasing.

No session store, no retrieval, no ingestion, no vector DB, no API project, no multiple provider implementations, no message bus. Per PRD §11, this milestone is explicitly:

```
Console App
    ↓
LLM
```

Secrets (API key) are loaded via `dotnet user-secrets`, never hardcoded or committed (PRD §16).

## 2. AI Concepts Being Learned

- The basic LLM request/response cycle: authentication, request construction, prompt vs. completion.
- Tokens and context windows as real constraints, not abstract terms.
- What a "raw" LLM call looks like with zero application scaffolding around it.
- Why naive string parsing of free-text output is brittle — the concrete friction that motivates Milestone 2's structured-output requirement (this must be *observed*, not just asserted).
- A cross-cutting software-engineering lesson (per PRD's Learning Objectives for Milestone 1): separating responsibilities, abstracting behavior, splitting projects, and splitting deployment units are four different decisions. Adding AI to the app does not by itself justify project/service separation — hence one project, one file is appropriate here.

## 3. Implementation Steps (in order)

1. **Secrets setup**: Initialize `dotnet user-secrets` for the `PokeJudge` project; store the LLM provider API key there. Confirm nothing secret is written to any tracked file.
2. **Provider interface**: Define a minimal `ILlmClient` (or similarly named) interface in `Program.cs` or a single small file — one method that takes a prompt string and returns a completion string. Keep it deliberately small; do not add configuration abstractions, retry policies, or streaming support not yet needed.
3. **Concrete implementation**: Implement the interface against one chosen provider's SDK (or Microsoft.Extensions.AI abstraction, per PRD §12) — pick one model/provider, read the key from configuration/user-secrets.
4. **Wire up console flow**: `Program.cs` takes a scenario string (hardcoded test scenario is fine to start, optionally `Console.ReadLine()`), calls the client, prints the raw response.
5. **Manual smoke test**: Run with 2–3 different hand-written judge scenario strings (e.g., a missed-prize scenario, an illegal-attack scenario) and confirm a plausible, varied completion is returned each time.
6. **Naive extraction exercise**: Pick one target signal (e.g., "does the model's answer indicate the scenario is sufficient to rule on, yes or no?"). Write a small naive parser (substring/regex) against the raw response. Run it across the same few scenarios repeated 3–5 times each (temperature default, don't force determinism) and log whether the naive parser's answer is consistent and correct.
7. **Document the failure**: Record concrete examples where the naive parser breaks (e.g., model says "Yes, this seems sufficient" vs. "I don't think we have enough information" vs. hedged phrasing that the parser misreads). This becomes the evidence trail for Milestone 2.

## 4. Expected Limitations / Failures to Intentionally Observe

- The model's phrasing will vary run-to-run even for semantically similar answers — naive string matching will sometimes misclassify sufficiency (false positive or false negative).
- No memory: each call is stateless: if the console app is re-run or a second scenario is sent, the model has no awareness of prior turns. This is expected and correct for this milestone — it's not a bug to fix here.
- No grounding: the model will answer from pretrained Pokémon knowledge, potentially fabricating rule specifics. This is expected and acceptable at Milestone 1 — grounding isn't introduced until Milestones 6–7. Don't try to fix hallucination here; just notice it.
- No structured guarantee: nothing stops the model from wrapping its answer in extra prose, disclaimers, or reordering the sufficiency signal, breaking naive parsing.

## 5. What I Should Understand by the End

- How to make an authenticated request to an LLM provider from C# and receive a text completion.
- What a prompt, a completion, a token, and a context window are, concretely (not just definitions).
- Why an interface (`ILlmClient`-style abstraction) around the provider call is worth having from day one, even with only one implementation — cheap now, expensive to retrofit later (ties to PRD §12's provider-swap goal).
- First-hand evidence (not just an assertion) that naive string parsing of free-text LLM output is unreliable enough to justify structured output — this is the actual motivating problem for Milestone 2.
- Why this milestone deliberately stays a single console app/single project, and why "we're adding AI" is not, by itself, a reason to introduce API layers, multiple projects, or services yet.

## Out of Scope for This Milestone

- Structured output / schema-constrained responses (Milestone 2).
- Multi-turn state, clarifying questions, sufficiency logic (Milestone 2).
- Any retrieval, ingestion, embeddings, vector store (Milestones 3–6).
- ASP.NET Core API project (deferred until needed, ~Milestone 2/10 per PRD §12).
