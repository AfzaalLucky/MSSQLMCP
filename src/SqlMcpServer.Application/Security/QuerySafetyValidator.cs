using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlMcpServer.Application.Configuration;
using SqlMcpServer.Domain.Contracts.Services;
using SqlMcpServer.Domain.Enums;
using SqlMcpServer.Domain.ValueObjects;

namespace SqlMcpServer.Application.Security;

public sealed class QuerySafetyValidator : IQuerySafetyValidator
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);

    private static readonly (Regex Pattern, string Reason)[] HardDenylist =
    [
        (new(@"\bDROP\s+(DATABASE|TABLE|SCHEMA|INDEX|VIEW|PROCEDURE|FUNCTION|TRIGGER|LOGIN|USER)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
            "DROP statements are not permitted"),

        (new(@"\bALTER\s+LOGIN\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
            "ALTER LOGIN is not permitted"),

        (new(@"\bSHUTDOWN\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
            "SHUTDOWN is not permitted"),

        (new(@"\bxp_cmdshell\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
            "xp_cmdshell is not permitted"),

        (new(@"\bEXEC\s*\(\s*@\w+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
            "Dynamic SQL execution via EXEC(@variable) is not permitted"),

        (new(@"\bsp_configure\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
            "sp_configure is not permitted"),

        (new(@"\b(OPENROWSET|OPENDATASOURCE|OPENQUERY)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
            "Linked server / external data source functions are not permitted"),

        (new(@"\bBULK\s+INSERT\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
            "BULK INSERT is not permitted"),

        (new(@"\b(RESTORE|BACKUP)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
            "BACKUP and RESTORE are not permitted"),

        (new(@"\bTRUNCATE\s+TABLE\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
            "TRUNCATE TABLE is not permitted"),

        (new(@"\bRECONFIGURE\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
            "RECONFIGURE is not permitted"),

        (new(@"\bCREATE\s+(LOGIN|USER|SERVER)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout),
            "Creating logins, users, or server objects is not permitted"),
    ];

    private static readonly Regex SelectPattern =
        new(@"^\s*(SELECT|WITH)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex InsertPattern =
        new(@"^\s*INSERT\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex UpdatePattern =
        new(@"^\s*UPDATE\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex DeletePattern =
        new(@"^\s*DELETE\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex MergePattern =
        new(@"^\s*MERGE\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex ExecPattern =
        new(@"^\s*EXEC(UTE)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex DdlPattern =
        new(@"^\s*(CREATE|ALTER|DROP)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);
    private static readonly Regex AdminPattern =
        new(@"^\s*(BACKUP|RESTORE|SHUTDOWN|RECONFIGURE|DBCC)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);

    private readonly SecuritySettings _settings;
    private readonly ILogger<QuerySafetyValidator> _logger;

    public QuerySafetyValidator(
        IOptions<SecuritySettings> settings,
        ILogger<QuerySafetyValidator> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public QuerySafetyResult Validate(string sql, UserRole userRole)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return QuerySafetyResult.Deny("SQL query cannot be empty", StatementType.Unknown);

        if (!_settings.EnableQuerySafetyValidation)
            return QuerySafetyResult.Allow(DetectStatementType(sql));

        var statementType = DetectStatementType(sql);

        // Hard denylist always applies regardless of role
        foreach (var (pattern, reason) in HardDenylist)
        {
            if (pattern.IsMatch(sql))
            {
                _logger.LogWarning("SQL safety violation [{Role}]: {Reason}", userRole, reason);
                return QuerySafetyResult.Deny(reason, statementType);
            }
        }

        // Configurable denied keywords
        foreach (var keyword in _settings.DeniedKeywords)
        {
            if (sql.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                var reason = $"SQL contains denied keyword: {keyword}";
                _logger.LogWarning("SQL safety violation [{Role}]: {Reason}", userRole, reason);
                return QuerySafetyResult.Deny(reason, statementType);
            }
        }

        // Role-based checks
        switch (userRole)
        {
            case UserRole.ReadOnly or UserRole.Auditor:
                if (statementType is not (StatementType.Select or StatementType.Execute))
                    return QuerySafetyResult.Deny(
                        $"{userRole} role cannot execute {statementType} statements", statementType);
                break;

            case UserRole.Developer:
                if (statementType is StatementType.DDL or StatementType.Admin)
                    return QuerySafetyResult.Deny(
                        $"Developer role cannot execute {statementType} statements", statementType);
                if (statementType is StatementType.Insert or StatementType.Update
                    or StatementType.Delete or StatementType.Merge
                    && !_settings.AllowWriteOperations)
                    return QuerySafetyResult.Deny(
                        "Write operations are disabled in security settings", statementType);
                break;

            case UserRole.DBA:
                if (statementType is StatementType.Admin && !_settings.AllowDdl)
                    return QuerySafetyResult.Deny(
                        "Administrative operations are not permitted", statementType);
                break;
        }

        return QuerySafetyResult.Allow(statementType);
    }

    public StatementType DetectStatementType(string sql)
    {
        var s = sql.TrimStart();
        if (SelectPattern.IsMatch(s)) return StatementType.Select;
        if (InsertPattern.IsMatch(s)) return StatementType.Insert;
        if (UpdatePattern.IsMatch(s)) return StatementType.Update;
        if (DeletePattern.IsMatch(s)) return StatementType.Delete;
        if (MergePattern.IsMatch(s)) return StatementType.Merge;
        if (ExecPattern.IsMatch(s)) return StatementType.Execute;
        if (DdlPattern.IsMatch(s)) return StatementType.DDL;
        if (AdminPattern.IsMatch(s)) return StatementType.Admin;
        return StatementType.Unknown;
    }
}
