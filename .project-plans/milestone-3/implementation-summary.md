# Milestone 3 — Implementation Summary

**Milestone:** Milestone 3 — Document Ingestion
**Branch:** `milestone/3-document-ingestion`

## What Changed

Added a fully deterministic (no LLM calls) document-ingestion pipeline alongside Milestone 2's
clarification loop, still one console project:

- **`PokeJudge/Ingestion/`**:
  - `IngestedDocument.cs` — `SourceDocumentMetadata`, `IngestedSection`, `IngestedDocument` records,
    matching the plan's approved shape.
  - `PdfTextExtractor.cs` — the real PDF/file I/O boundary (PdfPig + `ContentOrderTextExtractor`, not
    unit tested, same status as `GeminiLlmClient`'s network boundary in Milestone 2).
  - `PageTextNormalizer.cs` — three pure functions: strip a page-number footer, collapse whitespace,
    rejoin hyphenated line-wraps.
  - `TableOfContentsParser.cs` — parses a real TOC's dot-leader format (`"2.3.1 Heading .... 9"`) into
    `TocEntry` records; deliberately only recognizes numbered entries.
  - `DocumentMetadataParser.cs` — extracts the document's own stated revision date from its title page
    (`"LAST REVISION: <date>"`), throwing rather than guessing if absent.
  - `SectionSplitter.cs` — splits normalized body text into `IngestedSection`s using TOC entries as
    ground truth for section boundaries, throwing if a TOC entry's heading can't be located.
  - `IngestionPipeline.cs` — orchestrates the above, decoupled from `PdfTextExtractor` so it's testable
    against fixture page-text arrays instead of a real PDF (mirrors Milestone 2's `ClarificationLoop`
    pattern of injecting the untestable boundary).
- **`PokeJudge/Program.cs`** — added an `ingest <path> <document-code>` console mode (checked before
  the Milestone 2 Gemini setup, since ingestion needs no API key at all); prints a raw-vs-normalized
  comparison and a few sectioned/cited excerpts, then writes the full result to a local, gitignored
  JSON file. The document-code argument selects from a small lookup of known real documents (see
  "Scope expanded during implementation" below).
- **`PokeJudge.csproj`** — added the `PdfPig` package reference.
- **`.gitignore`** — excludes `docs/*.pdf` (the real source document(s)) and
  `PokeJudge/Ingestion/Output/` (the pipeline's output), per the plan's copyright/redistribution
  hygiene section — neither is committed.

### Design decision made during implementation (within the approved plan's scope)

The plan's step 5 described detecting section headings via a numbered-pattern regex scanned directly
over body text. Before writing any implementation, a quick real-document inspection showed this
approach false-positives on ordinary numbered instructions (e.g. *"1. Visit Pokemon.com."*). Instead,
the implementation parses the document's own Table of Contents first and cross-references TOC entries
against body text to find section boundaries — a more robust, still fully deterministic approach that
fulfills the same plan step's goal. This is a refinement of *how* step 5 works, not a scope or
architecture change; see `.project-plans/milestone-3/observed-limitations.md` for the concrete
evidence that motivated it.

### Scope expanded during implementation, at explicit user direction

The approved plan scoped ingestion to one real document ("one real document is sufficient to build
and prove the pipeline"). After the Tournament Rules Handbook run below, the developer asked to also
include `play-pokemon-tcg-tournament-handbook-en.pdf` specifically, since it — not the Tournament
Rules Handbook — is the main source for actual judge rulings (gameplay management, tournament play,
rules violations & penalties), directly relevant to PokéJudge's actual purpose. This is a legitimate,
explicit scope decision made during implementation, not something decided unilaterally: `Program.cs`'s
`ingest` mode was generalized from one hardcoded document's constants to a small lookup keyed by a
document code (`PPTRH`, `TCGTH`), and the second document was ingested using the same, unchanged
pipeline. No new abstractions or architecture were introduced — both documents share the same
page-layout shape (title p.1, TOC pp.2-3, body from p.4), so this was genuinely the "mechanical
repetition" the plan anticipated, just pulled into this milestone instead of deferred to Milestone 4.

## Post-review fixes

A milestone review found two real bugs, both fixed and re-verified afterward (see
`.project-plans/milestone-3/review.md` and the "Two real bugs, found by review" section of
`observed-limitations.md` for full detail):

1. `PageTextNormalizer.CollapseWhitespace`'s blank-line regex didn't match real `\r\n`-terminated PDF
   output, silently no-op'ing that part of normalization. Fixed by normalizing line endings to `\n`
   first; locked in with a new `\r\n`-based test case
   (`CollapseWhitespace_ManyBlankLinesWithCarriageReturns_CollapseToOneParagraphBreak`).
2. The ingested-output path depended on the process's working directory, and one common invocation
   (`dotnet run --project PokeJudge` from the repo root) wrote output outside `.gitignore`'s coverage.
   Fixed by anchoring the output path to `Program.cs`'s own compile-time source location via
   `[CallerFilePath]`, independent of how the tool is invoked.

Both real documents were re-ingested after the fixes to confirm: 37 and 64 sections respectively,
unchanged section counts, and `PPTRH.json`/`TCGTH.json` now contain no raw `\r\n` sequences.

## Validation

- **Build:** `dotnet build` — succeeded, 0 warnings, 0 errors.
- **Tests:** `dotnet test` — 55/55 passed (31 carried over from Milestones 1-2, unchanged, 23 from the
  initial implementation, plus 1 added post-review for the CRLF regression).
  - **Written first (Red step), all deterministic:** `PageTextNormalizerTests` (footer stripping,
    whitespace collapse, hyphenation rejoin, including the "don't touch a legitimate mid-word hyphen"
    case), `TableOfContentsParserTests` (dot-leader parsing, deeply nested section numbers, and — per
    the plan's own anticipated limitation — the non-numbered "Appendix A" entry being skipped, not
    guessed at), `DocumentMetadataParserTests` (revision-date extraction and its throw-if-absent
    contract), `SectionSplitterTests` (correct text boundaries, last-section-runs-to-end, and a
    "heading not found" failure case naming the missing section), `IngestionPipelineTests`
    (orchestration wired correctly across single- and multi-page bodies, with fixture page-text arrays
    standing in for a real PDF), and `IngestedDocumentSerializationTests` (JSON round-trip, written
    carefully to avoid the C# record-equality-on-`List<T>` pitfall by comparing fields, not whole
    records, with reference-type list members).
  - **Final coverage-review pass:** reviewed the diff after implementation; no additional deterministic
    logic emerged beyond what was captured in the Red step. `PdfTextExtractor` and `Program.cs`'s
    `RunIngestion` console/file I/O remain intentionally untested, consistent with Milestones 1-2's
    established boundary-testing precedent.
- **Real-document validation, two documents:** ran the pipeline against both the 49-page Play!
  Pokémon Tournament Rules Handbook (37 sections, `PPTRH-1` through `PPTRH-8`) and the 27-page Pokémon
  TCG Tournament Handbook (64 sections, `TCGTH-1` through `TCGTH-9`, including genuine three-level
  citations like `TCGTH-7.4.6`). Both completed with zero exceptions and correctly auto-extracted
  their real revision dates (`May 21, 2026` for both). Full findings, including one significant
  discovered limitation sharper than what the plan anticipated and its cross-document confirmation,
  are in `.project-plans/milestone-3/observed-limitations.md`.

## Intentional Limitations

- **Citation granularity is bounded by the source document's own Table of Contents depth.** Confirmed
  directly on the Tournament Rules Handbook: sections like `PPTRH-3.3` embed up to 11 real subsections
  as plain text within one citable unit, because that document's TOC only lists two levels while the
  body goes three-to-four deep. Confirmed as genuinely document-specific, not a pipeline ceiling, by
  the second document (TCG Tournament Handbook): its TOC lists three levels, and the same unchanged
  pipeline produced correspondingly finer citations (`TCGTH-7.4.6`, etc.). Addressing the coarser case
  would require heading detection that doesn't depend on the TOC listing every level, which
  reintroduces the false-positive risk this milestone's design deliberately avoided — appropriately
  left as future work rather than patched reactively.
- **Non-numbered TOC entries (e.g., appendices) are not ingested.** Predicted by the plan and confirmed
  against the real document; `Appendix A: Rating Zones` is simply absent from the output.
- **No automatic multi-version reconciliation.** A single document's own stated revision date is now
  extracted automatically, but comparing/reconciling across multiple versions of a document stays
  deferred (PRD §18, revisited at Milestone 7).
- **Still no retrieval, chunking, or embeddings.** `IngestedDocument`/`IngestedSection` are structured
  data sitting in a local JSON file; nothing can search over them yet. The Milestone 1-2 mock corpus
  remains the clarification loop's only source of context until Milestone 6.
- **Exact-substring heading search is a real, if unexercised, risk.** `SectionSplitter` takes the first
  occurrence of a heading's exact text in body text; a verbatim cross-reference elsewhere in the
  document could in principle cause a misplaced split. Didn't happen in the real run, but it's a known
  design constraint, not a proven-safe guarantee.

## Learning Focus

- **Extraction vs. normalization as genuinely separate concerns**, demonstrated concretely: the biggest
  "garbage in, garbage out" risk in this document turned out to be an extraction-method choice
  (`ContentOrderTextExtractor` vs. PdfPig's default blob text losing line structure), not a
  normalization heuristic — a real example of why conflating the two steps would have hidden the more
  important problem.
- **Citation metadata designed ahead of need**: `SourceDocumentMetadata` and `IngestedSection.SectionId`
  exist specifically so Milestone 4+ never has to retrofit citation traceability onto text that wasn't
  tagged with it at ingestion time.
- **Pattern-based section detection's real failure shape**, seen twice: once as directly predicted
  (Appendix A), and once sharper than predicted (TOC-depth-bounded citation granularity) — a good
  concrete example of the "build → observe → understand → improve" loop actually surfacing something
  more specific than what was anticipated on paper.
- **Not every AI-system milestone touches a model.** This entire pipeline — extraction, normalization,
  TOC parsing, section splitting, metadata extraction — is deterministic text processing with zero LLM
  calls, and that's a deliberate, meaningful part of what a real RAG system's reliability rests on.

## What I Should Try

1. Open `PokeJudge/Ingestion/Output/PPTRH.json` (gitignored, stays local) and read `PPTRH-3.3`'s
   `Text` field in full — see firsthand how much real structure (Competitors, Spectators, Organizers,
   Judges, Head Judge, Scorekeepers, eligibility) is compressed into one citation, and think about
   what a judge-facing citation to that section would actually point them at.
2. Run `dotnet run --project PokeJudge -- ingest <path> <code>` yourself against the third supplied
   document (the League Challenges/Cups/Prerelease Guide) with a new document code — `RunIngestion`'s
   `knownDocuments` lookup only has `PPTRH` and `TCGTH` entries so far, so this will fail with the
   "Unknown document code" message until you inspect that PDF's actual page layout and add an entry
   for it; a good exercise in exactly the "layout isn't auto-detected" limitation this milestone
   documents.
3. Try deleting the `LAST REVISION:` line from a copy of the title-page text and feed it through
   `DocumentMetadataParser.ExtractEffectiveDate` directly (or just re-read
   `DocumentMetadataParserTests.ExtractEffectiveDate_NoRevisionLine_ThrowsRatherThanGuessing`) to see
   the "fail visibly rather than guess" contract in action.
4. Compare the raw-vs-normalized console output for page 15 (shown when you run `ingest`) against a
   page elsewhere in the document that has a bulleted list or a table (like `PPTRH-1.2`'s audience
   chart, visible in the JSON) — bulleted/tabular content is exactly the kind of layout normalization
   heuristics built for prose don't handle well; see if you can spot where it shows.

## Git Status

- **Branch:** `milestone/3-document-ingestion`
- **Uncommitted:** yes — all implementation changes are in the working tree, nothing staged or
  committed yet (this skill does not commit automatically).
- **Unexpected files:** none. `git status` shows exactly the expected changes: new `PokeJudge/Ingestion/`
  and `PokeJudge.Tests/Ingestion/` folders, modified `PokeJudge/Program.cs`, `PokeJudge.csproj`, and
  `.gitignore`, plus `.project-plans/milestone-3/` (plan, this summary, observed-limitations, review).
  Re-confirmed clean after the output-path fix, including with the exact repo-root invocation that
  previously produced an untracked, ungitignored `Ingestion/` directory. The real
  source PDF and the pipeline's JSON output are correctly excluded by `.gitignore` and do not appear as
  untracked files.
