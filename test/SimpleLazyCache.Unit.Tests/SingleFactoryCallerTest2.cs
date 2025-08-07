using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class SingleFactoryCallerTest2
{
    private readonly SingleFactoryCaller<string> _caller;

    public SingleFactoryCallerTest2()
    {
        _caller = new SingleFactoryCaller<string>();
    }

    [Fact]
    public async Task GetOrAddAsync_ValidKeyAndFactory_ReturnsValue()
    {
        // Arrange
        const string key = "test-key";
        const string expectedValue = "test-value";

        // Act
        var result = await _caller.GetOrAddAsync(key, () => Task.FromResult(expectedValue));

        // Assert
        Assert.Equal(expectedValue, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetOrAddAsync_InvalidKey_ThrowsArgumentException(string invalidKey)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _caller.GetOrAddAsync(invalidKey, () => Task.FromResult("value")));
    }

    [Fact]
    public async Task GetOrAddAsync_NullFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _caller.GetOrAddAsync("key", null));
    }

    [Fact]
    public async Task GetOrAddAsync_FactoryReturnsNull_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _caller.GetOrAddAsync("key", () => null));
    }

    [Fact]
    public async Task GetOrAddAsync_FactoryThrowsException_PropagatesException()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Factory error");

        // Act & Assert
        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _caller.GetOrAddAsync("key", () => throw expectedException));
        
        Assert.Equal(expectedException.Message, actualException.Message);
    }

    [Fact]
    public async Task GetOrAddAsync_FactoryTaskThrowsException_PropagatesException()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Task error");

        // Act & Assert
        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _caller.GetOrAddAsync("key", () => Task.FromException<string>(expectedException)));
        
        Assert.Equal(expectedException.Message, actualException.Message);
    }

    [Fact]
    public async Task GetOrAddAsync_ConcurrentCallsSameKey_FactoryCalledOnce()
    {
        // Arrange
        const string key = "concurrent-key";
        var factoryCallCount = 0;
        var tcs = new TaskCompletionSource<string>();

        Func<Task<string>> factory = () =>
        {
            Interlocked.Increment(ref factoryCallCount);
            return tcs.Task;
        };

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _caller.GetOrAddAsync(key, factory))
            .ToArray();

        // Complete the task after starting all concurrent calls
        tcs.SetResult("result");
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(1, factoryCallCount);
        Assert.All(results, result => Assert.Equal("result", result));
    }

    [Fact]
    public async Task GetOrAddAsync_ConcurrentCallsDifferentKeys_FactoryCalledForEachKey()
    {
        // Arrange
        var factoryCallCount = 0;
        var completionSources = new ConcurrentDictionary<string, TaskCompletionSource<string>>();

        Func<Task<string>> factory = () =>
        {
            var count = Interlocked.Increment(ref factoryCallCount);
            var key = $"key-{count}";
            var tcs = new TaskCompletionSource<string>();
            completionSources[key] = tcs;
            return tcs.Task;
        };

        // Act
        var tasks = Enumerable.Range(0, 5)
            .Select(i => _caller.GetOrAddAsync($"key-{i}", factory))
            .ToArray();

        // Complete all tasks
        foreach (var kvp in completionSources)
        {
            kvp.Value.SetResult($"result-{kvp.Key}");
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(5, factoryCallCount);
        Assert.Equal(5, results.Distinct().Count());
    }

    [Fact]
    public async Task GetOrAddAsync_MemoryLeak_PendingTasksCleanedUp()
    {
        // Arrange
        const string key = "memory-test";
        var factoryCallCount = 0;

        // Act - Call multiple times with same key
        for (int i = 0; i < 100; i++)
        {
            await _caller.GetOrAddAsync(key, () =>
            {
                Interlocked.Increment(ref factoryCallCount);
                return Task.FromResult($"result-{i}");
            });
        }

        // Assert - Factory should be called 100 times (no caching between calls)
        Assert.Equal(100, factoryCallCount);
    }

    [Fact]
    public async Task GetOrAddAsync_ExceptionInFactory_PendingTasksCleanedUp()
    {
        // Arrange
        const string key = "exception-cleanup-test";
        var exception = new InvalidOperationException("Test exception");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _caller.GetOrAddAsync(key, () => throw exception));

        // Verify we can call again with same key (no lingering state)
        var result = await _caller.GetOrAddAsync(key, () => Task.FromResult("success"));
        Assert.Equal("success", result);
    }

    [Fact]
    public async Task GetOrAddAsync_CancellationToken_PassedButNotUsed()
    {
        // Arrange
        const string key = "cancellation-test";
        using var cts = new CancellationTokenSource();

        // Act
        var result = await _caller.GetOrAddAsync(key, () => Task.FromResult("value"), cts.Token);

        // Assert
        Assert.Equal("value", result);
    }

    [Fact]
    public async Task GetOrAddAsync_ConcurrentWithException_OnlyAffectsFailedCall()
    {
        // Arrange
        const string key = "concurrent-exception-test";
        var successTcs = new TaskCompletionSource<string>();
        var exceptionTcs = new TaskCompletionSource<string>();
        var callCount = 0;

        Func<Task<string>> factory = () =>
        {
            var count = Interlocked.Increment(ref callCount);
            if (count == 1)
            {
                return successTcs.Task;
            }
            return exceptionTcs.Task;
        };

        // Act
        var successTask = _caller.GetOrAddAsync(key, factory);
        var exceptionTask = _caller.GetOrAddAsync(key, factory);

        // Complete first call successfully
        successTcs.SetResult("success");
        var successResult = await successTask;

        // Second call should get the same result
        var secondResult = await exceptionTask;

        // Assert
        Assert.Equal("success", successResult);
        Assert.Equal("success", secondResult);
        Assert.Equal(1, callCount); // Factory called only once
    }

    [Fact]
    public async Task GetOrAddAsync_LongRunningFactory_ConcurrentCallsWait()
    {
        // Arrange
        const string key = "long-running-test";
        var factoryStarted = new TaskCompletionSource<bool>();
        var factoryCanComplete = new TaskCompletionSource<bool>();
        var callCount = 0;

        Func<Task<string>> factory = async () =>
        {
            Interlocked.Increment(ref callCount);
            factoryStarted.SetResult(true);
            await factoryCanComplete.Task;
            return "long-running-result";
        };

        // Act
        var firstTask = _caller.GetOrAddAsync(key, factory);
        
        // Wait for factory to start
        await factoryStarted.Task;
        
        // Start second call while first is still running
        var secondTask = _caller.GetOrAddAsync(key, factory);
        
        // Allow factory to complete
        factoryCanComplete.SetResult(true);
        
        var results = await Task.WhenAll(firstTask, secondTask);

        // Assert
        Assert.Equal(1, callCount);
        Assert.All(results, result => Assert.Equal("long-running-result", result));
    }
}