using TasmanianDevil.Analyzer;
using TasmanianDevil.Analyzer.Context;
using TasmanianDevil.Anonymizer;
using TasmanianDevil.Anonymizer.Operators;

namespace TasmanianDevil;

/// <summary>
/// Configures <see cref="PiiEngine"/>: which entities to detect, how to anonymize them, the
/// detection threshold and allow-list behavior.
/// </summary>
public sealed class PiiOptions
{
    /// <summary>
    /// Entity types to detect. When null or empty, all supported entities are detected. Use the
    /// <see cref="PiiEntities"/> constants for discoverability (e.g.
    /// <c>[PiiEntities.EmailAddress, PiiEntities.PhoneNumber, PiiEntities.UsSsn]</c>); custom
    /// entity-type strings from your own recognizers are also accepted.
    /// </summary>
    public IReadOnlyList<string>? Entities { get; init; }

    /// <summary>
    /// Per-entity anonymization operators. A <c>DEFAULT</c> entry applies to entities without a
    /// specific operator. When null, every entity is replaced with <c>&lt;ENTITY_TYPE&gt;</c>
    /// (unless <see cref="Replacement"/> is set).
    /// </summary>
    public IReadOnlyDictionary<string, OperatorConfig>? Operators { get; init; }

    /// <summary>
    /// Convenience flat replacement string applied to every entity (e.g. <c>[REDACTED]</c>). Ignored
    /// when <see cref="Operators"/> is provided.
    /// </summary>
    public string? Replacement { get; init; }

    /// <summary>Language of the analyzed text. Defaults to <c>en</c>.</summary>
    public string Language { get; init; } = "en";

    /// <summary>
    /// Country packs to enable in addition to the generic recognizers and the always-on US pack, by
    /// ISO 3166-1 alpha-2 code - use the <see cref="PiiCountries"/> constants (e.g.
    /// <c>[PiiCountries.Uk, PiiCountries.De]</c>). Country packs are opt-in because enabling every
    /// national identifier at once inflates false positives. When null or empty, only the generic and
    /// US recognizers run.
    /// </summary>
    public IReadOnlyList<string>? Countries { get; init; }

    /// <summary>How recognizer context words are matched against surrounding tokens. Defaults to substring.</summary>
    public ContextMatchingMode ContextMatchingMode { get; init; } = ContextMatchingMode.Substring;

    /// <summary>
    /// Minimum confidence for a detection to be acted on. Defaults to 0.4 so weak patterns without
    /// supporting context (e.g. a bare 9-digit number) are dropped.
    /// </summary>
    public double ScoreThreshold { get; init; } = 0.4;

    /// <summary>Terms to exempt from redaction.</summary>
    public IReadOnlyList<string>? AllowList { get; init; }

    /// <summary>How <see cref="AllowList"/> entries are interpreted. Defaults to exact match.</summary>
    public AllowListMatch AllowListMatch { get; init; } = AllowListMatch.Exact;

    /// <summary>Conflict resolution strategy for overlapping entities.</summary>
    public ConflictResolutionStrategy ConflictResolution { get; init; } = ConflictResolutionStrategy.MergeSimilarOrContained;

    /// <summary>
    /// When <c>true</c> (default), adjacent entities of the same type separated only by spaces are
    /// merged into a single span before anonymization. That is what keeps a multi-token name
    /// ("John Smith") one entity, but it also collapses genuinely separate values - two space-separated
    /// email addresses become one <c>&lt;EMAIL_ADDRESS&gt;</c>. Set to <c>false</c> to anonymize each
    /// detected span on its own.
    /// </summary>
    public bool MergeEntitiesWithSpaces { get; init; } = true;

    /// <summary>Builds the effective operator map from <see cref="Operators"/> / <see cref="Replacement"/>.</summary>
    public IReadOnlyDictionary<string, OperatorConfig>? BuildOperators()
    {
        if (Operators is { Count: > 0 })
        {
            return Operators;
        }

        if (!string.IsNullOrEmpty(Replacement))
        {
            var parameters = new Dictionary<string, object> { [OperatorParams.NewValue] = Replacement };
            return new Dictionary<string, OperatorConfig> { ["DEFAULT"] = new("replace", parameters) };
        }

        return null;
    }
}
