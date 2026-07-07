namespace TasmanianDevil.Azure;

/// <summary>
/// A single entity span returned by Azure AI Language, in Azure's own category vocabulary (before
/// <see cref="AzurePiiOptions.CategoryMap"/> is applied). <see cref="Offset"/>/<see cref="Length"/> are
/// UTF-16 code units - plain .NET <see cref="string"/> indices - because the request always sets
/// <c>stringIndexType: Utf16CodeUnit</c>.
/// </summary>
/// <param name="Text">The detected entity's text.</param>
/// <param name="Category">Azure's entity category, e.g. <c>Person</c>, <c>Address</c>.</param>
/// <param name="Subcategory">Azure's optional entity subcategory, e.g. <c>StreetAddress</c>.</param>
/// <param name="Offset">Start offset (inclusive), in UTF-16 code units.</param>
/// <param name="Length">Span length, in UTF-16 code units.</param>
/// <param name="ConfidenceScore">Confidence score in the range [0, 1].</param>
public sealed record AzurePiiEntity(string Text, string Category, string? Subcategory, int Offset, int Length, double ConfidenceScore);

/// <summary>The result of a single <see cref="AzurePiiClient.DetectAsync"/> call.</summary>
/// <param name="Entities">The detected entity spans.</param>
/// <param name="RedactedText">
/// Azure's own redacted-text preview, only populated when <see cref="AzurePiiOptions.PassthroughRedactedText"/>
/// is enabled; otherwise <c>null</c> even though Azure computed it.
/// </param>
public sealed record AzurePiiDetectionResult(IReadOnlyList<AzurePiiEntity> Entities, string? RedactedText);
