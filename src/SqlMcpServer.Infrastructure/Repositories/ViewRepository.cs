using Dapper;
using Polly;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Entities;
using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Infrastructure.Repositories;

internal sealed class ViewRepository : RepositoryBase, IViewRepository
{
    public ViewRepository(IConnectionFactory connectionFactory, ResiliencePipeline pipeline)
        : base(connectionFactory, pipeline) { }

    public async Task<IReadOnlyList<ViewInfo>> GetViewsAsync(
        string database, string? schema, CancellationToken cancellationToken = default)
    {
        var db = ValidateDb(database);
        var sql = $"""
            SELECT
                s.name          AS [Schema],
                v.name          AS Name,
                NULL            AS Definition,
                CAST(0 AS BIT)  AS IsUpdatable,
                NULL            AS CheckOption,
                v.create_date   AS CreateDate,
                v.modify_date   AS ModifyDate
            FROM [{db}].sys.views v
            INNER JOIN [{db}].sys.schemas s ON v.schema_id = s.schema_id
            WHERE v.is_ms_shipped = 0
              AND (@Schema IS NULL OR s.name = @Schema)
            ORDER BY s.name, v.name
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<ViewRow>(sql, new { Schema = schema });
            return rows.Select(Map).ToList().AsReadOnly();
        }, cancellationToken);
    }

    public async Task<ViewInfo?> DescribeViewAsync(
        string schema, string view, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(view);
        const string sql = """
            SELECT
                s.name                          AS [Schema],
                v.name                          AS Name,
                OBJECT_DEFINITION(v.object_id)  AS Definition,
                CAST(0 AS BIT)                  AS IsUpdatable,
                NULL                            AS CheckOption,
                v.create_date                   AS CreateDate,
                v.modify_date                   AS ModifyDate
            FROM sys.views v
            INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
            WHERE s.name = @Schema AND v.name = @View
            """;

        return await ExecuteAsync(async conn =>
        {
            var r = await conn.QueryFirstOrDefaultAsync<ViewRow>(sql, new { Schema = schema, View = view });
            return r is null ? null : Map(r);
        }, cancellationToken);
    }

    public async Task<string?> GetViewDefinitionAsync(
        string schema, string view, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(view);
        const string sql = """
            SELECT OBJECT_DEFINITION(OBJECT_ID(@ObjName))
            """;

        return await ExecuteAsync(async conn =>
            await conn.ExecuteScalarAsync<string?>(sql, new { ObjName = $"[{schema}].[{view}]" }),
            cancellationToken);
    }

    public async Task<IReadOnlyList<DependencyInfo>> GetViewDependenciesAsync(
        string schema, string view, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(view);
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
            var rows = await conn.QueryAsync<DepRow>(sql, new { Schema = schema, Name = view });
            return rows.Select(MapDep).ToList().AsReadOnly();
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ColumnInfo>> GetViewColumnsAsync(
        string schema, string view, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(view);
        const string sql = """
            SELECT
                SCHEMA_NAME(o.schema_id)    AS TableSchema,
                OBJECT_NAME(c.object_id)    AS TableName,
                c.name                      AS ColumnName,
                c.column_id                 AS OrdinalPosition,
                tp.name                     AS DataType,
                CASE
                    WHEN tp.name IN ('nvarchar','nchar','ntext')
                    THEN CASE WHEN c.max_length = -1 THEN -1 ELSE c.max_length / 2 END
                    ELSE CASE WHEN c.max_length = -1 THEN -1 ELSE CAST(c.max_length AS INT) END
                END                         AS MaxLength,
                c.precision                 AS Precision,
                c.scale                     AS Scale,
                c.is_nullable               AS IsNullable,
                CAST(0 AS BIT)              AS HasDefault,
                NULL                        AS DefaultValue,
                c.is_computed               AS IsComputed,
                CAST(0 AS BIT)              AS IsIdentity,
                NULL                        AS ComputedDefinition
            FROM sys.columns c
            INNER JOIN sys.objects o ON c.object_id = o.object_id
            INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
            INNER JOIN sys.types tp ON c.user_type_id = tp.user_type_id
            WHERE s.name = @Schema AND o.name = @View AND o.type = 'V'
            ORDER BY c.column_id
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<ColRow>(sql, new { Schema = schema, View = view });
            return rows.Select(r => new ColumnInfo(
                r.TableSchema, r.TableName, r.ColumnName, r.OrdinalPosition,
                r.DataType, r.MaxLength, r.Precision, r.Scale, r.IsNullable,
                r.HasDefault, r.DefaultValue, r.IsComputed, r.IsIdentity, r.ComputedDefinition))
                .ToList().AsReadOnly();
        }, cancellationToken);
    }

    private static ViewInfo Map(ViewRow r) =>
        new(r.Schema, r.Name, r.Definition, r.IsUpdatable, r.CheckOption, r.CreateDate, r.ModifyDate);

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

    private sealed class ViewRow
    {
        public string Schema { get; init; } = "";
        public string Name { get; init; } = "";
        public string? Definition { get; init; }
        public bool IsUpdatable { get; init; }
        public string? CheckOption { get; init; }
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

    private sealed class ColRow
    {
        public string TableSchema { get; init; } = "";
        public string TableName { get; init; } = "";
        public string ColumnName { get; init; } = "";
        public int OrdinalPosition { get; init; }
        public string DataType { get; init; } = "";
        public int? MaxLength { get; init; }
        public int? Precision { get; init; }
        public int? Scale { get; init; }
        public bool IsNullable { get; init; }
        public bool HasDefault { get; init; }
        public string? DefaultValue { get; init; }
        public bool IsComputed { get; init; }
        public bool IsIdentity { get; init; }
        public string? ComputedDefinition { get; init; }
    }
}
