using Dapper;
using Polly;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Infrastructure.Repositories;

internal sealed class TypeRepository : RepositoryBase, ITypeRepository
{
    public TypeRepository(IConnectionFactory connectionFactory, ResiliencePipeline pipeline)
        : base(connectionFactory, pipeline) { }

    public async Task<IReadOnlyList<UserDefinedTypeInfo>> GetUserDefinedTypesAsync(
        string? schema, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                s.name          AS [Schema],
                t.name          AS Name,
                bt.name         AS BaseType,
                CASE
                    WHEN bt.name IN ('nvarchar','nchar') THEN t.max_length / 2
                    ELSE t.max_length
                END             AS MaxLength,
                t.precision     AS Precision,
                t.scale         AS Scale,
                t.is_nullable   AS IsNullable,
                t.is_assembly_type AS IsAssemblyType,
                a.name          AS AssemblyName
            FROM sys.types t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            INNER JOIN sys.types bt ON t.system_type_id = bt.user_type_id
            LEFT JOIN sys.assemblies a ON t.assembly_id = a.assembly_id
            WHERE t.is_user_defined = 1
              AND t.is_table_type = 0
              AND (@Schema IS NULL OR s.name = @Schema)
            ORDER BY s.name, t.name
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<UdtRow>(sql, new { Schema = schema });
            return rows.Select(r => new UserDefinedTypeInfo(
                r.Schema, r.Name, r.BaseType, r.MaxLength, r.Precision, r.Scale,
                r.IsNullable, r.IsAssemblyType, r.AssemblyName))
                .ToList().AsReadOnly();
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<TableTypeInfo>> GetTableTypesAsync(
        string? schema, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                s.name  AS [Schema],
                t.name  AS Name
            FROM sys.types t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE t.is_table_type = 1
              AND (@Schema IS NULL OR s.name = @Schema)
            ORDER BY s.name, t.name
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<TtRow>(sql, new { Schema = schema });
            var result = new List<TableTypeInfo>();
            foreach (var r in rows)
            {
                var columns = await GetTableTypeColumnsAsync(conn, r.Schema, r.Name);
                result.Add(new TableTypeInfo(r.Schema, r.Name, columns));
            }
            return result.AsReadOnly();
        }, cancellationToken);
    }

    public async Task<UserDefinedTypeInfo?> DescribeUserDefinedTypeAsync(
        string schema, string name, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(name);
        const string sql = """
            SELECT
                s.name          AS [Schema],
                t.name          AS Name,
                bt.name         AS BaseType,
                CASE
                    WHEN bt.name IN ('nvarchar','nchar') THEN t.max_length / 2
                    ELSE t.max_length
                END             AS MaxLength,
                t.precision     AS Precision,
                t.scale         AS Scale,
                t.is_nullable   AS IsNullable,
                t.is_assembly_type AS IsAssemblyType,
                a.name          AS AssemblyName
            FROM sys.types t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            INNER JOIN sys.types bt ON t.system_type_id = bt.user_type_id
            LEFT JOIN sys.assemblies a ON t.assembly_id = a.assembly_id
            WHERE t.is_user_defined = 1 AND t.is_table_type = 0
              AND s.name = @Schema AND t.name = @Name
            """;

        return await ExecuteAsync(async conn =>
        {
            var r = await conn.QueryFirstOrDefaultAsync<UdtRow>(sql, new { Schema = schema, Name = name });
            return r is null ? null : new UserDefinedTypeInfo(
                r.Schema, r.Name, r.BaseType, r.MaxLength, r.Precision, r.Scale,
                r.IsNullable, r.IsAssemblyType, r.AssemblyName);
        }, cancellationToken);
    }

    public async Task<TableTypeInfo?> DescribeTableTypeAsync(
        string schema, string name, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(name);
        return await ExecuteAsync(async conn =>
        {
            const string existsSql = """
                SELECT COUNT(1) FROM sys.types t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE t.is_table_type = 1 AND s.name = @Schema AND t.name = @Name
                """;
            var count = await conn.ExecuteScalarAsync<int>(existsSql, new { Schema = schema, Name = name });
            if (count == 0) return null;

            var columns = await GetTableTypeColumnsAsync(conn, schema, name);
            return new TableTypeInfo(schema, name, columns);
        }, cancellationToken);
    }

    public async Task<string?> GetTypeDefinitionAsync(
        string schema, string name, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(name);
        return await ExecuteAsync(async conn =>
        {
            const string sql = """
                SELECT
                    'CREATE TYPE [' + s.name + '].[' + t.name + '] FROM ' + bt.name +
                    CASE
                        WHEN bt.name IN ('varchar','nvarchar','char','nchar','binary','varbinary')
                        THEN '(' + CASE WHEN t.max_length = -1 THEN 'MAX'
                                        WHEN bt.name IN ('nvarchar','nchar') THEN CAST(t.max_length/2 AS VARCHAR)
                                        ELSE CAST(t.max_length AS VARCHAR) END + ')'
                        WHEN bt.name IN ('decimal','numeric')
                        THEN '(' + CAST(t.precision AS VARCHAR) + ',' + CAST(t.scale AS VARCHAR) + ')'
                        ELSE ''
                    END +
                    CASE WHEN t.is_nullable = 0 THEN ' NOT NULL' ELSE ' NULL' END AS Definition
                FROM sys.types t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                INNER JOIN sys.types bt ON t.system_type_id = bt.user_type_id
                WHERE t.is_user_defined = 1 AND s.name = @Schema AND t.name = @Name
                """;
            return await conn.ExecuteScalarAsync<string?>(sql, new { Schema = schema, Name = name });
        }, cancellationToken);
    }

    private static async Task<IReadOnlyList<ColumnInfo>> GetTableTypeColumnsAsync(
        System.Data.Common.DbConnection conn, string schema, string name)
    {
        const string sql = """
            SELECT
                s.name          AS TableSchema,
                tt.name         AS TableName,
                c.name          AS ColumnName,
                c.column_id     AS OrdinalPosition,
                tp.name         AS DataType,
                CASE
                    WHEN tp.name IN ('nvarchar','nchar') THEN c.max_length / 2
                    ELSE c.max_length
                END             AS MaxLength,
                c.precision     AS Precision,
                c.scale         AS Scale,
                c.is_nullable   AS IsNullable,
                CAST(0 AS BIT)  AS HasDefault,
                NULL            AS DefaultValue,
                c.is_computed   AS IsComputed,
                CAST(0 AS BIT)  AS IsIdentity,
                NULL            AS ComputedDefinition
            FROM sys.table_types tt
            INNER JOIN sys.schemas s ON tt.schema_id = s.schema_id
            INNER JOIN sys.columns c ON tt.type_table_object_id = c.object_id
            INNER JOIN sys.types tp ON c.user_type_id = tp.user_type_id
            WHERE s.name = @Schema AND tt.name = @Name
            ORDER BY c.column_id
            """;

        var rows = await conn.QueryAsync<ColRow>(sql, new { Schema = schema, Name = name });
        return rows.Select(r => new ColumnInfo(
            r.TableSchema, r.TableName, r.ColumnName, r.OrdinalPosition,
            r.DataType, r.MaxLength, r.Precision, r.Scale, r.IsNullable,
            r.HasDefault, r.DefaultValue, r.IsComputed, r.IsIdentity, r.ComputedDefinition))
            .ToList().AsReadOnly();
    }

    private sealed class UdtRow
    {
        public string Schema { get; init; } = "";
        public string Name { get; init; } = "";
        public string BaseType { get; init; } = "";
        public int? MaxLength { get; init; }
        public int? Precision { get; init; }
        public int? Scale { get; init; }
        public bool IsNullable { get; init; }
        public bool IsAssemblyType { get; init; }
        public string? AssemblyName { get; init; }
    }

    private sealed class TtRow
    {
        public string Schema { get; init; } = "";
        public string Name { get; init; } = "";
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
