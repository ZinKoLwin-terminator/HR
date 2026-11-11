using Apex.Hrms.Application.Attendance.Holidays;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Leave.Applications;
using Apex.Hrms.Application.Leave.LeavePolicies;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Activity;
using Apex.Hrms.Domain.Common;
using static Apex.Hrms.Application.Tests.Fakes.AttendanceFixtures;

namespace Apex.Hrms.Application.Tests.Activity;

/// <summary>
/// What each write path of the API records in the activity feed: the category and the wording the frontend mock services
/// use, the actor, and the rule that an entry exists only when the change was saved. The fake unit of work commits the
/// entries the way the EF one does (in the same save).
/// </summary>
public sealed class ActivityWordingTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private static DateOnly D(int month, int day) => new(2026, month, day);

    private Task<Result<CompanyDto>> CreateCompany() =>
        host.Get<ICreateHandler<CompanyCreateRequest, CompanyDto>>().HandleAsync(Requests.Company(), default);

    private (string Category, string Message) Last()
    {
        var entry = host.ActivityLog.Entries[^1];
        return (entry.Category, entry.Message);
    }

    // ---- Organization (the generic create, update, delete and status flows) ----

    [Fact]
    public async Task Creating_an_organization_record_logs_label_and_name_under_organization()
    {
        await host.CreateCompanyAsync();

        Assert.Equal((ActivityCategories.Organization, "Company \"Apex Holdings\" created"), Last());
    }

    [Fact]
    public async Task Updating_deactivating_activating_and_deleting_use_the_mock_wording()
    {
        var company = await host.CreateCompanyAsync();

        var updated = ResultAssert.Succeeded(await host.Get<IUpdateHandler<CompanyUpdateRequest, CompanyDto>>()
            .HandleAsync(company.Id, Requests.CompanyUpdate(company.RowVersion, name: "Apex Group"), default));
        Assert.Equal("Company \"Apex Group\" updated", Last().Message);

        var inactive = ResultAssert.Succeeded(await host.Command<DeactivateCommand, CompanyDto>(
            new DeactivateCommand(company.Id, Requests.Version(updated.RowVersion))));
        Assert.Equal("Company \"Apex Group\" updated", Last().Message);

        var active = ResultAssert.Succeeded(await host.Command<ActivateCommand, CompanyDto>(
            new ActivateCommand(company.Id, Requests.Version(inactive.RowVersion))));
        Assert.Equal((ActivityCategories.Organization, "Company \"Apex Group\" updated"), Last());

        Assert.True((await host.Get<IDeleteHandler<CompanyDto>>().HandleAsync(company.Id, Requests.Version(active.RowVersion), default)).IsSuccess);
        Assert.Equal((ActivityCategories.Organization, "Company \"Apex Group\" deleted"), Last());
    }

    [Fact]
    public async Task A_status_command_that_changes_nothing_leaves_no_entry()
    {
        var company = await host.CreateCompanyAsync();
        var before = host.ActivityLog.Entries.Count;

        ResultAssert.Succeeded(await host.Command<ActivateCommand, CompanyDto>(new ActivateCommand(company.Id, Requests.Version(company.RowVersion))));

        Assert.Equal(before, host.ActivityLog.Entries.Count);
    }

    [Fact]
    public async Task A_job_grade_is_named_by_code_and_name()
    {
        await host.CreateJobGradeAsync("G7");

        Assert.Equal("Job grade \"G7 Junior\" created", Last().Message);
    }

    // ---- Employees: the code, never the name ----

    [Fact]
    public async Task Employee_entries_name_the_employee_by_code_and_never_by_name()
    {
        var org = await host.SeedOrgAsync();
        var employee = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org));

        Assert.Equal((ActivityCategories.Employee, $"Employee \"{employee.EmployeeCode}\" created"), Last());

        ResultAssert.Succeeded(await host.UpdateEmployee(employee.Id, EmployeeFixtures.UpdateOf(employee)));
        Assert.Equal($"Employee \"{employee.EmployeeCode}\" updated", Last().Message);

        var exited = ResultAssert.Succeeded(await host.Command<ExitEmployeeCommand, EmployeeDto>(
            new ExitEmployeeCommand(employee.Id, Requests.Version((await host.GetEmployee(employee.Id)).Value.RowVersion), D(11, 9), "Resigned")));
        Assert.Equal($"Employee \"{exited.EmployeeCode}\" updated", Last().Message);

        Assert.All(host.ActivityLog.Messages, message =>
        {
            Assert.DoesNotContain("Mia", message, StringComparison.Ordinal);
            Assert.DoesNotContain("Hartley", message, StringComparison.Ordinal);
            Assert.DoesNotContain("NRC", message, StringComparison.Ordinal);
        });
    }

    // ---- Leave ----

    [Fact]
    public async Task Leave_types_and_policies_are_logged_under_leave()
    {
        var type = await host.CreateTypeAsync();
        Assert.Equal((ActivityCategories.Leave, "Leave type \"Annual Leave\" created"), Last());

        var policy = await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id, status: "Inactive"));
        Assert.Equal((ActivityCategories.Leave, "Leave policy \"Annual leave standard\" created"), Last());

        var activated = ResultAssert.Succeeded(await host.Command<ActivateCommand, LeavePolicyDto>(
            new ActivateCommand(policy.Id, Requests.Version(policy.RowVersion))));
        Assert.Equal("Leave policy \"Annual leave standard\" updated", Last().Message);

        ResultAssert.Succeeded(await host.Command<DeactivateCommand, LeavePolicyDto>(
            new DeactivateCommand(policy.Id, Requests.Version(activated.RowVersion))));
        Assert.Equal("Leave policy \"Annual leave standard\" updated", Last().Message);
    }

    [Fact]
    public async Task A_leave_application_logs_each_workflow_move_with_the_application_number()
    {
        var org = await host.SeedOrgAsync();
        var employee = await host.CreateLeaveEmployeeAsync(org, "mia@example.test");
        var type = await host.CreateTypeAsync();
        var draft = await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16)));
        var number = draft.ApplicationNo;
        Assert.Equal((ActivityCategories.Leave, $"Leave application \"{number}\" created"), Last());

        var submitted = ResultAssert.Succeeded(await host.Submit(draft));
        Assert.Equal((ActivityCategories.Leave, $"{number} submitted for approval"), Last());

        ResultAssert.Succeeded(await host.Approve(submitted));
        Assert.Equal((ActivityCategories.Leave, $"{number} approved"), Last());
    }

    [Fact]
    public async Task Rejecting_cancelling_updating_and_deleting_a_leave_application_use_the_mock_wording()
    {
        var org = await host.SeedOrgAsync();
        var employee = await host.CreateLeaveEmployeeAsync(org, "mia@example.test");
        var type = await host.CreateTypeAsync();

        var first = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16)));
        ResultAssert.Succeeded(await host.Reject(first));
        Assert.Equal($"{first.ApplicationNo} rejected", Last().Message);

        var second = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 23)));
        ResultAssert.Succeeded(await host.Cancel(second));
        Assert.Equal($"{second.ApplicationNo} cancelled", Last().Message);

        var draft = await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 30)));
        var edited = ResultAssert.Succeeded(await host.Get<IUpdateHandler<LeaveApplicationUpdateRequest, LeaveApplicationDto>>()
            .HandleAsync(draft.Id, LeaveFixtures.ApplicationUpdateOf(draft), default));
        Assert.Equal($"Leave application \"{draft.ApplicationNo}\" updated", Last().Message);

        Assert.True((await host.Get<IDeleteHandler<LeaveApplicationDto>>()
            .HandleAsync(draft.Id, Requests.Version(edited.RowVersion), default)).IsSuccess);
        Assert.Equal($"Leave application \"{draft.ApplicationNo}\" deleted", Last().Message);
    }

    // ---- Attendance ----

    [Fact]
    public async Task Shifts_and_holidays_are_logged_under_attendance()
    {
        await host.CreateShiftAsync();
        Assert.Equal((ActivityCategories.Attendance, "Shift \"General Shift\" created"), Last());

        var holiday = await host.CreateHolidayAsync(D(12, 25), "Christmas");
        Assert.Equal((ActivityCategories.Attendance, "Holiday \"Christmas (2026-12-25)\" created"), Last());

        Assert.True((await host.Get<IDeleteHandler<HolidayDto>>().HandleAsync(holiday.Id, Requests.Version(holiday.RowVersion), default)).IsSuccess);
        Assert.Equal("Holiday \"Christmas (2026-12-25)\" deleted", Last().Message);
    }

    [Fact]
    public async Task A_punch_logs_the_employee_code_and_the_time_and_not_the_name()
    {
        var org = await host.SeedOrgAsync();
        var shift = await host.CreateShiftAsync();
        var employee = await host.CreateWorkerAsync(org, "mia@example.test", shift.Id);

        await host.PunchAsync(employee.Id, "CheckIn", 9, 2);
        Assert.Equal((ActivityCategories.Attendance, $"{employee.EmployeeCode} checked in at 09:02"), Last());

        await host.PunchAsync(employee.Id, "CheckOut", 18, 30);
        Assert.Equal($"{employee.EmployeeCode} checked out at 18:30", Last().Message);
    }

    [Fact]
    public async Task Adjustment_entries_carry_the_id_the_database_assigns_and_the_decision()
    {
        var org = await host.SeedOrgAsync();
        var shift = await host.CreateShiftAsync();
        var employee = await host.CreateWorkerAsync(org, "mia@example.test", shift.Id);
        await host.PunchAsync(employee.Id, "CheckIn", 9, 40);

        var first = await host.CreateAdjustmentAsync(employee.Id, At(9, 0));
        Assert.Equal((ActivityCategories.Attendance, $"Adjustment request \"ADJ-{first.Id:0000}\" created"), Last());

        ResultAssert.Succeeded(await host.ApproveAdjustment(first));
        Assert.Equal($"ADJ-{first.Id:0000} approved for 2026-11-09", Last().Message);

        var second = await host.CreateAdjustmentAsync(employee.Id, At(9, 5));
        ResultAssert.Succeeded(await host.RejectAdjustment(second));
        Assert.Equal($"ADJ-{second.Id:0000} rejected for 2026-11-09", Last().Message);
    }

    [Fact]
    public async Task Overtime_entries_are_created_approved_and_rejected_with_the_mock_wording()
    {
        var org = await host.SeedOrgAsync();
        var shift = await host.CreateShiftAsync();
        var employee = await host.CreateWorkerAsync(org, "mia@example.test", shift.Id);

        var first = await host.CreateOvertimeAsync(employee.Id, At(18, 0), At(20, 0));
        Assert.Equal((ActivityCategories.Attendance, $"Overtime entry \"OT-{first.Id:0000}\" created"), Last());

        ResultAssert.Succeeded(await host.ApproveOvertime(first));
        Assert.Equal($"OT-{first.Id:0000} approved", Last().Message);

        var second = ResultAssert.Succeeded(await host.CreateOvertime(employee.Id, At(18, 0), At(19, 0), Monday.AddDays(-1)));
        ResultAssert.Succeeded(await host.RejectOvertime(second));
        Assert.Equal($"OT-{second.Id:0000} rejected", Last().Message);
    }

    // ---- The rules that hold for every entry ----

    [Fact]
    public async Task A_change_that_fails_to_save_leaves_no_entry()
    {
        var before = host.ActivityLog.Entries.Count;
        host.UnitOfWork.FailNextSaveWith = AppError.Conflict("The record changed.");

        ResultAssert.Failed(await CreateCompany(), ErrorType.Conflict);

        Assert.Equal(before, host.ActivityLog.Entries.Count);
    }

    [Fact]
    public async Task A_rejected_request_leaves_no_entry()
    {
        await host.CreateCompanyAsync();
        var before = host.ActivityLog.Entries.Count;

        ResultAssert.FailedOnField(await CreateCompany(), "code");

        Assert.Equal(before, host.ActivityLog.Entries.Count);
    }

    [Fact]
    public async Task The_entry_carries_the_actor_and_the_time_from_the_clock()
    {
        host.User.UserId = 7;
        host.User.DisplayName = "Aye Test";

        await host.CreateCompanyAsync();

        var entry = host.ActivityLog.Entries[^1];
        Assert.Equal(7, entry.ActorUserId);
        Assert.Equal("Aye Test", entry.ActorName);
        Assert.Equal(host.Clock.UtcNow.UtcDateTime, entry.OccurredAtUtc);
    }

    [Fact]
    public async Task Every_message_stays_within_the_column_and_names_no_personal_data()
    {
        var org = await host.SeedOrgAsync();
        await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org));

        Assert.All(host.ActivityLog.Entries, entry =>
        {
            Assert.True(entry.Message.Length <= ActivityLogEntry.MessageMaxLength);
            Assert.DoesNotContain("NRC-TEST", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("1234567890", entry.Message, StringComparison.Ordinal);
        });
    }
}
