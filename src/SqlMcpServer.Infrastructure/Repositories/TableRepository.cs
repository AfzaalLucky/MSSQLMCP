using System.Diagnostics;
using Dapper;
using Polly;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Entities;
using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Infrastructure.Repositories;

internal sealed class TableRepository : RepositoryBase, ITableRepository
{
    public TableRepository(IConnectionFactory connectionFactory, ResiliencePipeline pipeline)
        : base(connectionFactory, pipeline) { }

    public async Task<IReadOnlyList<TableInfo>> GetTablesAsync(
        string database, string? schema, CancellationToken cancellationToken = default)
    {
        var db = ValidateDb(database);
        var sql = $"""
            SELECT
                s.name  AS [Schema],
                t.name  AS Name,
                'BASE TABLE' AS Type,
                ISNULL(rc.rows, 0) AS RowCount,
                t.create_date AS CreateDate,
                t.modify_date AS ModifyDate,
                CASE WHEN ci.object_id IS NOT NULL THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS HasClusteredIndex,
                fg.name AS FileGroup
            FROM [{db}].sys.tables t
            INNER JOIN [{db}].sys.schemas s ON t.schema_id = s.schema_id
            OUTER APPLY (
                SELECT SUM(p.rows) AS rows
                FROM [{db}].sys.partitions p
                WHERE p.object_id = t.object_id AND p.index_id IN (0, 1)
            ) rc
            LEFT JOIN (
                SELECT DISTINCT object_id
                FROM [{db}].sys.indexes
                WHERE type = 1 AND is_disabled = 0
            ) ci ON t.object_id = ci.object_id
            LEFT JOIN [{db}].sys.indexes idx ON t.object_id = idx.object_id AND idx.index_id = 1
            LEFT JOIN [{db}].sys.data_spaces fg ON idx.data_space_id = fg.data_space_id
            WHERE t.is_ms_shipped = 0
              AND (@Schema IS NULL OR s.name = @Schema)
            ORDER BY s.name, t.name
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<TableRow>(sql, new { Schema = schema });
            return rows.Select(r => new TableInfo(
                r.Schema, r.Name, r.Type, r.RowCount,
                r.CreateDate, r.ModifyDate, r.HasClusteredIndex, r.FileGroup))
                .ToList().AsReadOnly();
        }, cancellationToken);
    }

    public async Task<TableInfo?> DescribeTableAsync(
        string schema, string table, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(table);
        const string sql = """
            SELECT
                s.name  AS [Schema],
                t.name  AS Name,
                'BASE TABLE' AS Type,
                ISNULL(rc.rows, 0) AS RowCount,
                t.create_date AS CreateDate,
                t.modify_date AS ModifyDate,
                CASE WHEN ci.object_id IS NOT NULL THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS HasClusteredIndex,
                fg.name AS FileGroup
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            OUTER APPLY (
                SELECT SUM(p.rows) AS rows
                FROM sys.partitions p
                WHERE p.object_id = t.object_id AND p.index_id IN (0,1)
            ) rc
            LEFT JOIN (SELECT DISTINCT object_id FROM sys.indexes WHERE type = 1 AND is_disabled = 0) ci
                ON t.object_id = ci.object_id
            LEFT JOIN sys.indexes idx ON t.object_id = idx.object_id AND idx.index_id = 1
            LEFT JOIN sys.data_spaces fg ON idx.data_space_id = fg.data_space_id
            WHERE t.is_ms_shipped = 0 AND s.name = @Schema AND t.name = @Table
            """;

        return await ExecuteAsync(async conn =>
        {
            var r = await conn.QueryFirstOrDefaultAsync<TableRow>(sql, new { Schema = schema, Table = table });
            return r is null ? null : new TableInfo(
                r.Schema, r.Name, r.Type, r.RowCount,
                r.CreateDate, r.ModifyDate, r.HasClusteredIndex, r.FileGroup);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ColumnInfo>> GetTableColumnsAsync(
        string schema, string table, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(table);
        const string sql = """
            SELECT
                SCHEMA_NAME(o.schema_id)        AS TableSchema,
                OBJECT_NAME(c.object_id)        AS TableName,
                c.name                          AS ColumnName,
                c.column_id                     AS OrdinalPosition,
                tp.name                         AS DataType,
                CASE
                    WHEN tp.name IN ('nvarchar','nchar','ntext')
                    THEN CASE WHEN c.max_length = -1 THEN -1 ELSE c.max_length / 2 END
                    ELSE CASE WHEN c.max_length = -1 THEN -1 ELSE CAST(c.max_length AS INT) END
                END                             AS MaxLength,
                c.precision                     AS Precision,
                c.scale                         AS Scale,
                c.is_nullable                   AS IsNullable,
                CASE WHEN dc.definition IS NOT NULL THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS HasDefault,
                dc.definition                   AS DefaultValue,
                c.is_computed                   AS IsComputed,
                c.is_identity                   AS IsIdentity,
                cc.definition                   AS ComputedDefinition
            FROM sys.columns c
            INNER JOIN sys.objects o ON c.object_id = o.object_id
            INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
            INNER JOIN sys.types tp ON c.user_type_id = tp.user_type_id
            LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
            LEFT JOIN sys.computed_columns cc
                ON c.object_id = cc.object_id AND c.column_id = cc.column_id
            WHERE s.name = @Schema AND o.name = @Table
            ORDER BY c.column_id
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<ColRow>(sql, new { Schema = schema, Table = table });
            return rows.Select(r => new ColumnInfo(
                r.TableSchema, r.TableName, r.ColumnName, r.OrdinalPosition,
                r.DataType, r.MaxLength, r.Precision, r.Scale, r.IsNullable,
                r.HasDefault, r.DefaultValue, r.IsComputed, r.IsIdentity, r.ComputedDefinition))
                .ToList().AsReadOnly();
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ConstraintInfo>> GetPrimaryKeysAsync(
        string schema, string table, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(table);
        const string sql = """
            SELECT
                s.name AS [Schema],
                t.name AS TableName,
                kc.name AS Name,
                0 AS TypeCode,
                NULL AS Definition,
                (
                    SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal)
                    FROM sys.index_columns ic
                    INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                    WHERE ic.object_id = kc.parent_object_id
                      AND ic.index_id = kc.unique_index_id
                      AND ic.is_included_column = 0
                ) AS ColumnList,
                CAST(0 AS BIT) AS IsDisabled,
                kc.is_system_named AS IsSystemNamed
            FROM sys.key_constraints kc
            INNER JOIN sys.objects t ON kc.parent_object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE kc.type = 'PK'
              AND s.name = @Schema AND t.name = @Table
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<ConstraintRow>(sql, new { Schema = schema, Table = table });
            return rows.Select(r => new ConstraintInfo(
                r.Schema, r.TableName, r.Name,
                ConstraintType.PrimaryKey, r.Definition,
                SplitCsv(r.ColumnList), r.IsDisabled, r.IsSystemNamed))
                .ToList().AsReadOnly();
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ForeignKeyInfo>> GetTableRelationshipsAsync(
        string schema, string table, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(table);
        const string sql = """
            SELECT
                SCHEMA_NAME(t.schema_id) AS [Schema],
                t.name AS TableName,
                fk.name AS Name,
                (
                    SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY fkc.constraint_column_id)
                    FROM sys.foreign_key_columns fkc
                    INNER JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
                    WHERE fkc.constraint_object_id = fk.object_id
                ) AS ColumnList,
                SCHEMA_NAME(rt.schema_id) AS ReferencedSchema,
                rt.name AS ReferencedTable,
                (
                    SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY fkc.constraint_column_id)
                    FROM sys.foreign_key_columns fkc
                    INNER JOIN sys.columns c ON fkc.referenced_object_id = c.object_id AND fkc.referenced_column_id = c.column_id
                    WHERE fkc.constraint_object_id = fk.object_id
                ) AS ReferencedColumnList,
                fk.delete_referential_action AS DeleteAction,
                fk.update_referential_action AS UpdateAction,
                fk.is_disabled AS IsDisabled,
                fk.is_not_trusted AS IsNotTrusted
            FROM sys.foreign_keys fk
            INNER JOIN sys.objects t ON fk.parent_object_id = t.object_id
            INNER JOIN sys.objects rt ON fk.referenced_object_id = rt.object_id
            WHERE SCHEMA_NAME(t.schema_id) = @Schema AND t.name = @Table
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<FkRow>(sql, new { Schema = schema, Table = table });
            return rows.Select(r => new ForeignKeyInfo(
                r.Schema, r.TableName, r.Name,
                SplitCsv(r.ColumnList),
                r.ReferencedSchema, r.ReferencedTable,
                SplitCsv(r.ReferencedColumnList),
                (ReferentialAction)r.DeleteAction,
                (ReferentialAction)r.UpdateAction,
                r.IsDisabled, r.IsNotTrusted))
                .ToList().AsReadOnly();
        }, cancellationToken);
    }

    public async Task<TableStatistics?> GetTableStatisticsAsync(
        string schema, string table, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(table);
        var sql = $"""
            EXEC sp_spaceused N'[{schema}].[{table}]'
            """;

        return await ExecuteAsync(async conn =>
        {
            using var multi = await conn.QueryMultipleAsync(sql);
            var row = await multi.ReadFirstOrDefaultAsync<SpaceRow>();
            if (row is null) return null;

            ParseSpaceValue(row.Reserved, out var reservedKb);
            ParseSpaceValue(row.Data, out var dataKb);
            ParseSpaceValue(row.IndexSize, out var indexKb);
            ParseSpaceValue(row.Unused, out var unusedKb);
            long.TryParse(row.Rows, out var rowCount);

            const string statsSql = """
                SELECT
                    STATS_DATE(object_id, stats_id) AS LastUpdated,
                    rowcnt AS PageCount,
                    0.0 AS FragmentationPercent
                FROM sys.stats
                WHERE object_id = OBJECT_ID(@ObjName)
                ORDER BY stats_id
                """;

            var statsRow = await conn.QueryFirstOrDefaultAsync<StatsRow>(
                statsSql, new { ObjName = $"[{schema}].[{table}]" });

            return new TableStatistics(
                schema, table, rowCount,
                reservedKb, dataKb, indexKb, unusedKb,
                statsRow?.LastUpdated,
                statsRow?.PageCount ?? 0,
                statsRow?.FragmentationPercent ?? 0);
        }, cancellationToken);
    }

    public async Task<long> GetRowCountAsync(
        string schema, string table, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(table);
        const string sql = """
            SELECT ISNULL(SUM(p.rows), 0)
            FROM sys.partitions p
            INNER JOIN sys.objects o ON p.object_id = o.object_id
            INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
            WHERE p.index_id IN (0,1) AND s.name = @Schema AND o.name = @Table
            """;

        return await ExecuteAsync(async conn =>
        {
            var result = await conn.ExecuteScalarAsync<long>(sql, new { Schema = schema, Table = table });
            return result;
        }, cancellationToken);
    }

    public async Task<QueryResult> SampleTableDataAsync(
        string schema, string table, int rowCount, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(table);
        rowCount = Math.Clamp(rowCount, 1, 1000);
        var sql = $"SELECT TOP (@RowCount) * FROM [{schema}].[{table}]";

        return await ExecuteAsync(async conn =>
        {
            var sw = Stopwatch.StartNew();
            var rows = (await conn.QueryAsync(sql, new { RowCount = rowCount })).ToList();
            sw.Stop();
            return BuildQueryResult(rows, sw.ElapsedMilliseconds);
        }, cancellationToken);
    }

    public async Task<QueryResult> SearchTableDataAsync(
        string schema, string table, string searchTerm, IEnumerable<string>? columns,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(table);

        var colList = columns?.ToList() ?? [];
        colList.ForEach(c => ValidateIdentifier(c));

        return await ExecuteAsync(async conn =>
        {
            // Get column list if not specified
            if (colList.Count == 0)
            {
                const string colSql = """
                    SELECT c.name FROM sys.columns c
                    INNER JOIN sys.objects o ON c.object_id = o.object_id
                    INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                    INNER JOIN sys.types tp ON c.user_type_id = tp.user_type_id
                    WHERE s.name = @Schema AND o.name = @Table
                      AND tp.name IN ('char','nchar','varchar','nvarchar','text','ntext')
                    """;
                colList = (await conn.QueryAsync<string>(colSql, new { Schema = schema, Table = table })).ToList();
            }

            if (colList.Count == 0)
                return new QueryResult([], [], 0, 0, 0, false, null);

            var conditions = string.Join(" OR ", colList.Select(c => $"[{c}] LIKE @Search"));
            var sql = $"SELECT TOP 100 * FROM [{schema}].[{table}] WHERE {conditions}";

            var sw = Stopwatch.StartNew();
            var rows = (await conn.QueryAsync(sql, new { Search = $"%{searchTerm}%" })).ToList();
            sw.Stop();
            return BuildQueryResult(rows, sw.ElapsedMilliseconds);
        }, cancellationToken);
    }

    private static QueryResult BuildQueryResult(List<dynamic> rows, long elapsedMs)
    {
        if (rows.Count == 0)
            return new QueryResult([], [], 0, elapsedMs, 0, false, null);

        var first = (IDictionary<string, object?>)rows[0];
        var columns = first.Keys.ToList().AsReadOnly();
        var mapped = rows
            .Select(r => (IReadOnlyDictionary<string, object?>)
                ((IDictionary<string, object?>)r).ToDictionary(kv => kv.Key, kv => kv.Value))
            .ToList().AsReadOnly();

        return new QueryResult(columns, mapped, rows.Count, elapsedMs, 0, false, null);
    }

    private static void ParseSpaceValue(string? value, out long kb)
    {
        kb = 0;
        if (string.IsNullOrWhiteSpace(value)) return;
        var num = value.Replace("KB", "", StringComparison.OrdinalIgnoreCase).Trim();
        long.TryParse(num, out kb);
    }

    private sealed class TableRow
    {
        public string Schema { get; init; } = "";
        public string Name { get; init; } = "";
        public string Type { get; init; } = "";
        public long RowCount { get; init; }
        public DateTime CreateDate { get; init; }
        public DateTime? ModifyDate { get; init; }
        public bool HasClusteredIndex { get; init; }
        public string? FileGroup { get; init; }
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

    private sealed class ConstraintRow
    {
        public string Schema { get; init; } = "";
        public string TableName { get; init; } = "";
        public string Name { get; init; } = "";
        public int TypeCode { get; init; }
        public string? Definition { get; init; }
        public string? ColumnList { get; init; }
        public bool IsDisabled { get; init; }
        public bool IsSystemNamed { get; init; }
    }

    private sealed class FkRow
    {
        public string Schema { get; init; } = "";
        public string TableName { get; init; } = "";
        public string Name { get; init; } = "";
        public string? ColumnList { get; init; }
        public string ReferencedSchema { get; init; } = "";
        public string ReferencedTable { get; init; } = "";
        public string? ReferencedColumnList { get; init; }
        public int DeleteAction { get; init; }
        public int UpdateAction { get; init; }
        public bool IsDisabled { get; init; }
        public bool IsNotTrusted { get; init; }
    }

    private sealed class SpaceRow
    {
        public string? Rows { get; init; }
        public string? Reserved { get; init; }
        public string? Data { get; init; }
        public string? IndexSize { get; init; }
        public string? Unused { get; init; }
    }

    private sealed class StatsRow
    {
        public DateTime? LastUpdated { get; init; }
        public long PageCount { get; init; }
        public double FragmentationPercent { get; init; }
    }
}
