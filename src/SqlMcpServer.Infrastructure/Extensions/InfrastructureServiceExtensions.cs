using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Infrastructure.Audit;
using SqlMcpServer.Infrastructure.Caching;
using SqlMcpServer.Infrastructure.Configuration;
using SqlMcpServer.Infrastructure.Connection;
using SqlMcpServer.Infrastructure.Repositories;
using SqlMcpServer.Infrastructure.Resilience;
using SqlMcpServer.Infrastructure.Secrets;

namespace SqlMcpServer.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SqlServerSettings>(configuration.GetSection("SqlServer"));
        services.Configure<CacheSettings>(configuration.GetSection("Cache"));

        services.AddSingleton<ResiliencePipelineFactory>();
        services.AddSingleton<ResiliencePipeline>(sp =>
        {
            var factory = sp.GetRequiredService<ResiliencePipelineFactory>();
            var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SqlServerSettings>>().Value;
            return factory.CreateSqlPipeline(timeoutSeconds: settings.CommandTimeoutSeconds);
        });

        services.AddSingleton<IConnectionFactory, SqlConnectionFactory>();

        services.AddTransient<ISchemaRepository, SchemaRepository>();
        services.AddTransient<ITableRepository, TableRepository>();
        services.AddTransient<IViewRepository, ViewRepository>();
        services.AddTransient<IFunctionRepository, FunctionRepository>();
        services.AddTransient<IProcedureRepository, ProcedureRepository>();
        services.AddTransient<ITriggerRepository, TriggerRepository>();
        services.AddTransient<ITypeRepository, TypeRepository>();
        services.AddTransient<IIndexRepository, IndexRepository>();
        services.AddTransient<IConstraintRepository, ConstraintRepository>();
        services.AddTransient<IDependencyRepository, DependencyRepository>();
        services.AddTransient<IQueryExecutor, QueryExecutor>();

        services.AddMemoryCache();
        services.AddCaching(configuration);

        services.AddSingleton<SecretProviderFactory>();
        services.AddSingleton<ISecretProvider>(sp =>
            sp.GetRequiredService<SecretProviderFactory>().Create());

        services.AddSingleton<IAuditLogger, SerilogAuditLogger>();

        return services;
    }

    private static void AddCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var cacheProvider = configuration.GetSection("Cache")["Provider"] ?? "Memory";

        if (cacheProvider.Equals("Redis", StringComparison.OrdinalIgnoreCase))
        {
            var redis = configuration.GetSection("Cache")["RedisConnectionString"]
                        ?? throw new InvalidOperationException(
                            "Cache:RedisConnectionString is required when Provider is Redis.");
            services.AddStackExchangeRedisCache(options => options.Configuration = redis);
            services.AddSingleton<ICacheService, DistributedCacheService>();
        }
        else
        {
            services.AddSingleton<ICacheService, MemoryCacheService>();
        }
    }
}
