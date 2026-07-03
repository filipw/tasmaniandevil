using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TasmanianDevil.Remote;

/// <summary>
/// HTTP implementation of <see cref="IPiiDetectionClient"/> against the generic remote PII detection
/// wire contract: <c>POST {endpoint}</c> with request <c>{ "text", "language", "entities" }</c> and
/// response <c>{ "entities": [{ "type", "start", "end", "score" }] }</c>. Any JSON server implementing
/// this contract works - e.g. a small sidecar wrapping TasmanianDevil + the GLiNER ONNX bridge.
/// </summary>
public sealed class HttpPiiDetectionClient : IPiiDetectionClient, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly RemotePiiOptions _options;
    private readonly bool _ownsClient;

    /// <summary>Creates a client with a new, owned <see cref="HttpClient"/>.</summary>
    public HttpPiiDetectionClient(RemotePiiOptions options) : this(new HttpClient(), options, ownsClient: true)
    {
    }

    /// <summary>Creates a client over an existing <see cref="HttpClient"/> (e.g. from <c>IHttpClientFactory</c>).</summary>
    public HttpPiiDetectionClient(HttpClient httpClient, RemotePiiOptions options) : this(httpClient, options, ownsClient: false)
    {
    }

    private HttpPiiDetectionClient(HttpClient httpClient, RemotePiiOptions options, bool ownsClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            throw new ArgumentException("RemotePiiOptions.Endpoint is required.", nameof(options));
        }

        _httpClient = httpClient;
        _options = options;
        _ownsClient = ownsClient;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<RemotePiiEntity>> DetectAsync(
        string text, string language, IReadOnlyList<string> entities, CancellationToken ct = default)
    {
        var request = new RemotePiiDetectionRequest
        {
            Text = text,
            Language = language,
            Entities = entities,
            IncludeRedactedText = _options.RequestRedactedTextPassthrough ? true : null,
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };

        if (_options.AuthHeaderName is not null)
        {
            httpRequest.Headers.TryAddWithoutValidation(_options.AuthHeaderName, _options.AuthHeaderValue ?? string.Empty);
        }

        using var response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<RemotePiiDetectionResponse>(JsonOptions, ct).ConfigureAwait(false);
        if (payload is null || payload.Entities.Count == 0)
        {
            return [];
        }

        return payload.Entities
            .Select(e => new RemotePiiEntity(e.Type, e.Start, e.End, e.Score))
            .ToList();
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
