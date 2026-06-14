namespace SqlMcpServer.Application.Configuration;

public sealed class SecuritySettings
{
    public bool EnableQuerySafetyValidation { get; set; } = true;
    public bool AllowWriteOperations { get; set; } = false;
    public int MaxResultRows { get; set; } = 10_000;
    public int MaxQueryTimeoutSeconds { get; set; } = 300;
    public string[] DeniedKeywords { get; set; } =
    [
        "xp_cmdshell", "sp_configure", "OPENROWSET", "OPENDATASOURCE",
        "OPENQUERY", "SHUTDOWN", "RECONFIGURE", "BULK INSERT"
    ];
    public bool AllowDdl { get; set; } = false;
    public bool AllowDml { get; set; } = false;
}
