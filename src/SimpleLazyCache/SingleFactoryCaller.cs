
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

public class SingleFactoryCaller<T> where T : class
{
    private readonly ConcurrentDictionary<string, Lazy<Task<T>>> _pendingTasks
        = new ConcurrentDictionary<string, Lazy<Task<T>>>();

    public async Task<T> GetOrAddAsync(string key, Func<Task<T>> valueFactory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));
        if (valueFactory == null)
            throw new ArgumentNullException(nameof(valueFactory));

        var lazyValue = _pendingTasks.GetOrAdd(key, k =>
            new Lazy<Task<T>>(
                async () =>
                {
                    var task = valueFactory() ?? throw new InvalidOperationException("Value factory returned null task.");
                    return await task;
                }
            )
        );

        try
        {
            return await lazyValue.Value;
        }
        finally
        {
            _pendingTasks.TryRemove(key, out _);
        }
    }

}