# Milestone 3 — Document Ingestion

Status: planned, not started
Source: PRD.md §7-8 (Functional/AI-Specific Requirements), §11 (architecture progression), §12 (tech stack), §13 (Data/Source Strategy), §16 (Security), §18 (open questions), "Learning Objectives → Milestone 3"

## 1. What We Will Build

The first milestone with **no LLM calls at all**. Everything in this milestone is deterministic text
processing, laying the groundwork Milestones 4-6 build retrieval on top of:

1. A **PDF text-extraction step**: given a real, developer-supplied official Pokémon TCG policy
   document (PDF), extract its raw text using a .NET PDF library (candidate: PdfPig — pure .NET, MIT
   licensed, extraction-focused; confirmed at implementation start). This raw output is expected to be
   messy — running headers/footers, page numbers, hyphenated line-wraps, inconsistent whitespace — and
   that messiness is deliberately observed, not hidden, before any cleanup.
2. A **normalization step**: deterministic text-cleanup functions that turn the raw extraction into
   readable, citation-ready prose — collapsing whitespace, stripping repeated headers/footers/page
   numbers, rejoining hyphenated line-wraps, without silently destroying real structure (e.g., genuine
   paragraph breaks, legitimately hyphenated compound words).
3. A **section/citation-metadata step**: splitting normalized text into logical sections (e.g.,
   numbered rule/policy headings) and attaching citation metadata to each — document title, version
   or effective date, and section/rule number or heading — so every section is independently citable.
4. A **structured, serializable output model** (`IngestedDocument` / `IngestedSection`) that later
   milestones (chunking in M4, embeddings in M4, retrieval in M5) will consume. For this milestone,
   "storage" is as simple as writing this structured result to a local JSON file — no database, no
   vector store yet.
5. A **console entry point** to run the pipeline against a real local file and print/export a clear
   before/after comparison (raw extracted text vs. normalized, sectioned, citation-tagged output), so
   the "garbage in, garbage out" lesson is directly observable, not just asserted.

Per PRD §11's architecture progression, this stays **one project** — a new `PokeJudge/Ingestion/`
folder alongside `AI/`, `StructuredState/`, and `Clarification/`, not a separate ingestion project or
tool. `Program.cs` gains a lightweight mode switch (e.g., an `ingest <path>` argument) rather than a
second executable.

### Copyright / redistribution hygiene (important, not optional)

Per PRD §13's explicit caveat ("do not scrape or include unauthorized copyrighted redistributions
beyond fair-use excerpts needed for citation context") and §16 generally:

- The real source PDF the developer supplies is **not committed to source control** — it's a local
  input file, added to `.gitignore`, not pushed to the public GitHub repo.
- The pipeline's **output** for that real document (extracted/normalized text, the serialized
  `IngestedDocument` JSON) is likewise **not committed** — same reasoning, it would reproduce large
  portions of copyrighted text in git history.
- **Automated unit tests do not depend on the real document.** They run against small, hand-crafted
  text fixtures (committed, no copyright concern) that mimic the kinds of artifacts a real PDF
  extraction produces (repeated headers, hyphenated wraps, page numbers) — this is also just better
  test hygiene, since a unit test shouldn't depend on a large external file that may not exist in
  every checkout.
- The observed-limitations write-up (step 8 below) quotes only short excerpts for illustration, in
  the same fair-use spirit as a citation, not full reproduced sections.

## 2. AI Concepts Being Learned

- **Extraction vs. normalization as distinct concerns**: extraction gets text out of a source format;
  normalization decides what "clean" means for that text, and those are different jobs with different
  failure modes.
- **"Garbage in, garbage out" as a concrete, observable RAG-pipeline reality**: bad extraction or
  normalization here silently degrades every later milestone's retrieval and generation quality, even
  though this milestone itself never touches a model.
- **Metadata design for citation, decided ahead of need**: document title, version/date, and
  section/rule number have to be captured at ingestion time — PRD §7 FR7's citation requirement and
  §13's versioning requirement can't be retrofitted later onto text that was never tagged with them.
- **Why not every "AI system" milestone involves calling a model**: this milestone is plumbing. That's
  itself worth internalizing — a RAG system's reliability depends as much on unglamorous deterministic
  data preparation as it does on prompting or retrieval logic.
- **Real-world document handling as an actual engineering constraint** (not just a rules concern):
  working with authentic, copyrighted source material forces explicit decisions about what gets
  committed to a public repo vs. kept local-only — a genuine "AI engineering in practice" lesson, not
  a hypothetical one.

## 3. Implementation Steps (in order)

