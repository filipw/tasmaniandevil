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
    [AzureLanguageFact]
    public async Task DetectAsync_ShouldDetectPerson_AgainstTheLiveService()
    {
        var endpoint = Environment.GetEnvironmentVariable("AZURE_LANGUAGE_ENDPOINT")!;
        var key = Environment.GetEnvironmentVariable("AZURE_LANGUAGE_KEY")!;

        using var client = new AzurePiiClient(new AzurePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Endpoint = endpoint,
            SubscriptionKey = key,
        });

        var result = await client.DetectAsync("My name is John Smith and I live in Seattle.", "en");

        result.Entities.Should().Contain(e => e.Category == "Person");
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
