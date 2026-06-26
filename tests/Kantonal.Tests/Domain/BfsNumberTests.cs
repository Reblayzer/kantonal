using Kantonal.Domain;

namespace Kantonal.Tests.Domain;

public class BfsNumberTests
{
    [Fact]
    public void Create_ReturnsValue_ForPositiveNumber()
        => Assert.Equal(4551, BfsNumber.Create(4551).Value);

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_Throws_ForNonPositive(int value)
        => Assert.Throws<ArgumentOutOfRangeException>(() => BfsNumber.Create(value));
}
