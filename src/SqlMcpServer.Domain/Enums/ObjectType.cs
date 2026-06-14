namespace SqlMcpServer.Domain.Enums;

public enum ObjectType
{
    Table,
    View,
    Procedure,
    ScalarFunction,
    TVF,
    InlineTVF,
    Trigger,
    UDT,
    TableType,
    Sequence,
    Synonym,
    Index,
    Constraint,
    ForeignKey,
    Unknown
}
