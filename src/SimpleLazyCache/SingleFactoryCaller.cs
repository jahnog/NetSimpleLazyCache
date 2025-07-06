
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

public class SingleFactoryCaller<T> where T : class
{
    private readonly ConcurrentDictionary<string, Lazy<Task<T>>> _pendingTasks
        = new ConcurrentDictionary<string, Lazy<Task<T>>>();

    public async Task<T> GetOrAddAsync(string key, Func<Task<T>> valueFactory)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));
        }

        var lazyValue = _pendingTasks.GetOrAdd(key, k =>
            new Lazy<Task<T>>(
                async () =>
                {
                    try
                    {
                        return await valueFactory();
                    }
                    finally
                    {
                        // Remove the entry only after the task completes (success or failure)
                        // This ensures all concurrent waiters get the same result before cleanup
                        _pendingTasks.TryRemove(key, out _);
                    }
                }
            )
        );

        return await lazyValue.Value;
    }
}