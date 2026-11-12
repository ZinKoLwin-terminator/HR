using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Common;
using static Apex.Hrms.Application.Tests.Fakes.AttendanceFixtures;

namespace Apex.Hrms.Application.Tests.Attendance;

/// <summary>An account with no linked employee punches for others only when its role is in the "All" attendance tier.</summary>
public sealed class PunchAccountWithoutEmployeeTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private async Task<int> SeedWorkerIdAsync()
    {
        var org = await host.SeedOrgAsync();
        var shift = await host.CreateShiftAsync();
        return (await host.CreateWorkerAsync(org, "mia@example.test", shift.Id)).Id;
    }

    [Fact]
    public async Task A_caller_with_no_employee_and_only_the_employee_grants_cannot_punch_for_someone_else()
    {
        var workerId = await SeedWorkerIdAsync();
        host.User.EmployeeId = null;
        host.Permissions.Only("attendance:View", "attendance:Create");

        var result = await host.Punch(workerId, "CheckIn");

        ResultAssert.Failed(result, ErrorType.Forbidden);
    }

    [Fact]
    public async Task A_caller_with_no_employee_in_the_manager_tier_cannot_punch_for_someone_else()
    {
        var workerId = await SeedWorkerIdAsync();
        host.User.EmployeeId = null;
        host.Permissions.Only("attendance:View", "attendance:Create", "attendance:Approve");

        ResultAssert.Failed(await host.Punch(workerId, "CheckIn"), ErrorType.Forbidden);
    }

    [Fact]
    public async Task A_caller_with_no_employee_in_the_all_tier_may_punch_for_anyone()
    {
        var workerId = await SeedWorkerIdAsync();
        host.User.EmployeeId = null;
        host.Permissions.Only("attendance:Create", "attendance:Approve", "employees:Edit");

        var record = await host.PunchAsync(workerId, "CheckIn", 9, 0);

        Assert.Equal(workerId, record.EmployeeId);
    }

    [Fact]
    public async Task A_linked_employee_still_punches_only_for_themselves()
    {
        var workerId = await SeedWorkerIdAsync();
        host.User.EmployeeId = workerId + 1000;

        ResultAssert.Failed(await host.Punch(workerId, "CheckIn"), ErrorType.Forbidden);
    }
}
