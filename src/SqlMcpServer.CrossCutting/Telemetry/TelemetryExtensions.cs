using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace SqlMcpServer.CrossCutting.Telemetry;

public static class TelemetryExtensions
{
    private const string ActivitySourceName = "SqlMcpServer.Infrastructure";
    private const string MeterName = "SqlMcpServer.Infrastructure";

    public static IServiceCollection AddTelemetry(
        this IServiceCollection services, IConfiguration configuration)
    {
        var serviceName    = configuration["Telemetry:ServiceName"] ?? "SqlMcpServer";
        var otlpEndpoint   = configuration["Telemetry:OtlpEndpoint"];
        var enableTracing  = configuration.GetValue<bool>("Telemetry:EnableTracing", true);
        var enableMetrics  = configuration.GetValue<bool>("Telemetry:EnableMetrics", true);
        var samplingRatio  = configuration.GetValue<double>("Telemetry:SamplingRatio", 1.0);
        var deploymentEnv  = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";

        var otelBuilder = services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(serviceName, serviceVersion: "1.0.0")
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment", deploymentEnv)]));

        if (enableTracing)
        {
            otelBuilder.WithTracing(tracing =>
            {
                tracing
                    .AddSource(ActivitySourceName)
                    .SetSampler(samplingRatio >= 1.0
                        ? new AlwaysOnSampler()
                        : new TraceIdRatioBasedSampler(Math.Clamp(samplingRatio, 0.0, 1.0)))
                    .AddSqlClientInstrumentation(opt =>
                    {
                        opt.SetDbStatementForText = true;
                        opt.RecordException = true;
                    });

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    tracing.AddOtlpExporter(opt => opt.Endpoint = new Uri(otlpEndpoint));
            });
        }

        if (enableMetrics)
        {
            otelBuilder.WithMetrics(metrics =>
            {
                metrics.AddMeter(MeterName);

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    metrics.AddOtlpExporter(opt => opt.Endpoint = new Uri(otlpEndpoint));
            });
        }

        return services;
    }
}
