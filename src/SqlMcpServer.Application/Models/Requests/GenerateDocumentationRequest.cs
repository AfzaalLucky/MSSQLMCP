using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Application.Models.Requests;

public sealed record GenerateDocumentationRequest(
    string? Database = null,
    string? Schema = null,
    string? Table = null,
    DocumentFormat Format = DocumentFormat.Markdown,
    bool IncludeTables = true,
    bool IncludeViews = true,
    bool IncludeProcedures = true,
    bool IncludeFunctions = true);
