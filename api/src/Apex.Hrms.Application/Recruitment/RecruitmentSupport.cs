using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Recruitment;

namespace Apex.Hrms.Application.Recruitment;

/// <summary>Lookups the recruitment rules share: current employees and the number of selected candidates of an opening.</summary>
internal sealed class RecruitmentLookup(
    IRepository<Employee> employees,
    IRecordCounter<JobApplication> applications,
    IRepository<JobApplication> applicationRows,
    IRepository<Candidate> candidates,
    IRepository<JobOpening> openings)
{
    /// <summary>
    /// "Candidate for Job title", how the mock names an application in the activity feed (the candidate's name is the
    /// only personal data in it; no email, phone or salary).
    /// </summary>
    public async Task<string> ApplicationLabelAsync(JobApplication application, CancellationToken cancellationToken)
    {
        var candidate = await candidates.FindAsync(application.CandidateId, cancellationToken);
        var opening = await openings.FindAsync(application.JobOpeningId, cancellationToken);
        return $"{candidate?.FullName ?? $"candidate #{application.CandidateId}"} for {opening?.Title ?? $"job #{application.JobOpeningId}"}";
    }

    /// <summary>"Round · Candidate for Job title", how the mock names an interview in the activity feed.</summary>
    public async Task<string> InterviewLabelAsync(Interview interview, CancellationToken cancellationToken)
    {
        var application = await applicationRows.FindAsync(interview.ApplicationId, cancellationToken);
        var label = application is null
            ? $"application #{interview.ApplicationId}"
            : await ApplicationLabelAsync(application, cancellationToken);
        return $"{InterviewRoundText.ToText(interview.Round)} · {label}";
    }

    /// <summary>
    /// Succeeds when the employee exists and has not left (no exit date, not Resigned or Terminated). Otherwise a
    /// Validation error on <paramref name="field"/>.
    /// </summary>
    public async Task<Result> EnsureCurrentEmployeeAsync(
        int employeeId,
        string message,
        string field,
        CancellationToken cancellationToken)
    {
        var employee = await employees.FindAsync(employeeId, cancellationToken);
        return employee is null || employee.ExitDate is not null || employee.HasLeft
            ? Result.Failure(AppError.Validation(message, field, "Choose a current employee."))
            : Result.Success();
    }

    /// <summary>How many applications of the opening are Selected, not counting <paramref name="exceptApplicationId"/>.</summary>
    public Task<int> SelectedCountAsync(int jobOpeningId, CancellationToken cancellationToken, int exceptApplicationId = 0) =>
        applications.CountAsync(
            application => application.JobOpeningId == jobOpeningId
                && application.Stage == ApplicationStage.Selected
                && application.Id != exceptApplicationId,
            cancellationToken);
}
