namespace TasmanianDevil.Remote;

/// <summary>
/// A single entity span returned by a remote PII detector, in the detector's own wire format
/// (before <see cref="RemotePiiOptions.CategoryMap"/> is applied). Offsets are UTF-16 code units,
/// i.e. plain .NET <see cref="string"/> indices - no conversion is needed to slice the analyzed text.
/// </summary>
/// <param name="Type">The detector's entity type identifier (e.g. <c>PERSON</c>, <c>ADDRESS</c>).</param>
/// <param name="Start">Start offset (inclusive), in UTF-16 code units.</param>
/// <param name="End">End offset (exclusive), in UTF-16 code units.</param>
/// <param name="Score">Confidence score in the range [0, 1].</param>
public sealed record RemotePiiEntity(string Type, int Start, int End, double Score);
