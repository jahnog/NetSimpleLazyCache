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
    public async Task GetOrAddAsync_NullFactory_LeavesCallerUsable()
    {
        var expected = new TestData(1);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _caller.GetOrAddAsync("key", null!));
        var result = await _caller.GetOrAddAsync("key", () => Task.FromResult(expected));

        Assert.Equal("valueFactory", exception.ParamName);
        Assert.Same(expected, result);
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

    [Fact]
    public async Task GetOrAddAsync_RejectedKeyWhileAnotherCallIsInFlight_LeavesThatCallShared()
    {
        var held = BlockedFactory.Hold<TestData>();
        var expected = new TestData(1);
        var joinerCalls = 0;
        var inFlight = _caller.GetOrAddAsync("key", held.Factory);

        var rejected = await Assert.ThrowsAsync<ArgumentException>(() =>
            _caller.GetOrAddAsync("", () => Task.FromResult(new TestData(2))));
        var joiner = _caller.GetOrAddAsync("key", () =>
        {
            Interlocked.Increment(ref joinerCalls);
            return Task.FromResult(new TestData(3));
        });

        Assert.Equal("key", rejected.ParamName);
        Assert.Equal(1, held.Calls);
        Assert.Equal(0, joinerCalls);

        held.Release.SetResult(expected);
        var results = await Task.WhenAll(inFlight, joiner);

        Assert.Equal(1, held.Calls);
        Assert.Equal(0, joinerCalls);
        Assert.Same(expected, results[0]);
        Assert.Same(expected, results[1]);
    }
}
