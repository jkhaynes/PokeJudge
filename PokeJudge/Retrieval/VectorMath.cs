namespace PokeJudge.Retrieval;

// Cosine similarity: the standard metric for comparing embedding vectors --
// measures the angle between two vectors, not their magnitude, so a chunk's
// embedding scaled differently from a query's embedding for any reason still
// compares correctly. Deliberately NOT a probability or confidence value --
// just a geometric alignment score in [-1, 1].
public static class VectorMath
{
    public static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException(
                $"Vectors must be the same length to compare (got {a.Length} and {b.Length}).");
        }

        double dot = 0;
        double normA = 0;
        double normB = 0;

        for (var i = 0; i < a.Length; i++)
        {
            dot += (double)a[i] * b[i];
            normA += (double)a[i] * a[i];
            normB += (double)b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
        {
            throw new InvalidOperationException("Cannot compute cosine similarity against a zero vector.");
        }

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
