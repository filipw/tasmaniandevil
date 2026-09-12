using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace TasmanianDevil.Analyzer;

/// <summary>
/// A named regular expression with an associated base confidence score.
/// The compiled <see cref="Regex"/> is cached per set of options.
/// </summary>
public sealed class Pattern
{
    // one immutable tuple published atomically: a reader can never observe a Regex paired with
    // the wrong options/timeout, which a two-field cache allows under concurrent access.
    private volatile CompiledRegex? _compiled;

    /// <summary>Initializes a new instance of the <see cref="Pattern"/> class.</summary>
    public Pattern(string name, [StringSyntax(StringSyntaxAttribute.Regex)] string regex, double score)
    {
        Name = name;
        Regex = regex;
        Score = score;
    }

    /// <summary>The pattern's name (used in explanations).</summary>
    public string Name { get; }

    /// <summary>The regular expression source.</summary>
    [StringSyntax(StringSyntaxAttribute.Regex)]
    public string Regex { get; }

    /// <summary>The base confidence score assigned to matches of this pattern.</summary>
    public double Score { get; }

    /// <summary>
    /// Returns a compiled <see cref="System.Text.RegularExpressions.Regex"/> for the given options
    /// and timeout, caching it. Safe to call concurrently; the timeout is part of the cache key, so
    /// two recognizers sharing a <see cref="Pattern"/> with different timeouts each get their own.
    /// </summary>
    public Regex GetCompiled(RegexOptions options, TimeSpan timeout)
    {
        var cached = _compiled;
        if (cached is not null && cached.Options == options && cached.Timeout == timeout)
        {
            return cached.Regex;
        }

        var compiled = new CompiledRegex(new Regex(Regex, options, timeout), options, timeout);
        _compiled = compiled;
        return compiled.Regex;
    }

    private sealed record CompiledRegex(Regex Regex, RegexOptions Options, TimeSpan Timeout);
}
