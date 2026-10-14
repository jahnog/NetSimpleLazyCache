namespace SimpleLazyCache.Unit.Tests;

public class SingleFactoryCallerArgumentTests
{
    private readonly SingleFactoryCaller<TestData> _caller = new SingleFactoryCaller<TestData>();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetOrAddAsync_NullOrEmptyKey_ThrowsArgumentException(string? key)
    {
        var calls = 0;

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _caller.GetOrAddAsync(key!, () =>
            {
                calls++;
                return Task.FromResult(new TestData(1));
            }));

        Assert.Equal("key", exception.ParamName);
        Assert.StartsWith("Key cannot be null or empty.", exception.Message);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task GetOrAddAsync_NullFactory_ThrowsArgumentNullException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _caller.GetOrAddAsync("key", null!));

        Assert.Equal("valueFactory", exception.ParamName);
    }

    [Fact]
    public async Task GetOrAddAsync_NullKeyAndNullFactory_ThrowsArgumentException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _caller.GetOrAddAsync(null!, null!));

        Assert.Equal("key", exception.ParamName);
    }

    [Fact]
    public async Task GetOrAddAsync_WhitespaceKey_ReturnsFactoryInstance()
    {
        var expected = new TestData(1);

        var result = await _caller.GetOrAddAsync(" ", () => Task.FromResult(expected));

        Assert.Same(expected, result);
    }
}
