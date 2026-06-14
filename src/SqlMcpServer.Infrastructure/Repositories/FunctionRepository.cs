using Dapper;
using Polly;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Entities;
using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Infrastructure.Repositories;

internal sealed class FunctionRepository : RepositoryBase, IFunctionRepository
{
    public FunctionRepository(IConnectionFactory connectionFactory, ResiliencePipeline pipeline)
        : base(connectionFactory, pipeline) { }

    public async Task<IReadOnlyList<FunctionInfo>> GetFunctionsAsync(
        string database, string? schema, CancellationToken cancellationToken = default)
    {
        var db = ValidateDb(database);
        var sql = $"""
            SELECT
                s.name  AS [Schema],
                o.name  AS Name,
                o.type  AS TypeCode,
                NULL    AS ReturnType,
                NULL    AS Definition,
                o.create_date   AS CreateDate,
                o.modify_date   AS ModifyDate
            FROM [{db}].sys.objects o
            INNER JOIN [{db}].sys.schemas s ON o.schema_id = s.schema_id
            WHERE o.type IN ('FN','IF','TF')
              AND o.is_ms_shipped = 0
              AND (@Schema IS NULL OR s.name = @Schema)
            ORDER BY s.name, o.name
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<FuncRow>(sql, new { Schema = schema });
            return await BuildFunctionListAsync(conn, rows);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<FunctionInfo>> GetScalarFunctionsAsync(
        string? schema, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT s.name AS [Schema], o.name AS Name, o.type AS TypeCode,
                   NULL AS ReturnType, NULL AS Definition,
                   o.create_date AS CreateDate, o.modify_date AS ModifyDate
            FROM sys.objects o
            INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
            WHERE o.type = 'FN' AND o.is_ms_shipped = 0
              AND (@Schema IS NULL OR s.name = @Schema)
            ORDER BY s.name, o.name
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<FuncRow>(sql, new { Schema = schema });
            return await BuildFunctionListAsync(conn, rows);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<FunctionInfo>> GetTableValuedFunctionsAsync(
        string? schema, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT s.name AS [Schema], o.name AS Name, o.type AS TypeCode,
                   NULL AS ReturnType, NULL AS Definition,
                   o.create_date AS CreateDate, o.modify_date AS ModifyDate
            FROM sys.objects o
            INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
            WHERE o.type IN ('IF','TF') AND o.is_ms_shipped = 0
              AND (@Schema IS NULL OR s.name = @Schema)
            ORDER BY s.name, o.name
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<FuncRow>(sql, new { Schema = schema });
            return await BuildFunctionListAsync(conn, rows);
        }, cancellationToken);
    }

    public async Task<FunctionInfo?> DescribeFunctionAsync(
        string schema, string name, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(name);
        const string sql = """
            SELECT
                s.name  AS [Schema],
                o.name  AS Name,
                o.type  AS TypeCode,
                tp.name AS ReturnType,
                OBJECT_DEFINITION(o.object_id) AS Definition,
                o.create_date AS CreateDate,
                o.modify_date AS ModifyDate
            FROM sys.objects o
            INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
            LEFT JOIN sys.parameters ret
                ON ret.object_id = o.object_id AND ret.parameter_id = 0
            LEFT JOIN sys.types tp ON ret.user_type_id = tp.user_type_id
            WHERE o.type IN ('FN','IF','TF') AND s.name = @Schema AND o.name = @Name
            """;

        return await ExecuteAsync(async conn =>
        {
            var r = await conn.QueryFirstOrDefaultAsync<FuncRow>(sql, new { Schema = schema, Name = name });
            if (r is null) return null;
            var parameters = await GetParametersAsync(conn, schema, name);
            return Map(r, parameters);
        }, cancellationToken);
    }

    public async Task<string?> GetFunctionDefinitionAsync(
        string schema, string name, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(name);
        return await ExecuteAsync(async conn =>
            await conn.ExecuteScalarAsync<string?>(
                "SELECT OBJECT_DEFINITION(OBJECT_ID(@ObjName))",
                new { ObjName = $"[{schema}].[{name}]" }),
            cancellationToken);
    }

    public async Task<IReadOnlyList<DependencyInfo>> AnalyzeFunctionDependenciesAsync(
        string schema, string name, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(name);
        return await ExecuteAsync(async conn =>
            await GetDependenciesAsync(conn, schema, name), cancellationToken);
    }

    private static async Task<IReadOnlyList<FunctionInfo>> BuildFunctionListAsync(
        System.Data.Common.DbConnection conn, IEnumerable<FuncRow> rows)
    {
        var result = new List<FunctionInfo>();
        foreach (var r in rows)
        {
            var parameters = await GetParametersAsync(conn, r.Schema, r.Name);
            result.Add(Map(r, parameters));
        }
        return result.AsReadOnly();
    }

    private static async Task<IReadOnlyList<ProcedureParameter>> GetParametersAsync(
        System.Data.Common.DbConnection conn, string schema, string name)
    {
        const string sql = """
            SELECT
                p.name              AS Name,
                tp.name             AS DataType,
                p.parameter_id      AS OrdinalPosition,
                CASE WHEN p.is_output = 1 THEN 'OUT' ELSE 'IN' END AS ParameterMode,
                CASE
                    WHEN tp.name IN ('nvarchar','nchar') THEN p.max_length / 2
                    ELSE p.max_length
                END                 AS MaxLength,
                p.precision         AS Precision,
                p.scale             AS Scale,
                CAST(0 AS BIT)      AS HasDefault,
                NULL                AS DefaultValue,
                p.is_output         AS IsOutput,
                p.is_readonly       AS IsReadOnly
            FROM sys.parameters p
            INNER JOIN sys.objects o ON p.object_id = o.object_id
            INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
            INNER JOIN sys.types tp ON p.user_type_id = tp.user_type_id
            WHERE s.name = @Schema AND o.name = @Name AND p.parameter_id > 0
            ORDER BY p.parameter_id
            """;

        var rows = await conn.QueryAsync<ParamRow>(sql, new { Schema = schema, Name = name });
        return rows.Select(r => new ProcedureParameter(
            r.Name, r.DataType, r.OrdinalPosition, r.ParameterMode,
            r.MaxLength, r.Precision, r.Scale, r.HasDefault, r.DefaultValue,
            r.IsOutput, r.IsReadOnly))
            .ToList().AsReadOnly();
    }

    private static async Task<IReadOnlyList<DependencyInfo>> GetDependenciesAsync(
        System.Data.Common.DbConnection conn, string schema, string name)
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
            WHERE s.name = @Schema AND o.name = @Name
            """;

        var rows = await conn.QueryAsync<DepRow>(sql, new { Schema = schema, Name = name });
        return rows.Select(r => new DependencyInfo(
            r.ObjectSchema, r.ObjectName, ParseType(r.ObjectTypeDesc),
            r.ReferencedSchema, r.ReferencedName, ParseType(r.ReferencedTypeDesc),
            r.IsCallerDependent, r.IsAmbiguous))
            .ToList().AsReadOnly();
    }

