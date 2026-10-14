namespace SimpleLazyCache.Unit.Tests;

public class SingleFactoryCallerFailureTests
{
    private readonly SingleFactoryCaller<TestData> _caller = new SingleFactoryCaller<TestData>();

    [Fact]
    public async Task GetOrAddAsync_FactoryThrowsBeforeReturningTask_PropagatesAndNextCallSucceeds()
    {
        var calls = 0;
        var expected = new TestData(1);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _caller.GetOrAddAsync("key", () =>
            {
                calls++;
                throw new InvalidOperationException("factory failed");
            }));

        var result = await _caller.GetOrAddAsync("key", () =>
        {
            calls++;
            return Task.FromResult(expected);
        });

        Assert.Equal("factory failed", exception.Message);
        Assert.Equal(2, calls);
        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetOrAddAsync_FaultedTask_SurfacesInnerException()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _caller.GetOrAddAsync("key", () =>
                Task.FromException<TestData>(new InvalidOperationException("task failed"))));

        Assert.Equal("task failed", exception.Message);
    }

    [Fact]
    public async Task GetOrAddAsync_NullTask_ThrowsInvalidOperationException()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _caller.GetOrAddAsync("key", () => null!));

        Assert.Equal("Value factory returned null task.", exception.Message);
    }

    [Fact]
    public async Task GetOrAddAsync_NullTask_NextCallSucceeds()
    {
        var calls = 0;
        var expected = new TestData(4);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _caller.GetOrAddAsync("key", () =>
            {
                calls++;
                return null!;
            }));
        var result = await _caller.GetOrAddAsync("key", () =>
        {
            calls++;
            return Task.FromResult(expected);
        });

        Assert.Equal("Value factory returned null task.", exception.Message);
        Assert.Equal(2, calls);
        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetOrAddAsync_NullResult_ReturnsNull()
    {
        var result = await _caller.GetOrAddAsync("key", () => Task.FromResult<TestData>(null!));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOrAddAsync_JoinedFactoryFaults_AllWaitersShareTheException()
    {
        var held = BlockedFactory.Hold<TestData>();
        var expected = new InvalidOperationException("shared");
        var tasks = new Task<TestData>[2];

        for (var i = 0; i < tasks.Length; i++)
            tasks[i] = _caller.GetOrAddAsync("key", held.Factory);

        held.Release.SetException(expected);

        var first = await Assert.ThrowsAsync<InvalidOperationException>(() => tasks[0]);
        var second = await Assert.ThrowsAsync<InvalidOperationException>(() => tasks[1]);

        Assert.Equal(1, held.Calls);
        Assert.Same(expected, first);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetOrAddAsync_FailureOnOneKey_DoesNotAffectAnotherKey()
    {
        var expected = new TestData(2);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _caller.GetOrAddAsync("bad", () =>
                Task.FromException<TestData>(new InvalidOperationException("bad key"))));

        var result = await _caller.GetOrAddAsync("good", () => Task.FromResult(expected));

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetOrAddAsync_CanceledFactoryTask_ThrowsTaskCanceledException()
    {
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            _caller.GetOrAddAsync("key", () => Task.FromCanceled<TestData>(new CancellationToken(true))));
    }

    [Fact]
    public async Task GetOrAddAsync_CanceledFactoryTask_NextCallSucceeds()
    {
        var calls = 0;
        var expected = new TestData(5);

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            _caller.GetOrAddAsync("key", () =>
            {
                calls++;
                return Task.FromCanceled<TestData>(new CancellationToken(true));
            }));
        var result = await _caller.GetOrAddAsync("key", () =>
        {
            calls++;
            return Task.FromResult(expected);
        });

        Assert.Equal(2, calls);
        Assert.Same(expected, result);
    }
}
