using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Domain.Contracts.Repositories;

public interface ITypeRepository
{
    Task<IReadOnlyList<UserDefinedTypeInfo>> GetUserDefinedTypesAsync(string? schema, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TableTypeInfo>> GetTableTypesAsync(string? schema, CancellationToken cancellationToken = default);
    Task<UserDefinedTypeInfo?> DescribeUserDefinedTypeAsync(string schema, string name, CancellationToken cancellationToken = default);
    Task<TableTypeInfo?> DescribeTableTypeAsync(string schema, string name, CancellationToken cancellationToken = default);
    Task<string?> GetTypeDefinitionAsync(string schema, string name, CancellationToken cancellationToken = default);
}
