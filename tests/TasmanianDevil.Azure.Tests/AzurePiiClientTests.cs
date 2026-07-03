using System.Net;
using System.Text;
using System.Text.Json;
using TasmanianDevil.Azure;
using FluentAssertions;
using Xunit;

namespace TasmanianDevil.Azure.Tests;

public class AzurePiiClientTests
{
    // the exact response shape documented on the Azure AI Language "identify PII" how-to page for a
    // PiiEntityRecognition request restricted to the Person category
    private const string DocumentedSampleResponse = """
        {
            "kind": "PiiEntityRecognitionResults",
            "results": {
                "documents": [
                    {
                        "redactedText": "We went to Contoso foodplace located at downtown Seattle last week for a dinner party, and we adore the spot! They provide marvelous food and they have a great menu. The chief cook happens to be the owner (I think his name is ********) and he is super nice, coming out of the kitchen and greeted us all.",
                        "id": "1",
                        "entities": [
                            {
                                "text": "John Doe",
                                "category": "Person",
                                "offset": 226,
                                "length": 8,
                                "confidenceScore": 0.98
                            }
                        ],
                        "warnings": []
                    }
                ],
                "errors": [],
                "modelVersion": "2021-01-15"
            }
        }
        """;

    [Fact]
    public async Task DetectAsync_ShouldParseTheDocumentedSampleResponse()
    {
        var handler = new CapturingHandler(DocumentedSampleResponse);
        using var httpClient = new HttpClient(handler);
        var client = new AzurePiiClient(httpClient, new AzurePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Endpoint = "https://my-resource.cognitiveservices.azure.com",
            SubscriptionKey = "key",
        });

        var result = await client.DetectAsync("...", "en");

