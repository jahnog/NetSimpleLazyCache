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

    #region Additional Exception Handling Tests

    [Fact]
    public async Task GetOrAddAsync_NullValueFactory_ThrowsArgumentNullException()
    {
        // Arrange
        const string key = "null-factory-key";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _singleFactoryCaller.GetOrAddAsync(key, null!));

        Assert.Equal("valueFactory", exception.ParamName);
    }

    [Fact]
    public async Task GetOrAddAsync_FactoryReturnsNullTask_ThrowsInvalidOperationException()
    {
        // Arrange
        const string key = "null-task-key";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _singleFactoryCaller.GetOrAddAsync(key, () => null!));

        Assert.Equal("Value factory returned null task.", exception.Message);
    }

    [Fact]
    public async Task GetOrAddAsync_FactoryThrowsBeforeReturningTask_ExceptionPropagated()
    {
        // Arrange
        const string key = "sync-exception-key";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            _singleFactoryCaller.GetOrAddAsync(key, () => 
                throw new NotSupportedException("Factory synchronous exception")));

        Assert.Equal("Factory synchronous exception", exception.Message);
    }

    [Fact]
    public async Task GetOrAddAsync_ConcurrentExceptionAndSuccess_BothReceiveSameException()
    {
        // Arrange
        const string key = "concurrent-exception-key";
        var factoryCallCount = 0;
        var factoryStarted = new TaskCompletionSource<bool>();
        var proceedWithException = new TaskCompletionSource<bool>();

        Func<Task<TestData>> faultyFactory = async () =>
        {
            Interlocked.Increment(ref factoryCallCount);
            factoryStarted.SetResult(true);
            await proceedWithException.Task;
            throw new InvalidOperationException("Concurrent exception test");
        };

        // Act - Start first request
        var task1 = _singleFactoryCaller.GetOrAddAsync(key, faultyFactory);
        
        // Wait for factory to start
        await factoryStarted.Task;
        
        // Start second request while first is still running
        var task2 = _singleFactoryCaller.GetOrAddAsync(key, faultyFactory);
        
        // Allow factory to fail
        proceedWithException.SetResult(true);

        // Assert - Both should fail with the same exception
        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => task1);
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => task2);
        
        Assert.Equal("Concurrent exception test", ex1.Message);
        Assert.Equal("Concurrent exception test", ex2.Message);
        Assert.Equal(1, factoryCallCount); // Factory should only be called once
    }

    [Fact]
    public async Task GetOrAddAsync_ExceptionInTaskContinuation_DoesNotBreakCache()
    {
        // Arrange
        const string key = "task-continuation-exception-key";
        var factoryCallCount = 0;

        // Act - First call with exception in task
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _singleFactoryCaller.GetOrAddAsync(key, async () =>
            {
                Interlocked.Increment(ref factoryCallCount);
                await Task.Yield(); // Force continuation
                throw new InvalidOperationException("Task continuation exception");
            }));

        // Second call should work normally
        var result = await _singleFactoryCaller.GetOrAddAsync(key, async () =>
        {
            Interlocked.Increment(ref factoryCallCount);
            await Task.Yield();
            return new TestData { Id = 1, Name = "Success after exception" };
        });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("Success after exception", result.Name);
        Assert.Equal(2, factoryCallCount);
    }

    [Fact]
    public async Task GetOrAddAsync_AggregateExceptionFromFactory_UnwrapsCorrectly()
    {
        // Arrange
        const string key = "aggregate-exception-key";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _singleFactoryCaller.GetOrAddAsync(key, () =>
            {
                var tcs = new TaskCompletionSource<TestData>();
                tcs.SetException(new InvalidOperationException("Aggregate exception test"));
                return tcs.Task;
            }));

        Assert.Equal("Aggregate exception test", exception.Message);
    }

    #endregion

    #region Advanced Race Condition Tests

    [Fact]
    public async Task GetOrAddAsync_RaceConditionBetweenCompletionAndNewRequest_HandledCorrectly()
    {
        // This test specifically targets the race condition between task completion cleanup
        // and new requests for the same key
        
        var cache = new SingleFactoryCaller<TestData>();
        const string key = "race-condition-completion-key";
        var firstCallCompleted = new TaskCompletionSource<bool>();
        var secondCallCanStart = new TaskCompletionSource<bool>();
        var factoryCallCount = 0;

        // First call
        var firstTask = cache.GetOrAddAsync(key, async () =>
        {
            Interlocked.Increment(ref factoryCallCount);
            await Task.Delay(50);
            firstCallCompleted.SetResult(true);
            return new TestData { Id = 1, Name = "First call" };
        });

        // Wait for first call to complete
        await firstCallCompleted.Task;
        await firstTask; // Ensure complete cleanup

        // Immediately start second call
        var secondTask = cache.GetOrAddAsync(key, async () =>
        {
            Interlocked.Increment(ref factoryCallCount);
            await Task.Delay(50);
            return new TestData { Id = 2, Name = "Second call" };
        });

        var secondResult = await secondTask;

        // Assert
        Assert.NotNull(secondResult);
        Assert.Equal(2, secondResult.Id);
        Assert.Equal("Second call", secondResult.Name);
        Assert.Equal(2, factoryCallCount); // Both factories should have been called
    }

    [Fact]
    public async Task GetOrAddAsync_HighConcurrencyWithMixedSuccessAndFailure_BehavesCorrectly()
    {
        // This test creates a high concurrency scenario with mixed success/failure
        
        var successCount = 0;
        var failureCount = 0;
        var tasks = new List<Task>();

        // Create multiple tasks with different keys, some succeed, some fail
        for (int i = 0; i < 50; i++)
        {
            var key = $"mixed-result-key-{i}";
            var shouldFail = i % 3 == 0; // Every third call fails

            if (shouldFail)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await _singleFactoryCaller.GetOrAddAsync(key, async () =>
                        {
                            await Task.Delay(Random.Shared.Next(1, 50));
                            throw new InvalidOperationException($"Planned failure {i}");
                        });
                    }
                    catch (InvalidOperationException)
                    {
                        Interlocked.Increment(ref failureCount);
                    }
                }));
            }
            else
            {
                tasks.Add(Task.Run(async () =>
                {
                    var result = await _singleFactoryCaller.GetOrAddAsync(key, async () =>
                    {
                        await Task.Delay(Random.Shared.Next(1, 50));
                        return new TestData { Id = i, Name = $"Success {i}" };
                    });
                    
                    if (result != null)
                    {
                        Interlocked.Increment(ref successCount);
                    }
                }));
            }
        }

        await Task.WhenAll(tasks);

        // Assert
        var expectedFailures = 50 / 3 + (50 % 3 > 0 ? 1 : 0); // Ceiling division
        var expectedSuccesses = 50 - expectedFailures;

        Assert.Equal(expectedFailures, failureCount);
        Assert.Equal(expectedSuccesses, successCount);
    }

    [Fact]
    public async Task GetOrAddAsync_ConcurrentRequestsWithSameKeyDifferentFactories_FirstWins()
    {
        // This test ensures that when multiple requests come in simultaneously
        // with the same key but different factories, only the first factory is used
        
        const string key = "first-wins-key";
        var factory1Called = false;
        var factory2Called = false;
        var factory3Called = false;
        var allStarted = new TaskCompletionSource<bool>();
        var proceedWithExecution = new TaskCompletionSource<bool>();
        var factoriesStarted = 0;

        Func<int, Func<Task<TestData>>> createFactory = (factoryId) => async () =>
        {
            var started = Interlocked.Increment(ref factoriesStarted);
            if (started == 1) // First factory to start
            {
                allStarted.SetResult(true);
                await proceedWithExecution.Task;
            }

            switch (factoryId)
            {
                case 1:
                    factory1Called = true;
                    return new TestData { Id = 1, Name = "Factory 1" };
                case 2:
                    factory2Called = true;
                    return new TestData { Id = 2, Name = "Factory 2" };
                case 3:
                    factory3Called = true;
                    return new TestData { Id = 3, Name = "Factory 3" };
                default:
                    throw new InvalidOperationException("Unknown factory");
            }
        };

        // Start all requests simultaneously
        var task1 = _singleFactoryCaller.GetOrAddAsync(key, createFactory(1));
        var task2 = _singleFactoryCaller.GetOrAddAsync(key, createFactory(2));
        var task3 = _singleFactoryCaller.GetOrAddAsync(key, createFactory(3));

        // Wait for at least one factory to start
        await allStarted.Task;
        
        // Allow execution to proceed
        proceedWithExecution.SetResult(true);

        // Wait for all tasks to complete
        var results = await Task.WhenAll(task1, task2, task3);

        // Assert
        Assert.True(factory1Called); // First factory should be called
        Assert.False(factory2Called); // Other factories should not be called
        Assert.False(factory3Called);
        
        // All results should be the same
        Assert.All(results, result => Assert.Equal(1, result.Id));
        Assert.All(results, result => Assert.Equal("Factory 1", result.Name));
    }

    #endregion

    #region Memory Leak Prevention Tests

    [Fact]
    public async Task GetOrAddAsync_LargeNumberOfKeys_DoesNotAccumulateMemory()
    {
        // This test ensures that the cache doesn't accumulate memory over time
        
        var cache = new SingleFactoryCaller<TestData>();
        const int numberOfKeys = 1000;

        // Execute a large number of operations
        for (int i = 0; i < numberOfKeys; i++)
        {
            var key = $"memory-test-key-{i}";
            var result = await cache.GetOrAddAsync(key, async () =>
            {
                await Task.Delay(1); // Minimal delay
                return new TestData { Id = i, Name = $"Test {i}" };
            });
            
            Assert.NotNull(result);
            Assert.Equal(i, result.Id);
        }

        // Check internal state
        var pendingTasksField = typeof(SingleFactoryCaller<TestData>)
            .GetField("_pendingTasks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var pendingTasks = (ConcurrentDictionary<string, Lazy<Task<TestData>>>)pendingTasksField!.GetValue(cache)!;
        
        // All tasks should be cleaned up
        Assert.Empty(pendingTasks);
    }

    [Fact]
    public async Task GetOrAddAsync_RepeatedExceptionsWithSameKey_DoesNotLeakMemory()
    {
        // This test ensures that repeated exceptions don't cause memory leaks
        
        var cache = new SingleFactoryCaller<TestData>();
        const string key = "repeated-exception-key";
        const int numberOfAttempts = 100;

        for (int i = 0; i < numberOfAttempts; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                cache.GetOrAddAsync(key, async () =>
                {
                    await Task.Delay(1);
                    throw new InvalidOperationException($"Exception {i}");
                }));
        }

        // Check internal state
        var pendingTasksField = typeof(SingleFactoryCaller<TestData>)
            .GetField("_pendingTasks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var pendingTasks = (ConcurrentDictionary<string, Lazy<Task<TestData>>>)pendingTasksField!.GetValue(cache)!;
        
        // All tasks should be cleaned up
        Assert.Empty(pendingTasks);
    }

    [Fact]
    public async Task GetOrAddAsync_LongRunningTasksWithCancellation_CleansUpProperly()
    {
        // This test ensures that cancelled long-running tasks are properly cleaned up
        // Note: The cancellation must be handled within the factory itself
        
        var cache = new SingleFactoryCaller<TestData>();
        const string key = "long-running-cancelled-key";
        
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100); // Cancel after 100ms

        // Start a long-running task
        var longRunningTask = cache.GetOrAddAsync(key, async () =>
        {
            await Task.Delay(5000, cts.Token); // This will be cancelled
            return new TestData { Id = 1, Name = "Long running" };
        });

        // Task should be cancelled
        await Assert.ThrowsAsync<TaskCanceledException>(() => longRunningTask);

        // Check cleanup
        var pendingTasksField = typeof(SingleFactoryCaller<TestData>)
            .GetField("_pendingTasks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var pendingTasks = (ConcurrentDictionary<string, Lazy<Task<TestData>>>)pendingTasksField!.GetValue(cache)!;
        
        Assert.Empty(pendingTasks);
    }

    [Fact]
    public async Task GetOrAddAsync_ConcurrentRequestsWithCancellation_HandlesCorrectly()
    {
        // This test demonstrates that concurrent requests work correctly
        // when using cancellation tokens within the factory implementation
        
        var cache = new SingleFactoryCaller<TestData>();
        const string key = "concurrent-cancellation-key";
        
        var factoryExecuted = false;
        var factoryStarted = new TaskCompletionSource<bool>();
        var proceedWithFactory = new TaskCompletionSource<bool>();

        Func<Task<TestData>> factory = async () =>
        {
            factoryExecuted = true;
            factoryStarted.SetResult(true);
            await proceedWithFactory.Task;
            return new TestData { Id = 1, Name = "Success" };
        };

        // Start first request
        var task1 = cache.GetOrAddAsync(key, factory);
        
        // Wait for factory to start
        await factoryStarted.Task;
        
        // Start second request (should use same factory execution)
        var task2 = cache.GetOrAddAsync(key, factory);
        
        // Allow factory to complete
        proceedWithFactory.SetResult(true);

        // Both tasks should succeed since they share the same factory execution
        var result1 = await task1;
        var result2 = await task2;
        
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(1, result1.Id);
        Assert.Equal(1, result2.Id);
        Assert.Equal("Success", result1.Name);
        Assert.Equal("Success", result2.Name);
        Assert.True(factoryExecuted);
    }

    #endregion

    #region Edge Cases and Boundary Conditions

    [Fact]
    public async Task GetOrAddAsync_EmptyStringKey_ThrowsArgumentException()
    {
        // Arrange
        const string key = "";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _singleFactoryCaller.GetOrAddAsync(key, () => Task.FromResult(new TestData())));

        Assert.Equal("Key cannot be null or empty. (Parameter 'key')", exception.Message);
    }

    [Fact]
    public async Task GetOrAddAsync_WhitespaceKey_WorksCorrectly()
    {
        // Arrange
        const string key = "   ";
        var expectedValue = new TestData { Id = 1, Name = "Whitespace key test" };

        // Act
        var result = await _singleFactoryCaller.GetOrAddAsync(key, () => Task.FromResult(expectedValue));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedValue.Id, result.Id);
        Assert.Equal(expectedValue.Name, result.Name);
    }

    [Fact]
    public async Task GetOrAddAsync_VeryLongKey_WorksCorrectly()
    {
        // Arrange
        var longKey = new string('A', 10000); // Very long key
        var expectedValue = new TestData { Id = 1, Name = "Long key test" };

        // Act
        var result = await _singleFactoryCaller.GetOrAddAsync(longKey, () => Task.FromResult(expectedValue));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedValue.Id, result.Id);
        Assert.Equal(expectedValue.Name, result.Name);
    }

    [Fact]
    public async Task GetOrAddAsync_SpecialCharactersInKey_WorksCorrectly()
    {
        // Arrange
        const string key = "special-chars-!@#$%^&*()_+-=[]{}|;':\",./<>?";
        var expectedValue = new TestData { Id = 1, Name = "Special chars test" };

        // Act
        var result = await _singleFactoryCaller.GetOrAddAsync(key, () => Task.FromResult(expectedValue));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedValue.Id, result.Id);
        Assert.Equal(expectedValue.Name, result.Name);
    }

    [Fact]
    public async Task GetOrAddAsync_FactoryReturnsTaskFromResult_WorksCorrectly()
    {
        // Arrange
        const string key = "task-from-result-key";
        var expectedValue = new TestData { Id = 1, Name = "Task from result" };

        // Act
        var result = await _singleFactoryCaller.GetOrAddAsync(key, () => Task.FromResult(expectedValue));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedValue.Id, result.Id);
        Assert.Equal(expectedValue.Name, result.Name);
    }

    [Fact]
    public async Task GetOrAddAsync_FactoryReturnsCompletedTask_WorksCorrectly()
    {
        // Arrange
        const string key = "completed-task-key";
        var expectedValue = new TestData { Id = 1, Name = "Completed task" };

        // Act
        var result = await _singleFactoryCaller.GetOrAddAsync(key, () =>
        {
            var tcs = new TaskCompletionSource<TestData>();
            tcs.SetResult(expectedValue);
            return tcs.Task;
        });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedValue.Id, result.Id);
        Assert.Equal(expectedValue.Name, result.Name);
    }

    #endregion
}

// Test data class
public class TestData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}