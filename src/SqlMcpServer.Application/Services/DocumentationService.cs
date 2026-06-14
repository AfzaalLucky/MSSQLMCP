using System.Text;
using System.Text.Json;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Contracts.Services;
using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Application.Services;

public sealed class DocumentationService : IDocumentationService
{
    private readonly ISchemaRepository _schemaRepo;
    private readonly ITableRepository _tableRepo;
    private readonly IViewRepository _viewRepo;
    private readonly IProcedureRepository _procRepo;
    private readonly IFunctionRepository _funcRepo;

    public DocumentationService(
        ISchemaRepository schemaRepo,
        ITableRepository tableRepo,
        IViewRepository viewRepo,
        IProcedureRepository procRepo,
        IFunctionRepository funcRepo)
    {
        _schemaRepo = schemaRepo;
        _tableRepo = tableRepo;
        _viewRepo = viewRepo;
        _procRepo = procRepo;
        _funcRepo = funcRepo;
    }

    public async Task<string> GenerateDatabaseDocumentationAsync(
        string database, DocumentFormat format, CancellationToken ct = default)
    {
        var schemas = await _schemaRepo.GetSchemasAsync(database, ct);
        var tables = await _tableRepo.GetTablesAsync(database, null, ct);
        var views = await _viewRepo.GetViewsAsync(database, null, ct);
        var procs = await _procRepo.GetProceduresAsync(database, null, ct);
        var funcs = await _funcRepo.GetFunctionsAsync(database, null, ct);

        return format switch
        {
            DocumentFormat.Json => JsonSerializer.Serialize(new
            {
                Database = database,
                Schemas = schemas.Select(s => s.Name),
                TableCount = tables.Count,
                ViewCount = views.Count,
                ProcedureCount = procs.Count,
                FunctionCount = funcs.Count,
                Tables = tables,
                Views = views.Select(v => new { v.Schema, v.Name }),
                Procedures = procs.Select(p => new { p.Schema, p.Name }),
                Functions = funcs.Select(f => new { f.Schema, f.Name, Type = f.Type.ToString() })
            }, new JsonSerializerOptions { WriteIndented = true }),

            _ => BuildMarkdownDatabase(database, schemas.Select(s => s.Name).ToList(),
                tables, views, procs, funcs)
        };
    }

    public async Task<string> GenerateSchemaDocumentationAsync(
        string schema, DocumentFormat format, CancellationToken ct = default)
    {
        var tables = await _tableRepo.GetTablesAsync("", schema, ct);
        var views = await _viewRepo.GetViewsAsync("", schema, ct);
        var procs = await _procRepo.GetProceduresAsync("", schema, ct);
        var funcs = await _funcRepo.GetFunctionsAsync("", schema, ct);

        return format == DocumentFormat.Json
            ? JsonSerializer.Serialize(new { Schema = schema, Tables = tables, Views = views, Procedures = procs, Functions = funcs },
                new JsonSerializerOptions { WriteIndented = true })
            : BuildMarkdownSchema(schema, tables, views, procs, funcs);
    }

    public async Task<string> GenerateTableDocumentationAsync(
        string schema, string table, CancellationToken ct = default)
    {
        var tableInfo = await _tableRepo.DescribeTableAsync(schema, table, ct);
        if (tableInfo is null) return $"# Table Not Found\n\n`[{schema}].[{table}]` does not exist.";

        var columns = await _tableRepo.GetTableColumnsAsync(schema, table, ct);
        var rowCount = await _tableRepo.GetRowCountAsync(schema, table, ct);

        var sb = new StringBuilder();
        sb.AppendLine($"# Table: [{schema}].[{table}]");
        sb.AppendLine();
        sb.AppendLine($"**Rows:** {rowCount:N0}  |  **Created:** {tableInfo.CreateDate:yyyy-MM-dd}  |  **Modified:** {tableInfo.ModifyDate:yyyy-MM-dd}");
        sb.AppendLine();
        sb.AppendLine("## Columns");
        sb.AppendLine();
        sb.AppendLine("| # | Name | Type | Nullable | Identity | Default |");
        sb.AppendLine("|---|------|------|----------|----------|---------|");
        foreach (var col in columns)
        {
            var typeDef = FormatType(col.DataType, col.MaxLength, col.Precision, col.Scale);
            sb.AppendLine($"| {col.OrdinalPosition} | `{col.ColumnName}` | `{typeDef}` | {YN(col.IsNullable)} | {YN(col.IsIdentity)} | {col.DefaultValue ?? ""} |");
        }

        return sb.ToString();
    }

