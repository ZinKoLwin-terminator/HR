using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Performance.Scorecards;

/// <summary>
/// The scorecard list. Rows are scoped inside the query by the access scope of the caller (own, own and direct reports and the ones
/// they review, or all, by <c>kpi:Approve</c> and <c>employees:Edit</c>), filtered, sorted by the whitelist and paged on the server.
/// </summary>
internal sealed class ScorecardQueryHandler(ScorecardReader reader) : IQueryHandler<ScorecardDto, ScorecardFilter>
{
    public Task<Result<PagedResult<ScorecardDto>>> HandleAsync(PageRequest<ScorecardFilter> request, CancellationToken cancellationToken) =>
        reader.PageAsync(reader.Scoped(), request, fallbackKey: null, fallbackDescending: false, cancellationToken);
}

/// <summary>One scorecard with its items and history; outside the scope of the caller it does not exist (404).</summary>
internal sealed class ScorecardGetHandler(ScorecardReader reader) : IGetByIdHandler<ScorecardDetailDto>
{
    public Task<Result<ScorecardDetailDto>> HandleAsync(int id, CancellationToken cancellationToken) =>
        reader.DetailAsync(reader.Scoped(), id, cancellationToken);
}
