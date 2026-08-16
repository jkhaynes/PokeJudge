# Milestone 3 — Learning Checkpoint

## Q&A Transcript

### 1. Extraction vs. normalization

**Question:** Early in this milestone, before writing any implementation, you compared PdfPig's default `page.Text` output against `ContentOrderTextExtractor`. The default blob-text extraction was actually the bigger threat to the pipeline working at all — bigger than any of the normalization heuristics (whitespace collapsing, hyphen rejoining) built afterward. Why? What specifically would have broken if `PdfTextExtractor` had stuck with the default `page.Text`?

**Answer (verbatim):** "It showed things out of order. It wasn't readable in the way that it was meant to be so could have messed up reasoning."

**Feedback:** Mostly correct. Right general area (extraction quality upstream silently breaking downstream reasoning), but the specific mechanism wasn't scrambled order — the content stays in correct reading sequence. What actually breaks is line-break structure: a heading and its following paragraph can end up concatenated onto a single line with no newline separator. This mattered specifically because `TableOfContentsParser` and the earliest heading-detection experiment both work line-by-line; against raw `page.Text` that line-by-line scan found almost nothing (confirmed: the first scratch inspection against raw text found zero real headings on the TOC page). `ContentOrderTextExtractor` reconstructs real line breaks, which is what made line-based parsing — and therefore TOC-driven section splitting — possible at all. Memory trick given: *not "wrong order," but "missing seams" — the words are right, the line breaks between them are gone.*

### 2. Why TOC-driven detection, not body-text pattern matching

**Question:** Before settling on parsing the document's own Table of Contents, you tried detecting section headings by scanning body text directly for lines matching a numbered pattern. What specifically went wrong with that approach, and why does cross-referencing against the TOC avoid it?

**Answer (verbatim):** "The pattern returned false negatives. It was matching text in places that we were not looking for."

**Follow-up asked:** Clarified the false-positive/false-negative distinction (false positive = detector says "heading!" when the true answer is "not a heading"; false negative = a real heading the detector fails to catch) and asked whether the actual observed problem was a false-positive or false-negative problem, or could in principle be both.

**Follow-up answer (verbatim):** "False positives is what i Meant :)"

**Feedback:** Correct after self-correction. Connected the two failure modes into one framework: body-text pattern matching caused false positives (numbered instructions like "1. Visit Pokemon.com." falsely detected as headings), which TOC-driven detection fixed — but TOC-driven detection introduces its own false-negative risk (a real subsection not listed in the TOC is never detected as a boundary), which is the exact mechanism behind the citation-granularity finding in Question 3. Memory trick given: *you can't cross-reference your way out of every gap — you just trade which side of the error you're exposed to.*

### 3. Tracing the citation-granularity finding

**Question:** Walk me through the actual mechanism: why does `PPTRH-3.3` in the real output end up containing the full text of 11 real subsections instead of each getting its own citation, while `TCGTH-7.4.6` gets its own precise citation in the other document? What specifically differs between the two documents that causes this?

**Answer (verbatim):** "For 3.3, the subsections were not listed in the table of contents. 7.4.6 was listed in the table of contents."

