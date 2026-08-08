using System.Text.Json;
using System.Text.Json.Nodes;

namespace Postiz.Transport;

internal static class PostizErrorSanitizer
{
    private static readonly string[] SensitiveKeys =
        ["authorization", "token", "apikey", "api_key", "secret", "password", "media", "image"];

    internal static string Redact(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        try
        {
            var node = JsonNode.Parse(body);
            RedactNode(node);
            return Truncate(node?.ToJsonString() ?? string.Empty);
        }
        catch (JsonException)
        {
            return "[non-JSON response body redacted]";
        }
    }

    internal static string? ExtractCode(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static void RedactNode(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToArray())
            {
                if (SensitiveKeys.Any(key => property.Key.Contains(key, StringComparison.OrdinalIgnoreCase)))
                {
                    obj[property.Key] = "[redacted]";
                }
                else
                {
                    RedactNode(property.Value);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                RedactNode(child);
            }
        }
    }

    private static string Truncate(string value) => value.Length <= 2_048 ? value : value[..2_048] + "…";
}
