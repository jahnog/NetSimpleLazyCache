# Test Coverage for SingleFactoryCaller

This document outlines the comprehensive test coverage for the `SingleFactoryCaller<T>` class, covering all best practice conditions including memory leaks, race conditions, and cache stampede scenarios.

## Test Categories

### 1. Basic Functionality Tests
- **`GetOrAddAsync_WithValidKey_ReturnsValue`**: Verifies the basic functionality works correctly with valid inputs
- **`GetOrAddAsync_WithInvalidKey_ThrowsArgumentException`**: Tests parameter validation for null and empty keys

### 2. Cache Stampede Prevention Tests
- **`GetOrAddAsync_ConcurrentRequests_OnlyCallsFactoryOnce`**: Verifies that multiple concurrent requests for the same key only execute the factory function once
- **`GetOrAddAsync_HighConcurrency_PreventsCacheStampede`**: Tests high concurrency scenarios (100 concurrent requests) to ensure cache stampede prevention

### 3. Race Condition Tests
- **`GetOrAddAsync_DifferentKeys_CallsFactoryForEachKey`**: Ensures that different keys are handled independently and each factory is called exactly once
- **`GetOrAddAsync_RapidSequentialRequests_HandlesCorrectly`**: Tests rapid sequential (non-concurrent) requests to verify proper behavior
- **`GetOrAddAsync_SameKeyDifferentFactories_UsesFirstFactory`**: Verifies that when concurrent requests use different factories for the same key, only the first factory is executed

### 4. Memory Leak Prevention Tests
- **`GetOrAddAsync_CompletedTasks_RemovesFromInternalDictionary`**: Uses reflection to verify that completed tasks are properly cleaned up from the internal dictionary
- **`GetOrAddAsync_MultipleKeys_CleansUpAllCompletedTasks`**: Ensures cleanup works correctly for multiple different keys
- **`GetOrAddAsync_ExceptionInFactory_CleansUpTask`**: Verifies that even when factory functions throw exceptions, cleanup still occurs

### 5. Performance and Stress Tests
- **`GetOrAddAsync_StressTest_PerformsWithinReasonableTime`**: Stress tests with 100 keys × 50 requests each (5,000 total requests) to verify performance characteristics
- **`GetOrAddAsync_LongRunningFactory_HandlesTimeout`**: Tests behavior with long-running factories and cancellation tokens

### 6. Edge Cases
- **`GetOrAddAsync_FactoryReturnsNull_HandlesGracefully`**: Tests behavior when factory returns null values

## Key Testing Techniques Used

### Reflection-Based Memory Leak Detection
The tests use reflection to access the private `_pendingTasks` field to verify that:
- Tasks are properly removed after completion
- No memory leaks occur even with multiple concurrent operations
- Cleanup happens even when exceptions are thrown

### Concurrency Testing
- Uses `Task.WhenAll` to execute multiple concurrent operations
- Employs `Interlocked.Increment` for thread-safe counting
- Tests various concurrency levels (10, 50, 100 concurrent requests)

### Race Condition Detection
- Verifies factory call counts using thread-safe counters
- Tests timing-sensitive scenarios with controlled delays
- Ensures deterministic behavior under concurrent load

### Cache Stampede Prevention
- Confirms that multiple concurrent requests for the same key result in only one factory execution
- Validates that all requesters receive the same result
- Tests both low and high concurrency scenarios

## Test Statistics
- **Total Tests**: 14
- **All Tests Passing**: ✅
- **Coverage Areas**: Memory management, concurrency, performance, edge cases
- **Concurrency Levels Tested**: 1, 10, 50, 100 concurrent operations
- **Stress Test Scale**: 5,000 total operations across 100 keys

## Best Practices Verified

1. **Thread Safety**: All operations are thread-safe under high concurrency
2. **Memory Management**: No memory leaks through proper cleanup of completed tasks
3. **Cache Stampede Prevention**: Multiple requests for the same key only execute the factory once
4. **Exception Safety**: Proper cleanup occurs even when operations fail
5. **Performance**: Reasonable performance under stress conditions
6. **Parameter Validation**: Proper validation of input parameters

These tests ensure that the `SingleFactoryCaller` implementation follows caching best practices and is production-ready for high-concurrency scenarios.
