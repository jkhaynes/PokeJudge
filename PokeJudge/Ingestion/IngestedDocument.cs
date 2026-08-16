namespace PokeJudge.Ingestion;

// Citation metadata designed ahead of need (PRD S13): a citation has to point at a
// document, a version/date, and a specific section -- retrofitting this onto text
// that was never tagged with it isn't possible later, so it's captured now, at
// ingestion time, not deferred to Milestone 4+.
public sealed record SourceDocumentMetadata(string Title, string Version, string? EffectiveDate);

public sealed record IngestedSection(string SectionId, string Heading, string Text, SourceDocumentMetadata Source);

// The structured, serializable output this milestone produces. Milestone 4 (chunking
// and embeddings) is the next consumer of this shape; nothing here is chunked,
// embedded, or searchable yet.
public sealed record IngestedDocument(SourceDocumentMetadata Metadata, List<IngestedSection> Sections);
