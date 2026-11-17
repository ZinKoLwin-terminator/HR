using Apex.Hrms.Application.Performance.Categories;
using Apex.Hrms.Application.Performance.RatingBands;
using Apex.Hrms.Application.Performance.Templates;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Activity;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Tests.Performance;

/// <summary>KPI categories, templates and rating bands: validation, uniqueness, parent rules, delete protection and activity wording.</summary>
public sealed class KpiMasterDataTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private static ScorecardItem ItemOf(KpiTemplateDto template, int scorecardId = 1) =>
        ScorecardItem.Create(new ScorecardItemDetails(
            scorecardId,
            template.Id,
            template.CategoryId,
            template.Code,
            template.Name,
            KpiMeasurementTypeText.Parse(template.MeasurementType),
            template.Unit,
            KpiDirectionText.Parse(template.Direction),
            KpiScoringMethodText.Parse(template.ScoringMethod),
            template.DefaultTarget,
            template.RangeMin,
            template.RangeMax,
            template.MaxRating,
            template.DefaultWeight,
            template.MaxAchievementPct,
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 31),
            1,
            null,
            new DateOnly(2026, 11, 9))).Value;

    // ---- Categories ----

    [Fact]
    public async Task A_category_is_created_with_its_fields_and_row_version()
    {
        var created = ResultAssert.Succeeded(await host.Create<KpiCategoryCreateRequest, KpiCategoryDto>(
            KpiFixtures.NewCategory("PROD", "Productivity") with { Description = "  Output and quality  " }));

        Assert.Equal("PROD", created.Code);
        Assert.Equal("Productivity", created.Name);
        Assert.Equal("Output and quality", created.Description);
        Assert.Equal("Active", created.Status);
        Assert.NotEmpty(created.RowVersion);
    }

    [Theory]
    [InlineData("prod")]
    [InlineData("PR OD")]
    [InlineData("-PROD")]
    [InlineData("")]
    public async Task The_category_code_must_be_upper_case_letters_digits_and_dashes(string code) =>
        ResultAssert.FailedOnField(await host.Create<KpiCategoryCreateRequest, KpiCategoryDto>(KpiFixtures.NewCategory(code)), "code");

    [Fact]
    public async Task The_category_code_is_at_most_20_characters_the_name_100_and_the_description_300()
    {
        ResultAssert.FailedOnField(await host.Create<KpiCategoryCreateRequest, KpiCategoryDto>(KpiFixtures.NewCategory(new string('A', 21))), "code");
        Assert.True((await host.Create<KpiCategoryCreateRequest, KpiCategoryDto>(KpiFixtures.NewCategory(new string('A', 20)))).IsSuccess);
        ResultAssert.FailedOnField(await host.Create<KpiCategoryCreateRequest, KpiCategoryDto>(KpiFixtures.NewCategory("B", new string('x', 101))), "name");
        Assert.True((await host.Create<KpiCategoryCreateRequest, KpiCategoryDto>(KpiFixtures.NewCategory("C", new string('x', 100)))).IsSuccess);
        ResultAssert.FailedOnField(
            await host.Create<KpiCategoryCreateRequest, KpiCategoryDto>(KpiFixtures.NewCategory("D", "Other") with { Description = new string('d', 301) }),
            "description");
    }

    [Fact]
    public async Task The_category_code_and_name_must_be_unique()
    {
        await host.CreateCategoryAsync("PROD", "Productivity");

        ResultAssert.FailedOnField(
            await host.Create<KpiCategoryCreateRequest, KpiCategoryDto>(KpiFixtures.NewCategory("PROD", "Other")),
            "code",
            "KPI category code \"PROD\" already exists.");
        ResultAssert.FailedOnField(
            await host.Create<KpiCategoryCreateRequest, KpiCategoryDto>(KpiFixtures.NewCategory("QUAL", "Productivity")),
            "name",
            "KPI category name \"Productivity\" already exists.");
    }

    [Fact]
    public async Task A_category_keeps_its_own_code_and_name_on_update_and_status_changes_only_by_command()
    {
        var category = await host.CreateCategoryAsync();

        var changed = ResultAssert.Succeeded(await host.Update<KpiCategoryUpdateRequest, KpiCategoryDto>(
            category.Id,
            KpiFixtures.CategoryUpdateOf(category) with { Description = "Updated" }));
        var inactive = ResultAssert.Succeeded(await host.Command<DeactivateCommand, KpiCategoryDto>(new DeactivateCommand(category.Id, Requests.Version(changed.RowVersion))));
        var active = ResultAssert.Succeeded(await host.Command<ActivateCommand, KpiCategoryDto>(new ActivateCommand(category.Id, Requests.Version(inactive.RowVersion))));

        Assert.Equal("Updated", changed.Description);
        Assert.Equal("Active", changed.Status);
        Assert.Equal("Inactive", inactive.Status);
        Assert.Equal("Active", active.Status);
    }

    [Fact]
    public async Task A_stale_row_version_on_a_category_update_is_a_conflict()
    {
        var category = await host.CreateCategoryAsync();
        ResultAssert.Succeeded(await host.Update<KpiCategoryUpdateRequest, KpiCategoryDto>(category.Id, KpiFixtures.CategoryUpdateOf(category) with { Name = "First" }));

        var stale = await host.Update<KpiCategoryUpdateRequest, KpiCategoryDto>(category.Id, KpiFixtures.CategoryUpdateOf(category) with { Name = "Second" });

        ResultAssert.Failed(stale, ErrorType.Conflict);
    }

    [Fact]
    public async Task A_category_used_by_a_template_cannot_be_deleted_and_the_count_is_in_the_message()
    {
        var category = await host.CreateCategoryAsync();
        await host.CreateTemplateAsync(category.Id, "SALES-01", "Monthly sales");
        await host.CreateTemplateAsync(category.Id, "SALES-02", "Quarterly sales");

        var result = await host.Delete<KpiCategoryDto>(category.Id, category.RowVersion);

        ResultAssert.Failed(
            result,
            ErrorType.Conflict,
            "\"Productivity\" cannot be deleted because it is used by 2 template(s). Deactivate it instead.");
    }

    [Fact]
    public async Task A_category_used_only_by_an_assigned_kpi_cannot_be_deleted_either()
    {
        var category = await host.CreateCategoryAsync();
        var template = await host.CreateTemplateAsync(category.Id);
        Assert.True((await host.Delete<KpiTemplateDto>(template.Id, template.RowVersion)).IsSuccess);
        host.Store(ItemOf(template));

        var result = await host.Delete<KpiCategoryDto>(category.Id, category.RowVersion);

        ResultAssert.Failed(result, ErrorType.Conflict, "\"Productivity\" cannot be deleted because it is used by 1 assigned KPI(s). Deactivate it instead.");
    }

    [Fact]
    public async Task An_unused_category_can_be_deleted()
    {
        var category = await host.CreateCategoryAsync();

        Assert.True((await host.Delete<KpiCategoryDto>(category.Id, category.RowVersion)).IsSuccess);
        ResultAssert.Failed(await host.Get<IGetByIdHandler<KpiCategoryDto>>().HandleAsync(category.Id, default), ErrorType.NotFound);
    }

    [Fact]
    public async Task Category_changes_are_logged_under_performance()
    {
        var category = await host.CreateCategoryAsync();
        var changed = ResultAssert.Succeeded(await host.Update<KpiCategoryUpdateRequest, KpiCategoryDto>(category.Id, KpiFixtures.CategoryUpdateOf(category) with { Description = "x" }));
        await host.Command<DeactivateCommand, KpiCategoryDto>(new DeactivateCommand(category.Id, Requests.Version(changed.RowVersion)));
        var current = ResultAssert.Succeeded(await host.Get<IGetByIdHandler<KpiCategoryDto>>().HandleAsync(category.Id, default));
        await host.Delete<KpiCategoryDto>(category.Id, current.RowVersion);

        Assert.Equal(
            [
                "KPI category \"Productivity\" created",
                "KPI category \"Productivity\" updated",
                "KPI category \"Productivity\" updated",
                "KPI category \"Productivity\" deleted",
            ],
            host.ActivityLog.Messages);
        Assert.All(host.ActivityLog.Entries, entry => Assert.Equal(ActivityCategories.Performance, entry.Category));
    }

    // ---- Templates ----

    [Fact]
    public async Task A_template_is_created_with_the_texts_of_the_database_checks_and_its_numbers()
    {
        var category = await host.CreateCategoryAsync();

        var template = ResultAssert.Succeeded(await host.Create<KpiTemplateCreateRequest, KpiTemplateDto>(
            KpiFixtures.NewTemplate(category.Id, direction: "Lower Is Better", scoring: "Capped Percentage", target: 5m, cap: 120m, frequency: "Half-Yearly") with { Unit = " tickets " }));

        Assert.Equal("Number", template.MeasurementType);
        Assert.Equal("Lower Is Better", template.Direction);
        Assert.Equal("Capped Percentage", template.ScoringMethod);
        Assert.Equal("Half-Yearly", template.Frequency);
        Assert.Equal("tickets", template.Unit);
        Assert.Equal(5m, template.DefaultTarget);
        Assert.Equal(120m, template.MaxAchievementPct);
        Assert.Equal(20m, template.DefaultWeight);
        Assert.Null(template.DepartmentId);
        Assert.Equal("Active", template.Status);
    }

    [Fact]
    public async Task The_template_shape_is_validated_with_a_field_error_for_each_problem()
    {
        var category = await host.CreateCategoryAsync();
        Task<Result<KpiTemplateDto>> Create(KpiTemplateCreateRequest request) => host.Create<KpiTemplateCreateRequest, KpiTemplateDto>(request);
        var valid = KpiFixtures.NewTemplate(category.Id);

        ResultAssert.FailedOnField(await Create(valid with { Code = "bad code" }), "code");
        ResultAssert.FailedOnField(await Create(valid with { Name = " " }), "name");
        ResultAssert.FailedOnField(await Create(valid with { Name = new string('n', 151) }), "name");
        ResultAssert.FailedOnField(await Create(valid with { Description = new string('d', 501) }), "description");
        ResultAssert.FailedOnField(await Create(valid with { Unit = new string('u', 21) }), "unit");
        ResultAssert.FailedOnField(await Create(valid with { MeasurementType = "Distance" }), "measurementType");
        ResultAssert.FailedOnField(await Create(valid with { Direction = "Up" }), "direction");
        ResultAssert.FailedOnField(await Create(valid with { ScoringMethod = "Magic" }), "scoringMethod");
        ResultAssert.FailedOnField(await Create(valid with { Frequency = "Weekly" }), "frequency");
        ResultAssert.FailedOnField(await Create(valid with { DefaultTarget = 1.23456m }), "defaultTarget");
        ResultAssert.FailedOnField(await Create(valid with { DefaultWeight = 10.123m }), "defaultWeight");
        ResultAssert.FailedOnField(await Create(valid with { DefaultWeight = 0m }), "defaultWeight");
        ResultAssert.FailedOnField(await Create(valid with { DefaultWeight = 101m }), "defaultWeight");
        ResultAssert.FailedOnField(await Create(valid with { Status = "Draft" }), "status");
    }

    [Fact]
    public async Task The_scoring_rule_must_be_consistent_and_the_error_is_on_the_request_field()
    {
        var category = await host.CreateCategoryAsync();
        Task<Result<KpiTemplateDto>> Create(KpiTemplateCreateRequest request) => host.Create<KpiTemplateCreateRequest, KpiTemplateDto>(request);

        // Lower is better needs a cap (the database CK_Template_LowerCapped).
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewTemplate(category.Id, "T1", direction: "Lower Is Better", scoring: "Capped Percentage", cap: null)), "maxAchievementPct");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewTemplate(category.Id, "T2", scoring: "Capped Percentage", cap: null)), "maxAchievementPct");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewTemplate(category.Id, "T3", cap: 120m)), "maxAchievementPct");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewTemplate(category.Id, "T4", scoring: "Capped Percentage", cap: 501m)), "maxAchievementPct");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewTemplate(category.Id, "T5", target: 0m)), "defaultTarget");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewTemplate(category.Id, "T6", measurement: "Rating", scoring: "Rating Based", maxRating: null)), "maxRating");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewTemplate(category.Id, "T7", maxRating: 5m)), "maxRating");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewTemplate(category.Id, "T8", direction: "Range Based", scoring: "Range Based", rangeMin: 100m, rangeMax: 90m)), "rangeMax");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewTemplate(category.Id, "T9", rangeMin: 1m)), "rangeMin");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewTemplate(category.Id, "T10", measurement: "Boolean")), "scoringMethod");

        Assert.True((await Create(KpiFixtures.NewTemplate(category.Id, "OK1", direction: "Lower Is Better", scoring: "Capped Percentage", target: 0m, cap: 120m))).IsSuccess);
        Assert.True((await Create(KpiFixtures.NewTemplate(category.Id, "OK2", measurement: "Rating", scoring: "Rating Based", target: 5m, maxRating: 5m))).IsSuccess);
        Assert.True((await Create(KpiFixtures.NewTemplate(category.Id, "OK3", direction: "Range Based", scoring: "Range Based", rangeMin: 90m, rangeMax: 110m))).IsSuccess);
        Assert.True((await Create(KpiFixtures.NewTemplate(category.Id, "OK4", measurement: "Boolean", scoring: "Boolean", target: 1m))).IsSuccess);
    }

    [Fact]
    public async Task The_template_code_must_be_unique_and_the_category_must_exist_and_be_active()
    {
        var category = await host.CreateCategoryAsync();
        var inactive = await host.CreateCategoryAsync("OLD", "Retired", status: "Inactive");
        await host.CreateTemplateAsync(category.Id, "SALES-01");
        Task<Result<KpiTemplateDto>> Create(KpiTemplateCreateRequest request) => host.Create<KpiTemplateCreateRequest, KpiTemplateDto>(request);

        ResultAssert.FailedOnField(await Create(KpiFixtures.NewTemplate(category.Id, "SALES-01", "Other")), "code", "KPI code \"SALES-01\" already exists.");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewTemplate(9999, "NEW-01")), "categoryId");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewTemplate(inactive.Id, "NEW-02")), "categoryId");
    }

    [Fact]
    public async Task A_template_department_is_optional_but_must_exist_when_given()
    {
        var category = await host.CreateCategoryAsync();
        var org = await host.SeedOrgAsync();
        Task<Result<KpiTemplateDto>> Create(KpiTemplateCreateRequest request) => host.Create<KpiTemplateCreateRequest, KpiTemplateDto>(request);

        var global = ResultAssert.Succeeded(await Create(KpiFixtures.NewTemplate(category.Id, "ALL-01")));
        var scoped = ResultAssert.Succeeded(await Create(KpiFixtures.NewTemplate(category.Id, "ENG-01", departmentId: org.Department.Id)));

        Assert.Null(global.DepartmentId);
        Assert.Equal(org.Department.Id, scoped.DepartmentId);
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewTemplate(category.Id, "ENG-02", departmentId: 9999)), "departmentId");
    }

    [Fact]
    public async Task A_template_can_keep_an_inactive_category_but_not_move_to_one()
    {
        var category = await host.CreateCategoryAsync();
        var other = await host.CreateCategoryAsync("QUAL", "Quality");
        var template = await host.CreateTemplateAsync(category.Id);
        ResultAssert.Succeeded(await host.Command<DeactivateCommand, KpiCategoryDto>(new DeactivateCommand(category.Id, Requests.Version(category.RowVersion))));
        var otherInactive = ResultAssert.Succeeded(await host.Command<DeactivateCommand, KpiCategoryDto>(new DeactivateCommand(other.Id, Requests.Version(other.RowVersion))));

        var renamed = await host.Update<KpiTemplateUpdateRequest, KpiTemplateDto>(template.Id, KpiFixtures.TemplateUpdateOf(template) with { Name = "Renamed" });
        var moved = await host.Update<KpiTemplateUpdateRequest, KpiTemplateDto>(template.Id, KpiFixtures.TemplateUpdateOf(template) with { CategoryId = otherInactive.Id });

        Assert.True(renamed.IsSuccess);
        Assert.Equal("Renamed", renamed.Value.Name);
        Assert.False(moved.IsSuccess);
    }

    [Fact]
    public async Task A_template_update_applies_every_field_and_keeps_the_code_unique()
    {
        var category = await host.CreateCategoryAsync();
        var template = await host.CreateTemplateAsync(category.Id, "SALES-01");
        await host.CreateTemplateAsync(category.Id, "SALES-02", "Quarterly sales");

        var changed = ResultAssert.Succeeded(await host.Update<KpiTemplateUpdateRequest, KpiTemplateDto>(
            template.Id,
            KpiFixtures.TemplateUpdateOf(template) with { DefaultTarget = 250m, DefaultWeight = 35m, Frequency = "Quarterly" }));
        var duplicate = await host.Update<KpiTemplateUpdateRequest, KpiTemplateDto>(
            template.Id,
            KpiFixtures.TemplateUpdateOf(changed) with { Code = "SALES-02" });

        Assert.Equal(250m, changed.DefaultTarget);
        Assert.Equal(35m, changed.DefaultWeight);
        Assert.Equal("Quarterly", changed.Frequency);
        ResultAssert.FailedOnField(duplicate, "code");
    }

    [Fact]
    public async Task Editing_a_template_never_changes_the_scorecard_items_assigned_from_it()
    {
        var category = await host.CreateCategoryAsync();
        var template = await host.CreateTemplateAsync(category.Id);
        var item = ItemOf(template);
        host.Store(item);

        ResultAssert.Succeeded(await host.Update<KpiTemplateUpdateRequest, KpiTemplateDto>(
            template.Id,
            KpiFixtures.TemplateUpdateOf(template) with { Name = "Renamed", DefaultTarget = 999m, DefaultWeight = 80m }));

        var stored = host.Repository<ScorecardItem>().Items.Single();
        Assert.Same(item, stored);
        Assert.Equal("Monthly sales", stored.KpiName);
        Assert.Equal(100m, stored.Target);
        Assert.Equal(20m, stored.Weight);
    }

    [Fact]
    public async Task A_template_used_by_an_assigned_kpi_cannot_be_deleted_and_an_unused_one_can()
    {
        var category = await host.CreateCategoryAsync();
        var used = await host.CreateTemplateAsync(category.Id, "SALES-01");
        var unused = await host.CreateTemplateAsync(category.Id, "SALES-02", "Quarterly sales");
        host.Store(ItemOf(used));

        var blocked = await host.Delete<KpiTemplateDto>(used.Id, used.RowVersion);
        var deleted = await host.Delete<KpiTemplateDto>(unused.Id, unused.RowVersion);

        ResultAssert.Failed(blocked, ErrorType.Conflict, "\"Monthly sales\" cannot be deleted because it is used by 1 assigned KPI(s). Deactivate it instead.");
        Assert.True(deleted.IsSuccess);
    }

    [Fact]
    public async Task A_department_with_templates_cannot_be_deleted()
    {
        var category = await host.CreateCategoryAsync();
        var org = await host.SeedOrgAsync();
        await host.CreateTemplateAsync(category.Id, "ENG-01", departmentId: org.Department.Id);

        // The position is removed first so the template is the only thing left that points at the department.
        var positionDelete = await host.Delete<Apex.Hrms.Application.Organization.Positions.PositionDto>(org.Position.Id, org.Position.RowVersion);
        var result = await host.Delete<Apex.Hrms.Application.Organization.Departments.DepartmentDto>(org.Department.Id, org.Department.RowVersion);

        Assert.True(positionDelete.IsSuccess);
        ResultAssert.Failed(result, ErrorType.Conflict, "\"Engineering\" cannot be deleted because it is used by 1 KPI template(s). Deactivate it instead.");
    }

    [Fact]
    public async Task Template_changes_are_logged_under_performance()
    {
        var category = await host.CreateCategoryAsync();
        var template = await host.CreateTemplateAsync(category.Id);
        var changed = ResultAssert.Succeeded(await host.Update<KpiTemplateUpdateRequest, KpiTemplateDto>(template.Id, KpiFixtures.TemplateUpdateOf(template) with { Description = "x" }));
        var inactive = ResultAssert.Succeeded(await host.Command<DeactivateCommand, KpiTemplateDto>(new DeactivateCommand(template.Id, Requests.Version(changed.RowVersion))));
        await host.Delete<KpiTemplateDto>(template.Id, inactive.RowVersion);

        Assert.Equal(
            [
                "KPI category \"Productivity\" created",
                "KPI template \"Monthly sales\" created",
                "KPI template \"Monthly sales\" updated",
                "KPI template \"Monthly sales\" updated",
                "KPI template \"Monthly sales\" deleted",
            ],
            host.ActivityLog.Messages);
        Assert.All(host.ActivityLog.Entries, entry => Assert.Equal(ActivityCategories.Performance, entry.Category));
    }

    [Fact]
    public async Task Templates_are_filtered_by_search_category_department_status_and_scoring_method()
    {
        var productivity = await host.CreateCategoryAsync();
        var quality = await host.CreateCategoryAsync("QUAL", "Quality");
        var org = await host.SeedOrgAsync();
        var sales = await host.CreateTemplateAsync(productivity.Id, "SALES-01", "Monthly sales", org.Department.Id);
        await host.CreateTemplateAsync(quality.Id, "BUGS-01", "Open defects");
        var capped = ResultAssert.Succeeded(await host.Create<KpiTemplateCreateRequest, KpiTemplateDto>(
            KpiFixtures.NewTemplate(quality.Id, "CSAT-01", "Customer score", scoring: "Capped Percentage", cap: 120m, status: "Inactive")));
        var query = host.Get<IQueryHandler<KpiTemplateDto, KpiTemplateFilter>>();

        async Task<string[]> Codes(string? search = null, KpiTemplateFilter? filter = null) =>
            [.. ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<KpiTemplateFilter>(0, 50, new SortState("code", "asc"), search, filter ?? new KpiTemplateFilter()), default)).Items.Select(t => t.Code)];

        Assert.Equal(["BUGS-01", "CSAT-01", "SALES-01"], await Codes());
        Assert.Equal(["SALES-01"], await Codes("sales"));
        Assert.Equal(["BUGS-01"], await Codes("BUGS"));
        Assert.Equal(["BUGS-01", "CSAT-01"], await Codes(filter: new KpiTemplateFilter(CategoryId: quality.Id)));
        Assert.Equal(["SALES-01"], await Codes(filter: new KpiTemplateFilter(DepartmentId: org.Department.Id)));
        Assert.Equal(["CSAT-01"], await Codes(filter: new KpiTemplateFilter(Status: "Inactive")));
        Assert.Equal(["CSAT-01"], await Codes(filter: new KpiTemplateFilter(ScoringMethod: "Capped Percentage")));
        Assert.Equal(["BUGS-01", "SALES-01"], await Codes(filter: new KpiTemplateFilter(ScoringMethod: "Direct Percentage")));
        Assert.Empty(await Codes(filter: new KpiTemplateFilter(ScoringMethod: "Nonsense")));
        Assert.Equal(["BUGS-01", "CSAT-01", "SALES-01"], await Codes(filter: new KpiTemplateFilter(Status: "all", ScoringMethod: "all")));
        Assert.NotNull(sales);
        Assert.NotNull(capped);
    }

    [Fact]
    public async Task Templates_sort_by_a_known_field_and_refuse_an_unknown_one()
    {
        var category = await host.CreateCategoryAsync();
        await host.CreateTemplateAsync(category.Id);
        var query = host.Get<IQueryHandler<KpiTemplateDto, KpiTemplateFilter>>();

        var known = await query.HandleAsync(new PageRequest<KpiTemplateFilter>(0, 10, new SortState("defaultWeight", "desc"), null, new KpiTemplateFilter()), default);
        var unknown = await query.HandleAsync(new PageRequest<KpiTemplateFilter>(0, 10, new SortState("rowVersion", "asc"), null, new KpiTemplateFilter()), default);

        Assert.True(known.IsSuccess);
        ResultAssert.FailedOnField(unknown, "sort");
    }

    // ---- Rating bands ----

    [Fact]
    public async Task A_rating_band_is_created_with_its_fields()
    {
        var band = ResultAssert.Succeeded(await host.Create<RatingBandCreateRequest, RatingBandDto>(
            KpiFixtures.NewBand("  Exceeds  ", 90.5m) with { Description = " Strong " }));

        Assert.Equal("Exceeds", band.Label);
        Assert.Equal(90.5m, band.MinScore);
        Assert.Equal("Strong", band.Description);
        Assert.Equal("Active", band.Status);
    }

    [Fact]
    public async Task A_rating_band_has_a_unique_label_and_minimum_score_within_range_and_two_decimals()
    {
        await host.CreateBandAsync("Meets", 70m);
        Task<Result<RatingBandDto>> Create(RatingBandCreateRequest request) => host.Create<RatingBandCreateRequest, RatingBandDto>(request);

        ResultAssert.FailedOnField(await Create(KpiFixtures.NewBand("Meets", 80m)), "label", "Rating label \"Meets\" already exists.");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewBand("Other", 70m)), "minScore", "Minimum score \"70\" already exists.");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewBand("Other", -1m)), "minScore");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewBand("Other", 500.01m)), "minScore");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewBand("Other", 70.123m)), "minScore");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewBand(new string('l', 61), 10m)), "label");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewBand(" ", 10m)), "label");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewBand("Other", 10m) with { Description = new string('d', 301) }), "description");
        Assert.True((await Create(KpiFixtures.NewBand("Zero", 0m))).IsSuccess);
        Assert.True((await Create(KpiFixtures.NewBand("Top", 500m))).IsSuccess);
    }

    [Fact]
    public async Task A_rating_band_can_be_updated_deactivated_activated_and_deleted_and_is_logged()
    {
        var band = await host.CreateBandAsync();

        var changed = ResultAssert.Succeeded(await host.Update<RatingBandUpdateRequest, RatingBandDto>(band.Id, KpiFixtures.BandUpdateOf(band) with { MinScore = 72m }));
        var inactive = ResultAssert.Succeeded(await host.Command<DeactivateCommand, RatingBandDto>(new DeactivateCommand(band.Id, Requests.Version(changed.RowVersion))));
        var active = ResultAssert.Succeeded(await host.Command<ActivateCommand, RatingBandDto>(new ActivateCommand(band.Id, Requests.Version(inactive.RowVersion))));
        var deleted = await host.Delete<RatingBandDto>(band.Id, active.RowVersion);

        Assert.Equal(72m, changed.MinScore);
        Assert.Equal("Inactive", inactive.Status);
        Assert.Equal("Active", active.Status);
        Assert.True(deleted.IsSuccess);
        Assert.Equal(
            [
                "Rating band \"Meets Expectations\" created",
                "Rating band \"Meets Expectations\" updated",
                "Rating band \"Meets Expectations\" updated",
                "Rating band \"Meets Expectations\" updated",
                "Rating band \"Meets Expectations\" deleted",
            ],
            host.ActivityLog.Messages);
        Assert.All(host.ActivityLog.Entries, entry => Assert.Equal(ActivityCategories.Performance, entry.Category));
    }

    [Fact]
    public async Task A_rating_band_keeps_its_own_label_and_minimum_on_update_and_a_stale_version_is_a_conflict()
    {
        var band = await host.CreateBandAsync();
        var other = await host.CreateBandAsync("Exceeds", 90m);

        var same = await host.Update<RatingBandUpdateRequest, RatingBandDto>(band.Id, KpiFixtures.BandUpdateOf(band) with { Description = "x" });
        var clash = await host.Update<RatingBandUpdateRequest, RatingBandDto>(other.Id, KpiFixtures.BandUpdateOf(other) with { MinScore = 70m });
        var stale = await host.Update<RatingBandUpdateRequest, RatingBandDto>(band.Id, KpiFixtures.BandUpdateOf(band));

        Assert.True(same.IsSuccess);
        ResultAssert.FailedOnField(clash, "minScore");
        ResultAssert.Failed(stale, ErrorType.Conflict);
    }

    [Fact]
    public async Task Rating_bands_are_listed_by_minimum_score_and_filtered_by_status()
    {
        await host.CreateBandAsync("Meets", 70m);
        await host.CreateBandAsync("Exceeds", 90m);
        var low = ResultAssert.Succeeded(await host.Create<RatingBandCreateRequest, RatingBandDto>(KpiFixtures.NewBand("Below", 0m, "Inactive")));
        var query = host.Get<IQueryHandler<RatingBandDto, RatingBandFilter>>();

        var all = ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<RatingBandFilter>(0, 50, new SortState("minScore", "asc"), null, new RatingBandFilter()), default));
        var inactive = ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<RatingBandFilter>(0, 50, null, null, new RatingBandFilter("Inactive")), default));
        var search = ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<RatingBandFilter>(0, 50, null, "Exce", new RatingBandFilter()), default));

        Assert.Equal(["Below", "Meets", "Exceeds"], all.Items.Select(b => b.Label));
        Assert.Equal([low.Id], inactive.Items.Select(b => b.Id));
        Assert.Equal(["Exceeds"], search.Items.Select(b => b.Label));
    }
}
