using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.RatingBands;

/// <summary>An overall-score band (kpi.RatingBand): a score of at least <c>minScore</c> gets the label, up to the next band.</summary>
public sealed record RatingBandDto(
    int Id,
    string Label,
    decimal MinScore,
    string Description,
    string Status,
    byte[] RowVersion) : IIdentified;

/// <summary>The fields a rating band has on create and update.</summary>
internal interface IRatingBandFields
{
    string Label { get; }

    decimal MinScore { get; }

    string? Description { get; }
}

/// <summary>POST body. Status is the status the new band starts with.</summary>
public sealed record RatingBandCreateRequest(
    string Label,
    decimal MinScore,
    string? Description,
    string Status) : IRatingBandFields, ICreateRequest<RatingBandDetails>
{
    public RatingBandDetails ToDetails() => RatingBandDetailsFactory.From(this);

    public RecordStatus ParseStatus() => Enum.Parse<RecordStatus>(Status);
}

/// <summary>PUT body. It has no status (use activate and deactivate) and requires the row version.</summary>
public sealed record RatingBandUpdateRequest(
    string Label,
    decimal MinScore,
    string? Description,
    string? RowVersion) : IRatingBandFields, IDetailsRequest<RatingBandDetails>, IVersionedRequest
{
    public RatingBandDetails ToDetails() => RatingBandDetailsFactory.From(this);
}

internal static class RatingBandDetailsFactory
{
    public static RatingBandDetails From(IRatingBandFields fields) =>
        new RatingBandDetails(fields.Label, fields.MinScore, fields.Description).Normalized();
}

public sealed record RatingBandFilter(string? Status = null);
