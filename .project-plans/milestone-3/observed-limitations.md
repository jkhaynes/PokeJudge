# Milestone 3 — Observed Limitations & Failures (Real-Document Run)

Captured per the plan's step 8: concrete evidence from running the ingestion pipeline against a real
official document, in the same evidence-based style as Milestones 1-2's observation docs.

- **Date:** 2026-08-15
- **Branch:** `milestone/3-document-ingestion`
- **Document:** `play-pokemon-tournament-rules-handbook-en.pdf` — Play! Pokémon Tournament Rules
  Handbook, "LAST REVISION: May 21, 2026" (49 pages). Kept local/gitignored per the plan's copyright
  hygiene section; not committed.
- **Command:** `dotnet run --project PokeJudge -- ingest docs/play-pokemon-tournament-rules-handbook-en.pdf PPTRH` (run from the repository root)
- **Result:** Completed successfully, no exceptions. 37 sections extracted and written to
  `PokeJudge/Ingestion/Output/PPTRH.json` (gitignored).

## The most important finding: citation granularity is bounded by the document's own Table of Contents

This is a sharper, more specific version of the plan's predicted "pattern-based, not semantic" section-detection
limitation, discovered by actually running the pipeline against a real document rather than assumed in advance.

The Table of Contents (pages 2-3) only lists section numbers two levels deep (e.g. `2.3`, `3.3`,
`4.3`), even though the document's actual body text goes three and four levels deep (`2.3.1`, `3.3.4.2`,
`4.3.4.1`, etc.). Because `SectionSplitter` only splits at headings the TOC actually lists, every
deeper subsection ends up embedded as plain text *inside* its parent's `IngestedSection.Text`, not as
its own citable section. Concretely, in the real output:

- `PPTRH-2.3` ("Publishing Tournament Information") contains `2.3.1 Publishing Deck/Team Lists` in full,
  embedded mid-text.
- `PPTRH-3.3` ("Roles & Responsibilities") contains **eleven** deeper subsections embedded in one
  `Text` field: `3.3.1`, `3.3.1.1`, `3.3.2`, `3.3.2.1`, `3.3.3`, `3.3.4`, `3.3.4.1`, `3.3.4.2`, `3.3.5`,
  `3.3.5.1`, `3.3.6`, `3.3.6.1`, `3.3.6.2` — roughly 5,500 characters covering Competitors, Spectators,
  Organizers, Judges, Head Judge responsibilities, Scorekeepers, and eligibility, all under one
  `PPTRH-3.3` citation.
- `PPTRH-4.3` ("Tournament Integrity") similarly embeds `4.3.1` through `4.3.4.3`.

This means a citation to `PPTRH-3.3` is real and traceable, but coarser than the document's actual
structure allows — a judge-facing citation pointing a reader at "Roles & Responsibilities" when the
material fact is really Head Judge appeals (`4.3.3`) is less specific than PRD §10's "specific enough
to locate in the physical/PDF rulebook" bar ideally wants. Fixing this would mean detecting headings
directly in body text (not just from the TOC) — which reintroduces the false-positive problem this
milestone's design deliberately avoided (see below) — or building a smarter heading detector than
regex pattern-matching. Both are real engineering work, appropriately left for a later pass rather
than solved reactively mid-milestone.

## The predicted limitation, confirmed: "Appendix A" isn't ingested

Exactly as the plan's "Expected Limitations" section anticipated ("a table, an appendix without
numbered headings"), the TOC's `Appendix A: Rating Zones ... 47` entry has no leading section number
and is correctly skipped by `TableOfContentsParser` (locked in by
`TableOfContentsParserTests.Parse_NonNumberedEntry_IsSkippedNotThrown`, written against this exact
real TOC line before the real document was even ingested). Appendix A's content is simply absent from
`PPTRH.json` — not silently mangled, just not present.

## Why the TOC-driven design was chosen over generic heading-pattern-matching

Early inspection (before writing any implementation) tried detecting section headings by scanning body
text directly for lines matching `^\d+(\.\d+)*\s+[A-Z]...`. This produced real false positives:
ordinary numbered instructions like *"1. Visit Pokemon.com."* and *"2. Click on the 'Log In' button..."*
(step-by-step account-creation instructions in section 2.2) matched the same pattern as a real section
heading. Cross-referencing candidate headings against the document's own parsed Table of Contents
instead of pattern-matching body text blind eliminated this specific failure mode entirely — every
section boundary in the real run matched correctly, with zero false-positive splits.

## Extraction library choice mattered more than post-hoc normalization

`PageTextNormalizer`'s whitespace-collapse and hyphenation-rejoin steps had a real but *small* visible
effect on this document's raw-vs-normalized comparison (see console output: page 15 before/after are
nearly identical apart from the stripped page-number footer). The bigger "garbage in, garbage out"
lesson here turned out to be upstream of normalization entirely: PdfPig's default `page.Text` blob
extraction does **not** reliably preserve line breaks (a heading and its following paragraph can run
together on one line), which would have broken TOC-driven section splitting outright.
`ContentOrderTextExtractor` (used instead) reconstructs real reading-order line breaks, which is what
makes heading detection possible at all. This was discovered by direct comparison before implementation
began, not assumed — the choice of *extraction* method turned out to matter more than the normalization
heuristics built on top of it.