1. **Place the real source document locally.** Developer supplies one official Pokémon TCG policy PDF
   (e.g., the Play! Pokémon Tournament Rules Handbook, or the Penalty Guidelines — a shorter document
   is fine and keeps this milestone's scope proportional) at a local, gitignored path.
2. **Add a PDF text-extraction library** (PdfPig is the leading candidate; confirm at implementation
   start per PRD §12's "decide exact package when needed" policy) and build the raw-extraction step:
   given a file path, return the raw extracted text. Manually inspect this output against the real
   document and note its concrete artifacts — this becomes the evidence trail for step 4's
   normalization design, not a hypothetical list written in advance.
3. **Design the output data model** as C# records:
   - `SourceDocumentMetadata(string Title, string Version, string? EffectiveDate)`
   - `IngestedSection(string SectionId, string Heading, string Text, SourceDocumentMetadata Source)`
   - `IngestedDocument(SourceDocumentMetadata Metadata, IReadOnlyList<IngestedSection> Sections)`
4. **Build normalization functions** against the artifacts actually observed in step 2: collapse
   whitespace/line breaks while preserving paragraph structure, strip repeated headers/footers/page
   numbers, rejoin hyphenated line-wraps. Each function is a small, pure, independently testable unit.
5. **Build section/citation-metadata extraction**: detect logical section boundaries (e.g., a
   numbered-heading pattern matching this document's actual structure) and attach
   `SourceDocumentMetadata` to each resulting `IngestedSection`. Document title/version/effective date
   are manually supplied constants for now — automatic version detection stays out of scope (PRD §18).
6. **Serialize the result** (`IngestedDocument` → local JSON file, gitignored) so Milestone 4 has a
   concrete artifact to consume.
7. **Wire the console `ingest` mode**: run the full pipeline against the real local file and print a
   before/after comparison (raw extracted text vs. final normalized, sectioned, citation-tagged
   output) for at least a few representative sections.
8. **Document observed extraction/normalization issues** with short, fair-use-scale excerpts — where
   the real document's structure didn't match the section-detection pattern, where normalization had
   to make a judgment call, etc. — in the same evidence-based style as Milestones 1-2's observation
   docs.

## 4. Expected Limitations / Failures to Intentionally Observe

- **Section-boundary detection is pattern-based, not semantic.** It will misfire on real-world
  formatting the pattern wasn't designed for — a table, an appendix without numbered headings, a
  multi-column layout artifact surviving extraction. Expect concrete failures once run against the
  actual supplied document, not just in theory.
- **Normalization heuristics are tuned to one document's specific artifacts** and are not guaranteed
  to generalize to a differently-formatted source. This is the direct motivation for treating
  normalization as something to keep revisiting as more real documents are ingested, rather than a
  one-time solved problem.
- **No automatic document-version detection.** Version/effective date is manually supplied metadata,
  not parsed from the document itself — true multi-version reconciliation stays deferred (PRD §18,
  revisited at Milestone 7).
- **Still no retrieval, no chunking, no embeddings.** `IngestedSection` objects exist as structured
  data but nothing can search over them yet — a badly-extracted or mis-sectioned passage won't cause
  a visible failure *this milestone*, but will silently produce bad retrieval/citation results once
  Milestones 5-6 build on top of this data. That gap is real and intentional, not hidden.

## 5. What I Should Understand by the End

- The practical difference between extraction and normalization, and why conflating them produces
  worse results than treating them as separate steps with separate failure modes.
- Concrete, first-hand examples (from a real document, not a toy) of what raw PDF extraction artifacts
  look like, and why they would corrupt embeddings/retrieval quality if left unaddressed.
- Why citation metadata (document, version, section) has to be designed and captured at ingestion
  time — tracing directly back to PRD §7 FR7's citation requirement and §13's versioning requirement.
- Why this milestone intentionally has no LLM calls, and what "garbage in, garbage out" concretely
  means for a RAG pipeline's downstream quality.
- Why working with real, copyrighted source material requires explicit decisions about what belongs
  in a public git history and what doesn't — a genuine, non-hypothetical AI-engineering constraint.

## Out of Scope for This Milestone

- Chunking strategy and embeddings (Milestone 4).
- Vector storage and semantic search (Milestone 5).
- Retrieval-grounded ruling generation (Milestone 6) — ingestion output isn't wired into the
  clarification loop yet; Milestones 1-2's mock corpus remains untouched and still in use until then.
- Automatic document-version detection or multi-version conflict handling (PRD §18; revisited at
  Milestone 7).
- Ingesting all of PRD §13's document categories — one real document is sufficient to build and prove
  the pipeline; ingesting the rest is mechanical repetition of the same pipeline, not new engineering.
- A separate ingestion project/tool — stays inside the single PokeJudge console project per PRD §11's
  modular-monolith principle, unless a concrete need emerges.
