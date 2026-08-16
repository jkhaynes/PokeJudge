# Milestone 3 Review

**Milestone reviewed:** Milestone 3 — Document Ingestion
**Plan:** `.project-plans/milestone-3/plan.md`
**Branch:** `milestone/3-document-ingestion`
**Build:** `dotnet build` — succeeded, 0 warnings, 0 errors.
**Tests:** `dotnet test` — 54/54 passed at time of review; 55/55 after the fixes below (one new
regression test added).

**Update:** Both Must Fix items below have since been fixed and re-verified — see the note at the top
of each item. Details of the fixes are in `implementation-summary.md`'s "Post-review fixes" section and
`observed-limitations.md`'s "Two real bugs, found by review" section.

## ✅ Matches the Plan

- **All five "What We Will Build" items delivered**: PDF extraction (`PdfTextExtractor`), normalization
  (`PageTextNormalizer`), section/citation-metadata extraction (`TableOfContentsParser` +
  `DocumentMetadataParser` + `SectionSplitter`), a structured serializable output model
  (`IngestedDocument`/`IngestedSection`), and a console `ingest` entry point printing a raw-vs-normalized
  comparison.
- **Zero LLM calls anywhere in the ingestion path** — genuinely deterministic, matching the plan's
  explicit "first milestone with no LLM calls at all" framing.
- **Single project, no new architecture**: `PokeJudge/Ingestion/` sits alongside `AI/`,
  `StructuredState/`, `Clarification/` exactly as the PRD's own illustrative Milestone-3+ structure
  suggests. No new projects, no unnecessary dependencies beyond `PdfPig`.
- **The TOC-driven section-detection redesign is a well-justified refinement, not scope creep.** The
  plan's step 5 only specified the goal (numbered-heading detection); switching from blind
  body-text pattern matching to TOC cross-referencing was decided *before* writing implementation,
  based on a concrete, documented false-positive ("1. Visit Pokemon.com." matching as a heading) —
  exactly the kind of build-before-you-assume discipline the project values.
- **Real-document validation actually happened, on two documents, with genuine findings.** Both the
  Tournament Rules Handbook (37 sections) and the TCG Tournament Handbook (64 sections, including real
  3-level citations) were ingested end-to-end with zero exceptions, and the resulting citation-depth
  contrast between them is a legitimately valuable, evidence-based finding — not an assumption dressed
  up as a discovery.
- **Copyright hygiene was clearly *designed* correctly**: `.gitignore` entries for `docs/*.pdf` and
  `PokeJudge/Ingestion/Output/` exist, tests use small hand-crafted fixtures instead of the real PDFs,
  and `observed-limitations.md` quotes only short excerpts. (See Must Fix #2 below for where the
  *implementation* of this intent has a real gap.)
- **The mid-milestone scope expansion (ingesting a second document) was handled transparently.** The
  implementation summary explicitly documents that this went beyond the approved plan's stated minimum
  ("one real document is sufficient"), names it as an explicit user decision, and explains why it didn't
  require new architecture. This is exactly how a legitimate scope change during implementation should
  be recorded — nothing hidden.
