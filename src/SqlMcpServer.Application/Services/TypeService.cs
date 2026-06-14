using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Application.Services;

public sealed class TypeService
{
    private readonly ITypeRepository _repo;

    public TypeService(ITypeRepository repo) => _repo = repo;

    public Task<UserDefinedTypeInfo?> DescribeUserDefinedTypeAsync(
        string schema, string name, CancellationToken ct = default) =>
        _repo.DescribeUserDefinedTypeAsync(schema, name, ct);

    public Task<TableTypeInfo?> DescribeTableTypeAsync(
        string schema, string name, CancellationToken ct = default) =>
        _repo.DescribeTableTypeAsync(schema, name, ct);

    public Task<string?> GetTypeDefinitionAsync(
        string schema, string name, CancellationToken ct = default) =>
        _repo.GetTypeDefinitionAsync(schema, name, ct);
}
