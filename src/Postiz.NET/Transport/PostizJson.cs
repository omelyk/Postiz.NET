using System.Text.Json;
using System.Text.Json.Serialization;

namespace Postiz.Transport;

internal static class PostizJson
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };
}
