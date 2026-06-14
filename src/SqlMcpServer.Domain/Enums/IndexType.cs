namespace SqlMcpServer.Domain.Enums;

public enum IndexType
{
    Clustered,
    NonClustered,
    ColumnStore,
    ClusteredColumnStore,
    XML,
    Spatial,
    FullText,
    Hash
}
