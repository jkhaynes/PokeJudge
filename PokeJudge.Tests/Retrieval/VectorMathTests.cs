namespace PokeJudge.Tests.Retrieval;

using PokeJudge.Retrieval;

public class VectorMathTests
{
    [Fact]
    public void CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        var a = new float[] { 1f, 2f, 3f };
        var b = new float[] { 1f, 2f, 3f };

        var result = VectorMath.CosineSimilarity(a, b);

        Assert.Equal(1.0, result, precision: 10);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        var a = new float[] { 1f, 0f };
        var b = new float[] { 0f, 1f };

        var result = VectorMath.CosineSimilarity(a, b);

        Assert.Equal(0.0, result, precision: 10);
    }

    [Fact]
    public void CosineSimilarity_OppositeVectors_ReturnsNegativeOne()
    {
        var a = new float[] { 1f, 2f, 3f };
        var b = new float[] { -1f, -2f, -3f };

        var result = VectorMath.CosineSimilarity(a, b);

        Assert.Equal(-1.0, result, precision: 10);
    }

    [Fact]
    public void CosineSimilarity_IsMagnitudeInvariant_SameDirectionDifferentScale()
    {
        var a = new float[] { 1f, 2f, 3f };
        var scaled = new float[] { 10f, 20f, 30f };

        var result = VectorMath.CosineSimilarity(a, scaled);

        Assert.Equal(1.0, result, precision: 10);
    }

    [Fact]
    public void CosineSimilarity_MismatchedLengths_ThrowsArgumentException()
    {
        var a = new float[] { 1f, 2f };
        var b = new float[] { 1f, 2f, 3f };

        Assert.Throws<ArgumentException>(() => VectorMath.CosineSimilarity(a, b));
    }

    [Fact]
    public void CosineSimilarity_ZeroVector_ThrowsRatherThanDividingByZero()
    {
        var a = new float[] { 0f, 0f, 0f };
        var b = new float[] { 1f, 2f, 3f };

        Assert.Throws<InvalidOperationException>(() => VectorMath.CosineSimilarity(a, b));
    }
}
