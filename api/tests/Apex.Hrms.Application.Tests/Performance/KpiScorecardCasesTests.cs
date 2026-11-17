using System.Text.Json;
using Apex.Hrms.Application.Performance.Insights;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Tests.Performance;

/// <summary>
/// Runs every shared case of docs/test-data/kpi-scorecard-cases.json against the C# calculation: <see cref="KpiCalculator"/> (whole
/// scorecards, the weight rule, the rating label) and <see cref="KpiFigures"/> (average, completion rate, competition ranks, attention
/// threshold). The file documents its own shape in <c>_comment</c>; the Angular demo mock is meant to run the same file.
/// </summary>
public sealed class KpiScorecardCasesTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly string[] Kinds = ["score", "validateWeights", "ratingLabel", "average", "completionRate", "competitionRanks", "attentionThreshold"];

    private static List<SharedCase> LoadCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "kpi-scorecard-cases.json");
        return JsonSerializer.Deserialize<CaseFile>(File.ReadAllText(path), JsonOptions)!.Cases;
    }

    public static TheoryData<string> CaseNames()
    {
        var names = new TheoryData<string>();
        foreach (var shared in LoadCases())
        {
            names.Add(shared.Name);
        }

        return names;
    }

    [Fact]
    public void The_file_has_unique_names_known_kinds_and_cases_of_every_kind()
    {
        var cases = LoadCases();

        Assert.Equal(cases.Count, cases.Select(shared => shared.Name).Distinct().Count());
        Assert.All(cases, shared => Assert.Contains(shared.Kind, Kinds));
        Assert.All(Kinds, kind => Assert.Contains(cases, shared => shared.Kind == kind));
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void The_shared_case_gives_the_expected_result(string name)
    {
        var shared = LoadCases().Single(candidate => candidate.Name == name);
        switch (shared.Kind)
        {
            case "score":
                AssertScore(shared);
                break;
            case "validateWeights":
                AssertOutcome(KpiCalculator.ValidateWeights(shared.Weights!), shared.Valid!.Value ? null : shared.ExpectedError);
                break;
            case "ratingLabel":
                Assert.Equal(shared.ExpectedLabel, KpiCalculator.RatingLabel(shared.OverallScore!.Value, shared.Bands!.Select(band => (band.MinScore, band.Label))));
                break;
            case "average":
                Assert.Equal(Number(shared.Expected), KpiFigures.Average(shared.Sum, shared.Count!.Value));
                break;
            case "completionRate":
                Assert.Equal(Number(shared.Expected), KpiFigures.CompletionRate(shared.Finalized!.Value, shared.Evaluated!.Value));
                break;
            case "competitionRanks":
                Assert.Equal(
                    shared.Expected!.Value.Deserialize<int[]>(),
                    KpiFigures.CompetitionRanks(shared.ScoresDescending!, shared.FirstRank!.Value, shared.FirstPosition!.Value));
                break;
            case "attentionThreshold":
                Assert.Equal(Number(shared.Expected), KpiFigures.AttentionThreshold(shared.ActiveBandMinScores!));
                break;
            default:
                Assert.Fail($"Unknown kind \"{shared.Kind}\" in case \"{name}\".");
                break;
        }
    }

    private static void AssertScore(SharedCase shared)
    {
        var inputs = shared.Items!.Select(item => new KpiScoreInput(item.Rule.ToRule(), item.Weight, item.Actual)).ToList();

        var score = KpiCalculator.Score(inputs);

        if (shared.ExpectedError is { } error)
        {
            AssertOutcome(score.IsFailure ? Result.Failure(score.Error) : Result.Success(), error);
            return;
        }

        Assert.True(score.IsSuccess, score.IsFailure ? score.Error.Message : null);
        var expected = shared.ExpectedScore!;
        Assert.Equal(expected.OverallScore, score.Value.OverallScore);
        Assert.Equal(
            expected.Items.Select(item => (item.AchievementPct, item.WeightedScore)),
            score.Value.Items.Select(item => (item.AchievementPct, item.WeightedScore)));
    }

    private static void AssertOutcome(Result result, ExpectedError? error)
    {
        if (error is null)
        {
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
            return;
        }

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(error.Message, result.Error.Message);
        Assert.Equal(
            error.FieldErrors.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (pair.Key, string.Join(" | ", pair.Value))),
            result.Error.FieldErrors!.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (pair.Key, string.Join(" | ", pair.Value))));
    }

    /// <summary>A JSON number as a decimal, or null for a JSON null.</summary>
    private static decimal? Number(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Number } number ? number.GetDecimal() : null;

    public sealed record CaseFile(List<SharedCase> Cases);

    /// <summary>One case: <see cref="Kind"/> says which of the other fields it uses (see the <c>_comment</c> of the file).</summary>
    public sealed record SharedCase(
        string Kind,
        string Name,
        List<ScoreItem>? Items,
        JsonElement? Expected,
        ExpectedError? ExpectedError,
        decimal[]? Weights,
        bool? Valid,
        decimal? OverallScore,
        List<Band>? Bands,
        string? ExpectedLabel,
        decimal? Sum,
        int? Count,
        int? Finalized,
        int? Evaluated,
        decimal[]? ScoresDescending,
        int? FirstRank,
        int? FirstPosition,
        decimal[]? ActiveBandMinScores)
    {
        /// <summary>The <c>expected</c> object of a <c>score</c> case.</summary>
        public ExpectedScore? ExpectedScore => Expected is { ValueKind: JsonValueKind.Object } element ? element.Deserialize<ExpectedScore>(JsonOptions) : null;
    }

    public sealed record ScoreItem(SharedRule Rule, decimal Weight, decimal? Actual);

    public sealed record SharedRule(
        string MeasurementType,
        string Direction,
        string ScoringMethod,
        decimal Target,
        decimal? RangeMin,
        decimal? RangeMax,
        decimal? MaxRating,
        decimal? MaxAchievementPct)
    {
        public KpiRule ToRule() => new(
            KpiMeasurementTypeText.Parse(MeasurementType),
            KpiDirectionText.Parse(Direction),
            KpiScoringMethodText.Parse(ScoringMethod),
            Target,
            RangeMin,
            RangeMax,
            MaxRating,
            MaxAchievementPct);
    }

    public sealed record ExpectedScore(List<ExpectedItem> Items, decimal OverallScore);

    public sealed record ExpectedItem(decimal AchievementPct, decimal WeightedScore);

    public sealed record ExpectedError(string Message, Dictionary<string, string[]> FieldErrors);

    public sealed record Band(decimal MinScore, string Label);
}
