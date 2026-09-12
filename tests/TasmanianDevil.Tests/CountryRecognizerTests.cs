using FluentAssertions;
using TasmanianDevil.Analyzer;
using TasmanianDevil.Recognizers.Germany;
using TasmanianDevil.Recognizers.India;
using TasmanianDevil.Recognizers.Italy;
using TasmanianDevil.Recognizers.Spain;
using TasmanianDevil.Recognizers.TheNetherlands;
using TasmanianDevil.Recognizers.Uk;
using TasmanianDevil.Recognizers.Us;
using Xunit;

namespace TasmanianDevil.Tests;

public class CountryRecognizerTests
{
    private static readonly IReadOnlyList<string> All = [];

    // --- US ---

    [Fact]
    public void ShouldValidateAbaRouting_WhenChecksumValid()
    {
        var r = new AbaRoutingRecognizer();
        r.Analyze("122105155", All).Should().ContainSingle().Which.Score.Should().Be(EntityRecognizer.MaxScore);
        r.Analyze("122105156", All).Should().BeEmpty();
    }

    [Fact]
    public void ShouldValidateNpi_WhenLuhnValid()
    {
        var r = new UsNpiRecognizer();
        r.Analyze("1234567893", All).Should().ContainSingle().Which.Score.Should().Be(EntityRecognizer.MaxScore);
        r.Analyze("1234567890", All).Should().BeEmpty();
        r.Analyze("1111111111", All).Should().BeEmpty(); // degenerate body invalidated
    }

    [Fact]
    public void ShouldValidateMedicalLicense_WhenDeaChecksumValid()
    {
        var r = new MedicalLicenseRecognizer();
        r.Analyze("AB1234563", All).Should().ContainSingle().Which.Score.Should().Be(EntityRecognizer.MaxScore);
        r.Analyze("AB1234560", All).Should().BeEmpty();
    }

    // --- UK ---

    [Fact]
    public void ShouldValidateNhs_WhenMod11Valid()
    {
        var r = new UkNhsRecognizer();
        r.Analyze("943 476 5919", All).Should().ContainSingle().Which.Score.Should().Be(EntityRecognizer.MaxScore);
        r.Analyze("943 476 5918", All).Should().BeEmpty();
    }

    [Fact]
    public void ShouldRejectDrivingLicence_WhenSurnameAllNines()
    {
        var r = new UkDrivingLicenceRecognizer();
        r.Analyze("MORGA657054AB9CD", All).Should().ContainSingle();
        r.Analyze("99999657054AB9CD", All).Should().BeEmpty();
    }

    [Fact]
    public void ShouldValidateVehicleRegistration_WhenCurrentAgeInRange()
    {
        var r = new UkVehicleRegistrationRecognizer();
        r.Analyze("AB51ABC", All).Should().Contain(x => x.Score == EntityRecognizer.MaxScore);
    }

    // --- Germany ---

    [Fact]
    public void ShouldValidateTaxId_WhenMod1110Valid()
    {
        var r = new DeTaxIdRecognizer();
        r.Analyze("86095742719", All).Should().ContainSingle().Which.Score.Should().Be(EntityRecognizer.MaxScore);
        r.Analyze("86095742711", All).Should().BeEmpty();
    }

    [Fact]
    public void ShouldValidateSocialSecurity_WhenCheckDigitValid()
    {
        var r = new DeSocialSecurityRecognizer();
        r.Analyze("15070649C103", All).Should().ContainSingle().Which.Score.Should().Be(EntityRecognizer.MaxScore);
        r.Analyze("15070649C104", All).Should().BeEmpty();
    }

    [Theory]
    [InlineData("L01X00T44")]
    [InlineData("C01X00T41")]
    public void ShouldValidateIdDocument_WhenCheckDigitValid(string serial)
    {
        // Personalausweis and Reisepass serials share one format, so one recognizer covers both
        var r = new DeIdDocumentRecognizer();
        r.Analyze(serial, All).Should().ContainSingle().Which.Score.Should().Be(EntityRecognizer.MaxScore);
    }

    [Theory]
    [InlineData("L01X00T45")]
    [InlineData("C01X00T42")]
    public void ShouldDropIdDocument_WhenCheckDigitInvalid(string serial)
    {
        new DeIdDocumentRecognizer().Analyze(serial, All).Should().BeEmpty();
    }

