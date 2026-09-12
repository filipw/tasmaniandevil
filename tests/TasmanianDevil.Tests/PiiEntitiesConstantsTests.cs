using System.Reflection;
using TasmanianDevil;
using TasmanianDevil.Analyzer;
using FluentAssertions;
using Xunit;

namespace TasmanianDevil.Tests;

public class PiiEntitiesConstantsTests
{
    private static readonly string[] NerEntities =
        [PiiEntities.Person, PiiEntities.Location, PiiEntities.Organization, PiiEntities.DateTime];

    // detected only by an optional remote detector (TasmanianDevil.Remote / TasmanianDevil.Azure), not by
    // any local regex/checksum/NER recognizer
    private static readonly string[] RemoteOnlyEntities = [PiiEntities.Address];

    private static List<string> AllEntityConstants() => StringConstantsOf(typeof(PiiEntities));

    // every country pack, derived from PiiCountries rather than hand-listed: a hardcoded list here
    // silently stops guarding as soon as a new pack is added (which is how the NL pack shipped
    // without PiiEntities constants).
    private static List<string> AllCountryConstants() => StringConstantsOf(typeof(PiiCountries));

    private static List<string> StringConstantsOf(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true } && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .ToList();

    [Fact]
    public void RegexAndChecksumConstants_ShouldExactlyMatch_AllRecognizerEntities()
    {
        // build every always-on + opt-in pack so the registry exposes the full regex/checksum vocabulary
        var registry = PiiRecognizers.CreateRegistry("en", AllCountryConstants());
        var supported = registry.GetSupportedEntities("en");

        var excluded = NerEntities.Concat(RemoteOnlyEntities);
        var nonNerConstants = AllEntityConstants().Where(c => !excluded.Contains(c));

        // guards drift in BOTH directions: a new recognizer entity with no constant, or a constant
        // that no recognizer actually produces, fails this test.
        nonNerConstants.Should().BeEquivalentTo(supported);
    }

    [Fact]
    public void EveryCountryConstant_ShouldResolveToANonEmptyPack()
    {
        // guards the other half of the drift: a PiiCountries code with no recognizers behind it
        foreach (var country in AllCountryConstants())
        {
            PiiRecognizers.CreateForCountry(country).Should().NotBeEmpty(
                $"PiiCountries exposes '{country}' so a pack must exist for it");
        }
    }

    [Fact]
    public void Constants_ShouldBeUnique_AndUpperSnakeCase()
    {
        var all = AllEntityConstants();

        all.Should().OnlyHaveUniqueItems();
        all.Should().AllSatisfy(c => c.Should().MatchRegex("^[A-Z][A-Z0-9_]*$"));
    }
}
