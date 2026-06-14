namespace SqlMcpServer.Application.Configuration;

public sealed class TelemetrySettings
{
    public string ServiceName { get; set; } = "SqlMcpServer";
    public string? OtlpEndpoint { get; set; }
    public bool EnableMetrics { get; set; } = true;
    public bool EnableTracing { get; set; } = true;
    public bool EnableLogging { get; set; } = true;
    public double SamplingRatio { get; set; } = 1.0;
}
