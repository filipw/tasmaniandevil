namespace TasmanianDevil.Remote;

/// <summary>
/// Configures both <see cref="HttpPiiDetectionClient"/> (transport) and <see cref="RemotePiiRecognizer"/>
/// (recognizer behavior) for out-of-process PII detection.
/// </summary>
public sealed class RemotePiiOptions
{
    /// <summary>
    /// The entity types this remote detector can find (e.g. <c>[PiiEntities.Person, PiiEntities.Address]</c>).
    /// Drives registry selection: <c>AnalyzerEngine</c> only calls this recognizer when at least one
    /// requested entity is in this set, and the recognizer only asks the remote service for the
    /// intersection of what was requested and what it declares support for here.
    /// </summary>
    public required IReadOnlyList<string> SupportedEntities { get; init; }

    /// <summary>The analysis language this detector supports. Defaults to <c>en</c>.</summary>
    public string SupportedLanguage { get; init; } = "en";

    /// <summary>
    /// Base URL of the remote detection endpoint. Required when using <see cref="HttpPiiDetectionClient"/>;
    /// unused when a custom <see cref="IPiiDetectionClient"/> is supplied directly.
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>Optional HTTP header name used to authenticate against the endpoint (e.g. <c>X-Api-Key</c>).</summary>
    public string? AuthHeaderName { get; init; }

    /// <summary>The value sent for <see cref="AuthHeaderName"/>.</summary>
    public string? AuthHeaderValue { get; init; }

    /// <summary>
    /// Per-request timeout, enforced by <see cref="RemotePiiRecognizer"/> via a linked cancellation
    /// token independent of the caller's token. Defaults to 10 seconds.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// When <c>true</c> (default), a remote failure (exception, non-success response, or timeout) is
    /// swallowed and the recognizer returns no results for this request, so local-only recognizers
    /// still redact structured PII. When <c>false</c>, the failure propagates instead. Cancellation of
    /// the caller's own token always propagates regardless of this setting.
    /// </summary>
    public bool FailOpen { get; init; } = true;

    /// <summary>
    /// Optional remapping from the remote detector's entity type strings to canonical entity types
    /// (e.g. if a third-party service uses its own vocabulary). Types not present in the map pass
    /// through unchanged - the default assumption is that the remote service already speaks the
    /// canonical vocabulary (as a TasmanianDevil-based sidecar would).
    /// </summary>
    public IReadOnlyDictionary<string, string>? CategoryMap { get; init; }

    /// <summary>
    /// When <c>true</c>, <see cref="HttpPiiDetectionClient"/> asks the remote service to also compute
    /// and return a redacted preview of the text (<c>includeRedactedText</c> on the wire request). The
    /// value, if returned, is parsed but intentionally not exposed anywhere - detection stays remote,
    /// redaction stays local. As of this release this is a forward-looking, currently-inert hook (no
    /// consumer reads the parsed value yet); enabling it only costs the remote service extra work with
    /// no observable effect. Defaults to <c>false</c>.
    /// </summary>
    public bool RequestRedactedTextPassthrough { get; init; }

    /// <summary>
    /// Optional callback invoked with the exception whenever a remote call fails and is swallowed by
    /// <see cref="FailOpen"/>. Use for logging/telemetry without imposing a logging dependency here.
    /// </summary>
    public Action<Exception>? OnError { get; init; }
}
