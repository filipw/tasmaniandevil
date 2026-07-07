using TasmanianDevil.Analyzer;
using TasmanianDevil.Recognizers.Generic;
using TasmanianDevil.Remote;
using FluentAssertions;
using Xunit;

namespace TasmanianDevil.Remote.Tests;

public class RemotePiiRecognizerTests
{
    [Fact]
    public void SupportedLanguage_ShouldOverride_OptionsSupportedLanguage_WhenProvided()
    {
        var recognizer = new RemotePiiRecognizer(
            new StubPiiDetectionClient([]),
            new RemotePiiOptions { SupportedEntities = ["PERSON"], SupportedLanguage = "en" },
            supportedLanguage: "de");

        recognizer.SupportedLanguage.Should().Be("de");
    }

    [Fact]
    public void SupportedLanguage_ShouldFallBackToOptions_WhenOverrideIsNull()
    {
        var recognizer = new RemotePiiRecognizer(
            new StubPiiDetectionClient([]),
            new RemotePiiOptions { SupportedEntities = ["PERSON"], SupportedLanguage = "fr" });

        recognizer.SupportedLanguage.Should().Be("fr");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldMapRemoteEntities_ToRecognizerResults()
    {
        var client = new StubPiiDetectionClient(
            [new RemotePiiEntity("PERSON", 8, 12, 0.95)]);
        var recognizer = new RemotePiiRecognizer(client, new RemotePiiOptions { SupportedEntities = ["PERSON"] });

        var results = await recognizer.AnalyzeAsync("call me John please", ["PERSON"]);

        results.Should().ContainSingle();
        results[0].EntityType.Should().Be("PERSON");
        results[0].Start.Should().Be(8);
        results[0].End.Should().Be(12);
        results[0].Score.Should().Be(0.95);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldRemapEntityType_ViaCategoryMap()
    {
        var client = new StubPiiDetectionClient([new RemotePiiEntity("NAME", 0, 4, 0.9)]);
        var recognizer = new RemotePiiRecognizer(client, new RemotePiiOptions
        {
            SupportedEntities = ["PERSON"],
            CategoryMap = new Dictionary<string, string> { ["NAME"] = "PERSON" },
        });

        var results = await recognizer.AnalyzeAsync("John said hi", ["PERSON"]);

        results.Should().ContainSingle(r => r.EntityType == "PERSON");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldDropOutOfRangeOffsets_FromMisbehavingRemote()
    {
        var client = new StubPiiDetectionClient([new RemotePiiEntity("PERSON", 100, 200, 0.9)]);
        var recognizer = new RemotePiiRecognizer(client, new RemotePiiOptions { SupportedEntities = ["PERSON"] });

        var results = await recognizer.AnalyzeAsync("short text", ["PERSON"]);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldDropEntityType_NotAmongTheRequestedEntities()
    {
        // the remote server declares (and is asked for) only PERSON, but misbehaves and also returns
        // an ADDRESS span - AnalyzerEngine trusts recognizers to only emit what was requested, so the
        // recognizer itself must drop anything outside that set
        var client = new StubPiiDetectionClient(
        [
            new RemotePiiEntity("PERSON", 0, 4, 0.9),
            new RemotePiiEntity("ADDRESS", 5, 15, 0.9),
        ]);
        var recognizer = new RemotePiiRecognizer(client, new RemotePiiOptions { SupportedEntities = ["PERSON", "ADDRESS"] });

        var results = await recognizer.AnalyzeAsync("John 221B Baker", ["PERSON"]);

        results.Should().ContainSingle();
        results[0].EntityType.Should().Be("PERSON");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldClampScore_ToTheValidRange()
    {
        var client = new StubPiiDetectionClient([new RemotePiiEntity("PERSON", 0, 4, 1.5)]);
        var recognizer = new RemotePiiRecognizer(client, new RemotePiiOptions { SupportedEntities = ["PERSON"] });

        var results = await recognizer.AnalyzeAsync("John said hi", ["PERSON"]);

        results.Should().ContainSingle();
        results[0].Score.Should().Be(EntityRecognizer.MaxScore);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldOnlyRequestSupportedEntities_FromTheClient()
    {
        var client = new StubPiiDetectionClient([]);
        var recognizer = new RemotePiiRecognizer(client, new RemotePiiOptions { SupportedEntities = ["PERSON"] });

        await recognizer.AnalyzeAsync("text", ["PERSON", "CREDIT_CARD"]);

        client.LastRequestedEntities.Should().BeEquivalentTo(["PERSON"]);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReturnEmpty_WhenFailOpenAndClientThrows()
    {
        var client = new StubPiiDetectionClient(throwing: new InvalidOperationException("remote is down"));
        Exception? observed = null;
        var recognizer = new RemotePiiRecognizer(client, new RemotePiiOptions
        {
            SupportedEntities = ["PERSON"],
            FailOpen = true,
            OnError = ex => observed = ex,
        });

        var results = await recognizer.AnalyzeAsync("text", ["PERSON"]);

        results.Should().BeEmpty();
        observed.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldPropagateException_WhenFailClosedAndClientThrows()
    {
        var client = new StubPiiDetectionClient(throwing: new InvalidOperationException("remote is down"));
        var recognizer = new RemotePiiRecognizer(client, new RemotePiiOptions { SupportedEntities = ["PERSON"], FailOpen = false });

        var act = async () => await recognizer.AnalyzeAsync("text", ["PERSON"]);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReturnEmpty_WhenClientExceedsTimeout()
    {
        var client = new StubPiiDetectionClient(delay: TimeSpan.FromSeconds(5));
        var recognizer = new RemotePiiRecognizer(client, new RemotePiiOptions
        {
            SupportedEntities = ["PERSON"],
            Timeout = TimeSpan.FromMilliseconds(50),
        });

        var results = await recognizer.AnalyzeAsync("text", ["PERSON"]);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldPropagateCancellation_WhenCallerTokenIsCanceled()
    {
        var client = new StubPiiDetectionClient(delay: TimeSpan.FromSeconds(5));
        var recognizer = new RemotePiiRecognizer(client, new RemotePiiOptions { SupportedEntities = ["PERSON"] });
        using var cts = new CancellationTokenSource();

        var task = recognizer.AnalyzeAsync("text", ["PERSON"], cts.Token).AsTask();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldAlignOffsets_AcrossSurrogatePairs()
    {
        // an emoji outside the BMP takes two UTF-16 code units; the wire contract's offsets are
        // UTF-16 code units too, so they should slice the .NET string with no conversion
        const string text = "hi \U0001F600 this is John Smith";
        var nameStart = text.IndexOf("John Smith", StringComparison.Ordinal);
        var client = new StubPiiDetectionClient([new RemotePiiEntity("PERSON", nameStart, nameStart + "John Smith".Length, 0.9)]);
        var recognizer = new RemotePiiRecognizer(client, new RemotePiiOptions { SupportedEntities = ["PERSON"] });

        var results = await recognizer.AnalyzeAsync(text, ["PERSON"]);

        results.Should().ContainSingle();
        text[results[0].Start..results[0].End].Should().Be("John Smith");
    }

    [Fact]
    public async Task Analyze_ShouldReturnEmpty_BecauseTheRecognizerIsAsyncOnly()
    {
        var client = new StubPiiDetectionClient([new RemotePiiEntity("PERSON", 0, 4, 0.9)]);
        var recognizer = new RemotePiiRecognizer(client, new RemotePiiOptions { SupportedEntities = ["PERSON"] });

        recognizer.Analyze("John said hi", ["PERSON"]).Should().BeEmpty();
        recognizer.RequiresAsync.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldMergeThroughAnalyzerEngine_WithOverlapResolutionAgainstLocalRecognizers()
    {
        // the remote recognizer detects a PERSON span that overlaps a locally-detected EMAIL_ADDRESS;
        // different entity types don't conflict, so both should survive the merge
        const string text = "email John Smith at john@example.com";
        var personStart = text.IndexOf("John Smith", StringComparison.Ordinal);
        var client = new StubPiiDetectionClient([new RemotePiiEntity("PERSON", personStart, personStart + "John Smith".Length, 0.9)]);
        var remoteRecognizer = new RemotePiiRecognizer(client, new RemotePiiOptions { SupportedEntities = ["PERSON"] });

        var engine = new AnalyzerEngine(new RecognizerRegistry([new EmailRecognizer(), remoteRecognizer]), defaultScoreThreshold: 0);

        var results = await engine.AnalyzeAsync(text);

        results.Should().Contain(r => r.EntityType == "PERSON" && r.Start == personStart);
        results.Should().Contain(r => r.EntityType == "EMAIL_ADDRESS");
    }

    private sealed class StubPiiDetectionClient : IPiiDetectionClient
    {
        private readonly IReadOnlyList<RemotePiiEntity> _entities;
        private readonly Exception? _throwing;
        private readonly TimeSpan? _delay;

        public IReadOnlyList<string>? LastRequestedEntities { get; private set; }

        public StubPiiDetectionClient(IReadOnlyList<RemotePiiEntity>? entities = null, Exception? throwing = null, TimeSpan? delay = null)
        {
            _entities = entities ?? [];
            _throwing = throwing;
            _delay = delay;
        }

        public async ValueTask<IReadOnlyList<RemotePiiEntity>> DetectAsync(
            string text, string language, IReadOnlyList<string> entities, CancellationToken ct = default)
        {
            LastRequestedEntities = entities;

            if (_delay is { } delay)
            {
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }

            if (_throwing is not null)
            {
                throw _throwing;
            }

            return _entities;
        }
    }
}
