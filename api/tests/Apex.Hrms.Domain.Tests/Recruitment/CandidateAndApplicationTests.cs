using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Recruitment;

namespace Apex.Hrms.Domain.Tests.Recruitment;

public sealed class CandidateAndApplicationTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);

    private static CandidateDetails Details(string email = "  Ada.Test@Example.TEST ", decimal experience = 4.5m) => new(
        " Ada ",
        "Test",
        email,
        "+95 9 555 0100",
        CandidateSource.Referral,
        "  ",
        experience,
        null,
        null);

    // ---- Candidate ----

    [Fact]
    public void A_candidate_is_trimmed_and_its_email_stored_lower_case()
    {
        var candidate = Candidate.Create(Details()).Value;

        Assert.Equal("Ada", candidate.FirstName);
        Assert.Equal("ada.test@example.test", candidate.Email);
        Assert.Null(candidate.CurrentEmployer);
        Assert.Equal("Ada Test", candidate.FullName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(12.5)]
    public void Experience_within_0_to_50_years_is_accepted(double years) =>
        Assert.True(Candidate.Create(Details(experience: (decimal)years)).IsSuccess);

    [Theory]
    [InlineData(-0.1)]
    [InlineData(50.1)]
    [InlineData(80)]
    public void Experience_outside_0_to_50_years_is_refused(double years)
    {
        var result = Candidate.Create(Details(experience: (decimal)years));

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Experience must be between 0 and 50 years.", result.Error.Message);
        Assert.Equal("Enter 0 to 50.", result.Error.FieldErrors!["experienceYears"][0]);
    }

    [Fact]
    public void Names_email_and_phone_are_required()
    {
        var result = Candidate.Create(Details() with { FirstName = " ", LastName = "", Email = " ", Phone = "" });

        Assert.Equal(["email", "firstName", "lastName", "phone"], result.Error.FieldErrors!.Keys.Order(StringComparer.Ordinal));
    }

    // ---- Application: the stage transition table ----

    [Theory]
    [InlineData(ApplicationStage.Applied, ApplicationStage.Screening, true)]
    [InlineData(ApplicationStage.Applied, ApplicationStage.Rejected, true)]
    [InlineData(ApplicationStage.Applied, ApplicationStage.Interview, false)]
    [InlineData(ApplicationStage.Applied, ApplicationStage.Selected, false)]
    [InlineData(ApplicationStage.Applied, ApplicationStage.Applied, false)]
    [InlineData(ApplicationStage.Screening, ApplicationStage.Interview, true)]
    [InlineData(ApplicationStage.Screening, ApplicationStage.Rejected, true)]
    [InlineData(ApplicationStage.Screening, ApplicationStage.Applied, false)]
    [InlineData(ApplicationStage.Screening, ApplicationStage.Selected, false)]
    [InlineData(ApplicationStage.Interview, ApplicationStage.Selected, true)]
    [InlineData(ApplicationStage.Interview, ApplicationStage.Rejected, true)]
    [InlineData(ApplicationStage.Interview, ApplicationStage.Screening, false)]
    [InlineData(ApplicationStage.Interview, ApplicationStage.Applied, false)]
    [InlineData(ApplicationStage.Selected, ApplicationStage.Rejected, true)]
    [InlineData(ApplicationStage.Selected, ApplicationStage.Interview, false)]
    [InlineData(ApplicationStage.Selected, ApplicationStage.Applied, false)]
    [InlineData(ApplicationStage.Rejected, ApplicationStage.Applied, false)]
    [InlineData(ApplicationStage.Rejected, ApplicationStage.Screening, false)]
    [InlineData(ApplicationStage.Rejected, ApplicationStage.Rejected, false)]
    public void The_stage_transition_table_is_followed_exactly(ApplicationStage from, ApplicationStage to, bool allowed)
    {
        Assert.Equal(allowed, JobApplication.CanMove(from, to));
        var application = At(from);

        var result = application.MoveTo(to, "Not a fit");

        Assert.Equal(allowed, result.IsSuccess);
        if (!allowed)
        {
            Assert.Equal(ErrorType.Conflict, result.Error.Type);
            Assert.Equal(
                $"An application at \"{from}\" cannot move to \"{to}\".",
                result.Error.Message);
            Assert.Equal(from, application.Stage);
        }
    }

    private static JobApplication At(ApplicationStage stage)
    {
        var application = JobApplication.Create(1, 1, null, Today);
        ApplicationStage[] path = stage switch
        {
            ApplicationStage.Applied => [],
            ApplicationStage.Screening => [ApplicationStage.Screening],
            ApplicationStage.Interview => [ApplicationStage.Screening, ApplicationStage.Interview],
            ApplicationStage.Selected => [ApplicationStage.Screening, ApplicationStage.Interview, ApplicationStage.Selected],
            _ => [ApplicationStage.Rejected],
        };
        foreach (var step in path)
        {
            Assert.True(application.MoveTo(step, "Not a fit").IsSuccess);
        }

        return application;
    }

    [Fact]
    public void A_new_application_is_applied_today_with_no_reason()
    {
        var application = JobApplication.Create(3, 4, "  Referred by a colleague ", Today);

        Assert.Equal(ApplicationStage.Applied, application.Stage);
        Assert.Equal(Today, application.AppliedOn);
        Assert.Null(application.RejectionReason);
        Assert.Equal("Referred by a colleague", application.Notes);
        Assert.Equal((3, 4), (application.JobOpeningId, application.CandidateId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejecting_needs_a_non_blank_reason(string? reason)
    {
        var application = At(ApplicationStage.Screening);

        var result = application.MoveTo(ApplicationStage.Rejected, reason);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Enter a reason for rejecting this application.", result.Error.Message);
        Assert.Equal("A reason is required.", result.Error.FieldErrors!["reason"][0]);
        Assert.Equal(ApplicationStage.Screening, application.Stage);
    }

    [Fact]
    public void A_rejection_keeps_the_trimmed_reason_and_every_other_stage_clears_it()
    {
        var rejected = At(ApplicationStage.Interview);
        Assert.True(rejected.MoveTo(ApplicationStage.Rejected, "  Salary too high ").IsSuccess);
        Assert.Equal("Salary too high", rejected.RejectionReason);

        var forward = At(ApplicationStage.Applied);
        Assert.True(forward.MoveTo(ApplicationStage.Screening, "ignored").IsSuccess);
        Assert.Null(forward.RejectionReason);
    }

    [Fact]
    public void A_selected_application_cannot_be_deleted_but_others_can()
    {
        var selected = At(ApplicationStage.Selected);

        var refused = selected.EnsureCanDelete();

        Assert.Equal(ErrorType.Conflict, refused.Error.Type);
        Assert.Equal("A selected application cannot be deleted. Reject it first.", refused.Error.Message);
        Assert.True(At(ApplicationStage.Interview).EnsureCanDelete().IsSuccess);
        Assert.True(At(ApplicationStage.Rejected).EnsureCanDelete().IsSuccess);
    }

    [Fact]
    public void Scheduling_moves_a_screened_application_to_interview_and_nothing_else()
    {
        var screened = At(ApplicationStage.Screening);
        var applied = At(ApplicationStage.Applied);

        Assert.True(screened.AdvanceToInterview());
        Assert.Equal(ApplicationStage.Interview, screened.Stage);
        Assert.False(applied.AdvanceToInterview());
        Assert.False(screened.AdvanceToInterview());
    }
}
