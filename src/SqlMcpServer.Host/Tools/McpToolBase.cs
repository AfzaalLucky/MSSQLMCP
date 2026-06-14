using Microsoft.Extensions.DependencyInjection;
using SqlMcpServer.CrossCutting.Throttling;
using SqlMcpServer.Domain.Exceptions;
using SqlMcpServer.Host.Helpers;

namespace SqlMcpServer.Host.Tools;

public abstract class McpToolBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRequestThrottler _throttler;

    protected McpToolBase(IServiceScopeFactory scopeFactory, IRequestThrottler throttler)
    {
        _scopeFactory = scopeFactory;
        _throttler = throttler;
    }

    /// <summary>
    /// Executes the tool operation within a fresh DI scope, with structured error handling.
    /// </summary>
    protected async Task<string> ExecuteAsync(
        Func<IServiceProvider, CancellationToken, Task<string>> fn,
        CancellationToken ct = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            return await fn(scope.ServiceProvider, ct);
        }
        catch (McpDomainException ex)
        {
            return ToolHelper.Error(ex.ErrorCode, ex.Message);
        }
        catch (OperationCanceledException)
        {
            return ToolHelper.Error("Cancelled", "The operation was cancelled.");
        }
        catch (Exception ex)
        {
            return ToolHelper.Error("InternalError", ex.Message);
        }
    }

    /// <summary>
    /// Same as ExecuteAsync but acquires a concurrency slot first — use for heavy operations.
    /// </summary>
    protected async Task<string> ExecuteThrottledAsync(
        Func<IServiceProvider, CancellationToken, Task<string>> fn,
        CancellationToken ct = default)
    {
        await using var slot = await _throttler.AcquireAsync(ct);
        return await ExecuteAsync(fn, ct);
    }
}
