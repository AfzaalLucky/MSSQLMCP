using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SqlMcpServer.Infrastructure.Telemetry;

public static class InfrastructureTelemetry
{
    public const string ServiceName = "SqlMcpServer.Infrastructure";

    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");

    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    public static readonly Counter<long> QueryCount =
        Meter.CreateCounter<long>("sql.query.count", "queries", "Total number of SQL queries executed");

    public static readonly Histogram<double> QueryDuration =
        Meter.CreateHistogram<double>("sql.query.duration", "ms", "SQL query execution duration");

    public static readonly Counter<long> CacheHits =
        Meter.CreateCounter<long>("cache.hits", "hits", "Number of cache hits");

    public static readonly Counter<long> CacheMisses =
        Meter.CreateCounter<long>("cache.misses", "misses", "Number of cache misses");

    public static readonly Counter<long> ConnectionErrors =
        Meter.CreateCounter<long>("sql.connection.errors", "errors", "Number of SQL connection errors");

    public static readonly Counter<long> RetryCount =
        Meter.CreateCounter<long>("sql.retry.count", "retries", "Number of SQL operation retries");

    public static Activity? StartRepositoryActivity(string repositoryName, string operationName)
    {
        return ActivitySource.StartActivity(
            $"{repositoryName}.{operationName}",
            ActivityKind.Client,
            Activity.Current?.Context ?? default);
    }
}
