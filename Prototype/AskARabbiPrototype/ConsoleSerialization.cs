using System.Text.Json;
using System.Text.Json.Serialization;

namespace AskARabbiPrototype;

internal static class ConsoleSerialization
{
    internal static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
