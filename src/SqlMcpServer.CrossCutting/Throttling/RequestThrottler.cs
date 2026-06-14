namespace SqlMcpServer.CrossCutting.Throttling;

internal sealed class RequestThrottler : IRequestThrottler, IDisposable
{
    private readonly SemaphoreSlim _semaphore;

    public RequestThrottler(int maxConcurrency)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency, 1);
        MaxConcurrency = maxConcurrency;
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public int MaxConcurrency { get; }

    // SemaphoreSlim.CurrentCount is how many slots are still free
    public int CurrentCount => _semaphore.CurrentCount;

    public async Task<IAsyncDisposable> AcquireAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        return new ReleaseHandle(_semaphore);
    }

    public void Dispose() => _semaphore.Dispose();

    private sealed class ReleaseHandle : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private int _released;

        public ReleaseHandle(SemaphoreSlim semaphore) => _semaphore = semaphore;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                _semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
