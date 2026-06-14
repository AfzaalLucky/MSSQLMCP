using Dapper;
using Polly;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Entities;
using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Infrastructure.Repositories;

internal sealed class TriggerRepository : RepositoryBase, ITriggerRepository
{
    public TriggerRepository(IConnectionFactory connectionFactory, ResiliencePipeline pipeline)
        : base(connectionFactory, pipeline) { }

    public async Task<IReadOnlyList<TriggerInfo>> GetTriggersAsync(
        string database, string? schema, CancellationToken cancellationToken = default)
    {
        var db = ValidateDb(database);
        var sql = $"""
            SELECT
                s.name AS [Schema],
                o.name AS Name,
                pt.name AS ParentTable,
                CAST(CASE WHEN t.is_disabled = 0 THEN 1 ELSE 0 END AS BIT) AS IsEnabled,
                CASE WHEN t.is_instead_of_trigger = 1 THEN 'INSTEAD OF' ELSE 'AFTER' END AS TriggerType,
                (
                    SELECT
                        SUM(CASE WHEN te.type_desc = 'INSERT' THEN 1 ELSE 0 END) +
                        SUM(CASE WHEN te.type_desc = 'UPDATE' THEN 2 ELSE 0 END) +
                        SUM(CASE WHEN te.type_desc = 'DELETE' THEN 4 ELSE 0 END)
                    FROM [{db}].sys.trigger_events te
                    WHERE te.object_id = t.object_id
                ) AS EventFlags,
                NULL AS Definition,
                o.create_date AS CreateDate,
                o.modify_date AS ModifyDate
            FROM [{db}].sys.triggers t
            INNER JOIN [{db}].sys.objects o ON t.object_id = o.object_id
            INNER JOIN [{db}].sys.schemas s ON o.schema_id = s.schema_id
            INNER JOIN [{db}].sys.objects pt ON t.parent_id = pt.object_id
            WHERE t.parent_class = 1
              AND (@Schema IS NULL OR s.name = @Schema)
            ORDER BY s.name, o.name
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<TriggerRow>(sql, new { Schema = schema });
            return rows.Select(Map).ToList().AsReadOnly();
        }, cancellationToken);
    }

    public async Task<TriggerInfo?> DescribeTriggerAsync(
        string schema, string name, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(name);
        const string sql = """
            SELECT
                s.name AS [Schema],
                o.name AS Name,
                pt.name AS ParentTable,
                CAST(CASE WHEN t.is_disabled = 0 THEN 1 ELSE 0 END AS BIT) AS IsEnabled,
                CASE WHEN t.is_instead_of_trigger = 1 THEN 'INSTEAD OF' ELSE 'AFTER' END AS TriggerType,
                (
                    SELECT
                        SUM(CASE WHEN te.type_desc = 'INSERT' THEN 1 ELSE 0 END) +
                        SUM(CASE WHEN te.type_desc = 'UPDATE' THEN 2 ELSE 0 END) +
                        SUM(CASE WHEN te.type_desc = 'DELETE' THEN 4 ELSE 0 END)
                    FROM sys.trigger_events te WHERE te.object_id = t.object_id
                ) AS EventFlags,
                OBJECT_DEFINITION(t.object_id) AS Definition,
                o.create_date AS CreateDate,
                o.modify_date AS ModifyDate
            FROM sys.triggers t
            INNER JOIN sys.objects o ON t.object_id = o.object_id
            INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
            INNER JOIN sys.objects pt ON t.parent_id = pt.object_id
            WHERE t.parent_class = 1 AND s.name = @Schema AND o.name = @Name
            """;

        return await ExecuteAsync(async conn =>
        {
            var r = await conn.QueryFirstOrDefaultAsync<TriggerRow>(sql, new { Schema = schema, Name = name });
            return r is null ? null : Map(r);
        }, cancellationToken);
    }

    public async Task<string?> GetTriggerDefinitionAsync(
        string schema, string name, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(name);
        return await ExecuteAsync(async conn =>
            await conn.ExecuteScalarAsync<string?>(
                "SELECT OBJECT_DEFINITION(OBJECT_ID(@ObjName))",
                new { ObjName = $"[{schema}].[{name}]" }),
            cancellationToken);
    }

    public async Task<IReadOnlyList<DependencyInfo>> GetTriggerDependenciesAsync(
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
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<DepRow>(sql, new { Schema = schema, Name = name });
            return rows.Select(r => new DependencyInfo(
                r.ObjectSchema, r.ObjectName, ParseType(r.ObjectTypeDesc),
                r.ReferencedSchema, r.ReferencedName, ParseType(r.ReferencedTypeDesc),
                r.IsCallerDependent, r.IsAmbiguous))
                .ToList().AsReadOnly();
        }, cancellationToken);
    }

    private static TriggerInfo Map(TriggerRow r) =>
        new(r.Schema, r.Name, r.ParentTable, r.IsEnabled, r.TriggerType,
            (TriggerEvent)(r.EventFlags ?? 0), r.Definition, r.CreateDate, r.ModifyDate);

    private static ObjectType ParseType(string? desc) => desc?.ToUpperInvariant() switch
    {
        "USER_TABLE" => ObjectType.Table,
        "VIEW" => ObjectType.View,
        "SQL_STORED_PROCEDURE" => ObjectType.Procedure,
        _ => ObjectType.Unknown
    };

    private sealed class TriggerRow
    {
        public string Schema { get; init; } = "";
        public string Name { get; init; } = "";
        public string ParentTable { get; init; } = "";
        public bool IsEnabled { get; init; }
        public string TriggerType { get; init; } = "AFTER";
        public int? EventFlags { get; init; }
        public string? Definition { get; init; }
        public DateTime CreateDate { get; init; }
        public DateTime? ModifyDate { get; init; }
    }

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
