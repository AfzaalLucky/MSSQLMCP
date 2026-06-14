namespace SqlMcpServer.CrossCutting.Correlation;

/// <summary>
/// Scoped per-operation correlation ID, propagated via AsyncLocal so it flows
/// through async continuations without any HTTP dependency.
/// </summary>
public sealed class CorrelationContext
{
    private static readonly AsyncLocal<string?> _current = new();

    public string CorrelationId => _current.Value ??= NewId();

    public void Set(string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        _current.Value = correlationId;
    }

    public void Reset() => _current.Value = null;

    private static string NewId() => Guid.NewGuid().ToString("N");
}
