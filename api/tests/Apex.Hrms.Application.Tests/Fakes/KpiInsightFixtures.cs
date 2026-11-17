using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Performance.Categories;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Application.Performance.Templates;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Tests.Fakes;

/// <summary>
/// A fictional organization with scorecards in known states and scores, stored straight into the in-memory database (the workflow
/// itself is tested elsewhere), so every dashboard and report figure can be worked out by hand.
/// <para>
/// Q4 2026 (Active, 2026-10-01..2026-12-31, due 2027-01-10). Department ENG: Anna (EMP-0001) Finalized 90 Exceeds; Ben (EMP-0002, reports to
/// Anna) Finalized 80 Meets Expectations; Cara (EMP-0003, reports to Anna) Finalized 90 Exceeds; Dan (EMP-0004, reports to Anna) Submitted 55.
/// Department OPS: Eve (EMP-0005) Rejected 40; Finn (EMP-0006) Draft; Gail (EMP-0007, Anna is her reviewer but not her manager) Finalized 65
/// Needs Improvement; Hana (EMP-0008, HR) Planning without KPIs; Ivy (EMP-0009) Returned; Jon (EMP-0010) Approved 70.
/// </para>
/// <para>Q3 2026 (Completed, 2026-07-01..2026-09-30): Ben Finalized 70, Cara Finalized 70, Dan Finalized 60 (all ENG).</para>
/// <para>
/// Every scorecard but Hana's has one KPI of category PROD (template A-1, target 100, weight 100; Ben's Q4 one weight 80) whose
/// result is the score (none for Draft and Returned); Ben's Q4 scorecard also has a QUAL KPI (template Q-1, target 50, weight 20,
/// actual 40, so 80% achievement). Bands: Exceeds 90, Meets Expectations 70,
/// Needs Improvement 50 (active) and Old band 62 (inactive), so the attention threshold is 70.
/// </para>
/// </summary>
internal sealed record InsightWorld(
    OrgSeed Org,
    DepartmentDto Ops,
    EvaluationPeriodDto Q4,
    EvaluationPeriodDto Q3,
    KpiCategoryDto Productivity,
    KpiCategoryDto Quality,
    IReadOnlyDictionary<string, EmployeeDto> People,
    IReadOnlyDictionary<(string Who, int PeriodId), Scorecard> Cards)
{
    public int Id(string who) => People[who].Id;

    public Scorecard Card(string who, EvaluationPeriodDto period) => Cards[(who, period.Id)];
}

internal static class KpiInsightFixtures
{
    /// <summary>The manager's review draft every scorecard past submission carries (a return clears it).</summary>
    public const decimal ManagerRating = 4m;

    public const string ManagerNote = "Manager note that must never be exported.";

    private static readonly ReviewActor Admin = new(1, null, "Administrator");

