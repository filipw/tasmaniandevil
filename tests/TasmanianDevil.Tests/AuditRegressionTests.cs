using System.Globalization;
using System.Text.RegularExpressions;
using FluentAssertions;
using TasmanianDevil.Analyzer;
using TasmanianDevil.Analyzer.Context;
using TasmanianDevil.Anonymizer;
using TasmanianDevil.Anonymizer.Operators;
using TasmanianDevil.Batch;
using TasmanianDevil.Recognizers.Generic;
using TasmanianDevil.Recognizers.TheNetherlands;
using TasmanianDevil.Recognizers.Uk;
using TasmanianDevil.Structured;
using Xunit;

namespace TasmanianDevil.Tests;

/// <summary>
/// Regression coverage for defects found in the code audit. Each test names the behaviour that was
/// wrong, so a reintroduction fails here rather than silently shipping.
/// </summary>
public class AuditRegressionTests
{
    private static readonly string[] All = [];

    // Regex.Matches is lazy, so a match timeout surfaces while ENUMERATING the MatchCollection.
    // Guarding only the Matches() call let the exception escape all the way out of Analyze.
    [Fact]
    public void Analyze_ShouldNotThrow_WhenAPatternExceedsItsMatchTimeout()
    {
        var hostile = new string('a', 60) + "." + string.Concat(Enumerable.Repeat("a.", 600)) + "!";

        var act = () => new PiiEngine().Analyze(hostile + " contact real@example.com");

        act.Should().NotThrow<RegexMatchTimeoutException>();
    }

    [Fact]
    public void Analyze_ShouldStillDetectOtherEntities_WhenOnePatternTimesOut()
    {
        var hostile = new string('a', 60) + "." + string.Concat(Enumerable.Repeat("a.", 600)) + "!";

        var results = new PiiEngine().Analyze(hostile + " contact real@example.com");

        results.Should().Contain(r => r.EntityType == PiiEntities.EmailAddress);
    }

    [Fact]
    public void OnRegexTimeout_ShouldBeInvoked_SoTheDetectionGapIsObservable()
    {
        var recognizer = new TimeoutReportingRecognizer();
        var hostile = new string('a', 60) + "." + string.Concat(Enumerable.Repeat("a.", 600)) + "!";

        recognizer.Analyze(hostile, All);

        recognizer.Timeouts.Should().BeGreaterThan(0, "a skipped pattern means PII it would have found is not redacted");
    }

