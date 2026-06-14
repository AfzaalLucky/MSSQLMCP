using Dapper;
using Polly;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Infrastructure.Repositories;

internal sealed class SchemaRepository : RepositoryBase, ISchemaRepository
{
    public SchemaRepository(IConnectionFactory connectionFactory, ResiliencePipeline pipeline)
        : base(connectionFactory, pipeline) { }

    public async Task<IReadOnlyList<DatabaseInfo>> GetDatabasesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                d.name                                       AS Name,
                d.state_desc                                 AS State,
                d.compatibility_level                        AS CompatibilityLevel,
                ISNULL(d.collation_name, '')                 AS Collation,
                d.create_date                                AS CreateDate,
                d.is_read_only                               AS IsReadOnly,
                ISNULL(d.recovery_model_desc, 'SIMPLE')      AS RecoveryModel
            FROM sys.databases d
            ORDER BY d.name
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<DbRow>(sql);
            return rows.Select(r => new DatabaseInfo(
                r.Name, r.State, r.CompatibilityLevel, r.Collation,
                r.CreateDate, r.IsReadOnly, r.RecoveryModel))
                .ToList().AsReadOnly();
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<SchemaInfo>> GetSchemasAsync(string database, CancellationToken cancellationToken = default)
    {
        var db = ValidateDb(database);
        var sql = $"""
            SELECT
                s.name                      AS Name,
                USER_NAME(s.principal_id)   AS Owner,
                s.schema_id                 AS SchemaId
            FROM [{db}].sys.schemas s
            WHERE s.schema_id < 16384
            ORDER BY s.name
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<SchemaRow>(sql);
            return rows.Select(r => new SchemaInfo(r.Name, r.Owner ?? "", r.SchemaId))
                .ToList().AsReadOnly();
        }, cancellationToken);
    }

    private sealed class DbRow
    {
        public string Name { get; init; } = "";
        public string State { get; init; } = "";
        public int CompatibilityLevel { get; init; }
        public string Collation { get; init; } = "";
        public DateTime CreateDate { get; init; }
        public bool IsReadOnly { get; init; }
        public string RecoveryModel { get; init; } = "";
    }

    private sealed class SchemaRow
    {
        public string Name { get; init; } = "";
        public string? Owner { get; init; }
        public int SchemaId { get; init; }
    }
}