    public static async Task<InsightWorld> SeedInsightsAsync(this TestHost host)
    {
        var org = await host.SeedOrgAsync();
        var ops = await host.CreateDepartmentAsync(org.Company.Id, org.Branch.Id, "OPS");
        var productivity = await host.CreateCategoryAsync();
        var quality = await host.CreateCategoryAsync("QUAL", "Quality");
        var a1 = await host.CreateTemplateAsync(productivity.Id, "A-1", "Output");
        var q1 = await host.CreateTemplateAsync(quality.Id, "Q-1", "Defect-free work");

        var q3 = await host.CreatePeriodAsync(org.Company.Id, "2026 Q3", start: "2026-07-01", end: "2026-09-30", due: "2026-10-10");
        q3 = ResultAssert.Succeeded(await host.MoveAsync<ActivatePeriodCommand>(q3));
        q3 = ResultAssert.Succeeded(await host.MoveAsync<CompletePeriodCommand>(q3));
        var q4 = await host.CreatePeriodAsync(org.Company.Id, "2026 Q4");
        q4 = ResultAssert.Succeeded(await host.MoveAsync<ActivatePeriodCommand>(q4));

        await host.CreateBandAsync("Exceeds", 90m);
        await host.CreateBandAsync("Meets Expectations", 70m);
        await host.CreateBandAsync("Needs Improvement", 50m);
        ResultAssert.Succeeded(await host.Create<Apex.Hrms.Application.Performance.RatingBands.RatingBandCreateRequest, Apex.Hrms.Application.Performance.RatingBands.RatingBandDto>(
            KpiFixtures.NewBand("Old band", 62m, "Inactive")));

        var people = new Dictionary<string, EmployeeDto>();
        var anna = await host.CreateStaffAsync(org, "EMP-0001", first: "Anna", last: "Lead");
        people["Anna"] = anna;
        foreach (var (name, number, manager) in new (string Name, int Number, int? Manager)[]
        {
            ("Ben", 2, anna.Id), ("Cara", 3, anna.Id), ("Dan", 4, anna.Id), ("Eve", 5, null), ("Finn", 6, null),
            ("Gail", 7, null), ("Hana", 8, null), ("Ivy", 9, null), ("Jon", 10, null),
        })
        {
            people[name] = await host.CreateStaffAsync(org, $"EMP-{number:0000}", manager, name, "Test");
        }

        var world = new InsightWorld(org, ops, q4, q3, productivity, quality, people, new Dictionary<(string, int), Scorecard>());
        var cards = (Dictionary<(string, int), Scorecard>)world.Cards;
        var eng = org.Department.Id;
        void Add(string who, EvaluationPeriodDto period, int department, string? reviewer, ScorecardStatus status, decimal? score, string? label, bool items = true, decimal weight = 100m)
        {
            var card = host.StoreScorecard(world, people[who].Id, period, department, reviewer is null ? null : people[reviewer].Id, status, score, label);
            if (items)
            {
                host.StoreItem(card, a1, period, 1, 100m, weight, score);
            }

            cards[(who, period.Id)] = card;
        }

        Add("Anna", q4, eng, null, ScorecardStatus.Finalized, 90m, "Exceeds");
        Add("Ben", q4, eng, "Anna", ScorecardStatus.Finalized, 80m, "Meets Expectations", weight: 80m);
        host.StoreItem(world.Card("Ben", q4), q1, q4, 2, 50m, 20m, 40m);
        Add("Cara", q4, eng, "Anna", ScorecardStatus.Finalized, 90m, "Exceeds");
        Add("Dan", q4, eng, "Anna", ScorecardStatus.Submitted, 55m, "Needs Improvement");
        Add("Eve", q4, ops.Id, null, ScorecardStatus.Rejected, 40m, null);
        Add("Finn", q4, ops.Id, null, ScorecardStatus.Draft, null, null);
        Add("Gail", q4, ops.Id, "Anna", ScorecardStatus.Finalized, 65m, "Needs Improvement");
        Add("Hana", q4, ops.Id, null, ScorecardStatus.Planning, null, null, items: false);
        Add("Ivy", q4, ops.Id, null, ScorecardStatus.Returned, null, null);
        Add("Jon", q4, ops.Id, null, ScorecardStatus.Approved, 70m, "Meets Expectations");

        Add("Ben", q3, eng, "Anna", ScorecardStatus.Finalized, 70m, "Meets Expectations");
        Add("Cara", q3, eng, "Anna", ScorecardStatus.Finalized, 70m, "Meets Expectations");
        Add("Dan", q3, eng, "Anna", ScorecardStatus.Finalized, 60m, "Needs Improvement");
        return world;
    }

