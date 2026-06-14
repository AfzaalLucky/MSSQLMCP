using Dapper;
using Polly;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Entities;
using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Infrastructure.Repositories;

internal sealed class IndexRepository : RepositoryBase, IIndexRepository
{
    public IndexRepository(IConnectionFactory connectionFactory, ResiliencePipeline pipeline)
        : base(connectionFactory, pipeline) { }

    public async Task<IReadOnlyList<IndexInfo>> GetIndexesAsync(
        string schema, string table, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(table);
        const string sql = """
            SELECT
                s.name              AS [Schema],
                t.name              AS [Table],
                i.name              AS Name,
                i.type              AS IndexTypeCode,
                i.is_unique         AS IsUnique,
                i.is_primary_key    AS IsPrimaryKey,
                i.is_disabled       AS IsDisabled,
                i.fill_factor       AS FillFactor,
                i.has_filter        AS HasFilter,
                i.filter_definition AS FilterDefinition,
                (
                    SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal)
                    FROM sys.index_columns ic
                    INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                    WHERE ic.object_id = i.object_id
                      AND ic.index_id = i.index_id
                      AND ic.is_included_column = 0
                ) AS ColumnList,
                (
                    SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal)
                    FROM sys.index_columns ic
                    INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                    WHERE ic.object_id = i.object_id
                      AND ic.index_id = i.index_id
                      AND ic.is_included_column = 1
                ) AS IncludedColumnList
            FROM sys.indexes i
            INNER JOIN sys.objects t ON i.object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE i.type > 0
              AND s.name = @Schema AND t.name = @Table
            ORDER BY i.index_id
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<IndexRow>(sql, new { Schema = schema, Table = table });
            return rows.Select(r => new IndexInfo(
                r.Schema, r.Table, r.Name,
                MapIndexType(r.IndexTypeCode),
                r.IsUnique, r.IsPrimaryKey, r.IsDisabled,
                SplitCsv(r.ColumnList),
                SplitCsv(r.IncludedColumnList),
                r.FillFactor, r.HasFilter, r.FilterDefinition))
                .ToList().AsReadOnly();
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<IndexInfo>> GetMissingIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                OBJECT_SCHEMA_NAME(d.object_id) AS [Schema],
                OBJECT_NAME(d.object_id)        AS [Table],
                'MISSING_IX_' + CAST(gs.group_handle AS VARCHAR) AS Name,
                2  AS IndexTypeCode,
                CAST(0 AS BIT)  AS IsUnique,
                CAST(0 AS BIT)  AS IsPrimaryKey,
                CAST(0 AS BIT)  AS IsDisabled,
                0   AS FillFactor,
                CAST(0 AS BIT)  AS HasFilter,
                NULL AS FilterDefinition,
                ISNULL(d.equality_columns, '') +
                    CASE WHEN d.inequality_columns IS NOT NULL THEN ',' + d.inequality_columns ELSE '' END
                    AS ColumnList,
                ISNULL(d.included_columns, '') AS IncludedColumnList
            FROM sys.dm_db_missing_index_groups g
            INNER JOIN sys.dm_db_missing_index_group_stats gs
                ON g.index_group_handle = gs.group_handle
            INNER JOIN sys.dm_db_missing_index_details d
                ON g.index_handle = d.index_handle
            WHERE d.database_id = DB_ID()
            ORDER BY gs.avg_total_user_cost * gs.avg_user_impact * (gs.user_seeks + gs.user_scans) DESC
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<IndexRow>(sql);
            return rows.Select(r => new IndexInfo(
                r.Schema, r.Table, r.Name,
                MapIndexType(r.IndexTypeCode),
                r.IsUnique, r.IsPrimaryKey, r.IsDisabled,
                SplitCsv(r.ColumnList),
                SplitCsv(r.IncludedColumnList),
                r.FillFactor, r.HasFilter, r.FilterDefinition))
                .ToList().AsReadOnly();
        }, cancellationToken);
    }

    private static IndexType MapIndexType(int code) => code switch
    {
        1 => IndexType.Clustered,
        3 => IndexType.XML,
        4 => IndexType.Spatial,
        5 => IndexType.ClusteredColumnStore,
        6 => IndexType.ColumnStore,
        _ => IndexType.NonClustered
    };

    private sealed class IndexRow
    {
        public string Schema { get; init; } = "";
        public string Table { get; init; } = "";
        public string Name { get; init; } = "";
        public int IndexTypeCode { get; init; }
        public bool IsUnique { get; init; }
        public bool IsPrimaryKey { get; init; }
        public bool IsDisabled { get; init; }
        public int FillFactor { get; init; }
        public bool HasFilter { get; init; }
        public string? FilterDefinition { get; init; }
        public string? ColumnList { get; init; }
        public string? IncludedColumnList { get; init; }
    }
}
