using System.Text.Json;
using System.Text.Json.Serialization;

namespace Odin.Api.Services.Paddle;

/// <summary>
/// Serialization options for the Paddle API. Paddle uses snake_case for JSON properties,
/// returns RFC 3339 timestamps, and tolerates unknown fields. Enums are serialized as strings
/// because Paddle returns string values (and may add new ones without a version bump).
/// </summary>
public static class PaddleJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false),
        },
    };
}
