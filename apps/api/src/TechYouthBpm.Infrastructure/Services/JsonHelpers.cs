using System.Text.Json;

namespace TechYouthBpm.Infrastructure.Services;

internal static class JsonHelpers
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string value, T fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return JsonSerializer.Deserialize<T>(value, Options) ?? fallback;
    }

    public static JsonElement ToElement(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return document.RootElement.Clone();
    }
}
