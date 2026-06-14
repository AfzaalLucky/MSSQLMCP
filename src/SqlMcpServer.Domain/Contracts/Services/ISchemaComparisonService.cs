using SqlMcpServer.Domain.ValueObjects;

namespace SqlMcpServer.Domain.Contracts.Services;

public interface ISchemaComparisonService
{
    Task<SchemaComparisonResult> CompareSchemasAsync(string sourceDatabase, string sourceSchema, string targetDatabase, string targetSchema, CancellationToken cancellationToken = default);
    Task<SchemaComparisonResult> CompareDatabasesAsync(string sourceDatabase, string targetDatabase, CancellationToken cancellationToken = default);
    Task<string> GenerateMigrationScriptAsync(string sourceDatabase, string sourceSchema, string targetDatabase, string targetSchema, CancellationToken cancellationToken = default);
}
