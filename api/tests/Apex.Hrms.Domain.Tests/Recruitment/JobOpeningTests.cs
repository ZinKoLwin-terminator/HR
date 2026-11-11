using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Recruitment;

namespace Apex.Hrms.Domain.Tests.Recruitment;

public sealed class JobOpeningTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);

    private static JobOpeningDetails Details(
        int vacancies = 2,
        DateOnly? posted = null,
        DateOnly? closing = null,
        string title = "  Backend Engineer ") => new(
        title,
        DepartmentId: 1,
        HiringManagerId: 2,
        RecruitmentEmploymentType.FullTime,
        vacancies,
        posted,
        closing ?? Today.AddDays(30),
        "  ");

    private static JobOpening Draft(int vacancies = 2, DateOnly? closing = null) =>
        JobOpening.Create(Details(vacancies, closing: closing), "JOB-2026-001", Today).Value;

    private static JobOpening WithStatus(JobOpeningStatus status, int vacancies = 2)
    {
        var opening = Draft(vacancies);
        switch (status)
        {
            case JobOpeningStatus.Open:
                Assert.True(opening.ChangeStatus(JobOpeningStatus.Open, Today, 0).IsSuccess);
                break;
            case JobOpeningStatus.OnHold:
                opening.ChangeStatus(JobOpeningStatus.Open, Today, 0);
                Assert.True(opening.ChangeStatus(JobOpeningStatus.OnHold, Today, 0).IsSuccess);
                break;
            case JobOpeningStatus.Closed:
                Assert.True(opening.ChangeStatus(JobOpeningStatus.Closed, Today, 0).IsSuccess);
                break;
            case JobOpeningStatus.Filled:
                opening.ChangeStatus(JobOpeningStatus.Open, Today, 0);
                Assert.Equal(JobOpeningStatus.Filled, opening.SyncFilled(vacancies));
                break;
        }

        return opening;
    }

    [Fact]
    public void A_new_opening_is_a_trimmed_draft_posted_today_by_default()
    {
        var opening = Draft();

        Assert.Equal(JobOpeningStatus.Draft, opening.Status);
        Assert.Equal("Backend Engineer", opening.Title);
        Assert.Null(opening.Description);
        Assert.Equal(Today, opening.PostedOn);
        Assert.Equal("JOB-2026-001 Backend Engineer", opening.Label);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    [InlineData(-3)]
    public void Vacancies_must_be_between_1_and_50(int vacancies)
    {
        var result = JobOpening.Create(Details(vacancies), "JOB-2026-001", Today);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Vacancies must be between 1 and 50.", result.Error.Message);
        Assert.Equal("Enter 1 to 50.", result.Error.FieldErrors!["vacancies"][0]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public void The_vacancy_bounds_are_accepted(int vacancies) =>
        Assert.True(JobOpening.Create(Details(vacancies), "JOB-2026-001", Today).IsSuccess);

    [Fact]
    public void The_closing_date_cannot_be_before_the_posting_date()
    {
        var result = JobOpening.Create(Details(posted: Today, closing: Today.AddDays(-1)), "JOB-2026-001", Today);

        Assert.Equal("The closing date cannot be before the posting date.", result.Error.Message);
        Assert.Equal("Must be on or after the posting date.", result.Error.FieldErrors!["closingDate"][0]);
        Assert.True(JobOpening.Create(Details(posted: Today, closing: Today), "JOB-2026-001", Today).IsSuccess);
    }

    [Fact]
    public void A_title_is_required()
    {
        var result = JobOpening.Create(Details(title: "   "), "JOB-2026-001", Today);

        Assert.Equal("Title is required.", result.Error.FieldErrors!["title"][0]);
    }

    [Fact]
    public void Publishing_needs_a_closing_date_of_today_or_later()
    {
        var past = JobOpening.Create(Details(posted: Today.AddDays(-10), closing: Today.AddDays(-1)), "JOB-2026-002", Today).Value;
        var refused = past.ChangeStatus(JobOpeningStatus.Open, Today, 0);
        var onTheDay = JobOpening.Create(Details(closing: Today), "JOB-2026-003", Today).Value.ChangeStatus(JobOpeningStatus.Open, Today, 0);

        Assert.Equal(ErrorType.Conflict, refused.Error.Type);
        Assert.Equal("The closing date has passed. Edit the opening before reopening it.", refused.Error.Message);
        Assert.Equal(JobOpeningStatus.Draft, past.Status);
        Assert.True(onTheDay.IsSuccess);
    }

    [Fact]
    public void A_closed_opening_reopens_only_before_its_closing_date_passes()
    {
        var opening = WithStatus(JobOpeningStatus.Closed);

        Assert.True(opening.ChangeStatus(JobOpeningStatus.Open, Today.AddDays(30), 0).IsSuccess);

        var late = WithStatus(JobOpeningStatus.Closed);
        var refused = late.ChangeStatus(JobOpeningStatus.Open, Today.AddDays(31), 0);

        Assert.Equal(ErrorType.Conflict, refused.Error.Type);
        Assert.Equal(JobOpeningStatus.Closed, late.Status);
    }

    [Fact]
    public void An_opening_whose_vacancies_are_taken_cannot_be_opened()
    {
        var opening = WithStatus(JobOpeningStatus.Closed, vacancies: 2);

        var refused = opening.ChangeStatus(JobOpeningStatus.Open, Today, 2);

        Assert.Equal(ErrorType.Conflict, refused.Error.Type);
        Assert.Equal("All vacancies already have a selected candidate.", refused.Error.Message);
        Assert.True(opening.ChangeStatus(JobOpeningStatus.Open, Today, 1).IsSuccess);
    }

    [Theory]
    [InlineData(JobOpeningStatus.Draft, JobOpeningStatus.Open, true)]
    [InlineData(JobOpeningStatus.Draft, JobOpeningStatus.Closed, true)]
    [InlineData(JobOpeningStatus.Draft, JobOpeningStatus.OnHold, false)]
    [InlineData(JobOpeningStatus.Draft, JobOpeningStatus.Filled, false)]
    [InlineData(JobOpeningStatus.Open, JobOpeningStatus.OnHold, true)]
    [InlineData(JobOpeningStatus.Open, JobOpeningStatus.Closed, true)]
    [InlineData(JobOpeningStatus.Open, JobOpeningStatus.Draft, false)]
    [InlineData(JobOpeningStatus.Open, JobOpeningStatus.Filled, false)]
    [InlineData(JobOpeningStatus.OnHold, JobOpeningStatus.Open, true)]
    [InlineData(JobOpeningStatus.OnHold, JobOpeningStatus.Closed, true)]
    [InlineData(JobOpeningStatus.Closed, JobOpeningStatus.Open, true)]
    [InlineData(JobOpeningStatus.Closed, JobOpeningStatus.OnHold, false)]
    [InlineData(JobOpeningStatus.Filled, JobOpeningStatus.Open, false)]
    [InlineData(JobOpeningStatus.Filled, JobOpeningStatus.Closed, false)]
    public void The_status_transition_table_is_followed(JobOpeningStatus from, JobOpeningStatus to, bool allowed)
    {
        var opening = WithStatus(from);

        var result = opening.ChangeStatus(to, Today, selectedCount: 0);

        Assert.Equal(allowed, result.IsSuccess);
        if (!allowed)
        {
            Assert.Equal(ErrorType.Conflict, result.Error.Type);
            Assert.Equal(
                $"A {JobOpeningStatusText.ToText(from).ToLowerInvariant()} job opening cannot be set to {JobOpeningStatusText.ToText(to)}.",
                result.Error.Message);
            Assert.Equal(from, opening.Status);
        }
    }

    [Fact]
    public void An_opening_is_filled_when_the_selected_count_reaches_the_vacancies_and_opens_again_when_one_drops_out()
    {
        var opening = WithStatus(JobOpeningStatus.Open, vacancies: 2);

        Assert.Null(opening.SyncFilled(1));
        Assert.Equal(JobOpeningStatus.Filled, opening.SyncFilled(2));
        Assert.Equal(JobOpeningStatus.Filled, opening.Status);
        Assert.Null(opening.SyncFilled(2));
        Assert.Equal(JobOpeningStatus.Open, opening.SyncFilled(1));
    }

    [Fact]
    public void An_opening_on_hold_is_filled_too_but_a_draft_or_closed_one_is_not()
    {
        Assert.Equal(JobOpeningStatus.Filled, WithStatus(JobOpeningStatus.OnHold, 1).SyncFilled(1));
        Assert.Null(WithStatus(JobOpeningStatus.Closed, 1).SyncFilled(1));
        Assert.Null(Draft(1).SyncFilled(1));
    }

    [Theory]
    [InlineData(JobOpeningStatus.Closed)]
    [InlineData(JobOpeningStatus.Filled)]
    public void A_closed_or_filled_opening_cannot_be_edited(JobOpeningStatus status)
    {
        var opening = WithStatus(status);

        var result = opening.Update(Details(), Today, 0);

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal($"A {status.ToString().ToLowerInvariant()} job opening cannot be edited.", result.Error.Message);
    }

    [Fact]
    public void Vacancies_cannot_drop_below_the_selected_count()
    {
        var opening = WithStatus(JobOpeningStatus.Open, vacancies: 3);

        var result = opening.Update(Details(vacancies: 1), Today, selectedCount: 2);

        Assert.Equal("2 candidate(s) are already selected for this job.", result.Error.Message);
        Assert.Equal("At least 2 (already selected).", result.Error.FieldErrors!["vacancies"][0]);
        Assert.Equal(3, opening.Vacancies);
        Assert.True(opening.Update(Details(vacancies: 2), Today, 2).IsSuccess);
    }

    [Fact]
    public void An_update_keeps_the_posting_date_when_none_is_sent_and_never_changes_code_or_status()
    {
        var opening = WithStatus(JobOpeningStatus.Open);

        var result = opening.Update(Details(title: "Senior Engineer"), Today.AddDays(5), 0);

        Assert.True(result.IsSuccess);
        Assert.Equal(Today, opening.PostedOn);
        Assert.Equal("JOB-2026-001", opening.Code);
        Assert.Equal(JobOpeningStatus.Open, opening.Status);
        Assert.Equal("Senior Engineer", opening.Title);
    }
}
