namespace SqlMcpServer.Domain.ValueObjects;

public sealed record SchemaObjectName(string Schema, string Name)
{
    public string FullName => $"[{Schema}].[{Name}]";

    public override string ToString() => FullName;

    public static SchemaObjectName Parse(string fullName)
    {
        var parts = fullName.Trim('[', ']').Split("].[");
        if (parts.Length == 2)
            return new SchemaObjectName(parts[0], parts[1]);

        var dotParts = fullName.Split('.', 2);
        return dotParts.Length == 2
            ? new SchemaObjectName(dotParts[0].Trim('[', ']'), dotParts[1].Trim('[', ']'))
            : new SchemaObjectName("dbo", fullName.Trim('[', ']'));
    }
}
