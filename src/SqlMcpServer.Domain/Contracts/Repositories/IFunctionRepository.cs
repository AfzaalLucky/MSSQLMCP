using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Domain.Contracts.Repositories;

public interface IFunctionRepository
{
    Task<IReadOnlyList<FunctionInfo>> GetFunctionsAsync(string database, string? schema, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FunctionInfo>> GetScalarFunctionsAsync(string? schema, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FunctionInfo>> GetTableValuedFunctionsAsync(string? schema, CancellationToken cancellationToken = default);
    Task<FunctionInfo?> DescribeFunctionAsync(string schema, string name, CancellationToken cancellationToken = default);
    Task<string?> GetFunctionDefinitionAsync(string schema, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DependencyInfo>> AnalyzeFunctionDependenciesAsync(string schema, string name, CancellationToken cancellationToken = default);
}
