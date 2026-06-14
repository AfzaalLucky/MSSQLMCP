using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace SqlMcpServer.CrossCutting.Logging;

public static class LoggingExtensions
{
    public static IHostBuilder AddStructuredLogging(this IHostBuilder builder)
    {
        return builder.UseSerilog((context, _, loggerConfig) =>
        {
            var config = context.Configuration;
            var env = context.HostingEnvironment;

            var minLevelRaw = config["Serilog:MinimumLevel:Default"] ?? "Information";
            var minLevel = Enum.TryParse<LogEventLevel>(minLevelRaw, ignoreCase: true, out var parsed)
                ? parsed
                : LogEventLevel.Information;

            loggerConfig
                .MinimumLevel.Is(minLevel)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProperty("Application", "SqlMcpServer")
                .Enrich.WithProperty("Environment", env.EnvironmentName);

            var devTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";
            var prodTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{CorrelationId}] {SourceContext}: {Message:lj} {Properties:j}{NewLine}{Exception}";

            loggerConfig.WriteTo.Console(
                outputTemplate: env.IsEnvironment("Development") ? devTemplate : prodTemplate,
                standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose);

            var logPath = config["Serilog:LogPath"];
            if (!string.IsNullOrWhiteSpace(logPath))
            {
                loggerConfig.WriteTo.File(
                    logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    outputTemplate: prodTemplate,
                    shared: true);
            }

            var seqEndpoint = config["Serilog:SeqEndpoint"];
            if (!string.IsNullOrWhiteSpace(seqEndpoint))
                loggerConfig.WriteTo.Seq(seqEndpoint);
        });
    }
}
