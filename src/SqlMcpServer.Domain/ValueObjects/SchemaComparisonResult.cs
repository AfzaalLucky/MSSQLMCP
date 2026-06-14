namespace SqlMcpServer.Domain.ValueObjects;

public sealed record SchemaComparisonResult(
    IReadOnlyList<SchemaObjectName> Added,
    IReadOnlyList<SchemaObjectName> Removed,
    IReadOnlyList<SchemaObjectName> Modified,
    string MigrationScript)
{
    public bool HasDifferences => Added.Count > 0 || Removed.Count > 0 || Modified.Count > 0;
}
