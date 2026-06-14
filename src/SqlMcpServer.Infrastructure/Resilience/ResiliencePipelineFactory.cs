using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace SqlMcpServer.Infrastructure.Resilience;

public sealed class ResiliencePipelineFactory
{
    private static readonly HashSet<int> TransientSqlErrors =
    [
        -2,     // Timeout expired
        4060,   // Cannot open database
        40197,  // Service encountered an error
        40501,  // Service is busy
        40613,  // Database unavailable
        49918,  // Cannot process request — insufficient resources
        49919,  // Cannot process create/update request
        49920,  // Too many concurrent operations
        1205,   // Deadlock
        233,    // No process at the other end of the pipe
        10928,  // Resource limit exceeded
        10929,  // Resource limit exceeded
        10053,  // Transport-level error
        10054,  // Existing connection forcibly closed
        10060   // Connection timed out
    ];

    private readonly ILogger<ResiliencePipelineFactory> _logger;

    public ResiliencePipelineFactory(ILogger<ResiliencePipelineFactory> logger)
    {
        _logger = logger;
    }

    public ResiliencePipeline CreateSqlPipeline(
        int maxRetries = 3,
        int retryDelaySeconds = 1,
        int timeoutSeconds = 30)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                Delay = TimeSpan.FromSeconds(retryDelaySeconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<SqlException>(IsSqlTransient)
                    .Handle<TimeoutRejectedException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "SQL retry attempt {Attempt} after {Delay:g}. Exception: {Message}",
                        args.AttemptNumber + 1, args.RetryDelay, args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(60),
                ShouldHandle = new PredicateBuilder().Handle<SqlException>(IsSqlTransient),
                OnOpened = args =>
                {
                    _logger.LogError("SQL circuit breaker OPENED. Duration: {BreakDuration:g}", args.BreakDuration);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("SQL circuit breaker CLOSED — resuming normal operation");
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            })
            .Build();
    }

    private static bool IsSqlTransient(SqlException ex) =>
        TransientSqlErrors.Contains(ex.Number);
}
