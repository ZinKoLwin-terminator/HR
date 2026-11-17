using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Fakes;

/// <summary>The handler calls of the submission and review tests. A call that changes a scorecard sends the row version of the scorecard it is given, unless one is passed.</summary>
internal static class ReviewFixtures
{
    public static ResultItemRequest Actual(ScorecardItemDto item, decimal? actual, string? comment = null) => new(item.Id, actual, comment);

    public static ReviewItemRequest Rating(ScorecardItemDto item, decimal? rating, string? comment = null) => new(item.Id, rating, comment);

    public static SaveScorecardResultsRequest ResultsOf(ScorecardDetailDto card, string? employeeComment, params ResultItemRequest[] items) =>
        new(Requests.Version(card.RowVersion), employeeComment, items);

    public static SaveScorecardReviewRequest ReviewOf(ScorecardDetailDto card, decimal? rating, string? comment, params ReviewItemRequest[] items) =>
        new(Requests.Version(card.RowVersion), rating, comment, items);

    public static Task<Result<ScorecardDetailDto>> SaveResults(this TestHost host, int id, SaveScorecardResultsRequest request) =>
        host.Get<IUpdateHandler<SaveScorecardResultsRequest, ScorecardDetailDto>>().HandleAsync(id, request, default);

    public static Task<Result<ScorecardDetailDto>> SaveResults(this TestHost host, ScorecardDetailDto card, params ResultItemRequest[] items) =>
        host.SaveResults(card.Id, ResultsOf(card, null, items));

    public static Task<Result<ScorecardDetailDto>> SaveReview(this TestHost host, int id, SaveScorecardReviewRequest request) =>
        host.Get<IUpdateHandler<SaveScorecardReviewRequest, ScorecardDetailDto>>().HandleAsync(id, request, default);

    public static Task<Result<ScorecardDetailDto>> Submit(this TestHost host, ScorecardDetailDto card, string? rowVersion = null) =>
        host.Command<SubmitScorecardCommand, ScorecardDetailDto>(new SubmitScorecardCommand(card.Id, rowVersion ?? Requests.Version(card.RowVersion)));

    public static Task<Result<ScorecardDetailDto>> StartReview(this TestHost host, ScorecardDetailDto card, string? rowVersion = null) =>
        host.Command<StartScorecardReviewCommand, ScorecardDetailDto>(new StartScorecardReviewCommand(card.Id, rowVersion ?? Requests.Version(card.RowVersion)));

    public static Task<Result<ScorecardDetailDto>> Approve(this TestHost host, ScorecardDetailDto card, string? comment = null, string? rowVersion = null) =>
        host.Command<ApproveScorecardCommand, ScorecardDetailDto>(new ApproveScorecardCommand(card.Id, rowVersion ?? Requests.Version(card.RowVersion), comment));

    public static Task<Result<ScorecardDetailDto>> Return(this TestHost host, ScorecardDetailDto card, string? comment, string? rowVersion = null) =>
        host.Command<ReturnScorecardCommand, ScorecardDetailDto>(new ReturnScorecardCommand(card.Id, rowVersion ?? Requests.Version(card.RowVersion), comment));

    public static Task<Result<ScorecardDetailDto>> Reject(this TestHost host, ScorecardDetailDto card, string? comment, string? rowVersion = null) =>
        host.Command<RejectScorecardCommand, ScorecardDetailDto>(new RejectScorecardCommand(card.Id, rowVersion ?? Requests.Version(card.RowVersion), comment));

    public static Task<Result<ScorecardDetailDto>> FinalizeByHr(this TestHost host, ScorecardDetailDto card, string? comment = null, string? rowVersion = null) =>
        host.Command<FinalizeScorecardCommand, ScorecardDetailDto>(new FinalizeScorecardCommand(card.Id, rowVersion ?? Requests.Version(card.RowVersion), comment));

    public static Task<Result<PagedResult<ScorecardDto>>> MyScorecards(this TestHost host, ScorecardFilter? filter = null, int pageIndex = 0, int pageSize = 50, string? sort = null, string direction = "asc") =>
        host.Get<IMyScorecardsHandler>().HandleAsync(
            new PageRequest<ScorecardFilter>(pageIndex, pageSize, sort is null ? null : new SortState(sort, direction), null, filter ?? new ScorecardFilter()),
            default);

    public static Task<Result<PagedResult<ScorecardDto>>> PendingReviews(this TestHost host, ScorecardFilter? filter = null, string? search = null, int pageIndex = 0, int pageSize = 50) =>
        host.Get<IPendingReviewsHandler>().HandleAsync(
            new PageRequest<ScorecardFilter>(pageIndex, pageSize, null, search, filter ?? new ScorecardFilter()),
            default);

    /// <summary>The scorecard with the row version it has in the store now, whoever the caller is (what a screen holds after reloading it).</summary>
    public static ScorecardDetailDto Fresh(this TestHost host, ScorecardDetailDto card) =>
        card with { RowVersion = host.Repository<Apex.Hrms.Domain.Performance.Scorecard>().Items.Single(scorecard => scorecard.Id == card.Id).RowVersion };
}
