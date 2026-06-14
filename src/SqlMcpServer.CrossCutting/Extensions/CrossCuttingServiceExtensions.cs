using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlMcpServer.CrossCutting.Correlation;
using SqlMcpServer.CrossCutting.Telemetry;
using SqlMcpServer.CrossCutting.Throttling;

namespace SqlMcpServer.CrossCutting.Extensions;

public static class CrossCuttingServiceExtensions
{
    public static IServiceCollection AddCrossCutting(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Telemetry — OTel tracing + metrics
        services.AddTelemetry(configuration);

        // Per-operation correlation ID (flows through async via AsyncLocal)
        services.AddScoped<CorrelationContext>();

        // Concurrency throttle — singleton, bounded by Mcp:MaxConcurrentTools
        var maxConcurrent = configuration.GetValue<int>("Mcp:MaxConcurrentTools", 10);
        services.AddSingleton<IRequestThrottler>(new RequestThrottler(maxConcurrent));

        return services;
    }
}