    private static FunctionInfo Map(FuncRow r, IReadOnlyList<ProcedureParameter> parameters) =>
        new(r.Schema, r.Name, ParseFuncType(r.TypeCode), r.ReturnType, r.Definition,
            parameters, r.CreateDate, r.ModifyDate);

    private static FunctionType ParseFuncType(string? code) => code?.Trim() switch
    {
        "FN" => FunctionType.Scalar,
        "IF" => FunctionType.InlineTableValued,
        "TF" => FunctionType.MultiStatementTableValued,
        _ => FunctionType.Scalar
    };

    private static ObjectType ParseType(string? desc) => desc?.ToUpperInvariant() switch
    {
        "USER_TABLE" => ObjectType.Table,
        "VIEW" => ObjectType.View,
        "SQL_STORED_PROCEDURE" => ObjectType.Procedure,
        "SQL_SCALAR_FUNCTION" => ObjectType.ScalarFunction,
        "SQL_TABLE_VALUED_FUNCTION" => ObjectType.TVF,
        "SQL_INLINE_TABLE_VALUED_FUNCTION" => ObjectType.InlineTVF,
        _ => ObjectType.Unknown
    };

    private sealed class FuncRow
    {
        public string Schema { get; init; } = "";
        public string Name { get; init; } = "";
        public string? TypeCode { get; init; }
        public string? ReturnType { get; init; }
        public string? Definition { get; init; }
        public DateTime CreateDate { get; init; }
        public DateTime? ModifyDate { get; init; }
    }

    private sealed class ParamRow
    {
        public string Name { get; init; } = "";
        public string DataType { get; init; } = "";
        public int OrdinalPosition { get; init; }
        public string ParameterMode { get; init; } = "IN";
        public int? MaxLength { get; init; }
        public int? Precision { get; init; }
        public int? Scale { get; init; }
        public bool HasDefault { get; init; }
        public string? DefaultValue { get; init; }
        public bool IsOutput { get; init; }
        public bool IsReadOnly { get; init; }
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
