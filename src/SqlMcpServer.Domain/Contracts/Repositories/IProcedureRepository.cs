using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Domain.Contracts.Repositories;

public interface IProcedureRepository
{
    Task<IReadOnlyList<ProcedureInfo>> GetProceduresAsync(string database, string? schema, CancellationToken cancellationToken = default);
    Task<ProcedureInfo?> DescribeProcedureAsync(string schema, string name, CancellationToken cancellationToken = default);
    Task<string?> GetProcedureDefinitionAsync(string schema, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcedureParameter>> GetProcedureParametersAsync(string schema, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DependencyInfo>> AnalyzeProcedureDependenciesAsync(string schema, string name, CancellationToken cancellationToken = default);
}
