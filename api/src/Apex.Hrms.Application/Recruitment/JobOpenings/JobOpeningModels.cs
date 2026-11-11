using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Recruitment;

namespace Apex.Hrms.Application.Recruitment.JobOpenings;

/// <summary>
/// Mirrors the JobOpening interface in recruitment.models.ts. <c>HiringManagerId</c> is an employee id and
/// <c>EmploymentType</c> is "Full-time", "Part-time", "Contract" or "Internship"; <c>Status</c> is "Draft", "Open",
/// "On Hold", "Closed" or "Filled".
/// </summary>
public sealed record JobOpeningDto(
    int Id,
    string Code,
    string Title,
    int DepartmentId,
    int HiringManagerId,
    string EmploymentType,
    int Vacancies,
    DateOnly PostedOn,
    DateOnly ClosingDate,
    string? Description,
    string Status,
    byte[] RowVersion) : IIdentified;

internal interface IJobOpeningFields
{
    string Title { get; }

    int DepartmentId { get; }

    int HiringManagerId { get; }

    string EmploymentType { get; }

    int Vacancies { get; }

    DateOnly? PostedOn { get; }

    DateOnly ClosingDate { get; }

    string? Description { get; }
}

internal static class JobOpeningDetailsFactory
{
    /// <summary>Call only after the request validator passed (the employment type text is known to be valid).</summary>
    public static JobOpeningDetails From(IJobOpeningFields fields) => new JobOpeningDetails(
        fields.Title,
        fields.DepartmentId,
        fields.HiringManagerId,
        RecruitmentEmploymentTypeText.Parse(fields.EmploymentType.Trim()),
        fields.Vacancies,
        fields.PostedOn,
        fields.ClosingDate,
        fields.Description).Normalized();
}

/// <summary>
/// POST body. The code and the status are server-owned: the code is made from a sequence and the opening starts as a
/// Draft. <c>postedOn</c> is optional (today by default).
/// </summary>
public sealed record JobOpeningCreateRequest(
    string Title,
    int DepartmentId,
    int HiringManagerId,
    string EmploymentType,
    int Vacancies,
    DateOnly? PostedOn,
    DateOnly ClosingDate,
    string? Description) : IJobOpeningFields
{
    public JobOpeningDetails ToDetails() => JobOpeningDetailsFactory.From(this);
}

/// <summary>PUT body: changes an opening that is not closed or filled. Requires the row version. The status never changes here.</summary>
public sealed record JobOpeningUpdateRequest(
    string Title,
    int DepartmentId,
    int HiringManagerId,
    string EmploymentType,
    int Vacancies,
    DateOnly? PostedOn,
    DateOnly ClosingDate,
    string? Description,
    string? RowVersion) : IJobOpeningFields, IVersionedRequest
{
    public JobOpeningDetails ToDetails() => JobOpeningDetailsFactory.From(this);
}

/// <summary>Filters of the list; <c>status</c> is the text of a job opening status, "all" or blank for no filter.</summary>
public sealed record JobOpeningFilter(
    string? Status = null,
    int? DepartmentId = null,
    int? HiringManagerId = null,
    string? EmploymentType = null);

// ---- Commands (status never changes through PUT) ----

/// <summary>Draft or On Hold to Open (also Closed to Open). Needs a closing date of today or later and a free vacancy.</summary>
public sealed record OpenJobOpeningCommand(int Id, string? RowVersion) : IEntityCommand<OpenJobOpeningCommand>, IJobOpeningStatusCommand
{
    public static OpenJobOpeningCommand From(int id, string? rowVersion) => new(id, rowVersion);

    public static JobOpeningStatus Target => JobOpeningStatus.Open;
}

/// <summary>Closed to Open again; the same transition and rules as <see cref="OpenJobOpeningCommand"/>.</summary>
public sealed record ReopenJobOpeningCommand(int Id, string? RowVersion) : IEntityCommand<ReopenJobOpeningCommand>, IJobOpeningStatusCommand
{
    public static ReopenJobOpeningCommand From(int id, string? rowVersion) => new(id, rowVersion);

    public static JobOpeningStatus Target => JobOpeningStatus.Open;
}

/// <summary>Open to On Hold.</summary>
public sealed record HoldJobOpeningCommand(int Id, string? RowVersion) : IEntityCommand<HoldJobOpeningCommand>, IJobOpeningStatusCommand
{
    public static HoldJobOpeningCommand From(int id, string? rowVersion) => new(id, rowVersion);

    public static JobOpeningStatus Target => JobOpeningStatus.OnHold;
}

/// <summary>Draft, Open or On Hold to Closed.</summary>
public sealed record CloseJobOpeningCommand(int Id, string? RowVersion) : IEntityCommand<CloseJobOpeningCommand>, IJobOpeningStatusCommand
{
    public static CloseJobOpeningCommand From(int id, string? rowVersion) => new(id, rowVersion);

    public static JobOpeningStatus Target => JobOpeningStatus.Closed;
}
