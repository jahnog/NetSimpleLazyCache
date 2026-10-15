namespace SimpleLazyCache.Unit.Tests;

internal static class BlockedFactory
{
    public static HeldCall<T> Hold<T>() where T : class => new HeldCall<T>();
}

internal sealed class HeldCall<T> where T : class
{
    private int _calls;

    public HeldCall()
    {
        Release = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Factory = () =>
        {
            Interlocked.Increment(ref _calls);
            return Release.Task;
        };
    }

    public Func<Task<T>> Factory { get; }

    public TaskCompletionSource<T> Release { get; }

    public int Calls => Volatile.Read(ref _calls);
}
