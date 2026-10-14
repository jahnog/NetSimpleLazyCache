namespace SimpleLazyCache.Unit.Tests;

public class SingleFactoryCallerSingleFlightTests
{
    private readonly SingleFactoryCaller<TestData> _caller = new SingleFactoryCaller<TestData>();

    [Fact]
    public async Task GetOrAddAsync_SingleCall_ReturnsFactoryInstance()
    {
        var expected = new TestData(7);

        var result = await _caller.GetOrAddAsync("key", () => Task.FromResult(expected));

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetOrAddAsync_ManyJoiners_RunsFactoryOnceAndSharesInstance()
    {
        var held = BlockedFactory.Hold<TestData>();
        var expected = new TestData(1);
        var tasks = new Task<TestData>[32];

        for (var i = 0; i < tasks.Length; i++)
            tasks[i] = _caller.GetOrAddAsync("hot", held.Factory);

        held.Release.SetResult(expected);
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, held.Calls);
        Assert.All(results, result => Assert.Same(expected, result));
    }

    [Fact(Timeout = 5000)]
    public async Task GetOrAddAsync_DifferentKeys_RunFactoriesConcurrently()
    {
        var aEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var aCalls = 0;
        var bCalls = 0;

        var taskA = _caller.GetOrAddAsync("a", async () =>
        {
            Interlocked.Increment(ref aCalls);
            aEntered.TrySetResult(true);
            await bEntered.Task;
            return new TestData(1);
        });
        var taskB = _caller.GetOrAddAsync("b", async () =>
        {
            Interlocked.Increment(ref bCalls);
            bEntered.TrySetResult(true);
            await aEntered.Task;
            return new TestData(2);
        });

        var results = await Task.WhenAll(taskA, taskB);

        Assert.Equal(1, aCalls);
        Assert.Equal(1, bCalls);
        Assert.Equal(1, results[0].Id);
        Assert.Equal(2, results[1].Id);
    }

    [Fact]
    public async Task GetOrAddAsync_KeysThatDifferByCase_EachRunFactory()
    {
        var calls = 0;

        var lower = await _caller.GetOrAddAsync("key", () =>
        {
            calls++;
            return Task.FromResult(new TestData(1));
        });
        var upper = await _caller.GetOrAddAsync("Key", () =>
        {
            calls++;
            return Task.FromResult(new TestData(2));
        });

        Assert.Equal(2, calls);
        Assert.Equal(1, lower.Id);
        Assert.Equal(2, upper.Id);
    }

    [Fact]
    public async Task GetOrAddAsync_AfterCompletion_RunsFactoryAgain()
    {
        var first = new TestData(1);
        var second = new TestData(2);
        var calls = 0;

        var firstResult = await _caller.GetOrAddAsync("key", () =>
        {
            calls++;
            return Task.FromResult(first);
        });
        var secondResult = await _caller.GetOrAddAsync("key", () =>
        {
            calls++;
            return Task.FromResult(second);
        });

        Assert.Equal(2, calls);
        Assert.Same(first, firstResult);
        Assert.Same(second, secondResult);
    }

    [Fact]
    public async Task GetOrAddAsync_JoinersWithOtherFactories_UseTheFirstFactory()
    {
        var held = BlockedFactory.Hold<TestData>();
        var expected = new TestData(1);
        var secondCalls = 0;
        var thirdCalls = 0;

        var first = _caller.GetOrAddAsync("key", held.Factory);
        var second = _caller.GetOrAddAsync("key", () =>
        {
            Interlocked.Increment(ref secondCalls);
            return Task.FromResult(new TestData(2));
        });
        var third = _caller.GetOrAddAsync("key", () =>
        {
            Interlocked.Increment(ref thirdCalls);
            return Task.FromResult(new TestData(3));
        });

        held.Release.SetResult(expected);
        var results = await Task.WhenAll(first, second, third);

        Assert.Equal(1, held.Calls);
        Assert.Equal(0, secondCalls);
        Assert.Equal(0, thirdCalls);
        Assert.All(results, result => Assert.Same(expected, result));
    }

    [Fact]
    public async Task GetOrAddAsync_AlreadyCancelledToken_ReturnsFactoryInstance()
    {
        var expected = new TestData(1);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _caller.GetOrAddAsync("key", () => Task.FromResult(expected), cts.Token);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetOrAddAsync_OneWaiterCancels_OtherWaiterStillReceivesInstance()
    {
        var held = BlockedFactory.Hold<TestData>();
        var expected = new TestData(4);
        using var firstToken = new CancellationTokenSource();
        using var secondToken = new CancellationTokenSource();

        var first = _caller.GetOrAddAsync("key", held.Factory, firstToken.Token);
        var second = _caller.GetOrAddAsync("key", held.Factory, secondToken.Token);
        firstToken.Cancel();
        held.Release.SetResult(expected);

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, held.Calls);
        Assert.Same(expected, results[0]);
        Assert.Same(expected, results[1]);
    }
}
