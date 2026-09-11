namespace TasmanianDevil.Structured;

/// <summary>
/// Restricts which JSON string values <see cref="StructuredEngine"/> analyzes, by dotted key path.
/// A path is the dot-joined chain of property names from the root; array elements do not add a
/// segment, so every element of <c>tags</c> shares the path <c>tags</c>.
/// <para>
/// A configured path covers itself <em>and everything beneath it</em>: <c>user</c> matches
/// <c>user.email</c> and <c>user.address.city</c>, but not <c>username</c> - matching happens at
/// segment boundaries only.
/// </para>
/// <para>
/// A property name that itself contains a <c>.</c> is escaped as <c>\.</c> in the path, so the key
/// <c>"user.email"</c> is addressed as <c>user\.email</c> and stays distinct from the nested path
/// <c>user</c> → <c>email</c>. A literal backslash in a name is escaped as <c>\\</c>.
/// </para>
/// </summary>
public sealed class JsonRedactionScope
{
    /// <summary>The character separating path segments.</summary>
    internal const char PathSeparator = '.';

    /// <summary>
    /// When set, only string values whose path is covered by an entry in this list are analyzed (an
    /// allowlist). When null, all string values are analyzed except those excluded by
    /// <see cref="ExcludePaths"/>.
    /// </summary>
    public IReadOnlyList<string>? IncludePaths { get; init; }

    /// <summary>
    /// String values whose path is covered by an entry in this list are left untouched (a denylist).
    /// Takes precedence over <see cref="IncludePaths"/>.
    /// </summary>
    public IReadOnlyList<string>? ExcludePaths { get; init; }

    /// <summary>
    /// Appends a property name to a path, escaping any <c>.</c> or <c>\</c> it contains so a key
    /// holding a dot cannot be confused with nesting.
    /// </summary>
    internal static string AppendSegment(string path, string key)
    {
        var escaped = key
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(".", "\\.", StringComparison.Ordinal);

        return path.Length == 0 ? escaped : $"{path}{PathSeparator}{escaped}";
    }

    /// <summary>Returns <c>true</c> if a string value at <paramref name="path"/> should be analyzed.</summary>
    internal bool ShouldAnalyze(string path)
    {
        if (ExcludePaths is { Count: > 0 } && ExcludePaths.Any(p => Covers(p, path)))
        {
            return false;
        }

        if (IncludePaths is { Count: > 0 })
        {
            return IncludePaths.Any(p => Covers(p, path));
        }

        return true;
    }

    // a configured path matches itself and everything beneath it, so "user" covers "user.email";
    // requiring a separator at the boundary keeps "user" from also covering "username".
    private static bool Covers(string configured, string path)
    {
        if (string.Equals(configured, path, StringComparison.Ordinal))
        {
            return true;
        }

        return path.Length > configured.Length
            && path.StartsWith(configured, StringComparison.Ordinal)
            && path[configured.Length] == PathSeparator;
    }
}
