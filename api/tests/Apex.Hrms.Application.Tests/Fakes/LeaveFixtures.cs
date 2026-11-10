using System.Reflection;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Leave.Applications;
using Apex.Hrms.Application.Leave.Balances;
using Apex.Hrms.Application.Leave.Calendar;
using Apex.Hrms.Application.Leave.LeavePolicies;
using Apex.Hrms.Application.Leave.LeaveTypes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Fakes;

/// <summary>
/// Fictional leave requests and the handler calls the leave tests share. The fake clock says today is Monday
/// 2026-11-09; the employees of <see cref="EmployeeFixtures"/> joined on 2024-03-01.
/// </summary>
internal static class LeaveFixtures
{
    public static readonly DateOnly Monday = new(2026, 11, 16);

    public static LeaveTypeCreateRequest NewType(
        string code = "AL",
        string name = "Annual Leave",
        decimal days = 15m,
        short maxConsecutive = 10,
        bool requiresDocument = false,
        string? gender = null,
        string status = "Active") => new(
        code,
        name,
        Description: null,
        days,
        IsPaid: true,
        requiresDocument,
        maxConsecutive,
        CarryForward: false,
        MaxCarryForwardDays: 0m,
        "#3f6fd8",
        gender,
        status);

    public static LeaveTypeUpdateRequest TypeUpdateOf(LeaveTypeDto current) => new(
        current.Code,
        current.Name,
        current.Description,
        current.DefaultDaysPerYear,
        current.IsPaid,
        current.RequiresDocument,
        current.MaxConsecutiveDays,
        current.CarryForward,
        current.MaxCarryForwardDays,
        current.Color,
        current.EligibleGender,
        Requests.Version(current.RowVersion));

    public static LeavePolicyCreateRequest NewPolicy(
        int leaveTypeId,
        string name = "Annual leave standard",
        int? jobGradeId = null,
        string? employmentType = null,
        decimal days = 15m,
        string accrual = "Upfront",
        short minServiceMonths = 0,
        string status = "Active") => new(name, leaveTypeId, jobGradeId, employmentType, days, accrual, minServiceMonths, status);

    public static LeavePolicyUpdateRequest PolicyUpdateOf(LeavePolicyDto current, decimal? days = null, int? jobGradeId = null) => new(
        current.Name,
        current.LeaveTypeId,
        jobGradeId ?? current.JobGradeId,
        current.EmploymentType,
        days ?? current.DaysPerYear,
        current.Accrual,
        current.MinServiceMonths,
        Requests.Version(current.RowVersion));

    public static LeaveApplicationCreateRequest NewApplication(
        int employeeId,
        int leaveTypeId,
        DateOnly? start = null,
        DateOnly? end = null,
        bool halfDay = false,
        string? documentName = null) => new(
        employeeId,
        leaveTypeId,
        start ?? Monday,
        end ?? start ?? Monday,
        halfDay,
        "Family matter",
        documentName);

    public static LeaveApplicationUpdateRequest ApplicationUpdateOf(LeaveApplicationDto current, DateOnly? end = null) => new(
        current.EmployeeId,
        current.LeaveTypeId,
        current.StartDate,
        end ?? current.EndDate,
        current.HalfDay,
        current.Reason,
        current.DocumentName,
        Requests.Version(current.RowVersion));

    public static async Task<LeaveTypeDto> CreateTypeAsync(this TestHost host, LeaveTypeCreateRequest? request = null) =>
        ResultAssert.Succeeded(await host.Get<ICreateHandler<LeaveTypeCreateRequest, LeaveTypeDto>>().HandleAsync(request ?? NewType(), default));

    public static Task<Result<LeaveTypeDto>> CreateType(this TestHost host, LeaveTypeCreateRequest request) =>
        host.Get<ICreateHandler<LeaveTypeCreateRequest, LeaveTypeDto>>().HandleAsync(request, default);

    public static async Task<LeavePolicyDto> CreatePolicyAsync(this TestHost host, LeavePolicyCreateRequest request) =>
        ResultAssert.Succeeded(await host.CreatePolicy(request));

