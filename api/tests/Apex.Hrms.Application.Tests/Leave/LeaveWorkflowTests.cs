using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Leave.Applications;
using Apex.Hrms.Application.Leave.LeaveTypes;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Leave;

/// <summary>
/// The transition table of a leave application (Draft, Submitted, Approved, Rejected, Cancelled), who may decide, the
/// balance and document checks of submitting, and cancelling. Today is Monday 2026-11-09 in the fake clock.
/// </summary>
public sealed class LeaveWorkflowTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private static DateOnly D(int month, int day) => new(2026, month, day);

    private async Task<(OrgSeed Org, EmployeeDto Employee, LeaveTypeDto Type)> SeedAsync(decimal days = 15m, bool requiresDocument = false)
    {
        var org = await host.SeedOrgAsync();
        var employee = await host.CreateLeaveEmployeeAsync(org, "mia@example.test");
        var type = await host.CreateTypeAsync(LeaveFixtures.NewType(days: days, requiresDocument: requiresDocument));
        return (org, employee, type);
    }

    private async Task<LeaveApplicationDto> DraftAsync(EmployeeDto employee, LeaveTypeDto type, int startDay = 16, int? endDay = null) =>
        await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, startDay), D(11, endDay ?? startDay)));

    // ---- Submit ----

    [Fact]
    public async Task Submitting_a_draft_moves_it_to_submitted_and_sets_the_application_date()
    {
        var (_, employee, type) = await SeedAsync();
        var draft = await DraftAsync(employee, type);
        host.Clock.Today = D(11, 10);

        var submitted = ResultAssert.Succeeded(await host.Submit(draft));

        Assert.Equal("Submitted", submitted.Status);
        Assert.Equal(D(11, 10), submitted.AppliedOn);
        Assert.NotEqual(draft.RowVersion, submitted.RowVersion);
    }

    [Theory]
    [InlineData("Submitted")]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    [InlineData("Cancelled")]
    public async Task Only_a_draft_can_be_submitted(string status)
    {
        var (_, employee, type) = await SeedAsync();
        var current = await MoveToAsync(await DraftAsync(employee, type), status);

        var result = await host.Submit(current);

        ResultAssert.Failed(result, ErrorType.Conflict, $"Only a draft can be submitted (this one is {status}).");
    }

    [Fact]
    public async Task A_type_that_needs_a_document_cannot_be_submitted_without_one()
    {
        var (_, employee, type) = await SeedAsync(requiresDocument: true);
        var withoutDocument = await DraftAsync(employee, type);
        var withDocument = await host.CreateApplicationAsync(
            LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 23), documentName: "certificate.pdf"));

        var refused = await host.Submit(withoutDocument);
        var accepted = await host.Submit(withDocument);

        var error = ResultAssert.FailedOnField(refused, "documentName", "Annual Leave needs a supporting document before it can be submitted.");
        Assert.Equal("A supporting document is required.", Assert.Single(error.FieldErrors!["documentName"]));
        Assert.Equal("Submitted", ResultAssert.Succeeded(accepted).Status);
    }

    [Fact]
    public async Task Submitting_is_refused_when_the_balance_is_too_small()
    {
        var (_, employee, type) = await SeedAsync(days: 2m);
        var draft = await DraftAsync(employee, type, 16, 18);

        var result = await host.Submit(draft);

        ResultAssert.Failed(result, ErrorType.Conflict, "Insufficient Annual Leave balance: 3 day(s) requested, 2 available.");
    }

    [Fact]
    public async Task Pending_and_approved_days_are_taken_off_the_balance()
    {
        var (_, employee, type) = await SeedAsync(days: 5m);
        var first = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), D(11, 18)));
        ResultAssert.Succeeded(await host.Approve(first));
        var second = await DraftAsync(employee, type, 19, 20);
        var third = await DraftAsync(employee, type, 23);

        Assert.True((await host.Submit(second)).IsSuccess);
        var refused = await host.Submit(third);

        ResultAssert.Failed(refused, ErrorType.Conflict, "Insufficient Annual Leave balance: 1 day(s) requested, 0 available.");
    }

    [Fact]
    public async Task A_half_day_needs_half_a_day_of_balance()
    {
        var (_, employee, type) = await SeedAsync(days: 0.5m);
        var half = await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), halfDay: true));
        var full = await DraftAsync(employee, type, 17);

        Assert.True((await host.Submit(half)).IsSuccess);
        ResultAssert.Failed(
            await host.Submit(full),
            ErrorType.Conflict,
            "Insufficient Annual Leave balance: 1 day(s) requested, 0 available.");
    }

    [Fact]
    public async Task A_cancelled_application_gives_its_days_back()
    {
        var (_, employee, type) = await SeedAsync(days: 2m);
        var first = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), D(11, 17)));
        var second = await DraftAsync(employee, type, 18);
        ResultAssert.Failed(await host.Submit(second), ErrorType.Conflict);

        ResultAssert.Succeeded(await host.Cancel(first));

        var fresh = ResultAssert.Succeeded(await host.GetApplication(second.Id));
        Assert.True((await host.Submit(fresh)).IsSuccess);
    }

    [Fact]
    public async Task The_balance_of_the_year_the_leave_starts_in_is_the_one_that_counts()
    {
        var (_, employee, type) = await SeedAsync(days: 2m);
        var january = await host.CreateApplicationAsync(
            LeaveFixtures.NewApplication(employee.Id, type.Id, new DateOnly(2027, 1, 4), new DateOnly(2027, 1, 6)));

        var result = await host.Submit(january);

        ResultAssert.Failed(result, ErrorType.Conflict, "Insufficient Annual Leave balance: 3 day(s) requested, 2 available.");
    }

    // ---- Approve and reject ----

    [Fact]
    public async Task The_built_in_administrator_may_approve_and_is_recorded_without_an_employee()
    {
        var (_, employee, type) = await SeedAsync();
        var submitted = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16)));
        host.User.EmployeeId = null;
        host.User.UserId = 4;
        host.User.DisplayName = "Fictional Administrator";

        var approved = ResultAssert.Succeeded(await host.Approve(submitted));

        Assert.Equal("Approved", approved.Status);
        Assert.Null(approved.ApproverId);
        Assert.Equal("Fictional Administrator", approved.ActionedByName);
        Assert.Equal(D(11, 9), approved.ActionedOn);
        Assert.Equal("Approved.", approved.ApproverRemarks);
    }

    [Fact]
    public async Task Another_employee_may_approve_with_remarks_and_is_recorded_as_the_approver()
    {
        var (org, employee, type) = await SeedAsync();
        var manager = await host.CreateLeaveEmployeeAsync(org, "ben@example.test", "Male");
        var submitted = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16)));
        host.User.EmployeeId = manager.Id;

        var approved = ResultAssert.Succeeded(await host.Approve(submitted, "  Enjoy the break  "));

        Assert.Equal(manager.Id, approved.ApproverId);
        Assert.Equal("Enjoy the break", approved.ApproverRemarks);
    }

    [Fact]
    public async Task Nobody_approves_their_own_leave()
    {
        var (_, employee, type) = await SeedAsync();
        var submitted = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16)));
        host.User.EmployeeId = employee.Id;

        var result = await host.Approve(submitted);

        ResultAssert.Failed(result, ErrorType.Conflict, "You cannot decide your own leave request.");
        Assert.Equal("Submitted", ResultAssert.Succeeded(await host.GetApplication(submitted.Id)).Status);
    }

    [Fact]
    public async Task Nobody_rejects_their_own_leave()
    {
        var (_, employee, type) = await SeedAsync();
        var submitted = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16)));
        host.User.EmployeeId = employee.Id;

        var result = await host.Reject(submitted, "Because");

        ResultAssert.Failed(result, ErrorType.Conflict, "You cannot decide your own leave request.");
    }

    [Fact]
    public async Task A_rejection_needs_a_reason()
    {
        var (_, employee, type) = await SeedAsync();
        var submitted = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16)));

        var refused = await host.Reject(submitted, "   ");
        var missing = await host.Reject(submitted, null);
        var rejected = ResultAssert.Succeeded(await host.Reject(submitted, "Team is short that week"));

        var error = ResultAssert.FailedOnField(refused, "remarks", "Give a reason for rejecting the request.");
        Assert.Equal("A reason is required.", Assert.Single(error.FieldErrors!["remarks"]));
        ResultAssert.FailedOnField(missing, "remarks");
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("Team is short that week", rejected.ApproverRemarks);
        Assert.Equal(D(11, 9), rejected.ActionedOn);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    [InlineData("Cancelled")]
    public async Task Only_submitted_leave_can_be_decided(string status)
    {
        var (_, employee, type) = await SeedAsync();
        var current = await MoveToAsync(await DraftAsync(employee, type), status);
        var message = $"Only submitted applications can be decided (this one is {status}).";

        ResultAssert.Failed(await host.Approve(current), ErrorType.Conflict, message);
        ResultAssert.Failed(await host.Reject(current), ErrorType.Conflict, message);
    }

    [Fact]
    public async Task Remarks_are_limited_to_250_characters()
    {
        var (_, employee, type) = await SeedAsync();
        var submitted = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16)));

        var result = await host.Approve(submitted, new string('x', 251));

        ResultAssert.FailedOnField(result, "remarks");
        Assert.True((await host.Approve(submitted, new string('x', 250))).IsSuccess);
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_decide_or_cancel()
    {
        var (_, employee, type) = await SeedAsync();
        var submitted = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16)));
        host.User.UserId = null;
        host.User.IsAuthenticated = false;

        ResultAssert.Failed(await host.Approve(submitted), ErrorType.Unauthorized);
        ResultAssert.Failed(await host.Reject(submitted), ErrorType.Unauthorized);
        ResultAssert.Failed(await host.Cancel(submitted), ErrorType.Unauthorized);
    }

    [Fact]
    public async Task A_command_needs_a_current_row_version()
    {
        var (_, employee, type) = await SeedAsync();
        var submitted = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16)));
        var stale = submitted with { RowVersion = BitConverter.GetBytes(12345L) };

        ResultAssert.Failed(await host.Approve(stale), ErrorType.Conflict);
        ResultAssert.FailedOnField(
            await host.Command<ApproveLeaveCommand, LeaveApplicationDto>(new ApproveLeaveCommand(submitted.Id, null, null)),
            "rowVersion");
        ResultAssert.Failed(
            await host.Command<ApproveLeaveCommand, LeaveApplicationDto>(new ApproveLeaveCommand(999, Requests.Version(submitted.RowVersion), null)),
            ErrorType.NotFound);
    }

    // ---- Cancel ----

    [Fact]
    public async Task Submitted_leave_can_be_cancelled_with_default_remarks()
    {
        var (_, employee, type) = await SeedAsync();
        var submitted = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16)));
        host.User.DisplayName = "Fictional Person";

        var cancelled = ResultAssert.Succeeded(await host.Cancel(submitted));

        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Cancelled.", cancelled.ApproverRemarks);
        Assert.Equal(D(11, 9), cancelled.ActionedOn);
        Assert.Equal("Fictional Person", cancelled.ActionedByName);
        Assert.Null(cancelled.ApproverId);
    }

    [Fact]
    public async Task Approved_leave_that_has_not_started_can_be_cancelled_with_the_given_remarks()
    {
        var (_, employee, type) = await SeedAsync();
        var approved = await MoveToAsync(await DraftAsync(employee, type), "Approved");

        var cancelled = ResultAssert.Succeeded(await host.Cancel(approved, "Plans changed"));

        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Plans changed", cancelled.ApproverRemarks);
    }

    [Fact]
    public async Task Approved_leave_that_has_started_cannot_be_cancelled()
    {
        var (_, employee, type) = await SeedAsync();
        var approved = await MoveToAsync(await DraftAsync(employee, type), "Approved");
        host.Clock.Today = D(11, 16);

        var result = await host.Cancel(approved);

        ResultAssert.Failed(result, ErrorType.Conflict, "Leave that has already started cannot be cancelled.");
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Rejected")]
    [InlineData("Cancelled")]
    public async Task Only_submitted_or_approved_leave_can_be_cancelled(string status)
    {
        var (_, employee, type) = await SeedAsync();
        var current = await MoveToAsync(await DraftAsync(employee, type), status);

        var result = await host.Cancel(current);

        ResultAssert.Failed(
            result,
            ErrorType.Conflict,
            $"Only submitted or approved leave can be cancelled (this one is {status}).");
    }

    [Fact]
    public async Task A_decided_application_cannot_be_changed_back()
    {
        var (_, employee, type) = await SeedAsync();
        var rejected = await MoveToAsync(await DraftAsync(employee, type), "Rejected");

        var update = await host.Get<IUpdateHandler<LeaveApplicationUpdateRequest, LeaveApplicationDto>>()
            .HandleAsync(rejected.Id, LeaveFixtures.ApplicationUpdateOf(rejected), default);

        ResultAssert.Failed(update, ErrorType.Conflict);
        ResultAssert.Failed(await host.Submit(rejected), ErrorType.Conflict);
    }

    /// <summary>Walks a draft through the workflow to the given status with the real commands.</summary>
    private async Task<LeaveApplicationDto> MoveToAsync(LeaveApplicationDto draft, string status)
    {
        if (status == "Draft")
        {
            return draft;
        }

        var submitted = ResultAssert.Succeeded(await host.Submit(draft));
        return status switch
        {
            "Submitted" => submitted,
            "Approved" => ResultAssert.Succeeded(await host.Approve(submitted)),
            "Rejected" => ResultAssert.Succeeded(await host.Reject(submitted)),
            "Cancelled" => ResultAssert.Succeeded(await host.Cancel(submitted)),
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
    }
}
