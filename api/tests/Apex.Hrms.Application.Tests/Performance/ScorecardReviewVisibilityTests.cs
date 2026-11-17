using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Performance.Insights;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;
using static Apex.Hrms.Application.Tests.Fakes.ReviewFixtures;
using static Apex.Hrms.Application.Tests.Fakes.ScorecardFixtures;

namespace Apex.Hrms.Application.Tests.Performance;

/// <summary>
/// The review visibility rule (<c>ReviewVisibility</c>): the scorecard's own employee does not read the manager's rating and comments, the
/// HR comment or the decision comments of the history before the decision is final, in every answer that carries them (the detail of
/// the read route and of the employee's own writes, the employee dashboard). The reviewer and HR read everything. One case per status
/// and viewer.
/// </summary>
public sealed class ScorecardReviewVisibilityTests : ScorecardWorkflowTestBase
{
    private const string DraftNote = "Draft opinion";
    private const string ItemNote = "Item opinion";
    private const string ApprovalNote = "Approval note";
    private const string ReturnReason = "Return reason";
    private const string RejectReason = "Reject reason";
    private const string HrNote = "HR note";

    public enum Viewer
    {
        Owner,
        Reviewer,
        Hr,
    }

    public static TheoryData<ScorecardStatus, Viewer> Cases
    {
        get
        {
            var cases = new TheoryData<ScorecardStatus, Viewer>();
            foreach (var status in Enum.GetValues<ScorecardStatus>())
            {
                foreach (var viewer in Enum.GetValues<Viewer>())
                {
                    cases.Add(status, viewer);
                }
            }

            return cases;
        }
    }

    private void As(World world, Viewer viewer, EmployeeDto owner)
    {
        switch (viewer)
        {
            case Viewer.Owner:
                AsEmployee(owner, 20);
                break;
            case Viewer.Reviewer:
                AsManager(world.Anna, 21, owner.Id);
                break;
            default:
                AsHr(world.Hr, 30);
                break;
        }
    }

    private async Task<ScorecardDetailDto> DraftReviewAsync(World world, ScorecardDetailDto card)
    {
        AsManager(world.Anna, 21, world.Ben.Id);
        var fresh = Host.Fresh(card);
        return ResultAssert.Succeeded(await Host.SaveReview(card.Id, ReviewOf(fresh, 4m, DraftNote, Rating(Of(fresh, "A-1"), 5m, ItemNote))));
    }

