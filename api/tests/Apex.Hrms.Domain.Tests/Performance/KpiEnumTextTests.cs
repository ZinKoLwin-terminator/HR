using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Domain.Tests.Performance;

/// <summary>The enum texts must equal the database CHECK constraint texts exactly, in both directions.</summary>
public sealed class KpiEnumTextTests
{
    private static void AssertTexts<T>(TextMap<T> map, params string[] expected)
        where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        Assert.Equal(expected.Length, values.Length);

        for (var i = 0; i < values.Length; i++)
        {
            Assert.Equal(expected[i], map.ToText(values[i]));
            Assert.True(map.TryParse(expected[i], out var parsed));
            Assert.Equal(values[i], parsed);
            Assert.Equal(values[i], map.Parse(expected[i]));
        }

        Assert.False(map.TryParse(expected[0].ToLowerInvariant() + "x", out _));
        Assert.Throws<ArgumentException>(() => map.Parse("nope"));
    }

    [Fact]
    public void Measurement_types() =>
        AssertTexts(KpiMeasurementTypeText.Map, "Number", "Percentage", "Currency", "Hours", "Rating", "Boolean");

    [Fact]
    public void Directions() =>
        AssertTexts(KpiDirectionText.Map, "Higher Is Better", "Lower Is Better", "Exact Target", "Range Based");

    [Fact]
    public void Scoring_methods() =>
        AssertTexts(KpiScoringMethodText.Map, "Direct Percentage", "Capped Percentage", "Rating Based", "Boolean", "Range Based");

    [Fact]
    public void Frequencies() =>
        AssertTexts(KpiFrequencyText.Map, "Monthly", "Quarterly", "Half-Yearly", "Yearly", "Custom");

    [Fact]
    public void Evaluation_period_statuses() =>
        AssertTexts(EvaluationPeriodStatusText.Map, "Draft", "Active", "Completed", "Closed");

    [Fact]
    public void Scorecard_statuses() =>
        AssertTexts(
            ScorecardStatusText.Map,
            "Planning",
            "Draft",
            "Submitted",
            "Under Review",
            "Returned",
            "Resubmitted",
            "Approved",
            "Rejected",
            "Finalized");

    [Fact]
    public void Review_steps() => AssertTexts(ReviewStepText.Map, "Employee", "Manager", "HR");

    [Fact]
    public void Review_action_types() =>
        AssertTexts(
            ReviewActionTypeText.Map,
            "Finalize Plan",
            "Submit",
            "Resubmit",
            "Start Review",
            "Approve",
            "Reject",
            "Return",
            "Finalize");

    [Fact]
    public void The_static_helpers_agree_with_the_maps()
    {
        Assert.Equal("Under Review", ScorecardStatusText.ToText(ScorecardStatus.UnderReview));
        Assert.Equal(ReviewActionType.StartReview, ReviewActionTypeText.Parse("Start Review"));
        Assert.True(KpiFrequencyText.TryParse("Half-Yearly", out var frequency));
        Assert.Equal(KpiFrequency.HalfYearly, frequency);
        Assert.Equal("Exact Target", KpiDirectionText.ToText(KpiDirection.ExactTarget));
        Assert.Equal(KpiScoringMethod.RangeBased, KpiScoringMethodText.Parse("Range Based"));
        Assert.Equal("Rating", KpiMeasurementTypeText.ToText(KpiMeasurementType.Rating));
        Assert.Equal(EvaluationPeriodStatus.Closed, EvaluationPeriodStatusText.Parse("Closed"));
        Assert.Equal("HR", ReviewStepText.ToText(ReviewStep.HR));
    }
}
