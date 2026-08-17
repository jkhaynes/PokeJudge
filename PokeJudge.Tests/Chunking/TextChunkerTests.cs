namespace PokeJudge.Tests.Chunking;

using PokeJudge.Chunking;

public class TextChunkerTests
{
    [Fact]
    public void Chunk_TextShorterThanTargetSize_ReturnsSingleChunk()
    {
        var result = TextChunker.Chunk("One sentence. Another sentence.", targetChunkSize: 200, overlapSentences: 1);

        Assert.Single(result);
        Assert.Equal("One sentence. Another sentence.", result[0]);
    }

    [Fact]
    public void Chunk_TextLongerThanTargetSize_SplitsAtSentenceBoundaries()
    {
        const string text =
            "First sentence is here. Second sentence follows. Third sentence continues. Fourth sentence ends it.";

        var result = TextChunker.Chunk(text, targetChunkSize: 40, overlapSentences: 0);

        Assert.True(result.Count > 1);
        // Every chunk should be made of whole sentences -- none should start or end mid-word.
        Assert.All(result, chunk => Assert.Matches(@"^[A-Z].*[.!?]$", chunk));
    }

    [Fact]
    public void Chunk_WithOverlap_RepeatsLastSentencesOfPreviousChunkAtStartOfNext()
    {
        const string text =
            "First sentence is here. Second sentence follows. Third sentence continues. Fourth sentence ends it.";

        var result = TextChunker.Chunk(text, targetChunkSize: 40, overlapSentences: 1);

        Assert.True(result.Count > 1);
        // The overlap sentence carried into chunk 2 should be the last sentence of chunk 1.
        var lastSentenceOfFirstChunk = result[0].Split(". ").Last();
        Assert.Contains(lastSentenceOfFirstChunk.TrimEnd('.'), result[1]);
    }

    [Fact]
    public void Chunk_WithoutOverlap_ChunksDoNotShareSentences()
    {
        const string text =
            "First sentence is here. Second sentence follows. Third sentence continues. Fourth sentence ends it.";

        var result = TextChunker.Chunk(text, targetChunkSize: 40, overlapSentences: 0);

        Assert.True(result.Count > 1);
        Assert.DoesNotContain("First sentence is here", result[1]);
    }

    [Fact]
    public void Chunk_OverlapLargerThanSentencesPerChunk_DoesNotGrowChunksUnboundedly()
    {
        // overlapSentences (5) far exceeds how many ~20-25 char sentences fit in a
        // 10-character target. Without a defensive cap, each new chunk boundary
        // would fail to shrink the carried-forward overlap at all, so every
        // subsequent chunk would accumulate the entire document's sentences so far
        // instead of a bounded sliding window.
        const string text =
            "First sentence is here. Second sentence follows. Third sentence continues. Fourth sentence ends it.";

        var result = TextChunker.Chunk(text, targetChunkSize: 10, overlapSentences: 5);

        Assert.True(result.Count > 1);
        Assert.All(result, chunk =>
            Assert.True(chunk.Length <= 60, $"Chunk unexpectedly large: \"{chunk}\" ({chunk.Length} chars)"));
    }

    [Fact]
    public void Chunk_SingleSentenceLongerThanTargetSize_BecomesItsOwnChunkWithoutFurtherSplitting()
    {
        // Deliberate simplification: an oversized single sentence is not split further.
        var oneLongSentence = "This is one very long sentence that on its own already exceeds the target chunk size limit set for this test case.";

        var result = TextChunker.Chunk(oneLongSentence, targetChunkSize: 20, overlapSentences: 0);

        Assert.Single(result);
        Assert.Equal(oneLongSentence, result[0]);
    }

    [Fact]
    public void Chunk_EmptyText_ReturnsEmptyList()
    {
        var result = TextChunker.Chunk(string.Empty, targetChunkSize: 200, overlapSentences: 1);

        Assert.Empty(result);
    }

    [Fact]
    public void Chunk_TextWithoutTrailingPunctuation_StillCapturesFinalSentence()
    {
        var result = TextChunker.Chunk("A sentence with no ending punctuation", targetChunkSize: 200, overlapSentences: 0);

        Assert.Single(result);
        Assert.Equal("A sentence with no ending punctuation", result[0]);
    }
}
