using System.Net;
using System.Text;
using System.Text.Json;
using TasmanianDevil.Remote;
using FluentAssertions;
using Xunit;

namespace TasmanianDevil.Remote.Tests;

public class HttpPiiDetectionClientTests
{
    [Fact]
    public async Task DetectAsync_ShouldPostTheWireContractRequest_AndParseTheResponse()
    {
        var handler = new CapturingHandler("""{ "entities": [ { "type": "PERSON", "start": 3, "end": 11, "score": 0.98 } ] }""");
        using var httpClient = new HttpClient(handler);
        var client = new HttpPiiDetectionClient(httpClient, new RemotePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Endpoint = "https://detector.example.com/detect",
        });

        var results = await client.DetectAsync("hi John Smith", "en", ["PERSON"]);

        results.Should().ContainSingle();
        results[0].Should().Be(new RemotePiiEntity("PERSON", 3, 11, 0.98));

        handler.LastRequestUri.Should().Be("https://detector.example.com/detect");
        var body = JsonDocument.Parse(handler.LastRequestBody!);
        body.RootElement.GetProperty("text").GetString().Should().Be("hi John Smith");
        body.RootElement.GetProperty("language").GetString().Should().Be("en");
        body.RootElement.GetProperty("entities").EnumerateArray().Select(e => e.GetString()).Should().Equal("PERSON");
        body.RootElement.TryGetProperty("includeRedactedText", out _).Should().BeFalse();
    }

    [Fact]
    public async Task DetectAsync_ShouldSendAuthHeader_WhenConfigured()
    {
        var handler = new CapturingHandler("""{ "entities": [] }""");
        using var httpClient = new HttpClient(handler);
        var client = new HttpPiiDetectionClient(httpClient, new RemotePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Endpoint = "https://detector.example.com/detect",
            AuthHeaderName = "X-Api-Key",
            AuthHeaderValue = "secret-123",
        });

        await client.DetectAsync("text", "en", ["PERSON"]);

        handler.LastRequestHeaders!.GetValues("X-Api-Key").Should().ContainSingle("secret-123");
    }

    [Fact]
    public async Task DetectAsync_ShouldRequestRedactedTextPassthrough_WhenEnabled()
    {
        var handler = new CapturingHandler("""{ "entities": [], "redactedText": "hi <PERSON>" }""");
        using var httpClient = new HttpClient(handler);
        var client = new HttpPiiDetectionClient(httpClient, new RemotePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Endpoint = "https://detector.example.com/detect",
            RequestRedactedTextPassthrough = true,
        });

        await client.DetectAsync("hi John", "en", ["PERSON"]);

        var body = JsonDocument.Parse(handler.LastRequestBody!);
        body.RootElement.GetProperty("includeRedactedText").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task DetectAsync_ShouldReturnEmpty_WhenResponseHasNoEntities()
    {
        var handler = new CapturingHandler("""{ "entities": [] }""");
        using var httpClient = new HttpClient(handler);
        var client = new HttpPiiDetectionClient(httpClient, new RemotePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Endpoint = "https://detector.example.com/detect",
        });

        var results = await client.DetectAsync("no pii here", "en", ["PERSON"]);

        results.Should().BeEmpty();
    }

    [Fact]
    public void Ctor_ShouldThrow_WhenEndpointIsMissing()
    {
        using var httpClient = new HttpClient();

        var act = () => new HttpPiiDetectionClient(httpClient, new RemotePiiOptions { SupportedEntities = ["PERSON"] });

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
