using Apex.Hrms.Application.Performance.Categories;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Application.Performance.RatingBands;
using Apex.Hrms.Application.Performance.Templates;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Tests.Fakes;

/// <summary>Fictional KPI master data requests and the handler calls the KPI tests share. The fake clock says today is 2026-11-09.</summary>
internal static class KpiFixtures
{
    public static KpiCategoryCreateRequest NewCategory(string code = "PROD", string name = "Productivity", string status = "Active") =>
        new(code, name, Description: null, status);

    public static KpiCategoryUpdateRequest CategoryUpdateOf(KpiCategoryDto current) =>
        new(current.Code, current.Name, current.Description, Requests.Version(current.RowVersion));

    /// <summary>A higher-is-better number KPI scored as a direct percentage; override what a test cares about.</summary>
    public static KpiTemplateCreateRequest NewTemplate(
        int categoryId,
        string code = "SALES-01",
        string name = "Monthly sales",
        int? departmentId = null,
        string measurement = "Number",
        string direction = "Higher Is Better",
        string scoring = "Direct Percentage",
        decimal target = 100m,
        decimal? rangeMin = null,
        decimal? rangeMax = null,
        decimal? maxRating = null,
        decimal weight = 20m,
        decimal? cap = null,
        string frequency = "Monthly",
        string status = "Active") => new(
        code,
        name,
        Description: null,
        categoryId,
        departmentId,
        measurement,
        Unit: null,
        direction,
        scoring,
        target,
        rangeMin,
        rangeMax,
        maxRating,
        weight,
        cap,
        frequency,
        status);

    public static KpiTemplateUpdateRequest TemplateUpdateOf(KpiTemplateDto current) => new(
        current.Code,
        current.Name,
        current.Description,
        current.CategoryId,
        current.DepartmentId,
        current.MeasurementType,
        current.Unit,
        current.Direction,
        current.ScoringMethod,
        current.DefaultTarget,
        current.RangeMin,
        current.RangeMax,
        current.MaxRating,
        current.DefaultWeight,
        current.MaxAchievementPct,
        current.Frequency,
        Requests.Version(current.RowVersion));

    public static EvaluationPeriodCreateRequest NewPeriod(
        int companyId,
        string name = "2026 Q4",
        string type = "Quarterly",
        string start = "2026-10-01",
        string end = "2026-12-31",
        string? due = "2027-01-10",
        bool hr = false) => new(
        companyId,
        name,
        type,
        DateOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture),
        DateOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture),
        due is null ? null : DateOnly.Parse(due, System.Globalization.CultureInfo.InvariantCulture),
        hr);

    public static EvaluationPeriodUpdateRequest PeriodUpdateOf(EvaluationPeriodDto current, string? name = null, DateOnly? due = null, DateOnly? end = null, string? type = null) => new(
        current.CompanyId,
        name ?? current.Name,
        type ?? current.PeriodType,
        current.StartDate,
        end ?? current.EndDate,
        due ?? current.SubmissionDueDate,
        current.RequiresHrReview,
        Requests.Version(current.RowVersion));

    public static RatingBandCreateRequest NewBand(string label = "Meets Expectations", decimal minScore = 70m, string status = "Active") =>
        new(label, minScore, Description: null, status);

    public static RatingBandUpdateRequest BandUpdateOf(RatingBandDto current) =>
        new(current.Label, current.MinScore, current.Description, Requests.Version(current.RowVersion));

    // ---- The handler calls, one per request ----

    public static async Task<Result<T>> Create<TRequest, T>(this TestHost host, TRequest request) =>
        await host.Get<ICreateHandler<TRequest, T>>().HandleAsync(request, default);

    public static async Task<Result<T>> Update<TRequest, T>(this TestHost host, int id, TRequest request) =>
        await host.Get<IUpdateHandler<TRequest, T>>().HandleAsync(id, request, default);

    public static async Task<Result> Delete<T>(this TestHost host, int id, byte[] rowVersion) =>
        await host.Get<IDeleteHandler<T>>().HandleAsync(id, Requests.Version(rowVersion), default);

    public static async Task<KpiCategoryDto> CreateCategoryAsync(this TestHost host, string code = "PROD", string name = "Productivity", string status = "Active") =>
        ResultAssert.Succeeded(await host.Create<KpiCategoryCreateRequest, KpiCategoryDto>(NewCategory(code, name, status)));

    public static async Task<KpiTemplateDto> CreateTemplateAsync(this TestHost host, int categoryId, string code = "SALES-01", string name = "Monthly sales", int? departmentId = null) =>
        ResultAssert.Succeeded(await host.Create<KpiTemplateCreateRequest, KpiTemplateDto>(NewTemplate(categoryId, code, name, departmentId)));

    public static async Task<EvaluationPeriodDto> CreatePeriodAsync(
        this TestHost host,
        int companyId,
        string name = "2026 Q4",
        string type = "Quarterly",
        string start = "2026-10-01",
        string end = "2026-12-31",
        string? due = "2027-01-10",
        bool hr = false) =>
        ResultAssert.Succeeded(await host.Create<EvaluationPeriodCreateRequest, EvaluationPeriodDto>(NewPeriod(companyId, name, type, start, end, due, hr)));

    public static async Task<RatingBandDto> CreateBandAsync(this TestHost host, string label = "Meets Expectations", decimal minScore = 70m) =>
        ResultAssert.Succeeded(await host.Create<RatingBandCreateRequest, RatingBandDto>(NewBand(label, minScore)));

    public static async Task<Result<EvaluationPeriodDto>> MoveAsync<TCommand>(this TestHost host, EvaluationPeriodDto period)
        where TCommand : IEntityCommand<TCommand> =>
        await host.Command<TCommand, EvaluationPeriodDto>(TCommand.From(period.Id, Requests.Version(period.RowVersion)));

    /// <summary>Adds a record straight to the in-memory store, as if another request had saved it (for what only a later phase creates, such as a scorecard).</summary>
    public static void Store<TEntity>(this TestHost host, TEntity entity)
        where TEntity : Entity
    {
        host.Repository<TEntity>().Add(entity);
    }

}
