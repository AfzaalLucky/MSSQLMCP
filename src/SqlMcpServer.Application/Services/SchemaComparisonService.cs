using System.Text;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Contracts.Services;
using SqlMcpServer.Domain.ValueObjects;

namespace SqlMcpServer.Application.Services;

public sealed class SchemaComparisonService : ISchemaComparisonService
{
    private readonly ITableRepository _tableRepo;
    private readonly IViewRepository _viewRepo;

    public SchemaComparisonService(ITableRepository tableRepo, IViewRepository viewRepo)
    {
        _tableRepo = tableRepo;
        _viewRepo = viewRepo;
    }

    public async Task<SchemaComparisonResult> CompareSchemasAsync(
        string sourceDatabase, string sourceSchema,
        string targetDatabase, string targetSchema,
        CancellationToken ct = default)
    {
        var (srcTables, tgtTables, srcViews, tgtViews) = await (
            _tableRepo.GetTablesAsync(sourceDatabase, sourceSchema, ct),
            _tableRepo.GetTablesAsync(targetDatabase, targetSchema, ct),
            _viewRepo.GetViewsAsync(sourceDatabase, sourceSchema, ct),
            _viewRepo.GetViewsAsync(targetDatabase, targetSchema, ct)
        ).WhenAll4();

        var srcNames = srcTables.Select(t => new SchemaObjectName(t.Schema, t.Name)).ToHashSet();
        var tgtNames = tgtTables.Select(t => new SchemaObjectName(t.Schema, t.Name)).ToHashSet();
        var srcViewNames = srcViews.Select(v => new SchemaObjectName(v.Schema, v.Name)).ToHashSet();
        var tgtViewNames = tgtViews.Select(v => new SchemaObjectName(v.Schema, v.Name)).ToHashSet();

        var added = tgtNames.Except(srcNames).Concat(tgtViewNames.Except(srcViewNames)).ToList().AsReadOnly();
        var removed = srcNames.Except(tgtNames).Concat(srcViewNames.Except(tgtViewNames)).ToList().AsReadOnly();

        // Detect modified tables by column diff
        var common = srcNames.Intersect(tgtNames).ToList();
        var modified = new List<SchemaObjectName>();
        foreach (var name in common)
        {
            var srcCols = await _tableRepo.GetTableColumnsAsync(name.Schema, name.Name, ct);
            var tgtCols = await _tableRepo.GetTableColumnsAsync(name.Schema, name.Name, ct);
            var srcColNames = srcCols.Select(c => c.ColumnName + ":" + c.DataType).ToHashSet();
            var tgtColNames = tgtCols.Select(c => c.ColumnName + ":" + c.DataType).ToHashSet();
            if (!srcColNames.SetEquals(tgtColNames))
                modified.Add(name);
        }

        var script = GenerateMigrationScript(added, removed, modified, targetSchema);
        return new SchemaComparisonResult(added, removed, modified.AsReadOnly(), script);
    }

    public async Task<SchemaComparisonResult> CompareDatabasesAsync(
        string sourceDatabase, string targetDatabase, CancellationToken ct = default)
    {
        return await CompareSchemasAsync(sourceDatabase, "dbo", targetDatabase, "dbo", ct);
    }

    public async Task<string> GenerateMigrationScriptAsync(
        string sourceDatabase, string sourceSchema,
        string targetDatabase, string targetSchema,
        CancellationToken ct = default)
    {
        var result = await CompareSchemasAsync(sourceDatabase, sourceSchema, targetDatabase, targetSchema, ct);
        return result.MigrationScript;
    }

    private static string GenerateMigrationScript(
        IReadOnlyList<SchemaObjectName> added,
        IReadOnlyList<SchemaObjectName> removed,
        IReadOnlyList<SchemaObjectName> modified,
        string targetSchema)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- Auto-generated migration script");
        sb.AppendLine($"-- Generated: {DateTime.UtcNow:O}");
        sb.AppendLine();

        if (removed.Count > 0)
        {
            sb.AppendLine("-- Objects removed from source:");
            foreach (var obj in removed)
                sb.AppendLine($"-- DROP TABLE {obj.FullName};");
            sb.AppendLine();
        }

        if (added.Count > 0)
        {
            sb.AppendLine("-- Objects added in target (create in source):");
            foreach (var obj in added)
                sb.AppendLine($"-- CREATE TABLE {obj.FullName} ( /* columns */ );");
            sb.AppendLine();
        }

        if (modified.Count > 0)
        {
            sb.AppendLine("-- Modified objects (review changes):");
            foreach (var obj in modified)
                sb.AppendLine($"-- ALTER TABLE {obj.FullName} /* review column differences */;");
            sb.AppendLine();
        }

        if (added.Count == 0 && removed.Count == 0 && modified.Count == 0)
            sb.AppendLine("-- No differences found. Schemas are identical.");

        return sb.ToString();
    }
}

file static class TaskTuple4Extensions
{
    public static async Task<(T1, T2, T3, T4)> WhenAll4<T1, T2, T3, T4>(
        this (Task<T1>, Task<T2>, Task<T3>, Task<T4>) tasks)
    {
        await Task.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4);
        return (tasks.Item1.Result, tasks.Item2.Result, tasks.Item3.Result, tasks.Item4.Result);
    }
}