    [Fact]
    public void ShouldKeepLegacyIdCard_WhenTFormat()
    {
        // legacy "T + 8 digits" predates the check digit and keeps its base score
        var r = new DeIdDocumentRecognizer();
        var results = r.Analyze("T22000124", All);
        results.Should().ContainSingle();
        results[0].Score.Should().BeLessThan(EntityRecognizer.MaxScore);
    }

    [Fact]
    public void ShouldReportOneEntity_ForAGermanIdDocumentSerial()
    {
        // earlier versions reported the same span as both DE_ID_CARD and DE_PASSPORT at full
        // confidence, and anonymization picked between them by registration order
        using var engine = new PiiEngine(new PiiOptions { Countries = [PiiCountries.De] });

        var results = engine.Analyze("Ausweisnummer L01X00T44 vorgelegt");

        results.Should().ContainSingle().Which.EntityType.Should().Be(PiiEntities.DeIdDocument);
        engine.Anonymize("Ausweisnummer L01X00T44 vorgelegt").Text
            .Should().Be($"Ausweisnummer <{PiiEntities.DeIdDocument}> vorgelegt");
    }

    [Fact]
    public void ShouldValidateVatId_WhenChecksumValidOrStrict()
    {
        new DeVatIdRecognizer().Analyze("DE123456788", All).Should().ContainSingle().Which.Score.Should().Be(EntityRecognizer.MaxScore);

        // default mode keeps a checksum-failing match at its base score; strict mode drops it
        new DeVatIdRecognizer().Analyze("DE123456789", All).Should().ContainSingle().Which.Score.Should().BeLessThan(EntityRecognizer.MaxScore);
        new DeVatIdRecognizer(strictChecksum: true).Analyze("DE123456789", All).Should().BeEmpty();
    }

    // --- India ---

    [Fact]
    public void ShouldValidateAadhaar_WhenVerhoeffValid()
    {
        var r = new InAadhaarRecognizer();
        r.Analyze("234123412346", All).Should().ContainSingle().Which.Score.Should().Be(EntityRecognizer.MaxScore);
        r.Analyze("234123412347", All).Should().BeEmpty();
        r.Analyze("200000000002", All).Should().BeEmpty(); // palindrome rejected
    }

    [Fact]
    public void ShouldValidateGstin_WhenStructureValid()
    {
        var r = new InGstinRecognizer();
        r.Analyze("27AAPFU0939F1ZV", All).Should().ContainSingle().Which.Score.Should().Be(EntityRecognizer.MaxScore);
        r.Analyze("99AAPFU0939F1ZV", All).Should().BeEmpty(); // state code out of range
    }

    [Fact]
    public void ShouldPromoteVehicleRegistration_WhenStateDistrictValid()
    {
        var r = new InVehicleRegistrationRecognizer();
        // MH (Maharashtra) RTO district 12 is in the map -> promoted to max
        r.Analyze("MH12AB1234", All).Should().Contain(x => x.Score == EntityRecognizer.MaxScore);
        // district 99 is not a valid MH RTO -> keeps its base pattern score, never promoted
        r.Analyze("MH99AB1234", All).Should().NotContain(x => x.Score == EntityRecognizer.MaxScore)
            .And.NotBeEmpty();
    }

    [Fact]
    public void ShouldDetectPan_WhenFormatValid()
    {
        new InPanRecognizer().Analyze("ABCPD1234E", All).Should().NotBeEmpty();
    }

    // --- Italy ---

    [Fact]
    public void ShouldPromoteFiscalCode_WhenControlCharMatches()
    {
        var r = new ItFiscalCodeRecognizer();
        r.Analyze("MRTMTT25D09F205Z", All).Should().ContainSingle().Which.Score.Should().Be(EntityRecognizer.MaxScore);
        // a wrong control char keeps the base score rather than dropping the match
        r.Analyze("MRTMTT25D09F205A", All).Should().ContainSingle().Which.Score.Should().BeLessThan(EntityRecognizer.MaxScore);
    }

    [Fact]
    public void ShouldValidateVatCode_WhenChecksumValid()
    {
        var r = new ItVatCodeRecognizer();
        r.Analyze("07643520567", All).Should().ContainSingle().Which.Score.Should().Be(EntityRecognizer.MaxScore);
        r.Analyze("07643520568", All).Should().BeEmpty();
    }

