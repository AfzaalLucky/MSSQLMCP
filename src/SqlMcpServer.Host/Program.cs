using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SqlMcpServer.Application.Extensions;
using SqlMcpServer.CrossCutting.Extensions;
using SqlMcpServer.CrossCutting.Logging;
using SqlMcpServer.Infrastructure.Extensions;

await Host.CreateDefaultBuilder(args)
    .AddStructuredLogging()
    .ConfigureServices((ctx, services) =>
    {
        Console.Error.WriteLine("Server started");
        var config = ctx.Configuration;

        services
            .AddCrossCutting(config)
            .AddInfrastructure(config)
            .AddApplication(config);

        services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(Program).Assembly);
    })
    .RunConsoleAsync();
