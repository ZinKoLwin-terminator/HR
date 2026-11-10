using System.Data;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Leave.Applications;
using Apex.Hrms.Application.Leave.Balances;
using Apex.Hrms.Application.Leave.Calendar;
using Apex.Hrms.Application.Leave.LeavePolicies;
using Apex.Hrms.Application.Leave.LeaveTypes;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The leave module against SQL Server: the handlers with the real EF mapping, the sequence, the check constraints, the
/// unique indexes and the temporal table. The clock says today is Monday 2026-11-09; the first leave Monday is 2026-11-16.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class LeaveTests : DatabaseTest
{
    private static DateOnly D(int month, int day) => new(2026, month, day);

    private async Task<(EmployeeDto Employee, LeaveTypeDto Type)> SeedAsync(decimal days = 15m)
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org);
        var type = await SeedLeaveTypeAsync(days: days);
        return (employee, type);
    }

    private Task<Result<LeaveApplicationDto>> Approve(LeaveApplicationDto application, string? remarks = null) =>
        RunCommand<ApproveLeaveCommand, LeaveApplicationDto>(new ApproveLeaveCommand(application.Id, Requests.Version(application.RowVersion), remarks));

    private Task<Result<LeaveApplicationDto>> Reject(LeaveApplicationDto application, string? remarks) =>
        RunCommand<RejectLeaveCommand, LeaveApplicationDto>(new RejectLeaveCommand(application.Id, Requests.Version(application.RowVersion), remarks));

    private Task<Result<LeaveApplicationDto>> Cancel(LeaveApplicationDto application, string? remarks = null) =>
        RunCommand<CancelLeaveCommand, LeaveApplicationDto>(new CancelLeaveCommand(application.Id, Requests.Version(application.RowVersion), remarks));

    private Task<Result<LeaveApplicationDto>> Submit(LeaveApplicationDto application) =>
        RunCommand<SubmitLeaveCommand, LeaveApplicationDto>(new SubmitLeaveCommand(application.Id, Requests.Version(application.RowVersion)));

    private Task<Result<IReadOnlyList<LeaveBalanceDto>>> Balances(int employeeId, int? year = null) =>
        InScopeAsync(sp => sp.GetRequiredService<ILeaveBalanceHandler>().HandleAsync(employeeId, year, default));

    private Task<Result<LeaveCalendarDto>> Calendar(LeaveCalendarRequest request) =>
        InScopeAsync(sp => sp.GetRequiredService<ILeaveCalendarHandler>().HandleAsync(request, default));

    // ---- Types and policies ----

    [IntegrationFact]
    public async Task A_leave_type_is_saved_with_its_audit_stamps_and_a_new_row_version_on_update()
    {
        var type = await SeedLeaveTypeAsync();

        var changed = Succeeded(await Update(type.Id, Requests.LeaveTypeUpdate(type, name: Name("Yearly Leave"), days: 18m)));

        Assert.Equal(Name("Yearly Leave"), changed.Name);
        Assert.Equal(18m, changed.DefaultDaysPerYear);
        Assert.NotEqual(type.RowVersion, changed.RowVersion);
        Assert.Equal(TestAdministrator.AccountId, await Sql.ScalarAsync<int>("SELECT CreatedByUserId FROM lve.LeaveType WHERE Id = @id", ("@id", type.Id)));
        Assert.Equal(TestAdministrator.AccountId, await Sql.ScalarAsync<int>("SELECT ModifiedByUserId FROM lve.LeaveType WHERE Id = @id", ("@id", type.Id)));
        Assert.Equal(18m, await Sql.ScalarAsync<decimal>("SELECT DefaultDaysPerYear FROM lve.LeaveType WHERE Id = @id", ("@id", type.Id)));
    }

    [IntegrationFact]
    public async Task Leave_type_rules_hold_and_a_duplicate_code_is_refused()
    {
        await SeedLeaveTypeAsync("AL");

        FailedOnField(await Create(Requests.LeaveType("AL", "Other")), "code");
        FailedOnField(await Create(Requests.LeaveType("XX", maxConsecutive: 0)), "maxConsecutiveDays");
        FailedOnField(await Create(Requests.LeaveType("XY", gender: "Robot")), "eligibleGender");
        Assert.Equal("Male", Succeeded(await Create(Requests.LeaveType("PAT", gender: "Male"))).EligibleGender);
    }

    [IntegrationFact]
    public async Task Activate_and_deactivate_a_leave_type_and_a_policy_persist()
    {
        var type = await SeedLeaveTypeAsync();
        var policy = await SeedLeavePolicyAsync(type.Id);

        var inactiveType = Succeeded(await Deactivate<LeaveTypeDto>(type.Id, Requests.Version(type.RowVersion)));
        var inactivePolicy = Succeeded(await Deactivate<LeavePolicyDto>(policy.Id, Requests.Version(policy.RowVersion)));
        var activePolicy = Succeeded(await Activate<LeavePolicyDto>(policy.Id, Requests.Version(inactivePolicy.RowVersion)));

        Assert.Equal("Inactive", inactiveType.Status);
        Assert.Equal("Active", activePolicy.Status);
        Assert.Equal("Inactive", await Sql.ScalarAsync<string>("SELECT Status FROM lve.LeaveType WHERE Id = @id", ("@id", type.Id)));
        Assert.Equal("Active", await Sql.ScalarAsync<string>("SELECT Status FROM lve.LeavePolicy WHERE Id = @id", ("@id", policy.Id)));
    }

    [IntegrationFact]
    public async Task Only_one_active_policy_per_group_is_accepted_and_the_database_index_backs_it_up()
    {
        var type = await SeedLeaveTypeAsync();
        var org = await SeedOrganizationAsync();
        await SeedLeavePolicyAsync(type.Id, "All staff");

        var clash = await Create(Requests.LeavePolicy(type.Id, "All staff again"));
        var otherGroup = Succeeded(await Create(Requests.LeavePolicy(type.Id, "Grade", jobGradeId: org.Grade.Id)));
        var inactive = Succeeded(await Create(Requests.LeavePolicy(type.Id, "Spare", status: "Inactive")));
        var activateClash = await Activate<LeavePolicyDto>(inactive.Id, Requests.Version(inactive.RowVersion));

        FailedOnField(clash, "leaveTypeId");
        FailedOnField(activateClash, "leaveTypeId");
        Assert.NotNull(otherGroup);

        // Another session writes a clashing policy between the check and the save: the unique index refuses it and the
        // refusal is translated the same way as the pre-check.
        BeforeNextSave = () => Sql.ExecuteAsync(
            "INSERT INTO lve.LeavePolicy (Name, LeaveTypeId, JobGradeId, EmploymentType, DaysPerYear, Accrual, MinServiceMonths, Status) VALUES (@name, @type, NULL, 'Intern', 10, 'Upfront', 0, 'Active');",
            ("@name", Name("Raw intern policy")),
            ("@type", type.Id));
        var raced = await Create(Requests.LeavePolicy(type.Id, "Intern policy", employmentType: "Intern"));

        Assert.Equal("An active policy already exists for this leave type, grade and employment type.", Failed(raced, ErrorType.Validation).Message);
        FailedOnField(raced, "leaveTypeId");
    }

    [IntegrationFact]
    public async Task A_policy_can_be_changed_and_deleted_and_a_used_type_cannot_be_deleted()
    {
        var (employee, type) = await SeedAsync();
        var policy = await SeedLeavePolicyAsync(type.Id);
        await SeedLeaveApplicationAsync(employee.Id, type.Id, D(11, 16));

        var changed = Succeeded(await Update(policy.Id, Requests.LeavePolicyUpdate(policy, days: 20m)));
        var blocked = await Delete<LeaveTypeDto>(type.Id, Requests.Version(type.RowVersion));
        var deleted = await Delete<LeavePolicyDto>(policy.Id, Requests.Version(changed.RowVersion));

        Assert.Equal(20m, changed.DaysPerYear);
        Assert.Equal(
            $"\"{Name("Annual Leave")}\" cannot be deleted because it is used by 1 application(s), 1 policy(ies). Deactivate it instead.",
            Failed(blocked, ErrorType.Conflict).Message);
        Assert.True(deleted.IsSuccess);
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM lve.LeavePolicy WHERE Id = @id", ("@id", policy.Id)));
    }

    [IntegrationFact]
    public async Task A_job_grade_with_a_policy_and_an_employee_with_leave_cannot_be_deleted()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org);
        var type = await SeedLeaveTypeAsync();
        var grade = await SeedJobGradeAsync("G2", "Second Grade");
        await SeedLeavePolicyAsync(type.Id, jobGradeId: grade.Id);
        await SeedLeaveApplicationAsync(employee.Id, type.Id, D(11, 16));

        var gradeResult = await Delete<JobGradeDto>(grade.Id, Requests.Version(grade.RowVersion));
        var employeeResult = await Delete<EmployeeDto>(employee.Id, Requests.Version(employee.RowVersion));

        Assert.Contains("1 leave policy(ies)", Failed(gradeResult, ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.Contains("1 leave application(s)", Failed(employeeResult, ErrorType.Conflict).Message, StringComparison.Ordinal);
    }

    // ---- Applications ----

    [IntegrationFact]
    public async Task A_new_application_is_a_draft_with_a_number_from_the_sequence_and_audit_stamps()
    {
        var (employee, type) = await SeedAsync();

        var first = await SeedLeaveApplicationAsync(employee.Id, type.Id, D(11, 16), D(11, 20));
        var second = await SeedLeaveApplicationAsync(employee.Id, type.Id, D(11, 23));

        Assert.Matches(@"^LV-2026-\d{4}$", first.ApplicationNo);
        Assert.Matches(@"^LV-2026-\d{4}$", second.ApplicationNo);
        Assert.True(string.CompareOrdinal(second.ApplicationNo, first.ApplicationNo) > 0);
        Assert.Equal("Draft", first.Status);
        Assert.Equal(5m, first.Days);
        Assert.Equal(D(11, 9), first.AppliedOn);
        Assert.Equal("Draft", await Sql.ScalarAsync<string>("SELECT Status FROM lve.LeaveApplication WHERE Id = @id", ("@id", first.Id)));
        Assert.Equal(TestAdministrator.AccountId, await Sql.ScalarAsync<int>("SELECT CreatedByUserId FROM lve.LeaveApplication WHERE Id = @id", ("@id", first.Id)));
        Assert.Equal(5m, await Sql.ScalarAsync<decimal>("SELECT Days FROM lve.LeaveApplication WHERE Id = @id", ("@id", first.Id)));
    }

    [IntegrationFact]
    public async Task Weekends_and_holidays_from_the_database_are_left_out_of_the_days()
    {
        var (employee, type) = await SeedAsync();
        await Sql.ExecuteAsync(
            "INSERT INTO att.Holiday ([Date], Name, [Type]) VALUES ('2026-11-18', @name, 'Company');",
            ("@name", Requests.HolidayName()));

        var week = await SeedLeaveApplicationAsync(employee.Id, type.Id, D(11, 16), D(11, 22));
        var half = await SeedLeaveApplicationAsync(employee.Id, type.Id, D(11, 26), halfDay: true);
        var weekendOnly = await Create(Requests.LeaveApplication(employee.Id, type.Id, D(11, 28), D(11, 29)));

        Assert.Equal(4m, week.Days);
        Assert.Equal(0.5m, half.Days);
        FailedOnField(weekendOnly, "endDate");
    }

    [IntegrationFact]
    public async Task Submit_approve_by_the_administrator_saves_the_name_snapshot_and_no_approver_employee()
    {
        var (employee, type) = await SeedAsync();
        var submitted = await SeedSubmittedLeaveAsync(employee.Id, type.Id, D(11, 16), D(11, 18));

        var approved = Succeeded(await Approve(submitted));

        Assert.Equal("Submitted", submitted.Status);
        Assert.Equal("Approved", approved.Status);
        Assert.Null(approved.ApproverId);
        Assert.Equal($"{Tag} Test User", approved.ActionedByName);
        Assert.Equal(D(11, 9), approved.ActionedOn);
        Assert.Equal("Approved.", approved.ApproverRemarks);
        Assert.NotEqual(submitted.RowVersion, approved.RowVersion);
        Assert.Equal(TestAdministrator.AccountId, await Sql.ScalarAsync<int>("SELECT ActionedByUserId FROM lve.LeaveApplication WHERE Id = @id", ("@id", approved.Id)));
        Assert.Equal(DBNull.Value, await Sql.ScalarAsync<object>("SELECT ApproverEmployeeId FROM lve.LeaveApplication WHERE Id = @id", ("@id", approved.Id)));
        Assert.Equal($"{Tag} Test User", await Sql.ScalarAsync<string>("SELECT ActionedByName FROM lve.LeaveApplication WHERE Id = @id", ("@id", approved.Id)));
    }

    [IntegrationFact]
    public async Task Another_employee_is_saved_as_the_approver()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org, "1", "mia");
        var manager = await SeedEmployeeAsync(org, "2", "ben");
        var type = await SeedLeaveTypeAsync();
        var submitted = await SeedSubmittedLeaveAsync(employee.Id, type.Id, D(11, 16));
        CurrentEmployeeId = manager.Id;

        var approved = Succeeded(await Approve(submitted, "Enjoy"));

        Assert.Equal(manager.Id, approved.ApproverId);
        Assert.Equal(manager.Id, await Sql.ScalarAsync<int>("SELECT ApproverEmployeeId FROM lve.LeaveApplication WHERE Id = @id", ("@id", approved.Id)));
        Assert.Equal("Enjoy", await Sql.ScalarAsync<string>("SELECT ApproverRemarks FROM lve.LeaveApplication WHERE Id = @id", ("@id", approved.Id)));
    }

    [IntegrationFact]
    public async Task Nobody_decides_their_own_leave_and_the_database_check_backs_it_up()
    {
        var (employee, type) = await SeedAsync();
        var submitted = await SeedSubmittedLeaveAsync(employee.Id, type.Id, D(11, 16));
        CurrentEmployeeId = employee.Id;

        var approve = await Approve(submitted);
        var reject = await Reject(submitted, "No");

        Assert.Equal("You cannot decide your own leave request.", Failed(approve, ErrorType.Conflict).Message);
        Failed(reject, ErrorType.Conflict);
        Assert.Equal("Submitted", await Sql.ScalarAsync<string>("SELECT Status FROM lve.LeaveApplication WHERE Id = @id", ("@id", submitted.Id)));
        Assert.Equal(
            547,
            await Sql.ErrorNumberOfAsync("UPDATE lve.LeaveApplication SET ApproverEmployeeId = EmployeeId WHERE Id = @id", ("@id", submitted.Id)));
    }

    [IntegrationFact]
    public async Task A_decision_by_a_stale_row_version_is_a_conflict()
    {
        var (employee, type) = await SeedAsync();
        var submitted = await SeedSubmittedLeaveAsync(employee.Id, type.Id, D(11, 16));
        Succeeded(await Approve(submitted));

        var result = await Approve(submitted);

        Failed(result, ErrorType.Conflict);
    }

    [IntegrationFact]
    public async Task Rejecting_needs_a_reason_and_saves_it_and_the_history_keeps_every_move()
    {
        var (employee, type) = await SeedAsync();
        var submitted = await SeedSubmittedLeaveAsync(employee.Id, type.Id, D(11, 16));

        FailedOnField(await Reject(submitted, " "), "remarks");
        var rejected = Succeeded(await Reject(submitted, "Team is short that week"));

        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("Team is short that week", await Sql.ScalarAsync<string>("SELECT ApproverRemarks FROM lve.LeaveApplication WHERE Id = @id", ("@id", rejected.Id)));
        // Draft, then submitted, then rejected: two earlier versions sit in the system-versioned history table.
        Assert.True(await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM lve.LeaveApplicationHistory WHERE Id = @id", ("@id", rejected.Id)) >= 2);
    }

    [IntegrationFact]
    public async Task Submitted_and_approved_leave_that_has_not_started_can_be_cancelled_and_started_leave_cannot()
    {
        var (employee, type) = await SeedAsync();
        var submitted = await SeedSubmittedLeaveAsync(employee.Id, type.Id, D(11, 16));
        var approved = Succeeded(await Approve(await SeedSubmittedLeaveAsync(employee.Id, type.Id, D(11, 23))));

        var cancelled = Succeeded(await Cancel(submitted, "Plans changed"));
        var cancelledApproved = Succeeded(await Cancel(approved));
        Time.Advance(TimeSpan.FromDays(30));
        var started = Succeeded(await Approve(await SeedSubmittedLeaveAsync(employee.Id, type.Id, D(12, 14))));
        Time.Advance(TimeSpan.FromDays(10));

        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Plans changed", cancelled.ApproverRemarks);
        Assert.Equal("Cancelled.", cancelledApproved.ApproverRemarks);
        Assert.Equal("Leave that has already started cannot be cancelled.", Failed(await Cancel(started), ErrorType.Conflict).Message);
    }

    [IntegrationFact]
    public async Task Only_a_draft_can_be_changed_or_deleted()
    {
        var (employee, type) = await SeedAsync();
        var draft = await SeedLeaveApplicationAsync(employee.Id, type.Id, D(11, 16));
        var other = await SeedLeaveApplicationAsync(employee.Id, type.Id, D(11, 23));
        var submitted = await SeedSubmittedLeaveAsync(employee.Id, type.Id, D(11, 30));

        var changed = Succeeded(await Update(draft.Id, Requests.LeaveApplicationUpdate(draft, D(11, 18))));
        var refusedUpdate = await Update(submitted.Id, Requests.LeaveApplicationUpdate(submitted, D(12, 2)));
        var refusedDelete = await Delete<LeaveApplicationDto>(submitted.Id, Requests.Version(submitted.RowVersion));
        var deleted = await Delete<LeaveApplicationDto>(other.Id, Requests.Version(other.RowVersion));

        Assert.Equal(3m, changed.Days);
        Failed(refusedUpdate, ErrorType.Conflict);
        Failed(refusedDelete, ErrorType.Conflict);
        Assert.True(deleted.IsSuccess);
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM lve.LeaveApplication WHERE Id = @id", ("@id", other.Id)));
    }

    [IntegrationFact]
    public async Task Overlap_balance_gender_and_consecutive_day_rules_are_enforced_against_stored_rows()
    {
        var (employee, type) = await SeedAsync(days: 3m);
        var paternity = await SeedLeaveTypeAsync("PAT", "Paternity", gender: "Male");
        var casual = await SeedLeaveTypeAsync("CL", "Casual", days: 10m, maxConsecutive: 2);
        await SeedSubmittedLeaveAsync(employee.Id, type.Id, D(11, 16), D(11, 18));

        var overlap = await Create(Requests.LeaveApplication(employee.Id, type.Id, D(11, 18)));
        var gender = await Create(Requests.LeaveApplication(employee.Id, paternity.Id, D(11, 23)));
        var tooLong = await Create(Requests.LeaveApplication(employee.Id, casual.Id, D(11, 23), D(11, 25)));
        var draft = await SeedLeaveApplicationAsync(employee.Id, type.Id, D(11, 23));
        var balance = await Submit(draft);

        FailedOnField(overlap, "startDate");
        FailedOnField(gender, "leaveTypeId");
        FailedOnField(tooLong, "endDate");
        Assert.Equal("Insufficient Annual Leave balance: 1 day(s) requested, 0 available.", Failed(balance, ErrorType.Conflict).Message.Replace(Name("Annual Leave"), "Annual Leave", StringComparison.Ordinal));
    }

    [IntegrationFact]
    public async Task A_type_that_needs_a_document_is_submitted_only_with_a_document_name()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org);
        var type = await SeedLeaveTypeAsync("ML", "Medical", requiresDocument: true);
        var without = await SeedLeaveApplicationAsync(employee.Id, type.Id, D(11, 16));
        var with = Succeeded(await Create(Requests.LeaveApplication(employee.Id, type.Id, D(11, 23), documentName: "certificate.pdf")));

        FailedOnField(await Submit(without), "documentName");
        Assert.Equal("Submitted", Succeeded(await Submit(with)).Status);
        Assert.Equal("certificate.pdf", await Sql.ScalarAsync<string>("SELECT DocumentName FROM lve.LeaveApplication WHERE Id = @id", ("@id", with.Id)));
    }

    // ---- Balances ----

    [IntegrationFact]
    public async Task Balances_are_worked_out_from_the_stored_applications()
    {
        var (employee, type) = await SeedAsync();
        var approved = await SeedSubmittedLeaveAsync(employee.Id, type.Id, D(11, 16), D(11, 18));
        Succeeded(await Approve(approved));
        await SeedSubmittedLeaveAsync(employee.Id, type.Id, D(11, 23), D(11, 24));
        await SeedLeaveApplicationAsync(employee.Id, type.Id, D(11, 30));

        var line = Succeeded(await Balances(employee.Id)).Single(balance => balance.LeaveTypeId == type.Id);

        Assert.Equal(15m, line.Entitled);
        Assert.Equal(3m, line.Taken);
        Assert.Equal(2m, line.Pending);
        Assert.Equal(10m, line.Available);
    }

    [IntegrationFact]
    public async Task Policies_decide_the_entitlement_by_grade_employment_type_service_and_accrual()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org);
        var type = await SeedLeaveTypeAsync();
        var casual = await SeedLeaveTypeAsync("CL", "Casual", days: 6m);

        await SeedLeavePolicyAsync(type.Id, "All staff", days: 16m);
        await SeedLeavePolicyAsync(type.Id, "Grade", jobGradeId: org.Grade.Id, days: 18m);
        await SeedLeavePolicyAsync(casual.Id, "Casual monthly", days: 7m, accrual: "Monthly");

        var balances = Succeeded(await Balances(employee.Id));
        Assert.Equal(18m, balances.Single(line => line.LeaveTypeId == type.Id).Entitled);
        // November is month 11: 7 x 11 / 12 = 6.42 days, rounded to half days is 6.5; the whole year in 2025.
        Assert.Equal(6.5m, balances.Single(line => line.LeaveTypeId == casual.Id).Entitled);
        Assert.Equal(7m, Succeeded(await Balances(employee.Id, 2025)).Single(line => line.LeaveTypeId == casual.Id).Entitled);
        FailedOnField(await Balances(employee.Id, 1800), "year");
        Failed(await Balances(2_000_000), ErrorType.NotFound);
    }

    // ---- Calendar ----

    [IntegrationFact]
    public async Task The_calendar_returns_approved_and_submitted_leave_in_the_range_with_holidays_and_weekends()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org, "1", "mia");
        var colleague = await SeedEmployeeAsync(org, "2", "ben");
        var type = await SeedLeaveTypeAsync();
        await Sql.ExecuteAsync(
            "INSERT INTO att.Holiday ([Date], Name, [Type]) VALUES ('2026-11-19', @name, 'Public');",
            ("@name", Requests.HolidayName()));
        Succeeded(await Approve(await SeedSubmittedLeaveAsync(employee.Id, type.Id, D(11, 16), D(11, 18))));
        var pending = await SeedSubmittedLeaveAsync(colleague.Id, type.Id, D(11, 17));
        await SeedLeaveApplicationAsync(employee.Id, type.Id, D(11, 23));

        var calendar = Succeeded(await Calendar(new LeaveCalendarRequest(D(11, 14), D(11, 23))));
        var withoutPending = Succeeded(await Calendar(new LeaveCalendarRequest(D(11, 14), D(11, 23), IncludePending: false)));
        var narrow = Succeeded(await Calendar(new LeaveCalendarRequest(D(11, 18), D(11, 18))));
        var department = Succeeded(await Calendar(new LeaveCalendarRequest(D(11, 14), D(11, 23), DepartmentId: org.Department.Id, LeaveTypeId: type.Id)));

        Assert.Equal(10, calendar.Days.Count);
        Assert.True(calendar.Days[0].IsWeekend);
        Assert.Equal(Requests.HolidayName(), calendar.Days.Single(day => day.Date == D(11, 19)).Holiday);
        Assert.Equal(2, calendar.Days.Single(day => day.Date == D(11, 17)).Entries.Count);
        var entry = calendar.Days.Single(day => day.Date == D(11, 17)).Entries.Single(candidate => candidate.ApplicationId == pending.Id);
        Assert.Equal("Submitted", entry.Status);
        Assert.Equal(type.Id, entry.LeaveTypeId);
        Assert.Equal("Mia Testwell", entry.EmployeeName);
        Assert.Empty(calendar.Days.Single(day => day.Date == D(11, 23)).Entries);
        Assert.Single(withoutPending.Days.Single(day => day.Date == D(11, 17)).Entries);
        Assert.Equal(1, narrow.Days.Sum(day => day.Entries.Count));
        Assert.Equal(4, department.Days.Sum(day => day.Entries.Count));
    }

    // ---- Queries ----

    [IntegrationFact]
    public async Task Every_sortable_field_of_the_three_resources_translates_to_sql()
    {
        var (employee, type) = await SeedAsync();
        var policy = await SeedLeavePolicyAsync(type.Id);
        var application = await SeedSubmittedLeaveAsync(employee.Id, type.Id, D(11, 16));
        Succeeded(await Approve(application));

        foreach (var key in new[] { "id", "code", "name", "defaultDaysPerYear", "maxConsecutiveDays", "status" })
        {
            Succeeded(await Query<LeaveTypeDto, LeaveTypeFilter>(new PageRequest<LeaveTypeFilter>(0, 10, new SortState(key, "desc"), Tag, new LeaveTypeFilter())));
        }

        foreach (var key in new[] { "id", "name", "leaveTypeId", "daysPerYear", "minServiceMonths", "status" })
        {
            Succeeded(await Query<LeavePolicyDto, LeavePolicyFilter>(new PageRequest<LeavePolicyFilter>(0, 10, new SortState(key, "asc"), Tag, new LeavePolicyFilter())));
        }

        foreach (var key in new[] { "id", "applicationNo", "employeeId", "leaveTypeId", "startDate", "endDate", "days", "status", "appliedOn", "actionedOn" })
        {
            var page = Succeeded(await Query<LeaveApplicationDto, LeaveApplicationFilter>(
                new PageRequest<LeaveApplicationFilter>(0, 10, new SortState(key, "desc"), null, new LeaveApplicationFilter(EmployeeId: employee.Id))));
            Assert.Equal(application.Id, Assert.Single(page.Items).Id);
        }

        Assert.Equal(policy.Id, Assert.Single(Succeeded(await Query<LeavePolicyDto, LeavePolicyFilter>(
            new PageRequest<LeavePolicyFilter>(0, 10, null, null, new LeavePolicyFilter(LeaveTypeId: type.Id, Status: "Active", Accrual: "Upfront")))).Items).Id);
    }

    [IntegrationFact]
    public async Task The_application_query_filters_by_status_type_employee_and_date_overlap()
    {
        var (employee, type) = await SeedAsync();
        var draft = await SeedLeaveApplicationAsync(employee.Id, type.Id, D(11, 16));
        var submitted = await SeedSubmittedLeaveAsync(employee.Id, type.Id, D(11, 23), D(11, 24));

        async Task<IReadOnlyList<int>> Ids(LeaveApplicationFilter filter, string? search = null) =>
            [.. Succeeded(await Query<LeaveApplicationDto, LeaveApplicationFilter>(new PageRequest<LeaveApplicationFilter>(0, 10, null, search, filter with { EmployeeId = employee.Id }))).Items.Select(item => item.Id)];

        Assert.Equal([submitted.Id], await Ids(new LeaveApplicationFilter(Status: "Submitted")));
        Assert.Equal([draft.Id, submitted.Id], await Ids(new LeaveApplicationFilter(Status: "all")));
        Assert.Equal([submitted.Id], await Ids(new LeaveApplicationFilter(DateFrom: D(11, 24), DateTo: D(11, 30))));
        Assert.Equal([draft.Id], await Ids(new LeaveApplicationFilter(DateTo: D(11, 20))));
        Assert.Equal([draft.Id, submitted.Id], await Ids(new LeaveApplicationFilter(LeaveTypeId: type.Id)));
        Assert.Empty(await Ids(new LeaveApplicationFilter(Status: "Nope")));
        Assert.Equal([draft.Id], await Ids(new LeaveApplicationFilter(), draft.ApplicationNo));
        Assert.Equal([draft.Id, submitted.Id], await Ids(new LeaveApplicationFilter(), "family"));
    }
}