## Metadata extraction worked better than planned

The plan's expected-limitations section anticipated manually-supplied version/date metadata (no
automatic detection). In practice, this document's title page carries a machine-parseable
`LAST REVISION: May 21, 2026` string, so `DocumentMetadataParser.ExtractEffectiveDate` reads it
directly rather than relying on a hardcoded constant — confirmed working against the real document
(`Metadata.Version == "May 21, 2026"` in the actual output). This is a real, working capability, not
just a unit-test fixture matching a hoped-for pattern. It's still document-specific (a source without
this exact "LAST REVISION:" phrasing would need a different pattern or a manual fallback), and true
multi-version reconciliation across revisions remains out of scope, as planned.

## Cross-document confirmation: citation granularity really is document-specific, not a pipeline ceiling

After the Tournament Rules Handbook run above, the same pipeline (same code, no changes) was run
against a second real document: `play-pokemon-tcg-tournament-handbook-en.pdf` (the "Pokemon TCG
Tournament Handbook" — 27 pages, "LAST REVISION: May 21, 2026", the game's own gameplay-management,
tournament-play, and rules-violations content, and the document most directly relevant to PokéJudge's
actual judge-ruling scenarios). It completed with zero exceptions and produced **64 sections**,
including genuine three-level citations the first document never produced: `TCGTH-2.3.1`, `TCGTH-4.1.4`,
`TCGTH-6.3.4`, `TCGTH-7.4.6`, etc.

The difference is entirely explained by the source: this document's own Table of Contents lists three
levels of section numbering, where the Tournament Rules Handbook's TOC only listed two. Nothing in
`TableOfContentsParser` or `SectionSplitter` changed between runs. This confirms the earlier finding
more precisely: citation granularity is bounded by *whichever specific document's* TOC is being
ingested, not by any fixed depth the pipeline itself imposes — a real, cross-document data point rather
than a conclusion drawn from a single run.

Command used: `dotnet run --project PokeJudge -- ingest docs/play-pokemon-tcg-tournament-handbook-en.pdf TCGTH`
(run from the repository root).
`Program.cs`'s `ingest` mode was generalized from one hardcoded document to a small lookup of known
document codes (`PPTRH`, `TCGTH`) to support this — both documents happen to share the same page-layout
shape (title p.1, TOC pp.2-3, body from p.4), so no per-document extraction logic changed, only the
title/page-range constants supplied per run.

## Two real bugs, found by review, fixed and re-verified against the real documents

A milestone review (`.project-plans/milestone-3/review.md`) found two genuine correctness bugs after
the runs described above — both fixed, with both real documents re-ingested afterward to confirm.

1. **`CollapseWhitespace`'s blank-line collapsing never actually ran on real output.** Its regex
   (`\n{3,}`) required 3+ *consecutive* `\n` characters, but `PdfTextExtractor` produces `\r\n` line
   endings — confirmed directly in this document's own `PPTRH.json` output, which was full of literal
   `\r\n` before the fix. In a run of `\r\n` pairs, every `\n` is separated from the next by a `\r`, so
   the pattern could never match. The existing unit test passed anyway because its fixture used bare
   `\n`, not real `\r\n` — a concrete lesson in why a test fixture needs to reflect real input shape,
   not an idealized stand-in. Fixed by normalizing all line endings to `\n` before collapsing; the real
   output now uses consistent `\n` throughout (confirmed: `PPTRH.json` no longer contains any `\r\n`).
2. **The ingested-output path depended on how the tool was invoked.** `Program.cs` wrote to a path
   relative to the process's working directory. Running `dotnet run --project PokeJudge -- ingest ...`
   from the repository root — the exact form this document originally (incorrectly) described — wrote
   output to `<repo-root>/Ingestion/Output/`, which `.gitignore` does not cover, rather than
   `PokeJudge/Ingestion/Output/`, which it does. This was reproduced live during review (`git status`
   showed an untracked `Ingestion/` directory at the repo root) before being fixed by anchoring the
   output path to `Program.cs`'s own compile-time source location instead of the process's working
   directory. Re-verified with the exact invocation that triggered it: output now correctly lands in
   `PokeJudge/Ingestion/Output/` regardless of where `dotnet run` is invoked from.

## A latent risk, not yet triggered: exact-substring heading search

`SectionSplitter` locates each heading via `bodyText.IndexOf(headingLine)`, taking the *first* match.
If a document's body ever contained a verbatim cross-reference phrase identical to a heading's exact
text (e.g., a sentence quoting `"2.3 Publishing Tournament Information"` word-for-word before the real
heading occurs), the splitter would incorrectly split at the earlier, wrong occurrence. This did not
happen in the real run — a nearby cross-reference in section 4.5.2 reads *"Publishing Tournament
Information section (2.3)"*, reordered enough that it doesn't match the exact heading string — but
it's a real design constraint worth knowing about, not a hypothetical worry invented after the fact.