    /// <summary>
    /// A scorecard in <paramref name="status"/> in a period with an HR review, with a manager's review draft wherever the workflow keeps one,
    /// and its owner. Planning is a new scorecard of Dan; every other status is Ben's, reviewed by Anna (21) and finalized by HR (30).
    /// </summary>
    private async Task<(int Id, EmployeeDto Owner)> ReachAsync(World world, ScorecardStatus status)
    {
        if (status == ScorecardStatus.Planning)
        {
            AsHr(null, 1);
            var template = (await Host.CreateTemplateAsync(world.Plan.Category.Id, "P-1")).Id;
            return ((await Host.AssignedAsync(world.Dan.Id, world.Plan.Period.Id, Item(template, weight: 100m))).Id, world.Dan);
        }

        if (status == ScorecardStatus.Draft)
        {
            return (world.Card.Id, world.Ben);
        }

        var card = await SubmittedAsync(world);
        if (status == ScorecardStatus.UnderReview)
        {
            AsManager(world.Anna, 21, world.Ben.Id);
            card = ResultAssert.Succeeded(await Host.StartReview(Host.Fresh(card)));
        }

        card = await DraftReviewAsync(world, card);
        switch (status)
        {
            case ScorecardStatus.Submitted or ScorecardStatus.UnderReview:
                return (card.Id, world.Ben);
            case ScorecardStatus.Rejected:
                ResultAssert.Succeeded(await Host.Reject(Host.Fresh(card), RejectReason));
                return (card.Id, world.Ben);
        }

        card = ResultAssert.Succeeded(await Host.Approve(Host.Fresh(card), ApprovalNote));
        Assert.Equal("Approved", card.Status);
        if (status == ScorecardStatus.Approved)
        {
            return (card.Id, world.Ben);
        }

        AsHr(world.Hr, 30);
        if (status == ScorecardStatus.Finalized)
        {
            ResultAssert.Succeeded(await Host.FinalizeByHr(Host.Fresh(card), HrNote));
            return (card.Id, world.Ben);
        }

        card = ResultAssert.Succeeded(await Host.Return(Host.Fresh(card), ReturnReason));
        if (status == ScorecardStatus.Returned)
        {
            return (card.Id, world.Ben);
        }

        AsEmployee(world.Ben, 20);
        card = ResultAssert.Succeeded(await Host.Submit(Host.Fresh(card)));
        Assert.Equal("Resubmitted", card.Status);
        await DraftReviewAsync(world, card);
        return (card.Id, world.Ben);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_scorecard_detail_shows_each_viewer_what_the_status_allows(ScorecardStatus status, Viewer viewer)
    {
        var world = await SeedAsync(hrReview: true);
        var (id, owner) = await ReachAsync(world, status);
        As(world, viewer, owner);

        var result = await Host.GetScorecard(id);

        var isOwner = viewer == Viewer.Owner;
        if (status == ScorecardStatus.Planning && isOwner)
        {
            ResultAssert.Failed(result, ErrorType.NotFound);
            return;
        }

        var detail = ResultAssert.Succeeded(result);
        Assert.Equal(ScorecardStatusText.ToText(status), detail.Status);
        var decided = status is ScorecardStatus.Finalized or ScorecardStatus.Rejected;
        var reviewStored = status is ScorecardStatus.Submitted or ScorecardStatus.UnderReview or ScorecardStatus.Resubmitted
            or ScorecardStatus.Approved or ScorecardStatus.Rejected or ScorecardStatus.Finalized;
        var reviewShown = reviewStored && (!isOwner || decided);

        Assert.Equal(reviewShown ? (4m, DraftNote) : (null, null), (detail.ManagerRating, detail.ManagerComment));
        var item = detail.Items.FirstOrDefault(entry => entry.KpiCode == "A-1");
        if (item is not null)
        {
            Assert.Equal(reviewShown ? (5m, ItemNote) : (null, null), (item.ManagerRating, item.ManagerComment));
        }

        Assert.Equal(status == ScorecardStatus.Finalized ? HrNote : null, detail.HrComment);

        var history = detail.History.ToDictionary(entry => entry.Action, entry => entry.Comment);
        if (history.TryGetValue("Approve", out var approval))
        {
            Assert.Equal(!isOwner || decided ? ApprovalNote : null, approval);
        }

        if (history.TryGetValue("Finalize", out var hrFinal))
        {
            Assert.Equal(HrNote, hrFinal);
        }

        if (history.TryGetValue("Return", out var returned))
        {
            Assert.Equal(ReturnReason, returned);
        }

        if (history.TryGetValue("Reject", out var rejected))
        {
            Assert.Equal(RejectReason, rejected);
        }

        // Only comments are ever hidden: the steps, statuses, actors and times are the same for every viewer.
        AsHr(world.Hr, 30);
        var full = ResultAssert.Succeeded(await Host.GetScorecard(id));
        Assert.Equal(full.History.Select(entry => entry with { Comment = null }), detail.History.Select(entry => entry with { Comment = null }));
    }

    [Fact]
    public async Task The_stored_review_is_untouched_by_the_redaction()
    {
        var world = await SeedAsync(hrReview: true);
        var (id, owner) = await ReachAsync(world, ScorecardStatus.Approved);
        As(world, Viewer.Owner, owner);

        var seen = ResultAssert.Succeeded(await Host.GetScorecard(id));

        Assert.Null(seen.ManagerRating);
        Assert.Equal((4m, DraftNote), (Stored(id).ManagerRating, Stored(id).ManagerComment));
        Assert.Equal(ApprovalNote, HistoryOf(id).Single(entry => entry.Action == ReviewActionType.Approve).Comment);
    }

    [Fact]
    public async Task A_return_clears_the_review_draft_so_the_returned_scorecard_and_its_resubmission_hold_none_from_before()
    {
        var world = await SeedAsync(hrReview: true);
        var (id, _) = await ReachAsync(world, ScorecardStatus.Returned);

        Assert.Equal((null, null, null), (Stored(id).ManagerRating, Stored(id).ManagerComment, Stored(id).HrComment));
        Assert.All(
            Host.Repository<ScorecardItem>().Items.Where(item => item.ScorecardId == id),
            item => Assert.Equal((null, null), (item.ManagerRating, item.ManagerComment)));
        AsManager(world.Anna, 21, world.Ben.Id);
        var reviewer = ResultAssert.Succeeded(await Host.GetScorecard(id));
        Assert.Equal((null, null), (reviewer.ManagerRating, reviewer.ManagerComment));
        Assert.Equal(ReturnReason, reviewer.History.Single(entry => entry.Action == "Return").Comment);
    }

    [Fact]
    public async Task The_employees_own_writes_answer_with_the_same_redacted_detail()
    {
        var world = await SeedAsync(hrReview: true);
        var (id, owner) = await ReachAsync(world, ScorecardStatus.Returned);
        AsEmployee(owner, 20);
        var returned = ResultAssert.Succeeded(await Host.GetScorecard(id));
        Assert.Equal(ReturnReason, returned.History.Single(entry => entry.Action == "Return").Comment);

        var saved = ResultAssert.Succeeded(await Host.SaveResults(Host.Fresh(returned), Actual(Of(returned, "A-2"), 70m)));
        var submitted = ResultAssert.Succeeded(await Host.Submit(Host.Fresh(saved)));
        foreach (var answer in new[] { saved, submitted })
        {
            Assert.Null(answer.History.Single(entry => entry.Action == "Approve").Comment);
            Assert.Equal(ReturnReason, answer.History.Single(entry => entry.Action == "Return").Comment);
            Assert.Equal((null, null, null), (answer.ManagerRating, answer.ManagerComment, answer.HrComment));
            Assert.All(answer.Items, item => Assert.Equal((null, null), (item.ManagerRating, item.ManagerComment)));
        }

        Assert.Equal("Resubmitted", submitted.Status);
        AsManager(world.Anna, 21, owner.Id);
        var reviewerAnswer = await DraftReviewAsync(world, submitted);
        Assert.Equal((4m, DraftNote), (reviewerAnswer.ManagerRating, reviewerAnswer.ManagerComment));
        Assert.Equal(ApprovalNote, reviewerAnswer.History.Single(entry => entry.Action == "Approve").Comment);
        AsEmployee(owner, 20);
        var resubmitted = ResultAssert.Succeeded(await Host.GetScorecard(id));
        Assert.Equal((null, null), (resubmitted.ManagerRating, resubmitted.ManagerComment));
        Assert.Equal(4m, Stored(id).ManagerRating);
    }

    [Theory]
    [InlineData(ScorecardStatus.Submitted, false)]
    [InlineData(ScorecardStatus.UnderReview, false)]
    [InlineData(ScorecardStatus.Approved, false)]
    [InlineData(ScorecardStatus.Returned, false)]
    [InlineData(ScorecardStatus.Resubmitted, false)]
    [InlineData(ScorecardStatus.Rejected, true)]
    [InlineData(ScorecardStatus.Finalized, true)]
    public async Task The_employee_dashboard_feedback_follows_the_same_rule(ScorecardStatus status, bool shown)
    {
        var world = await SeedAsync(hrReview: true);
        var (_, owner) = await ReachAsync(world, status);
        AsEmployee(owner, 20);

        var dashboard = ResultAssert.Succeeded(await Host.Get<IKpiEmployeeDashboardHandler>().HandleAsync(null, default));

        Assert.Equal(ScorecardStatusText.ToText(status), dashboard.Scorecard!.Status);
        Assert.Equal(shown ? new KpiManagerFeedbackDto(4m, DraftNote) : new KpiManagerFeedbackDto(null, null), dashboard.Feedback);
    }
}
