using TasmanianDevil;
using TasmanianDevil.Analyzer;
using TasmanianDevil.Azure;
using FluentAssertions;
using Xunit;

namespace TasmanianDevil.Azure.Tests;

/// <summary>
/// End-to-end tests against a real Azure AI Language resource. Gated behind
/// <c>AZURE_LANGUAGE_ENDPOINT</c> / <c>AZURE_LANGUAGE_KEY</c>; skipped otherwise.
/// </summary>
public class AzureLanguagePiiE2ETests
{
    private static readonly string Endpoint = Environment.GetEnvironmentVariable("AZURE_LANGUAGE_ENDPOINT") ?? "";
    private static readonly string Key = Environment.GetEnvironmentVariable("AZURE_LANGUAGE_KEY") ?? "";

    [AzureLanguageFact]
    public async Task DetectAsync_ShouldDetectPerson_AgainstTheLiveService()
    {
        using var client = new AzurePiiClient(new AzurePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Endpoint = Endpoint,
            SubscriptionKey = Key,
        });

        var result = await client.DetectAsync("My name is John Smith and I live in Seattle.", "en");

        result.Entities.Should().Contain(e => e.Category == "Person");
    }

    [AzureLanguageFact]
    public async Task DetectAsync_ShouldDetectMultipleCategories_AgainstTheLiveService()
    {
        using var client = new AzurePiiClient(new AzurePiiOptions
        {
            SupportedEntities = ["PERSON", "PHONE_NUMBER", "EMAIL_ADDRESS"],
            Endpoint = Endpoint,
            SubscriptionKey = Key,
        });

        var result = await client.DetectAsync(
            "My name is John Smith, my phone number is 425-882-8080 and my email is john.smith@contoso.com.",
            "en");

        result.Entities.Should().Contain(e => e.Category == "Person");
        result.Entities.Should().Contain(e => e.Category == "PhoneNumber");
        result.Entities.Should().Contain(e => e.Category == "Email");
    }

    [AzureLanguageFact]
    public async Task DetectAsync_ShouldSurfaceRedactedText_WhenPassthroughEnabled_AgainstTheLiveService()
    {
        using var client = new AzurePiiClient(new AzurePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Endpoint = Endpoint,
            SubscriptionKey = Key,
            PassthroughRedactedText = true,
        });

        var result = await client.DetectAsync("My name is John Smith and I live in Seattle.", "en");

        result.RedactedText.Should().NotBeNull();
        result.RedactedText.Should().NotContain("John Smith");
    }

    [AzureLanguageFact]
    public async Task DetectAsync_ShouldFilterByPiiCategories_AgainstTheLiveService()
    {
        using var client = new AzurePiiClient(new AzurePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Endpoint = Endpoint,
            SubscriptionKey = Key,
            PiiCategories = ["Person"],
        });

        var result = await client.DetectAsync(
            "My name is John Smith, my phone number is 425-882-8080.",
            "en");

        result.Entities.Should().OnlyContain(e => e.Category == "Person");
    }

    [AzureLanguageFact]
    public async Task DetectAsync_ShouldUsePhiDomain_AgainstTheLiveService()
    {
        using var client = new AzurePiiClient(new AzurePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Endpoint = Endpoint,
            SubscriptionKey = Key,
            Domain = AzurePiiDomain.Phi,
        });

        var result = await client.DetectAsync(
            "The patient was diagnosed with diabetes and prescribed Metformin.",
            "en");

        result.Entities.Should().NotBeEmpty();
    }

    [AzureLanguageFact]
    public async Task AnalyzeAsync_ShouldMapPersonEntity_ThroughTheRecognizer_AgainstTheLiveService()
    {
        var options = new AzurePiiOptions
        {
            SupportedEntities = [PiiEntities.Person],
            Endpoint = Endpoint,
            SubscriptionKey = Key,
        };
        using var client = new AzurePiiClient(options);
        var recognizer = new AzurePiiRecognizer(client, options);

        var results = await recognizer.AnalyzeAsync("My name is John Smith and I live in Seattle.", [PiiEntities.Person]);

        results.Should().Contain(r => r.EntityType == PiiEntities.Person);
    }

    [AzureLanguageFact]
    public async Task AnalyzeAsync_ShouldMergeAzureRecognizer_WithLocalRecognizers_ThroughAnalyzerEngine_AgainstTheLiveService()
    {
        var options = new AzurePiiOptions
        {
            SupportedEntities = [PiiEntities.Person],
            Endpoint = Endpoint,
            SubscriptionKey = Key,
        };
        using var client = new AzurePiiClient(options);
        var registry = PiiRecognizers.CreateRegistry("en");
        registry.AddRecognizer(new AzurePiiRecognizer(client, options));
        var engine = new AnalyzerEngine(registry);

        var results = await engine.AnalyzeAsync("My name is John Smith and my email is john.smith@contoso.com.");

        results.Should().Contain(r => r.EntityType == PiiEntities.Person);
        results.Should().Contain(r => r.EntityType == PiiEntities.EmailAddress);
    }

    [AzureLanguageFact]
    public async Task DeidentifyAsync_ShouldRedactPersonViaAzure_AndEmailLocally_ThroughPiiEngine_AgainstTheLiveService()
    {
        var options = new AzurePiiOptions
        {
            SupportedEntities = [PiiEntities.Person],
            Endpoint = Endpoint,
            SubscriptionKey = Key,
        };
        using var client = new AzurePiiClient(options);
        var azureRecognizer = new AzurePiiRecognizer(client, options);
        var engine = new PiiEngine(extraRecognizers: [azureRecognizer]);

        var result = await engine.DeidentifyAsync("My name is John Smith and my email is john.smith@contoso.com.");

        result.AnonymizedText.Should().Contain($"<{PiiEntities.Person}>");
        result.AnonymizedText.Should().Contain($"<{PiiEntities.EmailAddress}>");
        result.AnonymizedText.Should().NotContain("John Smith");
        result.AnonymizedText.Should().NotContain("john.smith@contoso.com");
    }
}

/// <summary>Skip fact attribute that checks for Azure AI Language availability.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AzureLanguageFactAttribute : FactAttribute
{
    /// <summary>Initializes a new instance of the <see cref="AzureLanguageFactAttribute"/> class.</summary>
    public AzureLanguageFactAttribute()
    {
        var endpoint = Environment.GetEnvironmentVariable("AZURE_LANGUAGE_ENDPOINT");
        var key = Environment.GetEnvironmentVariable("AZURE_LANGUAGE_KEY");

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
        {
            Skip = "Set AZURE_LANGUAGE_ENDPOINT and AZURE_LANGUAGE_KEY to run Azure AI Language PII e2e tests.";
        }
    }
}
