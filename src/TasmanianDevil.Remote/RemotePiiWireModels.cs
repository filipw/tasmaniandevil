using System.Text.Json.Serialization;

namespace TasmanianDevil.Remote;

/// <summary>
/// Wire request body for the generic remote PII detection contract:
/// <c>POST {endpoint}</c> with <c>{ "text", "language", "entities" }</c>.
/// </summary>
internal sealed class RemotePiiDetectionRequest
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("language")]
    public required string Language { get; init; }

    [JsonPropertyName("entities")]
    public required IReadOnlyList<string> Entities { get; init; }

    /// <summary>Only sent when <see cref="RemotePiiOptions.RequestRedactedTextPassthrough"/> is true.</summary>
    [JsonPropertyName("includeRedactedText")]
    public bool? IncludeRedactedText { get; init; }
}

/// <summary>
/// Wire response body: <c>{ "entities": [{ "type", "start", "end", "score" }] }</c>, plus an optional
/// <c>redactedText</c> field mirrored from <see cref="RemotePiiDetectionRequest.IncludeRedactedText"/>.
/// </summary>
internal sealed class RemotePiiDetectionResponse
{
    [JsonPropertyName("entities")]
    public List<RemotePiiEntityDto> Entities { get; init; } = [];

    [JsonPropertyName("redactedText")]
    public string? RedactedText { get; init; }
}

/// <summary>Wire representation of a single entity span, before <see cref="RemotePiiOptions.CategoryMap"/> is applied.</summary>
internal sealed class RemotePiiEntityDto
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("start")]
    public int Start { get; init; }

    [JsonPropertyName("end")]
    public int End { get; init; }

    [JsonPropertyName("score")]
    public double Score { get; init; }
}