    public async Task<string> GenerateApiDocumentationAsync(
        string database, CancellationToken ct = default)
    {
        var procs = await _procRepo.GetProceduresAsync(database, null, ct);
        var funcs = await _funcRepo.GetFunctionsAsync(database, null, ct);

        var sb = new StringBuilder();
        sb.AppendLine($"# API Documentation: {database}");
        sb.AppendLine();
        sb.AppendLine("## Stored Procedures");
        sb.AppendLine();
        foreach (var proc in procs)
        {
            sb.AppendLine($"### `[{proc.Schema}].[{proc.Name}]`");
            if (proc.Parameters.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("**Parameters:**");
                sb.AppendLine();
                sb.AppendLine("| Name | Type | Mode | Nullable |");
                sb.AppendLine("|------|------|------|----------|");
                foreach (var p in proc.Parameters)
                    sb.AppendLine($"| `{p.Name}` | `{p.DataType}` | {p.ParameterMode} | {YN(!p.IsReadOnly)} |");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Functions");
        sb.AppendLine();
        foreach (var fn in funcs)
        {
            sb.AppendLine($"### `[{fn.Schema}].[{fn.Name}]` — {fn.Type}");
            if (fn.ReturnType is not null)
                sb.AppendLine($"**Returns:** `{fn.ReturnType}`");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildMarkdownDatabase(
        string database, IList<string> schemas,
        IReadOnlyList<Domain.Entities.TableInfo> tables,
        IReadOnlyList<Domain.Entities.ViewInfo> views,
        IReadOnlyList<Domain.Entities.ProcedureInfo> procs,
        IReadOnlyList<Domain.Entities.FunctionInfo> funcs)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Database: {database}");
        sb.AppendLine();
        sb.AppendLine($"**Schemas:** {string.Join(", ", schemas)}  ");
        sb.AppendLine($"**Tables:** {tables.Count}  |  **Views:** {views.Count}  |  **Procedures:** {procs.Count}  |  **Functions:** {funcs.Count}");
        sb.AppendLine();

        if (tables.Count > 0)
        {
            sb.AppendLine("## Tables");
            sb.AppendLine();
            sb.AppendLine("| Schema | Name | Rows | Created |");
            sb.AppendLine("|--------|------|------|---------|");
            foreach (var t in tables)
                sb.AppendLine($"| {t.Schema} | `{t.Name}` | {t.RowCount:N0} | {t.CreateDate:yyyy-MM-dd} |");
            sb.AppendLine();
        }

        if (views.Count > 0)
        {
            sb.AppendLine("## Views");
            sb.AppendLine();
            foreach (var v in views)
                sb.AppendLine($"- `[{v.Schema}].[{v.Name}]`");
            sb.AppendLine();
        }

        if (procs.Count > 0)
        {
            sb.AppendLine("## Stored Procedures");
            sb.AppendLine();
            foreach (var p in procs)
                sb.AppendLine($"- `[{p.Schema}].[{p.Name}]`");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildMarkdownSchema(
        string schema,
        IReadOnlyList<Domain.Entities.TableInfo> tables,
        IReadOnlyList<Domain.Entities.ViewInfo> views,
        IReadOnlyList<Domain.Entities.ProcedureInfo> procs,
        IReadOnlyList<Domain.Entities.FunctionInfo> funcs)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Schema: [{schema}]");
        sb.AppendLine();
        sb.AppendLine($"**Tables:** {tables.Count}  |  **Views:** {views.Count}  |  **Procedures:** {procs.Count}  |  **Functions:** {funcs.Count}");
        sb.AppendLine();
        foreach (var t in tables) sb.AppendLine($"- Table: `{t.Name}` ({t.RowCount:N0} rows)");
        foreach (var v in views) sb.AppendLine($"- View: `{v.Name}`");
        foreach (var p in procs) sb.AppendLine($"- Procedure: `{p.Name}`");
        foreach (var f in funcs) sb.AppendLine($"- Function: `{f.Name}` ({f.Type})");
        return sb.ToString();
    }

    private static string FormatType(string type, int? maxLen, int? precision, int? scale)
    {
        if (maxLen.HasValue && maxLen > 0 && type is "varchar" or "nvarchar" or "char" or "nchar")
            return $"{type}({(maxLen == -1 ? "MAX" : maxLen.ToString())})";
        if (precision.HasValue && scale.HasValue && type is "decimal" or "numeric")
            return $"{type}({precision},{scale})";
        return type;
    }

    private static string YN(bool value) => value ? "YES" : "NO";
}
