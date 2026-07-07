using TasmanianDevil.Analyzer;
using FluentAssertions;
using Xunit;

namespace TasmanianDevil.Tests;

public class AnalyzerEngineAsyncTests
{
    [Fact]
    public async Task AnalyzeAsync_ShouldEqualAnalyze_AndCompleteSynchronously_WhenAllRecognizersAreSync()
    {
        var engine = new AnalyzerEngine(new RecognizerRegistry([new LiteralStubRecognizer("LOCAL_ENTITY", "acme")]), defaultScoreThreshold: 0);
        const string text = "contact acme today";

        var syncResults = engine.Analyze(text);
        var asyncTask = engine.AnalyzeAsync(text);

        // the default AnalyzeAsync wrapper resolves the sync Analyze() call synchronously, so a
        // registry with no async recognizer never actually suspends
        asyncTask.IsCompleted.Should().BeTrue();

        var asyncResults = await asyncTask;
        asyncResults.Should().BeEquivalentTo(syncResults);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldBoostAsyncRecognizerScore_ThroughContextEnhancement()
    {
        var recognizer = new AsyncOnlyStubRecognizer("REMOTE_ENTITY", "acme corp", score: 0.6, context: ["company"]);
        var engine = new AnalyzerEngine(new RecognizerRegistry([recognizer]), defaultScoreThreshold: 0);

        var withoutContext = await engine.AnalyzeAsync("the name is acme corp");
        var withContext = await engine.AnalyzeAsync("the company is acme corp");

        withoutContext.Should().ContainSingle();
        withContext.Should().ContainSingle();
        withContext[0].Score.Should().BeGreaterThan(withoutContext[0].Score);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldDedupeOverlappingSpans_BetweenSyncAndAsyncRecognizers()
    {
        // both recognizers detect the same entity type over overlapping spans; the async recognizer's
        // superset span scores higher, so it should win the overlap-resolution and the sync
        // recognizer's contained, lower-scoring span should be dropped
        var syncRecognizer = new LiteralStubRecognizer("COMPANY", "acme", score: 0.5);
        var asyncRecognizer = new AsyncOnlyStubRecognizer("COMPANY", "acme corp", score: 0.9);
        var engine = new AnalyzerEngine(new RecognizerRegistry([syncRecognizer, asyncRecognizer]), defaultScoreThreshold: 0);

        var results = await engine.AnalyzeAsync("contact us at acme corp today");

        results.Should().ContainSingle();
        results[0].EntityType.Should().Be("COMPANY");
        results[0].Score.Should().Be(0.9);
    }

    [Fact]
    public void Analyze_ShouldIgnoreAsyncRecognizer_WhenCalledSynchronously()
    {
        var syncRecognizer = new LiteralStubRecognizer("LOCAL_ENTITY", "acme");
        var asyncRecognizer = new AsyncOnlyStubRecognizer("REMOTE_ENTITY", "acme corp");
        var engine = new AnalyzerEngine(new RecognizerRegistry([syncRecognizer, asyncRecognizer]), defaultScoreThreshold: 0);

        var results = engine.Analyze("contact us at acme corp today");

        results.Should().ContainSingle();
        results[0].EntityType.Should().Be("LOCAL_ENTITY");
    }

    /// <summary>A trivial synchronous recognizer that matches a fixed literal substring.</summary>
    private sealed class LiteralStubRecognizer(string entityType, string needle, double score = EntityRecognizer.MaxScore)
        : EntityRecognizer([entityType])
    {
        public override IReadOnlyList<RecognizerResult> Analyze(string text, IReadOnlyList<string> entities)
        {
            var index = text.IndexOf(needle, StringComparison.Ordinal);
            return index < 0 ? [] : [new RecognizerResult(entityType, index, index + needle.Length, score)];
        }
    }

    /// <summary>
    /// An async-only recognizer matching a fixed literal substring, simulating a remote detector: its
    /// synchronous <see cref="Analyze"/> returns no results, and <see cref="AnalyzeAsync"/> genuinely
    /// suspends via <see cref="Task.Yield"/>.
    /// </summary>
    private sealed class AsyncOnlyStubRecognizer(string entityType, string needle, double score = EntityRecognizer.MaxScore, IReadOnlyList<string>? context = null)
        : EntityRecognizer([entityType], context: context)
    {
        public override bool RequiresAsync => true;

        public override IReadOnlyList<RecognizerResult> Analyze(string text, IReadOnlyList<string> entities) => [];

        public override async ValueTask<IReadOnlyList<RecognizerResult>> AnalyzeAsync(
            string text, IReadOnlyList<string> entities, CancellationToken ct = default)
        {
            await Task.Yield();

            var index = text.IndexOf(needle, StringComparison.Ordinal);
            return index < 0 ? [] : [new RecognizerResult(entityType, index, index + needle.Length, score)];
        }
    }
}
