using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlMcpServer.Application.Configuration;
using SqlMcpServer.Application.Security;
using SqlMcpServer.Application.Services;
using SqlMcpServer.Application.Validators;
using SqlMcpServer.Domain.Contracts.Services;

namespace SqlMcpServer.Application.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<McpSettings>(configuration.GetSection("Mcp"));
        services.Configure<SecuritySettings>(configuration.GetSection("Security"));
        services.Configure<TelemetrySettings>(configuration.GetSection("Telemetry"));
        services.Configure<ToolSettings>(configuration.GetSection("Tools"));

        services.AddValidatorsFromAssemblyContaining<GetObjectsRequestValidator>(ServiceLifetime.Singleton);

        services.AddSingleton<IQuerySafetyValidator, QuerySafetyValidator>();

        services.AddScoped<DatabaseDiscoveryService>();
        services.AddScoped<TableService>();
        services.AddScoped<ViewService>();
        services.AddScoped<FunctionService>();
        services.AddScoped<ProcedureService>();
        services.AddScoped<TriggerService>();
        services.AddScoped<TypeService>();
        services.AddScoped<QueryService>();
        services.AddScoped<DependencyService>();
        services.AddScoped<IDocumentationService, DocumentationService>();
        services.AddScoped<ISchemaComparisonService, SchemaComparisonService>();
        services.AddScoped<HealthService>();

        return services;
    }
}
