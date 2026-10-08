# PokéJudge

[![CI](https://github.com/jkhaynes/PokeJudge/actions/workflows/ci.yml/badge.svg)](https://github.com/jkhaynes/PokeJudge/actions/workflows/ci.yml)

A decision-support tool for Pokémon Trading Card Game tournament judges, built in C# / .NET 10. A judge describes a problem at the table in plain English. PokéJudge works out which facts it still needs, asks for them, and then gives a cited recommendation based on the official rules and penalty documents. Each recommendation carries a **Source Support** label (Strong / Partial / Insufficient) that says how well the cited documents back it up.

It's also a hands-on AI engineering project. I built the retrieval-augmented generation (RAG) pipeline, the grounding checks and the evaluation harness myself, without an orchestration framework, so each step is visible and can be tested.

**In short:** C#, .NET 10, LLM integration (Google Gemini), RAG built from scratch (PDF ingestion, chunking, embeddings, vector search), structured model output, multi-turn state, grounding validation, and trajectory-based AI evaluation. Unit tests run in GitHub Actions CI.

---

## Why this problem is interesting

A chatbot that answers rules questions is easy to build. A tool a judge could actually rely on is harder, because a confident wrong ruling does more damage than no ruling. PokéJudge is designed around three rules:

1. **Investigate before advising.** Scenarios usually arrive incomplete. "Player A forgot to take a Prize" can't be ruled on until you know whether a Pokémon was Knocked Out. PokéJudge decides a fact matters only when a retrieved rule depends on it, not from what the model already knows about Pokémon. Every clarifying question is tied to a specific passage.
2. **The application owns the facts, not the model.** Facts are stored as data in three groups: **confirmed** (the judge said it, or it follows strictly from what they said), **unknown**, and **hypothesis** (plausible, but not confirmed). A hypothesis can never support a ruling. If it would change the outcome, PokéJudge asks about it.
3. **Label the evidence, not the model's confidence.** The model's own "how sure are you" is unvalidated, so it's never shown to the judge as the answer. Source Support is set by checks the code can run: were passages retrieved, do the cited IDs exist, were all the needed facts confirmed, and does each cited passage actually support its claim. The model's opinion and the validated label are printed next to each other so you can see where they disagree.

## How it works

```text
judge's scenario
      │
      ▼
 ┌──────────┐    ┌───────────────────┐  not enough facts  ┌────────────────────┐
 │ retrieve │───▶│ assess sufficiency│───────────────────▶│ ask questions tied │
 └──────────┘    │ (structured call) │                    │ to retrieved text  │
      ▲          └─────────┬─────────┘                    └─────────┬──────────┘
      │                    │ enough facts                           │ judge answers:
      │                    ▼                                        │ update confirmed /
      │          ┌───────────────────┐                              │ unknown / hypothesis
      │          │  generate ruling  │                              │
      │          └─────────┬─────────┘                              │
      │                    ▼                                        │
      │          ┌───────────────────┐                              │
      │          │ validate grounding│─▶ Strong / Partial /         │
      │          │ + assign Source   │   Insufficient               │
      │          │ Support           │                              │
      │          └───────────────────┘                              │
      └─────────────────────────────────────────────────────────────┘
                  retrieval runs again after every answer
```

Retrieval runs **before** the first question and again after every answer, because a new fact can make a different rule relevant.

The offline pipeline turns the official PDFs into something searchable: **PDF text extraction → cleanup → splitting by section (keeping section numbers for citations) → sentence-aware chunking → Gemini embeddings → in-memory vector store** that compares embeddings by cosine similarity.

### Code map

It's one .NET project with folders as module boundaries. See [Design decisions](#design-decisions) for why it isn't split into services.

| Folder | What's in it |
|---|---|
| [`AI/`](PokeJudge/AI) | `ILlmClient` / `IEmbeddingClient` interfaces, the Gemini client, a structured-response parser, and a client wrapper that paces calls to stay under rate limits |
| [`Ingestion/`](PokeJudge/Ingestion) | PDF extraction (PdfPig), text cleanup, table-of-contents and heading parsers, and document metadata (title, revision date) for citations |
| [`Chunking/`](PokeJudge/Chunking) | Sentence-boundary chunker and the embedding step |
| [`Retrieval/`](PokeJudge/Retrieval) | Brute-force cosine-similarity vector store, query builder, and a retrieval-only evaluation that makes no chat-model calls |
| [`StructuredState/`](PokeJudge/StructuredState) | `GameState` (confirmed / unknown / hypothesis) and the typed results of each model call |
| [`Clarification/`](PokeJudge/Clarification) | The retrieve → assess → clarify loop, prompt building, and ruling generation |
| [`Grounding/`](PokeJudge/Grounding) | Code-only grounding checks, the model check of whether each citation supports its claim, and the rules that turn both into a Source Support label |
| [`Evaluation/`](PokeJudge/Evaluation) | Scenario dataset, runner, scorer and simulated judge |

## Evaluation

The evaluation harness is the most substantial part of the project.

- **It scores how PokéJudge got to the answer, not just the answer.** Depending on what a scenario expects, it checks up to five things: did the first retrieval find the right section, did PokéJudge correctly decide whether facts were missing, was each question tied to a relevant rule, did retrieval after the answer find the right section, and was the final Source Support label acceptable. A correct ruling reached the wrong way doesn't get full credit.
- **Not every scenario should end in a ruling.** Some cover situations the rulebooks don't address, and pass only if PokéJudge stops without giving a ruling.
- **A simulated judge answers the questions.** At first the harness answered PokéJudge's questions from a fixed script, so it failed whenever PokéJudge asked a sensible question the script didn't expect. It now uses a separate model call that answers only from the scenario and a fact sheet written for it, or says "not known" ([design](docs/superpowers/specs/2026-09-26-simulated-judge-design.md)).
- **Each check passes or fails on its own**, so a report shows which stage went wrong, not just a single pass or fail. Rate-limit errors, timeouts and failures of the simulated judge itself are reported separately and never count against PokéJudge. `--repeat N` reruns a scenario to show how much results vary between identical runs, which they do; that variation includes the simulated judge as well as PokéJudge.
- **The scorer is plain code.** The model's output varies from run to run, but the scoring logic doesn't, and it's unit tested.

Each run's results, including every ruling PokéJudge gave, are written up in [`docs/superpowers/`](docs/superpowers).

This is a small, hand-written dataset. It catches regressions and shows where the pipeline is weak. It doesn't give a general accuracy rate, and I don't claim one.

## Design decisions

- **One project, organized by folder.** The design diagram lists responsibilities, not deployment units. Separating responsibilities, adding abstractions, splitting projects and splitting deployments are four different decisions, and adding AI doesn't justify any of the last three. The only separate project is the tests.
- **No vector database.** The corpus is small, so a brute-force search in memory is fast and has nothing to operate. A dedicated store can come later if the corpus grows enough to need one.
- **No orchestration framework.** Gemini is called over HTTP behind a small interface, so every prompt and parse is visible and a stub can stand in for it in tests.
- **Structured output everywhere the code makes decisions.** In Milestone 1 I tried reading a yes/no answer out of free text with string matching, and it proved unreliable across repeated runs. That's why every model result the code acts on uses a schema-constrained response.
- **The model version is pinned.** A `-latest` alias can switch models without warning, which would make eval runs impossible to compare.
- **A model checking its own work isn't independent.** Part of the grounding check is a model call, and its blind spots can overlap with the model that generated the ruling. [This analysis](.project-plans/milestone-7/grounding-analysis.md) separates the checks code can do (citation IDs exist, facts are confirmed, retrieval returned something) from the ones that need a model's judgment.

## Running it

**Requirements:** .NET 10 SDK. For anything that calls a model, you also need a [Gemini API key](https://aistudio.google.com/apikey); the free tier works.

```powershell
dotnet build PokeJudge.slnx
dotnet test PokeJudge.slnx          # unit tests use stubs, no API key needed
```

**Set up the corpus.** The official Play! Pokémon PDFs are copyrighted, so they aren't in the repo. Download them from the Play! Pokémon rules and resources page on pokemon.com into `docs/`, then ingest and embed each one:

```powershell
dotnet user-secrets set "Gemini:ApiKey" "<your-key>" --project PokeJudge

dotnet run --project PokeJudge -- ingest docs/play-pokemon-penalty-guidelines-en.pdf PPG
dotnet run --project PokeJudge -- chunk PPG
# repeat for PPTRH (Tournament Rules Handbook), TCGTH (TCG Tournament Handbook), TCGRULES (rulebook)
```

**Use it:**

| Command | What it does |
|---|---|
| `dotnet run --project PokeJudge [-- --rerank none]` | Interactive mode: describe a scenario, answer the questions, get a cited ruling |
| `... -- search [--top <n>] [--rerank jev|none] <text>` | Show the top chunks retrieved for a query (default 5) |
| `... -- eval [--rerank jev|none]` | Retrieval-only evaluation (embedding calls only, no chat model) |
| `... -- retrieval-depth` | For each eval scenario, where its expected sections rank in the top 30 (embedding calls only) |
| `... -- evaluate [--only <id>] [--from <id>] [--repeat <n>] [--rerank jev|none]` | Full scenario evaluation |

Optional settings (user secrets): `Gemini:Model` and `Gemini:RequestsPerMinute`. A full `evaluate` run uses a large share of the free tier's daily request limit, so use `--only` or `--from` to run part of it.

Retrieval is reranked with Jev by default: it fetches the top 100 by cosine similarity and keeps the 5 that TypeSafe's Jev model rates most likely to govern the situation. Pass `--rerank none` to any command for plain cosine top 5. Reranking needs `Jev:ApiKey` (`dotnet user-secrets set "Jev:ApiKey" "<your-key>" --project PokeJudge`); `Jev:Model` (default `jev-latest`), `Jev:CandidateCount` (default 100) and `Jev:MaxPerSection` (default 4, the most excerpts of one section among the 5 kept) are optional. A temporary Jev failure (5xx, 429 or a network error) is retried up to 3 times, after waits of 1, 2 and 4 seconds, with one line on stderr per retry.

## Status and roadmap

Done: Milestones 1 to 8.5, covering a first model call, structured output and the clarification loop, ingestion, embeddings, vector search, RAG, grounding and Source Support, the evaluation harness, and hardening of the eval dataset. Currently working through improvements based on what the evaluation found.

Still to come:
- **Confidence calibration:** check whether a number the model reports about its own confidence matches how often it's actually right. A number will only be shown to judges if the evidence supports it.
- **React/TypeScript web UI** for judges.
- **Deployment.**

The full plan is in the [PRD](docs/PRD.md).

## How it's built

I use Claude Code with a structured, human-approved workflow: design spec → implementation plan → test-first implementation → code review → a learning checkpoint where I'm quizzed on the concepts → PR. I approve each design and have to be able to explain each AI concept before moving on. Specs, plans and checkpoint transcripts are committed: current work is in [`docs/superpowers/`](docs/superpowers), and Milestones 1 to 8.5 are archived in [`.project-plans/`](.project-plans). The workflow is described in [docs/prompt-engineering.md](docs/prompt-engineering.md).

## Disclaimer

PokéJudge is a personal learning project. It isn't affiliated with or endorsed by The Pokémon Company, Nintendo or Play! Pokémon. Its output is a recommendation for a judge to weigh using their own authority, not a binding ruling.
