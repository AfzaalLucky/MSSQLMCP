namespace SqlMcpServer.Domain.Enums;

[Flags]
public enum TriggerEvent
{
    None   = 0,
    Insert = 1,
    Update = 2,
    Delete = 4
}
