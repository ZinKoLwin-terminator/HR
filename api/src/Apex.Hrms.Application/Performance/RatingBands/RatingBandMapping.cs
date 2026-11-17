using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.RatingBands;

internal sealed class RatingBandMapping :
    IEntityDescriptor<RatingBand>,
    IDtoMapper<RatingBand, RatingBandDto>,
    IEntityFactory<RatingBand, RatingBandDetails>
{
    public string Label => "Rating band";

    public string ActivityCategory => Domain.Activity.ActivityCategories.Performance;

    public string Describe(RatingBand entity) => entity.Label;

    public RatingBandDto ToDto(RatingBand entity) => new(
        entity.Id,
        entity.Label,
        entity.MinScore,
        entity.Description ?? string.Empty,
        entity.Status.ToString(),
        entity.RowVersion);

    public Result Validate(RatingBandDetails details, RatingBand? existing) => RatingBand.Validate(details);

    public Result<RatingBand> Create(RatingBandDetails details, RecordStatus status) => RatingBand.Create(details, status);

    public Result Update(RatingBand entity, RatingBandDetails details) => entity.Update(details);
}

internal sealed class ActivateRatingBandAction : ICommandAction<RatingBand, ActivateCommand>
{
    public Result Execute(RatingBand entity, ActivateCommand command) => entity.Activate();
}

internal sealed class DeactivateRatingBandAction : ICommandAction<RatingBand, DeactivateCommand>
{
    public Result Execute(RatingBand entity, DeactivateCommand command) => entity.Deactivate();
}
