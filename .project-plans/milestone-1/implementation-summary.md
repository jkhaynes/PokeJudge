# Milestone 1 — First LLM Interaction — Implementation Summary

## Milestone Implemented

**Milestone 1 — First LLM Interaction**

## What Changed

- `Program.cs`: single-file console app with a minimal `ILlmClient` interface (`Task<string> CompleteAsync(string prompt)`) and one concrete implementation, `GeminiLlmClient`, calling the Gemini `generateContent` REST endpoint directly via `HttpClient`. (Originally implemented against OpenAI via Microsoft.Extensions.AI per the initial provider choice; switched to Gemini mid-session because the OpenAI account had no billing/free-tier quota — see "Provider swap" note below.)
  - Runs 3 hand-written judge scenarios through the raw call and prints the raw response for each.
  - Runs a deliberate naive-parsing exercise: a regex-based `NaiveSufficiencyParser` looks for `sufficient`/`insufficient`-style substrings in the model's free-text answer to a "is this scenario clear enough to rule on?" prompt, repeated 4x per scenario, logging both the parser's verdict and the raw response for comparison.
- `PokeJudge.csproj`: added `Microsoft.Extensions.Configuration.UserSecrets` package reference and a `UserSecretsId`. (Microsoft.Extensions.AI / Microsoft.Extensions.AI.OpenAI were added then removed after the provider swap — the final implementation uses only `HttpClient` + `System.Text.Json`, no LLM SDK dependency.)
- Local-only (not committed): `dotnet user-secrets` holds `Gemini:ApiKey` for the `PokeJudge` project.

### Provider swap: OpenAI → Gemini

The plan/PRD left the exact provider undecided by design. OpenAI was chosen first, but the OpenAI API returned `HTTP 429 insufficient_quota` — OpenAI's API has no persistent free tier and requires a funded account. Switched to Google Gemini, which has a genuinely free tier via Google AI Studio. This required no change to the `ILlmClient` abstraction itself — only a new implementation behind it, which is exactly the swap-cost the interface is meant to keep cheap (PRD §12).

While wiring up Gemini, two more real-world provider issues surfaced and were resolved:
- The model IDs `gemini-2.0-flash` and `gemini-2.5-flash` are both retired for new API keys (`404 ... no longer available to new users`), even though Google's own migration docs say the underlying `generateContent` endpoint "remains fully supported." Fixed by using the `gemini-flash-lite-latest` alias, which always resolves to a current, non-retired model.
- Transient `503 UNAVAILABLE` ("high demand") errors occurred on the free tier; no retry logic was added (correctly out of scope for Milestone 1) — the app fails visibly per PRD §9, and a manual re-run succeeded.

## Validation

- **Build**: succeeded, 0 warnings / 0 errors (`dotnet build PokeJudge.csproj`).
- **Manual smoke test**: ran end-to-end (`dotnet run --project PokeJudge.csproj`) against the live Gemini API. All 3 scenarios returned plausible, varied completions in Part 1. Part 2's naive-parsing exercise ran 4 repeats × 3 scenarios (12 total calls) and completed with no unhandled exceptions.
- No automated tests were added. Appropriate for this milestone: there is no deterministic application logic to unit test, and the naive parser's entire purpose is to demonstrate unreliability — asserting it "correct" would misrepresent what it's for.

## Intentional Limitations (left in place, as designed)

- **Naive parsing is unreliable — confirmed live.** Concrete example: a response that said *"The information given is **not sufficient** to make a ruling..."* was misclassified as `SUFFICIENT` by `NaiveSufficiencyParser`, because the regex only checks for the substring `sufficient` and does not account for negation (`not sufficient`). This happened multiple times across the 12 runs (see full transcript in git history / rerun `dotnet run` to reproduce). This is the exact friction Milestone 2's structured-output requirement is meant to solve — the parser was deliberately left naive.
- **No memory across calls.** Each `CompleteAsync` call is fully stateless; re-running the app or sending a second scenario carries no awareness of prior turns. Not addressed until Milestone 2's structured multi-turn state.
- **No grounding.** The model answered from pretrained knowledge only. Notably, one response (the "61 cards in a 60-card deck" scenario) spontaneously answered for both *Magic: The Gathering* and Pokémon TCG rules unprompted, and cited specific-sounding rule/penalty names that are not verified against any real source document. Expected and intentionally untouched until Milestones 6–7 (retrieval/grounding).
- **No structured-output guarantee.** Nothing stops the model from wrapping its answer in prose/disclaimers/reordering — which is exactly why the naive parser breaks.

## Learning Focus

- **The raw LLM request/response cycle with zero scaffolding** — authentication (API key), request construction (prompt → JSON body), and the completion returned as unstructured text, with no framework hiding any of those steps.
- **Real provider friction, observed rather than assumed** — a chosen model ID or endpoint can be deprecated out from under you; providers differ in what "free tier" even means; transient server errors happen even to well-formed requests. A thin, swappable `ILlmClient` abstraction is what let the entire provider swap (OpenAI → Gemini) happen without touching `Program.cs`'s calling code or the naive-parsing exercise at all — only `GeminiLlmClient` itself changed.
- **First-hand evidence, not just an assertion, that naive free-text parsing is brittle.** The "not sufficient" → `SUFFICIENT` misclassification is the concrete failure this milestone exists to produce, and is the direct motivation for Milestone 2's structured output.
- **Why "adding AI" didn't justify more architecture.** The whole app stayed one file, one project, one interface, one implementation — the provider swap under real-world pressure (deprecated models, quota limits) is a good test of whether the interface earns its keep, and it did.

## What I Should Try

- Re-run `dotnet run` and compare the naive-parser verdicts against this run's — see whether the same "not sufficient → SUFFICIENT" failure mode reproduces, or whether different phrasing causes different misclassifications.
- Open `NaiveSufficiencyParser.Parse` in `Program.cs` and, before running, predict which of the 4 runs per scenario it will get wrong — then check your prediction against the actual output.
- Look again at the "61 cards in a 60-card deck" scenario's response and specifically notice where it drifts into *Magic: The Gathering* rules unprompted — that's the "no grounding" limitation made concrete, worth remembering once Milestone 6/7 grounding is introduced.
- Add a 4th, deliberately vaguer scenario of your own choosing and see how both the raw response and the naive parser behave on it.
- Read through `GeminiLlmClient.CompleteAsync` and note everything it does *not* handle (retries, timeouts, rate limiting, streaming) — all deliberately absent at this stage.

## Git Status

- Branch: `milestone/1-first-llm-interaction`
- Uncommitted: yes — `PokeJudge.csproj` and `Program.cs` are modified but not staged or committed (skill does not commit automatically).
- No unexpected files present. `.project-plans/` (including this summary) and `.claude/` remain excluded from git via `.git/info/exclude` — not tracked, nothing to clean up.