    public static Task<Result<LeavePolicyDto>> CreatePolicy(this TestHost host, LeavePolicyCreateRequest request) =>
        host.Get<ICreateHandler<LeavePolicyCreateRequest, LeavePolicyDto>>().HandleAsync(request, default);

    /// <summary>A new employee (the fictional one of the employee tests) of the given gender who joined on <paramref name="joined"/>.</summary>
    public static async Task<EmployeeDto> CreateLeaveEmployeeAsync(
        this TestHost host,
        OrgSeed org,
        string email,
        string gender = "Female",
        DateOnly? joined = null,
        string employmentType = "Full-time",
        int? jobGradeId = null) =>
        await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, email: email) with
        {
            Gender = gender,
            JoiningDate = joined ?? new DateOnly(2024, 3, 1),
            EmploymentType = employmentType,
            JobGradeId = jobGradeId ?? org.Grade.Id,
        });

    public static Task<Result<LeaveApplicationDto>> CreateApplication(this TestHost host, LeaveApplicationCreateRequest request) =>
        host.Get<ICreateHandler<LeaveApplicationCreateRequest, LeaveApplicationDto>>().HandleAsync(request, default);

    public static async Task<LeaveApplicationDto> CreateApplicationAsync(this TestHost host, LeaveApplicationCreateRequest request) =>
        ResultAssert.Succeeded(await host.CreateApplication(request));

    public static Task<Result<LeaveApplicationDto>> Submit(this TestHost host, LeaveApplicationDto application) =>
        host.Command<SubmitLeaveCommand, LeaveApplicationDto>(new SubmitLeaveCommand(application.Id, Requests.Version(application.RowVersion)));

    public static Task<Result<LeaveApplicationDto>> Approve(this TestHost host, LeaveApplicationDto application, string? remarks = null) =>
        host.Command<ApproveLeaveCommand, LeaveApplicationDto>(new ApproveLeaveCommand(application.Id, Requests.Version(application.RowVersion), remarks));

    public static Task<Result<LeaveApplicationDto>> Reject(this TestHost host, LeaveApplicationDto application, string? remarks = "Not possible") =>
        host.Command<RejectLeaveCommand, LeaveApplicationDto>(new RejectLeaveCommand(application.Id, Requests.Version(application.RowVersion), remarks));

    public static Task<Result<LeaveApplicationDto>> Cancel(this TestHost host, LeaveApplicationDto application, string? remarks = null) =>
        host.Command<CancelLeaveCommand, LeaveApplicationDto>(new CancelLeaveCommand(application.Id, Requests.Version(application.RowVersion), remarks));

    public static Task<Result<LeaveApplicationDto>> GetApplication(this TestHost host, int id) =>
        host.Get<IGetByIdHandler<LeaveApplicationDto>>().HandleAsync(id, default);

    /// <summary>Creates a draft and submits it, failing the test when either step is refused.</summary>
    public static async Task<LeaveApplicationDto> SubmittedAsync(this TestHost host, LeaveApplicationCreateRequest request) =>
        ResultAssert.Succeeded(await host.Submit(await host.CreateApplicationAsync(request)));

    public static Task<Result<IReadOnlyList<LeaveBalanceDto>>> Balances(this TestHost host, int employeeId, int? year = null) =>
        host.Get<ILeaveBalanceHandler>().HandleAsync(employeeId, year, default);

    public static Task<Result<LeaveCalendarDto>> Calendar(this TestHost host, LeaveCalendarRequest request) =>
        host.Get<ILeaveCalendarHandler>().HandleAsync(request, default);

    /// <summary>Adds a holiday. The domain class has no public constructor (it is read-only here), so reflection builds it.</summary>
    public static void AddHoliday(this TestHost host, DateOnly date, string name)
    {
        var holiday = (Holiday)Activator.CreateInstance(typeof(Holiday), nonPublic: true)!;
        typeof(Holiday).GetProperty(nameof(Holiday.Date))!.SetValue(holiday, date);
        typeof(Holiday).GetProperty(nameof(Holiday.Name))!.SetValue(holiday, name);
        typeof(Holiday).GetProperty(nameof(Holiday.Type), BindingFlags.Public | BindingFlags.Instance)!.SetValue(holiday, "Public");
        host.Repository<Holiday>().Add(holiday);
    }
}