    /// <summary>
    /// A scorecard of <paramref name="employeeId"/> moved to <paramref name="status"/> through the domain's own workflow methods (Submit
    /// stores <paramref name="score"/> and <paramref name="label"/>), then stored.
    /// </summary>
    public static Scorecard StoreScorecard(
        this TestHost host,
        InsightWorld world,
        int employeeId,
        EvaluationPeriodDto period,
        int departmentId,
        int? reviewerId,
        ScorecardStatus status,
        decimal? score,
        string? label)
    {
        var org = world.Org;
        var card = ResultAssert.Succeeded(Scorecard.Create(employeeId, period.Id, org.Company.Id, org.Branch.Id, departmentId, org.Position.Id, reviewerId));
        host.Store(card);
        var today = host.Clock.Today;
        var now = host.Clock.UtcNow.UtcDateTime;
        if (status == ScorecardStatus.Planning)
        {
            return card;
        }

        Step(card.FinalizePlan(today, reviewerId, [100m], Admin, now));
        if (status == ScorecardStatus.Draft)
        {
            return card;
        }

        var scored = new ScorecardScore([], score ?? 50m);
        Step(card.Submit(today, scored, label, Admin, now));

        // Every scorecard that reached the manager stage carries a review draft, so the dashboards can show whether it is visible.
        Assert.True(card.SaveReview(ManagerRating, ManagerNote).IsSuccess);
        switch (status)
        {
            case ScorecardStatus.Submitted:
                break;
            case ScorecardStatus.UnderReview:
                Step(card.StartReview(Admin, now));
                break;
            case ScorecardStatus.Returned:
                Step(card.Return(Admin, "More detail please.", [], now));
                break;
            case ScorecardStatus.Rejected:
                Step(card.Reject(Admin, "Not acceptable.", now));
                break;
            case ScorecardStatus.Approved:
                Step(card.Approve(true, today, scored, label, null, Admin, now));
                break;
            case ScorecardStatus.Finalized:
                Step(card.Approve(false, today, scored, label, null, Admin, now));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Not a status this fixture builds.");
        }

        return card;
    }

    /// <summary>A Higher Is Better, Direct Percentage KPI of the template with the given result (achievement = actual ÷ target × 100).</summary>
    public static ScorecardItem StoreItem(this TestHost host, Scorecard card, KpiTemplateDto template, EvaluationPeriodDto period, short sortOrder, decimal target, decimal weight, decimal? actual)
    {
        var item = ResultAssert.Succeeded(ScorecardItem.Create(new ScorecardItemDetails(
            card.Id,
            template.Id,
            template.CategoryId,
            template.Code,
            template.Name,
            KpiMeasurementType.Number,
            null,
            KpiDirection.HigherIsBetter,
            KpiScoringMethod.DirectPercentage,
            target,
            null,
            null,
            null,
            weight,
            null,
            period.StartDate,
            period.EndDate,
            sortOrder,
            null,
            host.Clock.Today)));
        if (actual is { } value)
        {
            Assert.True(item.RecordResult(value, "A comment that must never be exported.").IsSuccess);
        }

        host.Store(item);
        return item;
    }

    /// <summary>Signs in as an employee of the world: kpi:View and kpi:Create only (the Own tier).</summary>
    public static void AsEmployee(this TestHost host, InsightWorld world, string who)
    {
        host.User.EmployeeId = world.Id(who);
        host.User.UserId = 100 + world.Id(who);
        host.Permissions.Only("kpi:View", "kpi:Create", "excel:View", "excel:Export");
        host.Team.Set([]);
    }

    /// <summary>Signs in as a manager (the Team tier) with these direct reports.</summary>
    public static void AsManager(this TestHost host, InsightWorld world, string who, params string[] reports)
    {
        host.User.EmployeeId = world.Id(who);
        host.User.UserId = 100 + world.Id(who);
        host.Permissions.Only("kpi:View", "kpi:Create", "kpi:Approve", "excel:View", "excel:Export");
        host.Team.Set([.. reports.Select(world.Id)]);
    }

    /// <summary>Signs in as HR (the All tier: kpi:Approve with employees:Edit).</summary>
    public static void AsHr(this TestHost host, InsightWorld world, string? who = "Hana")
    {
        host.User.EmployeeId = who is null ? null : world.Id(who);
        host.User.UserId = who is null ? 1 : 100 + world.Id(who);
        host.Permissions.Only("kpi:View", "kpi:Create", "kpi:Approve", "employees:View", "employees:Edit", "excel:View", "excel:Export");
        host.Team.Set([]);
    }

    private static void Step(Result<ReviewAction> step) => ResultAssert.Succeeded(step);
}
