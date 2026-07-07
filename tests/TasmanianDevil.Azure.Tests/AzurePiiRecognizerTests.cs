using System.Net;
using System.Text;
using TasmanianDevil.Analyzer;
using TasmanianDevil.Azure;
using TasmanianDevil.Recognizers.Generic;
using FluentAssertions;
using Xunit;

namespace TasmanianDevil.Azure.Tests;

public class AzurePiiRecognizerTests
{
    [Fact]
    public void SupportedLanguage_ShouldOverride_OptionsSupportedLanguage_WhenProvided()
    {
        var options = new AzurePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Endpoint = "https://my-resource.cognitiveservices.azure.com",
            SubscriptionKey = "key",
            SupportedLanguage = "en",
        };
        var client = new AzurePiiClient(new HttpClient(new FakeAzureHandler("""{ "results": { "documents": [] } }""")), options);

        var recognizer = new AzurePiiRecognizer(client, options, supportedLanguage: "de");

        recognizer.SupportedLanguage.Should().Be("de");
    }

    [Theory]
    [InlineData("Person", "PERSON")]
    [InlineData("Address", "ADDRESS")]
    [InlineData("PhoneNumber", "PHONE_NUMBER")]
    [InlineData("Email", "EMAIL_ADDRESS")]
    [InlineData("Organization", "ORGANIZATION")]
    [InlineData("DateTime", "DATE_TIME")]
    [InlineData("CreditCardNumber", "CREDIT_CARD")]
    [InlineData("USSocialSecurityNumber", "US_SSN")]
    [InlineData("IPAddress", "IP_ADDRESS")]
    [InlineData("IBAN", "IBAN_CODE")]
    [InlineData("URL", "URL")]
    public async Task AnalyzeAsync_ShouldMapEveryDefaultAzureCategory_ToItsCanonicalEntityType(string azureCategory, string canonical)
    {
        var recognizer = BuildRecognizer([canonical], BuildResponse((azureCategory, 0, 4, 0.9)));

        var results = await recognizer.AnalyzeAsync("text", [canonical]);

        results.Should().ContainSingle(r => r.EntityType == canonical);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldRemapEntityType_ViaCategoryMapOverride()
    {
        var recognizer = BuildRecognizer(
            ["PERSON"],
            BuildResponse(("CustomPerson", 0, 4, 0.9)),
            categoryMap: new Dictionary<string, string> { ["CustomPerson"] = "PERSON" });

        var results = await recognizer.AnalyzeAsync("text", ["PERSON"]);

        results.Should().ContainSingle(r => r.EntityType == "PERSON");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldDropEntities_BelowTheConfidenceThreshold()
    {
        var recognizer = BuildRecognizer(
            ["PERSON"],
            BuildResponse(("Person", 0, 4, 0.3), ("Person", 10, 4, 0.9)),
            confidenceThreshold: 0.5);

        var results = await recognizer.AnalyzeAsync("0123456789abcdef", ["PERSON"]);

        results.Should().ContainSingle();
        results[0].Start.Should().Be(10);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldDropEntityType_NotAmongTheRequestedEntities()
    {
        // Azure returns both Person and Address, but this call only requested PERSON
        var recognizer = BuildRecognizer(
            ["PERSON", "ADDRESS"],
            BuildResponse(("Person", 0, 4, 0.9), ("Address", 5, 13, 0.9)));

        var results = await recognizer.AnalyzeAsync("John 221B Baker St", ["PERSON"]);

        results.Should().ContainSingle();
        results[0].EntityType.Should().Be("PERSON");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldClampScore_ToTheValidRange()
    {
        var recognizer = BuildRecognizer(["PERSON"], BuildResponse(("Person", 0, 4, 1.5)));

        var results = await recognizer.AnalyzeAsync("text", ["PERSON"]);

        results[0].Score.Should().Be(EntityRecognizer.MaxScore);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReturnEmpty_WhenFailOpenAndClientThrows()
    {
        Exception? observed = null;
        var recognizer = BuildRecognizer(
            ["PERSON"],
            throwing: new InvalidOperationException("azure is down"),
            onError: ex => observed = ex);

        var results = await recognizer.AnalyzeAsync("text", ["PERSON"]);

        results.Should().BeEmpty();
        observed.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldPropagateException_WhenFailClosedAndClientThrows()
    {
        var recognizer = BuildRecognizer(
            ["PERSON"],
            throwing: new InvalidOperationException("azure is down"),
            failOpen: false);

        var act = async () => await recognizer.AnalyzeAsync("text", ["PERSON"]);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReturnEmpty_WhenClientExceedsTimeout()
    {
        var recognizer = BuildRecognizer(["PERSON"], delay: TimeSpan.FromSeconds(5), timeout: TimeSpan.FromMilliseconds(50));

        var results = await recognizer.AnalyzeAsync("text", ["PERSON"]);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldPropagateCancellation_WhenCallerTokenIsCanceled()
    {
        var recognizer = BuildRecognizer(["PERSON"], delay: TimeSpan.FromSeconds(5));
        using var cts = new CancellationTokenSource();

        var task = recognizer.AnalyzeAsync("text", ["PERSON"], cts.Token).AsTask();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public void Analyze_ShouldReturnEmpty_BecauseTheRecognizerIsAsyncOnly()
    {
        var recognizer = BuildRecognizer(["PERSON"], BuildResponse(("Person", 0, 4, 0.9)));

        recognizer.Analyze("John said hi", ["PERSON"]).Should().BeEmpty();
        recognizer.RequiresAsync.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldMergeThroughAnalyzerEngine_AlongsideLocalRecognizers()
    {
        const string text = "email John Smith at john@example.com";
        var personStart = text.IndexOf("John Smith", StringComparison.Ordinal);
        var azureRecognizer = BuildRecognizer(["PERSON"], BuildResponse(("Person", personStart, "John Smith".Length, 0.9)));

        var engine = new AnalyzerEngine(new RecognizerRegistry([new EmailRecognizer(), azureRecognizer]), defaultScoreThreshold: 0);

        var results = await engine.AnalyzeAsync(text);

        results.Should().Contain(r => r.EntityType == "PERSON" && r.Start == personStart);
        results.Should().Contain(r => r.EntityType == "EMAIL_ADDRESS");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldDropSpan_WhenOffsetExceedsTextLength()
    {
        // a span running past the end of the analyzed text would throw when sliced downstream; drop it
        var recognizer = BuildRecognizer(["PERSON"], BuildResponse(("Person", 2, 10, 0.9)));

        var results = await recognizer.AnalyzeAsync("John", ["PERSON"]);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReturnEmpty_WhenAzureReportsDocumentError_AndFailOpen()
    {
        Exception? observed = null;
        var recognizer = BuildRecognizer(
            ["PERSON"],
            responseJson: DocumentErrorResponse("UnsupportedLanguageCode", "language not supported"),
            onError: ex => observed = ex);

        var results = await recognizer.AnalyzeAsync("text", ["PERSON"]);

        results.Should().BeEmpty();
        observed.Should().BeOfType<AzurePiiException>().Which.Code.Should().Be("UnsupportedLanguageCode");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldThrowAzurePiiException_WhenAzureReportsDocumentError_AndFailClosed()
    {
        var recognizer = BuildRecognizer(
            ["PERSON"],
            responseJson: DocumentErrorResponse("InvalidDocument", "bad doc"),
            failOpen: false);

        var act = async () => await recognizer.AnalyzeAsync("text", ["PERSON"]);

        (await act.Should().ThrowAsync<AzurePiiException>()).Which.Code.Should().Be("InvalidDocument");
    }

    private static AzurePiiRecognizer BuildRecognizer(
        IReadOnlyList<string> supportedEntities,
        string? responseJson = null,
        Exception? throwing = null,
        TimeSpan? delay = null,
        TimeSpan? timeout = null,
        bool failOpen = true,
        double? confidenceThreshold = null,
        IReadOnlyDictionary<string, string>? categoryMap = null,
        Action<Exception>? onError = null)
    {
        var handler = new FakeAzureHandler(responseJson, throwing, delay);
        var options = new AzurePiiOptions
        {
            SupportedEntities = supportedEntities,
            Endpoint = "https://my-resource.cognitiveservices.azure.com",
            SubscriptionKey = "key",
            Timeout = timeout ?? TimeSpan.FromSeconds(10),
            FailOpen = failOpen,
            ConfidenceThreshold = confidenceThreshold,
            CategoryMap = categoryMap,
            OnError = onError,
        };

        var client = new AzurePiiClient(new HttpClient(handler), options);
        return new AzurePiiRecognizer(client, options);
    }

    private static string BuildResponse(params (string Category, int Offset, int Length, double Score)[] entities)
    {
        var entityJson = string.Join(",", entities.Select(e =>
            $$"""{ "text": "x", "category": "{{e.Category}}", "offset": {{e.Offset}}, "length": {{e.Length}}, "confidenceScore": {{e.Score}} }"""));
        return $$"""{ "results": { "documents": [ { "id": "1", "entities": [ {{entityJson}} ] } ] } }""";
    }

    private static string DocumentErrorResponse(string code, string message) =>
        $$"""{ "results": { "documents": [], "errors": [ { "id": "1", "error": { "code": "{{code}}", "message": "{{message}}" } } ] } }""";

    private sealed class FakeAzureHandler(string? responseJson, Exception? throwing = null, TimeSpan? delay = null) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (delay is { } d)
            {
                await Task.Delay(d, cancellationToken).ConfigureAwait(false);
            }

            if (throwing is not null)
            {
                throw throwing;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseJson ?? """{ "results": { "documents": [ { "id": "1", "entities": [] } ] } }""",
                    Encoding.UTF8,
                    "application/json"),
            };
        }
    }
}