        result.Entities.Should().ContainSingle();
        result.Entities[0].Should().Be(new AzurePiiEntity("John Doe", "Person", null, 226, 8, 0.98));
    }

    [Fact]
    public async Task DetectAsync_ShouldThrowAzurePiiException_WhenTheServiceReportsADocumentError()
    {
        // Azure returns per-document failures on an HTTP 200 in results.errors, not as a transport error
        const string errorResponse =
            """{ "results": { "documents": [], "errors": [ { "id": "1", "error": { "code": "InvalidDocument", "message": "Document text is empty." } } ] } }""";
        var handler = new CapturingHandler(errorResponse);
        using var httpClient = new HttpClient(handler);
        var client = new AzurePiiClient(httpClient, new AzurePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Endpoint = "https://my-resource.cognitiveservices.azure.com",
            SubscriptionKey = "key",
        });

        var act = async () => await client.DetectAsync("", "en");

        (await act.Should().ThrowAsync<AzurePiiException>()).Which.Code.Should().Be("InvalidDocument");
    }

    [Fact]
    public async Task DetectAsync_ShouldPostToTheAnalyzeTextEndpoint_WithApiVersion()
    {
        var handler = new CapturingHandler(DocumentedSampleResponse);
        using var httpClient = new HttpClient(handler);
        var client = new AzurePiiClient(httpClient, new AzurePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Endpoint = "https://my-resource.cognitiveservices.azure.com",
            SubscriptionKey = "key",
        });

        await client.DetectAsync("hi", "en");

        handler.LastRequestUri.Should().Be("https://my-resource.cognitiveservices.azure.com/language/:analyze-text?api-version=2024-11-01");
    }

    [Theory]
    [InlineData(AzurePiiDomain.None, "none")]
    [InlineData(AzurePiiDomain.Phi, "phi")]
    public async Task DetectAsync_ShouldSerializeStringIndexTypeLoggingOptOutAndDomain_Correctly(AzurePiiDomain domain, string expectedDomain)
    {
        var handler = new CapturingHandler(DocumentedSampleResponse);
        using var httpClient = new HttpClient(handler);
        var client = new AzurePiiClient(httpClient, new AzurePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Endpoint = "https://my-resource.cognitiveservices.azure.com",
            SubscriptionKey = "key",
            Domain = domain,
            LoggingOptOut = true,
        });

        await client.DetectAsync("hi", "en");

        var body = JsonDocument.Parse(handler.LastRequestBody!).RootElement;
        var parameters = body.GetProperty("parameters");
        parameters.GetProperty("stringIndexType").GetString().Should().Be("Utf16CodeUnit");
        parameters.GetProperty("loggingOptOut").GetBoolean().Should().BeTrue();
        parameters.GetProperty("domain").GetString().Should().Be(expectedDomain);
        body.GetProperty("kind").GetString().Should().Be("PiiEntityRecognition");
    }

    [Fact]
    public async Task DetectAsync_ShouldSendSubscriptionKeyHeader_WhenConfigured()
    {
        var handler = new CapturingHandler(DocumentedSampleResponse);
        using var httpClient = new HttpClient(handler);
        var client = new AzurePiiClient(httpClient, new AzurePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Endpoint = "https://my-resource.cognitiveservices.azure.com",
            SubscriptionKey = "secret-key",
        });

        await client.DetectAsync("hi", "en");

        handler.LastRequestHeaders!.GetValues("Ocp-Apim-Subscription-Key").Should().ContainSingle("secret-key");
    }

    [Fact]
    public async Task DetectAsync_ShouldSendBearerToken_ViaTokenProvider()
    {
        var handler = new CapturingHandler(DocumentedSampleResponse);
        using var httpClient = new HttpClient(handler);
        var client = new AzurePiiClient(httpClient, new AzurePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Endpoint = "https://my-resource.cognitiveservices.azure.com",
            TokenProvider = _ => new ValueTask<string>("aad-token"),
        });

        await client.DetectAsync("hi", "en");

        handler.LastRequestHeaders!.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequestHeaders!.Authorization!.Parameter.Should().Be("aad-token");
    }

    [Fact]
    public async Task DetectAsync_ShouldDropRedactedText_WhenPassthroughDisabled()
    {
        var handler = new CapturingHandler(DocumentedSampleResponse);
        using var httpClient = new HttpClient(handler);
        var client = new AzurePiiClient(httpClient, new AzurePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Endpoint = "https://my-resource.cognitiveservices.azure.com",
            SubscriptionKey = "key",
        });

        var result = await client.DetectAsync("hi", "en");

        result.RedactedText.Should().BeNull();
    }

    [Fact]
    public async Task DetectAsync_ShouldSurfaceRedactedText_WhenPassthroughEnabled()
    {
        var handler = new CapturingHandler(DocumentedSampleResponse);
        using var httpClient = new HttpClient(handler);
        var client = new AzurePiiClient(httpClient, new AzurePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Endpoint = "https://my-resource.cognitiveservices.azure.com",
            SubscriptionKey = "key",
            PassthroughRedactedText = true,
        });

        var result = await client.DetectAsync("hi", "en");

        result.RedactedText.Should().Contain("********");
    }

    [Fact]
    public void Ctor_ShouldThrow_WhenEndpointIsMissing()
    {
        using var httpClient = new HttpClient();

        var act = () => new AzurePiiClient(httpClient, new AzurePiiOptions { SupportedEntities = ["PERSON"], Endpoint = "  ", SubscriptionKey = "key" });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_ShouldThrow_WhenNeitherSubscriptionKeyNorTokenProviderIsConfigured()
    {
        using var httpClient = new HttpClient();

        var act = () => new AzurePiiClient(httpClient, new AzurePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Endpoint = "https://my-resource.cognitiveservices.azure.com",
        });

        act.Should().Throw<ArgumentException>();
    }

    private sealed class CapturingHandler(string responseJson) : HttpMessageHandler
    {
        public string? LastRequestUri { get; private set; }
        public string? LastRequestBody { get; private set; }
        public System.Net.Http.Headers.HttpRequestHeaders? LastRequestHeaders { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString();
            LastRequestHeaders = request.Headers;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }
}