- **Tests were genuinely written first and test real behavior**, not implementation shape:
  `SectionSplitterTests`'s "heading not found" case, `TableOfContentsParserTests`'s Appendix-A-is-skipped
  case (written against the real TOC's actual wording before the real document was ingested), and
  `IngestionPipelineTests`'s footer-stripping-is-actually-applied assertions are all meaningful
  regression guards.

## 🚨 Must Fix

### 1. `PageTextNormalizer.CollapseWhitespace`'s blank-line collapsing does not work on real PDF output — ✅ FIXED

*Fixed by normalizing `\r\n`/`\r` to `\n` before applying the blank-line regex. Locked in with a new
`\r\n`-based test case. Re-verified against both real documents: their JSON output no longer contains
any raw `\r\n` sequences.*

**File/location:** `PokeJudge/Ingestion/PageTextNormalizer.cs:13, 44-48`

**Problem:** `ExcessBlankLines` is `new Regex(@"\n{3,}")` — it requires 3+ *consecutive* `\n` characters
with nothing between them. `PdfTextExtractor` (via PdfPig's `ContentOrderTextExtractor`) produces
`\r\n` line endings — confirmed directly in the real ingestion output (`PPTRH.json`'s `Text` fields are
full of literal `\r\n`). In `\r\n\r\n\r\n`, every `\n` is immediately preceded and followed by `\r`, so
there is never a run of even two consecutive `\n` characters, let alone three. Verified empirically
during this review:

```powershell
$text = "Line one`r`n`r`n`r`n`r`nLine two"
[regex]"\n{3,}"::Match($text).Success   # False
```

**Why it matters:** This silently makes one of `CollapseWhitespace`'s two stated jobs
("collapse runs of 3+ blank lines down to one paragraph break") a complete no-op on every real document
this pipeline will ever process, since PdfPig's real output is always `\r\n`-terminated. The unit test
covering this (`CollapseWhitespace_ManyBlankLines_CollapseToOneParagraphBreak`) passes only because its
fixture string uses bare `\n` literals, not `\r\n` — it gave false confidence rather than catching the
gap. This is a real defect in a function whose entire purpose is normalizing real-world extraction
artifacts, not a cosmetic issue.

**Recommended direction:** Make the pattern line-ending-agnostic, e.g. `(?:\r?\n){3,}` (mirroring how
`HyphenatedLineWrap` already correctly handles `\r?\n`), or normalize all line endings to `\n` once at
the start of `CollapseWhitespace`. Add a test case using `\r\n` fixtures (matching what
`PdfTextExtractor` actually produces) so this class of gap can't reappear silently.

### 2. The ingested-output path depends on how `dotnet run` is invoked, and one common invocation puts copyrighted output outside `.gitignore`'s coverage — ✅ FIXED

*Fixed by anchoring the output path to `Program.cs`'s own compile-time source location via
`[CallerFilePath]`, independent of the process's working directory. Re-verified with the exact
invocation that originally reproduced the bug (`dotnet run --project PokeJudge -- ingest ...` from the
repo root): output now correctly lands in `PokeJudge/Ingestion/Output/`, and `git status` shows no
stray untracked directory at the repo root.*

**File/location:** `PokeJudge/Program.cs:192-195` (`Path.Combine("Ingestion", "Output")`)

**Problem:** The output directory is a path relative to the process's working directory, not to the
`PokeJudge` project directory. Verified empirically during this review:

- Running from inside `PokeJudge/` (`cd PokeJudge; dotnet run -- ingest ...`) — the documented
  behavior — writes to `PokeJudge/Ingestion/Output/`, correctly covered by the `.gitignore` entry
  `PokeJudge/Ingestion/Output/`.
- Running `dotnet run --project PokeJudge -- ingest ...` from the **repository root** — an entirely
  standard, common way to invoke `dotnet run` in a multi-project repo, and in fact the exact form used
  in this milestone's own `implementation-summary.md`/`observed-limitations.md` write-ups — writes to
  `<repo-root>/Ingestion/Output/` instead. That path is **not** covered by any `.gitignore` rule.
  `git status` after this invocation shows `?? Ingestion/` as a plain untracked directory.

**Why it matters:** This directly undermines the plan's own explicit, called-out-as-"important, not
optional" copyright/redistribution hygiene requirement. The ingested JSON reproduces substantial
excerpts of copyrighted document text. A developer who runs the tool the "standard" way (from the repo
root, which is also how this milestone's own documentation describes running it) will produce output
that sits untracked at the repo root, one `git add -A` away from being accidentally committed to a
public GitHub repository. This isn't hypothetical — it was reproduced and then cleaned up as part of
this review.

**Recommended direction:** Anchor the output path to something invocation-independent — e.g.
`Path.Combine(AppContext.BaseDirectory, "Ingestion", "Output")`, or resolve it relative to the
executing assembly's location rather than `Environment.CurrentDirectory`. As defense in depth, also
consider a broader `.gitignore` pattern (e.g. `**/Ingestion/Output/`) so a future invocation-path
mistake doesn't silently bypass the intended protection. Also worth fixing the reproduction commands in
`implementation-summary.md`/`observed-limitations.md` once the underlying behavior is fixed, since they
currently document the exact invocation style that triggers this gap.

## ⚠️ Consider Improving

- **`IngestedDocument.Sections` is `List<IngestedSection>`, not the plan's specified
  `IReadOnlyList<IngestedSection>`** (`PokeJudge/Ingestion/IngestedDocument.cs:14`). Consistent with
  Milestone 2's precedent of preferring `List<T>` for JSON-round-tripped structured-output types, and
  disclosed nowhere as an explicit deviation the way Milestone 2's `CompleteAsync` removal was. Not a
  functional problem, just worth a one-line note next time a plan pins an exact type.
- **`SectionSplitter`'s exact-substring, first-match heading search** is already self-documented as a
  known, unexercised risk in `observed-limitations.md` (a verbatim cross-reference elsewhere in a
  document could cause a misplaced split). No action needed now — just flagging that this is a real
  design constraint worth remembering if a future document trips it.
- **`RunIngestion`'s `knownDocuments` dictionary is rebuilt on every invocation** — trivial for a
  console app that ingests one document per run, not worth changing, but if this pattern grows (more
  documents, more metadata) a `static readonly` field or a small data file would scale better than a
  dictionary literal inline in a local function.

## 🧪 Learning Observations

- **The extraction-method choice mattering more than normalization heuristics is a genuinely instructive
  finding**, and it's real: switching from PdfPig's default `page.Text` to `ContentOrderTextExtractor`
  is what made section detection possible at all, while the normalization functions built on top had a
  comparatively small visible effect on the sample page shown. Worth understanding this as "garbage in,
  garbage out" operating one layer earlier than expected — at the extraction API choice, not just at
  post-hoc cleanup.
- **The Must-Fix #1 bug is itself a good, concrete lesson about test fixture fidelity**: a unit test
  that uses `\n` when real input is always `\r\n` can pass while the code it's testing does nothing
  useful on real input. Worth internalizing as a general principle — a fixture should reflect the actual
  shape of real data, not a simplified stand-in, especially for text-processing code.
- **The TOC-depth-bounds-citation-granularity finding, now confirmed across two documents**, is a
  strong, concrete example of "build → observe → understand" surfacing something sharper than what the
  plan predicted on paper (it anticipated pattern-mismatch failures like the Appendix-A case, not this
  more nuanced granularity ceiling). Understanding *why* `PPTRH-3.3` and `TCGTH-7.4` differ so much in
  what they cover is a good comprehension check before moving to Milestone 4.
- **Must-Fix #2 is itself a live example of the "real-world document handling as an actual engineering
  constraint" learning objective** the plan called out — not a hypothetical one. Worth treating the fix
  (and understanding *why* `Environment.CurrentDirectory`-relative paths are fragile in a multi-project
  solution) as part of finishing this milestone's learning, not just a bug to patch and forget.

## 🎯 Learning Objective Check

1. **What AI concept was this milestone intended to teach?** Extraction vs. normalization as distinct
   concerns; citation metadata designed ahead of need; "garbage in, garbage out" as a concrete,
   observable RAG-pipeline reality; and that not every AI-system milestone involves calling a model.
2. **Does the implementation expose that concept clearly?** Mostly yes — the pipeline's stages are
   cleanly separated and individually inspectable, and the real-document run produced genuinely
   instructive evidence. The blank-line-collapse bug (Must Fix #1) is a small dent in the "garbage in,
   garbage out" story specifically, since one of the normalization functions doesn't actually do its
   job on real input yet — worth fixing so the lesson is fully accurate, not just mostly accurate.
3. **What should the developer be able to explain after completing this milestone?** Why TOC-driven
   section detection beats blind pattern matching (with the concrete false-positive example); why
   citation granularity in `PPTRH-3.3` vs `TCGTH-7.4` differs, and that the difference is about the
   *source document*, not the pipeline; why `SourceDocumentMetadata` exists before anything needs to
   query it yet; and, after the Must-Fix items are addressed, why line-ending assumptions and working-
   directory assumptions are both classic, easy-to-miss sources of "works on my machine" bugs.
4. **Is any abstraction hiding something the developer should understand directly?** No. Every stage
   (`PdfTextExtractor`, `PageTextNormalizer`, `TableOfContentsParser`, `DocumentMetadataParser`,
   `SectionSplitter`, `IngestionPipeline`) is a small, separately-inspectable, mostly-pure function or
   class with no framework indirection. The console `ingest` mode prints intermediate raw/normalized
   text directly, keeping the pipeline's real behavior visible rather than buried.

## 📋 Plan Completion

| Step | Status |
|---|---|
| 1. Place the real source document locally | Complete (two documents, exceeding the plan's stated minimum, at explicit user direction) |
| 2. Add a PDF text-extraction library + raw-extraction step | Complete |
| 3. Design the output data model as C# records | Complete (minor deviation: `List<T>` instead of the plan's `IReadOnlyList<T>` for `Sections`) |
| 4. Build normalization functions | Complete (Must Fix #1 fixed — blank-line collapsing now works on real `\r\n` input, re-verified) |
| 5. Build section/citation-metadata extraction | Complete, with a well-justified design refinement (TOC-driven rather than blind pattern matching) |
| 6. Serialize the result to a local JSON file | Complete |
| 7. Wire the console `ingest` mode | Complete (Must Fix #2 fixed — output path is now invocation-independent, re-verified with the exact command that originally triggered the bug) |
| 8. Document observed extraction/normalization issues | Complete, and substantially exceeded — concrete evidence, a cross-document confirmation, and transparent documentation of the mid-milestone scope expansion |

## Final Verdict

**Ready to Complete**

Both Must Fix items have been fixed and re-verified — the CRLF normalization gap is closed and locked
in with a regression test using real `\r\n` input, and the output-path bug is fixed and re-verified with
the exact invocation that originally reproduced it, including confirming `git status` no longer shows a
stray untracked directory at the repo root. The `implementation-summary.md` and `observed-limitations.md`
reproduction commands have also been corrected. The overall architecture, test discipline, and
real-document validation were already genuinely strong; with both confirmed defects closed, there's
nothing outstanding blocking this milestone.
