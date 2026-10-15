namespace SimpleLazyCache.Unit.Tests;

public class SingleFactoryCallerLoadTests
{
    private readonly SingleFactoryCaller<TestData> _caller = new SingleFactoryCaller<TestData>();

    [Fact(Timeout = 10000)]
    public async Task GetOrAddAsync_ParallelKeys_StaySingleFlightAndThenRunAgain()
    {
        const int keys = 8;
        const int workers = 8;
        const int rounds = 4;
        var depth = new int[keys];
        var maxDepth = new int[keys];
        var tasks = new Task[workers];

        for (var worker = 0; worker < workers; worker++)
        {
            var workerId = worker;
            tasks[worker] = Task.Run(async () =>
            {
                for (var round = 0; round < rounds; round++)
                {
                    var keyId = (workerId + round) % keys;
                    var result = await _caller.GetOrAddAsync(keyId.ToString(), async () =>
                    {
                        var now = Interlocked.Increment(ref depth[keyId]);
                        try
                        {
                            int observed;
                            do
                            {
                                observed = Volatile.Read(ref maxDepth[keyId]);
                            }
                            while (now > observed &&
                                   Interlocked.CompareExchange(ref maxDepth[keyId], now, observed) != observed);

                            await Task.Yield();
                            return new TestData(keyId);
                        }
                        finally
                        {
                            Interlocked.Decrement(ref depth[keyId]);
                        }
                    });

                    Assert.Equal(keyId, result.Id);
                }
            });
        }

        await Task.WhenAll(tasks);

        for (var key = 0; key < keys; key++)
            Assert.Equal(1, maxDepth[key]);

        var followUps = 0;
        for (var key = 0; key < keys; key++)
        {
            var keyId = key;
            var result = await _caller.GetOrAddAsync(keyId.ToString(), () =>
            {
                Interlocked.Increment(ref followUps);
                return Task.FromResult(new TestData(keyId));
            });
            Assert.Equal(keyId, result.Id);
        }

        Assert.Equal(keys, followUps);
    }

    [Fact]
    public async Task GetOrAddAsync_FailedStampede_NextStampedeCollapses()
    {
        var failure = BlockedFactory.Hold<TestData>();
        var expectedException = new InvalidOperationException("down");
        var failureTasks = new Task<TestData>[32];

        for (var i = 0; i < failureTasks.Length; i++)
            failureTasks[i] = _caller.GetOrAddAsync("hot", failure.Factory);

        failure.Release.SetException(expectedException);

        var thrown = new InvalidOperationException[failureTasks.Length];
        for (var i = 0; i < failureTasks.Length; i++)
            thrown[i] = await Assert.ThrowsAsync<InvalidOperationException>(() => failureTasks[i]);

        Assert.Equal(1, failure.Calls);
        Assert.All(thrown, exception => Assert.Same(expectedException, exception));

        var success = BlockedFactory.Hold<TestData>();
        var expected = new TestData(9);
        var successTasks = new Task<TestData>[32];

        for (var i = 0; i < successTasks.Length; i++)
            successTasks[i] = _caller.GetOrAddAsync("hot", success.Factory);

        success.Release.SetResult(expected);
        var results = await Task.WhenAll(successTasks);

        Assert.Equal(1, success.Calls);
        Assert.All(results, result => Assert.Same(expected, result));
    }

    [Fact(Timeout = 10000)]
    public void GetOrAddAsync_BlockedOnSyncContext_Completes()
    {
        var held = BlockedFactory.Hold<TestData>();
        var expected = new TestData(3);
        var context = new RecordingSyncContext();
        using var returned = new ManualResetEventSlim(false);
        TestData? result = null;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                var task = _caller.GetOrAddAsync("key", held.Factory);
                returned.Set();
                // Block on purpose. This is the sync-over-async case the library must survive.
#pragma warning disable xUnit1031
                result = task.GetAwaiter().GetResult();
#pragma warning restore xUnit1031
            }
            catch (Exception ex)
            {
                error = ex;
                returned.Set();
            }
        });
        thread.IsBackground = true;
        thread.Start();

        Assert.True(
            returned.Wait(TimeSpan.FromSeconds(5)),
            "Caller did not reach the wait. Posts=" + context.Posts + ".");
        held.Release.SetResult(expected);
        Assert.True(
            thread.Join(TimeSpan.FromSeconds(5)),
            "Caller did not finish. Posts=" + context.Posts + ".");
        Assert.Null(error);
        Assert.Same(expected, result);
    }

    private sealed class RecordingSyncContext : SynchronizationContext
    {
        private int _posts;

        public int Posts => Volatile.Read(ref _posts);

        public override void Post(SendOrPostCallback d, object? state) =>
            Interlocked.Increment(ref _posts);

        public override void Send(SendOrPostCallback d, object? state) =>
            Interlocked.Increment(ref _posts);
    }
}
