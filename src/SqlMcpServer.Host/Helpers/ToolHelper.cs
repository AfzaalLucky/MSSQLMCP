using System.Text.Json;
using System.Text.Json.Serialization;

namespace SqlMcpServer.Host.Helpers;

internal static class ToolHelper
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    internal static string Serialize(object? value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    internal static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { error = code, message }, JsonOptions);

    internal static string NotFound(string objectName) =>
        JsonSerializer.Serialize(new { error = "NotFound", message = $"'{objectName}' was not found." }, JsonOptions);

    internal static string Success(string message) =>
        JsonSerializer.Serialize(new { success = true, message }, JsonOptions);
}
