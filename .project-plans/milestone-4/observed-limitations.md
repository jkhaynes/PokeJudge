# Milestone 4 — Observed Limitations & Failures (Real-Document Run)

Captured per the plan's step 8: concrete evidence from running the chunking + embedding pipeline
against both real ingested documents from Milestone 3, in the same evidence-based style as
Milestones 1-3's observation docs.

- **Date:** 2026-08-16
- **Branch:** `milestone/4-chunking-embeddings`
- **Documents:** `PPTRH` (Play! Pokémon Tournament Rules Handbook, 37 sections) and `TCGTH` (Pokémon
  TCG Tournament Handbook, 64 sections) — Milestone 3's already-ingested output.
- **Model:** `gemini-embedding-001`, requested at `outputDimensionality: 768` (confirmed working
  against the real API; the model's native output is 3072-dimensional).
- **Result:** Both documents fully embedded — 162 chunks for `PPTRH`, 96 chunks for `TCGTH` (258
  total), written to `PokeJudge/Chunking/Output/*.chunks.json` (gitignored).

## The most important finding: the free-tier embedding quota counts items, not outer batch calls

The plan anticipated hitting a free-tier rate limit "likely to surface the same kind of ... behavior
already observed with the chat model in Milestone 2" and explicitly designed batching + resumability
to reduce (not eliminate) that risk. In practice, the failure mode was sharper and more specific than
expected:

The very first `chunk PPTRH` run — a single `batchEmbedContents` call carrying up to 100 chunks (the
initial default `batchSize`) — failed immediately with `429 RESOURCE_EXHAUSTED`, quota
`EmbedContentRequestsPerMinutePerUserPerProjectPerModel-FreeTier`, `quotaValue: "100"`. Waiting a full
75 seconds and retrying produced the *same* immediate failure on the first batch again. This strongly
suggests Google's free-tier "100 requests per minute" quota for this model counts each item inside a
`batchEmbedContents` call individually, not the outer HTTP request — so a single call carrying 100
chunks can consume the *entire* per-minute allowance by itself, leaving no room for anything else in
that window (including a second, much smaller call). This means **batching reduces round-trip
overhead and HTTP call count, but does not by itself protect against a per-minute item quota** — a
distinction the plan's original framing didn't anticipate (it treated batching primarily as a way to
cut *request count*).

Reducing `batchSize` from 100 to 25 (`Program.cs`) resolved this: subsequent runs completed multiple
25-item batches successfully within the same minute, well under the apparent 100-item ceiling.

## A real implementation gap, found by hitting the failure it was meant to prevent

The plan's step 6 explicitly called for the output to be "written incrementally enough that a partial
run (e.g., stopped by a rate limit) leaves a valid, resumable file rather than losing all progress."
The first implementation pass did not actually do this — `Program.cs` only serialized output once,
after `ChunkingPipeline.RunAsync` fully returned. When the first real 429 hit, **zero chunks had been
saved**, even though the pipeline itself was already resumable in principle (it accepts an
`alreadyEmbedded` dictionary and skips matching `ChunkId`s).

This was caught immediately by actually running the pipeline against a real document — not by code
review or by the unit tests, which used a stub embedding client that never fails and so never
exercised this path. Fixed by adding an `onProgress` callback to `ChunkingPipeline.RunAsync`, invoked
after every successful batch with a cumulative snapshot of everything embedded so far (pre-existing +
newly completed), which `Program.cs` uses to re-save the output file after each batch rather than only
at the end. A dedicated test
(`RunAsync_OnProgressCallback_FiresAfterEachBatchWithCumulativeSnapshotSoFar`) locks this in.

**This fix then paid off immediately, for real**: the second `chunk PPTRH` attempt started from
"Already embedded: 0" (the first run had saved nothing) but itself managed to save 100 chunks before
hitting the quota again; the *third* attempt (after reducing `batchSize` to 25) started from "Already
embedded: 100" and completed the remaining 62 chunks without needing to re-embed anything already
done. The resumable design was validated end-to-end by an actual, unplanned interruption — not just by
a unit test simulating one.

## Chunking quality: the oversized-single-sentence simplification was observed to matter

The plan's known simplification — a single "sentence" longer than `targetChunkSize` is not split
further — was not just a theoretical risk. Inspecting the real chunk output directly:

- **`PPTRH-3.3#3` is 2043 characters — 2.5x the 800-character target, and the single largest chunk out
  of all 162 in `PPTRH.chunks.json`.**
- Six more `PPTRH` chunks exceed 1200 characters: `PPTRH-3.2#5` (1692), `PPTRH-8#4` (1605), `PPTRH-8#3`
  (1592), `PPTRH-3.3#4` (1418), `PPTRH-3.2#4` (1290), `PPTRH-5.5#16` (1202) — 7 of 162 chunks (~4.3%)
  meaningfully oversized.
- The second document shows the same pattern: `TCGTH-4.1.1#0` is 1708 characters (2.1x target), the
  largest chunk in `TCGTH.chunks.json`.

The mechanism is visible directly in `PPTRH-3.3#3`'s text: bulleted and nested list content (`•`, `o`
markers) frequently carries no terminal `.`/`!`/`?` punctuation between items. `TextChunker`'s
regex-based sentence splitter (`[^.!?]+(?:[.!?]+)?`) has no concept of list structure, so an entire
multi-item bulleted list — sometimes spanning several real sub-topics — gets treated as a single
"sentence" and, per the documented simplification, is never split, however large it ends up being.

This recurs across both real documents (not a one-off) and is concentrated in list-heavy sections
(`3.2`, `3.3`, `5.5`, `8` in `PPTRH`; `4.1.1` in `TCGTH`). It's a real, direct cost of choosing a simple
period-based sentence splitter over something structure-aware — reasonably out of scope to fix this
milestone ("start simple, don't over-engineer a semantic chunker for a first pass"), but real, not
hypothetical. A judge-facing citation is still correct at the section level regardless (Milestone 3's
citation metadata is unaffected), but a retrieved *chunk* from one of these oversized blocks would hand
Milestone 5's eventual retrieval step a noticeably larger, less focused span of text than the target
size intends.

Separately, and unremarkably by contrast: `PPTRH-1`'s full section text (516 characters) stayed under
the 800-character target and produced exactly one chunk with no splitting — a reminder that the "does a
chunk boundary land somewhere awkward" question only shows up when checking *longer* sections, not the
short ones the console's default sample happens to show.

## Confirms the plan's core design choice: embedding cost is one-time, not ongoing

Both documents' 258 total chunks are now embedded exactly once and persisted locally. Nothing in
Milestone 5 (vector search) or beyond will need to re-call the embedding API for this already-ingested
content — only a judge's live scenario text will need embedding at query time, a single small call per
turn rather than hundreds. This is the plan's "Milestone 4 is the easy side of the free-tier concern"
framing, now demonstrated rather than assumed: the entire cost of getting both real documents
embeddable was a handful of interrupted-and-resumed runs during development, not an ongoing operational
cost.
