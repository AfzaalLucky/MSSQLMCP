using Dapper;
using Polly;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Entities;
using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Infrastructure.Repositories;

internal sealed class ConstraintRepository : RepositoryBase, IConstraintRepository
{
    public ConstraintRepository(IConnectionFactory connectionFactory, ResiliencePipeline pipeline)
        : base(connectionFactory, pipeline) { }

    public async Task<IReadOnlyList<ConstraintInfo>> GetConstraintsAsync(
        string schema, string table, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(table);

        const string sql = """
            SELECT s.name AS [Schema], t.name AS TableName, kc.name AS Name,
                CASE kc.type WHEN 'PK' THEN 0 ELSE 2 END AS TypeCode,
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
            WHERE s.name = @Schema AND t.name = @Table

            UNION ALL

            SELECT s.name, t.name, cc.name,
                3 AS TypeCode,
                cc.definition,
                '' AS ColumnList,
                cc.is_disabled,
                cc.is_system_named
            FROM sys.check_constraints cc
            INNER JOIN sys.objects t ON cc.parent_object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = @Schema AND t.name = @Table

            UNION ALL

            SELECT s.name, t.name, dc.name,
                4 AS TypeCode,
                dc.definition,
                col.name AS ColumnList,
                CAST(0 AS BIT) AS IsDisabled,
                dc.is_system_named
            FROM sys.default_constraints dc
            INNER JOIN sys.objects t ON dc.parent_object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            INNER JOIN sys.columns col
                ON dc.parent_object_id = col.object_id AND dc.parent_column_id = col.column_id
            WHERE s.name = @Schema AND t.name = @Table

            ORDER BY TypeCode, Name
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<ConstraintRow>(sql, new { Schema = schema, Table = table });
            return rows.Select(r => new ConstraintInfo(
                r.Schema, r.TableName, r.Name,
                (ConstraintType)r.TypeCode,
                r.Definition,
                SplitCsv(r.ColumnList),
                r.IsDisabled, r.IsSystemNamed))
                .ToList().AsReadOnly();
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ForeignKeyInfo>> GetForeignKeysAsync(
        string schema, string table, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(table);
        const string sql = """
            SELECT
                SCHEMA_NAME(t.schema_id)    AS [Schema],
                t.name                      AS TableName,
                fk.name                     AS Name,
                (
                    SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY fkc.constraint_column_id)
                    FROM sys.foreign_key_columns fkc
                    INNER JOIN sys.columns c
                        ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
                    WHERE fkc.constraint_object_id = fk.object_id
                ) AS ColumnList,
                SCHEMA_NAME(rt.schema_id)   AS ReferencedSchema,
                rt.name                     AS ReferencedTable,
                (
                    SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY fkc.constraint_column_id)
                    FROM sys.foreign_key_columns fkc
                    INNER JOIN sys.columns c
                        ON fkc.referenced_object_id = c.object_id AND fkc.referenced_column_id = c.column_id
                    WHERE fkc.constraint_object_id = fk.object_id
                ) AS ReferencedColumnList,
                fk.delete_referential_action    AS DeleteAction,
                fk.update_referential_action    AS UpdateAction,
                fk.is_disabled                  AS IsDisabled,
                fk.is_not_trusted               AS IsNotTrusted
            FROM sys.foreign_keys fk
            INNER JOIN sys.objects t ON fk.parent_object_id = t.object_id
            INNER JOIN sys.objects rt ON fk.referenced_object_id = rt.object_id
            WHERE SCHEMA_NAME(t.schema_id) = @Schema AND t.name = @Table
            ORDER BY fk.name
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

    public async Task<IReadOnlyList<SequenceInfo>> GetSequencesAsync(
        string? schema, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                SCHEMA_NAME(seq.schema_id)  AS [Schema],
                seq.name                    AS Name,
                tp.name                     AS DataType,
                CAST(seq.start_value AS BIGINT)     AS StartValue,
                CAST(seq.increment AS BIGINT)       AS Increment,
                CAST(seq.minimum_value AS BIGINT)   AS MinValue,
                CAST(seq.maximum_value AS BIGINT)   AS MaxValue,
                seq.is_cycling              AS IsCycling,
                seq.is_cached               AS IsCached,
                seq.cache_size              AS CacheSize,
                CAST(seq.current_value AS BIGINT)   AS CurrentValue
            FROM sys.sequences seq
            INNER JOIN sys.types tp ON seq.user_type_id = tp.user_type_id
            WHERE (@Schema IS NULL OR SCHEMA_NAME(seq.schema_id) = @Schema)
            ORDER BY SCHEMA_NAME(seq.schema_id), seq.name
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<SeqRow>(sql, new { Schema = schema });
            return rows.Select(r => new SequenceInfo(
                r.Schema, r.Name, r.DataType,
                r.StartValue, r.Increment, r.MinValue, r.MaxValue,
                r.IsCycling, r.IsCached, r.CacheSize, r.CurrentValue))
                .ToList().AsReadOnly();
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<SynonymInfo>> GetSynonymsAsync(
        string? schema, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                SCHEMA_NAME(sy.schema_id)   AS [Schema],
                sy.name                     AS Name,
                sy.base_object_name         AS BaseObject,
                sy.create_date              AS CreateDate,
                sy.modify_date              AS ModifyDate
            FROM sys.synonyms sy
            WHERE (@Schema IS NULL OR SCHEMA_NAME(sy.schema_id) = @Schema)
            ORDER BY SCHEMA_NAME(sy.schema_id), sy.name
            """;

        return await ExecuteAsync(async conn =>
        {
            var rows = await conn.QueryAsync<SynRow>(sql, new { Schema = schema });
            return rows.Select(r => new SynonymInfo(r.Schema, r.Name, r.BaseObject, r.CreateDate, r.ModifyDate))
                .ToList().AsReadOnly();
        }, cancellationToken);
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

    private sealed class SeqRow
    {
        public string Schema { get; init; } = "";
        public string Name { get; init; } = "";
        public string DataType { get; init; } = "";
        public long StartValue { get; init; }
        public long Increment { get; init; }
        public long MinValue { get; init; }
        public long MaxValue { get; init; }
        public bool IsCycling { get; init; }
        public bool IsCached { get; init; }
        public int? CacheSize { get; init; }
        public long CurrentValue { get; init; }
    }

    private sealed class SynRow
    {
        public string Schema { get; init; } = "";
        public string Name { get; init; } = "";
        public string BaseObject { get; init; } = "";
        public DateTime CreateDate { get; init; }
        public DateTime? ModifyDate { get; init; }
    }
}
