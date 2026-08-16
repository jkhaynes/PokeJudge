namespace PokeJudge.Chunking;

using PokeJudge.Ingestion;

// ChunkId extends IngestedSection.SectionId's scheme ("PPTRH-3.3#0", "PPTRH-3.3#1")
// so a retrieved chunk can always be traced back to its section-level citation.
public sealed record TextChunk(string ChunkId, string SectionId, string Text, SourceDocumentMetadata Source);

public sealed record EmbeddedChunk(TextChunk Chunk, float[] Embedding);

// Serializable output shape for a whole document's worth of embedded chunks --
// mirrors IngestedDocument's shape from Milestone 3, one level down.
public sealed record ChunkedDocument(SourceDocumentMetadata Metadata, List<EmbeddedChunk> Chunks);
