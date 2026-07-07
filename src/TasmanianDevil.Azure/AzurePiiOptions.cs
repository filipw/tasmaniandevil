namespace TasmanianDevil.Azure;

/// <summary>The Azure AI Language processing domain, controlling which entity categories the service considers.</summary>
public enum AzurePiiDomain
{
    /// <summary>The general PII domain (default).</summary>
    None,

    /// <summary>Protected Health Information domain - broader clinical/health-related categories.</summary>
    Phi,
}

/// <summary>Configures both <see cref="AzurePiiClient"/> (transport) and <see cref="AzurePiiRecognizer"/> (recognizer behavior).</summary>
public sealed class AzurePiiOptions
{
    /// <summary>The Azure AI Language resource endpoint, e.g. <c>https://my-resource.cognitiveservices.azure.com</c>.</summary>
    public required string Endpoint { get; init; }

    /// <summary>
    /// The canonical entity types this recognizer declares support for (after <see cref="CategoryMap"/>
    /// is applied), e.g. <c>[PiiEntities.Person, PiiEntities.Address]</c>. Drives registry selection and
    /// filters the Azure response: any returned entity that doesn't map into this set is dropped.
    /// </summary>
    public required IReadOnlyList<string> SupportedEntities { get; init; }

    /// <summary>The analysis language sent to Azure. Defaults to <c>en</c>.</summary>
    public string SupportedLanguage { get; init; } = "en";

    /// <summary>
    /// API key sent as the <c>Ocp-Apim-Subscription-Key</c> header. Mutually exclusive with
    /// <see cref="TokenProvider"/> - when both are set, <see cref="TokenProvider"/> wins.
    /// </summary>
    public string? SubscriptionKey { get; init; }

    /// <summary>
    /// Optional AAD bearer token provider, invoked per request. Lets callers plug in
    /// <c>DefaultAzureCredential</c> (or any other <c>TokenCredential</c>) without this package taking
    /// a dependency on <c>Azure.Identity</c>; the concrete wiring lives in the consuming layer.
    /// </summary>
    public Func<CancellationToken, ValueTask<string>>? TokenProvider { get; init; }

    /// <summary>The <c>:analyze-text</c> REST API version. Defaults to the GA <c>2024-11-01</c>.</summary>
    public string ApiVersion { get; init; } = "2024-11-01";

    /// <summary>The PII model version to request. Defaults to <c>latest</c>.</summary>
    public string ModelVersion { get; init; } = "latest";

    /// <summary>The processing domain. Defaults to <see cref="AzurePiiDomain.None"/> (general PII, not PHI).</summary>
    public AzurePiiDomain Domain { get; init; } = AzurePiiDomain.None;

    /// <summary>
    /// Azure entity category names to request (e.g. <c>["Person", "Address"]</c>). When null, Azure
    /// returns its default category set for the language.
    /// </summary>
    public IReadOnlyList<string>? PiiCategories { get; init; }

    /// <summary>
    /// Minimum <c>confidenceScore</c> (applied client-side, after the response is received) for a
    /// returned entity to be kept. When null, every entity Azure returns is kept (subject to the
    /// analyzer's own overall score threshold downstream).
    /// </summary>
    public double? ConfidenceThreshold { get; init; }

    /// <summary>
    /// When <c>true</c> (default), tells Azure not to log the submitted text. A PII detector should not
    /// let the request text be retained server-side, so this defaults to <c>true</c> rather than the
    /// service default.
    /// </summary>
    public bool LoggingOptOut { get; init; } = true;

    /// <summary>
    /// Optional override for the Azure category -> canonical entity type mapping. Defaults to
    /// <see cref="AzurePiiCategoryMap.Default"/> when null.
    /// </summary>
    public IReadOnlyDictionary<string, string>? CategoryMap { get; init; }

    /// <summary>
    /// When <c>true</c>, <see cref="AzurePiiClient.DetectAsync"/> surfaces the <c>redactedText</c>
    /// field Azure includes in its response on <see cref="AzurePiiDetectionResult.RedactedText"/>.
    /// When <c>false</c> (default) it is dropped even though Azure still computes and returns it on the
    /// wire - detection stays remote, redaction stays local, so nothing in the recognizer path reads
    /// this value; it is purely a passthrough for callers using <see cref="AzurePiiClient"/> directly.
    /// </summary>
    public bool PassthroughRedactedText { get; init; }

    /// <summary>
    /// When <c>true</c> (default), a remote failure (exception, non-success response, or timeout) is
    /// swallowed and the recognizer returns no results for this request, so local-only recognizers
    /// still redact structured PII. When <c>false</c>, the failure propagates instead. Cancellation of
    /// the caller's own token always propagates regardless of this setting.
    /// </summary>
    public bool FailOpen { get; init; } = true;

    /// <summary>
    /// Per-request timeout, enforced by <see cref="AzurePiiRecognizer"/> via a linked cancellation
    /// token independent of the caller's token. Defaults to 10 seconds.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Optional callback invoked with the exception whenever an Azure call fails and is swallowed by
    /// <see cref="FailOpen"/>. Use for logging/telemetry without imposing a logging dependency here.
    /// </summary>
    public Action<Exception>? OnError { get; init; }
}
