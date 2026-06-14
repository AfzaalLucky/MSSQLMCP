using Dapper;
using Polly;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Entities;
using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Infrastructure.Repositories;

internal sealed class DependencyRepository : RepositoryBase, IDependencyRepository
{
    public DependencyRepository(IConnectionFactory connectionFactory, ResiliencePipeline pipeline)
        : base(connectionFactory, pipeline) { }

    public async Task<IReadOnlyList<DependencyInfo>> FindObjectDependenciesAsync(
        string schema, string name, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(name);
        const string sql = """
            SELECT
                SCHEMA_NAME(o.schema_id)        AS ObjectSchema,
                o.name                          AS ObjectName,
                o.type_desc                     AS ObjectTypeDesc,
                d.referenced_schema_name        AS ReferencedSchema,
                d.referenced_entity_name        AS ReferencedName,
                ISNULL(ro.type_desc, 'UNKNOWN') AS ReferencedTypeDesc,
                d.is_caller_dependent           AS IsCallerDependent,
                d.is_ambiguous                  AS IsAmbiguous
            FROM sys.sql_expression_dependencies d
            INNER JOIN sys.objects o ON d.referencing_id = o.object_id
            INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
            LEFT JOIN sys.objects ro
                ON ro.object_id = OBJECT_ID(
                    ISNULL(d.referenced_schema_name,'dbo') + '.' + d.referenced_entity_name)
            WHERE s.name = @Schema AND o.name = @Name
            ORDER BY d.referenced_entity_name
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<DepRow>(sql, new { Schema = schema, Name = name });
            return rows.Select(MapDep).ToList().AsReadOnly();
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<DependencyInfo>> FindReferencingObjectsAsync(
        string schema, string name, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(name);
        const string sql = """
            SELECT
                SCHEMA_NAME(o.schema_id)        AS ObjectSchema,
                o.name                          AS ObjectName,
                o.type_desc                     AS ObjectTypeDesc,
                d.referenced_schema_name        AS ReferencedSchema,
                d.referenced_entity_name        AS ReferencedName,
                ISNULL(o.type_desc, 'UNKNOWN')  AS ReferencedTypeDesc,
                d.is_caller_dependent           AS IsCallerDependent,
                d.is_ambiguous                  AS IsAmbiguous
            FROM sys.sql_expression_dependencies d
            INNER JOIN sys.objects o ON d.referencing_id = o.object_id
            WHERE d.referenced_schema_name = @Schema
              AND d.referenced_entity_name = @Name
            ORDER BY SCHEMA_NAME(o.schema_id), o.name
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<DepRow>(sql, new { Schema = schema, Name = name });
            return rows.Select(MapDep).ToList().AsReadOnly();
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<DependencyInfo>> GenerateDependencyGraphAsync(
        string? schema, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                SCHEMA_NAME(o.schema_id)        AS ObjectSchema,
                o.name                          AS ObjectName,
                o.type_desc                     AS ObjectTypeDesc,
                d.referenced_schema_name        AS ReferencedSchema,
                d.referenced_entity_name        AS ReferencedName,
                ISNULL(ro.type_desc, 'UNKNOWN') AS ReferencedTypeDesc,
                d.is_caller_dependent           AS IsCallerDependent,
                d.is_ambiguous                  AS IsAmbiguous
            FROM sys.sql_expression_dependencies d
            INNER JOIN sys.objects o ON d.referencing_id = o.object_id
            INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
            LEFT JOIN sys.objects ro
                ON ro.object_id = OBJECT_ID(
                    ISNULL(d.referenced_schema_name,'dbo') + '.' + d.referenced_entity_name)
            WHERE (@Schema IS NULL OR s.name = @Schema)
              AND o.is_ms_shipped = 0
            ORDER BY SCHEMA_NAME(o.schema_id), o.name, d.referenced_entity_name
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<DepRow>(sql, new { Schema = schema });
            return rows.Select(MapDep).ToList().AsReadOnly();
        }, cancellationToken);
    }

    private static DependencyInfo MapDep(DepRow r) =>
        new(r.ObjectSchema, r.ObjectName, ParseType(r.ObjectTypeDesc),
            r.ReferencedSchema, r.ReferencedName, ParseType(r.ReferencedTypeDesc),
            r.IsCallerDependent, r.IsAmbiguous);

    private static ObjectType ParseType(string? desc) => desc?.ToUpperInvariant() switch
    {
        "USER_TABLE" => ObjectType.Table,
        "VIEW" => ObjectType.View,
        "SQL_STORED_PROCEDURE" => ObjectType.Procedure,
        "SQL_SCALAR_FUNCTION" => ObjectType.ScalarFunction,
        "SQL_TABLE_VALUED_FUNCTION" => ObjectType.TVF,
        "SQL_INLINE_TABLE_VALUED_FUNCTION" => ObjectType.InlineTVF,
        "SQL_TRIGGER" => ObjectType.Trigger,
        _ => ObjectType.Unknown
    };

    private sealed class DepRow
    {
        public string ObjectSchema { get; init; } = "";
        public string ObjectName { get; init; } = "";
        public string? ObjectTypeDesc { get; init; }
        public string? ReferencedSchema { get; init; }
        public string ReferencedName { get; init; } = "";
        public string? ReferencedTypeDesc { get; init; }
        public bool IsCallerDependent { get; init; }
        public bool IsAmbiguous { get; init; }
    }
}
