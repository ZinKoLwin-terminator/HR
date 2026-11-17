using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Domain.Tests.Performance;

/// <summary>The life of an evaluation period and what may be edited in each status.</summary>
public sealed class EvaluationPeriodTests
{
    private static readonly DateOnly Start = new(2026, 10, 1);
    private static readonly DateOnly End = new(2026, 12, 31);

    private static EvaluationPeriodDetails Details(
        string name = "2026 Q4",
        DateOnly? start = null,
        DateOnly? end = null,
        DateOnly? due = null,
        KpiFrequency type = KpiFrequency.Quarterly,
        bool hr = false,
        int companyId = 1) => new(companyId, name, type, start ?? Start, end ?? End, due, hr);

    private static EvaluationPeriod Period(EvaluationPeriodStatus status = EvaluationPeriodStatus.Draft, EvaluationPeriodDetails? details = null)
    {
        var period = EvaluationPeriod.Create(details ?? Details(due: new DateOnly(2027, 1, 10))).Value;
        if (status >= EvaluationPeriodStatus.Active)
        {
            Assert.True(period.Activate().IsSuccess);
        }

        if (status >= EvaluationPeriodStatus.Completed)
        {
            Assert.True(period.Complete().IsSuccess);
        }

        if (status >= EvaluationPeriodStatus.Closed)
        {
            Assert.True(period.Close().IsSuccess);
        }

        return period;
    }

