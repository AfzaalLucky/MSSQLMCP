namespace SqlMcpServer.Domain.Entities;

public sealed record SequenceInfo(
    string Schema,
    string Name,
    string DataType,
    long StartValue,
    long Increment,
    long MinValue,
    long MaxValue,
    bool IsCycling,
    bool IsCached,
    int? CacheSize,
    long CurrentValue)
{
    public string FullName => $"[{Schema}].[{Name}]";
}
