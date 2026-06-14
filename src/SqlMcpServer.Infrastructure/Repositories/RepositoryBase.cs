using System.Data.Common;
using System.Text.RegularExpressions;
using Polly;
using SqlMcpServer.Domain.Contracts.Infrastructure;

namespace SqlMcpServer.Infrastructure.Repositories;

internal abstract class RepositoryBase
{
    private static readonly Regex SafeIdentifier =
        new(@"^[\w ]{1,128}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    protected readonly IConnectionFactory ConnectionFactory;
    protected readonly ResiliencePipeline Pipeline;

    protected RepositoryBase(IConnectionFactory connectionFactory, ResiliencePipeline pipeline)
    {
        ConnectionFactory = connectionFactory;
        Pipeline = pipeline;
    }

    protected async Task<T> ExecuteAsync<T>(Func<DbConnection, Task<T>> operation, CancellationToken ct)
    {
        return await Pipeline.ExecuteAsync(async token =>
        {
            await using var connection = await ConnectionFactory.CreateConnectionAsync(token);
            return await operation(connection);
        }, ct);
    }

    protected async Task ExecuteAsync(Func<DbConnection, Task> operation, CancellationToken ct)
    {
        await Pipeline.ExecuteAsync(async token =>
        {
            await using var connection = await ConnectionFactory.CreateConnectionAsync(token);
            await operation(connection);
        }, ct);
    }

    protected static string ValidateDb(string database)
    {
        if (!SafeIdentifier.IsMatch(database))
            throw new ArgumentException($"Invalid database name: '{database}'", nameof(database));
        return database;
    }

    protected static string ValidateIdentifier(string value, string paramName = "value")
    {
        if (!SafeIdentifier.IsMatch(value))
            throw new ArgumentException($"Invalid identifier: '{value}'", paramName);
        return value;
    }

    protected static IReadOnlyList<string> SplitCsv(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
