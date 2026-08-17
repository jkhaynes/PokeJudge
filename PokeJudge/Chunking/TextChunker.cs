namespace PokeJudge.Chunking;

using System.Text.RegularExpressions;

// Deliberately simple, sentence-boundary-aware chunking -- not a sophisticated
// semantic chunker. Sentences are packed greedily into a chunk until the next
// sentence would exceed targetChunkSize, then a new chunk starts, carrying
// forward the last `overlapSentences` sentences of the previous chunk for
// context continuity. A single sentence longer than targetChunkSize is not
// split further (see .project-plans/milestone-4/observed-limitations.md).
public static class TextChunker
{
    private static readonly Regex SentencePattern = new(@"[^.!?]+(?:[.!?]+)?", RegexOptions.Compiled);

    public static List<string> Chunk(string text, int targetChunkSize, int overlapSentences)
    {
        var sentences = SplitIntoSentences(text);
        if (sentences.Count == 0)
        {
            return new List<string>();
        }

        var chunks = new List<string>();
        var current = new List<string>();
        var currentLength = 0;

        foreach (var sentence in sentences)
        {
            if (current.Count > 0 && currentLength + sentence.Length > targetChunkSize)
            {
                chunks.Add(string.Join(" ", current));
                current = OverlapTail(current, overlapSentences, targetChunkSize);
                currentLength = current.Sum(s => s.Length);
            }

            current.Add(sentence);
            currentLength += sentence.Length;
        }

        if (current.Count > 0)
        {
            chunks.Add(string.Join(" ", current));
        }

        return chunks;
    }

    private static List<string> OverlapTail(List<string> sentences, int overlapSentences, int targetChunkSize)
    {
        var skip = Math.Max(0, sentences.Count - overlapSentences);
        var tail = sentences.Skip(skip).ToList();

        // Defensive cap: if overlapSentences is large relative to how many
        // sentences actually fit in one chunk, the line above can return the
        // entire previous chunk unreduced -- and if that repeats at every
        // boundary, chunks grow without bound instead of forming a small sliding
        // window. Shrink the tail (oldest-first) until it fits within one
        // chunk's budget, always keeping at least the most recent sentence.
        while (tail.Count > 1 && tail.Sum(s => s.Length) > targetChunkSize)
        {
            tail.RemoveAt(0);
        }

        return tail;
    }

    private static List<string> SplitIntoSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        return SentencePattern.Matches(text.Trim())
            .Select(m => m.Value.Trim())
            .Where(s => s.Length > 0)
            .ToList();
    }
}
