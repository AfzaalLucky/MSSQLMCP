using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Domain.Contracts.Services;

public interface IDocumentationService
{
    Task<string> GenerateDatabaseDocumentationAsync(string database, DocumentFormat format, CancellationToken cancellationToken = default);
    Task<string> GenerateSchemaDocumentationAsync(string schema, DocumentFormat format, CancellationToken cancellationToken = default);
    Task<string> GenerateTableDocumentationAsync(string schema, string table, CancellationToken cancellationToken = default);
    Task<string> GenerateApiDocumentationAsync(string database, CancellationToken cancellationToken = default);
}
