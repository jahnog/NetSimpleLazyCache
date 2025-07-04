using System.Collections.Concurrent;
using System.Diagnostics;

namespace SimpleLazyCache.Unit.Tests;

public class SingleFactoryCallerTests
{
    private readonly SingleFactoryCaller<TestData> _singleFactoryCaller;

    public SingleFactoryCallerTests()
    {
        _singleFactoryCaller = new SingleFactoryCaller<TestData>();
    }

    #region Basic Functionality Tests

    [Fact]
    public async Task GetOrAddAsync_WithValidKey_ReturnsValue()
    {
        // Arrange
        const string key = "test-key";
        var expectedValue = new TestData { Id = 1, Name = "Test" };

        // Act
        var result = await _singleFactoryCaller.GetOrAddAsync(key, () => Task.FromResult(expectedValue));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedValue.Id, result.Id);
        Assert.Equal(expectedValue.Name, result.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetOrAddAsync_WithInvalidKey_ThrowsArgumentException(string invalidKey)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _singleFactoryCaller.GetOrAddAsync(invalidKey, () => Task.FromResult(new TestData())));

        Assert.Equal("Key cannot be null or empty. (Parameter 'key')", exception.Message);
    }

    #endregion

    #region Cache Stampede Tests

    [Fact]
    public async Task GetOrAddAsync_ConcurrentRequests_OnlyCallsFactoryOnce()
    {
        // Arrange
        const string key = "concurrent-key";
        var factoryCallCount = 0;
        var factoryDelay = TimeSpan.FromMilliseconds(100);
        var expectedValue = new TestData { Id = 42, Name = "Concurrent Test" };

        Func<Task<TestData>> factory = async () =>
        {
            Interlocked.Increment(ref factoryCallCount);
            await Task.Delay(factoryDelay);
            return expectedValue;
        };

        // Act - Start multiple concurrent requests
        const int concurrentRequests = 10;
        var tasks = new Task<TestData>[concurrentRequests];
        
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks[i] = _singleFactoryCaller.GetOrAddAsync(key, factory);
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(1, factoryCallCount); // Factory should only be called once
        Assert.All(results, result =>
        {
            Assert.NotNull(result);
            Assert.Equal(expectedValue.Id, result.Id);
            Assert.Equal(expectedValue.Name, result.Name);
        });
    }

    [Fact]
    public async Task GetOrAddAsync_HighConcurrency_PreventsCacheStampede()
    {
        // Arrange
        const string key = "high-concurrency-key";
        var factoryCallCount = 0;
        var factoryDelay = TimeSpan.FromMilliseconds(50);
        var expectedValue = new TestData { Id = 100, Name = "High Concurrency Test" };

        Func<Task<TestData>> factory = async () =>
        {
            Interlocked.Increment(ref factoryCallCount);
            await Task.Delay(factoryDelay);
            return expectedValue;
        };

        // Act - Start a large number of concurrent requests
        const int concurrentRequests = 100;
        var tasks = new Task<TestData>[concurrentRequests];
        
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks[i] = _singleFactoryCaller.GetOrAddAsync(key, factory);
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(1, factoryCallCount); // Factory should only be called once even with high concurrency
        Assert.All(results, result => Assert.NotNull(result));
        Assert.All(results, result => Assert.Equal(expectedValue.Id, result.Id));
    }

    #endregion

    #region Race Condition Tests

    [Fact]
    public async Task GetOrAddAsync_DifferentKeys_CallsFactoryForEachKey()
    {
        // Arrange
        var factoryCallCounts = new ConcurrentDictionary<string, int>();
        var factoryDelay = TimeSpan.FromMilliseconds(50);

        Func<string, Func<Task<TestData>>> createFactory = keyName => async () =>
        {
            factoryCallCounts.AddOrUpdate(keyName, 1, (k, v) => v + 1);
            await Task.Delay(factoryDelay);
            return new TestData { Id = keyName.GetHashCode(), Name = keyName };
        };

        // Act - Start concurrent requests with different keys
        const int numberOfKeys = 5;
        const int requestsPerKey = 10;
        var allTasks = new List<Task<TestData>>();

        for (int keyIndex = 0; keyIndex < numberOfKeys; keyIndex++)
        {
            var key = $"key-{keyIndex}";
            var factory = createFactory(key);
            
            for (int requestIndex = 0; requestIndex < requestsPerKey; requestIndex++)
            {
                allTasks.Add(_singleFactoryCaller.GetOrAddAsync(key, factory));
            }
        }

        var results = await Task.WhenAll(allTasks);

        // Assert
        Assert.Equal(numberOfKeys, factoryCallCounts.Count);
        Assert.All(factoryCallCounts.Values, count => Assert.Equal(1, count)); // Each factory should be called exactly once
        Assert.Equal(numberOfKeys * requestsPerKey, results.Length);
    }

    [Fact]
    public async Task GetOrAddAsync_RapidSequentialRequests_HandlesCorrectly()
    {
        // Arrange
        const string key = "rapid-sequential-key";
        var factoryCallCount = 0;
        var results = new List<TestData>();

        // Act - Make rapid sequential requests (not concurrent)
        for (int i = 0; i < 10; i++)
        {
            var result = await _singleFactoryCaller.GetOrAddAsync(key, async () =>
            {
                Interlocked.Increment(ref factoryCallCount);
                await Task.Delay(10);
                return new TestData { Id = i, Name = $"Sequential-{i}" };
            });
            results.Add(result);
        }

        // Assert
        Assert.Equal(10, factoryCallCount); // Each sequential call should invoke the factory
        Assert.Equal(10, results.Count);
        Assert.All(results, result => Assert.NotNull(result));
    }

    #endregion

    #region Memory Leak Tests

    [Fact]
    public async Task GetOrAddAsync_CompletedTasks_RemovesFromInternalDictionary()
    {
        // This test verifies that completed tasks are cleaned up to prevent memory leaks
        // We use reflection to access the private field for verification
        
        // Arrange
        var singleFactoryCaller = new SingleFactoryCaller<TestData>();
        var pendingTasksField = typeof(SingleFactoryCaller<TestData>)
            .GetField("_pendingTasks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(pendingTasksField);
        
        var pendingTasks = (ConcurrentDictionary<string, Lazy<Task<TestData>>>)pendingTasksField.GetValue(singleFactoryCaller)!;

        // Act
        const string key = "cleanup-test-key";
        var result = await singleFactoryCaller.GetOrAddAsync(key, () => 
            Task.FromResult(new TestData { Id = 1, Name = "Cleanup Test" }));

        // Assert
        Assert.NotNull(result);
        Assert.Empty(pendingTasks); // Dictionary should be empty after task completion
    }

    [Fact]
    public async Task GetOrAddAsync_MultipleKeys_CleansUpAllCompletedTasks()
    {
        // Arrange
        var singleFactoryCaller = new SingleFactoryCaller<TestData>();
        var pendingTasksField = typeof(SingleFactoryCaller<TestData>)
            .GetField("_pendingTasks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var pendingTasks = (ConcurrentDictionary<string, Lazy<Task<TestData>>>)pendingTasksField!.GetValue(singleFactoryCaller)!;

        // Act - Execute multiple tasks with different keys
        var tasks = new List<Task<TestData>>();
        for (int i = 0; i < 5; i++)
        {
            var key = $"cleanup-key-{i}";
            tasks.Add(singleFactoryCaller.GetOrAddAsync(key, () => 
                Task.FromResult(new TestData { Id = i, Name = $"Cleanup-{i}" })));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(5, results.Length);
        Assert.All(results, result => Assert.NotNull(result));
        Assert.Empty(pendingTasks); // All tasks should be cleaned up
    }

    [Fact]
    public async Task GetOrAddAsync_ExceptionInFactory_CleansUpTask()
    {
        // Arrange
        var singleFactoryCaller = new SingleFactoryCaller<TestData>();
        var pendingTasksField = typeof(SingleFactoryCaller<TestData>)
            .GetField("_pendingTasks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var pendingTasks = (ConcurrentDictionary<string, Lazy<Task<TestData>>>)pendingTasksField!.GetValue(singleFactoryCaller)!;

        const string key = "exception-test-key";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            singleFactoryCaller.GetOrAddAsync(key, () => 
                Task.FromException<TestData>(new InvalidOperationException("Test exception"))));

        // Assert cleanup occurred even after exception
        Assert.Empty(pendingTasks);
    }

    #endregion

    #region Performance and Stress Tests

    [Fact]
    public async Task GetOrAddAsync_StressTest_PerformsWithinReasonableTime()
    {
        // Arrange
        const int numberOfKeys = 100;
        const int requestsPerKey = 50;
        var factoryDelay = TimeSpan.FromMilliseconds(1);
        var allTasks = new List<Task<TestData>>();

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        for (int keyIndex = 0; keyIndex < numberOfKeys; keyIndex++)
        {
            var key = $"stress-key-{keyIndex}";
            
            for (int requestIndex = 0; requestIndex < requestsPerKey; requestIndex++)
            {
                allTasks.Add(_singleFactoryCaller.GetOrAddAsync(key, async () =>
                {
                    await Task.Delay(factoryDelay);
                    return new TestData { Id = keyIndex, Name = $"Stress-{keyIndex}" };
                }));
            }
        }

        var results = await Task.WhenAll(allTasks);
        stopwatch.Stop();

        // Assert
        Assert.Equal(numberOfKeys * requestsPerKey, results.Length);
        Assert.All(results, result => Assert.NotNull(result));
        
        // Performance assertion - should complete within reasonable time
        // This is a rough estimate and may need adjustment based on hardware
        var maxExpectedTime = TimeSpan.FromSeconds(numberOfKeys * factoryDelay.TotalSeconds * 1.5); // 50% buffer
        Assert.True(stopwatch.Elapsed < maxExpectedTime, 
            $"Stress test took too long: {stopwatch.Elapsed} > {maxExpectedTime}");
    }

    [Fact]
    public async Task GetOrAddAsync_LongRunningFactory_HandlesTimeout()
    {
        // Arrange
        const string key = "long-running-key";
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        
        // Act & Assert
        var task = _singleFactoryCaller.GetOrAddAsync(key, async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationTokenSource.Token);
            return new TestData { Id = 1, Name = "Long Running" };
        });

        // The task should handle cancellation appropriately
        await Assert.ThrowsAsync<TaskCanceledException>(() => task);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetOrAddAsync_FactoryReturnsNull_HandlesGracefully()
    {
        // Arrange
        const string key = "null-result-key";

        // Act
        var result = await _singleFactoryCaller.GetOrAddAsync(key, () => Task.FromResult<TestData>(null!));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetOrAddAsync_SameKeyDifferentFactories_UsesFirstFactory()
    {
        // Arrange
        const string key = "same-key-test";
        var firstFactoryCalled = false;
        var secondFactoryCalled = false;

        Func<Task<TestData>> firstFactory = async () =>
        {
            firstFactoryCalled = true;
            await Task.Delay(50);
            return new TestData { Id = 1, Name = "First Factory" };
        };

        Func<Task<TestData>> secondFactory = async () =>
        {
            secondFactoryCalled = true;
            await Task.Delay(50);
            return new TestData { Id = 2, Name = "Second Factory" };
        };

        // Act - Start both tasks concurrently
        var task1 = _singleFactoryCaller.GetOrAddAsync(key, firstFactory);
        var task2 = _singleFactoryCaller.GetOrAddAsync(key, secondFactory);

        var results = await Task.WhenAll(task1, task2);

        // Assert
        Assert.True(firstFactoryCalled);
        Assert.False(secondFactoryCalled); // Second factory should not be called
        Assert.Equal(results[0].Id, results[1].Id); // Both should return the same result
        Assert.Equal("First Factory", results[0].Name);
        Assert.Equal("First Factory", results[1].Name);
    }

    #endregion
}

// Test data class
public class TestData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}