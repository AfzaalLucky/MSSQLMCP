namespace SqlMcpServer.CrossCutting.Throttling;

public interface IRequestThrottler
{
    /// <summary>
    /// Acquires a concurrency slot. Dispose the returned handle to release it.
    /// </summary>
    Task<IAsyncDisposable> AcquireAsync(CancellationToken ct = default);

    int MaxConcurrency { get; }
    int CurrentCount { get; }
}