    // --- Spain ---

    [Fact]
    public void ShouldValidateNif_WhenControlLetterValid()
    {
        var r = new EsNifRecognizer();
        r.Analyze("12345678Z", All).Should().ContainSingle().Which.Score.Should().Be(EntityRecognizer.MaxScore);
        r.Analyze("12345678A", All).Should().BeEmpty();
    }

    [Fact]
    public void ShouldValidateNie_WhenControlLetterValid()
    {
        var r = new EsNieRecognizer();
        r.Analyze("X1234567L", All).Should().ContainSingle().Which.Score.Should().Be(EntityRecognizer.MaxScore);
        r.Analyze("X1234567A", All).Should().BeEmpty();
    }

    // --- The Netherlands ---

    [Theory]
    [InlineData("123456782")]
    [InlineData("744729063")]
    public void ShouldValidateBsn_When11ProefValid(string bsn)
    {
        // a passing 11-proef abstains rather than promoting to MaxScore: the check is too weak on its
        // own, so the match keeps its low base score and needs context to clear the threshold
        var r = new NlBsnRecognizer();
        var result = r.Analyze(bsn, All).Should().ContainSingle().Subject;
        result.Score.Should().Be(0.05);
        result.Score.Should().BeLessThan(EntityRecognizer.MaxScore);
    }

    [Fact]
    public void ShouldRequireContextForBsn_BecauseThe11ProefAloneIsWeak()
    {
        // a mod-11 check passes for roughly one in eleven nine-digit numbers, so an order reference
        // must not be redacted as a BSN without a Dutch context word nearby
        var engine = new PiiEngine(new PiiOptions { Countries = [PiiCountries.Nl] });

        engine.Analyze("Order reference 111222333 shipped.")
            .Should().NotContain(r => r.EntityType == PiiEntities.NlBsn);

        engine.Analyze("Burgerservicenummer 111222333 is geregistreerd.")
            .Should().Contain(r => r.EntityType == PiiEntities.NlBsn);
    }

    [Fact]
    public void ShouldRequireContextForPostcode_BecauseTheShapeCollidesWithProse()
    {
        var engine = new PiiEngine(new PiiOptions { Countries = [PiiCountries.Nl] });

        engine.Analyze("The treaty dates to 3000 BC and later.")
            .Should().NotContain(r => r.EntityType == PiiEntities.NlPostcode);

        engine.Analyze("Het adres is 1234 AB Amsterdam.")
            .Should().Contain(r => r.EntityType == PiiEntities.NlPostcode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("X")]
    [InlineData("1")]
    [InlineData("42")]
    [InlineData("123456789")]
    public void ShouldValidateBsn_When11ProefInvalid(string bsn)
    {
        var r = new NlBsnRecognizer();
        r.Analyze(bsn, All).Should().BeEmpty();
    }

    [Theory]
    [InlineData("1234 AB")]
    [InlineData("5678 CD")]
    public void ShouldValidatePostcode_WhenStructureValid(string postcode)
    {
        var r = new NlPostcodeRecognizer();
        r.Analyze(postcode, All).Should().ContainSingle().Which.Score.Should().Be(0.1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("X")]
    [InlineData("1")]
    [InlineData("0000AA")]
    [InlineData("1234SA")]
    [InlineData("1234SD")]
    [InlineData("1234SS")]
    public void ShouldValidatePostcode_WhenStructureInvalid(string postcode)
    {
        var r = new NlPostcodeRecognizer();
        r.Analyze(postcode, All).Should().BeEmpty();
    }

    [Theory]
    [InlineData("NL1234567")]
    [InlineData("NX90R18C7")]
    public void ShouldValidatePassport_WhenStructureValid(string passport)
    {
        var r = new NlPassportRecognizer();
        r.Analyze(passport, All).Should().ContainSingle().Which.Score.Should().Be(0.2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("X")]
    [InlineData("1")]
    [InlineData("NL123456")]
    [InlineData("NX90R18C")]
    public void ShouldValidatePassport_WhenStructureInvalid(string passport)
    {
        var r = new NlPassportRecognizer();
        r.Analyze(passport, All).Should().BeEmpty();
    }
}