    private static void AssertFailsOn(Result result, string field)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains(field, result.Error.FieldErrors!.Keys);
    }

    private static void AssertConflict(Result result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public void A_new_period_starts_in_draft_with_trimmed_name()
    {
        var created = EvaluationPeriod.Create(Details(name: "  2026 Q4 "));

        Assert.True(created.IsSuccess);
        Assert.Equal("2026 Q4", created.Value.Name);
        Assert.Equal(EvaluationPeriodStatus.Draft, created.Value.Status);
        Assert.Equal(KpiFrequency.Quarterly, created.Value.PeriodType);
    }

    [Fact]
    public void The_end_date_must_be_after_the_start_date()
    {
        AssertFailsOn(EvaluationPeriod.Validate(Details(end: Start)), "endDate");
        AssertFailsOn(EvaluationPeriod.Validate(Details(end: Start.AddDays(-1))), "endDate");
        Assert.True(EvaluationPeriod.Validate(Details(end: Start.AddDays(1))).IsSuccess);
    }

    [Fact]
    public void The_submission_due_date_cannot_be_before_the_start_but_may_be_after_the_end()
    {
        AssertFailsOn(EvaluationPeriod.Validate(Details(due: Start.AddDays(-1))), "submissionDueDate");
        Assert.True(EvaluationPeriod.Validate(Details(due: Start)).IsSuccess);
        Assert.True(EvaluationPeriod.Validate(Details(due: End.AddDays(30))).IsSuccess);
        Assert.True(EvaluationPeriod.Validate(Details(due: null)).IsSuccess);
    }

    [Fact]
    public void A_period_needs_a_name() => AssertFailsOn(EvaluationPeriod.Validate(Details(name: " ")), "name");

    [Theory]
    [InlineData("2026-10-01", "2026-12-31", "2026-12-31", "2027-03-31", true)]
    [InlineData("2026-10-01", "2026-12-31", "2027-01-01", "2027-03-31", false)]
    [InlineData("2026-10-01", "2026-12-31", "2026-07-01", "2026-09-30", false)]
    [InlineData("2026-10-01", "2026-12-31", "2026-09-30", "2026-10-01", true)]
    [InlineData("2026-10-01", "2026-12-31", "2026-11-01", "2026-11-30", true)]
    [InlineData("2026-11-01", "2026-11-30", "2026-10-01", "2026-12-31", true)]
    public void Overlap_is_inclusive_of_both_end_dates(string startA, string endA, string startB, string endB, bool overlaps) =>
        Assert.Equal(
            overlaps,
            EvaluationPeriod.Overlaps(DateOnly.Parse(startA, System.Globalization.CultureInfo.InvariantCulture), DateOnly.Parse(endA, System.Globalization.CultureInfo.InvariantCulture), DateOnly.Parse(startB, System.Globalization.CultureInfo.InvariantCulture), DateOnly.Parse(endB, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void The_status_moves_draft_active_completed_closed()
    {
        var period = Period();

        Assert.True(period.Activate().IsSuccess);
        Assert.Equal(EvaluationPeriodStatus.Active, period.Status);
        Assert.True(period.Complete().IsSuccess);
        Assert.Equal(EvaluationPeriodStatus.Completed, period.Status);
        Assert.True(period.Close().IsSuccess);
        Assert.Equal(EvaluationPeriodStatus.Closed, period.Status);
    }

    [Theory]
    [InlineData(EvaluationPeriodStatus.Draft, "complete")]
    [InlineData(EvaluationPeriodStatus.Draft, "close")]
    [InlineData(EvaluationPeriodStatus.Active, "activate")]
    [InlineData(EvaluationPeriodStatus.Active, "close")]
    [InlineData(EvaluationPeriodStatus.Completed, "activate")]
    [InlineData(EvaluationPeriodStatus.Completed, "complete")]
    [InlineData(EvaluationPeriodStatus.Closed, "activate")]
    [InlineData(EvaluationPeriodStatus.Closed, "complete")]
    [InlineData(EvaluationPeriodStatus.Closed, "close")]
    public void A_move_out_of_order_is_a_conflict_and_changes_nothing(EvaluationPeriodStatus status, string move)
    {
        var period = Period(status);

        var result = move switch
        {
            "activate" => period.Activate(),
            "complete" => period.Complete(),
            _ => period.Close(),
        };

        AssertConflict(result);
        Assert.Equal(status, period.Status);
        Assert.Contains(period.Name, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_draft_period_can_change_every_field()
    {
        var period = Period();
        var changed = Details("2027 H1", new DateOnly(2027, 1, 1), new DateOnly(2027, 6, 30), new DateOnly(2027, 7, 5), KpiFrequency.HalfYearly, hr: true, companyId: 2);

        var result = period.Update(changed);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, period.CompanyId);
        Assert.Equal("2027 H1", period.Name);
        Assert.Equal(KpiFrequency.HalfYearly, period.PeriodType);
        Assert.Equal(new DateOnly(2027, 1, 1), period.StartDate);
        Assert.Equal(new DateOnly(2027, 6, 30), period.EndDate);
        Assert.Equal(new DateOnly(2027, 7, 5), period.SubmissionDueDate);
        Assert.True(period.RequiresHrReview);
        Assert.Equal(EvaluationPeriodStatus.Draft, period.Status);
    }

    [Fact]
    public void An_active_period_changes_only_its_name_and_submission_due_date()
    {
        var period = Period(EvaluationPeriodStatus.Active);

        var allowed = period.Update(Details("2026 Q4 (final)", due: new DateOnly(2027, 1, 20)));

        Assert.True(allowed.IsSuccess);
        Assert.Equal("2026 Q4 (final)", period.Name);
        Assert.Equal(new DateOnly(2027, 1, 20), period.SubmissionDueDate);
        Assert.Equal(EvaluationPeriodStatus.Active, period.Status);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("end")]
    [InlineData("type")]
    [InlineData("hr")]
    [InlineData("company")]
    public void An_active_period_refuses_a_change_of_any_other_field_with_a_conflict(string field)
    {
        var period = Period(EvaluationPeriodStatus.Active);
        var details = field switch
        {
            "start" => Details(start: Start.AddDays(1), due: new DateOnly(2027, 1, 10)),
            "end" => Details(end: End.AddDays(1), due: new DateOnly(2027, 1, 10)),
            "type" => Details(type: KpiFrequency.Custom, due: new DateOnly(2027, 1, 10)),
            "hr" => Details(hr: true, due: new DateOnly(2027, 1, 10)),
            _ => Details(companyId: 2, due: new DateOnly(2027, 1, 10)),
        };

        var result = period.Update(details);

        AssertConflict(result);
        Assert.Contains("only its name and submission due date", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(Start, period.StartDate);
        Assert.Equal(End, period.EndDate);
        Assert.Equal(KpiFrequency.Quarterly, period.PeriodType);
        Assert.False(period.RequiresHrReview);
        Assert.Equal(1, period.CompanyId);
    }

    [Theory]
    [InlineData(EvaluationPeriodStatus.Completed)]
    [InlineData(EvaluationPeriodStatus.Closed)]
    public void A_completed_or_closed_period_cannot_be_edited_at_all(EvaluationPeriodStatus status)
    {
        var period = Period(status);

        var result = period.Update(Details(due: new DateOnly(2027, 1, 10)));

        AssertConflict(result);
        Assert.Contains("can no longer be changed", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_invalid_update_is_a_validation_error_before_any_status_rule_and_changes_nothing()
    {
        var period = Period(EvaluationPeriodStatus.Active);

        var result = period.Update(Details(name: " "));

        AssertFailsOn(result, "name");
        Assert.Equal("2026 Q4", period.Name);
    }

    [Theory]
    [InlineData(EvaluationPeriodStatus.Draft, true)]
    [InlineData(EvaluationPeriodStatus.Active, false)]
    [InlineData(EvaluationPeriodStatus.Completed, false)]
    [InlineData(EvaluationPeriodStatus.Closed, false)]
    public void Only_a_draft_period_can_be_deleted(EvaluationPeriodStatus status, bool allowed)
    {
        var result = Period(status).EnsureCanDelete();

        Assert.Equal(allowed, result.IsSuccess);
        if (!allowed)
        {
            AssertConflict(result);
        }
    }
}
