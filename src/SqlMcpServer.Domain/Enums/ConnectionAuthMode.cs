namespace SqlMcpServer.Domain.Enums;

public enum ConnectionAuthMode
{
    WindowsAuth,
    SqlAuth,
    AzureManagedIdentity,
    AzureAD
}