    // IgnoreCase without CultureInvariant folds case using CultureInfo.CurrentCulture, so the same
    // text produced different detections under a Turkish locale.
    [Fact]
    public void DefaultRegexOptions_ShouldBeCultureInvariant()
    {
        PatternRecognizer.DefaultRegexOptions.Should().HaveFlag(RegexOptions.CultureInvariant);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    [InlineData("az-Latn-AZ")]
    public void Matching_ShouldNotDependOnTheAmbientCulture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            // U+0131 is equivalent to 'I' only under Turkish-style case folding
            new Regex("^[a-zA-Z]$", PatternRecognizer.DefaultRegexOptions).IsMatch("ı").Should().BeFalse();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    // the salt used to be generated per span, so the same value hashed differently every occurrence -
    // which destroys the referential integrity that makes hash useful over replace.
    [Fact]
    public void Hash_ShouldProduceTheSameDigest_ForRepeatsOfTheSameValue()
    {
        using var engine = new PiiEngine(new PiiOptions
        {
            Operators = new Dictionary<string, OperatorConfig> { ["DEFAULT"] = new("hash") },
        });

        var result = engine.Anonymize("Contact a@b.com or a@b.com again.");

        result.Items.Should().HaveCount(2);
        result.Items[0].Text.Should().Be(result.Items[1].Text);
    }

    [Fact]
    public void Hash_ShouldBeStableAcrossCalls_OnTheSameEngine()
    {
        using var engine = new PiiEngine(new PiiOptions
        {
            Operators = new Dictionary<string, OperatorConfig> { ["DEFAULT"] = new("hash") },
        });

        engine.Anonymize("mail a@b.com").Text.Should().Be(engine.Anonymize("mail a@b.com").Text);
    }

    // Validate only checked the type, so a non-positive count silently returned the PII unredacted.
    [Theory]
    [InlineData(-5)]
    [InlineData(0)]
    public void Mask_ShouldRejectNonPositiveCharsToMask(int charsToMask)
    {
        var parameters = new Dictionary<string, object>
        {
            [OperatorParams.MaskingChar] = "*",
            [OperatorParams.CharsToMask] = charsToMask,
            [OperatorParams.FromEnd] = false,
        };

        var act = () => new MaskOperator().Validate(parameters);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("\U0001F600bc", false)]
    [InlineData("ab\U0001F600", true)]
    public void Mask_ShouldNotSplitASurrogatePair(string value, bool fromEnd)
    {
        var parameters = new Dictionary<string, object>
        {
            [OperatorParams.MaskingChar] = "*",
            [OperatorParams.CharsToMask] = 1,
            [OperatorParams.FromEnd] = fromEnd,
        };

        var masked = new MaskOperator().Operate(value, parameters);

        masked.EnumerateRunes().Should().NotContain(System.Text.Rune.ReplacementChar,
            "masking by UTF-16 code unit must not leave a lone surrogate");
    }

    // ExcludePaths=["user"] used to be a silent no-op because the leaf path is "user.email".
    [Fact]
    public void JsonScope_ShouldCoverAWholeSubtree()
    {
        const string Json = """{"user":{"email":"a@b.com","note":"c@d.com"},"other":"e@f.com"}""";

        var result = new StructuredEngine().AnonymizeJson(Json, new JsonRedactionScope { ExcludePaths = ["user"] });

        result.Should().Contain("a@b.com").And.Contain("c@d.com");
        result.Should().Contain($"<{PiiEntities.EmailAddress}>", "only the excluded subtree is spared");
    }

    [Fact]
    public void JsonScope_IncludePaths_ShouldCoverAWholeSubtree()
    {
        const string Json = """{"user":{"email":"a@b.com","note":"c@d.com"},"other":"e@f.com"}""";

        var result = new StructuredEngine().AnonymizeJson(Json, new JsonRedactionScope { IncludePaths = ["user"] });

        result.Should().Contain("e@f.com", "paths outside the include list are untouched");
        result.Should().NotContain("a@b.com").And.NotContain("c@d.com");
    }

    [Fact]
    public void JsonScope_ShouldMatchOnlyAtSegmentBoundaries()
    {
        // "user" must not cover "username"
        var result = new StructuredEngine().AnonymizeJson(
            """{"username":"a@b.com"}""", new JsonRedactionScope { ExcludePaths = ["user"] });

        result.Should().NotContain("a@b.com");
    }

    [Fact]
    public void JsonScope_ShouldDistinguishADottedKey_FromNesting()
    {
        // the key "user.email" is addressed as user\.email, not as the nested path user -> email
        var engine = new StructuredEngine();

        engine.AnonymizeJson("""{"user.email":"a@b.com"}""", new JsonRedactionScope { ExcludePaths = ["user.email"] })
            .Should().NotContain("a@b.com");

        engine.AnonymizeJson("""{"user.email":"a@b.com"}""", new JsonRedactionScope { ExcludePaths = [@"user\.email"] })
            .Should().Contain("a@b.com");
    }

    // StructuredEngine never received the entity filter or allow-list, so JSON/CSV ignored both
    // while free text honored them.
    [Fact]
    public void Json_ShouldHonorTheAllowList_LikeFreeTextDoes()
    {
        using var engine = new PiiEngine(new PiiOptions { AllowList = ["support@acme.com"] });

        engine.Anonymize("write support@acme.com").Text.Should().Contain("support@acme.com");
        engine.AnonymizeJson("""{"m":"support@acme.com"}""").Should().Contain("support@acme.com");
    }

    // the URL recognizer matches "acme.com" inside "support@acme.com"; allow-listing the address
    // must exempt the whole value, not just the span whose text matches exactly
    [Fact]
    public void AllowList_ShouldExemptEntitiesContainedInAnAllowedValue()
    {
        using var engine = new PiiEngine(new PiiOptions { AllowList = ["support@acme.com"] });

        engine.Analyze("write support@acme.com").Should().BeEmpty();
        engine.Anonymize("write support@acme.com").Text.Should().Be("write support@acme.com");
    }

    [Fact]
    public void AllowList_ShouldStillRedactOtherEntities()
    {
        using var engine = new PiiEngine(new PiiOptions { AllowList = ["support@acme.com"] });

        var text = engine.Anonymize("mail support@acme.com or bob@other.com").Text;

        text.Should().Contain("support@acme.com").And.Contain($"<{PiiEntities.EmailAddress}>");
    }

    [Fact]
    public void Json_ShouldHonorTheEntityFilter_LikeFreeTextDoes()
    {
        using var engine = new PiiEngine(new PiiOptions { Entities = [PiiEntities.EmailAddress] });

        var json = engine.AnonymizeJson("""{"a":"card 4111111111111111","b":"x@y.com"}""");

        json.Should().Contain("4111111111111111", "CREDIT_CARD is not in the requested entities");
        json.Should().Contain($"<{PiiEntities.EmailAddress}>");
    }

    // passing csvOptions used to discard the engine's configured operators entirely
    [Fact]
    public void Csv_ShouldKeepConfiguredOperators_WhenCsvOptionsAreSupplied()
    {
        using var engine = new PiiEngine(new PiiOptions { Replacement = "[REDACTED]" });
        IReadOnlyList<string>[] rows = [["a@b.com"]];

        var withOptions = engine.AnonymizeCsv(["m"], rows, new StructuredCsvOptions { SampleSize = 10 });

        withOptions.Rows[0][0].Should().Be("[REDACTED]");
    }

    [Fact]
    public void Csv_ShouldStillRespectExplicitOperators_FromCsvOptions()
    {
        using var engine = new PiiEngine(new PiiOptions { Replacement = "[REDACTED]" });
        IReadOnlyList<string>[] rows = [["a@b.com"]];
        var explicitOperators = new Dictionary<string, OperatorConfig>
        {
            ["DEFAULT"] = new("replace", new Dictionary<string, object> { [OperatorParams.NewValue] = "[CSV]" }),
        };

        var result = engine.AnonymizeCsv(["m"], rows, new StructuredCsvOptions { Operators = explicitOperators });

        result.Rows[0][0].Should().Be("[CSV]");
    }

    // two space-separated addresses collapsed into one placeholder with no way to opt out
    [Fact]
    public void MergeEntitiesWithSpaces_ShouldBeConfigurable()
    {
        using var merged = new PiiEngine();
        using var separate = new PiiEngine(new PiiOptions { MergeEntitiesWithSpaces = false });

        merged.Anonymize("alice@x.com bob@y.com").Items.Should().HaveCount(1);
        separate.Anonymize("alice@x.com bob@y.com").Items.Should().HaveCount(2);
    }

    // context matching is per token, so multi-word entries could never match anything
    [Theory]
    [InlineData("mac 00:1B:44:11:3A:B7")]
    [InlineData("hardware address 00:1B:44:11:3A:B7")]
    [InlineData("physical address 00:1B:44:11:3A:B7")]
    public void MultiWordContextWords_ShouldBoostTheScore(string text)
    {
        using var engine = new PiiEngine();

        var result = engine.Analyze(text).Single(r => r.EntityType == PiiEntities.MacAddress);

        result.Score.Should().BeGreaterThan(0.6, "the recognizer's context word should have applied");
    }

    [Fact]
    public void NoRecognizerContextWord_ShouldContainASpaceThatCannotMatch()
    {
        // every multi-word entry must be reachable through the n-gram path; a trailing/leading space
        // never can be, so guard against reintroducing one
        var contextWords = PiiRecognizers
            .CreateRegistry("en", [PiiCountries.Uk, PiiCountries.De, PiiCountries.In, PiiCountries.It, PiiCountries.Es, PiiCountries.Nl])
            .Recognizers
            .SelectMany(r => r.Context ?? [])
            .ToList();

        contextWords.Should().AllSatisfy(w => w.Should().Be(w.Trim()));
    }

    // both enhancers built their lookup with ToDictionary, which threw on a duplicate recognizer Id
    [Fact]
    public void Analyze_ShouldNotThrow_WhenTheSameRecognizerInstanceIsRegisteredTwice()
    {
        var recognizer = new EmailRecognizer();
        using var engine = new PiiEngine(new PiiOptions(), extraRecognizers: [recognizer, recognizer]);

        var act = () => engine.Analyze("a@b.com");

        act.Should().NotThrow();
    }

    // the dictionary overload substituted an empty detection list for a missing key, returning that
    // record unredacted; the list overload has always been strict about a shape mismatch
    [Fact]
    public void BatchAnonymize_ShouldThrow_WhenADetectionListIsMissing()
    {
        using var engine = new PiiEngine();
        var texts = new Dictionary<string, string> { ["a"] = "mail x@y.com", ["b"] = "mail p@q.com" };
        var detections = new Dictionary<string, IReadOnlyList<RecognizerResult>> { ["a"] = engine.Analyze(texts["a"]) };

        var act = () => new BatchAnonymizerEngine().Anonymize(texts, detections);

        act.Should().Throw<ArgumentException>().WithMessage("*no entry for key 'b'*");
    }

    // 'A-Z]{2}' was missing its opening bracket, matching the literal text "A-Z]]12345"
    [Fact]
    public void UsDriverLicense_ShouldNotMatchTheLiteralRegexSource()
    {
        using var engine = new PiiEngine();

        engine.Analyze("licence A-Z]]12345")
            .Should().NotContain(r => r.EntityType == PiiEntities.UsDriverLicense);
    }

    [Fact]
    public void UsDriverLicense_ShouldMatchTwoLettersThenDigits()
    {
        using var engine = new PiiEngine();

        engine.Analyze("licence AB12")
            .Should().Contain(r => r.EntityType == PiiEntities.UsDriverLicense);
    }

    // '{1}' had been absorbed into the character class, so '{' and '1' were valid NINO suffixes
    [Fact]
    public void UkNino_ShouldRejectADigitSuffix()
    {
        using var engine = new PiiEngine(new PiiOptions { Countries = [PiiCountries.Uk] });

        engine.Analyze("NI number AB1234561").Should().NotContain(r => r.EntityType == PiiEntities.UkNino);
        engine.Analyze("NI number AB123456A").Should().Contain(r => r.EntityType == PiiEntities.UkNino);
    }

    [Fact]
    public void UkNino_SpanShouldNotIncludeALeadingSpace()
    {
        using var engine = new PiiEngine(new PiiOptions { Countries = [PiiCountries.Uk] });
        const string Text = "NI number AB123456A";

        var result = engine.Analyze(Text).Single(r => r.EntityType == PiiEntities.UkNino);

        Text[result.Start..result.End].Should().Be("AB123456A");
    }

    // age identifiers run 01-49 and 51-99; '>= 2' dropped 2001 plates and 29/79 were a time bomb
    [Theory]
    [InlineData("AB01CDE", true)]
    [InlineData("AB25CDE", true)]
    [InlineData("AB30CDE", true)]
    [InlineData("AB49CDE", true)]
    [InlineData("AB51CDE", true)]
    [InlineData("AB80CDE", true)]
    [InlineData("AB50CDE", false)]
    [InlineData("AB00CDE", false)]
    public void UkVehicleRegistration_ShouldAcceptEveryRealAgeIdentifier(string plate, bool expected)
    {
        new UkVehicleRegistrationRecognizer().ValidateResult(plate).Should().Be(expected);
    }

    // ValidateResult is public and indexed digits[0] with no length guard
    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    public void NlBsnValidate_ShouldReturnFalse_RatherThanThrow_OnShortInput(string value)
    {
        new NlBsnRecognizer().ValidateResult(value).Should().Be(false);
    }

    // a mod-11 check passes for ~9% of nine-digit numbers, so it must not promote to full confidence
    [Fact]
    public void NlBsn_ShouldNotRedactArbitraryNineDigitNumbers()
    {
        using var engine = new PiiEngine(new PiiOptions { Countries = [PiiCountries.Nl] });
        var random = new Random(42);

        var falsePositives = Enumerable.Range(0, 500)
            .Select(_ => random.Next(100_000_000, 1_000_000_000).ToString(CultureInfo.InvariantCulture))
            .Count(n => engine.Analyze($"Order reference {n} shipped.").Any(r => r.EntityType == PiiEntities.NlBsn));

        falsePositives.Should().Be(0);
    }

    // a Regex could be returned paired with options it was not compiled with
    [Fact]
    public async Task PatternGetCompiled_ShouldBeSafeUnderConcurrentAccess()
    {
        var pattern = new Pattern("t", @"\d{4}", 1.0);
        var mismatches = 0;

        await Task.WhenAll(
            Task.Run(() => Probe(RegexOptions.None)),
            Task.Run(() => Probe(RegexOptions.IgnoreCase)));

        mismatches.Should().Be(0);

        void Probe(RegexOptions options)
        {
            for (var i = 0; i < 50_000; i++)
            {
                if (pattern.GetCompiled(options, TimeSpan.FromSeconds(1)).Options != options)
                {
                    Interlocked.Increment(ref mismatches);
                }
            }
        }
    }

    [Fact]
    public void PatternGetCompiled_ShouldTreatTheTimeoutAsPartOfTheCacheKey()
    {
        var pattern = new Pattern("t", @"\d{4}", 1.0);
        var first = TimeSpan.FromSeconds(1);
        var second = TimeSpan.FromSeconds(5);

        pattern.GetCompiled(PatternRecognizer.DefaultRegexOptions, first).MatchTimeout.Should().Be(first);
        pattern.GetCompiled(PatternRecognizer.DefaultRegexOptions, second).MatchTimeout.Should().Be(second);
    }

    // the suffix window started on the entity's own token, so a multi-token entity ate the budget
    [Fact]
    public void SuffixContext_ShouldLookPastAMultiTokenEntity()
    {
        var registry = new RecognizerRegistry([new TasmanianDevil.Recognizers.Us.UsSsnRecognizer()]);
        var engine = new AnalyzerEngine(registry, new LemmaContextAwareEnhancer(contextPrefixCount: 0, contextSuffixCount: 2));

        var boosted = engine.Analyze("574-28-3917 is the social security number").Single(r => r.Start == 0);
        var plain = engine.Analyze("574-28-3917 is the quarterly figure").SingleOrDefault(r => r.Start == 0);

        boosted.Score.Should().BeGreaterThan(plain?.Score ?? 0);
    }

    // equal span and equal score used to be resolved by recognizer registration order
    [Fact]
    public void Anonymize_ShouldResolveAnExactTie_Deterministically()
    {
        var anonymizer = new AnonymizerEngine();

        var forward = anonymizer.Anonymize("abcdefghij", [new("ZZZ_TYPE", 0, 5, 0.8), new("AAA_TYPE", 0, 5, 0.8)]);
        var reversed = anonymizer.Anonymize("abcdefghij", [new("AAA_TYPE", 0, 5, 0.8), new("ZZZ_TYPE", 0, 5, 0.8)]);

        forward.Text.Should().Be(reversed.Text);
    }

    // encryption was unauthenticated AES-CBC: malleable, and a distinguishable padding failure
    [Fact]
    public void Encrypt_ShouldRejectATamperedCiphertext()
    {
        var key = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var token = AesCipher.Encrypt(key, "alice@x.com");

        var raw = System.Buffers.Text.Base64Url.DecodeFromChars(token);
        raw[^1] ^= 0x01;

        var act = () => AesCipher.Decrypt(key, System.Buffers.Text.Base64Url.EncodeToString(raw));

        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    [Fact]
    public void Encrypt_ShouldRejectADowngradedVersionMarker()
    {
        var key = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var raw = System.Buffers.Text.Base64Url.DecodeFromChars(AesCipher.Encrypt(key, "alice@x.com"));
        raw[0] = 0x02;

        var act = () => AesCipher.Decrypt(key, System.Buffers.Text.Base64Url.EncodeToString(raw));

        act.Should().Throw<System.Security.Cryptography.CryptographicException>(
            "the version byte is bound in as associated data");
    }

    [Fact]
    public void Encrypt_ShouldRoundTrip_AndUseAFreshNoncePerCall()
    {
        var key = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);

        var first = AesCipher.Encrypt(key, "alice@x.com");
        var second = AesCipher.Encrypt(key, "alice@x.com");

        AesCipher.Decrypt(key, first).Should().Be("alice@x.com");
        first.Should().NotBe(second, "a fresh nonce is drawn per call");
    }

    [Fact]
    public void DecryptLegacyCbc_ShouldStillReadPre03Ciphertext()
    {
        var key = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var legacy = EncryptLegacyCbc(key, "legacy@x.com");

        AesCipher.DecryptLegacyCbc(key, legacy).Should().Be("legacy@x.com");

        var act = () => AesCipher.Decrypt(key, legacy);
        act.Should().Throw<System.Security.Cryptography.CryptographicException>(
            "the current reader must not silently accept the unauthenticated format");
    }

    // the same serial used to be reported as both DE_ID_CARD and DE_PASSPORT at full confidence
    [Fact]
    public void GermanIdDocument_ShouldBeReportedOnce()
    {
        using var engine = new PiiEngine(new PiiOptions { Countries = [PiiCountries.De] });

        engine.Analyze("Ausweisnummer L01X00T44 vorgelegt")
            .Should().ContainSingle().Which.EntityType.Should().Be(PiiEntities.DeIdDocument);
    }

    // reproduces the pre-0.3 wire format so the migration path stays covered
    private static string EncryptLegacyCbc(byte[] key, string text)
    {
        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = key;
        aes.Mode = System.Security.Cryptography.CipherMode.CBC;
        aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
        aes.GenerateIV();

        var ciphertext = aes.EncryptCbc(System.Text.Encoding.UTF8.GetBytes(text), aes.IV);
        var combined = new byte[aes.IV.Length + ciphertext.Length];
        Buffer.BlockCopy(aes.IV, 0, combined, 0, aes.IV.Length);
        Buffer.BlockCopy(ciphertext, 0, combined, aes.IV.Length, ciphertext.Length);

        return System.Buffers.Text.Base64Url.EncodeToString(combined);
    }

    private sealed class TimeoutReportingRecognizer : PatternRecognizer
    {
        public TimeoutReportingRecognizer()
            : base(
                "TEST_ENTITY",
                patterns: [new Pattern("catastrophic", @"([a-z0-9.\-]{1,253}[.])+[a-z]{2,}(/[^\s]*)?", 0.5)],
                timeout: TimeSpan.FromMilliseconds(50))
        {
        }

        public int Timeouts { get; private set; }

        protected override void OnRegexTimeout(Pattern pattern, RegexMatchTimeoutException exception) => Timeouts++;
    }
}
