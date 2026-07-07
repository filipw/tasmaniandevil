using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TasmanianDevil.Azure;

/// <summary>
/// Direct REST client for the Azure AI Language PII entity recognition endpoint
/// (<c>POST {Endpoint}/language/:analyze-text?api-version=...</c>, <c>PiiEntityRecognition</c> kind).
/// No dependency on <c>Azure.AI.TextAnalytics</c>/<c>Azure.AI.Language.Text</c> or <c>Azure.Identity</c> -
/// authentication is either a subscription key or an externally-supplied bearer token delegate.
/// </summary>
public sealed class AzurePiiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly AzurePiiOptions _options;
    private readonly bool _ownsClient;

    /// <summary>Creates a client with a new, owned <see cref="HttpClient"/>.</summary>
    public AzurePiiClient(AzurePiiOptions options) : this(new HttpClient(), options, ownsClient: true)
    {
    }

    /// <summary>Creates a client over an existing <see cref="HttpClient"/> (e.g. from <c>IHttpClientFactory</c>).</summary>
    public AzurePiiClient(HttpClient httpClient, AzurePiiOptions options) : this(httpClient, options, ownsClient: false)
    {
    }

    private AzurePiiClient(HttpClient httpClient, AzurePiiOptions options, bool ownsClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            throw new ArgumentException("AzurePiiOptions.Endpoint is required.", nameof(options));
        }

        if (options.SubscriptionKey is null && options.TokenProvider is null)
        {
            throw new ArgumentException("AzurePiiOptions requires either SubscriptionKey or TokenProvider.", nameof(options));
        }

        _httpClient = httpClient;
        _options = options;
        _ownsClient = ownsClient;
    }

    /// <summary>Detects PII entities in <paramref name="text"/> via the Azure AI Language REST API.</summary>
    public async ValueTask<AzurePiiDetectionResult> DetectAsync(string text, string language, CancellationToken ct = default)
    {
        var request = new AzureAnalyzeTextRequest
        {
            Kind = "PiiEntityRecognition",
            Parameters = new AzurePiiTaskParameters
            {
                ModelVersion = _options.ModelVersion,
                Domain = _options.Domain == AzurePiiDomain.Phi ? "phi" : "none",
                PiiCategories = _options.PiiCategories,
                // correctness-critical: the service default (TextElements_v8) misaligns offsets
                // against .NET strings on surrogate-pair text - always request Utf16CodeUnit
                StringIndexType = "Utf16CodeUnit",
                LoggingOptOut = _options.LoggingOptOut,
            },
            AnalysisInput = new AzureAnalysisInput
            {
                Documents = [new AzureDocumentInput { Id = "1", Language = language, Text = text }],
            },
        };

        var uri = $"{_options.Endpoint.TrimEnd('/')}/language/:analyze-text?api-version={_options.ApiVersion}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };

        await ApplyAuthAsync(httpRequest, ct).ConfigureAwait(false);

        using var response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AzureAnalyzeTextResponse>(JsonOptions, ct).ConfigureAwait(false);

        // Azure reports per-document failures (unsupported language, oversized text, ...) on an HTTP 200
        // in results.errors; surface them instead of silently returning no detections
        var error = payload?.Results?.Errors.FirstOrDefault();
        if (error is not null)
        {
            throw new AzurePiiException(error.Error?.Code, error.Error?.Message);
        }

        var document = payload?.Results?.Documents.FirstOrDefault();
        if (document is null)
        {
            return new AzurePiiDetectionResult([], null);
        }

        var entities = document.Entities
            .Select(e => new AzurePiiEntity(e.Text, e.Category, e.Subcategory, e.Offset, e.Length, e.ConfidenceScore))
            .ToList();

        var redactedText = _options.PassthroughRedactedText ? document.RedactedText : null;
        return new AzurePiiDetectionResult(entities, redactedText);
    }

    private async ValueTask ApplyAuthAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (_options.TokenProvider is not null)
        {
            var token = await _options.TokenProvider(ct).ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", _options.SubscriptionKey);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }
}
