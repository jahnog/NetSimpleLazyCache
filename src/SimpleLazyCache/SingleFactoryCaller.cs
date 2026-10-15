using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class SingleFactoryCaller<T> where T : class
{
    private readonly Dictionary<string, Lazy<Task<T>>> _pending =
        new Dictionary<string, Lazy<Task<T>>>();

    private readonly object _gate = new object();

    // cancellationToken is not applied: one waiter must not cancel the shared factory.
    public async Task<T> GetOrAddAsync(string key, Func<Task<T>> valueFactory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));
        if (valueFactory == null)
            throw new ArgumentNullException(nameof(valueFactory));

        Lazy<Task<T>> lazy;
        lock (_gate)
        {
            if (!_pending.TryGetValue(key, out lazy))
            {
                lazy = new Lazy<Task<T>>(() => Invoke(valueFactory), LazyThreadSafetyMode.ExecutionAndPublication);
                _pending.Add(key, lazy);
            }
        }

        try
        {
            return await lazy.Value.ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                // A finishing caller must leave a newer entry alone.
                if (_pending.TryGetValue(key, out var current) && ReferenceEquals(current, lazy))
                    _pending.Remove(key);
            }
        }
    }

    private static async Task<T> Invoke(Func<Task<T>> valueFactory)
    {
        var task = valueFactory() ?? throw new InvalidOperationException("Value factory returned null task.");
        return await task.ConfigureAwait(false);
    }
}
