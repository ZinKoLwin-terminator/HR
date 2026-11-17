using Apex.Hrms.Application;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Performance.Categories;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Application.Performance.RatingBands;
using Apex.Hrms.Application.Performance.Templates;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;
using Apex.Hrms.IntegrationTests.Support;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The KPI master data and the scorecard tables against SQL Server: the handlers with the real EF mapping, the unique indexes,
/// the CHECK constraints of the kpi schema, the rowversion, the delete protection and the temporal history. Fictional data
/// only; every row carries the tag of its test.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class KpiTests : DatabaseTest
{
    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    private Task<Result<EvaluationPeriodDto>> Move<TCommand>(EvaluationPeriodDto period)
        where TCommand : IEntityCommand<TCommand> =>
        RunCommand<TCommand, EvaluationPeriodDto>(TCommand.From(period.Id, Requests.Version(period.RowVersion)));

    /// <summary>The name of the CHECK constraint a statement violates, or null when it is accepted.</summary>
    private static async Task<string?> ViolatedCheckAsync(string sql, params (string Name, object? Value)[] parameters)
    {
        try
        {
            await Sql.ExecuteAsync(sql, parameters);
            return null;
        }
        catch (SqlException exception) when (exception.Number == 547)
        {
            var start = exception.Message.IndexOf("CK_", StringComparison.Ordinal);
            return start < 0 ? "?" : exception.Message[start..exception.Message.IndexOf('"', start)];
        }
    }

    private static void SetPrivate(object target, string property, object? value) =>
        target.GetType().GetProperty(property)!.DeclaringType!.GetProperty(property)!.SetValue(target, value);

    // ---- Categories ----

    [IntegrationFact]
    public async Task A_category_is_saved_with_its_audit_stamps_and_a_new_row_version_on_update()
    {
        var category = await SeedKpiCategoryAsync();

        var changed = Succeeded(await Update(category.Id, Requests.KpiCategoryUpdate(category, "Output and quality")));

        Assert.Equal("Output and quality", changed.Description);
        Assert.NotEqual(category.RowVersion, changed.RowVersion);
        Assert.Equal(changed.RowVersion, await Sql.ScalarAsync<byte[]>("SELECT RowVersion FROM kpi.Category WHERE Id = @id", ("@id", category.Id)));
        Assert.Equal(TestAdministrator.AccountId, await Sql.ScalarAsync<int>("SELECT CreatedByUserId FROM kpi.Category WHERE Id = @id", ("@id", category.Id)));
        Assert.Equal(TestAdministrator.AccountId, await Sql.ScalarAsync<int>("SELECT ModifiedByUserId FROM kpi.Category WHERE Id = @id", ("@id", category.Id)));
        Assert.Equal("Active", await Sql.ScalarAsync<string>("SELECT Status FROM kpi.Category WHERE Id = @id", ("@id", category.Id)));
    }

    [IntegrationFact]
    public async Task A_duplicate_category_code_or_name_is_a_validation_error_and_so_is_a_code_taken_between_check_and_save()
    {
        await SeedKpiCategoryAsync("PROD", "Productivity");

        FailedOnField(await Create(Requests.KpiCategory("PROD", "Other")), "code");
        FailedOnField(await Create(Requests.KpiCategory("QUAL", "Productivity")), "name");
        FailedOnField(await Create(Requests.KpiCategory("QUAL", "PRODUCTIVITY")), "name");

        BeforeNextSave = () => Sql.ExecuteAsync(
            "INSERT INTO kpi.Category (Code, Name) VALUES (@code, @name);",
            ("@code", Code("RACE")),
            ("@name", Name("Raw winner")));
        var raced = await Create(Requests.KpiCategory("RACE", "Quality"));

        Assert.Equal("Code must be unique.", FailedOnField(raced, "code"));
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM kpi.Category WHERE Code = @code", ("@code", Code("RACE"))));
    }

    [IntegrationFact]
    public async Task A_category_code_that_bypasses_the_domain_and_breaks_the_code_check_is_a_validation_error_on_code()
    {
        var category = await SeedKpiCategoryAsync();

        var result = await InScopeAsync(async services =>
        {
            var tracked = await services.GetRequiredService<IRepository<KpiCategory>>().FindAsync(category.Id, default);
            SetPrivate(tracked!, nameof(KpiCategory.Code), "TST bad");
            return await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);
        });

        Assert.Equal("This value is not allowed.", FailedOnField(result, "code"));
    }

    [IntegrationFact]
    public async Task A_category_can_be_deactivated_activated_and_a_stale_row_version_is_a_conflict()
    {
        var category = await SeedKpiCategoryAsync();

        var inactive = Succeeded(await Deactivate<KpiCategoryDto>(category.Id, Requests.Version(category.RowVersion)));
        var stale = await Activate<KpiCategoryDto>(category.Id, Requests.Version(category.RowVersion));
        var active = Succeeded(await Activate<KpiCategoryDto>(category.Id, Requests.Version(inactive.RowVersion)));

        Assert.Equal("Inactive", inactive.Status);
        Assert.Equal("Active", active.Status);
        Assert.Equal(RowVersionToken.Stale, Failed(stale, ErrorType.Conflict));
        Assert.Equal("Active", await Sql.ScalarAsync<string>("SELECT Status FROM kpi.Category WHERE Id = @id", ("@id", category.Id)));
    }

    [IntegrationFact]
    public async Task A_category_used_by_templates_cannot_be_deleted_and_an_unused_one_can()
    {
        var used = await SeedKpiCategoryAsync("USED", "Used");
        var unused = await SeedKpiCategoryAsync("FREE", "Free");
        await SeedKpiTemplateAsync(used.Id, "T1", "First");
        await SeedKpiTemplateAsync(used.Id, "T2", "Second");

        var blocked = await Delete<KpiCategoryDto>(used.Id, Requests.Version(used.RowVersion));
        var deleted = await Delete<KpiCategoryDto>(unused.Id, Requests.Version(unused.RowVersion));

        Assert.Equal(
            $"\"{Name("Used")}\" cannot be deleted because it is used by 2 template(s). Deactivate it instead.",
            Failed(blocked, ErrorType.Conflict).Message);
        Assert.True(deleted.IsSuccess);
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM kpi.Category WHERE Id = @id", ("@id", unused.Id)));
    }

    // ---- Templates ----

    [IntegrationFact]
    public async Task A_template_is_saved_with_every_column_and_its_decimals()
    {
        var category = await SeedKpiCategoryAsync();
        var org = await SeedOrganizationAsync();
        var request = Requests.KpiTemplate(
            category.Id,
            "RANGE-1",
            "Response time",
            org.Department.Id,
            direction: "Range Based",
            scoring: "Range Based",
            target: 100.1234m,
            rangeMin: 90.5m,
            rangeMax: 110.25m,
            weight: 12.34m) with { Unit = "ms", Description = "Median response" };

        var created = Succeeded(await Create(request));

        Assert.Equal("Range Based", created.Direction);
        Assert.Equal("Range Based", created.ScoringMethod);
        Assert.Equal(100.1234m, created.DefaultTarget);
        Assert.Equal(org.Department.Id, created.DepartmentId);
        Assert.Equal(8, created.RowVersion.Length);
        Assert.Equal(100.1234m, await Sql.ScalarAsync<decimal>("SELECT DefaultTarget FROM kpi.Template WHERE Id = @id", ("@id", created.Id)));
        Assert.Equal(90.5m, await Sql.ScalarAsync<decimal>("SELECT RangeMin FROM kpi.Template WHERE Id = @id", ("@id", created.Id)));
        Assert.Equal(110.25m, await Sql.ScalarAsync<decimal>("SELECT RangeMax FROM kpi.Template WHERE Id = @id", ("@id", created.Id)));
        Assert.Equal(12.34m, await Sql.ScalarAsync<decimal>("SELECT DefaultWeight FROM kpi.Template WHERE Id = @id", ("@id", created.Id)));
        Assert.Equal("Range Based", await Sql.ScalarAsync<string>("SELECT ScoringMethod FROM kpi.Template WHERE Id = @id", ("@id", created.Id)));
        Assert.Equal("Monthly", await Sql.ScalarAsync<string>("SELECT Frequency FROM kpi.Template WHERE Id = @id", ("@id", created.Id)));
        Assert.Equal("ms", await Sql.ScalarAsync<string>("SELECT Unit FROM kpi.Template WHERE Id = @id", ("@id", created.Id)));

        var loaded = Succeeded(await Get<KpiTemplateDto>(created.Id));
        Assert.Equal(created with { }, loaded with { RowVersion = created.RowVersion });
    }

    [IntegrationFact]
    public async Task A_template_needs_an_active_category_a_unique_code_and_an_existing_department()
    {
        var category = await SeedKpiCategoryAsync();
        var inactive = await SeedKpiCategoryAsync("OLD", "Retired");
        Succeeded(await Deactivate<KpiCategoryDto>(inactive.Id, Requests.Version(inactive.RowVersion)));
        await SeedKpiTemplateAsync(category.Id, "S-01");

        FailedOnField(await Create(Requests.KpiTemplate(category.Id, "S-01", "Other")), "code");
        FailedOnField(await Create(Requests.KpiTemplate(inactive.Id, "NEW-1")), "categoryId");
        FailedOnField(await Create(Requests.KpiTemplate(9999999, "NEW-2")), "categoryId");
        FailedOnField(await Create(Requests.KpiTemplate(category.Id, "NEW-3", departmentId: 9999999)), "departmentId");
    }

    [IntegrationFact]
    public async Task A_template_rule_that_is_inconsistent_is_refused_before_the_database_with_a_field_error()
    {
        var category = await SeedKpiCategoryAsync();

        FailedOnField(await Create(Requests.KpiTemplate(category.Id, "LOW", direction: "Lower Is Better", scoring: "Capped Percentage", target: 5m, cap: null)), "maxAchievementPct");
        FailedOnField(await Create(Requests.KpiTemplate(category.Id, "RAT", measurement: "Rating", scoring: "Rating Based", maxRating: null)), "maxRating");
        FailedOnField(await Create(Requests.KpiTemplate(category.Id, "RNG", direction: "Range Based", scoring: "Range Based", rangeMin: 100m, rangeMax: 100m)), "rangeMax");
        FailedOnField(await Create(Requests.KpiTemplate(category.Id, "ZERO", target: 0m)), "defaultTarget");
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM kpi.Template WHERE CategoryId = @id", ("@id", category.Id)));
        Assert.True((await Create(Requests.KpiTemplate(category.Id, "LOW-OK", direction: "Lower Is Better", scoring: "Capped Percentage", target: 0m, cap: 120m))).IsSuccess);
    }

    [IntegrationFact]
    public async Task A_lower_is_better_template_without_a_cap_that_bypasses_the_domain_is_a_validation_error_on_maxAchievementPct()
    {
        var category = await SeedKpiCategoryAsync();
        var template = await SeedKpiTemplateAsync(category.Id);

        var result = await InScopeAsync(async services =>
        {
            var tracked = await services.GetRequiredService<IRepository<KpiTemplate>>().FindAsync(template.Id, default);
            SetPrivate(tracked!, nameof(KpiTemplate.Direction), KpiDirection.LowerIsBetter);
            return await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);
        });

        // CK_Template_LowerCapped: a 400 with a field, not a 500.
        var error = Failed(result, ErrorType.Validation);
        Assert.Equal("A value is not allowed by a database rule.", error.Message);
        Assert.Equal("This value is not allowed.", Assert.Single(error.FieldErrors!["maxAchievementPct"]));
        Assert.Equal("Higher Is Better", await Sql.ScalarAsync<string>("SELECT Direction FROM kpi.Template WHERE Id = @id", ("@id", template.Id)));
    }

    [IntegrationFact]
    public async Task The_database_refuses_every_inconsistent_template_row_with_the_check_constraint_that_guards_it()
    {
        var category = await SeedKpiCategoryAsync();
        const string Insert =
            """
            INSERT INTO kpi.Template (Code, Name, CategoryId, MeasurementType, Direction, ScoringMethod, DefaultTarget, RangeMin, RangeMax, MaxRating, DefaultWeight, MaxAchievementPct, Frequency)
            VALUES (@code, N'Raw KPI', @category, @measurement, @direction, @scoring, @target, @rangeMin, @rangeMax, @maxRating, @weight, @cap, 'Monthly');
            """;

        async Task<string?> TryAsync(string suffix, string measurement, string direction, string scoring, decimal target, decimal? rangeMin, decimal? rangeMax, decimal? maxRating, decimal weight, decimal? cap, string? code = null) =>
            await ViolatedCheckAsync(
                Insert,
                ("@code", code ?? Code(suffix)),
                ("@category", category.Id),
                ("@measurement", measurement),
                ("@direction", direction),
                ("@scoring", scoring),
                ("@target", target),
                ("@rangeMin", rangeMin),
                ("@rangeMax", rangeMax),
                ("@maxRating", maxRating),
                ("@weight", weight),
                ("@cap", cap));

        Assert.Equal("CK_Template_LowerCapped", await TryAsync("A", "Number", "Lower Is Better", "Direct Percentage", 5m, null, null, null, 20m, null));
        Assert.Equal("CK_Template_Capped", await TryAsync("B", "Number", "Higher Is Better", "Capped Percentage", 100m, null, null, null, 20m, null));
        Assert.Equal("CK_Template_Uncapped", await TryAsync("C", "Number", "Higher Is Better", "Direct Percentage", 100m, null, null, null, 20m, 120m));
        Assert.Equal("CK_Template_Rating", await TryAsync("D", "Rating", "Higher Is Better", "Rating Based", 5m, null, null, null, 20m, null));
        Assert.Equal("CK_Template_Boolean", await TryAsync("E", "Number", "Higher Is Better", "Boolean", 1m, null, null, null, 20m, null));
        Assert.Equal("CK_Template_Range", await TryAsync("F", "Number", "Range Based", "Range Based", 100m, 100m, 90m, null, 20m, null));
        Assert.Equal("CK_Template_DefaultTarget", await TryAsync("G", "Number", "Higher Is Better", "Direct Percentage", 0m, null, null, null, 20m, null));
        Assert.Equal("CK_Template_DefaultWeight", await TryAsync("H", "Number", "Higher Is Better", "Direct Percentage", 100m, null, null, null, 0m, null));
        Assert.Equal("CK_Template_DefaultWeight", await TryAsync("I", "Number", "Higher Is Better", "Direct Percentage", 100m, null, null, null, 100.01m, null));
        Assert.Equal("CK_Template_MaxAchievementPct", await TryAsync("J", "Number", "Higher Is Better", "Capped Percentage", 100m, null, null, null, 20m, 501m));
        Assert.Equal("CK_Template_MeasurementType", await TryAsync("K", "Distance", "Higher Is Better", "Direct Percentage", 100m, null, null, null, 20m, null));
        Assert.Equal("CK_Template_Code", await TryAsync("L", "Number", "Higher Is Better", "Direct Percentage", 100m, null, null, null, 20m, null, code: "bad code"));
        Assert.Null(await TryAsync("OK", "Number", "Lower Is Better", "Capped Percentage", 0m, null, null, null, 20m, 120m));
    }

    [IntegrationFact]
    public async Task A_template_update_keeps_the_old_values_in_the_temporal_history_and_never_touches_assigned_items()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org);
        var category = await SeedKpiCategoryAsync();
        var template = await SeedKpiTemplateAsync(category.Id);
        var period = await SeedPeriodAsync(org.Company.Id);
        var scorecardId = await SaveScorecardAsync(employee.Id, period.Id, org);
        var itemId = await SaveItemAsync(scorecardId, template);
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM kpi.TemplateHistory WHERE Id = @id", ("@id", template.Id)));

        var changed = Succeeded(await Update(template.Id, Requests.KpiTemplateUpdate(template, name: Name("Renamed"), target: 250m, weight: 35m)));

        Assert.Equal(250m, changed.DefaultTarget);
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM kpi.TemplateHistory WHERE Id = @id", ("@id", template.Id)));
        Assert.Equal(100m, await Sql.ScalarAsync<decimal>("SELECT DefaultTarget FROM kpi.TemplateHistory WHERE Id = @id", ("@id", template.Id)));
        Assert.Equal(20m, await Sql.ScalarAsync<decimal>("SELECT DefaultWeight FROM kpi.TemplateHistory WHERE Id = @id", ("@id", template.Id)));
        Assert.Equal(Name("Monthly sales"), await Sql.ScalarAsync<string>("SELECT KpiName FROM kpi.ScorecardItem WHERE Id = @id", ("@id", itemId)));
        Assert.Equal(100m, await Sql.ScalarAsync<decimal>("SELECT [Target] FROM kpi.ScorecardItem WHERE Id = @id", ("@id", itemId)));
        Assert.Equal(20m, await Sql.ScalarAsync<decimal>("SELECT Weight FROM kpi.ScorecardItem WHERE Id = @id", ("@id", itemId)));
    }

    [IntegrationFact]
    public async Task A_stale_template_row_version_is_a_conflict_in_the_handler_and_in_the_database()
    {
        var category = await SeedKpiCategoryAsync();
        var template = await SeedKpiTemplateAsync(category.Id);
        Succeeded(await Update(template.Id, Requests.KpiTemplateUpdate(template, target: 150m)));

        var stale = await Update(template.Id, Requests.KpiTemplateUpdate(template, target: 175m));
        Assert.Equal(RowVersionToken.Stale, Failed(stale, ErrorType.Conflict));

        var current = Succeeded(await Get<KpiTemplateDto>(template.Id));
        BeforeNextSave = () => Sql.ExecuteAsync("UPDATE kpi.Template SET Unit = N'x' WHERE Id = @id", ("@id", template.Id));
        var raced = await Update(template.Id, Requests.KpiTemplateUpdate(current, target: 190m));

        Assert.Equal(RowVersionToken.Stale, Failed(raced, ErrorType.Conflict));
        Assert.Equal(150m, await Sql.ScalarAsync<decimal>("SELECT DefaultTarget FROM kpi.Template WHERE Id = @id", ("@id", template.Id)));
    }

    [IntegrationFact]
    public async Task A_template_used_by_an_assigned_kpi_or_a_department_with_templates_cannot_be_deleted()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org);
        var category = await SeedKpiCategoryAsync();
        var used = await SeedKpiTemplateAsync(category.Id, "USED", "Used KPI");
        var scoped = await SeedKpiTemplateAsync(category.Id, "ENG", "Engineering KPI", org.Department.Id);
        var period = await SeedPeriodAsync(org.Company.Id);
        var scorecardId = await SaveScorecardAsync(employee.Id, period.Id, org);
        await SaveItemAsync(scorecardId, used);

        var template = await Delete<KpiTemplateDto>(used.Id, Requests.Version(used.RowVersion));
        var categoryDelete = await Delete<KpiCategoryDto>(category.Id, Requests.Version(category.RowVersion));
        var department = await Delete<DepartmentDto>(org.Department.Id, Requests.Version(org.Department.RowVersion));

        Assert.Equal(
            $"\"{Name("Used KPI")}\" cannot be deleted because it is used by 1 assigned KPI(s). Deactivate it instead.",
            Failed(template, ErrorType.Conflict).Message);
        Assert.Contains("2 template(s)", Failed(categoryDelete, ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.Contains("1 assigned KPI(s)", Failed(categoryDelete, ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.Contains("1 KPI template(s)", Failed(department, ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.NotNull(scoped);
    }

    // ---- Periods ----

    [IntegrationFact]
    public async Task A_period_is_saved_in_draft_with_its_dates_and_flags()
    {
        var org = await SeedOrganizationAsync();

        var period = Succeeded(await Create(Requests.Period(org.Company.Id, "2026 Q4", hr: true)));

        Assert.Equal("Draft", period.Status);
        Assert.Equal("Draft", await Sql.ScalarAsync<string>("SELECT Status FROM kpi.EvaluationPeriod WHERE Id = @id", ("@id", period.Id)));
        Assert.Equal("Quarterly", await Sql.ScalarAsync<string>("SELECT PeriodType FROM kpi.EvaluationPeriod WHERE Id = @id", ("@id", period.Id)));
        Assert.Equal("2026-10-01", await Sql.ScalarAsync<string>("SELECT CONVERT(varchar(10), StartDate, 23) FROM kpi.EvaluationPeriod WHERE Id = @id", ("@id", period.Id)));
        Assert.Equal("2026-12-31", await Sql.ScalarAsync<string>("SELECT CONVERT(varchar(10), EndDate, 23) FROM kpi.EvaluationPeriod WHERE Id = @id", ("@id", period.Id)));
        Assert.Equal("2027-01-10", await Sql.ScalarAsync<string>("SELECT CONVERT(varchar(10), SubmissionDueDate, 23) FROM kpi.EvaluationPeriod WHERE Id = @id", ("@id", period.Id)));
        Assert.True(await Sql.ScalarAsync<bool>("SELECT RequiresHrReview FROM kpi.EvaluationPeriod WHERE Id = @id", ("@id", period.Id)));
    }

    [IntegrationFact]
    public async Task Periods_of_the_same_type_in_a_company_cannot_overlap_and_touching_dates_overlap_because_they_are_inclusive()
    {
        var org = await SeedOrganizationAsync();
        await SeedPeriodAsync(org.Company.Id, "2026 Q4", "Quarterly", "2026-10-01", "2026-12-31");

        var touchingEnd = await Create(Requests.Period(org.Company.Id, "Touching end", "Quarterly", "2026-12-31", "2027-03-31"));
        var touchingStart = await Create(Requests.Period(org.Company.Id, "Touching start", "Quarterly", "2026-07-01", "2026-10-01"));
        var inside = await Create(Requests.Period(org.Company.Id, "Inside", "Quarterly", "2026-11-01", "2026-11-30"));
        var adjacent = await Create(Requests.Period(org.Company.Id, "2027 Q1", "Quarterly", "2027-01-01", "2027-03-31"));
        var otherType = await Create(Requests.Period(org.Company.Id, "2026 Year", "Yearly", "2026-01-01", "2026-12-31"));

        Assert.Contains(Name("2026 Q4"), Failed(touchingEnd, ErrorType.Conflict).Message, StringComparison.Ordinal);
        Failed(touchingStart, ErrorType.Conflict);
        Failed(inside, ErrorType.Conflict);
        Assert.True(adjacent.IsSuccess);
        Assert.True(otherType.IsSuccess);
    }

    [IntegrationFact]
    public async Task The_same_dates_are_fine_in_another_company_and_a_period_name_is_unique_per_company()
    {
        var org = await SeedOrganizationAsync();
        var other = await SeedCompanyAsync("B", "Test Company B");
        await SeedPeriodAsync(org.Company.Id, "2026 Q4");

        var otherCompany = await Create(Requests.Period(other.Id, "2026 Q4"));
        var sameName = await Create(Requests.Period(org.Company.Id, "2026 Q4", "Custom", "2027-02-01", "2027-02-28", due: null));

        Assert.True(otherCompany.IsSuccess);
        Assert.Equal("Period name must be unique within the company.", FailedOnField(sameName, "name"));
    }

    [IntegrationFact]
    public async Task A_period_name_taken_between_the_check_and_the_save_is_a_validation_error_on_name()
    {
        var org = await SeedOrganizationAsync();
        BeforeNextSave = () => Sql.ExecuteAsync(
            "INSERT INTO kpi.EvaluationPeriod (CompanyId, Name, PeriodType, StartDate, EndDate) VALUES (@company, @name, 'Custom', '2030-01-01', '2030-01-31');",
            ("@company", org.Company.Id),
            ("@name", Name("2026 Q4")));

        var result = await Create(Requests.Period(org.Company.Id, "2026 Q4"));

        Assert.Equal("Period name must be unique within the company.", FailedOnField(result, "name"));
        Assert.Equal("A period with that name already exists in this company.", result.Error.Message);
    }

    [IntegrationFact]
    public async Task The_database_refuses_a_period_with_inverted_dates_or_a_due_date_before_the_start()
    {
        var org = await SeedOrganizationAsync();
        const string Insert =
            "INSERT INTO kpi.EvaluationPeriod (CompanyId, Name, PeriodType, StartDate, EndDate, SubmissionDueDate) VALUES (@company, @name, 'Custom', @start, @end, @due);";

        Task<string?> TryAsync(string name, string start, string end, string? due) => ViolatedCheckAsync(
            Insert,
            ("@company", org.Company.Id),
            ("@name", Name(name)),
            ("@start", start),
            ("@end", end),
            ("@due", due));

        Assert.Equal("CK_EvaluationPeriod_DateOrder", await TryAsync("Same day", "2026-10-01", "2026-10-01", null));
        Assert.Equal("CK_EvaluationPeriod_DateOrder", await TryAsync("Inverted", "2026-10-02", "2026-10-01", null));
        Assert.Equal("CK_EvaluationPeriod_SubmissionDueDate", await TryAsync("Early due", "2026-10-01", "2026-10-31", "2026-09-30"));
        Assert.Null(await TryAsync("Fine", "2026-10-01", "2026-10-31", "2026-10-01"));
    }

    [IntegrationFact]
    public async Task A_period_goes_through_activate_complete_and_close_and_a_move_out_of_order_is_a_conflict()
    {
        var org = await SeedOrganizationAsync();
        var draft = await SeedPeriodAsync(org.Company.Id);

        var early = await Move<CompletePeriodCommand>(draft);
        var active = Succeeded(await Move<ActivatePeriodCommand>(draft));
        var again = await Move<ActivatePeriodCommand>(active);
        var completed = Succeeded(await Move<CompletePeriodCommand>(active));
        var closed = Succeeded(await Move<ClosePeriodCommand>(completed));

        Failed(early, ErrorType.Conflict);
        Failed(again, ErrorType.Conflict);
        Assert.Equal(["Active", "Completed", "Closed"], [active.Status, completed.Status, closed.Status]);
        Assert.Equal("Closed", await Sql.ScalarAsync<string>("SELECT Status FROM kpi.EvaluationPeriod WHERE Id = @id", ("@id", draft.Id)));
        Assert.Equal(closed.RowVersion, await Sql.ScalarAsync<byte[]>("SELECT RowVersion FROM kpi.EvaluationPeriod WHERE Id = @id", ("@id", draft.Id)));
        Assert.True(await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM kpi.EvaluationPeriodHistory WHERE Id = @id", ("@id", draft.Id)) >= 3);
    }

    [IntegrationFact]
    public async Task A_stale_row_version_on_a_period_command_is_a_conflict_and_changes_nothing()
    {
        var org = await SeedOrganizationAsync();
        var draft = await SeedPeriodAsync(org.Company.Id);
        Succeeded(await Move<ActivatePeriodCommand>(draft));

        var stale = await Move<CompletePeriodCommand>(draft);

        Assert.Equal(RowVersionToken.Stale, Failed(stale, ErrorType.Conflict));
        Assert.Equal("Active", await Sql.ScalarAsync<string>("SELECT Status FROM kpi.EvaluationPeriod WHERE Id = @id", ("@id", draft.Id)));
    }

    [IntegrationFact]
    public async Task Edits_depend_on_the_status_draft_all_active_name_and_due_date_only_completed_and_closed_none()
    {
        var org = await SeedOrganizationAsync();
        var draft = await SeedPeriodAsync(org.Company.Id);

        var draftEdit = Succeeded(await Update(draft.Id, Requests.PeriodUpdate(draft, end: D(2026, 12, 30))));
        var active = Succeeded(await Move<ActivatePeriodCommand>(draftEdit));
        var nameAndDue = Succeeded(await Update(active.Id, Requests.PeriodUpdate(active, Name("2026 Q4 final"), due: D(2027, 1, 31))));
        var locked = await Update(active.Id, Requests.PeriodUpdate(nameAndDue, end: D(2026, 12, 29)));
        var completed = Succeeded(await Move<CompletePeriodCommand>(nameAndDue));
        var frozen = await Update(completed.Id, Requests.PeriodUpdate(completed, Name("Nope")));

        Assert.Equal(D(2026, 12, 30), draftEdit.EndDate);
        Assert.Equal(Name("2026 Q4 final"), nameAndDue.Name);
        Assert.Equal(D(2027, 1, 31), nameAndDue.SubmissionDueDate);
        Assert.Contains("only its name and submission due date", Failed(locked, ErrorType.Conflict).Message, StringComparison.Ordinal);
        Failed(frozen, ErrorType.Conflict);
        Assert.Equal(D(2026, 12, 30), DateOnly.FromDateTime(await Sql.ScalarAsync<DateTime>("SELECT EndDate FROM kpi.EvaluationPeriod WHERE Id = @id", ("@id", draft.Id))));
    }

    [IntegrationFact]
    public async Task Only_a_draft_period_without_scorecards_can_be_deleted()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org);
        var free = await SeedPeriodAsync(org.Company.Id, "Free", "Custom", "2026-01-01", "2026-01-31");
        var withScorecard = await SeedPeriodAsync(org.Company.Id, "Planned", "Custom", "2026-02-01", "2026-02-28");
        var active = await SeedPeriodAsync(org.Company.Id, "Running", "Custom", "2026-03-01", "2026-03-31");
        await SaveScorecardAsync(employee.Id, withScorecard.Id, org);
        var running = Succeeded(await Move<ActivatePeriodCommand>(active));

        var blockedByScorecard = await Delete<EvaluationPeriodDto>(withScorecard.Id, Requests.Version(withScorecard.RowVersion));
        var blockedByStatus = await Delete<EvaluationPeriodDto>(running.Id, Requests.Version(running.RowVersion));
        var deleted = await Delete<EvaluationPeriodDto>(free.Id, Requests.Version(free.RowVersion));
        var company = await Delete<CompanyDto>(org.Company.Id, Requests.Version(org.Company.RowVersion));

        Assert.Equal(
            $"\"{Name("Planned")}\" cannot be deleted because it is used by 1 scorecard(s).",
            Failed(blockedByScorecard, ErrorType.Conflict).Message);
        Assert.Contains("only a Draft period can be deleted", Failed(blockedByStatus, ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.True(deleted.IsSuccess);
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM kpi.EvaluationPeriod WHERE Id = @id", ("@id", free.Id)));
        Assert.Contains("evaluation period(s)", Failed(company, ErrorType.Conflict).Message, StringComparison.Ordinal);
    }

    [IntegrationFact]
    public async Task Periods_are_queried_by_company_type_status_year_and_search_and_the_picker_hides_drafts()
    {
        var org = await SeedOrganizationAsync();
        var q4 = await SeedPeriodAsync(org.Company.Id, "2026 Q4", "Quarterly", "2026-10-01", "2026-12-31");
        await SeedPeriodAsync(org.Company.Id, "2027 Q1", "Quarterly", "2027-01-01", "2027-03-31");
        await SeedPeriodAsync(org.Company.Id, "2026 Year", "Yearly", "2026-01-01", "2026-12-31");
        Succeeded(await Move<ActivatePeriodCommand>(q4));

        async Task<string[]> Names(EvaluationPeriodFilter filter, string? search = null) =>
            [.. Succeeded(await Query<EvaluationPeriodDto, EvaluationPeriodFilter>(new PageRequest<EvaluationPeriodFilter>(0, 50, new SortState("startDate", "asc"), search ?? Tag, filter))).Items.Select(p => p.Name)];

        Assert.Equal([Name("2026 Year"), Name("2026 Q4"), Name("2027 Q1")], await Names(new EvaluationPeriodFilter()));
        Assert.Equal([Name("2026 Q4"), Name("2027 Q1")], await Names(new EvaluationPeriodFilter(PeriodType: "Quarterly")));
        Assert.Equal([Name("2026 Q4")], await Names(new EvaluationPeriodFilter(Status: "Active")));
        Assert.Equal([Name("2027 Q1")], await Names(new EvaluationPeriodFilter(Year: 2027)));
        Assert.Equal([Name("2026 Year")], await Names(new EvaluationPeriodFilter(CompanyId: org.Company.Id), search: "Year"));

        var options = await InScopeAsync(sp => sp.GetRequiredService<IEvaluationPeriodOptionsHandler>().HandleAsync(org.Company.Id, default));
        Assert.Equal([q4.Id], Succeeded(options).Select(option => option.Id));
    }

    // ---- Rating bands ----

    [IntegrationFact]
    public async Task A_rating_band_is_saved_updated_deactivated_and_deleted()
    {
        var band = await SeedRatingBandAsync("Top", 410.5m);

        var changed = Succeeded(await Update(band.Id, Requests.RatingBandUpdate(band, 411.75m)));
        var inactive = Succeeded(await Deactivate<RatingBandDto>(band.Id, Requests.Version(changed.RowVersion)));
        var active = Succeeded(await Activate<RatingBandDto>(band.Id, Requests.Version(inactive.RowVersion)));
        var stale = await Delete<RatingBandDto>(band.Id, Requests.Version(band.RowVersion));
        var deleted = await Delete<RatingBandDto>(band.Id, Requests.Version(active.RowVersion));

        Assert.Equal(411.75m, changed.MinScore);
        Assert.Equal("Inactive", inactive.Status);
        Assert.Equal("Active", active.Status);
        Assert.Equal(RowVersionToken.Stale, Failed(stale, ErrorType.Conflict));
        Assert.True(deleted.IsSuccess);
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM kpi.RatingBand WHERE Id = @id", ("@id", band.Id)));
    }

    [IntegrationFact]
    public async Task A_rating_band_label_and_minimum_score_are_unique_and_the_score_has_two_decimals()
    {
        await SeedRatingBandAsync("Top", 410.5m);

        FailedOnField(await Create(Requests.RatingBand("Top", 420m)), "label");
        FailedOnField(await Create(Requests.RatingBand("Other", 410.5m)), "minScore");
        FailedOnField(await Create(Requests.RatingBand("Other", 410.555m)), "minScore");
        FailedOnField(await Create(Requests.RatingBand("Other", 500.01m)), "minScore");
        Assert.Equal(410.5m, await Sql.ScalarAsync<decimal>("SELECT MinScore FROM kpi.RatingBand WHERE Label = @label", ("@label", Name("Top"))));
        Assert.True((await Create(Requests.RatingBand("Cap", 500m))).IsSuccess);
    }

    [IntegrationFact]
    public async Task A_minimum_score_taken_between_the_check_and_the_save_is_a_validation_error_on_minScore()
    {
        BeforeNextSave = () => Sql.ExecuteAsync(
            "INSERT INTO kpi.RatingBand (Label, MinScore) VALUES (@label, 433.33);",
            ("@label", Name("Raw winner")));

        var result = await Create(Requests.RatingBand("Mine", 433.33m));

        Assert.Equal("Minimum score must be unique.", FailedOnField(result, "minScore"));
    }

    [IntegrationFact]
    public async Task A_minimum_score_outside_the_range_that_bypasses_the_domain_is_a_validation_error_on_minScore()
    {
        var band = await SeedRatingBandAsync();

        var result = await InScopeAsync(async services =>
        {
            var tracked = await services.GetRequiredService<IRepository<RatingBand>>().FindAsync(band.Id, default);
            SetPrivate(tracked!, nameof(RatingBand.MinScore), 600m);
            return await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);
        });

        Assert.Equal("This value is not allowed.", FailedOnField(result, "minScore"));
        Assert.Equal("CK_RatingBand_MinScore", await ViolatedCheckAsync("INSERT INTO kpi.RatingBand (Label, MinScore) VALUES (@label, 500.01);", ("@label", Name("Too high"))));
    }

    // ---- Activity ----

    [IntegrationFact]
    public async Task Changes_are_logged_under_the_performance_category()
    {
        var org = await SeedOrganizationAsync();
        var category = await SeedKpiCategoryAsync();
        var period = await SeedPeriodAsync(org.Company.Id);
        Succeeded(await Move<ActivatePeriodCommand>(period));
        await SeedRatingBandAsync();

        var messages = await Sql.ScalarAsync<int>(
            "SELECT COUNT(*) FROM app.ActivityLog WHERE Category = 'performance' AND ActorName LIKE @tag AND (Message LIKE @category OR Message LIKE @period OR Message LIKE @band)",
            ("@tag", Tag + "%"),
            ("@category", $"KPI category \"{Name("Productivity")}\" created"),
            ("@period", $"Evaluation period \"{Name("2026 Q4")}\" %"),
            ("@band", $"Rating band \"{Name("Test band")}\" created"));

        Assert.Equal(4, messages);
        Assert.NotNull(category);
    }

    // ---- The scorecard tables ----

    private async Task<int> SaveScorecardAsync(
        int employeeId,
        int periodId,
        (Apex.Hrms.Application.Organization.Companies.CompanyDto Company, Apex.Hrms.Application.Organization.Branches.BranchDto Branch, DepartmentDto Department, Apex.Hrms.Application.Organization.JobGrades.JobGradeDto Grade, PositionDto Position) org) =>
        await InScopeAsync(async services =>
        {
            var scorecard = Scorecard.Create(employeeId, periodId, org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, reviewerEmployeeId: null).Value;
            services.GetRequiredService<IRepository<Scorecard>>().Add(scorecard);
            Assert.True((await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default)).IsSuccess);
            return scorecard.Id;
        });

    private async Task<int> SaveItemAsync(int scorecardId, KpiTemplateDto template) =>
        await InScopeAsync(async services =>
        {
            var item = ScorecardItem.Create(new ScorecardItemDetails(
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
                D(2026, 10, 1),
                D(2026, 12, 31),
                SortOrder: 1,
                AssignedByUserId: TestAdministrator.AccountId,
                AssignedOn: D(2026, 11, 9))).Value;
            services.GetRequiredService<IRepository<ScorecardItem>>().Add(item);
            Assert.True((await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default)).IsSuccess);
            return item.Id;
        });

    [IntegrationFact]
    public async Task A_scorecard_with_an_item_an_attachment_and_review_history_is_saved_and_read_back_through_the_mapping()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org);
        var category = await SeedKpiCategoryAsync();
        var template = await SeedKpiTemplateAsync(category.Id);
        var period = await SeedPeriodAsync(org.Company.Id);

        var scorecardId = await SaveScorecardAsync(employee.Id, period.Id, org);
        var itemId = await SaveItemAsync(scorecardId, template);
        var key = Guid.NewGuid().ToString("N");
        var attachmentId = await InScopeAsync(async services =>
        {
            var attachment = KpiAttachment.Create(itemId, "evidence.pdf", "application/pdf", 2048, key, D(2026, 11, 9)).Value;
            services.GetRequiredService<IRepository<KpiAttachment>>().Add(attachment);
            Assert.True((await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default)).IsSuccess);
            return attachment.Id;
        });
        var actionId = await InScopeAsync(async services =>
        {
            var action = ReviewAction.Create(
                scorecardId,
                ReviewStep.Employee,
                ReviewActionType.FinalizePlan,
                ScorecardStatus.Planning,
                ScorecardStatus.Draft,
                TestAdministrator.AccountId,
                actorEmployeeId: null,
                $"{Tag} Test User",
                comment: null,
                overallScore: null,
                new DateTime(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc)).Value;
            services.GetRequiredService<IRepository<ReviewAction>>().Add(action);
            Assert.True((await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default)).IsSuccess);
            return action.Id;
        });

        var (scorecard, item, attachment, action) = await InScopeAsync(async services => (
            (await services.GetRequiredService<IRepository<Scorecard>>().FindAsync(scorecardId, default))!,
            (await services.GetRequiredService<IRepository<ScorecardItem>>().FindAsync(itemId, default))!,
            (await services.GetRequiredService<IRepository<KpiAttachment>>().FindAsync(attachmentId, default))!,
            (await services.GetRequiredService<IRepository<ReviewAction>>().FindAsync(actionId, default))!));
        Assert.Equal(ScorecardStatus.Planning, scorecard.Status);
        Assert.Equal((employee.Id, period.Id, org.Department.Id), (scorecard.EmployeeId, scorecard.PeriodId, scorecard.DepartmentId));
        Assert.Null(scorecard.OverallScore);
        Assert.Equal(8, scorecard.RowVersion.Length);
        Assert.Equal(TestAdministrator.AccountId, scorecard.CreatedByUserId);
        Assert.Equal(KpiScoringMethod.DirectPercentage, item.ScoringMethod);
        Assert.Equal((100m, 20m, 1), (item.Target, item.Weight, (int)item.SortOrder));
        Assert.Equal(D(2026, 11, 9), item.AssignedOn);
        Assert.Equal((key, 2048L), (attachment.StorageKey, attachment.SizeBytes));
        Assert.Equal((ReviewActionType.FinalizePlan, ScorecardStatus.Draft), (action.Action, action.ToStatus));
        Assert.Equal("Planning", await Sql.ScalarAsync<string>("SELECT Status FROM kpi.Scorecard WHERE Id = @id", ("@id", scorecardId)));
        Assert.Equal("Finalize Plan", await Sql.ScalarAsync<string>("SELECT Action FROM kpi.ReviewAction WHERE Id = @id", ("@id", actionId)));
        Assert.Equal(key, (await Sql.ScalarAsync<string>("SELECT StorageKey FROM kpi.Attachment WHERE Id = @id", ("@id", attachmentId))).Trim());
    }

    [IntegrationFact]
    public async Task One_scorecard_per_employee_and_period_and_one_assignment_of_a_template_per_scorecard_are_enforced_by_the_database()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org);
        var category = await SeedKpiCategoryAsync();
        var template = await SeedKpiTemplateAsync(category.Id);
        var period = await SeedPeriodAsync(org.Company.Id);
        var scorecardId = await SaveScorecardAsync(employee.Id, period.Id, org);
        await SaveItemAsync(scorecardId, template);
        var secondScorecard = await InScopeAsync(async services =>
        {
            services.GetRequiredService<IRepository<Scorecard>>().Add(Scorecard.Create(employee.Id, period.Id, org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, null).Value);
            return await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);
        });
        var secondItem = await InScopeAsync(async services =>
        {
            var item = ScorecardItem.Create(new ScorecardItemDetails(scorecardId, template.Id, category.Id, template.Code, template.Name, KpiMeasurementType.Number, null, KpiDirection.HigherIsBetter, KpiScoringMethod.DirectPercentage, 100m, null, null, null, 20m, null, D(2026, 10, 1), D(2026, 12, 31), 2, null, D(2026, 11, 9))).Value;
            services.GetRequiredService<IRepository<ScorecardItem>>().Add(item);
            return await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);
        });

        // The composite unique constraints have no single field, so each is a plain Conflict, and no second row exists.
        Failed(secondScorecard, ErrorType.Conflict);
        Failed(secondItem, ErrorType.Conflict);
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM kpi.Scorecard WHERE EmployeeId = @e AND PeriodId = @p", ("@e", employee.Id), ("@p", period.Id)));
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM kpi.ScorecardItem WHERE ScorecardId = @s", ("@s", scorecardId)));
    }
}
