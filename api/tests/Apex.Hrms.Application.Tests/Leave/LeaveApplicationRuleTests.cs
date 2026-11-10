using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Leave.Applications;
using Apex.Hrms.Application.Leave.LeaveTypes;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Leave;

/// <summary>
/// Creating and changing leave applications: the rules the mock leave service enforced (eligibility, joining date,
/// consecutive days, overlap, half days, working days) and the draft-only rules of update and delete. The fake clock
/// says today is Monday 2026-11-09, the first leave Monday is 2026-11-16.
/// </summary>
public sealed class LeaveApplicationRuleTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private async Task<(OrgSeed Org, EmployeeDto Employee, LeaveTypeDto Type)> SeedAsync(
        string gender = "Female",
        short maxConsecutive = 10,
        decimal days = 15m)
    {
        var org = await host.SeedOrgAsync();
        var employee = await host.CreateLeaveEmployeeAsync(org, "mia@example.test", gender);
        var type = await host.CreateTypeAsync(LeaveFixtures.NewType(maxConsecutive: maxConsecutive, days: days));
        return (org, employee, type);
    }

    private static DateOnly D(int month, int day) => new(2026, month, day);

    // ---- Creating ----

    [Fact]
    public async Task A_new_application_is_a_numbered_draft_with_the_working_days_counted()
    {
        var (_, employee, type) = await SeedAsync();

        // Monday 16 to Sunday 22 November: five working days.
        var created = await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), D(11, 22)));

        Assert.Equal("LV-2026-0001", created.ApplicationNo);
        Assert.Equal("Draft", created.Status);
        Assert.Equal(5m, created.Days);
        Assert.Equal(new DateOnly(2026, 11, 9), created.AppliedOn);
        Assert.Null(created.ApproverId);
        Assert.Null(created.ActionedOn);
        Assert.Null(created.ApproverRemarks);
        Assert.NotEmpty(created.RowVersion);
    }

    [Fact]
    public async Task Application_numbers_follow_the_sequence_and_a_gap_is_fine()
    {
        var (_, employee, type) = await SeedAsync();
        var first = await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16)));
        host.LeaveNumbers.Skip(1);

        var second = await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 17)));

        Assert.Equal("LV-2026-0001", first.ApplicationNo);
        Assert.Equal("LV-2026-0003", second.ApplicationNo);
    }

    [Fact]
    public async Task The_number_of_a_used_application_is_skipped()
    {
        var (_, employee, type) = await SeedAsync();
        await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16)));
        host.LeaveNumbers.Reset();

        var next = await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 17)));

        Assert.Equal("LV-2026-0002", next.ApplicationNo);
    }

    [Fact]
    public async Task The_weekend_comes_from_the_configured_week()
    {
        var (_, employee, type) = await SeedAsync();
        host.Week.Days.Clear();
        host.Week.Days.Add(DayOfWeek.Friday);

        // Monday to Sunday with only Friday off: six working days.
        var created = await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), D(11, 22)));

        Assert.Equal(6m, created.Days);
    }

    [Fact]
    public async Task Holidays_are_not_counted()
    {
        var (_, employee, type) = await SeedAsync();
        host.AddHoliday(D(11, 18), "Test holiday");

        var created = await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), D(11, 20)));

        Assert.Equal(4m, created.Days);
    }

    [Fact]
    public async Task Dates_without_a_working_day_are_refused()
    {
        var (_, employee, type) = await SeedAsync();

        // Saturday 21 and Sunday 22 November.
        var result = await host.CreateApplication(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 21), D(11, 22)));

        var error = ResultAssert.FailedOnField(result, "endDate", "The selected dates contain no working days.");
        Assert.Equal("Choose dates that include a working day.", Assert.Single(error.FieldErrors!["endDate"]));
    }

    [Fact]
    public async Task The_end_date_cannot_be_before_the_start_date()
    {
        var (_, employee, type) = await SeedAsync();

        var result = await host.CreateApplication(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 18), D(11, 17)));

        ResultAssert.FailedOnField(result, "endDate", "End date must be on or after the start date.");
    }

    [Fact]
    public async Task A_request_cannot_cover_more_than_366_days()
    {
        var (_, employee, type) = await SeedAsync(maxConsecutive: 1000);

        var result = await host.CreateApplication(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), new DateOnly(2028, 1, 1)));

        ResultAssert.FailedOnField(result, "endDate");
    }

    [Fact]
    public async Task A_reason_is_required()
    {
        var (_, employee, type) = await SeedAsync();

        var result = await host.CreateApplication(LeaveFixtures.NewApplication(employee.Id, type.Id) with { Reason = "  " });

        ResultAssert.FailedOnField(result, "reason");
    }

    // ---- Half days ----

    [Fact]
    public async Task A_half_day_is_half_a_day_on_a_single_date()
    {
        var (_, employee, type) = await SeedAsync();

        var created = await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), halfDay: true));

        Assert.True(created.HalfDay);
        Assert.Equal(0.5m, created.Days);
    }

    [Fact]
    public async Task A_half_day_over_several_dates_is_refused()
    {
        var (_, employee, type) = await SeedAsync();

        var result = await host.CreateApplication(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), D(11, 17), halfDay: true));

        var error = ResultAssert.FailedOnField(result, "endDate", "A half day must start and end on the same date.");
        Assert.Equal("A half day covers a single date.", Assert.Single(error.FieldErrors!["endDate"]));
    }

    // ---- Employee and leave type ----

    [Fact]
    public async Task An_unknown_employee_is_refused()
    {
        var (_, _, type) = await SeedAsync();

        var result = await host.CreateApplication(LeaveFixtures.NewApplication(9999, type.Id));

        ResultAssert.FailedOnField(result, "employeeId", "The selected employee does not exist.");
    }

    [Fact]
    public async Task An_unknown_or_inactive_leave_type_is_refused()
    {
        var (_, employee, type) = await SeedAsync();
        var inactive = await host.CreateTypeAsync(LeaveFixtures.NewType("OLD", "Old Leave", status: "Inactive"));

        ResultAssert.FailedOnField(
            await host.CreateApplication(LeaveFixtures.NewApplication(employee.Id, 9999)),
            "leaveTypeId",
            "Choose an active leave type.");
        ResultAssert.FailedOnField(
            await host.CreateApplication(LeaveFixtures.NewApplication(employee.Id, inactive.Id)),
            "leaveTypeId",
            "Choose an active leave type.");
        Assert.True((await host.CreateApplication(LeaveFixtures.NewApplication(employee.Id, type.Id))).IsSuccess);
    }

    [Fact]
    public async Task A_type_for_one_gender_is_refused_for_the_other()
    {
        var (_, woman, _) = await SeedAsync();
        var paternity = await host.CreateTypeAsync(LeaveFixtures.NewType("PAT", "Paternity Leave", gender: "Male"));

        var result = await host.CreateApplication(LeaveFixtures.NewApplication(woman.Id, paternity.Id));

        var error = ResultAssert.FailedOnField(result, "leaveTypeId", "Paternity Leave is not available to this employee.");
        Assert.Equal("Not available for this employee.", Assert.Single(error.FieldErrors!["leaveTypeId"]));
    }

    [Fact]
    public async Task A_type_for_one_gender_is_open_to_that_gender()
    {
        var org = await host.SeedOrgAsync();
        var man = await host.CreateLeaveEmployeeAsync(org, "ben@example.test", "Male");
        var paternity = await host.CreateTypeAsync(LeaveFixtures.NewType("PAT", "Paternity Leave", gender: "Male"));

        var result = await host.CreateApplication(LeaveFixtures.NewApplication(man.Id, paternity.Id));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Leave_cannot_start_before_the_joining_date()
    {
        var org = await host.SeedOrgAsync();
        var newcomer = await host.CreateLeaveEmployeeAsync(org, "new@example.test", joined: D(11, 17));
        var type = await host.CreateTypeAsync();

        var result = await host.CreateApplication(LeaveFixtures.NewApplication(newcomer.Id, type.Id, D(11, 16), D(11, 18)));

        var error = ResultAssert.FailedOnField(result, "startDate", "Leave cannot start before the joining date.");
        Assert.Equal("Must be on or after 2026-11-17.", Assert.Single(error.FieldErrors!["startDate"]));
    }

    // ---- Consecutive days ----

    [Fact]
    public async Task More_days_than_the_type_allows_at_a_time_are_refused()
    {
        var (_, employee, type) = await SeedAsync(maxConsecutive: 3);

        // Monday to Friday is five working days.
        var result = await host.CreateApplication(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), D(11, 20)));

        var error = ResultAssert.FailedOnField(result, "endDate", "Annual Leave is limited to 3 consecutive days.");
        Assert.Equal("At most 3 days at a time.", Assert.Single(error.FieldErrors!["endDate"]));
    }

    [Fact]
    public async Task Exactly_the_allowed_days_are_accepted()
    {
        var (_, employee, type) = await SeedAsync(maxConsecutive: 3);

        var result = await host.CreateApplication(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), D(11, 18)));

        Assert.Equal(3m, ResultAssert.Succeeded(result).Days);
    }

    // ---- Overlap ----

    [Fact]
    public async Task Dates_that_overlap_submitted_leave_are_refused()
    {
        var (_, employee, type) = await SeedAsync();
        await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), D(11, 18)));

        var result = await host.CreateApplication(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 18), D(11, 19)));

        var error = ResultAssert.FailedOnField(result, "startDate", "These dates overlap LV-2026-0001 (2026-11-16 – 2026-11-18).");
        Assert.Equal("Overlaps LV-2026-0001.", Assert.Single(error.FieldErrors!["startDate"]));
    }

    [Fact]
    public async Task Dates_that_overlap_approved_leave_are_refused()
    {
        var (_, employee, type) = await SeedAsync();
        var submitted = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), D(11, 18)));
        ResultAssert.Succeeded(await host.Approve(submitted));

        var result = await host.CreateApplication(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16)));

        ResultAssert.FailedOnField(result, "startDate");
    }

    [Fact]
    public async Task Draft_rejected_and_cancelled_leave_do_not_block_other_requests()
    {
        var (_, employee, type) = await SeedAsync();
        await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16)));
        var rejected = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 17)));
        ResultAssert.Succeeded(await host.Reject(rejected));
        var cancelled = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 18)));
        ResultAssert.Succeeded(await host.Cancel(cancelled));

        Assert.True((await host.CreateApplication(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), D(11, 18)))).IsSuccess);
    }

    [Fact]
    public async Task Leave_of_another_employee_does_not_overlap()
    {
        var (org, employee, type) = await SeedAsync();
        var colleague = await host.CreateLeaveEmployeeAsync(org, "ben@example.test");
        await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), D(11, 18)));

        Assert.True((await host.CreateApplication(LeaveFixtures.NewApplication(colleague.Id, type.Id, D(11, 16), D(11, 18)))).IsSuccess);
    }

    [Fact]
    public async Task Overlap_is_checked_again_when_a_draft_is_submitted()
    {
        var (_, employee, type) = await SeedAsync();
        var first = await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), D(11, 18)));
        var second = await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 17), D(11, 19)));
        ResultAssert.Succeeded(await host.Submit(first));

        var result = await host.Submit(second);

        ResultAssert.FailedOnField(result, "startDate");
    }

    // ---- Update ----

    [Fact]
    public async Task A_draft_can_be_changed_and_its_days_are_counted_again()
    {
        var (_, employee, type) = await SeedAsync();
        var draft = await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), D(11, 17)));

        var updated = ResultAssert.Succeeded(await host.Get<IUpdateHandler<LeaveApplicationUpdateRequest, LeaveApplicationDto>>()
            .HandleAsync(draft.Id, LeaveFixtures.ApplicationUpdateOf(draft, D(11, 20)), default));

        Assert.Equal(5m, updated.Days);
        Assert.Equal(draft.ApplicationNo, updated.ApplicationNo);
        Assert.NotEqual(draft.RowVersion, updated.RowVersion);
    }

    [Fact]
    public async Task A_submitted_application_cannot_be_changed()
    {
        var (_, employee, type) = await SeedAsync();
        var submitted = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16)));

        var result = await host.Get<IUpdateHandler<LeaveApplicationUpdateRequest, LeaveApplicationDto>>()
            .HandleAsync(submitted.Id, LeaveFixtures.ApplicationUpdateOf(submitted, D(11, 17)), default);

        ResultAssert.Failed(result, ErrorType.Conflict, "Only draft applications can be changed. Cancel a submitted one instead.");
    }

    [Fact]
    public async Task Updating_without_a_row_version_or_with_a_stale_one_is_refused()
    {
        var (_, employee, type) = await SeedAsync();
        var draft = await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16)));
        var update = host.Get<IUpdateHandler<LeaveApplicationUpdateRequest, LeaveApplicationDto>>();

        ResultAssert.FailedOnField(
            await update.HandleAsync(draft.Id, LeaveFixtures.ApplicationUpdateOf(draft) with { RowVersion = null }, default),
            "rowVersion");
        ResultAssert.Succeeded(await update.HandleAsync(draft.Id, LeaveFixtures.ApplicationUpdateOf(draft, D(11, 17)), default));
        ResultAssert.Failed(
            await update.HandleAsync(draft.Id, LeaveFixtures.ApplicationUpdateOf(draft, D(11, 18)), default),
            ErrorType.Conflict);
    }

    [Fact]
    public async Task Updating_an_unknown_application_is_not_found()
    {
        var (_, employee, type) = await SeedAsync();
        var draft = await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id));

        var result = await host.Get<IUpdateHandler<LeaveApplicationUpdateRequest, LeaveApplicationDto>>()
            .HandleAsync(999, LeaveFixtures.ApplicationUpdateOf(draft), default);

        ResultAssert.Failed(result, ErrorType.NotFound);
    }

    // ---- Delete ----

    [Fact]
    public async Task A_draft_can_be_deleted()
    {
        var (_, employee, type) = await SeedAsync();
        var draft = await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id));

        var result = await host.Get<IDeleteHandler<LeaveApplicationDto>>().HandleAsync(draft.Id, Requests.Version(draft.RowVersion), default);

        Assert.True(result.IsSuccess);
        ResultAssert.Failed(await host.GetApplication(draft.Id), ErrorType.NotFound);
    }

    [Fact]
    public async Task A_submitted_application_cannot_be_deleted()
    {
        var (_, employee, type) = await SeedAsync();
        var submitted = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id));

        var result = await host.Get<IDeleteHandler<LeaveApplicationDto>>().HandleAsync(submitted.Id, Requests.Version(submitted.RowVersion), default);

        ResultAssert.Failed(result, ErrorType.Conflict, "Only draft applications can be deleted. Cancel a submitted one instead.");
    }

    // ---- Reads ----

    [Fact]
    public void There_is_no_handler_that_lists_every_application_at_once() =>
        Assert.Null(host.GetOrDefault<IGetAllHandler<LeaveApplicationDto>>());

    [Fact]
    public async Task The_query_filters_by_status_employee_and_overlap_with_a_date_range()
    {
        var (org, employee, type) = await SeedAsync();
        var colleague = await host.CreateLeaveEmployeeAsync(org, "ben@example.test");
        await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16)));
        await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 23)));
        await host.SubmittedAsync(LeaveFixtures.NewApplication(colleague.Id, type.Id, D(11, 24)));
        var query = host.Get<IQueryHandler<LeaveApplicationDto, LeaveApplicationFilter>>();

        var submitted = ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<LeaveApplicationFilter>(0, 10, null, null, new LeaveApplicationFilter(Status: "Submitted")), default));
        var mine = ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<LeaveApplicationFilter>(0, 10, null, null, new LeaveApplicationFilter(EmployeeId: employee.Id)), default));
        var inRange = ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<LeaveApplicationFilter>(0, 10, null, null, new LeaveApplicationFilter(DateFrom: D(11, 22), DateTo: D(11, 23))), default));
        var all = ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<LeaveApplicationFilter>(0, 10, new SortState("startDate", "desc"), null, new LeaveApplicationFilter(Status: "all")), default));
        var searched = ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<LeaveApplicationFilter>(0, 10, null, "LV-2026-0003", new LeaveApplicationFilter()), default));

        Assert.Equal(2, submitted.Total);
        Assert.Equal(2, mine.Total);
        Assert.Equal(D(11, 23), Assert.Single(inRange.Items).StartDate);
        Assert.Equal([D(11, 24), D(11, 23), D(11, 16)], all.Items.Select(item => item.StartDate));
        Assert.Equal("LV-2026-0003", Assert.Single(searched.Items).ApplicationNo);
    }

    [Fact]
    public async Task An_unknown_status_filter_matches_nothing_and_an_unknown_sort_is_refused()
    {
        var (_, employee, type) = await SeedAsync();
        await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id));
        var query = host.Get<IQueryHandler<LeaveApplicationDto, LeaveApplicationFilter>>();

        var none = ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<LeaveApplicationFilter>(0, 10, null, null, new LeaveApplicationFilter(Status: "Nope")), default));
        var badSort = await query.HandleAsync(new PageRequest<LeaveApplicationFilter>(0, 10, new SortState("reason", "asc"), null, new LeaveApplicationFilter()), default);

        Assert.Equal(0, none.Total);
        ResultAssert.FailedOnField(badSort, "sort");
    }
}