**Feedback:** Correct — identified the right document-level causal explanation, directly following from Question 2's false-negative framing. Added the code-level connective tissue: `SectionSplitter` only creates a citation boundary at a location where a *parsed* `TocEntry`'s heading text is found in the body; since `3.3.1` never became a `TocEntry` (not listed in PPTRH's TOC), `SectionSplitter` has no reason to split there, so all the text between `3.3` and `4` (the next parsed entry) becomes one section. `TCGTH-7.4.6` got its own citation for the mirror-image reason — its heading was parsed into a `TocEntry`. Memory trick given: *the pipeline only knows the sections its input told it about — a document with a shallow TOC produces shallow citations, regardless of how deep the real content actually goes.*

### 4. The CRLF bug's root cause

**Question:** The review found that `CollapseWhitespace`'s blank-line regex (`\n{3,}`) silently did nothing on real PDF output, and the existing unit test for it passed anyway. Explain precisely why the test passed despite the code being broken — what was different between the test's input and the real input that let this slip through?

**Answer (verbatim):** "The test tested a different newline format than the one PdfTextExtractor was actually producing."

**Feedback:** Correct. Added the precise mechanism: `\n{3,}` requires three or more *consecutive* `\n` characters; in real `\r\n\r\n\r\n` output, every `\n` has a `\r` immediately before it, so there is never a run of even two consecutive `\n`s, let alone three — the pattern was structurally incapable of matching real output regardless of how many blank lines existed. Against the test's `\n`-only fixture, three bare `\n`s in a row is exactly what the pattern was built to find, so the test passed while testing something that didn't resemble production input. Memory trick given: *a fixture that's cleaner than reality gives you false confidence, not real coverage — test with the ugly data, not the tidy stand-in.*

### 5. The output-path fix

**Question:** The fix for the second bug anchors the output directory to `Program.cs`'s own file location at compile time via `[CallerFilePath]`, instead of using a plain relative path. Why does a plain relative path break depending on how `dotnet run` is invoked, and why does the `[CallerFilePath]` approach avoid that?

**Answer (verbatim):** "The plain relative path will resolve to whatever the working directy is that the run command was initiated from. CallerFilePath sets the path to where the file is that makes the call to callerfilepath, in this case that is program.cs which is located in the correct folder."

**Feedback:** Correct, and precisely stated on both halves of the mechanism — relative paths resolve against the process's runtime working directory (which varies by invocation style), while `[CallerFilePath]` is filled in by the compiler at build time with the literal source-file path of the call site, so it's fixed correctly regardless of later invocation. Memory trick given: *relative paths ask "where am I running from right now?" — `[CallerFilePath]` already knows "where I was written," permanently.*

### 6. Why a zero-LLM-call milestone still matters

**Question:** Milestone 3 makes zero calls to Gemini or any model. Why does this still count as meaningful AI-engineering work for a RAG system, rather than being a detour from the actual learning track?

**Answer (verbatim):** "Because in a RAG system, one of the most important parts is retrieving and formatting the data you will give the llm to make its reasonings. That's what we are working on now."

**Feedback:** Correct. Added the sharper framing: the risk of bad ingestion is specifically *silent* degradation — no LLM call to misbehave, nothing crashes, the pipeline just runs. The damage (e.g., `PPTRH-3.3` swallowing 11 subsections) only becomes visible once real retrieval is wired up in Milestones 5-6, as a retrieval-precision problem rather than something traceable back to ingestion. Underscores that a large fraction of what makes a real RAG system work is unglamorous deterministic data engineering, not prompting. Memory trick given: *bad ingestion doesn't announce itself — it just quietly caps how good every later milestone can possibly be.*

### 7. Copyright hygiene design

**Question:** Both the real source PDFs and the pipeline's JSON output are gitignored, never committed. If those `.gitignore` entries were accidentally removed and `PPTRH.json` got committed to the public GitHub repo, what specifically would be exposed, and why is that a real concern rather than an overcautious one for a personal learning project?

**Answer (verbatim):** "Because of possible copywrite infringements."

**Follow-up asked:** Requested specifics — what exactly is inside `PPTRH.json` that makes committing it different from just having the PDF on a laptop, and why the "public" part of "public GitHub" specifically matters.

**Follow-up answer (verbatim):** "It amounts to reproducing the potentially copywrited material and is available publicly for anyone to see."

**Feedback:** Correct after the follow-up. Named the real distinction: a local copy is personal use; a public commit is publication — anyone could view and copy a substantial, largely-complete reproduction of the source document's text, not just a short citation-style excerpt. This is exactly the line the plan's "fair-use excerpts needed for citation context" language draws, and why gitignoring both the PDF and the ingested output was treated as "important, not optional." Memory trick given: *a laptop is a drawer; a public GitHub repo is a bulletin board — the same file means something legally different depending on which one it's in.*

---

## Learning Checkpoint Result

**Strong**

## Concepts I Understand

- The precise mechanism by which raw PDF text extraction can lose line-break structure, and why that specifically breaks line-based parsing (not just "readability" in the abstract).
- False positive vs. false negative as a framework, including self-correcting the terminology and then applying it to explain a real tradeoff (body-text pattern matching's false positives vs. TOC-driven detection's false negatives).
- Precise, code-level tracing of why citation granularity differs between two real documents, connecting a document-level fact (TOC depth) to the exact code mechanism (`SectionSplitter` only creates boundaries at parsed `TocEntry` locations).
- The exact regex mechanism behind the CRLF bug, and the transferable lesson about test fixtures needing to reflect real data shape.
- The precise difference between working-directory-relative paths and compile-time-anchored paths (`[CallerFilePath]`), and why that distinction matters for invocation-independence.
- Why deterministic data-engineering work (with zero LLM calls) is still core AI-engineering work, framed around *silent* degradation rather than a vague appeal to importance.

## Concepts to Reinforce

- Precision on first-pass answers when a question asks "why specifically" — several answers (Q1, Q4-partially, Q7) were directionally correct on the first attempt but needed a nudge or follow-up to reach the specific mechanism. Not a gap in understanding so much as a habit of answering at the right altitude the first time rather than needing a prompt to go one level deeper.
- False positive / false negative terminology — corrected quickly and confidently once flagged, but worth having crisply memorized going in next time it's relevant (e.g., Milestone 8's evaluation metrics will lean on this same vocabulary).

## Milestone Takeaway

1. Extraction and normalization are genuinely separate concerns with separate failure modes — the extraction *method* choice (line-break-preserving vs. not) mattered more here than any normalization heuristic built afterward.
2. A redesign that fixes one failure mode (false positives from blind pattern matching) can introduce a different one (false negatives bounded by the TOC's own depth) — fixing a bug rarely means eliminating error, just relocating it somewhere more contained.
3. Bad ingestion fails silently — there's no crash, no misbehaving LLM call to notice. The cost only becomes visible once retrieval is built on top of it in later milestones, which is exactly why this milestone's real-document validation and honest documentation of limitations mattered.
4. Two real bugs (CRLF-blind regex, working-directory-dependent output path) were caught by review, not by the original test suite — both are concrete, memorable examples of why test fixtures need to match real data shape, and why file paths need to be anchored independent of how a program gets invoked.

## Interview Readiness

1. **"How do you decide whether a data-pipeline failure is worth investigating before or after it causes a visible problem?"** — A strong answer distinguishes failures that surface immediately (exceptions, crashes) from ones that degrade quality silently (like ingestion errors that only show up as bad retrieval two milestones later), and explains why the latter category demands more deliberate validation (real-data smoke tests, not just unit tests) precisely because nothing will complain on its own.
2. **"Describe a time a design change fixed one bug but introduced a different failure mode — how did you reason about whether that tradeoff was worth it?"** — A strong answer can point to the TOC-driven redesign specifically: it traded body-text false positives for TOC-depth-bounded false negatives, and can explain why that tradeoff was still net-positive (the false-negative failure mode is bounded and predictable — "as granular as the source's own TOC" — versus the false-positive mode being unpredictable and unbounded).
3. **"Why do file paths sometimes behave differently in production than they did in development?"** — A strong answer explains relative-path resolution against a runtime working directory that isn't guaranteed to match what a developer assumed, and can name at least one invocation-independent alternative (anchoring to the executing assembly's location, embedding a path at compile time, or requiring an explicit configured root).

## Recommendation

**Ready for PR Review**
