using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Periods;

/// <summary>
/// An evaluation window of a company (kpi.EvaluationPeriod). <c>periodType</c> is Monthly, Quarterly, Half-Yearly, Yearly or
/// Custom; <c>status</c> is Draft, Active, Completed or Closed and only changes through the activate, complete and close commands.
/// </summary>
public sealed record EvaluationPeriodDto(
    int Id,
    int CompanyId,
    string Name,
    string PeriodType,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly? SubmissionDueDate,
    bool RequiresHrReview,
    string Status,
    byte[] RowVersion) : IIdentified;

/// <summary>
/// One entry of the period picker (<c>GET /api/kpi/periods/options</c>): what an employee or manager needs to choose a
/// period, without the setup permission. Draft periods are never listed.
/// </summary>
public sealed record EvaluationPeriodOptionDto(
    int Id,
    string Name,
    int CompanyId,
    string PeriodType,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    bool RequiresHrReview);

/// <summary>The fields an evaluation period has on create and update.</summary>
internal interface IEvaluationPeriodFields
{
    int CompanyId { get; }

    string Name { get; }

    /// <summary>Monthly, Quarterly, Half-Yearly, Yearly or Custom.</summary>
    string PeriodType { get; }

    DateOnly StartDate { get; }

    DateOnly EndDate { get; }

    DateOnly? SubmissionDueDate { get; }

    bool RequiresHrReview { get; }
}

/// <summary>POST body. A period always starts in Draft; there is no status field.</summary>
public sealed record EvaluationPeriodCreateRequest(
    int CompanyId,
    string Name,
    string PeriodType,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly? SubmissionDueDate,
    bool RequiresHrReview) : IEvaluationPeriodFields, ICreateRequest<EvaluationPeriodDetails>
{
    public EvaluationPeriodDetails ToDetails() => EvaluationPeriodDetailsFactory.From(this);

    // A period always starts in Draft; the shared create flow asks for a status and the factory ignores it.
    public RecordStatus ParseStatus() => RecordStatus.Active;
}

/// <summary>PUT body. Which fields may change depends on the status (Draft: all, Active: name and submission due date). Requires the row version.</summary>
public sealed record EvaluationPeriodUpdateRequest(
    int CompanyId,
    string Name,
    string PeriodType,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly? SubmissionDueDate,
    bool RequiresHrReview,
    string? RowVersion) : IEvaluationPeriodFields, IDetailsRequest<EvaluationPeriodDetails>, IVersionedRequest
{
    public EvaluationPeriodDetails ToDetails() => EvaluationPeriodDetailsFactory.From(this);
}

internal static class EvaluationPeriodDetailsFactory
{
    public static EvaluationPeriodDetails From(IEvaluationPeriodFields fields) => new EvaluationPeriodDetails(
        fields.CompanyId,
        fields.Name,
        KpiFrequencyText.Parse(fields.PeriodType.Trim()),
        fields.StartDate,
        fields.EndDate,
        fields.SubmissionDueDate,
        fields.RequiresHrReview).Normalized();
}

public sealed record EvaluationPeriodFilter(
    int? CompanyId = null,
    string? PeriodType = null,
    string? Status = null,
    int? Year = null);

// ---- Commands (the status never changes through PUT) ----

/// <summary>A move of a period through its life. <see cref="Verb"/> is the past tense used in the activity entry.</summary>
public interface IPeriodMove
{
    string Verb { get; }
}

/// <summary>Draft to Active.</summary>
public sealed record ActivatePeriodCommand(int Id, string? RowVersion) : IEntityCommand<ActivatePeriodCommand>, IPeriodMove
{
    public string Verb => "activated";

    public static ActivatePeriodCommand From(int id, string? rowVersion) => new(id, rowVersion);
}

/// <summary>Active to Completed.</summary>
public sealed record CompletePeriodCommand(int Id, string? RowVersion) : IEntityCommand<CompletePeriodCommand>, IPeriodMove
{
    public string Verb => "completed";

    public static CompletePeriodCommand From(int id, string? rowVersion) => new(id, rowVersion);
}

/// <summary>Completed to Closed (final).</summary>
public sealed record ClosePeriodCommand(int Id, string? RowVersion) : IEntityCommand<ClosePeriodCommand>, IPeriodMove
{
    public string Verb => "closed";

    public static ClosePeriodCommand From(int id, string? rowVersion) => new(id, rowVersion);
}
