namespace TasmanianDevil.Remote;

/// <summary>
/// Abstraction for an out-of-process PII detector. Implementations call an external service (a
/// generic HTTP sidecar via <see cref="HttpPiiDetectionClient"/>, or a custom transport) and return
/// entity spans only - detection, not anonymization; the caller's <c>AnonymizerEngine</c> still owns
/// redaction.
/// </summary>
public interface IPiiDetectionClient
{
    /// <summary>
    /// Detects PII entities in <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The text to analyze. Sent to the remote service as-is (raw, unredacted).</param>
    /// <param name="language">The analysis language (e.g. <c>en</c>).</param>
    /// <param name="entities">The entity types to look for.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<IReadOnlyList<RemotePiiEntity>> DetectAsync(
        string text, string language, IReadOnlyList<string> entities, CancellationToken ct = default);
}
