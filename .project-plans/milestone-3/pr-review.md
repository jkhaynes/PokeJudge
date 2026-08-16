# PR Review — Milestone 3

## PR Review Summary

- **Milestone:** Milestone 3 — Document Ingestion
- **Current branch:** `milestone/3-document-ingestion`
- **Base branch:** `master`
- **What changed:** No commits exist yet on this branch (`git rev-list --count master..HEAD` = 0) — all Milestone 3 work is currently uncommitted working-tree changes, same situation Milestone 2 was in before its first commit. The effective diff against `master` is: new `PokeJudge/Ingestion/` (`IngestedDocument`, `PdfTextExtractor`, `PageTextNormalizer`, `TableOfContentsParser`, `DocumentMetadataParser`, `SectionSplitter`, `IngestionPipeline`); a new `ingest <path> <document-code>` console mode added to `PokeJudge/Program.cs`; the `PdfPig` package reference added to `PokeJudge.csproj`; two new `.gitignore` entries (`docs/*.pdf`, `PokeJudge/Ingestion/Output/`); matching new test folders under `PokeJudge.Tests/Ingestion/`; and `.project-plans/milestone-3/` planning docs.
- **Overall impression:** Strong. A prior milestone review on this same branch found two real, confirmed bugs (a CRLF-blind normalization regex, and a working-directory-dependent output path that could leak copyrighted content outside `.gitignore`'s coverage) — both have since been fixed, covered by a new regression test, and re-verified against both real documents with the exact invocation that originally reproduced the bug. This review independently re-confirms that fix held up and finds nothing else blocking.
- **Build/test status:** `dotnet build` — succeeded, 0 warnings, 0 errors. `dotnet test` — 56/56 passed (55 at time of review, plus 1 added for Minor Issue #3 below).

## 🚫 Blockers

None.

## ⚠️ Major Issues

None.

## 🔎 Minor Issues

- ~~**`PokeJudge/Program.cs:21`** — the Milestone 3 header comment still documents usage as `` `dotnet run -- ingest <path-to-pdf>` ``, missing the `<document-code>` argument the command actually requires.~~ **Fixed.** The header comment now reads `` `dotnet run -- ingest <path-to-pdf> <document-code>` ``, matching `RunIngestion`'s actual usage message.
- **`PokeJudge/Ingestion/IngestedDocument.cs:14`** — `Sections` is `List<IngestedSection>`, not the plan's specified `IReadOnlyList<IngestedSection>`. Consistent with Milestone 2's precedent of preferring `List<T>` for JSON-round-tripped structured types, and not a functional issue, but — as already noted in the milestone review — it's a plan deviation that was never explicitly called out the way Milestone 2's `CompleteAsync` removal was. (Not addressed — left as a documented, low-priority deviation, not requested to be fixed.)
- ~~**`PageTextNormalizerTests.cs`** — `RejoinHyphenatedLineWraps` has no explicit `\r\n` test case, unlike `CollapseWhitespace` (which needed one to catch its real bug).~~ **Fixed.** Added `RejoinHyphenatedLineWraps_HyphenAtLineEndWithCarriageReturn_JoinsAcrossLineBreak`, confirming the existing `\r?\n` regex handles real `\r\n` input as intended — passed immediately, since there was no underlying bug here, just missing explicit coverage.

## 💬 Review Notes

- Worth noting explicitly: this branch already went through one review → fix → re-verify cycle (the two bugs above) before this PR review started. Both fixes were independently re-confirmed here by reading the current code and diff directly, not just trusting the milestone documents' account of them — `PageTextNormalizer.CollapseWhitespace` now normalizes line endings before collapsing blank lines, and `Program.cs`'s output path is anchored via `[CallerFilePath]` rather than a working-directory-relative path. Both match what `review.md` and `observed-limitations.md` describe.
- `SectionSplitter`'s exact-substring, first-match heading search remains a known, already-documented, unexercised risk (a verbatim cross-reference elsewhere in a document could cause a misplaced split). No new information here — just confirming it's still accurately disclosed, not silently dropped from the record after the bug-fix pass.

## 🤖 AI-Specific Review

Milestone 3 introduces no LLM-related code — that is the deliberate point of this milestone. Verified
directly, not just taken on faith: the `ingest` branch (`if (args.Length > 0 && args[0] == "ingest") { return RunIngestion(args); }`) sits *before* `Program.cs` builds the Gemini configuration and reads the API key, so the ingestion path genuinely never touches the LLM provider, the user-secrets configuration, or any network call — this is an enforced code-level boundary, not just a documented intention. Nothing else in this diff carries AI-specific risk (no prompts, no model output, no retrieval) — the only thing worth flagging under this heading is confirming that boundary actually holds, which it does.

## 🧪 Test Review

- **Coverage of deterministic logic remains thorough**, now including a regression test for the CRLF
  bug (`CollapseWhitespace_ManyBlankLinesWithCarriageReturns_CollapseToOneParagraphBreak`) that uses
  real `\r\n` input specifically, closing the gap that let the original bug pass silently.
- **The fix was validated against real data, not just the new unit test**: both real documents
  (`PPTRH.json`, `TCGTH.json`) were re-ingested after the fix and confirmed to no longer contain any
  raw `\r\n` sequences — a meaningful check beyond what a unit test alone can prove.
- **The output-path fix was validated the same way**: re-run with the exact `dotnet run --project
  PokeJudge -- ingest ...` invocation (from the repo root) that originally reproduced the bug, with
  `git status` confirmed clean afterward (no stray untracked directory). This is exactly the right kind
  of regression check for a bug that a unit test can't easily express (`Program.cs`'s console/file I/O
  remains intentionally untested, consistent with Milestones 1-2's precedent).
- **Build/test results:** `dotnet build` clean; `dotnet test` 55/55 passed.

## 📦 Scope Check

- **Does this branch correspond to the current milestone?** Yes.
- **Does the diff contain only work appropriate to this milestone?** Yes — including the mid-implementation scope expansion (ingesting a second real document), which was made at explicit developer direction and is transparently documented as a deviation from the plan's stated minimum, not something slipped in silently.
- **Did unrelated changes get mixed into the branch?** No — `git status` shows exactly the expected files plus `.project-plans/milestone-3/` docs.
- **Did it implement anything from future milestones?** No — no chunking, embeddings, vector search, or retrieval wiring; the Milestone 1-2 mock corpus is untouched and still the clarification loop's only source of context.
- **Did it introduce unnecessary architecture or dependencies?** No. One new NuGet package (`PdfPig`), directly necessary for the milestone's stated purpose. No new projects — `PokeJudge/Ingestion/` sits inside the existing single console project, consistent with the PRD's modular-monolith direction.

## Final Verdict

**Approve**

No blockers or major issues. The two real bugs a prior review caught were fixed, tested, and
independently re-verified here. Of the three minor items, two (the stale usage comment, the missing
`\r\n` test case) are now fixed and confirmed via a clean build and 56/56 passing tests; the remaining
one (`List<T>` vs. the plan's `IReadOnlyList<T>`) is a low-priority, documented deviation left
unaddressed by choice, not oversight. Nothing outstanding blocks this PR.
