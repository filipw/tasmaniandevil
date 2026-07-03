using System.Text.Json.Serialization;

namespace TasmanianDevil.Azure;

/// <summary>
/// Wire request body for <c>POST {Endpoint}/language/:analyze-text?api-version=...</c>, the
/// <c>PiiEntityRecognition</c> task.
/// </summary>
internal sealed class AzureAnalyzeTextRequest
{
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    [JsonPropertyName("parameters")]
    public required AzurePiiTaskParameters Parameters { get; init; }

    [JsonPropertyName("analysisInput")]
    public required AzureAnalysisInput AnalysisInput { get; init; }
}

internal sealed class AzurePiiTaskParameters
{
    [JsonPropertyName("modelVersion")]
    public string? ModelVersion { get; init; }

    [JsonPropertyName("domain")]
    public string? Domain { get; init; }

    [JsonPropertyName("piiCategories")]
    public IReadOnlyList<string>? PiiCategories { get; init; }

    /// <summary>
    /// Mandatory: the service default (<c>TextElements_v8</c>) misaligns offsets against .NET strings
    /// for surrogate-pair text (emoji etc.); this must always be sent as <c>Utf16CodeUnit</c>.
    /// </summary>
    [JsonPropertyName("stringIndexType")]
    public required string StringIndexType { get; init; }

    [JsonPropertyName("loggingOptOut")]
    public bool LoggingOptOut { get; init; }
}

internal sealed class AzureAnalysisInput
{
    [JsonPropertyName("documents")]
    public required IReadOnlyList<AzureDocumentInput> Documents { get; init; }
}

internal sealed class AzureDocumentInput
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("language")]
    public required string Language { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

/// <summary>Wire response body: <c>{ "kind", "results": { "documents", "errors", "modelVersion" } }</c>.</summary>
internal sealed class AzureAnalyzeTextResponse
{
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    [JsonPropertyName("results")]
    public AzurePiiResults? Results { get; init; }
}

internal sealed class AzurePiiResults
{
    [JsonPropertyName("documents")]
    public List<AzurePiiDocumentResult> Documents { get; init; } = [];

    [JsonPropertyName("errors")]
    public List<AzureDocumentError> Errors { get; init; } = [];

    [JsonPropertyName("modelVersion")]
    public string? ModelVersion { get; init; }
}

/// <summary>A per-document error entry: the service returns HTTP 200 but reports the failure here.</summary>
internal sealed class AzureDocumentError
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("error")]
    public AzureErrorDetail? Error { get; init; }
}

internal sealed class AzureErrorDetail
{
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

internal sealed class AzurePiiDocumentResult
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("redactedText")]
    public string? RedactedText { get; init; }

    [JsonPropertyName("entities")]
    public List<AzurePiiEntityDto> Entities { get; init; } = [];
}

/// <summary>Wire representation of a single detected entity, before <see cref="AzurePiiOptions.CategoryMap"/> is applied.</summary>
internal sealed class AzurePiiEntityDto
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("category")]
    public required string Category { get; init; }

    [JsonPropertyName("subcategory")]
    public string? Subcategory { get; init; }

    [JsonPropertyName("offset")]
    public int Offset { get; init; }

    [JsonPropertyName("length")]
    public int Length { get; init; }

    [JsonPropertyName("confidenceScore")]
    public double ConfidenceScore { get; init; }
}
