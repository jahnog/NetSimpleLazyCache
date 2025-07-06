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

    #region Exception Handling Analysis Tests

    [Fact]
    public async Task ExceptionProblem1_ConcurrentExceptionThenImmediateRetry_RaceCondition()
    {
        // This test demonstrates a potential race condition when an exception occurs
        // and immediately after, another request comes in for the same key
        
        var cache = new SingleFactoryCaller<TestData>();
        var firstCallStarted = new TaskCompletionSource<bool>();
        var proceedWithException = new TaskCompletionSource<bool>();
        
        var key = "race-condition-key";
        
        // Start a request that will fail
        var failingTask = Task.Run(async () =>
        {
            return await cache.GetOrAddAsync(key, async () =>
            {
                firstCallStarted.SetResult(true);
                await proceedWithException.Task; // Wait for signal
                throw new InvalidOperationException("Simulated failure");
            });
        });
        
        // Wait for the first call to start
        await firstCallStarted.Task;
        
        // Now start a second request that should succeed
        var successTask = Task.Run(async () =>
        {
            // Small delay to ensure timing
            await Task.Delay(10);
            return await cache.GetOrAddAsync(key, () => Task.FromResult(new TestData { Id = 1, Name = "success" }));
        });
        
        // Allow the first request to fail
        proceedWithException.SetResult(true);
        
        // The failing task should throw
        await Assert.ThrowsAsync<InvalidOperationException>(() => failingTask);
        
        // The success task should complete successfully
        var result = await successTask;
        Assert.Equal("success", result.Name);
    }
    
    [Fact]
    public async Task ExceptionProblem2_FactoryThrowsSynchronously_HandledCorrectly()
    {
        // This demonstrates what happens when the factory itself throws synchronously
        // before returning a Task
        
        var key = "sync-exception-key";
        
        // Factory that throws immediately (not in a Task)
        Func<Task<TestData>> faultyFactory = () =>
        {
            throw new InvalidOperationException("Synchronous exception in factory");
        };
        
        // This should still propagate the exception correctly
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _singleFactoryCaller.GetOrAddAsync(key, faultyFactory));
    }
    
    [Fact]
    public async Task ExceptionProblem3_TaskFactoryExceptionInLazy_OnlyCalledOnce()
    {
        // This shows what happens when the Lazy<Task<T>> itself throws
        // during evaluation. We need true concurrency at the GetOrAdd level.
        
        var key = "lazy-exception-key";
        var factoryCallCount = 0;
        var factoryEntered = new TaskCompletionSource<bool>();
        var continueFactory = new TaskCompletionSource<bool>();
        
        Func<Task<TestData>> faultyFactory = async () =>
        {
            Interlocked.Increment(ref factoryCallCount);
            factoryEntered.SetResult(true);
            await continueFactory.Task; // Block until we say continue
            throw new InvalidOperationException("Exception during Lazy evaluation");
        };
        
        // Start first request
        var task1 = _singleFactoryCaller.GetOrAddAsync(key, faultyFactory);
        
        // Wait for factory to start executing
        await factoryEntered.Task;
        
        // Start second request while first is blocked in factory
        var task2 = _singleFactoryCaller.GetOrAddAsync(key, faultyFactory);
        
        // Now let the factory complete and fail
        continueFactory.SetResult(true);
        
        // Both should fail
        await Assert.ThrowsAsync<InvalidOperationException>(() => task1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => task2);
        
        // Factory should only be called once due to Lazy<T>
        Assert.Equal(1, factoryCallCount);
    }
    
    [Fact]
    public async Task ExceptionProblem4_CancellationInFactory_CleansUpProperly()
    {
        // This tests behavior with cancellation tokens in the factory
        
        var cache = new SingleFactoryCaller<TestData>();
        var key = "cancellation-test";
        
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100); // Cancel after 100ms
        
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            cache.GetOrAddAsync(key, async () =>
            {
                await Task.Delay(1000, cts.Token); // This will be cancelled
                return new TestData { Id = 1, Name = "success" };
            }));
        
        // Verify cleanup happened by accessing internal state
        var pendingTasksField = typeof(SingleFactoryCaller<TestData>)
            .GetField("_pendingTasks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var pendingTasks = (ConcurrentDictionary<string, Lazy<Task<TestData>>>)pendingTasksField!.GetValue(cache)!;
        Assert.Empty(pendingTasks);
    }
    
    [Fact]
    public async Task ExceptionProblem5_UnobservedTaskException_DoesNotPreventCleanup()
    {
        // This test verifies that unobserved task exceptions don't prevent cleanup
        
        var cache = new SingleFactoryCaller<TestData>();
        var key = "unobserved-exception-key";
        var factoryExecuted = false;
        
        // Create a task that will have an unobserved exception
        _ = cache.GetOrAddAsync(key, async () =>
        {
            factoryExecuted = true;
            await Task.Delay(10);
            throw new InvalidOperationException("Unobserved exception");
        });
        
        // Wait a bit for the factory to execute
        await Task.Delay(200);
        Assert.True(factoryExecuted);
        
        // Verify the cache was cleaned up
        var pendingTasksField = typeof(SingleFactoryCaller<TestData>)
            .GetField("_pendingTasks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var pendingTasks = (ConcurrentDictionary<string, Lazy<Task<TestData>>>)pendingTasksField!.GetValue(cache)!;
        Assert.Empty(pendingTasks);
    }

    #endregion

    #region Exception Handling Problem Demonstration

    [Fact]
    public async Task GetOrAddAsync_ConcurrentRequestsWithFactoryException_AllReceiveSameException()
    {
        // This test demonstrates that concurrent requests all receive the same exception
        // which is actually the correct behavior for a cache-stampede prevention mechanism
        
        var factoryCallCount = 0;
        const string key = "exception-demonstration-key";

        Func<Task<TestData>> faultyFactory = async () =>
        {
            Interlocked.Increment(ref factoryCallCount);
            await Task.Delay(100); // Simulate some work
            throw new InvalidOperationException("Factory intentionally failed");
        };

        // Start multiple concurrent requests
        var task1 = _singleFactoryCaller.GetOrAddAsync(key, faultyFactory);
        var task2 = _singleFactoryCaller.GetOrAddAsync(key, faultyFactory);
        var task3 = _singleFactoryCaller.GetOrAddAsync(key, faultyFactory);

        // All should fail with the same exception
        await Assert.ThrowsAsync<InvalidOperationException>(() => task1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => task2);
        await Assert.ThrowsAsync<InvalidOperationException>(() => task3);

        // The factory should only be called once (this prevents cache stampede)
        Assert.Equal(1, factoryCallCount);
    }

    [Fact]
    public async Task GetOrAddAsync_ExceptionThenSuccess_WorksCorrectly()
    {
        // This test demonstrates that after an exception, subsequent calls work correctly
        
        var factoryCallCount = 0;
        const string key = "exception-then-success-key";

        // First call should fail
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _singleFactoryCaller.GetOrAddAsync(key, async () =>
            {
                Interlocked.Increment(ref factoryCallCount);
                await Task.Delay(50);
                throw new InvalidOperationException("First call failed");
            }));

        // Second call should succeed
        var result = await _singleFactoryCaller.GetOrAddAsync(key, async () =>
        {
            Interlocked.Increment(ref factoryCallCount);
            await Task.Delay(50);
            return new TestData { Id = 1, Name = "Success after failure" };
        });

        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("Success after failure", result.Name);
        Assert.Equal(2, factoryCallCount); // Both factory calls should have executed
    }

    #endregion

    #region Critical Problem Demonstration

    [Fact]
    public async Task CriticalProblem_LazyTaskExceptionNotPropagatedToAllWaiters()
    {
        // This test demonstrates a critical issue: when a Lazy<Task<T>> fails,
        // concurrent waiters should all receive the same exception, but there's
        // a potential issue with the cleanup timing
        
        var cache = new SingleFactoryCaller<TestData>();
        var key = "critical-exception-key";
        var factoryStarted = new TaskCompletionSource<bool>();
        var allowFactoryToFail = new TaskCompletionSource<bool>();
        var factoryCallCount = 0;
        
        Func<Task<TestData>> faultyFactory = async () =>
        {
            Interlocked.Increment(ref factoryCallCount);
            factoryStarted.SetResult(true);
            await allowFactoryToFail.Task;
            throw new InvalidOperationException("Critical test exception");
        };
        
        // Start the first request
        var task1 = cache.GetOrAddAsync(key, faultyFactory);
        
        // Wait for factory to start
        await factoryStarted.Task;
        
        // Start second request while first is still running
        var task2 = cache.GetOrAddAsync(key, faultyFactory);
        
        // Allow factory to fail
        allowFactoryToFail.SetResult(true);
        
        // Both tasks should fail with the same exception
        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => task1);
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => task2);
        
        Assert.Equal("Critical test exception", ex1.Message);
        Assert.Equal("Critical test exception", ex2.Message);
        Assert.Equal(1, factoryCallCount); // Factory should only be called once
    }
    
    [Fact] 
    public async Task EdgeCase_VeryFastExceptionVsCleanup()
    {
        // This test tries to find race conditions between exception propagation and cleanup
        var cache = new SingleFactoryCaller<TestData>();
        
        for (int i = 0; i < 100; i++)
        {
            var key = $"fast-exception-{i}";
            
            // Very fast failing factory
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                cache.GetOrAddAsync(key, () => 
                    Task.FromException<TestData>(new InvalidOperationException($"Fast exception {i}"))));
            
            // Verify cleanup
            var pendingTasksField = typeof(SingleFactoryCaller<TestData>)
                .GetField("_pendingTasks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var pendingTasks = (ConcurrentDictionary<string, Lazy<Task<TestData>>>)pendingTasksField!.GetValue(cache)!;
            
            Assert.Empty(pendingTasks);
        }
    }

    #endregion
}

// Test data class
public class TestData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}